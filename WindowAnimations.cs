using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MESInsight
{
    internal static class WindowAnimations
    {
        public static void FadeOutAndClose(Window window, bool dialogResult)
        {
            Border overlay = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0, 5, 15, 8)),
                IsHitTestVisible = false
            };

            if (window.Content is Grid root)
                root.Children.Add(overlay);

            System.Windows.Threading.DispatcherTimer timer =
                new System.Windows.Threading.DispatcherTimer
                    { Interval = TimeSpan.FromMilliseconds(16) };

            byte alpha = 0;
            timer.Tick += (s, e) =>
            {
                alpha = (byte)Math.Min(255, alpha + 18);
                overlay.Background = new SolidColorBrush(Color.FromArgb(alpha, 5, 15, 8));
                if (alpha >= 255)
                {
                    timer.Stop();
                    window.DialogResult = dialogResult;
                }
            };
            timer.Start();
        }
    }
}