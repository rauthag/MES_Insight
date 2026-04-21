using RTAnalyzer.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using LiveCharts;
using LiveCharts.Wpf;
using LiveCharts.Defaults;
using System.Windows.Media;

namespace RTAnalyzer.Charts.Builders
{
    public class TrendChart : IChartDataBuilder
    {
        private static readonly TimeSpan GapPlaceholderSpan = TimeSpan.FromDays(21);

        public ChartType GetChartType() => ChartType.Trend;

        public bool CanBuild(List<ResponseRecord> records) => records.Count >= 2;

        public ChartData Build(ChartInputData input)
        {
            var items = input.Records;

            var timeGroups = GroupByDay(items);
            if (timeGroups.Count < 2) return null;

            var orderedGroups = timeGroups.OrderBy(g => g.Key).ToList();
            var mapping       = BuildDateAxisCompressionMapping(orderedGroups);
            var data          = BuildDailyData(orderedGroups, mapping);

            if (data.AvgValues.Count < 2 || data.P95Values.Count < 2) return null;

            double pointSize = CalcPointSize(orderedGroups.Count);
            var    series    = BuildSeriesCollection(data, pointSize);

            int slaThreshold = GetSlaThreshold(items);
            CalcSlaViolationCounts(orderedGroups, data.DailyStats, slaThreshold);
            AddSlaThresholdLine(series, data, slaThreshold);
            AddSlaViolationMarkers(series, data.DailyStats, slaThreshold);

            var chartSeries = new ChartSeries
            {
                Name   = "Response Time Over Time (Daily)",
                Series = series,
                Labels = null,
                RemappedGaps                      = mapping.RemappedGaps,
                GapLabels                         = mapping.GapLabels,
                GapCenterAxisValues               = mapping.GapCenterAxisValues,
                CompressedAxisValueToCalendarDate = mapping.CompressedAxisValueToCalendarDate
            };

            return new ChartData
            {
                TrendChart      = chartSeries,
                FilteredRecords = items
            };
        }

        // Gives each day a position on the X axis, shrinking large date gaps so the chart stays readable
        private DateAxisCompressionMapping BuildDateAxisCompressionMapping(
            List<KeyValuePair<DateTime, List<ResponseRecord>>> orderedGroups)
        {
            var mapping = new DateAxisCompressionMapping();
            // Tracks how much each day's position has been shifted to remove gap space
            long compressionOffsetTicks = 0;

            for (int i = 0; i < orderedGroups.Count; i++)
            {
                var group = orderedGroups[i];

                if (i > 0)
                {
                    var prev = orderedGroups[i - 1].Key;
                    var curr = group.Key;
                    double diffDays = (curr - prev).TotalDays;

                    // Gap is too large to show as-is, replace it with a shorter placeholder
                    if (diffDays >= 14)
                    {
                        long realGap   = (curr - prev).Ticks;
                        long fakeGap   = GapPlaceholderSpan.Ticks;
                        long reduction = realGap - fakeGap;
                        compressionOffsetTicks -= reduction;

                        long prevCompressedAxisValue = prev.Ticks + compressionOffsetTicks + reduction;
                        long currCompressedAxisValue = curr.Ticks + compressionOffsetTicks;
                        long gapFrom = prevCompressedAxisValue + TimeSpan.FromDays(1).Ticks;
                        long gapTo   = currCompressedAxisValue - TimeSpan.FromDays(1).Ticks;
                        mapping.RemappedGaps.Add((gapFrom, gapTo));
                        mapping.GapCenterAxisValues.Add(gapFrom + (gapTo - gapFrom) / 2);

                        var enUS = new System.Globalization.CultureInfo("en-US");
                        mapping.GapLabels.Add(
                            prev.ToString("MMM", enUS) + " - " + curr.ToString("MMM yyyy", enUS));
                    }
                }

                // Save the link between the chart position and the real calendar date
                long compressedAxisValue = group.Key.Ticks + compressionOffsetTicks;
                mapping.CompressedAxisValueToCalendarDate[compressedAxisValue] = group.Key;
                mapping.CalendarDateToCompressedAxisValue[group.Key] = compressedAxisValue;
                mapping.CompressedAxisValueToDailyRecordCount[compressedAxisValue] = group.Value.Count;
            }

            return mapping;
        }

        private DailyData BuildDailyData(
            List<KeyValuePair<DateTime, List<ResponseRecord>>> orderedGroups,
            DateAxisCompressionMapping mapping)
        {
            var data = new DailyData();

            for (int i = 0; i < orderedGroups.Count; i++)
            {
                var group = orderedGroups[i];
                double avgMs = group.Value.Average(r => r.ResponseTime);
                int p95Ms = ChartCalculator.GetPercentile(group.Value, 0.95);
                int count = group.Value.Count;

                long compressedAxisValue = mapping.CalendarDateToCompressedAxisValue[group.Key];

                data.AvgValues.Add(new DateTimePoint(new DateTime(compressedAxisValue), avgMs));
                data.P95Values.Add(new DateTimePoint(new DateTime(compressedAxisValue), p95Ms));

                data.DailyStats.Add(new DailyStats
                {
                    CompressedAxisValue = compressedAxisValue,
                    RealDate  = group.Key,
                    Avg       = avgMs,
                    P95       = p95Ms,
                    Min       = group.Value.Min(r => r.ResponseTime),
                    Max       = group.Value.Max(r => r.ResponseTime),
                    Count     = count
                });

                double rollingAvg = CalculateRollingAverage(orderedGroups, i, 7);
                if (rollingAvg > 0)
                    data.RollingAvgValues.Add(new DateTimePoint(new DateTime(compressedAxisValue), rollingAvg));
            }

            return data;
        }

        private SeriesCollection BuildSeriesCollection(DailyData data, double pointSize)
        {
            return new SeriesCollection
            {
                BuildAvgLine(data),
                BuildAvgPoints(data, pointSize),
                BuildP95Line(data),
                BuildP95Points(data, pointSize),
                BuildRollingAvgLine(data.RollingAvgValues)
            };
        }

        private LineSeries BuildAvgLine(DailyData data)
        {
            return new LineSeries
            {
                Title = "AVG",
                Values = data.AvgValues,
                Stroke = new SolidColorBrush(Color.FromRgb(79, 195, 247)),
                StrokeThickness = 4,
                Fill = new SolidColorBrush(Color.FromArgb(25, 79, 195, 247)),
                PointGeometry = null,
                LineSmoothness = 0,
                LabelPoint = point => FormatAvgTooltip(point, data)
            };
        }

        private ScatterSeries BuildAvgPoints(DailyData data, double pointSize)
        {
            return new ScatterSeries
            {
                Title = "",
                Values = data.AvgValues,
                Fill = new SolidColorBrush(Color.FromRgb(2, 136, 209)),
                Stroke = Brushes.Transparent,
                StrokeThickness = 0,
                MinPointShapeDiameter = pointSize,
                MaxPointShapeDiameter = pointSize,
                LabelPoint = point => FormatAvgTooltip(point, data)
            };
        }

        private LineSeries BuildP95Line(DailyData data)
        {
            return new LineSeries
            {
                Title = "P95",
                Values = data.P95Values,
                Stroke = new SolidColorBrush(Color.FromRgb(165, 214, 167)),
                StrokeThickness = 3.5,
                Fill = Brushes.Transparent,
                PointGeometry = null,
                LineSmoothness = 0,
                LabelPoint = point => FormatP95Tooltip(point, data)
            };
        }

        private ScatterSeries BuildP95Points(DailyData data, double pointSize)
        {
            return new ScatterSeries
            {
                Title = "",
                Values = data.P95Values,
                Fill = new SolidColorBrush(Color.FromRgb(56, 142, 60)),
                Stroke = Brushes.Transparent,
                StrokeThickness = 0,
                PointGeometry = DefaultGeometries.Diamond,
                MinPointShapeDiameter = pointSize,
                MaxPointShapeDiameter = pointSize,
                LabelPoint = point => FormatP95Tooltip(point, data)
            };
        }

        private LineSeries BuildRollingAvgLine(ChartValues<DateTimePoint> values)
        {
            return new LineSeries
            {
                Title = "7-Day AVG",
                Values = values,
                Stroke = new SolidColorBrush(Color.FromRgb(255, 112, 67)),
                StrokeThickness = 3.5,
                Fill = Brushes.Transparent,
                PointGeometry = null,
                LineSmoothness = 0.5,
                StrokeDashArray = new DoubleCollection { 8, 4 },
                LabelPoint = point => FormatRollingAvgTooltip(point)
            };
        }

        private static string FormatAvgTooltip(ChartPoint point, DailyData data)
        {
            long ticks = (long)point.X;
            var stat = data.DailyStats.FirstOrDefault(s => s.CompressedAxisValue == ticks);
            if (stat == null) return point.Y.ToString("F1") + "ms";
            return stat.RealDate.ToString("dd.MM.yyyy") + "\n" + new string('─', 16)
                + "\nAVG: " + point.Y.ToString("F1") + "ms"
                + "\nRecord Count: " + stat.Count;
        }

        private static string FormatP95Tooltip(ChartPoint point, DailyData data)
        {
            long ticks = (long)point.X;
            var stat = data.DailyStats.FirstOrDefault(s => s.CompressedAxisValue == ticks);
            if (stat == null) return point.Y.ToString("F0") + "ms";
            return stat.RealDate.ToString("dd.MM.yyyy") + "\n" + new string('─', 16)
                + "\nP95: " + point.Y.ToString("F0") + "ms"
                + "\nRecord Count: " + stat.Count;
        }

        private static string FormatRollingAvgTooltip(ChartPoint point)
        {
            var date = new DateTime((long)point.X);
            return date.ToString("dd.MM.yyyy") + "\n" + new string('─', 16)
                + "\n7-Day AVG: " + point.Y.ToString("F1") + "ms";
        }

        public static double CalcPointSize(int visibleCount)
        {
            if      (visibleCount <=   4) return 18;
            else if (visibleCount <=   7) return 17;
            else if (visibleCount <=  10) return 16;
            else if (visibleCount <=  14) return 15;
            else if (visibleCount <=  18) return 14;
            else if (visibleCount <=  22) return 13;
            else if (visibleCount <=  30) return 13;
            else if (visibleCount <=  40) return 12;
            else if (visibleCount <=  50) return 12;
            else if (visibleCount <=  60) return 11;
            else if (visibleCount <=  75) return 11;
            else if (visibleCount <=  90) return 10;
            else if (visibleCount <= 120) return 10;
            else if (visibleCount <= 150) return  9;
            else if (visibleCount <= 180) return  9;
            else if (visibleCount <= 220) return  9;
            else                          return  9;
        }

        private double CalculateRollingAverage(
            List<KeyValuePair<DateTime, List<ResponseRecord>>> groups,
            int currentIndex, int windowSize)
        {
            int start = Math.Max(0, currentIndex - windowSize + 1);
            int count = 0;
            double sum = 0;

            for (int i = start; i <= currentIndex; i++)
            {
                double avg = groups[i].Value.Average(r => r.ResponseTime);
                sum += avg;
                count++;
            }

            return count > 0 ? sum / count : 0;
        }

        private void CalcSlaViolationCounts(
            List<KeyValuePair<DateTime, List<ResponseRecord>>> orderedGroups,
            List<DailyStats> dailyStats,
            int slaThreshold)
        {
            foreach (var stat in dailyStats)
            {
                var group = orderedGroups.FirstOrDefault(g => g.Key == stat.RealDate);
                if (group.Value == null) continue;
                int violationCount = 0;
                foreach (var r in group.Value)
                {
                    if (r.ResponseTime > slaThreshold) violationCount++;
                }
                stat.SlaViolationCount = violationCount;
            }
        }

        private void AddSlaViolationMarkers(
            SeriesCollection series, List<DailyStats> stats, int slaThreshold)
        {
            var violations = new ChartValues<DateTimePoint>();
            var violationStats = new List<DailyStats>();

            foreach (var stat in stats)
            {
                if (stat.P95 > slaThreshold)
                {
                    violations.Add(new DateTimePoint(new DateTime(stat.CompressedAxisValue), stat.P95));
                    violationStats.Add(stat);
                }
            }

            if (violations.Count > 0)
            {
                series.Add(new ScatterSeries
                {
                    Title = "SLA Violations",
                    Values = violations,
                    Fill = new SolidColorBrush(Color.FromArgb(180, 231, 76, 60)),
                    Stroke = new SolidColorBrush(Color.FromRgb(192, 57, 43)),
                    StrokeThickness = 2,
                    MinPointShapeDiameter = 15,
                    MaxPointShapeDiameter = 15,
                    LabelPoint = point => FormatViolationTooltip(point, violationStats, slaThreshold)
                });
            }
        }

        private static string FormatViolationTooltip(ChartPoint point, List<DailyStats> violationStats, int slaThreshold)
        {
            long ticks = (long)point.X;
            var stat = violationStats.FirstOrDefault(s => s.CompressedAxisValue == ticks);
            if (stat == null) return "SLA Violation";
            int excess = stat.P95 - slaThreshold;
            return stat.RealDate.ToString("dd.MM.yyyy") + "\n" + new string('─', 16)
                + "\nP95: " + stat.P95 + "ms"
                + "\nRecord Count: " + stat.Count
                + "\n"
                + "\nSLA Violations: " + stat.SlaViolationCount
                + "\nSLA Target: " + slaThreshold + "ms (exceeded by +" + excess + "ms)";
        }

        private int GetSlaThreshold(List<ResponseRecord> items)
        {
            int p99 = ChartCalculator.GetPercentile(items, 0.99);
            return RoundUpToNiceNumber(p99);
        }

        private int RoundUpToNiceNumber(int value)
        {
            if (value <= 50)  return ((value +  9) / 10)  * 10;
            if (value <= 100) return ((value + 24) / 25)  * 25;
            if (value <= 500) return ((value + 49) / 50)  * 50;
            return                   ((value + 99) / 100) * 100;
        }

        private void AddSlaThresholdLine(SeriesCollection series, DailyData data, int slaThreshold)
        {
            if (data.DailyStats.Count == 0) return;

            int underSlaCount = 0;
            foreach (var stat in data.DailyStats)
            {
                if (stat.P95 <= slaThreshold)
                    underSlaCount++;
            }

            double slaCompliance = data.DailyStats.Count > 0
                ? (underSlaCount / (double)data.DailyStats.Count) * 100
                : 0;

            var thresholdValues = new ChartValues<DateTimePoint>
            {
                new DateTimePoint(new DateTime(data.DailyStats.First().CompressedAxisValue), slaThreshold),
                new DateTimePoint(new DateTime(data.DailyStats.Last().CompressedAxisValue),  slaThreshold)
            };

            series.Add(new LineSeries
            {
                Title = "Target: " + slaThreshold + "ms (" + slaCompliance.ToString("F0") + "% OK)",
                Values = thresholdValues,
                Stroke = new SolidColorBrush(Color.FromRgb(231, 76, 60)),
                StrokeThickness = 2,
                StrokeDashArray = new DoubleCollection { 5, 5 },
                Fill = Brushes.Transparent,
                PointGeometry = null
            });
        }

        private Dictionary<DateTime, List<ResponseRecord>> GroupByDay(List<ResponseRecord> items)
        {
            var groups = new Dictionary<DateTime, List<ResponseRecord>>();

            foreach (var item in items)
            {
                if (item.TimestampParsed == DateTime.MinValue) continue;
                DateTime key = item.TimestampParsed.Date;

                if (!groups.ContainsKey(key))
                    groups[key] = new List<ResponseRecord>();

                groups[key].Add(item);
            }

            return groups;
        }

        // Stats for a single day
        internal class DailyStats
        {
            public DateTime RealDate         { get; set; }
            public long     CompressedAxisValue        { get; set; }
            public double   Avg              { get; set; }
            public int      P95              { get; set; }
            public int      Min              { get; set; }
            public int      Max              { get; set; }
            public int      Count            { get; set; }
            public int      SlaViolationCount { get; set; }
        }

        internal class DailyData
        {
            public ChartValues<DateTimePoint> AvgValues        { get; } = new ChartValues<DateTimePoint>();
            public ChartValues<DateTimePoint> P95Values        { get; } = new ChartValues<DateTimePoint>();
            public ChartValues<DateTimePoint> RollingAvgValues { get; } = new ChartValues<DateTimePoint>();
            public List<DailyStats>           DailyStats       { get; } = new List<DailyStats>();
        }

        // Maps real calendar dates to their shifted chart positions, and back
        private class DateAxisCompressionMapping
        {
            public Dictionary<long, DateTime> CompressedAxisValueToCalendarDate { get; } = new Dictionary<long, DateTime>();
            public Dictionary<DateTime, long> CalendarDateToCompressedAxisValue { get; } = new Dictionary<DateTime, long>();
            public Dictionary<long, int>      CompressedAxisValueToDailyRecordCount       { get; } = new Dictionary<long, int>();
            public List<(long From, long To)> RemappedGaps    { get; } = new List<(long, long)>();
            public List<string>               GapLabels       { get; } = new List<string>();
            public List<long>                 GapCenterAxisValues     { get; } = new List<long>();
        }
    }
}