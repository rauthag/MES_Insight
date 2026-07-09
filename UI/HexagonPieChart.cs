using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using MESInsight.Core;

namespace MESInsight.UI
{
    public class HexagonPieSlice
    {
        public string Label { get; set; }
        public double Value { get; set; }
        public Color Color { get; set; }
    }

    public static class HexagonPieChart
    {
        private const double GapDeg = 2.2;

        // ── Main chart builder ────────────────────────────────────────────────
        public static UIElement Build(List<HexagonPieSlice> slices, double size = 140,
            string centerText = null, string centerSubtext = null)
        {
            if (slices == null || slices.Count == 0)
                return new Canvas { Width = size, Height = size };

            double total = slices.Sum(s => s.Value);
            if (total <= 0) total = 1;

            double cx = size / 2;
            double cy = size / 2;
            double outerR = size / 2 - 5;
            double innerR = outerR * 0.50;

            var root = new Grid { Width = size, Height = size };
            var canvas = new Canvas { Width = size, Height = size };

            // Subtle hex glow background
            var hexPts = BuildHexagonPoints(cx, cy, outerR + 3);
            canvas.Children.Add(new Polygon
            {
                Points = new PointCollection(hexPts),
                Fill = new SolidColorBrush(Color.FromArgb(12, 63, 185, 80)),
                Stroke = new SolidColorBrush(Color.FromArgb(35, 63, 185, 80)),
                StrokeThickness = 1.0
            });

            // Slices clipped to hexagon
            var clipGeo = BuildHexPathGeometry(hexPts);
            var sliceCanvas = new Canvas { Width = size, Height = size, Clip = clipGeo };

            bool multiSlice = slices.Count(s => s.Value > 0) > 1;
            double gap = multiSlice ? GapDeg : 0;
            double startAngle = -90;

            foreach (var slice in slices)
            {
                if (slice.Value <= 0) continue;
                double fullSweep = slice.Value / total * 360.0;
                double sweep = Math.Max(0.1, fullSweep - gap);
                var geo = BuildDonutSlicePath(cx, cy, outerR, innerR,
                    startAngle + gap / 2, startAngle + gap / 2 + sweep);
                sliceCanvas.Children.Add(new Path
                {
                    Data = geo,
                    Fill = new SolidColorBrush(slice.Color)
                });
                startAngle += fullSweep;
            }

            canvas.Children.Add(sliceCanvas);

            // Hex outline
            canvas.Children.Add(new Polygon
            {
                Points = new PointCollection(hexPts),
                Stroke = new SolidColorBrush(Color.FromRgb(48, 54, 61)),
                StrokeThickness = 1.5,
                Fill = Brushes.Transparent
            });

            // Center hole (donut effect)
            var hole = new Ellipse
            {
                Width = innerR * 2, Height = innerR * 2,
                Fill = new SolidColorBrush(Color.FromRgb(22, 27, 34))
            };
            Canvas.SetLeft(hole, cx - innerR);
            Canvas.SetTop(hole, cy - innerR);
            canvas.Children.Add(hole);

            // Center text
            if (!string.IsNullOrEmpty(centerText))
            {
                var stack = new StackPanel
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                stack.Children.Add(new TextBlock
                {
                    Text = centerText,
                    FontSize = size * 0.148,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromRgb(201, 209, 217)),
                    HorizontalAlignment = HorizontalAlignment.Center
                });
                if (!string.IsNullOrEmpty(centerSubtext))
                    stack.Children.Add(new TextBlock
                    {
                        Text = centerSubtext,
                        FontSize = size * 0.072,
                        Foreground = new SolidColorBrush(Color.FromRgb(110, 118, 129)),
                        HorizontalAlignment = HorizontalAlignment.Center
                    });

                double gridSz = innerR * 1.75;
                var grid = new Grid { Width = gridSz, Height = gridSz };
                grid.Children.Add(stack);
                Canvas.SetLeft(grid, cx - gridSz / 2);
                Canvas.SetTop(grid, cy - gridSz / 2);
                canvas.Children.Add(grid);
            }

            root.Children.Add(canvas);
            return root;
        }

        // ── Full quality widget: chart + legend ───────────────────────────────
        public static UIElement BuildQualityWidget(List<HexagonPieSlice> slices, double chartSize = 130)
        {
            if (slices == null || slices.Count == 0) return new Canvas();

            double total = slices.Sum(s => s.Value);
            if (total <= 0) total = 1;

            var dominant = slices.OrderByDescending(s => s.Value).FirstOrDefault();
            string centerPct = dominant != null
                ? ((int)Math.Round(dominant.Value / total * 100)) + "%"
                : null;
            string centerLbl = dominant?.Label?.ToLower();

            var root = new StackPanel();

            // Chart
            var chartHost = new Grid { HorizontalAlignment = HorizontalAlignment.Center };
            chartHost.Children.Add(Build(slices, chartSize, centerPct, centerLbl));
            root.Children.Add(chartHost);

            // Legend rows
            var legend = new StackPanel { Margin = new Thickness(2, 8, 2, 0) };
            const double barW = 50;

            foreach (var slice in slices.OrderByDescending(s => s.Value))
            {
                if (slice.Value <= 0) continue;
                double pct = slice.Value / total * 100.0;

                var row = new Grid { Margin = new Thickness(0, 0, 0, 5) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                // Dot
                var dot = new Ellipse
                {
                    Width = 6, Height = 6,
                    Fill = new SolidColorBrush(slice.Color),
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(dot, 0);

                // Label
                var lbl = new TextBlock
                {
                    Text = slice.Label,
                    FontSize = 9.5,
                    Foreground = new SolidColorBrush(Color.FromRgb(139, 148, 158)),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(4, 0, 0, 0)
                };
                Grid.SetColumn(lbl, 1);

                // Bar + percentage
                Color barColor = pct >= 85
                    ? Color.FromRgb(46, 160, 67)
                    : pct >= 60
                        ? Color.FromRgb(210, 153, 34)
                        : Color.FromRgb(248, 81, 73);

                // override with slice color if it's a "good" value (pass/process/ok)
                bool isGoodValue = slice.Color == ColorForValue("P") || slice.Color == ColorForValue("Y")
                    || slice.Color == ColorForValue("G");
                Color pctColor = isGoodValue ? barColor : Color.FromRgb(248, 81, 73);

                var barContainer = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

                var barBg = new Grid { Width = barW, Margin = new Thickness(6, 0, 5, 0) };
                barBg.Children.Add(new Border
                {
                    Height = 3, CornerRadius = new CornerRadius(1.5),
                    Background = new SolidColorBrush(Color.FromRgb(33, 38, 45))
                });
                barBg.Children.Add(new Border
                {
                    Height = 3, CornerRadius = new CornerRadius(1.5),
                    Background = new SolidColorBrush(slice.Color),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Width = barW * pct / 100.0
                });
                barContainer.Children.Add(barBg);

                barContainer.Children.Add(new TextBlock
                {
                    Text = pct.ToString("F0") + "%",
                    FontSize = 9,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(pctColor),
                    VerticalAlignment = VerticalAlignment.Center,
                    MinWidth = 28,
                    TextAlignment = TextAlignment.Right
                });

                Grid.SetColumn(barContainer, 2);

                row.Children.Add(dot);
                row.Children.Add(lbl);
                row.Children.Add(barContainer);
                legend.Children.Add(row);
            }

            root.Children.Add(legend);
            return root;
        }

        // ── Static color / label helpers ──────────────────────────────────────
        public static Color ColorForValue(string value)
        {
            switch (value?.ToUpper())
            {
                case "P":  return Color.FromRgb(46,  160,  67);   // Pass / Pass through
                case "F":  return Color.FromRgb(248,  81,  73);   // Fail
                case "Y":  return Color.FromRgb(56,  139, 253);   // Process
                case "N":  return Color.FromRgb(210,  70,  55);   // Reject
                case "G":  return Color.FromRgb(187, 128,   9);   // Golden sample
                case "R":  return Color.FromRgb(219, 109,  40);   // Rework
                case "T":  return Color.FromRgb(130,  80, 220);   // Transient
                case "B":  return Color.FromRgb(155,  89, 182);   // Lock
                case "S":  return Color.FromRgb(100,  60,  30);   // Scrap
                case "-":  return Color.FromRgb( 78,  86,  97);   // Do not process
                default:   return Color.FromRgb(110, 118, 129);
            }
        }

        public static string LabelForValue(string value, MessageType msgType)
        {
            bool isResult = msgType == MessageType.UNIT_RESULT || msgType == MessageType.PANEL_RESULT;

            switch (value?.ToUpper())
            {
                case "Y": return "Process";
                case "N": return "Reject";
                case "G": return "Golden sample";
                case "R": return "Rework";
                case "T": return "Transient";
                case "P": return isResult ? "Pass" : "Pass through";
                case "F": return "Fail";
                case "B": return "Lock";
                case "S": return "Scrap";
                case "-": return "Do not process";
                default:  return value ?? "Unknown";
            }
        }

        public static List<HexagonPieSlice> BuildSlicesFromResults(
            IEnumerable<string> resultValues, MessageType msgType)
        {
            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            int errorCount = 0;

            foreach (var v in resultValues)
            {
                if (string.IsNullOrEmpty(v)) continue;
                if (IsErrorValue(v)) { errorCount++; continue; }
                var key = v.ToUpper();
                if (!counts.ContainsKey(key)) counts[key] = 0;
                counts[key]++;
            }

            var order = new[] { "P", "Y", "G", "T", "R", "N", "F", "-" };
            var slices = counts
                .OrderBy(kv => { int i = Array.IndexOf(order, kv.Key); return i < 0 ? 99 : i; })
                .Select(kv => new HexagonPieSlice
                {
                    Label = LabelForValue(kv.Key, msgType),
                    Value = kv.Value,
                    Color = ColorForValue(kv.Key)
                })
                .ToList();

            if (errorCount > 0)
                slices.Add(new HexagonPieSlice
                {
                    Label = "Errors",
                    Value = errorCount,
                    Color = Color.FromRgb(210, 126, 30)
                });

            return slices;
        }

        private static readonly HashSet<string> KnownResultValues =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Y", "N", "P", "F", "G", "R", "T", "-" };

        private static bool IsErrorValue(string v)
        {
            if (string.IsNullOrEmpty(v)) return false;
            return !KnownResultValues.Contains(v);
        }

        // ── Geometry helpers ──────────────────────────────────────────────────
        private static Geometry BuildDonutSlicePath(double cx, double cy, double outerR, double innerR,
            double startDeg, double endDeg)
        {
            double sweep = endDeg - startDeg;
            if (sweep >= 359.9)
            {
                var g = new GeometryGroup { FillRule = FillRule.EvenOdd };
                g.Children.Add(new EllipseGeometry(new Point(cx, cy), outerR, outerR));
                g.Children.Add(new EllipseGeometry(new Point(cx, cy), innerR, innerR));
                return g;
            }

            double sRad = startDeg * Math.PI / 180;
            double eRad = endDeg   * Math.PI / 180;
            var oS = new Point(cx + outerR * Math.Cos(sRad), cy + outerR * Math.Sin(sRad));
            var oE = new Point(cx + outerR * Math.Cos(eRad), cy + outerR * Math.Sin(eRad));
            var iS = new Point(cx + innerR * Math.Cos(sRad), cy + innerR * Math.Sin(sRad));
            var iE = new Point(cx + innerR * Math.Cos(eRad), cy + innerR * Math.Sin(eRad));
            bool large = sweep > 180;

            var fig = new PathFigure { StartPoint = oS, IsClosed = true };
            fig.Segments.Add(new ArcSegment(oE, new Size(outerR, outerR), 0, large, SweepDirection.Clockwise, true));
            fig.Segments.Add(new LineSegment(iE, true));
            fig.Segments.Add(new ArcSegment(iS, new Size(innerR, innerR), 0, large, SweepDirection.Counterclockwise, true));

            var geo = new PathGeometry();
            geo.Figures.Add(fig);
            return geo;
        }

        private static List<Point> BuildHexagonPoints(double cx, double cy, double r)
        {
            var pts = new List<Point>();
            for (int i = 0; i < 6; i++)
            {
                double a = Math.PI / 180 * (60 * i - 90);
                pts.Add(new Point(cx + r * Math.Cos(a), cy + r * Math.Sin(a)));
            }
            return pts;
        }

        private static PathGeometry BuildHexPathGeometry(List<Point> pts)
        {
            var fig = new PathFigure { StartPoint = pts[0], IsClosed = true };
            for (int i = 1; i < pts.Count; i++)
                fig.Segments.Add(new LineSegment(pts[i], true));
            var geo = new PathGeometry();
            geo.Figures.Add(fig);
            return geo;
        }
    }
}