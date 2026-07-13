using SABPlus.Radial.Core.Services;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace SABPlus.Radial.Overlay.Services
{
    public sealed class EditorWindowLayout
    {
        public double Width { get; set; }

        public double Height { get; set; }

        public double CommandCatalogFraction { get; set; }

        public EditorWindowLayout()
        {
            Width = 1440.0;
            Height = 860.0;
            CommandCatalogFraction = 0.60;
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

        public void Restore(
            Window window,
            RowDefinition commandCatalogRow,
            RowDefinition selectedPositionRow)
        {
            if (window == null || !_store.Exists())
            {
                return;
            }

            EditorWindowLayout layout = _store.Load();
            window.Width = Math.Max(window.MinWidth, layout.Width);
            window.Height = Math.Max(window.MinHeight, layout.Height);
            if (commandCatalogRow != null && selectedPositionRow != null)
            {
                double fraction = Math.Max(0.35, Math.Min(0.80, layout.CommandCatalogFraction));
                commandCatalogRow.Height = new GridLength(fraction, GridUnitType.Star);
                selectedPositionRow.Height = new GridLength(1.0 - fraction, GridUnitType.Star);
            }
        }

        public void Save(
            Window window,
            RowDefinition commandCatalogRow,
            RowDefinition selectedPositionRow)
        {
            if (window == null || window.WindowState != WindowState.Normal)
            {
                return;
            }

            double catalogFraction = 0.60;
            if (commandCatalogRow != null && selectedPositionRow != null)
            {
                double totalHeight = commandCatalogRow.ActualHeight + selectedPositionRow.ActualHeight;
                if (totalHeight > 0.0)
                {
                    catalogFraction = commandCatalogRow.ActualHeight / totalHeight;
                }
            }

            _store.SaveAtomic(new EditorWindowLayout
            {
                Width = window.ActualWidth,
                Height = window.ActualHeight,
                CommandCatalogFraction = Math.Max(0.35, Math.Min(0.80, catalogFraction))
            });
        }

        private static void Validate(EditorWindowLayout layout)
        {
            if (layout == null || layout.Width < 900.0 || layout.Height < 600.0)
            {
                throw new InvalidDataException("Некорректные размеры окна редактора.");
            }

            if (layout.CommandCatalogFraction <= 0.0)
            {
                layout.CommandCatalogFraction = 0.60;
            }
        }
    }
}
