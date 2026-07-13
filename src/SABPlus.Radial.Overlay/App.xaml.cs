using SABPlus.Radial.Core.Models;
using SABPlus.Radial.Core.Services;
using SABPlus.Radial.Overlay.Services;
using SABPlus.Radial.Overlay.UI.Views;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace SABPlus.Radial.Overlay
{
    public partial class App : Application
    {
        private Mutex _singleInstanceMutex;
        private WheelSettingsRepository _repository;
        private BridgeClientService _bridgeClient;
        private RevitBridgeDiscoveryService _discoveryService;
        private ProjectWheelStateService _projectStateService;
        private GlobalInputHookService _inputHook;
        private OverlayController _overlayController;
        private SingleInstanceCommandServer _instanceServer;
        private TrayIconService _trayIcon;
        private WheelSettingsWindow _settingsWindow;
        private bool _isHandlingDispatcherException;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            DispatcherUnhandledException += App_DispatcherUnhandledException;

            bool createdNew;
            _singleInstanceMutex = new Mutex(true, PipeNameFactory.GetOverlayMutexName(), out createdNew);
            if (!createdNew)
            {
                bool forwarded = SingleInstanceCommandServer.TrySend(e.Args);
                if (!forwarded && e.Args.Any(item => string.Equals(item, "--settings", StringComparison.OrdinalIgnoreCase)))
                {
                    MessageBox.Show(
                        "Overlay уже запущен, но не ответил на запрос открытия настроек.\n\n" +
                        "Закройте SAB+ через значок в системном трее и повторите запуск из Revit.",
                        "SAB+ Радиальное колесо",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
                Shutdown(0);
                return;
            }

            try
            {
                string settingsDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "SAB+",
                    "RadialWheel");

                _repository = new WheelSettingsRepository(settingsDirectory);
                WheelSettings settings = _repository.LoadOrCreateDefault();

                _bridgeClient = new BridgeClientService();
                _discoveryService = new RevitBridgeDiscoveryService(_bridgeClient);
                _projectStateService = new ProjectWheelStateService(_repository);
                _inputHook = new GlobalInputHookService(
                    settings.StageTrigger,
                    settings.CommandTrigger);
                _overlayController = new OverlayController(
                    _repository,
                    _projectStateService,
                    _discoveryService,
                    _bridgeClient,
                    _inputHook);

                _trayIcon = new TrayIconService();
                _trayIcon.OpenSettingsRequested += TrayIcon_OpenSettingsRequested;
                _trayIcon.ExitRequested += TrayIcon_ExitRequested;
                _overlayController.StatusMessage += OverlayController_StatusMessage;

                _instanceServer = new SingleInstanceCommandServer(HandleForwardedArguments);
                _instanceServer.Start();
                _discoveryService.Start();
                _inputHook.Start();

                Dispatcher.BeginInvoke(new Action(() => ProcessArguments(e.Args)));
            }
            catch (Exception exception)
            {
                MessageBox.Show(
                    "Не удалось запустить радиальное колесо.\n\n" + exception,
                    "SAB+ Радиальное колесо",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Shutdown(1);
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            DispatcherUnhandledException -= App_DispatcherUnhandledException;
            if (_trayIcon != null)
            {
                _trayIcon.OpenSettingsRequested -= TrayIcon_OpenSettingsRequested;
                _trayIcon.ExitRequested -= TrayIcon_ExitRequested;
            }

            if (_overlayController != null)
            {
                _overlayController.StatusMessage -= OverlayController_StatusMessage;
            }

            _settingsWindow?.Close();
            _overlayController?.Dispose();
            _inputHook?.Dispose();
            _discoveryService?.Dispose();
            _instanceServer?.Dispose();
            _trayIcon?.Dispose();

            if (_singleInstanceMutex != null)
            {
                try
                {
                    _singleInstanceMutex.ReleaseMutex();
                }
                catch
                {
                    // The mutex may already be released during failed startup.
                }

                _singleInstanceMutex.Dispose();
                _singleInstanceMutex = null;
            }

            base.OnExit(e);
        }

        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            e.Handled = true;
            if (_isHandlingDispatcherException)
            {
                return;
            }

            _isHandlingDispatcherException = true;
            string logPath = WriteUiErrorLog(e.Exception);
            TryShowUiDebugInRevit(e.Exception);

            string message =
                "Необработанная ошибка интерфейса радиального колеса.\n\n" +
                "Тип: " + e.Exception.GetType().FullName + "\n" +
                "Сообщение: " + e.Exception.Message;
            if (!string.IsNullOrWhiteSpace(logPath))
            {
                message += "\n\nДиагностический журнал:\n" + logPath;
            }

            MessageBox.Show(
                message,
                "SAB+ Радиальное колесо",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            Dispatcher.BeginInvoke(new Action(() => Shutdown(1)));
        }

        private string WriteUiErrorLog(Exception exception)
        {
            try
            {
                string directory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "SAB+",
                    "RadialWheel",
                    "Logs");
                Directory.CreateDirectory(directory);
                string path = Path.Combine(directory, "ui-errors.log");
                string entry =
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + Environment.NewLine +
                    exception + Environment.NewLine +
                    new string('-', 80) + Environment.NewLine;
                File.AppendAllText(path, entry);
                return path;
            }
            catch
            {
                return string.Empty;
            }
        }

        private async void TryShowUiDebugInRevit(Exception exception)
        {
            try
            {
                RevitContextSnapshot context = _discoveryService?.GetContexts().FirstOrDefault();
                if (context == null || _bridgeClient == null)
                {
                    return;
                }

                string debugMessage =
                    "Шаг 1. Получено DispatcherUnhandledException.\n" +
                    "Шаг 2. Тип: " + exception.GetType().FullName + ".\n" +
                    "Шаг 3. Сообщение: " + exception.Message + "\n" +
                    "Шаг 4. Повторный показ ошибки заблокирован; Overlay будет завершён.";
                await _bridgeClient.SendAsync(
                    context.ProcessId,
                    new BridgeRequest
                    {
                        RequestType = BridgeRequestTypes.DebugWheelState,
                        DebugMessage = debugMessage
                    },
                    400);
            }
            catch
            {
                // The local WPF error dialog remains the fallback when Revit is unavailable.
            }
        }

        private void HandleForwardedArguments(string[] arguments)
        {
            Dispatcher.BeginInvoke(new Action(() => ProcessArguments(arguments)));
        }

        private void ProcessArguments(string[] arguments)
        {
            string[] safeArguments = arguments ?? new string[0];
            if (safeArguments.Any(item => string.Equals(item, "--settings", StringComparison.OrdinalIgnoreCase)))
            {
                OpenSettingsWindow(ReadProcessId(safeArguments));
            }
        }

        private void TrayIcon_OpenSettingsRequested(object sender, EventArgs e)
        {
            RevitContextSnapshot context = _discoveryService.GetContexts().FirstOrDefault();
            OpenSettingsWindow(context?.ProcessId ?? 0);
        }

        private void TrayIcon_ExitRequested(object sender, EventArgs e)
        {
            Shutdown(0);
        }

        private void OverlayController_StatusMessage(object sender, string message)
        {
            if (_settingsWindow != null && _settingsWindow.IsLoaded)
            {
                _settingsWindow.ViewModel.SetStatus(message);
                return;
            }

            if (ContainsErrorSignal(message))
            {
                _trayIcon.ShowError(message);
            }
        }

        private void OpenSettingsWindow(int preferredRevitProcessId)
        {
            if (_settingsWindow != null && _settingsWindow.IsLoaded)
            {
                _settingsWindow.SelectRevitProcess(preferredRevitProcessId);
                if (_settingsWindow.WindowState == WindowState.Minimized)
                {
                    _settingsWindow.WindowState = WindowState.Normal;
                }

                _settingsWindow.Show();
                _settingsWindow.Activate();
                return;
            }

            _settingsWindow = new WheelSettingsWindow(
                _repository,
                _overlayController,
                _discoveryService,
                _bridgeClient,
                preferredRevitProcessId);
            _settingsWindow.Closed += SettingsWindow_Closed;
            _settingsWindow.Show();
            _settingsWindow.Activate();
        }

        private void SettingsWindow_Closed(object sender, EventArgs e)
        {
            if (_settingsWindow != null)
            {
                _settingsWindow.Closed -= SettingsWindow_Closed;
                _settingsWindow = null;
            }
        }

        private static int ReadProcessId(string[] arguments)
        {
            for (int index = 0; index < arguments.Length - 1; index++)
            {
                if (string.Equals(arguments[index], "--revit-process", StringComparison.OrdinalIgnoreCase))
                {
                    int processId;
                    if (int.TryParse(arguments[index + 1], out processId))
                    {
                        return processId;
                    }
                }
            }

            return 0;
        }

        private static bool ContainsErrorSignal(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return false;
            }

            return message.IndexOf("ошиб", StringComparison.CurrentCultureIgnoreCase) >= 0 ||
                   message.IndexOf("не удалось", StringComparison.CurrentCultureIgnoreCase) >= 0 ||
                   message.IndexOf("не найден", StringComparison.CurrentCultureIgnoreCase) >= 0 ||
                   message.IndexOf("недоступ", StringComparison.CurrentCultureIgnoreCase) >= 0;
        }
    }
}
