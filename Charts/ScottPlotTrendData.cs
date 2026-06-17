using System;
using System.Collections.Generic;

namespace MESInsight.Core
{
    /// <summary>
    /// Data transfer object for ScottPlot trend chart.
    /// Replaces LiveCharts SeriesCollection — pure data, no WPF objects.
    /// Can be built on background thread.
    /// </summary>
    public class ScottPlotTrendData
    {
        // X axis — compressed tick values (long) mapped to positions
        public double[] AvgX           { get; set; }
        public double[] AvgY           { get; set; }

        public double[] P95X           { get; set; }
        public double[] P95Y           { get; set; }

        public double[] RollingAvgX    { get; set; }
        public double[] RollingAvgY    { get; set; }

        // SLA threshold line
        public double   SlaThreshold   { get; set; }
        public double   SlaCompliancePct { get; set; }

        // SLA violation points
        public double[] ViolationX     { get; set; }
        public double[] ViolationY     { get; set; }

        // Per-day stats for tooltips
        public List<ScottPlotDailyStats> DailyStats { get; set; } = new List<ScottPlotDailyStats>();

        // Gap regions to shade
        public List<(double From, double To)> Gaps { get; set; } = new List<(double, double)>();
        public List<string> GapLabels              { get; set; } = new List<string>();

        // Maps compressed X value → real calendar date (for axis labels + click handling)
        public Dictionary<double, DateTime> XToDate { get; set; } = new Dictionary<double, DateTime>();

        // Chart name
        public string Name { get; set; }

        // Y axis range hint
        public double YMin { get; set; }
        public double YMax { get; set; }
    }

    public class ScottPlotDailyStats
    {
        public double   X                 { get; set; }  // compressed axis value
        public DateTime RealDate          { get; set; }
        public double   Avg               { get; set; }
        public int      P95               { get; set; }
        public int      Min               { get; set; }
        public int      Max               { get; set; }
        public int      Count             { get; set; }
        public int      SlaViolationCount { get; set; }
    }
}