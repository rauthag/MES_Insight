using RTAnalyzer.Core;
using System.Collections.Generic;

namespace RTAnalyzer.Charts
{
    public interface IChartDataBuilder
    {
        ChartType  GetChartType();
        bool       CanBuild(List<ResponseRecord> records);
        ChartData  Build(ChartInputData input);
    }
}