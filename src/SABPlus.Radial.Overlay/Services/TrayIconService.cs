using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Forms = System.Windows.Forms;

namespace SABPlus.Radial.Overlay.Services
{
    public sealed class TrayIconService : IDisposable
    {
        private readonly Forms.NotifyIcon _notifyIcon;
        private readonly Icon _applicationIcon;

        public event EventHandler OpenSettingsRequested;

        public event EventHandler ExitRequested;

        public TrayIconService()
        {
            Forms.ContextMenuStrip menu = new Forms.ContextMenuStrip();
            Forms.ToolStripMenuItem settingsItem = new Forms.ToolStripMenuItem("Настройка колеса");
            Forms.ToolStripMenuItem exitItem = new Forms.ToolStripMenuItem("Выход");

            settingsItem.Click += (sender, args) => Dispatch(() => OpenSettingsRequested?.Invoke(this, EventArgs.Empty));
            exitItem.Click += (sender, args) => Dispatch(() => ExitRequested?.Invoke(this, EventArgs.Empty));

            menu.Items.Add(settingsItem);
            menu.Items.Add(new Forms.ToolStripSeparator());
            menu.Items.Add(exitItem);

            _applicationIcon = LoadApplicationIcon();
            _notifyIcon = new Forms.NotifyIcon
            {
                Icon = _applicationIcon,
                Text = "SAB+ Радиальное колесо",
                ContextMenuStrip = menu,
                Visible = true
            };
            _notifyIcon.DoubleClick += (sender, args) => Dispatch(() => OpenSettingsRequested?.Invoke(this, EventArgs.Empty));
        }

        public void ShowError(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            _notifyIcon.BalloonTipTitle = "SAB+ Радиальное колесо";
            _notifyIcon.BalloonTipText = message.Length > 240 ? message.Substring(0, 240) : message;
            _notifyIcon.BalloonTipIcon = Forms.ToolTipIcon.Warning;
            _notifyIcon.ShowBalloonTip(2500);
        }

        public void Dispose()
        {
            _notifyIcon.Visible = false;
            _notifyIcon.ContextMenuStrip?.Dispose();
            _notifyIcon.Dispose();
            _applicationIcon?.Dispose();
        }

        private static Icon LoadApplicationIcon()
        {
            Assembly assembly = typeof(TrayIconService).Assembly;
            using (Stream stream = assembly.GetManifestResourceStream("SABPlus.Assets.CommandRing_32.png"))
            {
                if (stream == null)
                {
                    return (Icon)SystemIcons.Application.Clone();
                }

                using (Bitmap bitmap = new Bitmap(stream))
                {
                    IntPtr iconHandle = bitmap.GetHicon();
                    try
                    {
                        using (Icon temporaryIcon = Icon.FromHandle(iconHandle))
                        {
                            return (Icon)temporaryIcon.Clone();
                        }
                    }
                    finally
                    {
                        DestroyIcon(iconHandle);
                    }
                }
            }
        }

        private static void Dispatch(Action action)
        {
            System.Windows.Application application = System.Windows.Application.Current;
            if (application?.Dispatcher == null)
            {
                return;
            }

            application.Dispatcher.BeginInvoke(action);
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr iconHandle);
    }
}
