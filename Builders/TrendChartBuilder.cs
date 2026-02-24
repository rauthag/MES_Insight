using System;
using System.Collections.Generic;
using System.Linq;
using LiveCharts;
using LiveCharts.Wpf;
using LiveCharts.Defaults;
using System.Windows.Media;

namespace RTAnalyzer.Builders
{
    public class TrendChartBuilder
    {
public ChartSeries BuildChart(List<ResponseRecord> items, MessageType type)
{
    if (items.Count == 0) return null;

    var timeGroups = GroupByDay(items);
    if (timeGroups.Count == 0) return null;
    
    if (timeGroups.Count < 2) return null;

    var avgValues = new ChartValues<DateTimePoint>();
    var p95Values = new ChartValues<DateTimePoint>();
    var volumeValues = new ChartValues<DateTimePoint>();
    var rollingAvgValues = new ChartValues<DateTimePoint>();
    var dailyStats = new List<DailyStats>();

    var orderedGroups = timeGroups.OrderBy(g => g.Key).ToList();

    int samplingRate = orderedGroups.Count > 90 ? 2 : 1;

    for (int i = 0; i < orderedGroups.Count; i += samplingRate)
    {
        var group = orderedGroups[i];
        double avgMs = group.Value.Average(r => r.ResponseTime);
        int p95Ms = ChartCalculator.GetPercentile(group.Value, 0.95);
        int minMs = group.Value.Min(r => r.ResponseTime);
        int maxMs = group.Value.Max(r => r.ResponseTime);
        int count = group.Value.Count;

        avgValues.Add(new DateTimePoint(group.Key, avgMs));
        p95Values.Add(new DateTimePoint(group.Key, p95Ms));
        volumeValues.Add(new DateTimePoint(group.Key, count));

        dailyStats.Add(new DailyStats
        {
            Date = group.Key,
            Avg = avgMs,
            P95 = p95Ms,
            Min = minMs,
            Max = maxMs,
            Count = count
        });

        double rollingAvg = CalculateRollingAverage(orderedGroups, i, 7);
        if (rollingAvg > 0)
        {
            rollingAvgValues.Add(new DateTimePoint(group.Key, rollingAvg));
        }
    }
    
    if (avgValues.Count < 2 || p95Values.Count < 2)
    {
        return null;
    }

    var series = new SeriesCollection
    {
        new LineSeries
        {
            Title = "AVG",
            Values = avgValues,
            Stroke = new SolidColorBrush(Color.FromRgb(41, 128, 185)),
            StrokeThickness = 2,
            Fill = new SolidColorBrush(Color.FromArgb(30, 41, 128, 185)),
            PointGeometry = DefaultGeometries.Circle,
            PointGeometrySize = 6,
            LineSmoothness = 0
        },
        new LineSeries
        {
            Title = "P95",
            Values = p95Values,
            Stroke = new SolidColorBrush(Color.FromRgb(142, 68, 173)),
            StrokeThickness = 2,
            Fill = Brushes.Transparent,
            PointGeometry = DefaultGeometries.Diamond,
            PointGeometrySize = 6,
            LineSmoothness = 0
        },
        new LineSeries
        {
            Title = "7-Day AVG",
            Values = rollingAvgValues,
            Stroke = new SolidColorBrush(Color.FromRgb(241, 196, 15)),
            StrokeThickness = 3,
            Fill = Brushes.Transparent,
            PointGeometry = null,
            LineSmoothness = 0,
            StrokeDashArray = new DoubleCollection { 8, 4 }
        }
    };

    int slaThreshold = GetSlaThreshold(items);
    
    AddSlaThresholdLine(series, timeGroups.Keys.Min(), timeGroups.Keys.Max(), p95Values, slaThreshold);
    AddVolumeSeries(series, volumeValues);

    var chartSeries = new ChartSeries
    {
        Name = "Response Time Over Time (Daily)",
        Series = series,
        Labels = null
    };

    return chartSeries;
}

        private double CalculateRollingAverage(List<KeyValuePair<DateTime, List<ResponseRecord>>> groups,
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

        private List<PeakDay> FindPeakDays(List<DailyStats> stats, int slaThreshold)
        {
            var peaks = new List<PeakDay>();

            foreach (var stat in stats)
            {
                if (stat.P95 > slaThreshold)
                {
                    peaks.Add(new PeakDay
                    {
                        Date = stat.Date,
                        Value = stat.P95,
                        Type = PeakType.Bad
                    });
                }
            }

            return peaks;
        }

        private int GetSlaThreshold(List<ResponseRecord> items)
        {
            int p99 = ChartCalculator.GetPercentile(items, 0.99);
            return RoundUpToNiceNumber(p99);
        }

        private int RoundUpToNiceNumber(int value)
        {
            if (value <= 50) return ((value + 9) / 10) * 10;
            if (value <= 100) return ((value + 24) / 25) * 25;
            if (value <= 500) return ((value + 49) / 50) * 50;
            return ((value + 99) / 100) * 100;
        }

        private void AddSlaThresholdLine(SeriesCollection series, DateTime startDate, DateTime endDate,
            ChartValues<DateTimePoint> p95Values, int slaThreshold)
        {
            int underSlaCount = 0;
            foreach (var point in p95Values)
            {
                if (point.Value <= slaThreshold)
                    underSlaCount++;
            }

            double slaCompliance = p95Values.Count > 0
                ? (underSlaCount / (double)p95Values.Count) * 100
                : 0;

            var thresholdValues = new ChartValues<DateTimePoint>
            {
                new DateTimePoint(startDate, slaThreshold),
                new DateTimePoint(endDate, slaThreshold)
            };

            series.Add(new LineSeries
            {
                Title = $"Target: {slaThreshold}ms ({slaCompliance:F0}% OK)",
                Values = thresholdValues,
                Stroke = new SolidColorBrush(Color.FromRgb(231, 76, 60)),
                StrokeThickness = 2,
                StrokeDashArray = new DoubleCollection { 5, 5 },
                Fill = Brushes.Transparent,
                PointGeometry = null
            });
        }

        private void AddVolumeSeries(SeriesCollection series, ChartValues<DateTimePoint> volumeValues)
        {
            series.Add(new ColumnSeries
            {
                Title = "Volume",
                Values = volumeValues,
                Fill = new SolidColorBrush(Color.FromArgb(40, 149, 165, 166)),
                Stroke = new SolidColorBrush(Color.FromArgb(80, 149, 165, 166)),
                StrokeThickness = 1,
                MaxColumnWidth = 10,
                ScalesYAt = 1
            });
        }

        private Dictionary<DateTime, List<ResponseRecord>> GroupByDay(List<ResponseRecord> items)
        {
            var groups = new Dictionary<DateTime, List<ResponseRecord>>();

            foreach (var item in items)
            {
                if (!DateTime.TryParse(item.Timestamp, out DateTime dt)) continue;
                DateTime key = dt.Date;

                if (!groups.ContainsKey(key))
                    groups[key] = new List<ResponseRecord>();

                groups[key].Add(item);
            }

            return groups;
        }

        private class DailyStats
        {
            public DateTime Date { get; set; }
            public double Avg { get; set; }
            public int P95 { get; set; }
            public int Min { get; set; }
            public int Max { get; set; }
            public int Count { get; set; }
        }

        private class PeakDay
        {
            public DateTime Date { get; set; }
            public int Value { get; set; }
            public PeakType Type { get; set; }
        }

        private enum PeakType
        {
            Good,
            Bad
        }
    }
}