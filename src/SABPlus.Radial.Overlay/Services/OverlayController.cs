using SABPlus.Radial.Core.Geometry;
using SABPlus.Radial.Core.Models;
using SABPlus.Radial.Core.Services;
using SABPlus.Radial.Overlay.UI.Views;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace SABPlus.Radial.Overlay.Services
{
    public sealed class OverlayController : IDisposable
    {
        private enum WheelInteractionPhase
        {
            Stages = 0,
            Commands = 1
        }

        private readonly WheelSettingsRepository _repository;
        private readonly ProjectWheelStateService _projectStateService;
        private readonly RevitBridgeDiscoveryService _discoveryService;
        private readonly BridgeClientService _bridgeClient;
        private readonly GlobalInputHookService _inputHook;

        private WheelSettings _settings;
        private WheelSettings _testSettings;
        private RadialOverlayWindow _window;
        private WheelHitResult _currentHit;
        private WheelDisplayLevel _displayLevel;
        private WheelInteractionPhase _interactionPhase;
        private WheelProfile _activeProfile;
        private List<WheelProfile> _orderedProfiles;
        private RevitContextSnapshot _activeContext;
        private int _stageOriginPhysicalX;
        private int _stageOriginPhysicalY;
        private double _dpiScale;
        private bool _isWheelVisible;
        private bool _isTestMode;
        private string _testProfileId;
        private Action _testCompleted;

        public event EventHandler<string> StatusMessage;

        public bool IsTestMode => _isTestMode;

        public OverlayController(
            WheelSettingsRepository repository,
            ProjectWheelStateService projectStateService,
            RevitBridgeDiscoveryService discoveryService,
            BridgeClientService bridgeClient,
            GlobalInputHookService inputHook)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _projectStateService = projectStateService ?? throw new ArgumentNullException(nameof(projectStateService));
            _discoveryService = discoveryService ?? throw new ArgumentNullException(nameof(discoveryService));
            _bridgeClient = bridgeClient ?? throw new ArgumentNullException(nameof(bridgeClient));
            _inputHook = inputHook ?? throw new ArgumentNullException(nameof(inputHook));

            _settings = _repository.LoadOrCreateDefault();
            _orderedProfiles = GetOrderedProfiles(_settings);

            _inputHook.TriggerPressed += InputHook_TriggerPressed;
            _inputHook.TriggerReleased += InputHook_TriggerReleased;
            _inputHook.CursorMoved += InputHook_CursorMoved;
            _inputHook.EscapeHandler = HandleEscape;
        }

        public WheelSettings GetSettingsCopy()
        {
            return JsonSerialization.DeepClone(_settings);
        }

        public void ReloadSettings()
        {
            _settings = _repository.LoadOrCreateDefault();
            _orderedProfiles = GetOrderedProfiles(_settings);
            _inputHook.UpdateTriggers(_settings.StageTrigger, _settings.CommandTrigger);
        }

        public void StartTestMode(WheelSettings settings, Action completed)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            _testSettings = JsonSerialization.DeepClone(settings);
            WheelSettingsValidator.Normalize(_testSettings);
            _testProfileId = string.Empty;
            _testCompleted = completed;
            _isTestMode = true;
            _inputHook.UpdateTriggers(_testSettings.StageTrigger, _testSettings.CommandTrigger);
            RaiseStatus("Тестовый режим: ближний триггер открывает стадии, дальний — команды. Escape возвращает редактор.");
        }

        public void Dispose()
        {
            _inputHook.TriggerPressed -= InputHook_TriggerPressed;
            _inputHook.TriggerReleased -= InputHook_TriggerReleased;
            _inputHook.CursorMoved -= InputHook_CursorMoved;
            _inputHook.EscapeHandler = null;

            if (_window != null)
            {
                _window.Close();
                _window = null;
            }
        }

        private bool InputHook_TriggerPressed(object sender, GlobalPointEventArgs e)
        {
            if (_isWheelVisible)
            {
                return true;
            }

            WheelSettings currentSettings = GetCurrentSettings();
            if (currentSettings == null)
            {
                return false;
            }

            _orderedProfiles = GetOrderedProfiles(currentSettings);
            if (_orderedProfiles.Count == 0)
            {
                return false;
            }

            string selectedProfileId;
            if (_isTestMode)
            {
                _activeContext = null;
                selectedProfileId = _testProfileId;
            }
            else
            {
                _activeContext = _discoveryService.GetForegroundRevitContext();
                if (!WheelActivationPolicy.CanOpenForContext(_activeContext))
                {
                    return false;
                }

                selectedProfileId = _projectStateService.GetActiveProfileId(_activeContext.ProjectKey);
            }

            // Stage and command wheels are invoked independently and use the current cursor as one stable center.
            _activeProfile = FindProfile(selectedProfileId);
            if (e.Action == WheelTriggerAction.Commands && _activeProfile == null)
            {
                RaiseStatus("Сначала выберите активную стадию ближним триггером.");
                return false;
            }

            _displayLevel = e.Action == WheelTriggerAction.Stages
                ? WheelDisplayLevel.Stages
                : WheelDisplayLevel.Commands;
            _interactionPhase = e.Action == WheelTriggerAction.Stages
                ? WheelInteractionPhase.Stages
                : WheelInteractionPhase.Commands;
            _stageOriginPhysicalX = e.X;
            _stageOriginPhysicalY = e.Y;

            MonitorPlacement placement = MonitorDpiService.GetPlacement(e.X, e.Y);
            _dpiScale = placement.DpiScale;

            if (_window == null)
            {
                _window = new RadialOverlayWindow();
            }

            ConfigureWindow(false, false);
            _currentHit = new WheelHitResult(WheelHitKind.Cancel, -1, 0.0, 0.0);
            _window.ShowAtPhysicalCenter(e.X, e.Y, _dpiScale);
            _isWheelVisible = true;
            return true;
        }

        private void InputHook_CursorMoved(object sender, GlobalPointEventArgs e)
        {
            if (!_isWheelVisible)
            {
                return;
            }

            UpdateHit(e.X, e.Y);
        }

        private void InputHook_TriggerReleased(object sender, GlobalPointEventArgs e)
        {
            if (!_isWheelVisible)
            {
                return;
            }

            UpdateHit(e.X, e.Y);
            WheelHitResult finalHit = _currentHit;
            WheelInteractionPhase finalPhase = _interactionPhase;
            _window.HideOverlay();
            _isWheelVisible = false;

            if (finalHit == null)
            {
                return;
            }

            if (finalPhase == WheelInteractionPhase.Stages)
            {
                if (finalHit.Kind == WheelHitKind.Sector)
                {
                    SelectStage(finalHit.SectorIndex);
                }

                return;
            }

            if (finalHit.Kind == WheelHitKind.Sector)
            {
                ExecuteSlot(finalHit.SectorIndex);
            }
        }

        private void UpdateHit(int physicalX, int physicalY)
        {
            WheelSettings currentSettings = GetCurrentSettings();
            if (currentSettings?.Geometry == null)
            {
                return;
            }

            double scale = _dpiScale <= 0.0 ? 1.0 : _dpiScale;
            int originX = _stageOriginPhysicalX;
            int originY = _stageOriginPhysicalY;
            double deltaX = (physicalX - originX) / scale;
            double deltaY = (physicalY - originY) / scale;

            if (_interactionPhase == WheelInteractionPhase.Stages)
            {
                _currentHit = WheelGeometryCalculator.HitTest(
                    deltaX,
                    deltaY,
                    _orderedProfiles.Count,
                    WheelDisplayLevel.Stages,
                    currentSettings.Geometry);
                _window.UpdateHit(_currentHit);
                return;
            }

            IReadOnlyList<int> assignedSlotIndexes =
                WheelGeometryCalculator.GetAssignedCommandSlotIndexes(_activeProfile);
            int commandCount = assignedSlotIndexes.Count;
            if (commandCount <= 0)
            {
                _currentHit = new WheelHitResult(WheelHitKind.Cancel, -1, 0.0, 0.0);
                _window.UpdateHit(_currentHit);
                return;
            }

            WheelHitResult commandHit = WheelGeometryCalculator.HitTest(
                deltaX,
                deltaY,
                commandCount,
                WheelDisplayLevel.Commands,
                currentSettings.Geometry);
            _currentHit = commandHit.Kind == WheelHitKind.Sector &&
                          commandHit.SectorIndex >= 0 &&
                          commandHit.SectorIndex < assignedSlotIndexes.Count
                ? new WheelHitResult(
                    commandHit.Kind,
                    assignedSlotIndexes[commandHit.SectorIndex],
                    commandHit.Distance,
                    commandHit.AngleDegrees)
                : commandHit;

            _window.UpdateHit(_currentHit);
        }

        private void SelectStage(int sectorIndex)
        {
            if (sectorIndex < 0 || sectorIndex >= _orderedProfiles.Count)
            {
                return;
            }

            WheelProfile profile = _orderedProfiles[sectorIndex];
            _activeProfile = profile;
            SaveSelectedProfile(profile.Id);
            RaiseStatus("Активная стадия: " + profile.Name);
        }

        private void ConfigureWindow(bool showStageCapsules, bool reserveExpandedCommandRing)
        {
            WheelSettings currentSettings = GetCurrentSettings();
            _window.Configure(
                currentSettings,
                _displayLevel,
                _activeProfile,
                _orderedProfiles,
                showStageCapsules,
                reserveExpandedCommandRing,
                _activeProfile?.Id ?? string.Empty);
        }

        private void SaveSelectedProfile(string profileId)
        {
            if (_isTestMode)
            {
                _testProfileId = profileId ?? string.Empty;
            }
            else if (_activeContext != null)
            {
                _projectStateService.SetActiveProfileId(
                    _activeContext.ProjectKey,
                    profileId ?? string.Empty);
            }
        }

        private void ExecuteSlot(int sectorIndex)
        {
            WheelSettings currentSettings = GetCurrentSettings();
            if (_activeProfile?.Slots == null ||
                sectorIndex < 0 ||
                sectorIndex >= _activeProfile.Slots.Count)
            {
                return;
            }

            WheelSlot slot = _activeProfile.Slots[sectorIndex];
            if (slot == null || string.IsNullOrWhiteSpace(slot.CommandId))
            {
                RaiseStatus("Позиция колеса не назначена.");
                return;
            }

            CommandDescriptor command = currentSettings.CommandCatalog.FirstOrDefault(
                item => string.Equals(item.Id, slot.CommandId, StringComparison.OrdinalIgnoreCase));
            if (command == null)
            {
                RaiseStatus("Команда позиции не найдена в каталоге.");
                return;
            }

            if (_isTestMode)
            {
                RaiseStatus("Тест: выбрана команда «" + command.DisplayName + "». Команда не запущена.");
                return;
            }

            if (command.Source == WheelCommandSource.LocalUtility)
            {
                LaunchLocalUtility(command);
                return;
            }

            if (_activeContext == null)
            {
                RaiseStatus("Связь с активным Revit потеряна.");
                return;
            }

            QueueRevitCommand(_activeContext.ProcessId, command);
        }

        private async void QueueRevitCommand(int processId, CommandDescriptor command)
        {
            try
            {
                BridgeResponse response = await _bridgeClient.SendAsync(
                    processId,
                    new BridgeRequest
                    {
                        RequestType = BridgeRequestTypes.ExecuteCommand,
                        Command = JsonSerialization.DeepClone(command)
                    },
                    400);

                RaiseStatus(response.Message);
            }
            catch (Exception exception)
            {
                RaiseStatus("Не удалось передать команду в Revit: " + exception.Message);
            }
        }

        private void LaunchLocalUtility(CommandDescriptor command)
        {
            try
            {
                string path = Environment.ExpandEnvironmentVariables(command.UtilityPath ?? string.Empty);
                if (!File.Exists(path))
                {
                    RaiseStatus("Файл утилиты не найден: " + path);
                    return;
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName = path,
                    Arguments = command.UtilityArguments ?? string.Empty,
                    WorkingDirectory = Path.GetDirectoryName(path),
                    UseShellExecute = true
                });
                RaiseStatus("Запущена утилита: " + command.DisplayName);
            }
            catch (Exception exception)
            {
                RaiseStatus("Не удалось запустить утилиту: " + exception.Message);
            }
        }

        private bool HandleEscape()
        {
            if (_isWheelVisible)
            {
                _window.HideOverlay();
                _isWheelVisible = false;
                return true;
            }

            if (_isTestMode)
            {
                _isTestMode = false;
                _testSettings = null;
                _testProfileId = string.Empty;
                _inputHook.UpdateTriggers(_settings.StageTrigger, _settings.CommandTrigger);
                Action completed = _testCompleted;
                _testCompleted = null;
                completed?.Invoke();
                RaiseStatus("Тестовый режим завершён.");
                return true;
            }

            return false;
        }

        private WheelSettings GetCurrentSettings()
        {
            return _isTestMode ? _testSettings : _settings;
        }

        private WheelProfile FindProfile(string profileId)
        {
            return _orderedProfiles.FirstOrDefault(
                profile => string.Equals(profile.Id, profileId, StringComparison.OrdinalIgnoreCase));
        }

        private void RaiseStatus(string message)
        {
            StatusMessage?.Invoke(this, message ?? string.Empty);
        }

        private static List<WheelProfile> GetOrderedProfiles(WheelSettings settings)
        {
            return settings.Profiles
                .OrderBy(profile => profile.Order)
                .ThenBy(profile => profile.Name)
                .ToList();
        }
    }
}
