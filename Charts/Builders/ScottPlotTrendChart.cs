using System;
using System.Collections.Generic;
using System.Linq;
using MESInsight.Charts.Interfaces;
using MESInsight.Core;

namespace MESInsight.Charts.Builders
{
    public class ScottPlotTrendChart : IChartDataBuilder
    {
        private static readonly TimeSpan GapPlaceholderSpan = TimeSpan.FromDays(21);

        public ChartType GetChartType() => ChartType.Trend;

        //TODO:
        public bool CanBuild(List<ResponseRecord> records) => records.Count >= 2;
        //public bool CanBuild(List<ResponseRecord> records) => records != null && records.Count > 0;

        public ChartData Build(ChartInputData input)
        {
            var items = input.Records;

            var timeGroups   = GroupByDay(items);
            if (timeGroups.Count < 2) return null;

            var orderedGroups = timeGroups.OrderBy(g => g.Key).ToList();
            var mapping       = BuildCompressionMapping(orderedGroups);
            var daily         = BuildDailyData(orderedGroups, mapping);

            if (daily.Stats.Count < 2) return null;

            int    slaThreshold = GetSlaThreshold(items);
            double slaCompliance = CalcSlaCompliance(daily.Stats, slaThreshold);
            CalcViolationCounts(orderedGroups, daily.Stats, slaThreshold);

            var data = new ScottPlotTrendData
            {
                Name             = "Response Time Over Time (Daily)",
                SlaThreshold     = slaThreshold,
                SlaCompliancePct = slaCompliance,
                XToDate          = mapping.XToDate,
                Gaps             = mapping.Gaps,
                GapLabels        = mapping.GapLabels,
                DailyStats       = daily.Stats,
                AvgX             = daily.Stats.Select(s => s.X).ToArray(),
                AvgY             = daily.Stats.Select(s => s.Avg).ToArray(),
                P95X             = daily.Stats.Select(s => s.X).ToArray(),
                P95Y             = daily.Stats.Select(s => (double)s.P95).ToArray(),
                RollingAvgX      = daily.RollingX.ToArray(),
                RollingAvgY      = daily.RollingY.ToArray(),
                ViolationX       = daily.Stats.Where(s => s.P95 > slaThreshold).Select(s => s.X).ToArray(),
                ViolationY       = daily.Stats.Where(s => s.P95 > slaThreshold).Select(s => (double)s.P95).ToArray(),
                YMin             = CalcYMin(daily.Stats),
                YMax             = daily.Stats.Max(s => s.Max) * 1.05
            };

            return new ChartData
            {
                ScottPlotTrend  = data,
                FilteredRecords = items
            };
        }

        private CompressionMapping BuildCompressionMapping(
            List<KeyValuePair<DateTime, List<ResponseRecord>>> groups)
        {
            var m = new CompressionMapping();
            long offsetTicks = 0;

            for (int i = 0; i < groups.Count; i++)
            {
                var group = groups[i];

                if (i > 0)
                {
                    var prev = groups[i - 1].Key;
                    var curr = group.Key;
                    double diffDays = (curr - prev).TotalDays;

                    if (diffDays >= 14)
                    {
                        long realGap   = (curr - prev).Ticks;
                        long fakeGap   = GapPlaceholderSpan.Ticks;
                        long reduction = realGap - fakeGap;
                        offsetTicks -= reduction;

                        double prevX = TicksToDouble(prev.Ticks + offsetTicks + reduction);
                        double currX = TicksToDouble(curr.Ticks + offsetTicks);
                        long gapFrom = (long)(prevX + 1);
                        long gapTo   = (long)(currX - 1);

                        m.Gaps.Add(((double)gapFrom, (double)gapTo));

                        var enUS = new System.Globalization.CultureInfo("en-US");
                        m.GapLabels.Add(
                            prev.ToString("MMM", enUS) + " - " + curr.ToString("MMM yyyy", enUS));
                    }
                }

                long compressedTicks = group.Key.Ticks + offsetTicks;
                double x = TicksToDouble(compressedTicks);
                m.XToDate[x]           = group.Key;
                m.DateToX[group.Key]   = x;
            }

            return m;
        }
        
        private static double TicksToDouble(long ticks) => ticks / (double)TimeSpan.TicksPerDay;
        

        private DailyDataRaw BuildDailyData(
            List<KeyValuePair<DateTime, List<ResponseRecord>>> groups,
            CompressionMapping mapping)
        {
            var raw = new DailyDataRaw();

            for (int i = 0; i < groups.Count; i++)
            {
                var group = groups[i];
                double avg  = group.Value.Average(r => r.ResponseTime);
                int    p95  = ChartCalculator.GetPercentile(group.Value, 0.95);
                int    min  = group.Value.Min(r => r.ResponseTime);
                int    max  = group.Value.Max(r => r.ResponseTime);
                int    count = group.Value.Count;
                double x    = mapping.DateToX[group.Key];

                raw.Stats.Add(new ScottPlotDailyStats
                {
                    X        = x,
                    RealDate = group.Key,
                    Avg      = avg,
                    P95      = p95,
                    Min      = min,
                    Max      = max,
                    Count    = count
                });

                double rollingAvg = CalcRollingAverage(groups, i, 7);
                if (rollingAvg > 0)
                {
                    raw.RollingX.Add(x);
                    raw.RollingY.Add(rollingAvg);
                }
            }

            return raw;
        }
        
        // ── Helpers ───────────────────────────────────────────────────────────

        private static double CalcYMin(List<ScottPlotDailyStats> stats)
        {
            double lowest = stats.Min(s => s.Min);
            if (lowest <= 0) return 0;
            return Math.Max(0, Math.Floor((lowest * 0.85) / 5) * 5);
        }

        private static double CalcSlaCompliance(List<ScottPlotDailyStats> stats, int threshold)
        {
            if (stats.Count == 0) return 0;
            int ok = stats.Count(s => s.P95 <= threshold);
            return ok * 100.0 / stats.Count;
        }

        private static void CalcViolationCounts(
            List<KeyValuePair<DateTime, List<ResponseRecord>>> groups,
            List<ScottPlotDailyStats> stats,
            int threshold)
        {
            foreach (var stat in stats)
            {
                var group = groups.FirstOrDefault(g => g.Key == stat.RealDate);
                if (group.Value == null) continue;
                stat.SlaViolationCount = group.Value.Count(r => r.ResponseTime > threshold);
            }
        }

        private double CalcRollingAverage(
            List<KeyValuePair<DateTime, List<ResponseRecord>>> groups,
            int idx, int window)
        {
            int start = Math.Max(0, idx - window + 1);
            double sum = 0;
            int count = 0;
            for (int i = start; i <= idx; i++)
            {
                sum += groups[i].Value.Average(r => r.ResponseTime);
                count++;
            }
            return count > 0 ? sum / count : 0;
        }

        private int GetSlaThreshold(List<ResponseRecord> items)
        {
            int p99 = ChartCalculator.GetPercentile(items, 0.99);
            return RoundUpToNice(p99);
        }

        private int RoundUpToNice(int value)
        {
            if (value <= 50)  return ((value +  9) / 10)  * 10;
            if (value <= 100) return ((value + 24) / 25)  * 25;
            if (value <= 500) return ((value + 49) / 50)  * 50;
            return                   ((value + 99) / 100) * 100;
        }

        private Dictionary<DateTime, List<ResponseRecord>> GroupByDay(List<ResponseRecord> items)
        {
            var groups = new Dictionary<DateTime, List<ResponseRecord>>();
            foreach (var r in items)
            {
                if (r.TimestampParsed == DateTime.MinValue) continue;
                var key = r.TimestampParsed.Date;
                if (!groups.ContainsKey(key)) groups[key] = new List<ResponseRecord>();
                groups[key].Add(r);
            }
            return groups;
        }

        // ── Inner types ───────────────────────────────────────────────────────

        private class CompressionMapping
        {
            public Dictionary<double, DateTime>   XToDate   { get; } = new Dictionary<double, DateTime>();
            public Dictionary<DateTime, double>   DateToX   { get; } = new Dictionary<DateTime, double>();
            public List<(double From, double To)> Gaps      { get; } = new List<(double, double)>();
            public List<string>                   GapLabels { get; } = new List<string>();
        }

        private class DailyDataRaw
        {
            public List<ScottPlotDailyStats> Stats    { get; } = new List<ScottPlotDailyStats>();
            public List<double>              RollingX { get; } = new List<double>();
            public List<double>              RollingY { get; } = new List<double>();
        }
    }
}