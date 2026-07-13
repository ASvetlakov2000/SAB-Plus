using SABPlus.Radial.Core.Geometry;
using SABPlus.Radial.Core.Models;
using SABPlus.Radial.Overlay.Interop;
using SABPlus.Radial.Overlay.Services;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace SABPlus.Radial.Overlay.UI.Views
{
    public partial class RadialOverlayWindow : Window
    {
        private IntPtr _windowHandle;

        public RadialOverlayWindow()
        {
            InitializeComponent();
            SourceInitialized += Window_SourceInitialized;
        }

        public void Configure(
            WheelSettings settings,
            WheelDisplayLevel displayLevel,
            WheelProfile activeProfile,
            IReadOnlyList<WheelProfile> stageProfiles,
            bool showStageCapsules,
            bool reserveExpandedCommandRing,
            string selectedStageProfileId)
        {
            WheelControl.Settings = settings;
            WheelControl.DisplayLevel = displayLevel;
            WheelControl.ActiveProfile = activeProfile;
            WheelControl.StageProfiles = stageProfiles;
            WheelControl.ShowStageCapsules = showStageCapsules;
            WheelControl.ReserveExpandedCommandRing = reserveExpandedCommandRing;
            WheelControl.SelectedStageProfileId = selectedStageProfileId;
            WheelControl.HitResult = new WheelHitResult(WheelHitKind.Cancel, -1, 0.0, 0.0);

            IReadOnlyList<WheelCapsuleItemMetrics> stageItems =
                displayLevel == WheelDisplayLevel.Stages ||
                showStageCapsules ||
                reserveExpandedCommandRing
                    ? WheelCapsuleMetricsService.CreateStageItems(stageProfiles, settings.Geometry)
                    : new List<WheelCapsuleItemMetrics>();
            IReadOnlyList<WheelCapsuleItemMetrics> commandItems =
                displayLevel == WheelDisplayLevel.Commands
                    ? WheelCapsuleMetricsService.CreateCommandItems(settings, activeProfile)
                    : new List<WheelCapsuleItemMetrics>();
            double size = WheelGeometryCalculator.GetWindowSize(
                settings.Geometry,
                WheelCapsuleMetricsService.GetWidths(stageItems),
                WheelCapsuleMetricsService.GetWidths(commandItems));
            Width = size;
            Height = size;
        }

        public void UpdateHit(WheelHitResult hitResult)
        {
            WheelControl.HitResult = hitResult;
        }

        public void ShowAtPhysicalCenter(int physicalX, int physicalY, double dpiScale)
        {
            if (!IsVisible)
            {
                Show();
            }

            double effectiveScale = dpiScale <= 0.0 ? 1.0 : dpiScale;
            int physicalWidth = (int)Math.Round(Width * effectiveScale);
            int physicalHeight = (int)Math.Round(Height * effectiveScale);
            int left = physicalX - (physicalWidth / 2);
            int top = physicalY - (physicalHeight / 2);

            NativeMethods.SetWindowPos(
                _windowHandle,
                new IntPtr(-1),
                left,
                top,
                physicalWidth,
                physicalHeight,
                NativeMethods.SwpNoActivate | NativeMethods.SwpShowWindow);
        }

        public void HideOverlay()
        {
            if (IsVisible)
            {
                Hide();
            }
        }

        private void Window_SourceInitialized(object sender, EventArgs e)
        {
            _windowHandle = new WindowInteropHelper(this).Handle;
            IntPtr extendedStyle = NativeMethods.GetWindowLongPtr(_windowHandle, NativeMethods.GwlExStyle);
            long styleValue = extendedStyle.ToInt64() |
                              NativeMethods.WsExNoActivate |
                              NativeMethods.WsExToolWindow;
            NativeMethods.SetWindowLongPtr(_windowHandle, NativeMethods.GwlExStyle, new IntPtr(styleValue));
            RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;
        }
    }
}
