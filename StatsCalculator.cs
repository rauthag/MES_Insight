using System;
using System.Collections.Generic;
using System.Windows.Media;

namespace RTAnalyzer
{
    public class StatsResult
    {
        public int    Count          { get; set; }
        public double Average        { get; set; }
        public double P95            { get; set; }
        public double Min            { get; set; }
        public double Max            { get; set; }
        public double StdDev         { get; set; }
        public double CV             { get; set; }
        public string StabilityLabel { get; set; }
        public Color  StabilityColor { get; set; }
    }

    public class StatsCalculator
    {
        public StatsResult Calculate(List<ResponseRecord> records, MessageType type)
        {
            var values = new List<double>();
            foreach (var r in records)
            {
                if (r.Type == type)
                    values.Add(r.ResponseTime);
            }

            if (values.Count == 0) return null;

            values.Sort();

            double sum = 0;
            foreach (double v in values) sum += v;
            double avg = sum / values.Count;

            double sumSquares = 0;
            foreach (double v in values) sumSquares += Math.Pow(v - avg, 2);
            double stdDev = Math.Sqrt(sumSquares / values.Count);

            double cv  = avg > 0 ? (stdDev / avg) * 100 : 0;
            double p95 = values[(int)(values.Count * 0.95)];

            return new StatsResult
            {
                Count          = values.Count,
                Average        = avg,
                P95            = p95,
                Min            = values[0],
                Max            = values[values.Count - 1],
                StdDev         = stdDev,
                CV             = cv,
                StabilityLabel = GetStabilityLabel(cv),
                StabilityColor = GetStabilityColor(cv)
            };
        }

        private string GetStabilityLabel(double cv)
        {
            if (cv < 15) return "Excellent";
            if (cv < 30) return "Good";
            if (cv < 50) return "Unstable";
            return "Critical";
        }

        private Color GetStabilityColor(double cv)
        {
            if (cv < 15) return (Color)ColorConverter.ConvertFromString("#27AE60");
            if (cv < 30) return (Color)ColorConverter.ConvertFromString("#F39C12");
            return           (Color)ColorConverter.ConvertFromString("#C0392B");
        }
    }
}
