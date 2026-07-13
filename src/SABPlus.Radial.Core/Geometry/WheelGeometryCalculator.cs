using SABPlus.Radial.Core.Models;
using System;
using System.Collections.Generic;

namespace SABPlus.Radial.Core.Geometry
{
    public enum WheelHitKind
    {
        None = 0,
        Cancel = 1,
        ChangeStage = 2,
        Sector = 3
    }

    public enum WheelCapsuleLevel
    {
        Stage = 0,
        Command = 1
    }

    public struct PointD
    {
        public double X { get; }

        public double Y { get; }

        public PointD(double x, double y)
        {
            X = x;
            Y = y;
        }
    }

    public sealed class WheelHitResult
    {
        public WheelHitKind Kind { get; }

        public int SectorIndex { get; }

        public double Distance { get; }

        public double AngleDegrees { get; }

        public WheelHitResult(WheelHitKind kind, int sectorIndex, double distance, double angleDegrees)
        {
            Kind = kind;
            SectorIndex = sectorIndex;
            Distance = distance;
            AngleDegrees = angleDegrees;
        }
    }

    public sealed class WheelCapsuleLayout
    {
        public int Index { get; }

        public double CenterAngleDegrees { get; }

        public PointD Center { get; }

        public double Radius { get; }

        public double Width { get; }

        public double Height { get; }

        public WheelCapsuleLayout(
            int index,
            double centerAngleDegrees,
            PointD center,
            double radius,
            double width,
            double height)
        {
            Index = index;
            CenterAngleDegrees = centerAngleDegrees;
            Center = center;
            Radius = radius;
            Width = width;
            Height = height;
        }
    }

    public static class WheelGeometryCalculator
    {
        public const int MinimumSectorCount = 1;
        public const int MinimumCommandSectorCount = 4;
        public const int MaximumSectorCount = 12;

        public static IReadOnlyList<int> GetAssignedCommandSlotIndexes(WheelProfile profile)
        {
            List<int> indexes = new List<int>();
            if (profile?.Slots == null)
            {
                return indexes;
            }

            for (int index = 0; index < profile.Slots.Count; index++)
            {
                WheelSlot slot = profile.Slots[index];
                if (slot != null && !string.IsNullOrWhiteSpace(slot.CommandId))
                {
                    indexes.Add(index);
                }
            }

            return indexes;
        }

        public static WheelHitResult HitTest(
            double deltaX,
            double deltaY,
            int sectorCount,
            WheelDisplayLevel displayLevel,
            WheelGeometrySettings settings)
        {
            ValidateArguments(sectorCount, settings);

            double distance = Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
            double angle = NormalizeDegrees(Math.Atan2(deltaY, deltaX) * 180.0 / Math.PI);

            if (distance <= settings.CancelRadius)
            {
                return new WheelHitResult(WheelHitKind.Cancel, -1, distance, angle);
            }

            if (displayLevel == WheelDisplayLevel.Stages)
            {
                if (distance < settings.StageActivationRadius)
                {
                    return new WheelHitResult(WheelHitKind.None, -1, distance, angle);
                }
            }
            else
            {
                if (distance <= settings.CenterRingOuterRadius)
                {
                    return new WheelHitResult(WheelHitKind.Cancel, -1, distance, angle);
                }

                // Command capsules are visual targets only. The actual hit area starts shortly
                // outside the center and continues through the whole angular sector, so the user
                // does not have to place the cursor exactly over a capsule.
                double commandSelectionStartRadius = GetCommandSelectionStartRadius(settings);
                if (distance < commandSelectionStartRadius)
                {
                    return new WheelHitResult(WheelHitKind.None, -1, distance, angle);
                }
            }

            int sectorIndex = GetSectorIndex(angle, sectorCount);
            return new WheelHitResult(WheelHitKind.Sector, sectorIndex, distance, angle);
        }

        public static double GetCommandSelectionStartRadius(WheelGeometrySettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            double comfortableStartRadius = settings.CenterRingOuterRadius + 24.0;
            return Math.Min(settings.CommandActivationRadius, comfortableStartRadius);
        }

        public static int GetSectorIndex(double normalizedAngleDegrees, int sectorCount)
        {
            if (sectorCount < MinimumSectorCount || sectorCount > MaximumSectorCount)
            {
                throw new ArgumentOutOfRangeException(nameof(sectorCount));
            }

            double sectorAngle = 360.0 / sectorCount;

            // Position zero is always centered at the top, matching Blender's directional layout.
            double firstBoundary = -90.0 - (sectorAngle / 2.0);
            double relativeAngle = NormalizeDegrees(normalizedAngleDegrees - firstBoundary);
            int index = (int)Math.Floor(relativeAngle / sectorAngle);

            return index >= sectorCount ? 0 : index;
        }

        public static bool IsInsideReturnToStagesZone(
            double deltaXFromStageCenter,
            double deltaYFromStageCenter,
            WheelGeometrySettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            double distance = Math.Sqrt(
                (deltaXFromStageCenter * deltaXFromStageCenter) +
                (deltaYFromStageCenter * deltaYFromStageCenter));
            return distance <= settings.ReturnToStagesRadius;
        }

        public static IReadOnlyList<WheelCapsuleLayout> CreateCapsuleLayouts(
            int itemCount,
            WheelCapsuleLevel level,
            WheelGeometrySettings settings,
            double minimumRadius)
        {
            ValidateArguments(itemCount, settings);

            List<WheelCapsuleLayout> layouts = new List<WheelCapsuleLayout>();
            double itemAngle = 360.0 / itemCount;
            double width = GetCapsuleWidth(itemCount, level, settings);
            double baseRadius = Math.Max(GetCapsuleRadius(itemCount, level, settings), minimumRadius);

            for (int index = 0; index < itemCount; index++)
            {
                double centerAngle = -90.0 + (index * itemAngle);
                double radius = GetEqualClearanceCenterRadius(
                    baseRadius,
                    centerAngle,
                    width,
                    settings.CapsuleHeight);
                PointD center = PointOnCircle(centerAngle, radius);
                layouts.Add(new WheelCapsuleLayout(
                    index,
                    centerAngle,
                    center,
                    radius,
                    width,
                    settings.CapsuleHeight));
            }

            return layouts;
        }

        public static IReadOnlyList<WheelCapsuleLayout> CreateCapsuleLayouts(
            IReadOnlyList<double> capsuleWidths,
            WheelCapsuleLevel level,
            WheelGeometrySettings settings,
            double minimumRadius)
        {
            ValidateCapsuleWidths(capsuleWidths, settings);

            int itemCount = capsuleWidths.Count;
            List<WheelCapsuleLayout> layouts = new List<WheelCapsuleLayout>();
            double itemAngle = 360.0 / itemCount;
            double baseRadius = Math.Max(GetCapsuleRadius(capsuleWidths, level, settings), minimumRadius);

            for (int index = 0; index < itemCount; index++)
            {
                double centerAngle = -90.0 + (index * itemAngle);
                double radius = GetEqualClearanceCenterRadius(
                    baseRadius,
                    centerAngle,
                    capsuleWidths[index],
                    settings.CapsuleHeight);
                PointD center = PointOnCircle(centerAngle, radius);
                layouts.Add(new WheelCapsuleLayout(
                    index,
                    centerAngle,
                    center,
                    radius,
                    capsuleWidths[index],
                    settings.CapsuleHeight));
            }

            return layouts;
        }

        public static double GetCapsuleWidth(
            int itemCount,
            WheelCapsuleLevel level,
            WheelGeometrySettings settings)
        {
            ValidateArguments(itemCount, settings);

            if (level == WheelCapsuleLevel.Stage)
            {
                if (itemCount <= 4)
                {
                    return settings.StageCapsuleWidth;
                }

                return itemCount <= 6
                    ? Math.Min(settings.StageCapsuleWidth, 132.0)
                    : Math.Min(settings.StageCapsuleWidth, 118.0);
            }

            if (itemCount <= 6)
            {
                return settings.CommandCapsuleWidth;
            }

            if (itemCount <= 8)
            {
                return Math.Min(settings.CommandCapsuleWidth, 158.0);
            }

            return itemCount <= 10
                ? Math.Min(settings.CommandCapsuleWidth, 140.0)
                : Math.Min(settings.CommandCapsuleWidth, 124.0);
        }

        public static double GetCapsuleRadius(
            int itemCount,
            WheelCapsuleLevel level,
            WheelGeometrySettings settings)
        {
            ValidateArguments(itemCount, settings);

            double configuredRadius = level == WheelCapsuleLevel.Stage
                ? settings.StageCapsuleRadius
                : settings.CommandCapsuleRadius;
            double width = GetCapsuleWidth(itemCount, level, settings);
            double radius = configuredRadius;

            // Horizontal capsules need axis-aligned collision checks. A diagonal-based estimate
            // leaves unnecessary empty space between neighbouring commands.
            for (int attempt = 0; attempt < 300; attempt++)
            {
                if (!HasSameRingCollision(itemCount, radius, width, settings))
                {
                    return radius;
                }

                radius += 2.0;
            }

            throw new InvalidOperationException("Не удалось разместить капсулы кольца без пересечений.");
        }

        public static double GetCapsuleRadius(
            IReadOnlyList<double> capsuleWidths,
            WheelCapsuleLevel level,
            WheelGeometrySettings settings)
        {
            ValidateCapsuleWidths(capsuleWidths, settings);

            double radius = level == WheelCapsuleLevel.Stage
                ? settings.StageCapsuleRadius
                : settings.CommandCapsuleRadius;
            for (int attempt = 0; attempt < 300; attempt++)
            {
                if (!HasSameRingCollision(capsuleWidths, radius, settings))
                {
                    return radius;
                }

                radius += 2.0;
            }

            throw new InvalidOperationException("Не удалось разместить динамические капсулы без пересечений.");
        }

        public static double GetExpandedCommandRadius(
            int stageCount,
            int commandCount,
            WheelGeometrySettings settings)
        {
            ValidateArguments(stageCount, settings);
            ValidateArguments(commandCount, settings);

            double stageRadius = GetCapsuleRadius(stageCount, WheelCapsuleLevel.Stage, settings);
            double stageWidth = GetCapsuleWidth(stageCount, WheelCapsuleLevel.Stage, settings);
            double commandWidth = GetCapsuleWidth(commandCount, WheelCapsuleLevel.Command, settings);
            double commandRadius = GetCapsuleRadius(commandCount, WheelCapsuleLevel.Command, settings);

            // Capsules remain horizontal. Increase only as much as needed to keep both visible rings apart.
            for (int attempt = 0; attempt < 300; attempt++)
            {
                if (!HasCapsuleCollision(
                        stageCount,
                        commandCount,
                        stageRadius,
                        commandRadius,
                        stageWidth,
                        commandWidth,
                        settings))
                {
                    return commandRadius;
                }

                commandRadius += 2.0;
            }

            throw new InvalidOperationException("Не удалось разместить кольца стадий и команд без пересечений.");
        }

        public static double GetWindowSize(
            WheelGeometrySettings settings,
            int stageCount,
            int commandCount,
            bool reserveExpandedCommandRing)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            double maximumExtent = settings.CenterRingOuterRadius;
            if (stageCount > 0)
            {
                IReadOnlyList<WheelCapsuleLayout> stageLayouts = CreateCapsuleLayouts(
                    stageCount,
                    WheelCapsuleLevel.Stage,
                    settings,
                    0.0);
                maximumExtent = Math.Max(maximumExtent, GetMaximumLayoutExtent(stageLayouts));
            }

            if (commandCount > 0)
            {
                double minimumCommandRadius = reserveExpandedCommandRing && stageCount > 0
                    ? GetExpandedCommandRadius(stageCount, commandCount, settings)
                    : 0.0;
                IReadOnlyList<WheelCapsuleLayout> commandLayouts = CreateCapsuleLayouts(
                    commandCount,
                    WheelCapsuleLevel.Command,
                    settings,
                    minimumCommandRadius);
                maximumExtent = Math.Max(maximumExtent, GetMaximumLayoutExtent(commandLayouts));
            }

            return Math.Ceiling((maximumExtent + settings.WindowPadding) * 2.0);
        }

        public static double GetWindowSize(
            WheelGeometrySettings settings,
            IReadOnlyList<double> stageWidths,
            IReadOnlyList<double> commandWidths)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            double maximumExtent = settings.CenterRingOuterRadius;
            if (stageWidths != null && stageWidths.Count > 0)
            {
                IReadOnlyList<WheelCapsuleLayout> stageLayouts = CreateCapsuleLayouts(
                    stageWidths,
                    WheelCapsuleLevel.Stage,
                    settings,
                    0.0);
                maximumExtent = Math.Max(
                    maximumExtent,
                    GetMaximumLayoutExtent(stageLayouts));
            }

            if (commandWidths != null && commandWidths.Count > 0)
            {
                IReadOnlyList<WheelCapsuleLayout> commandLayouts = CreateCapsuleLayouts(
                    commandWidths,
                    WheelCapsuleLevel.Command,
                    settings,
                    0.0);
                maximumExtent = Math.Max(
                    maximumExtent,
                    GetMaximumLayoutExtent(commandLayouts));
            }

            return Math.Ceiling((maximumExtent + settings.WindowPadding) * 2.0);
        }

        public static PointD PointOnCircle(double angleDegrees, double radius)
        {
            double radians = angleDegrees * Math.PI / 180.0;
            return new PointD(Math.Cos(radians) * radius, Math.Sin(radians) * radius);
        }

        public static double GetEqualClearanceCenterRadius(
            double topCapsuleCenterRadius,
            double angleDegrees,
            double capsuleWidth,
            double capsuleHeight)
        {
            if (topCapsuleCenterRadius <= 0.0 || capsuleWidth <= 0.0 || capsuleHeight <= 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(topCapsuleCenterRadius));
            }

            // A horizontal capsule is a line segment with two semicircular ends. Its radial
            // extent is the support function below. Keeping the inner edge radius constant
            // removes the apparent offset difference between top, side and diagonal capsules.
            double radians = angleDegrees * Math.PI / 180.0;
            double halfHeight = capsuleHeight / 2.0;
            double straightHalfLength = Math.Max(0.0, (capsuleWidth - capsuleHeight) / 2.0);
            double radialExtent = halfHeight + (straightHalfLength * Math.Abs(Math.Cos(radians)));
            double innerEdgeRadius = topCapsuleCenterRadius - halfHeight;
            return innerEdgeRadius + radialExtent;
        }

        public static double NormalizeDegrees(double angleDegrees)
        {
            double result = angleDegrees % 360.0;
            return result < 0.0 ? result + 360.0 : result;
        }

        private static double GetMaximumLayoutExtent(
            IReadOnlyList<WheelCapsuleLayout> layouts)
        {
            double maximumExtent = 0.0;
            foreach (WheelCapsuleLayout layout in layouts)
            {
                double horizontalExtent = Math.Abs(layout.Center.X) + (layout.Width / 2.0);
                double verticalExtent = Math.Abs(layout.Center.Y) + (layout.Height / 2.0);
                maximumExtent = Math.Max(
                    maximumExtent,
                    Math.Max(horizontalExtent, verticalExtent));
            }

            return maximumExtent;
        }

        private static bool HasCapsuleCollision(
            int stageCount,
            int commandCount,
            double stageRadius,
            double commandRadius,
            double stageWidth,
            double commandWidth,
            WheelGeometrySettings settings)
        {
            double minimumHorizontalDistance = ((stageWidth + commandWidth) / 2.0) + settings.CapsuleGap;
            double minimumVerticalDistance = settings.CapsuleHeight + settings.CapsuleGap;

            for (int stageIndex = 0; stageIndex < stageCount; stageIndex++)
            {
                double stageAngle = -90.0 + (stageIndex * (360.0 / stageCount));
                double adjustedStageRadius = GetEqualClearanceCenterRadius(
                    stageRadius,
                    stageAngle,
                    stageWidth,
                    settings.CapsuleHeight);
                PointD stageCenter = PointOnCircle(stageAngle, adjustedStageRadius);

                for (int commandIndex = 0; commandIndex < commandCount; commandIndex++)
                {
                    double commandAngle = -90.0 + (commandIndex * (360.0 / commandCount));
                    double adjustedCommandRadius = GetEqualClearanceCenterRadius(
                        commandRadius,
                        commandAngle,
                        commandWidth,
                        settings.CapsuleHeight);
                    PointD commandCenter = PointOnCircle(commandAngle, adjustedCommandRadius);
                    double horizontalDistance = Math.Abs(stageCenter.X - commandCenter.X);
                    double verticalDistance = Math.Abs(stageCenter.Y - commandCenter.Y);

                    if (horizontalDistance < minimumHorizontalDistance &&
                        verticalDistance < minimumVerticalDistance)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool HasSameRingCollision(
            int itemCount,
            double radius,
            double width,
            WheelGeometrySettings settings)
        {
            if (itemCount <= 1)
            {
                return false;
            }

            double minimumHorizontalDistance = width + settings.CapsuleGap;
            double minimumVerticalDistance = settings.CapsuleHeight + settings.CapsuleGap;

            for (int firstIndex = 0; firstIndex < itemCount; firstIndex++)
            {
                double firstAngle = -90.0 + (firstIndex * (360.0 / itemCount));
                double firstRadius = GetEqualClearanceCenterRadius(
                    radius,
                    firstAngle,
                    width,
                    settings.CapsuleHeight);
                PointD firstCenter = PointOnCircle(firstAngle, firstRadius);

                for (int secondIndex = firstIndex + 1; secondIndex < itemCount; secondIndex++)
                {
                    double secondAngle = -90.0 + (secondIndex * (360.0 / itemCount));
                    double secondRadius = GetEqualClearanceCenterRadius(
                        radius,
                        secondAngle,
                        width,
                        settings.CapsuleHeight);
                    PointD secondCenter = PointOnCircle(secondAngle, secondRadius);
                    double horizontalDistance = Math.Abs(firstCenter.X - secondCenter.X);
                    double verticalDistance = Math.Abs(firstCenter.Y - secondCenter.Y);

                    if (horizontalDistance < minimumHorizontalDistance &&
                        verticalDistance < minimumVerticalDistance)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool HasSameRingCollision(
            IReadOnlyList<double> capsuleWidths,
            double radius,
            WheelGeometrySettings settings)
        {
            int itemCount = capsuleWidths.Count;
            if (itemCount <= 1)
            {
                return false;
            }

            double minimumVerticalDistance = settings.CapsuleHeight + settings.CapsuleGap;
            for (int firstIndex = 0; firstIndex < itemCount; firstIndex++)
            {
                double firstAngle = -90.0 + (firstIndex * (360.0 / itemCount));
                double firstRadius = GetEqualClearanceCenterRadius(
                    radius,
                    firstAngle,
                    capsuleWidths[firstIndex],
                    settings.CapsuleHeight);
                PointD firstCenter = PointOnCircle(firstAngle, firstRadius);

                for (int secondIndex = firstIndex + 1; secondIndex < itemCount; secondIndex++)
                {
                    double secondAngle = -90.0 + (secondIndex * (360.0 / itemCount));
                    double secondRadius = GetEqualClearanceCenterRadius(
                        radius,
                        secondAngle,
                        capsuleWidths[secondIndex],
                        settings.CapsuleHeight);
                    PointD secondCenter = PointOnCircle(secondAngle, secondRadius);
                    double minimumHorizontalDistance =
                        ((capsuleWidths[firstIndex] + capsuleWidths[secondIndex]) / 2.0) +
                        settings.CapsuleGap;
                    double horizontalDistance = Math.Abs(firstCenter.X - secondCenter.X);
                    double verticalDistance = Math.Abs(firstCenter.Y - secondCenter.Y);

                    if (horizontalDistance < minimumHorizontalDistance &&
                        verticalDistance < minimumVerticalDistance)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static void ValidateCapsuleWidths(
            IReadOnlyList<double> capsuleWidths,
            WheelGeometrySettings settings)
        {
            if (capsuleWidths == null)
            {
                throw new ArgumentNullException(nameof(capsuleWidths));
            }

            ValidateArguments(capsuleWidths.Count, settings);
            for (int index = 0; index < capsuleWidths.Count; index++)
            {
                if (capsuleWidths[index] <= 0.0 ||
                    double.IsNaN(capsuleWidths[index]) ||
                    double.IsInfinity(capsuleWidths[index]))
                {
                    throw new ArgumentException("Ширина каждой капсулы должна быть положительным числом.", nameof(capsuleWidths));
                }
            }
        }

        private static void ValidateArguments(int itemCount, WheelGeometrySettings settings)
        {
            if (itemCount < MinimumSectorCount || itemCount > MaximumSectorCount)
            {
                throw new ArgumentOutOfRangeException(nameof(itemCount));
            }

            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            if (settings.CancelRadius < 0.0 ||
                settings.CenterRingOuterRadius <= settings.CancelRadius ||
                settings.StageActivationRadius <= settings.CenterRingOuterRadius ||
                settings.ReturnToStagesRadius <= settings.CenterRingOuterRadius ||
                settings.CommandActivationRadius <= settings.ReturnToStagesRadius ||
                settings.StageCapsuleRadius <= settings.StageActivationRadius ||
                settings.CommandCapsuleRadius <= settings.CommandActivationRadius ||
                settings.StageCapsuleWidth <= 0.0 ||
                settings.CommandCapsuleWidth <= 0.0 ||
                settings.CapsuleHeight <= 0.0 ||
                settings.CapsuleCornerRadius < 0.0 ||
                settings.CapsuleIconSize <= 0.0 ||
                settings.CapsuleGap < 0.0 ||
                settings.WindowPadding < 0.0)
            {
                throw new ArgumentException("Некорректные параметры геометрии колеса.", nameof(settings));
            }
        }
    }
}
