using RTAnalyzer.Core;
using LiveCharts;
using LiveCharts.Configurations;
using LiveCharts.Defaults;
using LiveCharts.Wpf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;

namespace RTAnalyzer.Charts.Builders
{
    public class HistogramChart : IChartDataBuilder
    {
        private const int    TargetBucketCount  = 60;
        private const int    MaxBucketCount     = 120;
        private const double MergeBelowFraction = 0.003;

        public ChartType GetChartType() => ChartType.Histogram;

        public bool CanBuild(List<ResponseRecord> records) => records.Count > 0;

        public ChartData Build(ChartInputData input)
        {
            var    items = input.Records;
            double avg   = input.Average;
            int    p95   = input.P95;
            int    minMs = items.Min(r => r.ResponseTime);
            int    maxMs = items.Max(r => r.ResponseTime);

            int bucketSize   = CalcBucketSize(items);
            var distribution = BuildDistribution(items, bucketSize);
            if (distribution.Count == 0) return null;

            distribution = MergeIsolatedTinyBuckets(distribution, items.Count);

            var buckets    = BuildBuckets(distribution, bucketSize, avg, p95);
            var series     = BuildSeriesCollection(buckets);

            return new ChartData
            {
                Charts = new List<ChartSeries>
                {
                    new ChartSeries
                    {
                        Name    = $"Occurrences by Response Time ({minMs}–{maxMs} ms)",
                        Series  = series,
                        Labels  = buckets.Select(b => b.Label).ToArray(),
                        Buckets = buckets
                    }
                },
                FilteredRecords = items
            };
        }

        // ── Bucket Sizing ─────────────────────────────────────────────────────

        private static int CalcBucketSize(List<ResponseRecord> items)
        {
            var sorted    = items.Select(r => r.ResponseTime).OrderBy(x => x).ToList();
            int n         = sorted.Count;
            int fullRange = Math.Max(1, sorted[n - 1] - sorted[0]);

            // Freedman-Diaconis rule: optimal bin width = 2 * IQR * n^(-1/3)
            double p25 = sorted[(int)(n * 0.25)];
            double p75 = sorted[(int)(n * 0.75)];
            double iqr = p75 - p25;

            double fdSize = iqr > 0
                ? 2.0 * iqr * Math.Pow(n, -1.0 / 3.0)
                : fullRange / 30.0;

            // Round to nearest multiple of 5
            int bucketSize = Math.Max(1, (int)(Math.Ceiling(fdSize / 5.0) * 5));

            // Hard cap: never more than 60 buckets across full range
            int[] steps = { 5, 10, 15, 20, 25, 50, 100, 200, 500, 1000 };
            while ((int)Math.Ceiling(fullRange / (double)bucketSize) > 60)
            {
                int next = steps.FirstOrDefault(s => s > bucketSize);
                if (next == 0) { bucketSize = steps.Last(); break; }
                bucketSize = next;
            }

            // Hard floor: never fewer than 15 buckets in core range (P5-P95)
            int p5        = sorted[(int)(n * 0.05)];
            int p95       = sorted[(int)(n * 0.95)];
            int coreRange = Math.Max(1, p95 - p5);
            while (bucketSize > 5 && (int)Math.Ceiling(coreRange / (double)bucketSize) < 15)
            {
                int prev = steps.LastOrDefault(s => s < bucketSize);
                if (prev == 0) break;
                bucketSize = prev;
            }

            return bucketSize;
        }
        private static Dictionary<int, int> BuildDistribution(List<ResponseRecord> items, int bucketSize)
        {
            var dist = new Dictionary<int, int>();
            foreach (var r in items)
            {
                int bucket = (int)Math.Floor(r.ResponseTime / (double)bucketSize) * bucketSize;
                if (dist.ContainsKey(bucket)) dist[bucket]++;
                else dist[bucket] = 1;
            }
            return dist;
        }

        private static Dictionary<int, int> MergeIsolatedTinyBuckets(Dictionary<int, int> dist, int totalRecords)
        {
            if (dist.Count <= 4) return dist;
            int threshold = Math.Max(1, (int)(totalRecords * MergeBelowFraction));
            var ordered   = dist.OrderBy(kv => kv.Key).ToList();
            var result    = new Dictionary<int, int>(dist);

            for (int i = 0; i < ordered.Count; i++)
            {
                if (ordered[i].Value >= threshold) continue;
                bool prevTiny = i == 0               || ordered[i-1].Value < threshold;
                bool nextTiny = i == ordered.Count-1 || ordered[i+1].Value < threshold;
                if (!prevTiny || !nextTiny) continue;
                if (i < ordered.Count - 1)
                {
                    result[ordered[i+1].Key] = (result.ContainsKey(ordered[i+1].Key) ? result[ordered[i+1].Key] : 0) + ordered[i].Value;
                    result.Remove(ordered[i].Key);
                }
            }
            return result;
        }

        // ── Buckets ───────────────────────────────────────────────────────────

        private static List<ChartBucket> BuildBuckets(Dictionary<int, int> distribution, int bucketSize, double avg, int p95)
        {
            var buckets = new List<ChartBucket>();
            int index   = 0;

            // Determine which bucket contains AVG and P95
            int avgBucket = (int)Math.Floor(avg / bucketSize) * bucketSize;
            int p95Bucket = (int)Math.Floor(p95 / (double)bucketSize) * bucketSize;

            foreach (var key in distribution.Keys.OrderBy(k => k))
            {
                bool isAvg = key == avgBucket;
                bool isP95 = key == p95Bucket;

                // Label: show AVG/P95 tag inside the bar label if applicable
                string barLabel = isAvg && isP95 ? "⌀ AVG  |  P₉₅"
                                : isAvg           ? "⌀ AVG"
                                : isP95           ? "P₉₅"
                                : "";

                buckets.Add(new ChartBucket
                {
                    Index      = index++,
                    BucketMs   = key,
                    Count      = distribution[key],
                    Label      = key + "–" + (key + bucketSize - 1) + " ms",
                    BarColor   = GetBarColor(key, avg, isAvg, isP95),
                    RangeStart = key,
                    RangeEnd   = key + bucketSize - 1,
                    BarLabel   = barLabel
                });
            }
            return buckets;
        }

        private static SeriesCollection BuildSeriesCollection(List<ChartBucket> buckets)
        {
            var mapper = Mappers.Xy<ChartBucket>()
                .X(b => b.Count)
                .Y(b => b.Index)
                .Fill(b => b.BarColor);

            var series = new SeriesCollection();
            series.Add(new RowSeries(mapper)
            {
                Title        = "",
                Values       = new ChartValues<ChartBucket>(buckets),
                DataLabels   = true,
                MaxRowHeigth = 16,
                Foreground   = new SolidColorBrush(Color.FromRgb(200, 210, 220)),
                FontSize     = 11,
                LabelPoint   = p =>
                {
                    int idx = (int)Math.Round(p.Y);
                    if (idx < 0 || idx >= buckets.Count) return "";

                    string tag   = buckets[idx].BarLabel;
                    int    count = (int)p.X;

                    // Always show count if bar is wide enough (count >= 2)
                    string countS = count >= 2 ? count.ToString("N0") : "";

                    if (!string.IsNullOrEmpty(tag))
                        return string.IsNullOrEmpty(countS) ? tag : $"{countS}  ← {tag}";

                    return countS;
                }
            });

            return series;
        }

        // ── Colors ────────────────────────────────────────────────────────────

        private static SolidColorBrush GetBarColor(int bucketMs, double avg, bool isAvg, bool isP95)
        {
            // Highlight AVG/P95 buckets with brighter border-like color
            if (isAvg && isP95) return new SolidColorBrush(Color.FromRgb(180, 100, 220));
            if (isAvg)          return new SolidColorBrush(Color.FromRgb(56, 182, 255));
            if (isP95)          return new SolidColorBrush(Color.FromRgb(180, 80, 220));

            if (avg <= 0) return new SolidColorBrush(Color.FromRgb(100, 180, 100));
            double ratio = bucketMs / avg;

            var palette = new[]
            {
                (0.00, (0,   30,  90)), (0.20, (0,  110,   0)), (0.50, ( 34, 160,  50)),
                (0.80, (100, 208, 135)), (1.00, (160, 230, 180)), (1.08, (100, 180, 210)),
                (1.15, ( 41, 128, 185)), (1.25, (255, 245, 150)), (1.40, (255, 220,  90)),
                (1.60, (255, 195,  57)), (1.80, (249, 168,  29)), (2.10, (243, 140,  25)),
                (2.40, (230, 115,  30)), (2.70, (210,  95,  40)), (3.00, (200,  80,  60)),
                (4.00, (160,  50, 110)), (6.00, (120,  35,  70)), (8.00, (100,  30,  60))
            };

            if (ratio <= palette[0].Item1) { var c = palette[0].Item2; return new SolidColorBrush(Color.FromRgb((byte)c.Item1,(byte)c.Item2,(byte)c.Item3)); }
            if (ratio >= palette[palette.Length-1].Item1) { var c = palette[palette.Length-1].Item2; return new SolidColorBrush(Color.FromRgb((byte)c.Item1,(byte)c.Item2,(byte)c.Item3)); }

            for (int i = 0; i < palette.Length - 1; i++)
            {
                if (ratio >= palette[i].Item1 && ratio <= palette[i+1].Item1)
                {
                    double t = (ratio - palette[i].Item1) / (palette[i+1].Item1 - palette[i].Item1);
                    int r = (int)(palette[i].Item2.Item1 + t * (palette[i+1].Item2.Item1 - palette[i].Item2.Item1));
                    int g = (int)(palette[i].Item2.Item2 + t * (palette[i+1].Item2.Item2 - palette[i].Item2.Item2));
                    int b = (int)(palette[i].Item2.Item3 + t * (palette[i+1].Item2.Item3 - palette[i].Item2.Item3));
                    return new SolidColorBrush(Color.FromRgb((byte)r,(byte)g,(byte)b));
                }
            }
            return new SolidColorBrush(Color.FromRgb(192, 57, 43));
        }
    }
}