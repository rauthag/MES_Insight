using System.Collections.Generic;
using RTAnalyzer.Core;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LiveCharts;
using LiveCharts.Wpf;

namespace RTAnalyzer.Charts.Renderers
{
    public abstract class ChartRenderer : IChartRenderer
    {
        public abstract ChartType  GetChartType();
        public abstract int        GetMinimumHeight(RenderContext context);
        public abstract UIElement  Render(ChartData data, RenderContext context);

        protected static Border WrapInSectionBorder(UIElement content, bool isHistogram)
        {
            return new Border
            {
                Child           = content,
                Background      = new SolidColorBrush(isHistogram ? Color.FromRgb(18, 24, 32)  : Color.FromRgb(13, 17, 23)),
                BorderBrush     = new SolidColorBrush(isHistogram ? Color.FromRgb(30, 40, 50)  : Color.FromRgb(22, 60, 36)),
                BorderThickness = new Thickness(1),
                CornerRadius    = new CornerRadius(8),
                Margin          = new Thickness(0, 5, 0, 5)
            };
        }

        protected static Border BuildSectionDividerLine()
        {
            return new Border
            {
                Height     = 1,
                Background = new SolidColorBrush(Color.FromRgb(33, 38, 45)),
                Margin     = new Thickness(20, 5, 20, 5),
                Opacity    = 0.5
            };
        }
        
        protected static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) yield break;
            int count = VisualTreeHelper.GetChildrenCount(parent);
            
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                
                if (child is T t) yield return t;
                
                foreach (var descendant in FindVisualChildren<T>(child))
                    yield return descendant;
            }
        }

        protected static DefaultTooltip BuildDarkStyledTooltip()
        {
            return new DefaultTooltip
            {
                Background      = new SolidColorBrush(Color.FromArgb(245, 22, 27, 34)),
                Foreground      = System.Windows.Media.Brushes.White,
                BorderBrush     = new SolidColorBrush(Color.FromRgb(56, 139, 253)),
                BorderThickness = new Thickness(2),
                CornerRadius    = new CornerRadius(6),
                FontSize        = 12,
                FontWeight      = FontWeights.Normal,
                Padding         = new Thickness(12, 10, 12, 10),
                SelectionMode   = TooltipSelectionMode.OnlySender
            };
        }

        protected static Color GetMonthAccentColor(int monthNumber)
        {
            var colors = new[]
            {
                Color.FromRgb( 52, 152, 219), Color.FromRgb( 46, 204, 113),
                Color.FromRgb(155,  89, 182), Color.FromRgb(241, 196,  15),
                Color.FromRgb(230, 126,  34), Color.FromRgb(231,  76,  60)
            };
            return colors[(monthNumber - 1) % colors.Length];
        }
    }
}