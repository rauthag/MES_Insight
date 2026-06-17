using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using LiveCharts.Wpf;
using MESInsight.Charts.Builders;
using MESInsight.Charts.Interfaces;
using MESInsight.Charts.Renderers;
using MESInsight.Core;
using MESInsight.UI;
using ScottPlot.WPF;

namespace MESInsight.Charts
{
    public class ChartFactory
    {
        private readonly List<IChartDataBuilder> _builders;
        private readonly List<IChartRenderer>    _renderers;
        private readonly ScottPlotTrendChartRenderer _scottPlotRenderer;

        public ChartFactory(
            DayRecordsPanelBuilder dayRecordsPanelBuilder,
            Dictionary<MessageType, (Border panel, ColumnDefinition col, bool open)> dayRecordsPanelByMessageType,
            Dictionary<MessageType, CartesianChart> trendChartByMessageType,
            Dictionary<MessageType, LiveCharts.Wpf.AxisSection> selectedDayHighlightByMessageType,
            Dictionary<MessageType, WpfPlot> scottPlotByMessageType,
            Dictionary<MessageType, (Border container, StackPanel panel)> timelineContainerByMessageType,
            Dictionary<DateTime, List<ResponseRecord>> recordsGroupedByDay,
            List<ResponseRecord> filteredRecords,
            Action<MessageType> onShowAllRecordsRequested)
        {
            _builders = new List<IChartDataBuilder>
            {
                //new TrendChart(),
                new ScottPlotTrendChart(),
                new HistogramChart(),
                new TimelineChart()
            };

            _scottPlotRenderer = new ScottPlotTrendChartRenderer(
                dayRecordsPanelBuilder,
                dayRecordsPanelByMessageType,
                scottPlotByMessageType,
                timelineContainerByMessageType,
                this,
                recordsGroupedByDay,
                filteredRecords,
                onShowAllRecordsRequested);

            _renderers = new List<IChartRenderer>
            {
                /*new TrendChartRenderer(
                    dayRecordsPanelBuilder,
                    dayRecordsPanelByMessageType,
                    trendChartByMessageType,
                    selectedDayHighlightByMessageType,
                    timelineContainerByMessageType,
                    this,
                    recordsGroupedByDay,
                    filteredRecords,
                    onShowAllRecordsRequested),*/
                new ScottPlotTrendChartRenderer(
                    dayRecordsPanelBuilder,
                    dayRecordsPanelByMessageType,
                    scottPlotByMessageType,
                    timelineContainerByMessageType,
                    this,
                    recordsGroupedByDay,
                    filteredRecords,
                    onShowAllRecordsRequested),
                new HistogramChartRenderer(dayRecordsPanelBuilder),
                new TimelineChartRenderer()
            };
        }

        public ChartData Build(ChartType chartType, List<ResponseRecord> records, MessageType messageType)
        {
            var filtered = FilterByMessageType(records, messageType);
            var builder = _builders.FirstOrDefault(b => b.GetChartType() == chartType);
            if (builder == null || !builder.CanBuild(filtered)) return null;
            return builder.Build(PrepareInput(filtered, messageType));
        }

        public UIElement Render(ChartType chartType, ChartData data, RenderContext context)
        {
            return GetRenderer(chartType)?.Render(data, context);
        }

        public UIElement RenderScottPlot(ChartData data, RenderContext context)
        {
            return _scottPlotRenderer?.Render(data, context);
        }

        public int GetMinimumHeight(ChartType chartType, RenderContext context)
        {
            return GetRenderer(chartType)?.GetMinimumHeight(context) ?? 300;
        }

        public IChartRenderer GetRenderer(ChartType chartType)
        {
            return _renderers.FirstOrDefault(r => r.GetChartType() == chartType);
        }

        private static ChartInputData PrepareInput(List<ResponseRecord> records, MessageType messageType,
            Dictionary<DateTime, List<ResponseRecord>> precomputedGroupedByDay = null)
        {
            double avg    = records.Average(r => (double)r.ResponseTime);
            double stdDev = Math.Sqrt(records.Average(r => Math.Pow(r.ResponseTime - avg, 2)));

            var sorted = records.Select(r => r.ResponseTime).OrderBy(x => x).ToList();
            int p95    = sorted[(int)(sorted.Count * 0.95)];
            int p99    = sorted[(int)(sorted.Count * 0.99)];

            var groupedByDay = precomputedGroupedByDay
                ?? records
                    .GroupBy(r => r.TimestampParsed.Date)
                    .ToDictionary(g => g.Key, g => g.ToList());

            return new ChartInputData
            {
                Records      = records,
                MessageType  = messageType,
                Average      = avg,
                StdDev       = stdDev,
                P95          = p95,
                P99          = p99,
                GroupedByDay = groupedByDay
            };
        }

        public Dictionary<MessageType, ChartInputData> PrepareAllInputs(
            List<ResponseRecord> records,
            MessageType[] messageTypes)
        {
            var result = new ConcurrentDictionary<MessageType, ChartInputData>();

            Parallel.ForEach(
                messageTypes,
                new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1) },
                type =>
                {
                    var filtered = FilterByMessageType(records, type);
                    if (filtered.Count == 0) return;

                    var byDay = filtered
                        .GroupBy(r => r.TimestampParsed.Date)
                        .ToDictionary(g => g.Key, g => g.ToList());

                    result[type] = PrepareInput(filtered, type, byDay);
                });

            return new Dictionary<MessageType, ChartInputData>(result);
        }

        public ChartData BuildSingle(ChartType chartType, ChartInputData input)
        {
            var builder = _builders.FirstOrDefault(b => b.GetChartType() == chartType);
            if (builder == null || !builder.CanBuild(input.Records)) return null;
            return builder.Build(input);
        }

        private static List<ResponseRecord> FilterByMessageType(List<ResponseRecord> records, MessageType messageType)
        {
            if (messageType == MessageType.ALL)
                return records.Where(r => r.TimestampParsed != DateTime.MinValue).ToList();

            return records
                .Where(r => r.Type == messageType && r.TimestampParsed != DateTime.MinValue)
                .ToList();
        }
        
        public ChartData BuildSingleScottPlot(ChartInputData input)
        {
            var builder = new ScottPlotTrendChart();
            if (!builder.CanBuild(input.Records)) return null;
            return builder.Build(input);
        }
    }
}