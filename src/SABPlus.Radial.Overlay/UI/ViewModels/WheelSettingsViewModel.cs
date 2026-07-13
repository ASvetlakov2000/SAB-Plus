using SABPlus.Radial.Core.Geometry;
using SABPlus.Radial.Core.Models;
using SABPlus.Radial.Core.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Data;

namespace SABPlus.Radial.Overlay.UI.ViewModels
{
    public sealed class CommandSourceFilterItem
    {
        public string Name { get; }

        public WheelCommandSource? Source { get; }

        public CommandSourceFilterItem(string name, WheelCommandSource? source)
        {
            Name = name;
            Source = source;
        }
    }

    public sealed class WheelSettingsViewModel : INotifyPropertyChanged
    {
        private WheelSettings _workingSettings;
        private string _savedSnapshot;
        private WheelProfile _selectedProfile;
        private WheelSlot _selectedSlot;
        private int _selectedSlotIndex;
        private CommandDescriptor _selectedCatalogCommand;
        private string _searchText;
        private CommandSourceFilterItem _selectedSourceFilter;
        private WheelDisplayLevel _previewLevel;
        private string _validationSummary;
        private RevitContextSnapshot _selectedRevitContext;

        public event PropertyChangedEventHandler PropertyChanged;

        public ObservableCollection<WheelProfile> Profiles { get; }

        public ObservableCollection<CommandDescriptor> CatalogCommands { get; }

        public ObservableCollection<CommandSourceFilterItem> SourceFilters { get; }

        public ObservableCollection<RevitContextSnapshot> RevitContexts { get; }

        public ICollectionView FilteredCommands { get; }

        public WheelSettings WorkingSettings => _workingSettings;

        public WheelProfile SelectedProfile
        {
            get => _selectedProfile;
            set
            {
                if (ReferenceEquals(_selectedProfile, value))
                {
                    return;
                }

                _selectedProfile = value;
                _selectedSlotIndex = 0;
                _selectedSlot = _selectedProfile?.Slots?.FirstOrDefault();
                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedSlot));
                OnPropertyChanged(nameof(SelectedSlotIndex));
                OnPropertyChanged(nameof(SelectedSectorTitle));
            }
        }

        public WheelSlot SelectedSlot
        {
            get => _selectedSlot;
            private set
            {
                _selectedSlot = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedSectorTitle));
            }
        }

        public int SelectedSlotIndex
        {
            get => _selectedSlotIndex;
            private set
            {
                _selectedSlotIndex = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedSectorTitle));
            }
        }

        public string SelectedSectorTitle => SelectedSlot == null
            ? "Позиция не выбрана"
            : "Позиция " + (SelectedSlotIndex + 1);

        public CommandDescriptor SelectedCatalogCommand
        {
            get => _selectedCatalogCommand;
            set
            {
                _selectedCatalogCommand = value;
                OnPropertyChanged();
            }
        }

        public string SearchText
        {
            get => _searchText;
            set
            {
                _searchText = value ?? string.Empty;
                OnPropertyChanged();
                FilteredCommands.Refresh();
            }
        }

        public CommandSourceFilterItem SelectedSourceFilter
        {
            get => _selectedSourceFilter;
            set
            {
                _selectedSourceFilter = value;
                OnPropertyChanged();
                FilteredCommands.Refresh();
            }
        }

        public WheelDisplayLevel PreviewLevel
        {
            get => _previewLevel;
            set
            {
                _previewLevel = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsStagePreview));
                OnPropertyChanged(nameof(IsCommandPreview));
            }
        }

        public bool IsStagePreview => PreviewLevel == WheelDisplayLevel.Stages;

        public bool IsCommandPreview => PreviewLevel == WheelDisplayLevel.Commands;

        public string ValidationSummary
        {
            get => _validationSummary;
            private set
            {
                _validationSummary = value;
                OnPropertyChanged();
            }
        }

        public RevitContextSnapshot SelectedRevitContext
        {
            get => _selectedRevitContext;
            set
            {
                _selectedRevitContext = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ConnectionText));
                MergePostableCommands(value);
            }
        }

        public string ConnectionText => SelectedRevitContext == null
            ? "Revit не подключён"
            : "Revit " + SelectedRevitContext.RevitVersion +
              " · PID " + SelectedRevitContext.ProcessId +
              (string.IsNullOrWhiteSpace(SelectedRevitContext.ProjectTitle)
                  ? string.Empty
                  : " · " + SelectedRevitContext.ProjectTitle);

        public string StageTriggerDisplayText => FormatTrigger(_workingSettings.StageTrigger);

        public string CommandTriggerDisplayText => FormatTrigger(_workingSettings.CommandTrigger);

        public bool IsDirty => !string.Equals(
            _savedSnapshot,
            JsonSerialization.Serialize(CreateSettingsSnapshot(), false),
            StringComparison.Ordinal);

        public WheelSettingsViewModel(WheelSettings settings)
        {
            _workingSettings = JsonSerialization.DeepClone(
                settings ?? throw new ArgumentNullException(nameof(settings)));
            WheelSettingsValidator.Normalize(_workingSettings);

            Profiles = new ObservableCollection<WheelProfile>(
                _workingSettings.Profiles.OrderBy(profile => profile.Order));
            CatalogCommands = new ObservableCollection<CommandDescriptor>(_workingSettings.CommandCatalog);
            RevitContexts = new ObservableCollection<RevitContextSnapshot>();

            SourceFilters = new ObservableCollection<CommandSourceFilterItem>
            {
                new CommandSourceFilterItem("Все источники", null),
                new CommandSourceFilterItem("Системные Revit", WheelCommandSource.RevitPostable),
                new CommandSourceFilterItem("Команды SAB+", WheelCommandSource.SabCommand),
                new CommandSourceFilterItem("Сторонние add-in", WheelCommandSource.RevitCommandId),
                new CommandSourceFilterItem("Локальные утилиты", WheelCommandSource.LocalUtility)
            };

            _selectedSourceFilter = SourceFilters[0];
            _previewLevel = WheelDisplayLevel.Commands;
            _searchText = string.Empty;
            _validationSummary = string.Empty;

            FilteredCommands = CollectionViewSource.GetDefaultView(CatalogCommands);
            FilteredCommands.Filter = FilterCommand;

            SelectedProfile = Profiles.FirstOrDefault();
            _savedSnapshot = JsonSerialization.Serialize(CreateSettingsSnapshot(), false);
            UpdateValidationSummary();
        }

        public void SelectSlot(int index)
        {
            if (SelectedProfile?.Slots == null || index < 0 || index >= SelectedProfile.Slots.Count)
            {
                return;
            }

            SelectedSlotIndex = index;
            SelectedSlot = SelectedProfile.Slots[index];
        }

        public void AddProfile()
        {
            if (Profiles.Count >= 8)
            {
                throw new InvalidOperationException("Можно создать не более 8 стадий.");
            }

            WheelProfile profile = WheelSettingsFactory.CreateProfile(
                "Новая стадия",
                "НС",
                "#0F6CBD",
                Profiles.Count,
                8);
            Profiles.Add(profile);
            SelectedProfile = profile;
            MarkChanged();
        }

        public void DuplicateSelectedProfile()
        {
            if (SelectedProfile == null)
            {
                return;
            }

            if (Profiles.Count >= 8)
            {
                throw new InvalidOperationException("Можно создать не более 8 стадий.");
            }

            WheelProfile duplicate = JsonSerialization.DeepClone(SelectedProfile);
            duplicate.Id = Guid.NewGuid().ToString("D");
            duplicate.Name = SelectedProfile.Name + " — копия";
            duplicate.Order = Profiles.Count;
            Profiles.Add(duplicate);
            SelectedProfile = duplicate;
            MarkChanged();
        }

        public void DeleteSelectedProfile()
        {
            if (SelectedProfile == null || Profiles.Count <= 1)
            {
                return;
            }

            int index = Profiles.IndexOf(SelectedProfile);
            Profiles.Remove(SelectedProfile);
            NormalizeProfileOrder();
            SelectedProfile = Profiles[Math.Max(0, Math.Min(index, Profiles.Count - 1))];
            MarkChanged();
        }

        public void MoveSelectedProfile(int offset)
        {
            if (SelectedProfile == null)
            {
                return;
            }

            int oldIndex = Profiles.IndexOf(SelectedProfile);
            int newIndex = oldIndex + offset;
            if (newIndex < 0 || newIndex >= Profiles.Count)
            {
                return;
            }

            Profiles.Move(oldIndex, newIndex);
            NormalizeProfileOrder();
            MarkChanged();
        }

        public void ChangeSectorCount(int sectorCount)
        {
            if (SelectedProfile == null)
            {
                return;
            }

            int normalized = Math.Max(
                WheelGeometryCalculator.MinimumCommandSectorCount,
                Math.Min(WheelGeometryCalculator.MaximumSectorCount, sectorCount));

            SelectedProfile.SectorCount = normalized;
            while (SelectedProfile.Slots.Count < normalized)
            {
                SelectedProfile.Slots.Add(new WheelSlot());
            }

            if (SelectedProfile.Slots.Count > normalized)
            {
                SelectedProfile.Slots.RemoveRange(normalized, SelectedProfile.Slots.Count - normalized);
            }

            for (int index = 0; index < SelectedProfile.Slots.Count; index++)
            {
                SelectedProfile.Slots[index].Index = index;
            }

            SelectSlot(Math.Min(SelectedSlotIndex, normalized - 1));
            MarkChanged();
        }

        public void AssignCommandToSelectedSlot(CommandDescriptor command)
        {
            if (SelectedSlot == null || command == null)
            {
                return;
            }

            EnsureCommandSaved(command);
            SelectedSlot.CommandId = command.Id;
            SelectedSlot.DisplayName = command.DisplayName;
            SelectedSlot.ShortLabel = command.DisplayName;
            SelectedSlot.ToolTip = command.Description;
            SelectedSlot.IconPath = string.Empty;
            SelectedSlot.ShowText = true;
            OnPropertyChanged(nameof(SelectedSlot));
            MarkChanged();
        }

        public void AddCommandToFirstAvailableSlot(CommandDescriptor command)
        {
            if (SelectedProfile?.Slots == null)
            {
                return;
            }

            if (command == null)
            {
                SetStatus("Сначала выберите команду в каталоге.");
                return;
            }

            for (int index = 0; index < SelectedProfile.Slots.Count; index++)
            {
                WheelSlot slot = SelectedProfile.Slots[index];
                if (slot == null || string.IsNullOrWhiteSpace(slot.CommandId))
                {
                    SelectSlot(index);
                    AssignCommandToSelectedSlot(command);
                    return;
                }
            }

            SetStatus("В стадии нет свободных позиций. Увеличьте их количество или очистите позицию.");
        }

        public void ReplaceSelectedSlotCommand(CommandDescriptor command)
        {
            if (command == null)
            {
                SetStatus("Сначала выберите команду в каталоге.");
                return;
            }

            AssignCommandToSelectedSlot(command);
        }

        public void ClearSelectedSlot()
        {
            if (SelectedSlot == null)
            {
                return;
            }

            int index = SelectedSlot.Index;
            WheelSlot emptySlot = new WheelSlot { Index = index };
            SelectedProfile.Slots[index] = emptySlot;
            SelectSlot(index);
            MarkChanged();
        }

        public void SwapSlots(int sourceIndex, int targetIndex)
        {
            if (SelectedProfile?.Slots == null ||
                sourceIndex < 0 || sourceIndex >= SelectedProfile.Slots.Count ||
                targetIndex < 0 || targetIndex >= SelectedProfile.Slots.Count ||
                sourceIndex == targetIndex)
            {
                return;
            }

            WheelSlot source = SelectedProfile.Slots[sourceIndex];
            WheelSlot target = SelectedProfile.Slots[targetIndex];
            SelectedProfile.Slots[sourceIndex] = target;
            SelectedProfile.Slots[targetIndex] = source;
            SelectedProfile.Slots[sourceIndex].Index = sourceIndex;
            SelectedProfile.Slots[targetIndex].Index = targetIndex;
            SelectSlot(targetIndex);
            MarkChanged();
        }

        public void MoveSelectedCommand(int offset)
        {
            if (SelectedProfile?.Slots == null || SelectedSlot == null || offset == 0)
            {
                return;
            }

            List<int> assignedIndexes = new List<int>();
            for (int index = 0; index < SelectedProfile.Slots.Count; index++)
            {
                WheelSlot slot = SelectedProfile.Slots[index];
                if (slot != null && !string.IsNullOrWhiteSpace(slot.CommandId))
                {
                    assignedIndexes.Add(index);
                }
            }

            int assignedPosition = assignedIndexes.IndexOf(SelectedSlotIndex);
            if (assignedPosition < 0 || assignedIndexes.Count < 2)
            {
                return;
            }

            int targetPosition = (assignedPosition + offset) % assignedIndexes.Count;
            if (targetPosition < 0)
            {
                targetPosition += assignedIndexes.Count;
            }

            SwapSlots(SelectedSlotIndex, assignedIndexes[targetPosition]);
        }

        public WheelSettingsValidationResult Validate()
        {
            WheelSettings snapshot = CreateSettingsSnapshot();
            WheelSettingsValidationResult result = WheelSettingsValidator.Validate(snapshot);
            ValidationSummary = result.IsValid
                ? "Настройки корректны" + (result.Warnings.Count > 0 ? " · предупреждений: " + result.Warnings.Count : string.Empty)
                : "Ошибок: " + result.Errors.Count;
            return result;
        }

        public WheelSettings CreateSettingsSnapshot()
        {
            WheelSettings snapshot = JsonSerialization.DeepClone(_workingSettings);
            snapshot.Profiles = Profiles.Select(item => JsonSerialization.DeepClone(item)).ToList();
            snapshot.CommandCatalog = CatalogCommands
                .Select(item => JsonSerialization.DeepClone(item))
                .ToList();
            WheelSettingsValidator.Normalize(snapshot);
            return snapshot;
        }

        public void ReplaceSettings(WheelSettings settings)
        {
            _workingSettings = JsonSerialization.DeepClone(settings);
            WheelSettingsValidator.Normalize(_workingSettings);

            Profiles.Clear();
            foreach (WheelProfile profile in _workingSettings.Profiles.OrderBy(item => item.Order))
            {
                Profiles.Add(profile);
            }

            CatalogCommands.Clear();
            foreach (CommandDescriptor command in _workingSettings.CommandCatalog)
            {
                CatalogCommands.Add(command);
            }

            SelectedProfile = Profiles.FirstOrDefault();
            FilteredCommands.Refresh();
            MarkChanged();
        }

        public void MarkSaved(WheelSettings savedSettings)
        {
            _workingSettings = JsonSerialization.DeepClone(savedSettings);
            _savedSnapshot = JsonSerialization.Serialize(savedSettings, false);
            UpdateValidationSummary();
            OnPropertyChanged(nameof(IsDirty));
        }

        public void MarkChanged()
        {
            UpdateValidationSummary();
            OnPropertyChanged(nameof(StageTriggerDisplayText));
            OnPropertyChanged(nameof(CommandTriggerDisplayText));
            OnPropertyChanged(nameof(IsDirty));
        }

        public void SetStatus(string message)
        {
            ValidationSummary = message ?? string.Empty;
        }

        public void UpdateContexts(IEnumerable<RevitContextSnapshot> contexts, int preferredProcessId)
        {
            int currentProcessId = SelectedRevitContext?.ProcessId ?? preferredProcessId;
            RevitContexts.Clear();
            foreach (RevitContextSnapshot context in contexts.OrderBy(item => item.ProcessId))
            {
                RevitContexts.Add(context);
            }

            SelectedRevitContext = RevitContexts.FirstOrDefault(item => item.ProcessId == currentProcessId)
                                   ?? RevitContexts.FirstOrDefault();
            OnPropertyChanged(nameof(ConnectionText));
        }

        private bool FilterCommand(object item)
        {
            CommandDescriptor command = item as CommandDescriptor;
            if (command == null)
            {
                return false;
            }

            if (SelectedSourceFilter?.Source != null && command.Source != SelectedSourceFilter.Source.Value)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(SearchText))
            {
                return true;
            }

            return (command.DisplayName ?? string.Empty).IndexOf(SearchText, StringComparison.CurrentCultureIgnoreCase) >= 0 ||
                   (command.Description ?? string.Empty).IndexOf(SearchText, StringComparison.CurrentCultureIgnoreCase) >= 0;
        }

        private void MergePostableCommands(RevitContextSnapshot context)
        {
            if (context?.PostableCommandNames == null)
            {
                return;
            }

            Dictionary<string, CommandDescriptor> commandsByApiName = CatalogCommands
                .Where(item => item.Source == WheelCommandSource.RevitPostable)
                .GroupBy(
                    item => RevitPostableCommandCatalog.NormalizeApiName(item.RevitPostableCommandName),
                    StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

            foreach (string commandName in context.PostableCommandNames.OrderBy(item => item))
            {
                string normalizedName = RevitPostableCommandCatalog.NormalizeApiName(commandName);
                string ribbonLabel = string.Empty;
                if (context.PostableCommandDisplayNames != null)
                {
                    context.PostableCommandDisplayNames.TryGetValue(normalizedName, out ribbonLabel);
                }

                CommandDescriptor localizedDescriptor =
                    RevitPostableCommandCatalog.CreateDescriptor(normalizedName, ribbonLabel);
                CommandDescriptor existing;
                if (commandsByApiName.TryGetValue(normalizedName, out existing))
                {
                    existing.DisplayName = localizedDescriptor.DisplayName;
                    existing.Description = localizedDescriptor.Description;
                    existing.RevitPostableCommandName = normalizedName;
                    continue;
                }

                CatalogCommands.Add(localizedDescriptor);
                commandsByApiName.Add(normalizedName, localizedDescriptor);
            }

            FilteredCommands.Refresh();
        }

        private void EnsureCommandSaved(CommandDescriptor command)
        {
            if (_workingSettings.CommandCatalog.Any(
                item => string.Equals(item.Id, command.Id, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            _workingSettings.CommandCatalog.Add(JsonSerialization.DeepClone(command));
        }

        private void NormalizeProfileOrder()
        {
            for (int index = 0; index < Profiles.Count; index++)
            {
                Profiles[index].Order = index;
            }
        }

        private void UpdateValidationSummary()
        {
            Validate();
        }

        private static string FormatTrigger(WheelTriggerSettings trigger)
        {
            if (trigger == null)
            {
                return "не настроен";
            }

            if (trigger.Type == WheelTriggerType.MouseXButton2)
            {
                return "XButton2 (дальняя боковая)";
            }

            if (trigger.Type == WheelTriggerType.MouseXButton1)
            {
                return "XButton1 (ближняя боковая)";
            }

            List<string> parts = new List<string>();
            if (trigger.Modifiers.HasFlag(WheelKeyboardModifiers.Control))
            {
                parts.Add("Ctrl");
            }

            if (trigger.Modifiers.HasFlag(WheelKeyboardModifiers.Shift))
            {
                parts.Add("Shift");
            }

            if (trigger.Modifiers.HasFlag(WheelKeyboardModifiers.Alt))
            {
                parts.Add("Alt");
            }

            if (trigger.Modifiers.HasFlag(WheelKeyboardModifiers.Windows))
            {
                parts.Add("Win");
            }

            parts.Add(trigger.VirtualKey > 0 ? "VK " + trigger.VirtualKey : "клавиша не выбрана");
            return string.Join(" + ", parts);
        }

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
