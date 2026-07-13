using SABPlus.Radial.Setup.Services;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace SABPlus.Radial.Setup
{
    public partial class MainWindow : Window
    {
        private readonly InstallerService _installerService;
        private readonly SetupDiagnosticsService _diagnosticsService;
        private readonly int _uiThreadId;

        public MainWindow()
        {
            InitializeComponent();
            _installerService = new InstallerService();
            _diagnosticsService = new SetupDiagnosticsService();
            _uiThreadId = Thread.CurrentThread.ManagedThreadId;
            InstallPathText.Text = _installerService.InstallDirectory;
            UninstallButton.IsEnabled = _installerService.IsInstalled;
        }

        public void BeginUninstallFromCommandLine()
        {
            Dispatcher.BeginInvoke(new Action(async () => await UninstallAsync(false)));
        }

        private async void InstallButton_Click(object sender, RoutedEventArgs e)
        {
            // Read every WPF-owned value on the UI thread before starting background work.
            bool installRevit2023 = Revit2023CheckBox.IsChecked == true;
            bool installRevit2024 = Revit2024CheckBox.IsChecked == true;
            bool launchOverlay = LaunchOverlayCheckBox.IsChecked == true;
            string currentStep = "Чтение параметров интерфейса";

            if (!installRevit2023 && !installRevit2024)
            {
                MessageBox.Show(this, "Выберите хотя бы одну версию Revit.", "SAB+", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _diagnosticsService.WriteStep(
                "Шаг 1. Параметры прочитаны в UI-потоке",
                "UI thread=" + _uiThreadId +
                "; Revit2023=" + installRevit2023 +
                "; Revit2024=" + installRevit2024 +
                "; LaunchOverlay=" + launchOverlay + ".");
            SetBusy(true, "Установка компонентов SAB+…");
            try
            {
                currentStep = "Копирование файлов и создание подключений Revit";
                await Task.Run(() =>
                {
                    _diagnosticsService.WriteStep(
                        "Шаг 2. Фоновая установка запущена",
                        "Получены обычные bool; WPF-элементы в фоновом потоке не читаются.");
                    _installerService.Install(installRevit2023, installRevit2024);
                    _diagnosticsService.WriteStep(
                        "Шаг 3. Фоновая установка завершена",
                        "Файлы и add-in-манифесты установлены.");
                });

                currentStep = "Обновление окна после установки";
                StatusText.Text = "Установка завершена. Перезапустите Revit, если он был открыт.";
                UninstallButton.IsEnabled = true;

                if (launchOverlay)
                {
                    currentStep = "Запуск SAB+ Overlay";
                    _installerService.LaunchOverlay();
                }

                _diagnosticsService.WriteStep(
                    "Шаг 4. Установка успешно завершена",
                    "Возврат в UI thread=" + Thread.CurrentThread.ManagedThreadId + ".");
            }
            catch (Exception exception)
            {
                _diagnosticsService.WriteException("Ошибка. Этап: " + currentStep, exception);
                StatusText.Text = "Установка не завершена.";
                MessageBox.Show(
                    this,
                    "Установка остановлена.\n\n" +
                    "Этап: " + currentStep + "\n" +
                    "UI thread: " + _uiThreadId + "\n" +
                    "Текущий thread: " + Thread.CurrentThread.ManagedThreadId + "\n" +
                    "Журнал: " + _diagnosticsService.LogFilePath + "\n\n" +
                    exception,
                    "Ошибка установки SAB+",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                SetBusy(false, StatusText.Text);
            }
        }

        private async void UninstallButton_Click(object sender, RoutedEventArgs e)
        {
            await UninstallAsync(true);
        }

        private async Task UninstallAsync(bool askConfirmation)
        {
            if (askConfirmation)
            {
                MessageBoxResult result = MessageBox.Show(
                    this,
                    "Удалить SAB+ Радиальное колесо и подключения Revit 2023–2024? Пользовательские настройки колеса будут сохранены.",
                    "Удаление SAB+",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                if (result != MessageBoxResult.Yes)
                {
                    return;
                }
            }

            SetBusy(true, "Подготовка удаления…");
            try
            {
                await Task.Run(() => _installerService.BeginSelfUninstall());
                Close();
            }
            catch (Exception exception)
            {
                SetBusy(false, "Удаление не запущено.");
                MessageBox.Show(this, exception.ToString(), "Ошибка удаления SAB+", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void SetBusy(bool isBusy, string status)
        {
            InstallButton.IsEnabled = !isBusy;
            UninstallButton.IsEnabled = !isBusy && _installerService.IsInstalled;
            ProgressBar.IsIndeterminate = isBusy;
            StatusText.Text = status;
        }
    }
}
