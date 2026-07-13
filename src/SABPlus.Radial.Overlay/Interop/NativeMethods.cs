using System;
using System.Runtime.InteropServices;

namespace SABPlus.Radial.Overlay.Interop
{
    internal static class NativeMethods
    {
        internal const int WhKeyboardLowLevel = 13;
        internal const int WhMouseLowLevel = 14;

        internal const int WmMouseMove = 0x0200;
        internal const int WmXButtonDown = 0x020B;
        internal const int WmXButtonUp = 0x020C;
        internal const int WmKeyDown = 0x0100;
        internal const int WmKeyUp = 0x0101;
        internal const int WmSysKeyDown = 0x0104;
        internal const int WmSysKeyUp = 0x0105;

        internal const int VkEscape = 0x1B;
        internal const int VkControl = 0x11;
        internal const int VkShift = 0x10;
        internal const int VkMenu = 0x12;
        internal const int VkLwin = 0x5B;
        internal const int VkRwin = 0x5C;

        internal const int GwlExStyle = -20;
        internal const long WsExNoActivate = 0x08000000L;
        internal const long WsExToolWindow = 0x00000080L;

        internal const uint SwpNoActivate = 0x0010;
        internal const uint SwpShowWindow = 0x0040;
        internal const uint MonitorDefaultToNearest = 0x00000002;

        internal delegate IntPtr HookProcedure(int code, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        internal struct Point
        {
            internal int X;
            internal int Y;

            internal Point(int x, int y)
            {
                X = x;
                Y = y;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct Rect
        {
            internal int Left;
            internal int Top;
            internal int Right;
            internal int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct MonitorInfo
        {
            internal int Size;
            internal Rect Monitor;
            internal Rect WorkArea;
            internal uint Flags;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct MouseHookData
        {
            internal Point Point;
            internal uint MouseData;
            internal uint Flags;
            internal uint Time;
            internal IntPtr ExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct KeyboardHookData
        {
            internal uint VirtualKey;
            internal uint ScanCode;
            internal uint Flags;
            internal uint Time;
            internal IntPtr ExtraInfo;
        }

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern IntPtr SetWindowsHookEx(
            int hookId,
            HookProcedure hookProcedure,
            IntPtr moduleHandle,
            uint threadId);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool UnhookWindowsHookEx(IntPtr hookHandle);

        [DllImport("user32.dll")]
        internal static extern IntPtr CallNextHookEx(
            IntPtr hookHandle,
            int code,
            IntPtr wParam,
            IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern IntPtr GetModuleHandle(string moduleName);

        [DllImport("user32.dll")]
        internal static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        internal static extern uint GetWindowThreadProcessId(IntPtr windowHandle, out uint processId);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetCursorPos(out Point point);

        [DllImport("user32.dll")]
        internal static extern short GetAsyncKeyState(int virtualKey);

        [DllImport("user32.dll")]
        internal static extern IntPtr MonitorFromPoint(Point point, uint flags);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetMonitorInfo(IntPtr monitorHandle, ref MonitorInfo monitorInfo);

        [DllImport("shcore.dll")]
        internal static extern int GetDpiForMonitor(
            IntPtr monitorHandle,
            int dpiType,
            out uint dpiX,
            out uint dpiY);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr", SetLastError = true)]
        private static extern IntPtr GetWindowLongPtr64(IntPtr windowHandle, int index);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr", SetLastError = true)]
        private static extern IntPtr SetWindowLongPtr64(IntPtr windowHandle, int index, IntPtr value);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetWindowPos(
            IntPtr windowHandle,
            IntPtr insertAfter,
            int x,
            int y,
            int width,
            int height,
            uint flags);

        internal static IntPtr GetWindowLongPtr(IntPtr windowHandle, int index)
        {
            return GetWindowLongPtr64(windowHandle, index);
        }

        internal static IntPtr SetWindowLongPtr(IntPtr windowHandle, int index, IntPtr value)
        {
            return SetWindowLongPtr64(windowHandle, index, value);
        }
    }
}
