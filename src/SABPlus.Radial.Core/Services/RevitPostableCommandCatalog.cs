using SABPlus.Radial.Core.Models;
using System;
using System.Collections.Generic;

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

        public static CommandDescriptor CreateDescriptor(string apiName)
        {
            string normalized = NormalizeApiName(apiName);
            string displayName;
            string description;
            if (!TryGetRussianText(normalized, out displayName, out description))
            {
                throw new ArgumentException("Для команды Revit отсутствует русское название: " + normalized, nameof(apiName));
            }

            return new CommandDescriptor
            {
                Id = "postable." + normalized.ToLowerInvariant(),
                Source = WheelCommandSource.RevitPostable,
                DisplayName = displayName,
                Description = description,
                RevitPostableCommandName = normalized
            };
        }
    }
}
