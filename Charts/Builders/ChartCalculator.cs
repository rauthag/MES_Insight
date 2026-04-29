using System;
using System.Collections.Generic;
using System.Linq;
using MESInsight.Core;

namespace MESInsight.Charts.Builders
{
    public static class ChartCalculator
    {
        // Finds the value at the given percentile position without sorting the entire array
        public static int GetPercentile(List<ResponseRecord> items, double percentile)
        {
            if (items.Count == 0) return 0;
            if (items.Count == 1) return items[0].ResponseTime;

            var times = new int[items.Count];
            for (int i = 0; i < items.Count; i++)
                times[i] = items[i].ResponseTime;

            int targetPosition = (int)(times.Length * percentile);
            return FindValueAtPosition(times, 0, times.Length - 1, targetPosition);
        }

        // Narrows the search range each call until the value at targetPosition is found
        private static int FindValueAtPosition(int[] times, int left, int right, int targetPosition)
        {
            if (left == right) return times[left];

            int pivotFinalIndex = PlacePivotInFinalPosition(times, left, right);

            if (targetPosition == pivotFinalIndex) return times[pivotFinalIndex];
            if (targetPosition < pivotFinalIndex)
                return FindValueAtPosition(times, left, pivotFinalIndex - 1, targetPosition);
            return FindValueAtPosition(times, pivotFinalIndex + 1, right, targetPosition);
        }

        // Moves all values smaller than pivot to its left, returns pivot's final index
        private static int PlacePivotInFinalPosition(int[] times, int left, int right)
        {
            int pivot = times[right];
            int insertAt = left - 1;

            for (int j = left; j < right; j++)
            {
                if (times[j] <= pivot)
                {
                    insertAt++;
                    int tmp = times[insertAt];
                    times[insertAt] = times[j];
                    times[j] = tmp;
                }
            }

            int tmp2 = times[insertAt + 1];
            times[insertAt + 1] = times[right];
            times[right] = tmp2;
            return insertAt + 1;
        }

        public static int GetAdaptiveBucketSize(List<ResponseRecord> items)
        {
            var times = new int[items.Count];
            for (int i = 0; i < items.Count; i++)
                times[i] = items[i].ResponseTime;
            Array.Sort(times);

            int p5 = times[(int)(times.Length * 0.05)];
            int p95 = times[(int)(times.Length * 0.95)];
            int range = p95 - p5;

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