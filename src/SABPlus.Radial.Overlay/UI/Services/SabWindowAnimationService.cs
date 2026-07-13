using System;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace SABPlus.Radial.Overlay.UI.Services
{
    public static class SabWindowAnimationService
    {
        private static readonly DependencyProperty IsAnimationAttachedProperty =
            DependencyProperty.RegisterAttached(
                "IsAnimationAttached",
                typeof(bool),
                typeof(SabWindowAnimationService),
                new PropertyMetadata(false));

        public static void AttachWindowAnimations(Window window)
        {
            if (window == null)
            {
                return;
            }

            window.Opacity = 0.0;
            DoubleAnimation entranceAnimation = new DoubleAnimation(
                0.0,
                1.0,
                TimeSpan.FromMilliseconds(180))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            window.BeginAnimation(UIElement.OpacityProperty, entranceAnimation);

            AttachToVisualChildren(window);
        }

        private static void AttachToVisualChildren(DependencyObject parent)
        {
            int childCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int index = 0; index < childCount; index++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, index);
                ButtonBase button = child as ButtonBase;
                if (button != null)
                {
                    AttachButtonAnimation(button);
                }

                AttachToVisualChildren(child);
            }
        }

        private static void AttachButtonAnimation(ButtonBase button)
        {
            if ((bool)button.GetValue(IsAnimationAttachedProperty))
            {
                return;
            }

            button.SetValue(IsAnimationAttachedProperty, true);
            ScaleTransform scale = new ScaleTransform(1.0, 1.0);
            button.RenderTransform = scale;
            button.RenderTransformOrigin = new Point(0.5, 0.5);

            button.MouseEnter += (sender, args) => AnimateScale(scale, 1.012, 90);
            button.MouseLeave += (sender, args) => AnimateScale(scale, 1.0, 110);
            button.PreviewMouseLeftButtonDown += (sender, args) => AnimateScale(scale, 0.985, 70);
            button.PreviewMouseLeftButtonUp += (sender, args) =>
                AnimateScale(scale, button.IsMouseOver ? 1.012 : 1.0, 90);
            button.LostMouseCapture += (sender, args) =>
                AnimateScale(scale, button.IsMouseOver ? 1.012 : 1.0, 90);
        }

        private static void AnimateScale(ScaleTransform scale, double target, int durationMilliseconds)
        {
            CubicEase easing = new CubicEase { EasingMode = EasingMode.EaseOut };
            Duration duration = new Duration(TimeSpan.FromMilliseconds(durationMilliseconds));
            scale.BeginAnimation(
                ScaleTransform.ScaleXProperty,
                new DoubleAnimation(target, duration) { EasingFunction = easing });
            scale.BeginAnimation(
                ScaleTransform.ScaleYProperty,
                new DoubleAnimation(target, duration) { EasingFunction = easing });
        }
    }
}
