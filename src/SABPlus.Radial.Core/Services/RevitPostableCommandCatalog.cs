using SABPlus.Radial.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace SABPlus.Radial.Core.Services
{
    public static class RevitPostableCommandCatalog
    {
        private sealed class LocalizedCommand
        {
            public string DisplayName { get; }

            public string Description { get; }

            public LocalizedCommand(string displayName, string description)
            {
                DisplayName = displayName;
                Description = description;
            }
        }

        private static readonly Dictionary<string, string> LegacyAliases =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "Wall", "ArchitecturalWall" },
                { "Component", "PlaceAComponent" },
                { "VisibilityGraphics", "VisibilityOrGraphics" }
            };

        private static readonly Dictionary<string, LocalizedCommand> Commands =
            new Dictionary<string, LocalizedCommand>(StringComparer.OrdinalIgnoreCase)
            {
                { "ArchitecturalWall", new LocalizedCommand("Архитектурная стена", "Создание архитектурной стены") },
                { "StructuralWall", new LocalizedCommand("Несущая стена", "Создание несущей стены") },
                { "WallByFaceWall", new LocalizedCommand("Стена по грани", "Создание стены по выбранной грани") },
                { "WallOpening", new LocalizedCommand("Проём в стене", "Создание прямоугольного проёма в стене") },
                { "CurtainWallMullion", new LocalizedCommand("Импост витража", "Размещение импоста витражной системы") },
                { "Door", new LocalizedCommand("Дверь", "Размещение двери") },
                { "Window", new LocalizedCommand("Окно", "Размещение окна") },
                { "PlaceAComponent", new LocalizedCommand("Компонент", "Размещение компонента") },
                { "Beam", new LocalizedCommand("Балка", "Размещение несущей балки") },
                { "Stair", new LocalizedCommand("Лестница", "Создание лестницы") },
                { "Railing", new LocalizedCommand("Ограждение", "Создание ограждения") },
                { "Ramp", new LocalizedCommand("Пандус", "Создание пандуса") },
                { "Room", new LocalizedCommand("Помещение", "Размещение помещения") },
                { "RoomTag", new LocalizedCommand("Марка помещения", "Маркировка помещения") },
                { "RoomSeparator", new LocalizedCommand("Разделитель помещений", "Создание линии разделения помещений") },
                { "Area", new LocalizedCommand("Область", "Размещение области") },
                { "AreaTag", new LocalizedCommand("Марка области", "Маркировка области") },
                { "Level", new LocalizedCommand("Уровень", "Создание уровня") },
                { "Grid", new LocalizedCommand("Ось", "Создание координационной оси") },
                { "Move", new LocalizedCommand("Переместить", "Перемещение выбранных элементов") },
                { "Copy", new LocalizedCommand("Копировать", "Копирование выбранных элементов") },
                { "Align", new LocalizedCommand("Выровнять", "Выравнивание элементов") },
                { "Rotate", new LocalizedCommand("Повернуть", "Поворот выбранных элементов") },
                { "MirrorPickAxis", new LocalizedCommand("Зеркально — выбрать ось", "Отражение элементов относительно выбранной оси") },
                { "MirrorDrawAxis", new LocalizedCommand("Зеркально — нарисовать ось", "Отражение элементов относительно построенной оси") },
                { "Offset", new LocalizedCommand("Смещение", "Создание копии со смещением") },
                { "Array", new LocalizedCommand("Массив", "Создание массива элементов") },
                { "Delete", new LocalizedCommand("Удалить", "Удаление выбранных элементов") },
                { "Pin", new LocalizedCommand("Закрепить", "Закрепление выбранных элементов") },
                { "Unpin", new LocalizedCommand("Открепить", "Снятие закрепления с выбранных элементов") },
                { "CreateSimilar", new LocalizedCommand("Создать аналог", "Создание элемента, аналогичного выбранному") },
                { "VisibilityOrGraphics", new LocalizedCommand("Видимость и графика", "Открытие параметров видимости и графики вида") },
                { "ThinLines", new LocalizedCommand("Тонкие линии", "Переключение отображения тонких линий") },
                { "Text", new LocalizedCommand("Текст", "Создание текстового примечания") },
                { "AlignedDimension", new LocalizedCommand("Параллельный размер", "Создание параллельного линейного размера") },
                { "DetailLine", new LocalizedCommand("Линия детализации", "Создание линии детализации") },
                { "ModelLine", new LocalizedCommand("Линия модели", "Создание линии модели") },
                { "Default3DView", new LocalizedCommand("3D-вид по умолчанию", "Открытие стандартного ортогонального 3D-вида") },
                { "Section", new LocalizedCommand("Разрез", "Создание разреза") },
                { "FloorPlan", new LocalizedCommand("План этажа", "Создание плана этажа") },
                { "ProjectBrowser", new LocalizedCommand("Диспетчер проекта", "Показ или скрытие диспетчера проекта") }
            };

        private static readonly Dictionary<string, string> RussianTokens =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "Add", "добавить" }, { "Align", "выровнять" }, { "All", "все" },
                { "Annotation", "аннотация" }, { "Architectural", "архитектурная" },
                { "Area", "область" }, { "Array", "массив" }, { "Attach", "присоединить" },
                { "Beam", "балка" }, { "Browser", "диспетчер" }, { "By", "по" },
                { "Callout", "фрагмент" }, { "Ceiling", "потолок" }, { "Change", "изменить" },
                { "Column", "колонна" }, { "Command", "команда" }, { "Component", "компонент" },
                { "Copy", "копировать" }, { "Create", "создать" }, { "Curtain", "витражная" },
                { "Delete", "удалить" }, { "Detail", "деталь" }, { "Dimension", "размер" },
                { "Door", "дверь" }, { "Draw", "нарисовать" }, { "Duplicate", "дублировать" },
                { "Edit", "редактировать" }, { "Element", "элемент" }, { "Elevation", "фасад" },
                { "Export", "экспорт" }, { "Face", "грани" }, { "Family", "семейство" },
                { "Filter", "фильтр" }, { "Floor", "пол" }, { "Graphics", "графика" },
                { "Grid", "ось" }, { "Group", "группа" }, { "Hide", "скрыть" },
                { "Import", "импорт" }, { "In", "в" }, { "Insert", "вставить" },
                { "Join", "соединить" }, { "Level", "уровень" }, { "Line", "линия" },
                { "Link", "связь" }, { "Load", "загрузить" }, { "Manage", "управление" },
                { "Material", "материал" }, { "Mirror", "зеркально" }, { "Model", "модель" },
                { "Move", "переместить" }, { "Mullion", "импост" }, { "New", "новый" },
                { "Offset", "смещение" }, { "Opening", "проём" }, { "Options", "параметры" },
                { "Panel", "панель" }, { "Paste", "вставить" }, { "Pick", "выбрать" },
                { "Pin", "закрепить" }, { "Place", "разместить" }, { "Plan", "план" },
                { "Project", "проекта" }, { "Properties", "свойства" }, { "Railing", "ограждение" },
                { "Ramp", "пандус" }, { "Redo", "повторить" }, { "Reference", "опорная" },
                { "Remove", "удалить" }, { "Rename", "переименовать" }, { "Reveal", "показать" },
                { "Roof", "крыша" }, { "Room", "помещение" }, { "Rotate", "повернуть" },
                { "Save", "сохранить" }, { "Schedule", "спецификация" }, { "Section", "разрез" },
                { "Select", "выбрать" }, { "Sheet", "лист" }, { "Similar", "аналог" },
                { "Stair", "лестница" }, { "Structural", "несущая" }, { "System", "система" },
                { "Tag", "марка" }, { "Text", "текст" }, { "Thin", "тонкие" },
                { "To", "в" }, { "Type", "тип" }, { "Undo", "отменить" },
                { "Unhide", "показать" }, { "Unpin", "открепить" }, { "View", "вид" },
                { "Visibility", "видимость" }, { "Wall", "стена" }, { "Window", "окно" },
                { "Work", "рабочая" }, { "Workset", "рабочий набор" }, { "Zoom", "масштаб" },
                { "3D", "3D" }, { "IFC", "IFC" }, { "PDF", "PDF" }, { "DWG", "DWG" }
            };

        public static string NormalizeApiName(string apiName)
        {
            if (string.IsNullOrWhiteSpace(apiName))
            {
                return string.Empty;
            }

            string normalized = apiName.Trim();
            string replacement;
            return LegacyAliases.TryGetValue(normalized, out replacement)
                ? replacement
                : normalized;
        }

        public static bool TryGetRussianText(
            string apiName,
            out string displayName,
            out string description)
        {
            string normalized = NormalizeApiName(apiName);
            LocalizedCommand command;
            if (Commands.TryGetValue(normalized, out command))
            {
                displayName = command.DisplayName;
                description = command.Description;
                return true;
            }

            displayName = string.Empty;
            description = string.Empty;
            return false;
        }

        public static string GetRussianDisplayName(string apiName, string revitRibbonLabel)
        {
            string normalized = NormalizeApiName(apiName);
            if (!string.IsNullOrWhiteSpace(revitRibbonLabel))
            {
                return revitRibbonLabel.Trim();
            }

            string displayName;
            string description;
            if (TryGetRussianText(normalized, out displayName, out description))
            {
                return displayName;
            }

            return CreateRussianFallbackName(normalized);
        }

        public static CommandDescriptor CreateDescriptor(string apiName)
        {
            return CreateDescriptor(apiName, string.Empty);
        }

        public static CommandDescriptor CreateDescriptor(string apiName, string revitRibbonLabel)
        {
            string normalized = NormalizeApiName(apiName);
            string displayName = GetRussianDisplayName(normalized, revitRibbonLabel);
            string localizedName;
            string localizedDescription;
            string description = TryGetRussianText(normalized, out localizedName, out localizedDescription)
                ? localizedDescription
                : "Системная команда Revit. API: " + normalized + ".";

            return new CommandDescriptor
            {
                Id = "postable." + normalized.ToLowerInvariant(),
                Source = WheelCommandSource.RevitPostable,
                DisplayName = displayName,
                Description = description,
                RevitPostableCommandName = normalized
            };
        }

        private static string CreateRussianFallbackName(string apiName)
        {
            if (string.IsNullOrWhiteSpace(apiName))
            {
                return "Команда Revit";
            }

            string[] tokens = Regex.Split(
                apiName,
                @"(?<=[a-z0-9])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])");
            List<string> translatedTokens = new List<string>();
            foreach (string token in tokens)
            {
                if (string.IsNullOrWhiteSpace(token))
                {
                    continue;
                }

                string translated;
                translatedTokens.Add(RussianTokens.TryGetValue(token, out translated)
                    ? translated
                    : TransliterateToRussian(token));
            }

            string result = string.Join(" ", translatedTokens).Trim();
            return result.Length == 0
                ? "Команда Revit"
                : char.ToUpperInvariant(result[0]) + result.Substring(1);
        }

        private static string TransliterateToRussian(string value)
        {
            Dictionary<string, string> combinations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "sch", "щ" }, { "sh", "ш" }, { "ch", "ч" }, { "th", "т" },
                { "ph", "ф" }, { "ck", "к" }, { "qu", "кв" }, { "ya", "я" },
                { "yu", "ю" }, { "yo", "ё" }, { "zh", "ж" }
            };
            Dictionary<char, string> characters = new Dictionary<char, string>
            {
                { 'a', "а" }, { 'b', "б" }, { 'c', "к" }, { 'd', "д" }, { 'e', "е" },
                { 'f', "ф" }, { 'g', "г" }, { 'h', "х" }, { 'i', "и" }, { 'j', "й" },
                { 'k', "к" }, { 'l', "л" }, { 'm', "м" }, { 'n', "н" }, { 'o', "о" },
                { 'p', "п" }, { 'q', "к" }, { 'r', "р" }, { 's', "с" }, { 't', "т" },
                { 'u', "у" }, { 'v', "в" }, { 'w', "в" }, { 'x', "кс" }, { 'y', "ы" },
                { 'z', "з" }
            };

            StringBuilder builder = new StringBuilder();
            string lower = value.ToLowerInvariant();
            int index = 0;
            while (index < lower.Length)
            {
                bool combinationAdded = false;
                foreach (KeyValuePair<string, string> pair in combinations)
                {
                    if (index + pair.Key.Length <= lower.Length &&
                        string.Equals(
                            lower.Substring(index, pair.Key.Length),
                            pair.Key,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        builder.Append(pair.Value);
                        index += pair.Key.Length;
                        combinationAdded = true;
                        break;
                    }
                }

                if (combinationAdded)
                {
                    continue;
                }

                string translatedCharacter;
                builder.Append(characters.TryGetValue(lower[index], out translatedCharacter)
                    ? translatedCharacter
                    : lower[index].ToString());
                index++;
            }

            return builder.ToString();
        }
    }
}
