using System;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SABPlus.RevitBridge.Services
{
    public static class RibbonIconFactory
    {
        public static BitmapSource CreateWheelIcon(int size)
        {
            if (size <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(size));
            }

            BitmapSource embeddedIcon = LoadEmbeddedIcon(size);
            if (embeddedIcon != null)
            {
                return embeddedIcon;
            }

            DrawingVisual drawingVisual = new DrawingVisual();
            using (DrawingContext context = drawingVisual.RenderOpen())
            {
                double center = size / 2.0;
                double centerRadius = size * 0.18;
                double capsuleWidth = size * 0.29;
                double capsuleHeight = Math.Max(2.0, size * 0.12);
                double capsuleRadius = size * 0.31;
                Brush accentBrush = new SolidColorBrush(Color.FromRgb(15, 108, 189));
                Brush capsuleBrush = new SolidColorBrush(Color.FromRgb(43, 49, 59));
                Pen capsulePen = new Pen(Brushes.White, Math.Max(0.8, size / 32.0));

                // Block responsible for the capsule-wheel visual used by the Revit ribbon button.
                context.DrawEllipse(accentBrush, null, new Point(center, center), centerRadius, centerRadius);
                context.DrawEllipse(Brushes.White, null, new Point(center, center), centerRadius * 0.35, centerRadius * 0.35);

                for (int index = 0; index < 4; index++)
                {
                    double angle = (-90.0 + (index * 90.0)) * Math.PI / 180.0;
                    double capsuleCenterX = center + (Math.Cos(angle) * capsuleRadius);
                    double capsuleCenterY = center + (Math.Sin(angle) * capsuleRadius);
                    Rect capsuleRect = new Rect(
                        capsuleCenterX - (capsuleWidth / 2.0),
                        capsuleCenterY - (capsuleHeight / 2.0),
                        capsuleWidth,
                        capsuleHeight);
                    context.DrawRoundedRectangle(
                        capsuleBrush,
                        capsulePen,
                        capsuleRect,
                        capsuleHeight / 2.0,
                        capsuleHeight / 2.0);
                }
            }

            RenderTargetBitmap bitmap = new RenderTargetBitmap(size, size, 96.0, 96.0, PixelFormats.Pbgra32);
            bitmap.Render(drawingVisual);
            bitmap.Freeze();
            return bitmap;
        }

        private static BitmapSource LoadEmbeddedIcon(int size)
        {
            string resourceName = size <= 16
                ? "SABPlus.Assets.CommandRing_16.png"
                : "SABPlus.Assets.CommandRing_32.png";
            Assembly assembly = typeof(RibbonIconFactory).Assembly;
            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                {
                    return null;
                }

                BitmapImage image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.StreamSource = stream;
                image.EndInit();
                image.Freeze();
                return image;
            }
        }
    }
}
