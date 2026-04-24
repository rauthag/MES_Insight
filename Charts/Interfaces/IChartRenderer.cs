using System.Windows;
using MESInsight.Core;

namespace MESInsight.Charts.Interfaces
{
    public interface IChartRenderer
    {
        ChartType  GetChartType();
        int        GetMinimumHeight(RenderContext context);
        UIElement  Render(ChartData data, RenderContext context);
    }
}