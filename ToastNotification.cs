using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace MESInsight
{
    internal enum ToastKind
    {
        NoRecords,
        StationLoaded,
        StationUnloaded
    }

    internal static class ToastNotification
    {
        private const double ToastW      = 320;
        private const double MarginRight = 16;
        private const double MarginTop   = 16;
        private const double Gap         = 8;
        private const int    DisplayMs   = 3200;
        private const int    FadeMs      = 280;

        private static readonly List<Border> _active = new List<Border>();

        public static void Show(Panel root, ToastKind kind, string stationName)
        {
            if (root == null) return;
            var (bg, border, icon, textColor) = ResolveStyle(kind);
            string message = ResolveMessage(kind, stationName);

            var toast = BuildToast(message, icon, bg, border, textColor);
            root.Children.Add(toast);
            Panel.SetZIndex(toast, 2000);
            _active.Add(toast);

            RepositionAll(root);
            FadeIn(toast);

            var timer = new System.Windows.Threading.DispatcherTimer
                { Interval = TimeSpan.FromMilliseconds(DisplayMs) };

            timer.Tick += (s, e) =>
            {
                timer.Stop();
                FadeOut(toast, () =>
                {
                    root.Children.Remove(toast);
                    _active.Remove(toast);
                    RepositionAll(root);
                });
            };
            timer.Start();
        }

        private static Border BuildToast(
            string message, string icon, Color bg, Color borderColor, Color textColor)
        {
            var row = new StackPanel
            {
                Orientation       = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };

            row.Children.Add(new TextBlock
            {
                Text              = icon,
                FontSize          = 15,
                VerticalAlignment = VerticalAlignment.Center,
                Margin            = new Thickness(0, 0, 10, 0)
            });

            row.Children.Add(new TextBlock
            {
                Text              = message,
                FontSize          = 11,
                FontWeight        = FontWeights.SemiBold,
                Foreground        = new SolidColorBrush(textColor),
                TextWrapping      = TextWrapping.Wrap,
                MaxWidth          = ToastW - 56,
                VerticalAlignment = VerticalAlignment.Center
            });

            var toast = new Border
            {
                MinWidth            = ToastW,
                MinHeight           = 48,
                Background          = new SolidColorBrush(bg),
                BorderBrush         = new SolidColorBrush(borderColor),
                BorderThickness     = new Thickness(1),
                CornerRadius        = new CornerRadius(8),
                Padding             = new Thickness(14, 10, 14, 10),
                Child               = row,
                Opacity             = 0,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment   = VerticalAlignment.Top
            };

            return toast;
        }

        private static void RepositionAll(Panel root)
        {
            double top = MarginTop;
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                Canvas.SetRight(_active[i], MarginRight);
                Canvas.SetTop(_active[i],   top);
                top += 48 + Gap;
            }
        }

        private static void FadeIn(Border toast)
        {
            var anim = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(FadeMs))
                { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
            toast.BeginAnimation(UIElement.OpacityProperty, anim);
        }

        private static void FadeOut(Border toast, Action onDone)
        {
            var anim = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(FadeMs))
                { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn } };
            anim.Completed += (s, e) => onDone();
            toast.BeginAnimation(UIElement.OpacityProperty, anim);
        }

        private static string ResolveMessage(ToastKind kind, string stationName)
        {
            switch (kind)
            {
                case ToastKind.NoRecords:       return stationName + " — no records found";
                case ToastKind.StationLoaded:   return stationName + " — loaded";
                case ToastKind.StationUnloaded: return stationName + " — removed from memory";
                default:                        return stationName;
            }
        }

        private static (Color bg, Color border, string icon, Color text) ResolveStyle(ToastKind kind)
        {
            switch (kind)
            {
                case ToastKind.NoRecords:
                    return (Color.FromRgb(60, 14, 14),
                            Color.FromRgb(160, 40, 40),
                            "⚠",
                            Color.FromRgb(240, 160, 160));
                case ToastKind.StationLoaded:
                    return (Color.FromRgb(12, 40, 20),
                            Color.FromRgb(46, 160, 67),
                            "✓",
                            Color.FromRgb(160, 240, 180));
                case ToastKind.StationUnloaded:
                    return (Color.FromRgb(50, 35, 8),
                            Color.FromRgb(180, 120, 30),
                            "🗑",
                            Color.FromRgb(240, 200, 120));
                default:
                    return (Color.FromRgb(22, 27, 34),
                            Color.FromRgb(56, 139, 253),
                            "ℹ",
                            Color.FromRgb(200, 210, 220));
            }
        }
    }
}