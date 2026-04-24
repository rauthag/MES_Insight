using System.Collections.Generic;
using MESInsight.Core;

namespace MESInsight.Charts.Interfaces
{
    public interface IChartDataBuilder
    {
        ChartType  GetChartType();
        bool       CanBuild(List<ResponseRecord> records);
        ChartData  Build(ChartInputData input);
    }
}