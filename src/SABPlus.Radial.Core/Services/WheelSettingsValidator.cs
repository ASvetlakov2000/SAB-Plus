using SABPlus.Radial.Core.Geometry;
using SABPlus.Radial.Core.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace SABPlus.Radial.Core.Services
{
    public sealed class WheelSettingsValidationResult
    {
        public List<string> Errors { get; }

        public List<string> Warnings { get; }

        public bool IsValid => Errors.Count == 0;

        public WheelSettingsValidationResult()
        {
            Errors = new List<string>();
            Warnings = new List<string>();
        }
    }

    public static class WheelSettingsValidator
    {
        public static WheelSettingsValidationResult Validate(WheelSettings settings)
        {
            WheelSettingsValidationResult result = new WheelSettingsValidationResult();

            if (settings == null)
            {
                result.Errors.Add("Настройки отсутствуют.");
                return result;
            }

            if (settings.SchemaVersion > WheelSettings.CurrentSchemaVersion)
            {
                result.Errors.Add("Версия схемы настроек новее поддерживаемой.");
            }

            ValidateTrigger(settings.StageTrigger, "стадий", result);
            ValidateTrigger(settings.CommandTrigger, "команд", result);
            if (AreSameTrigger(settings.StageTrigger, settings.CommandTrigger))
            {
                result.Errors.Add("Триггеры стадий и команд должны отличаться.");
            }

            ValidateGeometry(settings.Geometry, result);

            if (settings.Profiles == null || settings.Profiles.Count == 0)
            {
                result.Errors.Add("Должен существовать хотя бы один профиль.");
                return result;
            }

            if (settings.Profiles.Count > 8)
            {
                result.Errors.Add("Колесо стадий поддерживает не более 8 профилей.");
            }

            HashSet<string> profileIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> commandIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (settings.CommandCatalog != null)
            {
                foreach (CommandDescriptor command in settings.CommandCatalog)
                {
                    if (command == null || string.IsNullOrWhiteSpace(command.Id))
                    {
                        result.Errors.Add("В каталоге есть команда без ID.");
                        continue;
                    }

                    if (!commandIds.Add(command.Id))
                    {
                        result.Errors.Add("Повторяющийся ID команды: " + command.Id);
                    }
                }
            }

            foreach (WheelProfile profile in settings.Profiles)
            {
                ValidateProfile(profile, profileIds, commandIds, result);
            }

            return result;
        }

        public static void Normalize(WheelSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            int sourceSchemaVersion = settings.SchemaVersion;
            settings.SchemaVersion = WheelSettings.CurrentSchemaVersion;
            settings.Trigger = settings.Trigger ?? new WheelTriggerSettings();
            if (sourceSchemaVersion < 5)
            {
                // Version 5 separates stage and command invocation into two independent triggers.
                settings.StageTrigger = new WheelTriggerSettings
                {
                    Type = WheelTriggerType.MouseXButton1
                };
                settings.CommandTrigger = JsonSerialization.DeepClone(settings.Trigger);
            }

            settings.StageTrigger = settings.StageTrigger ?? new WheelTriggerSettings
            {
                Type = WheelTriggerType.MouseXButton1
            };
            settings.CommandTrigger = settings.CommandTrigger ?? new WheelTriggerSettings
            {
                Type = WheelTriggerType.MouseXButton2
            };
            settings.Geometry = settings.Geometry ?? new WheelGeometrySettings();
            if (sourceSchemaVersion < 4)
            {
                // Version 4 applies assigned-command layout and user-controlled capsule spacing.
                settings.Geometry = new WheelGeometrySettings();
            }
            else if (sourceSchemaVersion < 5)
            {
                WheelGeometrySettings defaults = new WheelGeometrySettings();
                settings.Geometry.CapsuleFillColorHex = defaults.CapsuleFillColorHex;
                settings.Geometry.CapsuleBorderColorHex = defaults.CapsuleBorderColorHex;
                settings.Geometry.CenterFillColorHex = defaults.CenterFillColorHex;
                settings.Geometry.CenterBorderColorHex = defaults.CenterBorderColorHex;
                settings.Geometry.CapsuleFillOpacity = defaults.CapsuleFillOpacity;
                settings.Geometry.CenterFillOpacity = defaults.CenterFillOpacity;
            }
            settings.Profiles = settings.Profiles ?? new List<WheelProfile>();
            settings.CommandCatalog = settings.CommandCatalog ?? new List<CommandDescriptor>();
            NormalizePostableCommandCatalog(settings);

            for (int profileIndex = 0; profileIndex < settings.Profiles.Count; profileIndex++)
            {
                WheelProfile profile = settings.Profiles[profileIndex];
                profile.Id = string.IsNullOrWhiteSpace(profile.Id) ? Guid.NewGuid().ToString("D") : profile.Id;
                profile.Name = string.IsNullOrWhiteSpace(profile.Name) ? "Стадия " + (profileIndex + 1) : profile.Name.Trim();
                profile.Abbreviation = string.IsNullOrWhiteSpace(profile.Abbreviation)
                    ? profile.Name.Substring(0, 1).ToUpperInvariant()
                    : profile.Abbreviation.Trim();
                profile.Order = profileIndex;
                profile.SectorCount = Math.Max(
                    WheelGeometryCalculator.MinimumCommandSectorCount,
                    Math.Min(WheelGeometryCalculator.MaximumSectorCount, profile.SectorCount));
                profile.ColorHex = string.IsNullOrWhiteSpace(profile.ColorHex)
                    ? "#0F6CBD"
                    : profile.ColorHex.Trim().ToUpperInvariant();
                profile.Slots = profile.Slots ?? new List<WheelSlot>();

                while (profile.Slots.Count < profile.SectorCount)
                {
                    profile.Slots.Add(new WheelSlot());
                }

                if (profile.Slots.Count > profile.SectorCount)
                {
                    profile.Slots.RemoveRange(profile.SectorCount, profile.Slots.Count - profile.SectorCount);
                }

                for (int slotIndex = 0; slotIndex < profile.Slots.Count; slotIndex++)
                {
                    WheelSlot slot = profile.Slots[slotIndex] ?? new WheelSlot();
                    slot.Index = slotIndex;
                    profile.Slots[slotIndex] = slot;
                }
            }
        }

        private static void ValidateGeometry(
            WheelGeometrySettings geometry,
            WheelSettingsValidationResult result)
        {
            if (geometry == null)
            {
                result.Errors.Add("Не настроена геометрия колеса.");
                return;
            }

            if (geometry.CancelRadius < 0.0 ||
                geometry.CenterRingOuterRadius <= geometry.CancelRadius ||
                geometry.StageActivationRadius <= geometry.CenterRingOuterRadius ||
                geometry.ReturnToStagesRadius <= geometry.CenterRingOuterRadius ||
                geometry.CommandActivationRadius <= geometry.ReturnToStagesRadius ||
                geometry.StageCapsuleRadius <= geometry.StageActivationRadius ||
                geometry.CommandCapsuleRadius <= geometry.CommandActivationRadius)
            {
                result.Errors.Add("Радиусы центрального кольца, зон выбора и капсул настроены некорректно.");
            }

            if (geometry.StageCapsuleWidth <= 0.0 ||
                geometry.CommandCapsuleWidth <= 0.0 ||
                geometry.CapsuleHeight <= 0.0 ||
                geometry.CapsuleCornerRadius < 0.0 ||
                geometry.CapsuleIconSize <= 0.0 ||
                geometry.CapsuleIconSize > geometry.CapsuleHeight ||
                geometry.CapsuleGap < 0.0 ||
                geometry.CapsuleGap > 20.0 ||
                geometry.WindowPadding < 0.0)
            {
                result.Errors.Add("Размеры капсул должны быть положительными, интервал — от 0 до 20 px.");
            }

            if (geometry.StageHoverDelayMilliseconds < 50 ||
                geometry.StageHoverDelayMilliseconds > 1000)
            {
                result.Errors.Add("Задержка выбора стадии должна быть от 50 до 1000 мс.");
            }

            if (!IsValidColor(geometry.CapsuleFillColorHex) ||
                !IsValidColor(geometry.CapsuleBorderColorHex) ||
                !IsValidColor(geometry.CenterFillColorHex) ||
                !IsValidColor(geometry.CenterBorderColorHex))
            {
                result.Errors.Add("Цвета заливки и границ колеса должны быть записаны в формате #RRGGBB.");
            }

            if (geometry.CapsuleFillOpacity < 0.0 || geometry.CapsuleFillOpacity > 1.0 ||
                geometry.CenterFillOpacity < 0.0 || geometry.CenterFillOpacity > 1.0)
            {
                result.Errors.Add("Прозрачность фона капсул и круга должна быть от 0 до 100%.");
            }
        }

        private static void ValidateTrigger(
            WheelTriggerSettings trigger,
            string triggerName,
            WheelSettingsValidationResult result)
        {
            if (trigger == null)
            {
                result.Errors.Add("Не настроен триггер " + triggerName + ".");
                return;
            }

            if (trigger.Type == WheelTriggerType.Keyboard && trigger.VirtualKey <= 0)
            {
                result.Errors.Add("Для клавиатурного триггера " + triggerName + " не выбрана основная клавиша.");
            }
        }

        private static bool AreSameTrigger(
            WheelTriggerSettings first,
            WheelTriggerSettings second)
        {
            if (first == null || second == null || first.Type != second.Type)
            {
                return false;
            }

            if (first.Type != WheelTriggerType.Keyboard)
            {
                return true;
            }

            return first.VirtualKey == second.VirtualKey && first.Modifiers == second.Modifiers;
        }

        private static void ValidateProfile(
            WheelProfile profile,
            ISet<string> profileIds,
            ISet<string> commandIds,
            WheelSettingsValidationResult result)
        {
            if (profile == null)
            {
                result.Errors.Add("Обнаружен пустой профиль.");
                return;
            }

            if (string.IsNullOrWhiteSpace(profile.Id) || !profileIds.Add(profile.Id))
            {
                result.Errors.Add("ID профилей должны быть заполнены и уникальны.");
            }

            if (string.IsNullOrWhiteSpace(profile.Name))
            {
                result.Errors.Add("У каждого профиля должно быть название.");
            }

            if (profile.SectorCount < WheelGeometryCalculator.MinimumCommandSectorCount ||
                profile.SectorCount > WheelGeometryCalculator.MaximumSectorCount)
            {
                result.Errors.Add("Профиль «" + profile.Name + "» должен содержать от 4 до 12 позиций команд.");
            }

            if (profile.Slots == null || profile.Slots.Count != profile.SectorCount)
            {
                result.Errors.Add("Количество слотов профиля «" + profile.Name + "» не совпадает с числом позиций.");
                return;
            }

            if (!IsValidColor(profile.ColorHex))
            {
                result.Errors.Add("Некорректный цвет профиля «" + profile.Name + "».");
            }

            HashSet<string> usedCommands = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (WheelSlot slot in profile.Slots)
            {
                if (slot == null || string.IsNullOrWhiteSpace(slot.CommandId))
                {
                    continue;
                }

                if (!commandIds.Contains(slot.CommandId))
                {
                    result.Errors.Add("Позиция профиля «" + profile.Name + "» ссылается на неизвестную команду.");
                }

                if (!usedCommands.Add(slot.CommandId))
                {
                    result.Warnings.Add("Команда повторяется в профиле «" + profile.Name + "».");
                }
            }
        }

        private static bool IsValidColor(string color)
        {
            if (string.IsNullOrWhiteSpace(color) || color.Length != 7 || color[0] != '#')
            {
                return false;
            }

            int parsed;
            return int.TryParse(color.Substring(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out parsed);
        }

        private static void NormalizePostableCommandCatalog(WheelSettings settings)
        {
            HashSet<string> assignedCommandIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (WheelProfile profile in settings.Profiles)
            {
                if (profile?.Slots == null)
                {
                    continue;
                }

                foreach (WheelSlot slot in profile.Slots)
                {
                    if (slot != null && !string.IsNullOrWhiteSpace(slot.CommandId))
                    {
                        assignedCommandIds.Add(slot.CommandId);
                    }
                }
            }

            Dictionary<string, CommandDescriptor> commandsByApiName =
                new Dictionary<string, CommandDescriptor>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, string> replacementIds =
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            List<CommandDescriptor> normalizedCatalog = new List<CommandDescriptor>();

            foreach (CommandDescriptor command in settings.CommandCatalog)
            {
                if (command == null || command.Source != WheelCommandSource.RevitPostable)
                {
                    if (command != null)
                    {
                        normalizedCatalog.Add(command);
                    }

                    continue;
                }

                string originalApiName = command.RevitPostableCommandName ?? string.Empty;
                string normalizedApiName = RevitPostableCommandCatalog.NormalizeApiName(originalApiName);
                string russianName;
                string russianDescription;
                bool isLocalized = RevitPostableCommandCatalog.TryGetRussianText(
                    normalizedApiName,
                    out russianName,
                    out russianDescription);

                // Old auto-generated entries are removed when they are not used in any profile.
                if (!isLocalized && !assignedCommandIds.Contains(command.Id ?? string.Empty))
                {
                    continue;
                }

                command.RevitPostableCommandName = normalizedApiName;
                if (isLocalized)
                {
                    command.DisplayName = russianName;
                    command.Description = russianDescription;
                }

                CommandDescriptor existing;
                if (commandsByApiName.TryGetValue(normalizedApiName, out existing))
                {
                    if (!string.IsNullOrWhiteSpace(command.Id) &&
                        !string.Equals(command.Id, existing.Id, StringComparison.OrdinalIgnoreCase))
                    {
                        replacementIds[command.Id] = existing.Id;
                    }

                    continue;
                }

                commandsByApiName[normalizedApiName] = command;
                normalizedCatalog.Add(command);
            }

            settings.CommandCatalog = normalizedCatalog;
            foreach (WheelProfile profile in settings.Profiles)
            {
                if (profile?.Slots == null)
                {
                    continue;
                }

                foreach (WheelSlot slot in profile.Slots)
                {
                    if (slot == null || string.IsNullOrWhiteSpace(slot.CommandId))
                    {
                        continue;
                    }

                    string replacementId;
                    if (replacementIds.TryGetValue(slot.CommandId, out replacementId))
                    {
                        slot.CommandId = replacementId;
                    }

                    CommandDescriptor linkedCommand = normalizedCatalog.FirstOrDefault(
                        item => string.Equals(item.Id, slot.CommandId, StringComparison.OrdinalIgnoreCase));
                    if (linkedCommand != null &&
                        string.Equals(linkedCommand.RevitPostableCommandName, "ArchitecturalWall", StringComparison.OrdinalIgnoreCase) &&
                        (string.Equals(slot.DisplayName, "Стена", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(slot.DisplayName, "Wall", StringComparison.OrdinalIgnoreCase)))
                    {
                        slot.DisplayName = linkedCommand.DisplayName;
                        slot.ShortLabel = linkedCommand.DisplayName;
                        slot.ToolTip = linkedCommand.Description;
                    }
                }
            }
        }

    }
}
