using Microsoft.Win32;
using SABPlus.Radial.Core.Geometry;
using SABPlus.Radial.Core.Models;
using SABPlus.Radial.Core.Services;
using SABPlus.Radial.Overlay.Services;
using SABPlus.Radial.Overlay.UI.ViewModels;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Forms = System.Windows.Forms;

namespace SABPlus.Radial.Overlay.UI.Views
{
    public partial class WheelSettingsWindow : Window
    {
        private const string CommandDragFormat = "SABPlus.Radial.Command";
        private const string SlotDragFormat = "SABPlus.Radial.SlotIndex";

        private readonly WheelSettingsRepository _repository;
        private readonly OverlayController _overlayController;
        private readonly RevitBridgeDiscoveryService _discoveryService;
        private readonly BridgeClientService _bridgeClient;
        private readonly WindowLayoutService _layoutService;
        private readonly int _preferredRevitProcessId;

        private bool _capturingTrigger;
        private WheelTriggerAction _capturingTriggerAction;
        private bool _closeWithoutPrompt;
        private Point _wheelDragStart;
        private int _wheelDragSourceIndex;

        public WheelSettingsViewModel ViewModel { get; }

        public WheelSettingsWindow(
            WheelSettingsRepository repository,
            OverlayController overlayController,
            RevitBridgeDiscoveryService discoveryService,
            BridgeClientService bridgeClient,
            int preferredRevitProcessId)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _overlayController = overlayController ?? throw new ArgumentNullException(nameof(overlayController));
            _discoveryService = discoveryService ?? throw new ArgumentNullException(nameof(discoveryService));
            _bridgeClient = bridgeClient ?? throw new ArgumentNullException(nameof(bridgeClient));
            _preferredRevitProcessId = preferredRevitProcessId;
            _wheelDragSourceIndex = -1;

            InitializeComponent();

            string settingsDirectory = Path.GetDirectoryName(_repository.SettingsFilePath);
            _layoutService = new WindowLayoutService(settingsDirectory);
            _layoutService.Restore(this);

            ViewModel = new WheelSettingsViewModel(_overlayController.GetSettingsCopy());
            DataContext = ViewModel;

            for (int count = WheelGeometryCalculator.MinimumCommandSectorCount;
                 count <= WheelGeometryCalculator.MaximumSectorCount;
                 count++)
            {
                SectorCountComboBox.Items.Add(count);
            }

            StageTriggerTypeComboBox.ItemsSource = Enum.GetValues(typeof(WheelTriggerType));
            CommandTriggerTypeComboBox.ItemsSource = Enum.GetValues(typeof(WheelTriggerType));

            AttachRevitOwner(preferredRevitProcessId);
            RefreshContexts();
            RefreshPreview();
            _discoveryService.ContextsChanged += DiscoveryService_ContextsChanged;
        }

        public void SelectRevitProcess(int processId)
        {
            RevitContextSnapshot context = ViewModel.RevitContexts.FirstOrDefault(
                item => item.ProcessId == processId);
            if (context != null)
            {
                ViewModel.SelectedRevitContext = context;
            }
        }

        private void AttachRevitOwner(int processId)
        {
            if (processId <= 0)
            {
                ShowInTaskbar = true;
                WindowStartupLocation = WindowStartupLocation.CenterScreen;
                return;
            }

            try
            {
                using (Process process = Process.GetProcessById(processId))
                {
                    if (process.MainWindowHandle != IntPtr.Zero)
                    {
                        new WindowInteropHelper(this).Owner = process.MainWindowHandle;
                        ShowInTaskbar = false;
                        WindowStartupLocation = WindowStartupLocation.CenterOwner;
                    }
                }
            }
            catch
            {
                ShowInTaskbar = true;
                WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }
        }

        private void RefreshContexts()
        {
            ViewModel.UpdateContexts(_discoveryService.GetContexts(), _preferredRevitProcessId);
        }

        private void RefreshPreview()
        {
            if (EditorWheelControl == null || ViewModel == null)
            {
                return;
            }

            EditorWheelControl.Settings = ViewModel.WorkingSettings;
            EditorWheelControl.DisplayLevel = ViewModel.PreviewLevel;
            EditorWheelControl.ActiveProfile = ViewModel.SelectedProfile;
            var orderedProfiles = ViewModel.Profiles
                .OrderBy(profile => profile.Order)
                .ToList();
            EditorWheelControl.StageProfiles = orderedProfiles;
            EditorWheelControl.ShowStageCapsules = false;
            EditorWheelControl.ReserveExpandedCommandRing = false;
            EditorWheelControl.SelectedStageProfileId = ViewModel.SelectedProfile?.Id ?? string.Empty;
            EditorWheelControl.HitResult = new WheelHitResult(WheelHitKind.Cancel, -1, 0.0, 0.0);

            var stageItems = ViewModel.PreviewLevel == WheelDisplayLevel.Stages
                ? WheelCapsuleMetricsService.CreateStageItems(
                    orderedProfiles,
                    ViewModel.WorkingSettings.Geometry)
                : new System.Collections.Generic.List<WheelCapsuleItemMetrics>();
            var commandItems = ViewModel.PreviewLevel == WheelDisplayLevel.Commands
                ? WheelCapsuleMetricsService.CreateCommandItems(
                    ViewModel.WorkingSettings,
                    ViewModel.SelectedProfile)
                : new System.Collections.Generic.List<WheelCapsuleItemMetrics>();
            double previewSize = WheelGeometryCalculator.GetWindowSize(
                ViewModel.WorkingSettings.Geometry,
                WheelCapsuleMetricsService.GetWidths(stageItems),
                WheelCapsuleMetricsService.GetWidths(commandItems));
            EditorWheelControl.Width = previewSize;
            EditorWheelControl.Height = previewSize;
            ProfileList?.Items.Refresh();
        }

        private void DiscoveryService_ContextsChanged(object sender, EventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(RefreshContexts));
        }

        private void Window_SourceInitialized(object sender, EventArgs e)
        {
            RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // SAB window entrance animation: short and non-blocking.
            Opacity = 0.0;
            BeginAnimation(
                OpacityProperty,
                new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(180))
                {
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                });
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            _discoveryService.ContextsChanged -= DiscoveryService_ContextsChanged;
            _layoutService.Save(this);
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (_closeWithoutPrompt || !ViewModel.IsDirty)
            {
                return;
            }

            MessageBoxResult result = MessageBox.Show(
                this,
                "Сохранить изменения радиального колеса перед закрытием?",
                "SAB+ — Настройка колеса",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Cancel)
            {
                e.Cancel = true;
                return;
            }

            if (result == MessageBoxResult.Yes && !TrySaveSettings(false))
            {
                e.Cancel = true;
            }
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (!_capturingTrigger)
            {
                return;
            }

            Key key = e.Key == Key.System ? e.SystemKey : e.Key;
            if (key == Key.LeftCtrl || key == Key.RightCtrl ||
                key == Key.LeftShift || key == Key.RightShift ||
                key == Key.LeftAlt || key == Key.RightAlt ||
                key == Key.LWin || key == Key.RWin)
            {
                e.Handled = true;
                return;
            }

            WheelTriggerSettings trigger = _capturingTriggerAction == WheelTriggerAction.Stages
                ? ViewModel.WorkingSettings.StageTrigger
                : ViewModel.WorkingSettings.CommandTrigger;
            trigger.Type = WheelTriggerType.Keyboard;
            trigger.VirtualKey = KeyInterop.VirtualKeyFromKey(key);
            trigger.Modifiers = ConvertModifiers(Keyboard.Modifiers);
            _capturingTrigger = false;
            if (_capturingTriggerAction == WheelTriggerAction.Stages)
            {
                StageTriggerTypeComboBox.SelectedItem = WheelTriggerType.Keyboard;
            }
            else
            {
                CommandTriggerTypeComboBox.SelectedItem = WheelTriggerType.Keyboard;
            }
            ViewModel.MarkChanged();
            e.Handled = true;
        }

        private static WheelKeyboardModifiers ConvertModifiers(ModifierKeys modifiers)
        {
            WheelKeyboardModifiers result = WheelKeyboardModifiers.None;
            if (modifiers.HasFlag(ModifierKeys.Control))
            {
                result |= WheelKeyboardModifiers.Control;
            }

            if (modifiers.HasFlag(ModifierKeys.Shift))
            {
                result |= WheelKeyboardModifiers.Shift;
            }

            if (modifiers.HasFlag(ModifierKeys.Alt))
            {
                result |= WheelKeyboardModifiers.Alt;
            }

            if (modifiers.HasFlag(ModifierKeys.Windows))
            {
                result |= WheelKeyboardModifiers.Windows;
            }

            return result;
        }

        private void AddProfile_Click(object sender, RoutedEventArgs e)
        {
            RunEditorAction(ViewModel.AddProfile);
        }

        private void DuplicateProfile_Click(object sender, RoutedEventArgs e)
        {
            RunEditorAction(ViewModel.DuplicateSelectedProfile);
        }

        private void DeleteProfile_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.SelectedProfile == null || ViewModel.Profiles.Count <= 1)
            {
                return;
            }

            MessageBoxResult result = MessageBox.Show(
                this,
                "Удалить стадию «" + ViewModel.SelectedProfile.Name + "»?",
                "Удаление стадии",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                RunEditorAction(ViewModel.DeleteSelectedProfile);
            }
        }

        private void MoveProfileUp_Click(object sender, RoutedEventArgs e)
        {
            RunEditorAction(() => ViewModel.MoveSelectedProfile(-1));
        }

        private void MoveProfileDown_Click(object sender, RoutedEventArgs e)
        {
            RunEditorAction(() => ViewModel.MoveSelectedProfile(1));
        }

        private void ProfileList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            RefreshPreview();
        }

        private void ProfileProperty_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (!IsLoaded || ViewModel?.SelectedProfile == null)
            {
                return;
            }

            ViewModel.MarkChanged();
            RefreshPreview();
        }

        private void ChooseColor_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.SelectedProfile == null)
            {
                return;
            }

            using (Forms.ColorDialog dialog = new Forms.ColorDialog())
            {
                try
                {
                    dialog.Color = System.Drawing.ColorTranslator.FromHtml(ViewModel.SelectedProfile.ColorHex);
                }
                catch
                {
                    dialog.Color = System.Drawing.Color.FromArgb(15, 108, 189);
                }

                if (dialog.ShowDialog() == Forms.DialogResult.OK)
                {
                    ViewModel.SelectedProfile.ColorHex = string.Format(
                        "#{0:X2}{1:X2}{2:X2}",
                        dialog.Color.R,
                        dialog.Color.G,
                        dialog.Color.B);
                    ViewModel.MarkChanged();
                    RefreshPreview();
                }
            }
        }

        private void SectorCountComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (!IsLoaded || SectorCountComboBox.SelectedItem == null)
            {
                return;
            }

            ViewModel.ChangeSectorCount((int)SectorCountComboBox.SelectedItem);
            RefreshPreview();
        }

        private void CapsuleGapSlider_ValueChanged(
            object sender,
            RoutedPropertyChangedEventArgs<double> e)
        {
            if (!IsLoaded || ViewModel == null)
            {
                return;
            }

            ViewModel.WorkingSettings.Geometry.CapsuleGap = e.NewValue;
            ViewModel.MarkChanged();
            RefreshPreview();
        }

        private void GeometrySlider_ValueChanged(
            object sender,
            RoutedPropertyChangedEventArgs<double> e)
        {
            if (!IsLoaded || ViewModel == null)
            {
                return;
            }

            ViewModel.MarkChanged();
            RefreshPreview();
        }

        private void GeometryText_TextChanged(
            object sender,
            System.Windows.Controls.TextChangedEventArgs e)
        {
            if (!IsLoaded || ViewModel == null)
            {
                return;
            }

            ViewModel.MarkChanged();
            RefreshPreview();
        }

        private void ChooseWheelColor_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Controls.Button button = sender as System.Windows.Controls.Button;
            string propertyName = button?.Tag as string;
            if (string.IsNullOrWhiteSpace(propertyName))
            {
                return;
            }

            WheelGeometrySettings geometry = ViewModel.WorkingSettings.Geometry;
            string currentColor = GetGeometryColor(geometry, propertyName);
            using (Forms.ColorDialog dialog = new Forms.ColorDialog())
            {
                try
                {
                    dialog.Color = System.Drawing.ColorTranslator.FromHtml(currentColor);
                }
                catch
                {
                    dialog.Color = System.Drawing.Color.FromArgb(31, 35, 43);
                }

                if (dialog.ShowDialog() != Forms.DialogResult.OK)
                {
                    return;
                }

                string selectedColor = string.Format(
                    "#{0:X2}{1:X2}{2:X2}",
                    dialog.Color.R,
                    dialog.Color.G,
                    dialog.Color.B);
                SetGeometryColor(geometry, propertyName, selectedColor);
                ViewModel.MarkChanged();
                DataContext = null;
                DataContext = ViewModel;
                RefreshPreview();
            }
        }

        private void StageTriggerTypeComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (!IsLoaded || StageTriggerTypeComboBox.SelectedItem == null)
            {
                return;
            }

            ViewModel.WorkingSettings.StageTrigger.Type = (WheelTriggerType)StageTriggerTypeComboBox.SelectedItem;
            ViewModel.MarkChanged();
        }

        private void CommandTriggerTypeComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (!IsLoaded || CommandTriggerTypeComboBox.SelectedItem == null)
            {
                return;
            }

            ViewModel.WorkingSettings.CommandTrigger.Type = (WheelTriggerType)CommandTriggerTypeComboBox.SelectedItem;
            ViewModel.MarkChanged();
        }

        private void CaptureTrigger_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Controls.Button button = sender as System.Windows.Controls.Button;
            string actionName = button?.Tag as string;
            _capturingTriggerAction = string.Equals(actionName, "Stages", StringComparison.OrdinalIgnoreCase)
                ? WheelTriggerAction.Stages
                : WheelTriggerAction.Commands;
            _capturingTrigger = true;
            ViewModel.SetStatus(
                "Нажмите основную клавишу для " +
                (_capturingTriggerAction == WheelTriggerAction.Stages ? "стадий" : "команд") +
                " вместе с нужными модификаторами.");
            Focus();
            Keyboard.Focus(this);
        }

        private void StagesPreview_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.PreviewLevel = WheelDisplayLevel.Stages;
            RefreshPreview();
        }

        private void CommandsPreview_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.PreviewLevel = WheelDisplayLevel.Commands;
            RefreshPreview();
        }

        private void EditorWheelControl_MouseMove(object sender, MouseEventArgs e)
        {
            WheelHitResult hit = EditorWheelControl.HitTestPoint(e.GetPosition(EditorWheelControl));
            EditorWheelControl.HitResult = hit;
        }

        private void EditorWheelControl_MouseLeave(object sender, MouseEventArgs e)
        {
            EditorWheelControl.HitResult = new WheelHitResult(WheelHitKind.Cancel, -1, 0.0, 0.0);
        }

        private void EditorWheelControl_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Point point = e.GetPosition(EditorWheelControl);
            WheelHitResult hit = EditorWheelControl.HitTestPoint(point);
            if (hit.Kind != WheelHitKind.Sector)
            {
                return;
            }

            if (ViewModel.PreviewLevel == WheelDisplayLevel.Stages)
            {
                WheelProfile profile = ViewModel.Profiles
                    .OrderBy(item => item.Order)
                    .ElementAtOrDefault(hit.SectorIndex);
                if (profile != null)
                {
                    ViewModel.SelectedProfile = profile;
                    ViewModel.PreviewLevel = WheelDisplayLevel.Commands;
                    RefreshPreview();
                }

                return;
            }

            ViewModel.SelectSlot(hit.SectorIndex);
            _wheelDragStart = point;
            _wheelDragSourceIndex = hit.SectorIndex;
        }

        private void EditorWheelControl_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed || _wheelDragSourceIndex < 0)
            {
                return;
            }

            Point point = e.GetPosition(EditorWheelControl);
            if (Math.Abs(point.X - _wheelDragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(point.Y - _wheelDragStart.Y) < SystemParameters.MinimumVerticalDragDistance)
            {
                return;
            }

            DataObject data = new DataObject();
            data.SetData(SlotDragFormat, _wheelDragSourceIndex);
            DragDrop.DoDragDrop(EditorWheelControl, data, DragDropEffects.Move);
            _wheelDragSourceIndex = -1;
        }

        private void CommandCatalogList_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed || ViewModel.SelectedCatalogCommand == null)
            {
                return;
            }

            DataObject data = new DataObject();
            data.SetData(CommandDragFormat, ViewModel.SelectedCatalogCommand);
            DragDrop.DoDragDrop(CommandCatalogList, data, DragDropEffects.Copy);
        }

        private void EditorWheelControl_DragOver(object sender, DragEventArgs e)
        {
            WheelHitResult hit = EditorWheelControl.HitTestPoint(e.GetPosition(EditorWheelControl));
            bool validTarget = ViewModel.PreviewLevel == WheelDisplayLevel.Commands &&
                               hit.Kind == WheelHitKind.Sector;

            if (!validTarget)
            {
                e.Effects = DragDropEffects.None;
            }
            else if (e.Data.GetDataPresent(CommandDragFormat))
            {
                e.Effects = DragDropEffects.Copy;
            }
            else if (e.Data.GetDataPresent(SlotDragFormat))
            {
                e.Effects = DragDropEffects.Move;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }

            EditorWheelControl.HitResult = hit;
            e.Handled = true;
        }

        private void EditorWheelControl_Drop(object sender, DragEventArgs e)
        {
            WheelHitResult hit = EditorWheelControl.HitTestPoint(e.GetPosition(EditorWheelControl));
            if (ViewModel.PreviewLevel != WheelDisplayLevel.Commands || hit.Kind != WheelHitKind.Sector)
            {
                return;
            }

            if (e.Data.GetDataPresent(CommandDragFormat))
            {
                CommandDescriptor command = e.Data.GetData(CommandDragFormat) as CommandDescriptor;
                ViewModel.SelectSlot(hit.SectorIndex);
                ViewModel.AssignCommandToSelectedSlot(command);
            }
            else if (e.Data.GetDataPresent(SlotDragFormat))
            {
                int sourceIndex = (int)e.Data.GetData(SlotDragFormat);
                ViewModel.SwapSlots(sourceIndex, hit.SectorIndex);
            }

            RefreshPreview();
        }

        private void AssignSelectedCommand_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.AddCommandToFirstAvailableSlot(ViewModel.SelectedCatalogCommand);
            RefreshPreview();
        }

        private void ReplaceSelectedCommand_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.ReplaceSelectedSlotCommand(ViewModel.SelectedCatalogCommand);
            RefreshPreview();
        }

        private async void VerifySelectedCommand_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.SelectedCatalogCommand == null)
            {
                ViewModel.SetStatus("Выберите команду в каталоге.");
                return;
            }

            if (ViewModel.SelectedRevitContext == null)
            {
                ViewModel.SetStatus("Для проверки требуется подключение к Revit.");
                return;
            }

            try
            {
                BridgeResponse response = await _bridgeClient.SendAsync(
                    ViewModel.SelectedRevitContext.ProcessId,
                    new BridgeRequest
                    {
                        RequestType = BridgeRequestTypes.VerifyCommand,
                        Command = JsonSerialization.DeepClone(ViewModel.SelectedCatalogCommand)
                    },
                    400);
                ViewModel.SetStatus(response.Message);
            }
            catch (Exception exception)
            {
                ViewModel.SetStatus("Ошибка проверки: " + exception.Message);
            }
        }

        private void SlotProperty_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (!IsLoaded || ViewModel.SelectedSlot == null)
            {
                return;
            }

            ViewModel.MarkChanged();
            RefreshPreview();
        }

        private void ClearSelectedSlot_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.ClearSelectedSlot();
            RefreshPreview();
        }

        private void MoveSlotEarlier_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.MoveSelectedCommand(-1);
            RefreshPreview();
        }

        private void MoveSlotLater_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.MoveSelectedCommand(1);
            RefreshPreview();
        }

        private void Import_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog
            {
                Title = "Импорт настроек колеса",
                Filter = "JSON (*.json)|*.json",
                CheckFileExists = true,
                Multiselect = false
            };

            if (dialog.ShowDialog(this) != true)
            {
                return;
            }

            try
            {
                WheelSettings imported = JsonSerialization.Deserialize<WheelSettings>(
                    File.ReadAllText(dialog.FileName, Encoding.UTF8));
                WheelSettingsValidator.Normalize(imported);
                WheelSettingsValidationResult validation = WheelSettingsValidator.Validate(imported);
                if (!validation.IsValid)
                {
                    throw new InvalidDataException(string.Join(Environment.NewLine, validation.Errors));
                }

                MessageBoxResult confirmation = MessageBox.Show(
                    this,
                    "Импортировать " + imported.Profiles.Count + " профилей и " +
                    imported.CommandCatalog.Count + " команд? Изменения применятся только после сохранения.",
                    "Предпросмотр импорта",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (confirmation == MessageBoxResult.Yes)
                {
                    ViewModel.ReplaceSettings(imported);
                    RefreshPreview();
                }
            }
            catch (Exception exception)
            {
                MessageBox.Show(this, exception.Message, "Ошибка импорта", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Export_Click(object sender, RoutedEventArgs e)
        {
            WheelSettings snapshot = ViewModel.CreateSettingsSnapshot();
            WheelSettingsValidationResult validation = WheelSettingsValidator.Validate(snapshot);
            if (!validation.IsValid)
            {
                MessageBox.Show(
                    this,
                    string.Join(Environment.NewLine, validation.Errors),
                    "Экспорт невозможен",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }

            SaveFileDialog dialog = new SaveFileDialog
            {
                Title = "Экспорт настроек колеса",
                Filter = "JSON (*.json)|*.json",
                FileName = "SABPlus-WheelSettings.json",
                AddExtension = true,
                OverwritePrompt = true
            };

            if (dialog.ShowDialog(this) == true)
            {
                File.WriteAllText(dialog.FileName, JsonSerialization.Serialize(snapshot, true), new UTF8Encoding(false));
                ViewModel.SetStatus("Экспорт завершён: " + dialog.FileName);
            }
        }

        private void Restore_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult result = MessageBox.Show(
                this,
                "Да — восстановить последнюю резервную копию.\nНет — восстановить стандартные профили.",
                "Восстановление настроек",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);

            try
            {
                if (result == MessageBoxResult.Yes)
                {
                    ViewModel.ReplaceSettings(_repository.LoadBackup());
                }
                else if (result == MessageBoxResult.No)
                {
                    ViewModel.ReplaceSettings(WheelSettingsFactory.CreateDefault());
                }
                else
                {
                    return;
                }

                RefreshPreview();
            }
            catch (Exception exception)
            {
                MessageBox.Show(this, exception.Message, "Ошибка восстановления", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Test_Click(object sender, RoutedEventArgs e)
        {
            WheelSettingsValidationResult validation = ViewModel.Validate();
            if (!validation.IsValid)
            {
                MessageBox.Show(
                    this,
                    string.Join(Environment.NewLine, validation.Errors),
                    "Исправьте настройки",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }

            MessageBox.Show(
                this,
                "Окно будет скрыто. Ближний триггер открывает стадии, дальний — команды активной стадии. Команды выполняться не будут. Escape возвращает редактор.",
                "Тест радиального колеса",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            Hide();
            _overlayController.StartTestMode(
                ViewModel.CreateSettingsSnapshot(),
                () => Dispatcher.BeginInvoke(new Action(() =>
                {
                    Show();
                    Activate();
                })));
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (TrySaveSettings(true))
            {
                _closeWithoutPrompt = true;
                Close();
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            _closeWithoutPrompt = true;
            Close();
        }

        private bool TrySaveSettings(bool showSuccess)
        {
            WheelSettingsValidationResult validation = ViewModel.Validate();
            if (!validation.IsValid)
            {
                MessageBox.Show(
                    this,
                    string.Join(Environment.NewLine, validation.Errors),
                    "Настройки не сохранены",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return false;
            }

            try
            {
                WheelSettings snapshot = ViewModel.CreateSettingsSnapshot();
                _repository.Save(snapshot);
                _overlayController.ReloadSettings();
                ViewModel.MarkSaved(snapshot);

                if (showSuccess)
                {
                    ViewModel.SetStatus("Настройки сохранены атомарно. Резервная копия обновлена.");
                }

                return true;
            }
            catch (Exception exception)
            {
                MessageBox.Show(
                    this,
                    exception.Message,
                    "Ошибка сохранения",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return false;
            }
        }

        private void RunEditorAction(Action action)
        {
            try
            {
                action();
                RefreshPreview();
            }
            catch (Exception exception)
            {
                MessageBox.Show(this, exception.Message, "SAB+", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private static string GetGeometryColor(WheelGeometrySettings geometry, string propertyName)
        {
            switch (propertyName)
            {
                case "CapsuleFillColorHex":
                    return geometry.CapsuleFillColorHex;
                case "CapsuleBorderColorHex":
                    return geometry.CapsuleBorderColorHex;
                case "CenterFillColorHex":
                    return geometry.CenterFillColorHex;
                case "CenterBorderColorHex":
                    return geometry.CenterBorderColorHex;
                default:
                    return "#1F232B";
            }
        }

        private static void SetGeometryColor(
            WheelGeometrySettings geometry,
            string propertyName,
            string colorHex)
        {
            switch (propertyName)
            {
                case "CapsuleFillColorHex":
                    geometry.CapsuleFillColorHex = colorHex;
                    break;
                case "CapsuleBorderColorHex":
                    geometry.CapsuleBorderColorHex = colorHex;
                    break;
                case "CenterFillColorHex":
                    geometry.CenterFillColorHex = colorHex;
                    break;
                case "CenterBorderColorHex":
                    geometry.CenterBorderColorHex = colorHex;
                    break;
            }
        }
    }
}
