using System;
using System.Collections.Generic;
using System.Linq;
using MESInsight.Charts;
using MESInsight.Charts.Interfaces;
using MESInsight.Core;

namespace MESInsight
{
    public class BoxPlotChartBuilder : IChartDataBuilder
    {
        public ChartType GetChartType() => ChartType.BoxPlot;

        public bool CanBuild(List<ResponseRecord> records) => records != null && records.Count >= 5;

        public ChartData Build(ChartInputData input)
        {
            var data = new ChartData
            {
                FilteredRecords = input.Records
            };

            data.BoxPlotFull = ComputeFullPeriodStats(input.Records);
            data.BoxPlotDaily = new BoxPlotData { PerDay = ComputePerDayStats(input.GroupedByDay) };
            data.BoxPlotDaily.FullMin = data.BoxPlotFull.FullMin;
            data.BoxPlotDaily.FullQ1 = data.BoxPlotFull.FullQ1;
            data.BoxPlotDaily.FullMedian = data.BoxPlotFull.FullMedian;
            data.BoxPlotDaily.FullQ3 = data.BoxPlotFull.FullQ3;
            data.BoxPlotDaily.FullMax = data.BoxPlotFull.FullMax;
            data.BoxPlotDaily.FullMean = data.BoxPlotFull.FullMean;
            data.BoxPlotDaily.FullStdDev = data.BoxPlotFull.FullStdDev;
            data.BoxPlotDaily.FullWhiskerLow = data.BoxPlotFull.FullWhiskerLow;
            data.BoxPlotDaily.FullWhiskerHigh = data.BoxPlotFull.FullWhiskerHigh;
            data.BoxPlotDaily.FullOutliers = data.BoxPlotFull.FullOutliers;

            return data;
        }

        private static BoxPlotData ComputeFullPeriodStats(List<ResponseRecord> records)
        {
            var values = records
                .Where(r => r.ResponseTime > 0)
                .Select(r => (double)r.ResponseTime)
                .OrderBy(v => v)
                .ToList();

            if (values.Count < 2) return new BoxPlotData();

            double mean = values.Average();
            double variance = values.Sum(v => (v - mean) * (v - mean)) / values.Count;
            double stdDev = Math.Sqrt(variance);

            double q1 = Percentile(values, 25);
            double median = Percentile(values, 50);
            double q3 = Percentile(values, 75);
            double iqr = q3 - q1;

            double whiskerLow = q1 - 1.5 * iqr;
            double whiskerHigh = q3 + 1.5 * iqr;

            double actualWhiskerLow = values.FirstOrDefault(v => v >= whiskerLow);
            double actualWhiskerHigh = values.LastOrDefault(v => v <= whiskerHigh);

            List<double> outliers = values
                .Where(v => v < whiskerLow || v > whiskerHigh)
                .ToList();

            return new BoxPlotData
            {
                FullMin = values.First(),
                FullQ1 = q1,
                FullMedian = median,
                FullQ3 = q3,
                FullMax = values.Last(),
                FullMean = mean,
                FullStdDev = stdDev,
                FullWhiskerLow = actualWhiskerLow,
                FullWhiskerHigh = actualWhiskerHigh,
                FullOutliers = outliers
            };
        }

        private static List<DayBoxStats> ComputePerDayStats(Dictionary<DateTime, List<ResponseRecord>> byDay)
        {
            var result = new List<DayBoxStats>();

            foreach (var kvp in byDay.OrderBy(k => k.Key))
            {
                var values = kvp.Value
                    .Where(r => r.ResponseTime > 0)
                    .Select(r => (double)r.ResponseTime)
                    .OrderBy(v => v)
                    .ToList();

                if (values.Count < 2) continue;

                double mean = values.Average();
                double variance = values.Sum(v => (v - mean) * (v - mean)) / values.Count;
                double stdDev = Math.Sqrt(variance);

                double q1 = Percentile(values, 25);
                double median = Percentile(values, 50);
                double q3 = Percentile(values, 75);
                double iqr = q3 - q1;

                double whiskerLow = q1 - 1.5 * iqr;
                double whiskerHigh = q3 + 1.5 * iqr;

                double actualWhiskerLow = values.FirstOrDefault(v => v >= whiskerLow);
                double actualWhiskerHigh = values.LastOrDefault(v => v <= whiskerHigh);

                List<double> outliers = values
                    .Where(v => v < whiskerLow || v > whiskerHigh)
                    .ToList();

                result.Add(new DayBoxStats
                {
                    Date = kvp.Key,
                    Min = values.First(),
                    Q1 = q1,
                    Median = median,
                    Q3 = q3,
                    Max = values.Last(),
                    Mean = mean,
                    StdDev = stdDev,
                    WhiskerLow = actualWhiskerLow,
                    WhiskerHigh = actualWhiskerHigh,
                    Outliers = outliers,
                    Count = values.Count
                });
            }

            return result;
        }

        private static double Percentile(List<double> sortedValues, double percentile)
        {
            if (sortedValues.Count == 0) return 0;
            double index = (percentile / 100.0) * (sortedValues.Count - 1);
            int lower = (int)Math.Floor(index);
            int upper = Math.Min(lower + 1, sortedValues.Count - 1);
            double fraction = index - lower;
            return sortedValues[lower] + fraction * (sortedValues[upper] - sortedValues[lower]);
        }
    }
}