using SABPlus.Radial.Core.Services;
using System;
using System.IO;
using System.Windows;

namespace SABPlus.Radial.Overlay.Services
{
    public sealed class EditorWindowLayout
    {
        public double Width { get; set; }

        public double Height { get; set; }

        public EditorWindowLayout()
        {
            Width = 1440.0;
            Height = 860.0;
        }
    }

    public sealed class WindowLayoutService
    {
        private readonly AtomicJsonFileStore<EditorWindowLayout> _store;

        public WindowLayoutService(string settingsDirectory)
        {
            _store = new AtomicJsonFileStore<EditorWindowLayout>(
                Path.Combine(settingsDirectory, "editor-window.json"),
                Validate);
        }

        public void Restore(Window window)
        {
            if (window == null || !_store.Exists())
            {
                return;
            }

            EditorWindowLayout layout = _store.Load();
            window.Width = Math.Max(window.MinWidth, layout.Width);
            window.Height = Math.Max(window.MinHeight, layout.Height);
        }

        public void Save(Window window)
        {
            if (window == null || window.WindowState != WindowState.Normal)
            {
                return;
            }

            _store.SaveAtomic(new EditorWindowLayout
            {
                Width = window.ActualWidth,
                Height = window.ActualHeight
            });
        }

        private static void Validate(EditorWindowLayout layout)
        {
            if (layout == null || layout.Width < 900.0 || layout.Height < 600.0)
            {
                throw new InvalidDataException("Некорректные размеры окна редактора.");
            }
        }
    }
}
