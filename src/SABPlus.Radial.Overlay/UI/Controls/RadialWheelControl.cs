using SABPlus.Radial.Core.Geometry;
using SABPlus.Radial.Core.Models;
using SABPlus.Radial.Overlay.Services;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace SABPlus.Radial.Overlay.UI.Controls
{
    public sealed class RadialWheelControl : FrameworkElement
    {
        private enum CapsuleContentFlow
        {
            Centered = 0,
            FromLeft = 1,
            FromRight = 2
        }

        private static readonly Brush CancelHoverBrush = CreateBrush(Color.FromRgb(185, 48, 48));
        private static readonly Brush SecondaryTextBrush = CreateBrush(Color.FromRgb(183, 190, 201));

        private WheelSettings _settings;
        private WheelDisplayLevel _displayLevel;
        private WheelProfile _activeProfile;
        private IReadOnlyList<WheelProfile> _stageProfiles;
        private WheelHitResult _hitResult;
        private bool _showStageCapsules;
        private bool _reserveExpandedCommandRing;
        private string _selectedStageProfileId;

        public WheelSettings Settings
        {
            get => _settings;
            set
            {
                _settings = value;
                InvalidateMeasure();
                InvalidateVisual();
            }
        }

        public WheelDisplayLevel DisplayLevel
        {
            get => _displayLevel;
            set
            {
                _displayLevel = value;
                InvalidateMeasure();
                InvalidateVisual();
            }
        }

        public WheelProfile ActiveProfile
        {
            get => _activeProfile;
            set
            {
                _activeProfile = value;
                InvalidateMeasure();
                InvalidateVisual();
            }
        }

        public IReadOnlyList<WheelProfile> StageProfiles
        {
            get => _stageProfiles;
            set
            {
                _stageProfiles = value;
                InvalidateMeasure();
                InvalidateVisual();
            }
        }

        public WheelHitResult HitResult
        {
            get => _hitResult;
            set
            {
                _hitResult = value;
                InvalidateVisual();
            }
        }

        public bool ShowStageCapsules
        {
            get => _showStageCapsules;
            set
            {
                _showStageCapsules = value;
                InvalidateMeasure();
                InvalidateVisual();
            }
        }

        public bool ReserveExpandedCommandRing
        {
            get => _reserveExpandedCommandRing;
            set
            {
                _reserveExpandedCommandRing = value;
                InvalidateMeasure();
                InvalidateVisual();
            }
        }

        public string SelectedStageProfileId
        {
            get => _selectedStageProfileId;
            set
            {
                _selectedStageProfileId = value ?? string.Empty;
                InvalidateVisual();
            }
        }

        public RadialWheelControl()
        {
            SnapsToDevicePixels = true;
            UseLayoutRounding = true;
            Focusable = false;
            _stageProfiles = new List<WheelProfile>();
            _selectedStageProfileId = string.Empty;
        }

        public WheelHitResult HitTestPoint(Point point)
        {
            if (_settings?.Geometry == null)
            {
                return new WheelHitResult(WheelHitKind.None, -1, 0.0, 0.0);
            }

            int itemCount = GetHitTestItemCount();
            if (itemCount <= 0)
            {
                return new WheelHitResult(WheelHitKind.None, -1, 0.0, 0.0);
            }

            double deltaX = point.X - (ActualWidth / 2.0);
            double deltaY = point.Y - (ActualHeight / 2.0);
            WheelHitResult hit = WheelGeometryCalculator.HitTest(
                deltaX,
                deltaY,
                itemCount,
                _displayLevel,
                _settings.Geometry);

            if (_displayLevel == WheelDisplayLevel.Commands &&
                hit.Kind == WheelHitKind.Sector)
            {
                IReadOnlyList<int> slotIndexes = GetAssignedCommandSlotIndexes();
                if (hit.SectorIndex >= 0 && hit.SectorIndex < slotIndexes.Count)
                {
                    return new WheelHitResult(
                        hit.Kind,
                        slotIndexes[hit.SectorIndex],
                        hit.Distance,
                        hit.AngleDegrees);
                }
            }

            return hit;
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            if (_settings?.Geometry == null)
            {
                return new Size(640.0, 640.0);
            }

            IReadOnlyList<WheelCapsuleItemMetrics> stageItems = ShouldDrawStages()
                ? WheelCapsuleMetricsService.CreateStageItems(_stageProfiles, _settings.Geometry)
                : new List<WheelCapsuleItemMetrics>();
            IReadOnlyList<WheelCapsuleItemMetrics> commandItems = ShouldDrawCommands()
                ? WheelCapsuleMetricsService.CreateCommandItems(_settings, _activeProfile)
                : new List<WheelCapsuleItemMetrics>();
            double size = WheelGeometryCalculator.GetWindowSize(
                _settings.Geometry,
                WheelCapsuleMetricsService.GetWidths(stageItems),
                WheelCapsuleMetricsService.GetWidths(commandItems));
            return new Size(size, size);
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);

            if (_settings?.Geometry == null)
            {
                return;
            }

            Point center = new Point(ActualWidth / 2.0, ActualHeight / 2.0);
            if (ShouldDrawStages())
            {
                DrawStageCapsules(drawingContext, center);
            }

            if (ShouldDrawCommands())
            {
                DrawCommandCapsules(drawingContext, center);
            }

            DrawCenter(drawingContext, center);
            DrawDirectionIndicator(drawingContext, center);
        }

        private void DrawStageCapsules(DrawingContext drawingContext, Point center)
        {
            IReadOnlyList<WheelCapsuleItemMetrics> items =
                WheelCapsuleMetricsService.CreateStageItems(_stageProfiles, _settings.Geometry);
            if (items.Count <= 0)
            {
                return;
            }

            IReadOnlyList<WheelCapsuleLayout> layouts = WheelGeometryCalculator.CreateCapsuleLayouts(
                WheelCapsuleMetricsService.GetWidths(items),
                WheelCapsuleLevel.Stage,
                _settings.Geometry,
                0.0);

            foreach (WheelCapsuleLayout layout in layouts)
            {
                WheelCapsuleItemMetrics item = items[layout.Index];
                WheelProfile profile = GetStageProfile(item.SourceIndex);
                if (profile == null)
                {
                    continue;
                }

                bool hovered = _displayLevel == WheelDisplayLevel.Stages &&
                               IsHoveredPosition(item.SourceIndex);
                bool selected = string.Equals(
                    profile.Id,
                    _selectedStageProfileId,
                    StringComparison.OrdinalIgnoreCase);
                DrawCapsule(
                    drawingContext,
                    center,
                    layout,
                    item.Text,
                    profile.ColorHex,
                    hovered,
                    selected);
            }
        }

        private void DrawCommandCapsules(DrawingContext drawingContext, Point center)
        {
            IReadOnlyList<WheelCapsuleItemMetrics> items =
                WheelCapsuleMetricsService.CreateCommandItems(_settings, _activeProfile);
            if (items.Count <= 0 || _activeProfile == null)
            {
                return;
            }

            double minimumRadius = 0.0;
            int stageCount = GetStageCount();
            if (_reserveExpandedCommandRing && stageCount > 0)
            {
                minimumRadius = WheelGeometryCalculator.GetExpandedCommandRadius(
                    stageCount,
                    items.Count,
                    _settings.Geometry);
            }

            IReadOnlyList<WheelCapsuleLayout> layouts = WheelGeometryCalculator.CreateCapsuleLayouts(
                WheelCapsuleMetricsService.GetWidths(items),
                WheelCapsuleLevel.Command,
                _settings.Geometry,
                minimumRadius);

            foreach (WheelCapsuleLayout layout in layouts)
            {
                if (layout.Index < 0 || layout.Index >= items.Count)
                {
                    continue;
                }

                WheelCapsuleItemMetrics item = items[layout.Index];
                bool hovered = _displayLevel == WheelDisplayLevel.Commands &&
                               IsHoveredPosition(item.SourceIndex);

                DrawCapsule(
                    drawingContext,
                    center,
                    layout,
                    item.Text,
                    _activeProfile.ColorHex,
                    hovered,
                    false);
            }
        }

        private void DrawCapsule(
            DrawingContext drawingContext,
            Point wheelCenter,
            WheelCapsuleLayout layout,
            string label,
            string accentColorHex,
            bool hovered,
            bool selected)
        {
            Point capsuleCenter = new Point(
                wheelCenter.X + layout.Center.X,
                wheelCenter.Y + layout.Center.Y);
            Rect rect = new Rect(
                capsuleCenter.X - (layout.Width / 2.0),
                capsuleCenter.Y - (layout.Height / 2.0),
                layout.Width,
                layout.Height);
            Color accentColor = ParseColor(accentColorHex, Color.FromRgb(15, 108, 189), 255);
            Color capsuleColor = ParseColor(
                _settings.Geometry.CapsuleFillColorHex,
                Color.FromRgb(31, 35, 43),
                ToAlpha(_settings.Geometry.CapsuleFillOpacity));
            Color capsuleBorderColor = ParseColor(
                _settings.Geometry.CapsuleBorderColorHex,
                Color.FromRgb(92, 99, 112),
                255);
            Brush fill = hovered
                ? CreateBrush(Color.FromArgb(
                    ToAlpha(_settings.Geometry.CapsuleFillOpacity),
                    accentColor.R,
                    accentColor.G,
                    accentColor.B))
                : CreateBrush(capsuleColor);
            Pen border = selected || hovered
                ? new Pen(CreateBrush(accentColor), selected ? 2.0 : 1.5)
                : new Pen(CreateBrush(capsuleBorderColor), 1.0);
            double endRadius = layout.Height / 2.0;

            drawingContext.DrawRoundedRectangle(
                fill,
                border,
                rect,
                endRadius,
                endRadius);

            DrawCapsuleContent(
                drawingContext,
                rect,
                layout.CenterAngleDegrees,
                label);
        }

        private void DrawCapsuleContent(
            DrawingContext drawingContext,
            Rect rect,
            double centerAngleDegrees,
            string label)
        {
            CapsuleContentFlow flow = GetCapsuleContentFlow(centerAngleDegrees);
            FormattedText text = CreateFormattedText(label, 12.0, FontWeights.SemiBold, Brushes.White);
            text.MaxLineCount = 1;
            text.Trimming = TextTrimming.CharacterEllipsis;

            double endPadding = WheelCapsuleMetricsService.GetTextEndPadding(_settings.Geometry);
            double textLeft = rect.Left + endPadding;
            double textWidth = Math.Max(12.0, rect.Width - (endPadding * 2.0));
            text.TextAlignment = flow == CapsuleContentFlow.FromRight
                ? TextAlignment.Right
                : flow == CapsuleContentFlow.Centered
                    ? TextAlignment.Center
                    : TextAlignment.Left;
            text.MaxTextWidth = textWidth;
            drawingContext.DrawText(
                text,
                new Point(textLeft, rect.Top + ((rect.Height - text.Height) / 2.0)));
        }

        private static CapsuleContentFlow GetCapsuleContentFlow(double centerAngleDegrees)
        {
            double radians = centerAngleDegrees * Math.PI / 180.0;
            double horizontalDirection = Math.Cos(radians);
            if (Math.Abs(horizontalDirection) < 0.5)
            {
                return CapsuleContentFlow.Centered;
            }

            return horizontalDirection > 0.0
                ? CapsuleContentFlow.FromLeft
                : CapsuleContentFlow.FromRight;
        }

        private void DrawCenter(DrawingContext drawingContext, Point center)
        {
            WheelGeometrySettings geometry = _settings.Geometry;
            Color centerFillColor = ParseColor(
                geometry.CenterFillColorHex,
                Color.FromRgb(24, 28, 35),
                ToAlpha(geometry.CenterFillOpacity));
            Color centerBorderColor = ParseColor(
                geometry.CenterBorderColorHex,
                Color.FromRgb(15, 108, 189),
                255);
            Pen outerPen = new Pen(CreateBrush(centerBorderColor), 2.0);
            drawingContext.DrawEllipse(
                CreateBrush(centerFillColor),
                outerPen,
                center,
                geometry.CenterRingOuterRadius,
                geometry.CenterRingOuterRadius);

            bool cancelHovered = _hitResult != null && _hitResult.Kind == WheelHitKind.Cancel;
            Brush innerFill = cancelHovered
                ? CancelHoverBrush
                : CreateBrush(centerFillColor);
            drawingContext.DrawEllipse(
                innerFill,
                null,
                center,
                geometry.CancelRadius,
                geometry.CancelRadius);

            FormattedText closeText = CreateFormattedText("×", 19.0, FontWeights.Normal, Brushes.White);
            drawingContext.DrawText(
                closeText,
                new Point(center.X - (closeText.Width / 2.0), center.Y - (closeText.Height / 2.0) - 1.0));

            string centerLabel = _displayLevel == WheelDisplayLevel.Stages
                ? "СТАДИИ"
                : FirstNotEmpty(_activeProfile?.Name, "КОМАНДЫ").ToUpperInvariant();
            FormattedText labelText = CreateFormattedText(centerLabel, 9.0, FontWeights.SemiBold, SecondaryTextBrush);
            labelText.MaxTextWidth = (geometry.CenterRingOuterRadius * 2.0) - 14.0;
            labelText.MaxLineCount = 1;
            labelText.Trimming = TextTrimming.CharacterEllipsis;
            labelText.TextAlignment = TextAlignment.Center;
            drawingContext.DrawText(
                labelText,
                new Point(center.X - (labelText.MaxTextWidth / 2.0), center.Y + geometry.CancelRadius + 8.0));
        }

        private void DrawDirectionIndicator(DrawingContext drawingContext, Point center)
        {
            if (_hitResult == null || _hitResult.Distance <= _settings.Geometry.CenterRingOuterRadius)
            {
                return;
            }

            double endRadius = Math.Min(
                _hitResult.Distance,
                _displayLevel == WheelDisplayLevel.Stages
                    ? _settings.Geometry.StageCapsuleRadius - 28.0
                    : _settings.Geometry.CommandActivationRadius - 10.0);
            if (endRadius <= _settings.Geometry.CenterRingOuterRadius)
            {
                return;
            }

            PointD startOffset = WheelGeometryCalculator.PointOnCircle(
                _hitResult.AngleDegrees,
                _settings.Geometry.CenterRingOuterRadius + 5.0);
            PointD endOffset = WheelGeometryCalculator.PointOnCircle(
                _hitResult.AngleDegrees,
                endRadius);
            Color accentColor = ParseColor(
                _activeProfile?.ColorHex,
                Color.FromRgb(15, 108, 189),
                190);
            Pen indicatorPen = new Pen(CreateBrush(accentColor), 1.5);
            drawingContext.DrawLine(
                indicatorPen,
                new Point(center.X + startOffset.X, center.Y + startOffset.Y),
                new Point(center.X + endOffset.X, center.Y + endOffset.Y));
        }

        private int GetHitTestItemCount()
        {
            return _displayLevel == WheelDisplayLevel.Stages
                ? GetStageCount()
                : GetCommandCount();
        }

        private int GetStageCount()
        {
            return _stageProfiles?.Count ?? 0;
        }

        private int GetCommandCount()
        {
            return GetAssignedCommandSlotIndexes().Count;
        }

        private IReadOnlyList<int> GetAssignedCommandSlotIndexes()
        {
            return WheelGeometryCalculator.GetAssignedCommandSlotIndexes(_activeProfile);
        }

        private bool ShouldDrawStages()
        {
            return _displayLevel == WheelDisplayLevel.Stages || _showStageCapsules;
        }

        private bool ShouldDrawCommands()
        {
            return _displayLevel == WheelDisplayLevel.Commands && _activeProfile != null;
        }

        private bool IsHoveredPosition(int index)
        {
            return _hitResult != null &&
                   _hitResult.Kind == WheelHitKind.Sector &&
                   _hitResult.SectorIndex == index;
        }

        private WheelProfile GetStageProfile(int index)
        {
            return _stageProfiles != null && index >= 0 && index < _stageProfiles.Count
                ? _stageProfiles[index]
                : null;
        }

        private FormattedText CreateFormattedText(
            string text,
            double size,
            FontWeight fontWeight,
            Brush brush)
        {
            return new FormattedText(
                text ?? string.Empty,
                CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, fontWeight, FontStretches.Normal),
                size,
                brush,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);
        }

        private static Brush CreateBrush(Color color)
        {
            SolidColorBrush brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }

        private static byte ToAlpha(double opacity)
        {
            double normalized = Math.Max(0.0, Math.Min(1.0, opacity));
            return (byte)Math.Round(normalized * 255.0);
        }

        private static Color ParseColor(string colorHex, Color fallback, byte alpha)
        {
            try
            {
                Color color = (Color)ColorConverter.ConvertFromString(colorHex);
                color.A = alpha;
                return color;
            }
            catch
            {
                fallback.A = alpha;
                return fallback;
            }
        }

        private static string FirstNotEmpty(params string[] values)
        {
            return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
        }

    }
}
