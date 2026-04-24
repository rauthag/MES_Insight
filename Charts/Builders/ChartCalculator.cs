using System;
using System.Collections.Generic;
using System.Linq;
using MESInsight.Core;

namespace MESInsight.Charts.Builders
{
    public static class ChartCalculator
    {
        public static int GetPercentile(List<ResponseRecord> items, double percentile)
        {
            var sorted = items.Select(r => r.ResponseTime).OrderBy(x => x).ToList();
            return sorted[(int)(sorted.Count * percentile)];
        }

        public static int GetAdaptiveBucketSize(List<ResponseRecord> items)
        {
            var sorted = items.Select(r => r.ResponseTime).OrderBy(x => x).ToList();
            var p5 = sorted[(int)(sorted.Count * 0.05)];
            var p95 = sorted[(int)(sorted.Count * 0.95)];
            var range = p95 - p5;

            if (range <= 50) return 2;
            if (range <= 100) return 5;
            if (range <= 300) return 10;
            if (range <= 1000) return 25;
            return 50;
        }

        public static Dictionary<int, int> BuildDistribution(List<ResponseRecord> items, int bucketSize)
        {
            var distribution = new Dictionary<int, int>();

            foreach (var r in items)
            {
                var bucket = (int)Math.Floor(r.ResponseTime / (double)bucketSize) * bucketSize;
                if (distribution.ContainsKey(bucket)) distribution[bucket]++;
                else distribution[bucket] = 1;
            }

            return distribution;
        }

        public static int GetMaxCount(List<ChartBucket> buckets)
        {
            var maxCount = 0;
            foreach (var b in buckets)
                if ((int)b.Count > maxCount)
                    maxCount = (int)b.Count;
            return maxCount;
        }
    }
}