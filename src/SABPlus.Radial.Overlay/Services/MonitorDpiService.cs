using SABPlus.Radial.Overlay.Interop;
using System;

namespace SABPlus.Radial.Overlay.Services
{
    internal sealed class MonitorPlacement
    {
        internal double DpiScale { get; }

        internal NativeMethods.Rect WorkArea { get; }

        internal MonitorPlacement(double dpiScale, NativeMethods.Rect workArea)
        {
            DpiScale = dpiScale;
            WorkArea = workArea;
        }
    }

    internal static class MonitorDpiService
    {
        internal static MonitorPlacement GetPlacement(int physicalX, int physicalY)
        {
            NativeMethods.Point point = new NativeMethods.Point(physicalX, physicalY);
            IntPtr monitor = NativeMethods.MonitorFromPoint(point, NativeMethods.MonitorDefaultToNearest);

            NativeMethods.MonitorInfo monitorInfo = new NativeMethods.MonitorInfo
            {
                Size = System.Runtime.InteropServices.Marshal.SizeOf(typeof(NativeMethods.MonitorInfo))
            };

            NativeMethods.GetMonitorInfo(monitor, ref monitorInfo);

            double dpiScale = 1.0;
            try
            {
                uint dpiX;
                uint dpiY;
                if (NativeMethods.GetDpiForMonitor(monitor, 0, out dpiX, out dpiY) == 0 && dpiX > 0)
                {
                    dpiScale = dpiX / 96.0;
                }
            }
            catch (DllNotFoundException)
            {
                dpiScale = 1.0;
            }

            return new MonitorPlacement(dpiScale, monitorInfo.WorkArea);
        }
    }
}
