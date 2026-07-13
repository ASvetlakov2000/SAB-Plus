using SABPlus.Radial.Core.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace SABPlus.Radial.Overlay.Services
{
    public sealed class WheelCapsuleItemMetrics
    {
        public int SourceIndex { get; }

        public string Text { get; }

        public double Width { get; }

        public WheelCapsuleItemMetrics(int sourceIndex, string text, double width)
        {
            SourceIndex = sourceIndex;
            Text = text ?? string.Empty;
            Width = width;
        }
    }

    public static class WheelCapsuleMetricsService
    {
        private const double TextSize = 12.0;
        private const double MinimumWidthFactor = 1.8;
        private const double MaximumCapsuleWidth = 280.0;

        public static IReadOnlyList<WheelCapsuleItemMetrics> CreateStageItems(
            IReadOnlyList<WheelProfile> profiles,
            WheelGeometrySettings geometry)
        {
            List<WheelCapsuleItemMetrics> items = new List<WheelCapsuleItemMetrics>();
            if (profiles == null || geometry == null)
            {
                return items;
            }

            for (int index = 0; index < profiles.Count; index++)
            {
                WheelProfile profile = profiles[index];
                string text = profile?.Name ?? string.Empty;
                items.Add(new WheelCapsuleItemMetrics(
                    index,
                    text,
                    MeasureCapsuleWidth(text, geometry)));
            }

            return items;
        }

        public static IReadOnlyList<WheelCapsuleItemMetrics> CreateCommandItems(
            WheelSettings settings,
            WheelProfile profile)
        {
            List<WheelCapsuleItemMetrics> items = new List<WheelCapsuleItemMetrics>();
            if (settings?.Geometry == null || profile?.Slots == null)
            {
                return items;
            }

            for (int slotIndex = 0; slotIndex < profile.Slots.Count; slotIndex++)
            {
                WheelSlot slot = profile.Slots[slotIndex];
                if (slot == null || string.IsNullOrWhiteSpace(slot.CommandId))
                {
                    continue;
                }

                CommandDescriptor command = ResolveCommand(settings, slot.CommandId);
                string text = FirstNotEmpty(
                    slot.ShortLabel,
                    slot.DisplayName,
                    command?.DisplayName);
                items.Add(new WheelCapsuleItemMetrics(
                    slotIndex,
                    text,
                    MeasureCapsuleWidth(text, settings.Geometry)));
            }

            return items;
        }

        public static IReadOnlyList<double> GetWidths(
            IReadOnlyList<WheelCapsuleItemMetrics> items)
        {
            List<double> widths = new List<double>();
            if (items == null)
            {
                return widths;
            }

            for (int index = 0; index < items.Count; index++)
            {
                widths.Add(items[index].Width);
            }

            return widths;
        }

        public static double GetTextEndPadding(WheelGeometrySettings geometry)
        {
            if (geometry == null)
            {
                throw new ArgumentNullException(nameof(geometry));
            }

            return (geometry.CapsuleHeight / 2.0) + 4.0;
        }

        private static double MeasureCapsuleWidth(
            string text,
            WheelGeometrySettings geometry)
        {
            FormattedText formattedText = new FormattedText(
                text ?? string.Empty,
                CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight,
                new Typeface(
                    new FontFamily("Segoe UI"),
                    FontStyles.Normal,
                    FontWeights.SemiBold,
                    FontStretches.Normal),
                TextSize,
                Brushes.White,
                1.0);

            double width = Math.Ceiling(
                formattedText.WidthIncludingTrailingWhitespace +
                (GetTextEndPadding(geometry) * 2.0));
            double minimumWidth = geometry.CapsuleHeight * MinimumWidthFactor;
            return Math.Min(MaximumCapsuleWidth, Math.Max(minimumWidth, width));
        }

        private static CommandDescriptor ResolveCommand(
            WheelSettings settings,
            string commandId)
        {
            if (settings.CommandCatalog == null || string.IsNullOrWhiteSpace(commandId))
            {
                return null;
            }

            foreach (CommandDescriptor command in settings.CommandCatalog)
            {
                if (command != null &&
                    string.Equals(command.Id, commandId, StringComparison.OrdinalIgnoreCase))
                {
                    return command;
                }
            }

            return null;
        }

        private static string FirstNotEmpty(params string[] values)
        {
            if (values == null)
            {
                return string.Empty;
            }

            foreach (string value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            return string.Empty;
        }
    }
}
