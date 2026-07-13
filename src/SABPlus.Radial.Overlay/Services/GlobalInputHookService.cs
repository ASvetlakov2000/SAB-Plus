using SABPlus.Radial.Core.Models;
using SABPlus.Radial.Core.Services;
using SABPlus.Radial.Overlay.Interop;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SABPlus.Radial.Overlay.Services
{
    public sealed class GlobalPointEventArgs : EventArgs
    {
        public int X { get; }

        public int Y { get; }

        public WheelTriggerAction Action { get; }

        public GlobalPointEventArgs(int x, int y, WheelTriggerAction action)
        {
            X = x;
            Y = y;
            Action = action;
        }
    }

    public delegate bool GlobalTriggerPressedEventHandler(object sender, GlobalPointEventArgs e);

    public sealed class GlobalInputHookService : IDisposable
    {
        private readonly NativeMethods.HookProcedure _mouseProcedure;
        private readonly NativeMethods.HookProcedure _keyboardProcedure;

        private IntPtr _mouseHook;
        private IntPtr _keyboardHook;
        private WheelTriggerSettings _stageTrigger;
        private WheelTriggerSettings _commandTrigger;
        private bool _triggerHeld;
        private WheelTriggerAction _heldAction;
        private int _heldVirtualKey;

        public event GlobalTriggerPressedEventHandler TriggerPressed;

        public event EventHandler<GlobalPointEventArgs> TriggerReleased;

        public event EventHandler<GlobalPointEventArgs> CursorMoved;

        public Func<bool> EscapeHandler { get; set; }

        public GlobalInputHookService(
            WheelTriggerSettings stageTrigger,
            WheelTriggerSettings commandTrigger)
        {
            UpdateTriggers(stageTrigger, commandTrigger);
            _mouseProcedure = MouseHookCallback;
            _keyboardProcedure = KeyboardHookCallback;
        }

        public void Start()
        {
            if (_mouseHook != IntPtr.Zero || _keyboardHook != IntPtr.Zero)
            {
                return;
            }

            using (Process process = Process.GetCurrentProcess())
            using (ProcessModule module = process.MainModule)
            {
                IntPtr moduleHandle = NativeMethods.GetModuleHandle(module.ModuleName);
                _mouseHook = NativeMethods.SetWindowsHookEx(
                    NativeMethods.WhMouseLowLevel,
                    _mouseProcedure,
                    moduleHandle,
                    0);

                _keyboardHook = NativeMethods.SetWindowsHookEx(
                    NativeMethods.WhKeyboardLowLevel,
                    _keyboardProcedure,
                    moduleHandle,
                    0);
            }

            if (_mouseHook == IntPtr.Zero || _keyboardHook == IntPtr.Zero)
            {
                Dispose();
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Не удалось установить глобальные хуки ввода.");
            }
        }

        public void UpdateTriggers(
            WheelTriggerSettings stageTrigger,
            WheelTriggerSettings commandTrigger)
        {
            _stageTrigger = JsonSerialization.DeepClone(
                stageTrigger ?? throw new ArgumentNullException(nameof(stageTrigger)));
            _commandTrigger = JsonSerialization.DeepClone(
                commandTrigger ?? throw new ArgumentNullException(nameof(commandTrigger)));
            ResetHeldState();
        }

        public void Dispose()
        {
            if (_mouseHook != IntPtr.Zero)
            {
                NativeMethods.UnhookWindowsHookEx(_mouseHook);
                _mouseHook = IntPtr.Zero;
            }

            if (_keyboardHook != IntPtr.Zero)
            {
                NativeMethods.UnhookWindowsHookEx(_keyboardHook);
                _keyboardHook = IntPtr.Zero;
            }

            ResetHeldState();
        }

        private IntPtr MouseHookCallback(int code, IntPtr wParam, IntPtr lParam)
        {
            try
            {
                if (code >= 0)
                {
                    int message = wParam.ToInt32();
                    NativeMethods.MouseHookData data = Marshal.PtrToStructure<NativeMethods.MouseHookData>(lParam);

                    if (message == NativeMethods.WmMouseMove && _triggerHeld)
                    {
                        CursorMoved?.Invoke(
                            this,
                            new GlobalPointEventArgs(data.Point.X, data.Point.Y, _heldAction));
                    }

                    if (message == NativeMethods.WmXButtonDown || message == NativeMethods.WmXButtonUp)
                    {
                        int xButton = (int)((data.MouseData >> 16) & 0xFFFF);
                        WheelTriggerAction? action = GetMouseAction(xButton);

                        if (message == NativeMethods.WmXButtonDown && !_triggerHeld && action.HasValue)
                        {
                            GlobalPointEventArgs pressedArgs = new GlobalPointEventArgs(
                                data.Point.X,
                                data.Point.Y,
                                action.Value);
                            if (RaiseTriggerPressed(pressedArgs))
                            {
                                _triggerHeld = true;
                                _heldAction = action.Value;
                                _heldVirtualKey = 0;
                                return new IntPtr(1);
                            }
                        }

                        if (message == NativeMethods.WmXButtonUp &&
                            _triggerHeld &&
                            _heldVirtualKey == 0 &&
                            action.HasValue &&
                            action.Value == _heldAction)
                        {
                            WheelTriggerAction releasedAction = _heldAction;
                            ResetHeldState();
                            TriggerReleased?.Invoke(
                                this,
                                new GlobalPointEventArgs(data.Point.X, data.Point.Y, releasedAction));
                            return new IntPtr(1);
                        }
                    }
                }
            }
            catch
            {
                // A global hook must not destabilize other applications.
            }

            return NativeMethods.CallNextHookEx(_mouseHook, code, wParam, lParam);
        }

        private IntPtr KeyboardHookCallback(int code, IntPtr wParam, IntPtr lParam)
        {
            try
            {
                if (code >= 0)
                {
                    int message = wParam.ToInt32();
                    NativeMethods.KeyboardHookData data = Marshal.PtrToStructure<NativeMethods.KeyboardHookData>(lParam);
                    int virtualKey = (int)data.VirtualKey;

                    bool isKeyDown = message == NativeMethods.WmKeyDown || message == NativeMethods.WmSysKeyDown;
                    bool isKeyUp = message == NativeMethods.WmKeyUp || message == NativeMethods.WmSysKeyUp;

                    if (virtualKey == NativeMethods.VkEscape && isKeyDown)
                    {
                        if (EscapeHandler != null && EscapeHandler())
                        {
                            return new IntPtr(1);
                        }
                    }

                    WheelTriggerAction? action = GetKeyboardAction(virtualKey);
                    if (isKeyDown && !_triggerHeld && action.HasValue)
                    {
                        GlobalPointEventArgs pressedArgs = GetCurrentCursorEventArgs(action.Value);
                        if (RaiseTriggerPressed(pressedArgs))
                        {
                            _triggerHeld = true;
                            _heldAction = action.Value;
                            _heldVirtualKey = virtualKey;
                            return new IntPtr(1);
                        }
                    }

                    if (isKeyUp && _triggerHeld && virtualKey == _heldVirtualKey)
                    {
                        WheelTriggerAction releasedAction = _heldAction;
                        ResetHeldState();
                        TriggerReleased?.Invoke(this, GetCurrentCursorEventArgs(releasedAction));
                        return new IntPtr(1);
                    }
                }
            }
            catch
            {
                // A global hook must not destabilize other applications.
            }

            return NativeMethods.CallNextHookEx(_keyboardHook, code, wParam, lParam);
        }

        private WheelTriggerAction? GetMouseAction(int xButton)
        {
            if (MatchesMouseTrigger(_stageTrigger, xButton))
            {
                return WheelTriggerAction.Stages;
            }

            if (MatchesMouseTrigger(_commandTrigger, xButton))
            {
                return WheelTriggerAction.Commands;
            }

            return null;
        }

        private WheelTriggerAction? GetKeyboardAction(int virtualKey)
        {
            if (MatchesKeyboardTrigger(_stageTrigger, virtualKey))
            {
                return WheelTriggerAction.Stages;
            }

            if (MatchesKeyboardTrigger(_commandTrigger, virtualKey))
            {
                return WheelTriggerAction.Commands;
            }

            return null;
        }

        private static bool MatchesMouseTrigger(WheelTriggerSettings trigger, int xButton)
        {
            return (trigger.Type == WheelTriggerType.MouseXButton1 && xButton == 1) ||
                   (trigger.Type == WheelTriggerType.MouseXButton2 && xButton == 2);
        }

        private static bool MatchesKeyboardTrigger(WheelTriggerSettings trigger, int virtualKey)
        {
            return trigger.Type == WheelTriggerType.Keyboard &&
                   trigger.VirtualKey == virtualKey &&
                   AreRequiredModifiersPressed(trigger.Modifiers);
        }

        private bool RaiseTriggerPressed(GlobalPointEventArgs eventArgs)
        {
            GlobalTriggerPressedEventHandler handlers = TriggerPressed;
            if (handlers == null)
            {
                return false;
            }

            bool accepted = false;
            foreach (GlobalTriggerPressedEventHandler handler in handlers.GetInvocationList())
            {
                accepted = handler(this, eventArgs) || accepted;
            }

            return accepted;
        }

        private static bool AreRequiredModifiersPressed(WheelKeyboardModifiers required)
        {
            if (required.HasFlag(WheelKeyboardModifiers.Control) && !IsKeyPressed(NativeMethods.VkControl))
            {
                return false;
            }

            if (required.HasFlag(WheelKeyboardModifiers.Shift) && !IsKeyPressed(NativeMethods.VkShift))
            {
                return false;
            }

            if (required.HasFlag(WheelKeyboardModifiers.Alt) && !IsKeyPressed(NativeMethods.VkMenu))
            {
                return false;
            }

            if (required.HasFlag(WheelKeyboardModifiers.Windows) &&
                !IsKeyPressed(NativeMethods.VkLwin) &&
                !IsKeyPressed(NativeMethods.VkRwin))
            {
                return false;
            }

            return true;
        }

        private static bool IsKeyPressed(int virtualKey)
        {
            return (NativeMethods.GetAsyncKeyState(virtualKey) & 0x8000) != 0;
        }

        private static GlobalPointEventArgs GetCurrentCursorEventArgs(WheelTriggerAction action)
        {
            NativeMethods.Point point;
            if (!NativeMethods.GetCursorPos(out point))
            {
                return new GlobalPointEventArgs(0, 0, action);
            }

            return new GlobalPointEventArgs(point.X, point.Y, action);
        }

        private void ResetHeldState()
        {
            _triggerHeld = false;
            _heldAction = WheelTriggerAction.Stages;
            _heldVirtualKey = 0;
        }
    }
}
