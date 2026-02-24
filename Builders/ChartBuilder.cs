using System.Collections.Generic;
using System.Linq;

namespace RTAnalyzer.Builders
{
    public class ChartBuilder
    {
        private readonly HistogramChartBuilder _histogramBuilder = new HistogramChartBuilder();
        private readonly TrendChartBuilder _trendBuilder = new TrendChartBuilder();

        public ChartData Build(List<ResponseRecord> records, MessageType type)
        {
            var items = records.Where(r => r.Type == type).ToList();
            if (items.Count == 0) return null;

            var histogramCharts = _histogramBuilder.BuildCharts(items);
            var trendChart = _trendBuilder.BuildChart(items, type);

            return new ChartData
            {
                Charts = histogramCharts ?? new List<ChartSeries>(),
                TrendChart = trendChart,
                FilteredRecords = items
            };
        }
    }
}