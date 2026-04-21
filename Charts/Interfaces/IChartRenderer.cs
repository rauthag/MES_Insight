using System.Windows;
using RTAnalyzer.Core;

namespace RTAnalyzer.Charts
{
    public interface IChartRenderer
    {
        ChartType  GetChartType();
        int        GetMinimumHeight(RenderContext context);
        UIElement  Render(ChartData data, RenderContext context);
    }
}