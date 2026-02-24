using LiveCharts;
using LiveCharts.Configurations;
using LiveCharts.Defaults;
using LiveCharts.Wpf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;

namespace RTAnalyzer.Builders
{
    public class HistogramChartBuilder
    {
        public List<ChartSeries> BuildCharts(List<ResponseRecord> items)
        {
            if (items.Count == 0) return null;

            var bucketSize = ChartCalculator.GetAdaptiveBucketSize(items);
            var distribution = ChartCalculator.BuildDistribution(items, bucketSize);
            if (distribution.Count == 0) return null;

            double avg = items.Average(r => (double)r.ResponseTime);
            double stdDev = Math.Sqrt(items.Average(r => Math.Pow(r.ResponseTime - avg, 2)));
            int p95 = ChartCalculator.GetPercentile(items, 0.95);

            int maxCount = 0;
            foreach (var count in distribution.Values)
                if (count > maxCount)
                    maxCount = count;

            int minVisibleCount = (int)(maxCount * 0.01);

            var allBuckets = BuildVisibleBuckets(distribution, minVisibleCount, avg, bucketSize);

            var charts = new List<ChartSeries>();

            int splitPoint = FindSplitPoint(allBuckets);

            if (splitPoint < 0 || allBuckets.Count < 5)
            {
                int minMs = items.Min(r => r.ResponseTime);
                int maxMs = items.Max(r => r.ResponseTime);
                charts.Add(BuildChartSeries($"{minMs}-{maxMs} ms", allBuckets, avg, stdDev, items, p95, showAvg: true,
                    showSpikes: true));
            }
            else
            {
                int splitMs = allBuckets[splitPoint].BucketMs;

                var mainBuckets = new List<ChartBucket>();
                var tailBuckets = new List<ChartBucket>();

                for (int i = 0; i < allBuckets.Count; i++)
                {
                    if (i < splitPoint)
                    {
                        var b = allBuckets[i];
                        mainBuckets.Add(new ChartBucket
                        {
                            Index = mainBuckets.Count,
                            BucketMs = b.BucketMs,
                            Count = b.Count,
                            Label = b.Label,
                            BarColor = b.BarColor,
                            RangeStart = b.RangeStart,
                            RangeEnd = b.RangeEnd
                        });
                    }
                    else
                    {
                        var b = allBuckets[i];
                        tailBuckets.Add(new ChartBucket
                        {
                            Index = tailBuckets.Count,
                            BucketMs = b.BucketMs,
                            Count = b.Count,
                            Label = b.Label,
                            BarColor = b.BarColor,
                            RangeStart = b.RangeStart,
                            RangeEnd = b.RangeEnd
                        });
                    }
                }

                var mainItems = new List<ResponseRecord>();
                var tailItems = new List<ResponseRecord>();

                foreach (var item in items)
                {
                    if (item.ResponseTime < splitMs)
                        mainItems.Add(item);
                    else
                        tailItems.Add(item);
                }

                if (mainBuckets.Count > 0 && mainItems.Count > 0)
                {
                    int mainMin = mainItems.Min(r => r.ResponseTime);
                    int mainMax = mainItems.Max(r => r.ResponseTime);
                    charts.Add(BuildChartSeries($"{mainMin}-{mainMax} ms", mainBuckets, avg, stdDev, mainItems, p95,
                        showAvg: true, showSpikes: false));
                }

                if (tailBuckets.Count > 0 && tailItems.Count > 0)
                {
                    int tailMin = tailItems.Min(r => r.ResponseTime);
                    int tailMax = tailItems.Max(r => r.ResponseTime);
                    charts.Add(BuildChartSeries($"{tailMin}-{tailMax} ms", tailBuckets, avg, stdDev, tailItems, p95,
                        showAvg: false, showSpikes: true));
                }
            }

            return charts;
        }

        private static int FindSplitPoint(List<ChartBucket> buckets)
        {
            if (buckets.Count < 5) return -1;

            int maxCount = 0;
            foreach (var b in buckets)
                if ((int)b.Count > maxCount)
                    maxCount = (int)b.Count;

            for (int i = buckets.Count / 2; i < buckets.Count - 2; i++)
            {
                int leftMax = 0;
                for (int j = 0; j <= i; j++)
                    if ((int)buckets[j].Count > leftMax)
                        leftMax = (int)buckets[j].Count;

                int rightMax = 0;
                for (int j = i + 1; j < buckets.Count; j++)
                    if ((int)buckets[j].Count > rightMax)
                        rightMax = (int)buckets[j].Count;

                if (rightMax > 0 && leftMax / rightMax > 10)
                    return i + 1;
            }

            return -1;
        }

        private static List<ChartBucket> BuildVisibleBuckets(
            Dictionary<int, int> distribution, int minVisibleCount, double avg, int bucketSize)
        {
            var sortedKeys = distribution.Keys.OrderBy(k => k).ToList();
            var buckets = new List<ChartBucket>();
            int index = 0;

            int mergeStart = -1;
            int mergeEnd = -1;
            int mergeCount = 0;

            foreach (var key in sortedKeys)
            {
                int count = distribution[key];

                if (count >= minVisibleCount)
                {
                    if (mergeStart >= 0)
                    {
                        string mergeLabel = mergeStart == mergeEnd
                            ? mergeStart.ToString()
                            : $"{mergeStart}-{mergeEnd + bucketSize - 1}";

                        buckets.Add(new ChartBucket
                        {
                            Index = index++,
                            BucketMs = (mergeStart + mergeEnd) / 2,
                            Count = mergeCount,
                            Label = mergeLabel,
                            BarColor = GetBarColor((mergeStart + mergeEnd) / 2, avg),
                            RangeStart = mergeStart,
                            RangeEnd = mergeEnd + bucketSize - 1
                        });

                        mergeStart = -1;
                        mergeCount = 0;
                    }

                    buckets.Add(new ChartBucket
                    {
                        Index = index++,
                        BucketMs = key,
                        Count = count,
                        Label = key.ToString(),
                        BarColor = GetBarColor(key, avg),
                        RangeStart = key,
                        RangeEnd = key + bucketSize - 1
                    });
                }
                else
                {
                    if (mergeStart < 0) mergeStart = key;
                    mergeEnd = key;
                    mergeCount += count;

                    if (mergeCount >= minVisibleCount)
                    {
                        string mergeLabel = mergeStart == mergeEnd
                            ? mergeStart.ToString()
                            : $"{mergeStart}-{mergeEnd + bucketSize - 1}";

                        buckets.Add(new ChartBucket
                        {
                            Index = index++,
                            BucketMs = (mergeStart + mergeEnd) / 2,
                            Count = mergeCount,
                            Label = mergeLabel,
                            BarColor = GetBarColor((mergeStart + mergeEnd) / 2, avg),
                            RangeStart = mergeStart,
                            RangeEnd = mergeEnd + bucketSize - 1
                        });

                        mergeStart = -1;
                        mergeCount = 0;
                    }
                }
            }

            if (mergeStart >= 0)
            {
                string finalLabel = mergeStart == mergeEnd
                    ? mergeStart.ToString()
                    : $"{mergeStart}-{mergeEnd + bucketSize - 1}";

                buckets.Add(new ChartBucket
                {
                    Index = index++,
                    BucketMs = (mergeStart + mergeEnd) / 2,
                    Count = mergeCount,
                    Label = finalLabel,
                    BarColor = GetBarColor((mergeStart + mergeEnd) / 2, avg),
                    RangeStart = mergeStart,
                    RangeEnd = mergeEnd + bucketSize - 1
                });
            }

            return buckets;
        }

        private void AddColumnSeries(List<ChartBucket> buckets, SeriesCollection series)
        {
            var mapper = Mappers.Xy<ChartBucket>()
                .X(p => p.Index)
                .Y(p => p.Count)
                .Fill(p => p.BarColor);

            var barValues = new ChartValues<ChartBucket>();
            foreach (var b in buckets)
                barValues.Add(b);

            var bucketsCapture = buckets;
            series.Add(new ColumnSeries(mapper)
            {
                Title = "",
                Values = barValues,
                DataLabels = true,
                MaxColumnWidth = 200,
                LabelPoint = p => GetLabel(p, bucketsCapture)
            });
        }

        private static string GetLabel(ChartPoint p, List<ChartBucket> buckets)
        {
            int idx = (int)p.X;
            if (idx < 0 || idx >= buckets.Count) return "";
            int cnt = (int)buckets[idx].Count;
            return cnt < 3 ? "" : cnt.ToString();
        }

        private ChartSeries BuildChartSeries(
            string name, List<ChartBucket> buckets, double avg, double stdDev, List<ResponseRecord> items,
            int p95, bool showAvg = true, bool showSpikes = false)
        {
            var series = new SeriesCollection();
            AddColumnSeries(buckets, series);
            if (showAvg) AddAvgLine(avg, buckets, series);

            if (showSpikes)
            {
                if (p95 > 0) AddP95Line(p95, buckets, series);
                AddSpikeIndicators(avg, stdDev, buckets, items, series);
            }

            return new ChartSeries
            {
                Name = name,
                Series = series,
                Labels = GetLabels(buckets)
            };
        }

        private static string[] GetLabels(List<ChartBucket> buckets)
        {
            var labels = new List<string>();
            foreach (var b in buckets)
                labels.Add(b.Label);
            return labels.ToArray();
        }

        private static void AddAvgLine(double avg, List<ChartBucket> buckets, SeriesCollection series)
        {
            AddVerticalMarkerLine(avg, buckets, series,
                Color.FromRgb(41, 128, 185),
                $"⌀ AVG ({Math.Round(avg, 0)} ms)",
                goLeft: true);
        }

        private static void AddP95Line(int p95, List<ChartBucket> buckets, SeriesCollection series)
        {
            AddVerticalMarkerLine(p95, buckets, series,
                Color.FromRgb(142, 68, 173),
                $"P₉₅ Speed ({p95} ms)",
                goLeft: false);
        }

        private static void AddVerticalMarkerLine(double value, List<ChartBucket> buckets, SeriesCollection series,
            Color color, string label, bool goLeft)
        {
            var (valueIndex, maxCount) = FindClosestBucketIndex(value, buckets);
            if (valueIndex < 0) return;

            double horizontalY = maxCount * 1.15;
            var (horizontalStart, horizontalEnd) = CalculateHorizontalLine(valueIndex, buckets, horizontalY, goLeft);

            var brush = new SolidColorBrush(color);

            AddVerticalLine(valueIndex, horizontalY, brush, series);
            AddHorizontalLineWithLabel(horizontalStart, horizontalEnd, horizontalY, label, goLeft, brush, series);
        }

        private static (int index, int maxCount) FindClosestBucketIndex(double value, List<ChartBucket> buckets)
        {
            int valueIndex = -1;
            int maxCount = 0;
            double minDist = double.MaxValue;

            for (int i = 0; i < buckets.Count; i++)
            {
                double dist = Math.Abs(buckets[i].BucketMs - value);
                if (dist < minDist)
                {
                    minDist = dist;
                    valueIndex = i;
                }

                if ((int)buckets[i].Count > maxCount)
                    maxCount = (int)buckets[i].Count;
            }

            return (valueIndex, maxCount);
        }

        private static (double start, double end) CalculateHorizontalLine(int valueIndex, List<ChartBucket> buckets,
            double horizontalY, bool goLeft)
        {
            double maxLength = 0.5;

            if (goLeft)
            {
                double start = Math.Max(0, valueIndex - maxLength);
                return (start, valueIndex);
            }
            else
            {
                double end = Math.Min(buckets.Count - 1, valueIndex + maxLength);
                return (valueIndex, end);
            }
        }

        private static void AddVerticalLine(int index, double height, SolidColorBrush brush, SeriesCollection series)
        {
            series.Add(new LineSeries
            {
                Title = "",
                Values = new ChartValues<ObservablePoint>
                {
                    new ObservablePoint(index, 0),
                    new ObservablePoint(index, height)
                },
                Stroke = brush,
                StrokeThickness = 2,
                StrokeDashArray = new DoubleCollection { 4, 2 },
                Fill = Brushes.Transparent,
                PointGeometry = null,
                DataLabels = false
            });
        }

        private static void AddHorizontalLineWithLabel(double start, double end, double y, string label,
            bool goLeft, SolidColorBrush brush, SeriesCollection series)
        {
            double labelX = goLeft ? start : end;
            string labelText = goLeft ? $"{label}  " : $"  {label}";

            series.Add(new LineSeries
            {
                Title = "",
                Values = new ChartValues<ObservablePoint>
                {
                    new ObservablePoint(start, y),
                    new ObservablePoint(end, y)
                },
                Stroke = brush,
                StrokeThickness = 2,
                StrokeDashArray = new DoubleCollection { 4, 2 },
                Fill = Brushes.Transparent,
                PointGeometry = null,
                DataLabels = true,
                LabelPoint = p => p.X == labelX ? labelText : "",
                Foreground = brush,
                FontSize = 13,
                FontWeight = System.Windows.FontWeights.Bold
            });
        }

        private static void AddSpikeIndicators(double avg, double stdDev, List<ChartBucket> buckets,
            List<ResponseRecord> items, SeriesCollection series)
        {
            if (items.Count == 0) return;

            int p95 = ChartCalculator.GetPercentile(items, 0.95);
            int maxCount = ChartCalculator.GetMaxCount(buckets);

            var bucketMaxSpikes = FindMaxSpikesPerBucket(items, buckets, p95);
            if (bucketMaxSpikes.Count == 0) return;

            var (spikeMarkers, spikeMs) = CreateSpikeMarkers(bucketMaxSpikes, buckets, maxCount);
            AddSpikeMarkerSeries(spikeMarkers, spikeMs, series);
        }

        private static Dictionary<int, int> FindMaxSpikesPerBucket(List<ResponseRecord> items,
            List<ChartBucket> buckets, int p95)
        {
            var bucketMaxSpikes = new Dictionary<int, int>();

            foreach (var item in items)
            {
                if (item.ResponseTime < p95) continue;

                int bucketIndex = FindBucketIndex(item.ResponseTime, buckets);
                if (bucketIndex < 0) continue;

                if (!bucketMaxSpikes.ContainsKey(bucketIndex))
                    bucketMaxSpikes[bucketIndex] = item.ResponseTime;
                else if (item.ResponseTime > bucketMaxSpikes[bucketIndex])
                    bucketMaxSpikes[bucketIndex] = item.ResponseTime;
            }

            return bucketMaxSpikes;
        }

        private static int FindBucketIndex(int responseTime, List<ChartBucket> buckets)
        {
            for (int i = 0; i < buckets.Count; i++)
            {
                if (responseTime >= buckets[i].RangeStart && responseTime <= buckets[i].RangeEnd)
                    return i;
            }

            return -1;
        }

        private static (ChartValues<ObservablePoint> markers, List<int> ms) CreateSpikeMarkers(
            Dictionary<int, int> bucketMaxSpikes, List<ChartBucket> buckets, int maxCount)
        {
            var spikeMarkers = new ChartValues<ObservablePoint>();
            var spikeMs = new List<int>();

            var sortedBuckets = bucketMaxSpikes.Keys.OrderBy(k => k).ToList();
            double baseY = maxCount * 1.25;

            for (int i = 0; i < sortedBuckets.Count; i++)
            {
                int bucketIndex = sortedBuckets[i];
                int maxSpikeMs = bucketMaxSpikes[bucketIndex];

                bool hasLeftNeighbor = i > 0 && sortedBuckets[i - 1] == bucketIndex - 1;
                bool hasRightNeighbor = i < sortedBuckets.Count - 1 && sortedBuckets[i + 1] == bucketIndex + 1;

                double yPos = baseY;

                if (hasLeftNeighbor || hasRightNeighbor)
                {
                    if (i % 2 == 0)
                        yPos = baseY + (maxCount * 0.08);
                    else
                        yPos = baseY - (maxCount * 0.08);
                }

                spikeMarkers.Add(new ObservablePoint(bucketIndex, yPos));
                spikeMs.Add(maxSpikeMs);
            }

            return (spikeMarkers, spikeMs);
        }

        private static void AddSpikeMarkerSeries(ChartValues<ObservablePoint> spikeMarkers,
            List<int> spikeMs, SeriesCollection series)
        {
            var spikeMsCapture = spikeMs;
            series.Add(new ScatterSeries
            {
                Title = "",
                Values = spikeMarkers,
                Fill = Brushes.Transparent,
                Stroke = Brushes.Transparent,
                MinPointShapeDiameter = 1,
                MaxPointShapeDiameter = 1,
                PointGeometry = null,
                DataLabels = true,
                LabelPoint = p =>
                {
                    int idx = (int)p.X;
                    int markerIdx = FindMarkerIndex(idx, spikeMarkers);

                    if (markerIdx >= 0 && markerIdx < spikeMsCapture.Count)
                        return $"⚠️ {spikeMsCapture[markerIdx]} ms";
                    return "";
                },
                Foreground = new SolidColorBrush(Color.FromRgb(255, 69, 0)),
                FontSize = 16,
                FontWeight = System.Windows.FontWeights.ExtraBold
            });
        }

        private static int FindMarkerIndex(int idx, ChartValues<ObservablePoint> markers)
        {
            for (int i = 0; i < markers.Count; i++)
            {
                if ((int)markers[i].X == idx)
                    return i;
            }

            return -1;
        }

        private static SolidColorBrush GetBarColor(int bucketMs, double avg)
        {
            if (avg <= 0) return new SolidColorBrush(Color.FromRgb(100, 180, 100));

            double ratio = bucketMs / avg;

            var palette = new[]
            {
                (0.00, (0, 30, 90)), (0.20, (0, 110, 0)), (0.50, (34, 160, 50)), (0.80, (100, 208, 135)),
                (1.00, (160, 230, 180)), (1.08, (100, 180, 210)), (1.15, (41, 128, 185)), (1.25, (255, 245, 150)),
                (1.40, (255, 220, 90)), (1.60, (255, 195, 57)), (1.80, (249, 168, 29)), (2.10, (243, 140, 25)),
                (2.40, (230, 115, 30)), (2.70, (210, 95, 40)), (3.00, (200, 80, 60)), (3.30, (190, 70, 80)),
                (3.60, (175, 60, 95)), (4.00, (160, 50, 110)), (4.50, (145, 45, 100)), (5.00, (135, 40, 85)),
                (6.00, (120, 35, 70)), (8.00, (100, 30, 60))
            };

            return InterpolateColor(ratio, palette);
        }

        private static SolidColorBrush InterpolateColor(double ratio,
            (double ratio, (int r, int g, int b) color)[] palette)
        {
            if (ratio <= palette[0].ratio)
                return new SolidColorBrush(Color.FromRgb((byte)palette[0].color.r, (byte)palette[0].color.g,
                    (byte)palette[0].color.b));

            if (ratio >= palette[palette.Length - 1].ratio)
                return new SolidColorBrush(Color.FromRgb((byte)palette[palette.Length - 1].color.r,
                    (byte)palette[palette.Length - 1].color.g, (byte)palette[palette.Length - 1].color.b));

            for (int i = 0; i < palette.Length - 1; i++)
            {
                if (ratio >= palette[i].ratio && ratio <= palette[i + 1].ratio)
                {
                    double t = (ratio - palette[i].ratio) / (palette[i + 1].ratio - palette[i].ratio);

                    int r = (int)(palette[i].color.r + t * (palette[i + 1].color.r - palette[i].color.r));
                    int g = (int)(palette[i].color.g + t * (palette[i + 1].color.g - palette[i].color.g));
                    int b = (int)(palette[i].color.b + t * (palette[i + 1].color.b - palette[i].color.b));

                    return new SolidColorBrush(Color.FromRgb((byte)r, (byte)g, (byte)b));
                }
            }

            return new SolidColorBrush(Color.FromRgb(192, 57, 43));
        }
    }
}