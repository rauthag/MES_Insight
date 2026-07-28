using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MESInsight.Charts.Renderers;
using MESInsight.Core;
using ScottPlot.WPF;

namespace MESInsight.Charts.Renderers
{
    public class BoxPlotChartRenderer : ChartRenderer
    {
        private static readonly ScottPlot.Color ColorBg      = new ScottPlot.Color(13, 17, 23);
        private static readonly ScottPlot.Color ColorAxis    = new ScottPlot.Color(100, 150, 115);
        private static readonly ScottPlot.Color ColorGrid    = new ScottPlot.Color(30, 60, 38);
        private static readonly ScottPlot.Color ColorWhisk   = new ScottPlot.Color(63, 185, 80);
        private static readonly ScottPlot.Color ColorMedian  = new ScottPlot.Color(255, 220, 60);
        private static readonly ScottPlot.Color ColorMean    = new ScottPlot.Color(80, 160, 255);
        private static readonly ScottPlot.Color ColorOutlier = new ScottPlot.Color(220, 80, 60);
        private static readonly ScottPlot.Color ColorCurve   = new ScottPlot.Color(63, 185, 80);
        private static readonly ScottPlot.Color ColorSigma1  = new ScottPlot.Color(150, 200, 120);
        private static readonly ScottPlot.Color ColorSigma2  = new ScottPlot.Color(100, 150, 80);
        private static readonly ScottPlot.Color ColorText    = new ScottPlot.Color(140, 190, 155);
        private static readonly ScottPlot.Color ColorActive  = new ScottPlot.Color(63, 185, 80);
        private static readonly ScottPlot.Color ColorNormal  = new ScottPlot.Color(40, 120, 60);
        private static readonly ScottPlot.Color ColorWorse   = new ScottPlot.Color(200, 100, 40);
        private static readonly ScottPlot.Color ColorBetter  = new ScottPlot.Color(40, 160, 100);

        private readonly Action<DateTime> _onDaySelected;
        private const int VisibleDays = 8;

        public BoxPlotChartRenderer(Action<DateTime> onDaySelected = null)
        {
            _onDaySelected = onDaySelected;
        }

        public override ChartType GetChartType() => ChartType.BoxPlot;
        public override int GetMinimumHeight(RenderContext context) => 560;

        public override UIElement Render(ChartData data, RenderContext context)
        {
            if (data?.BoxPlotFull == null || data.BoxPlotDaily?.PerDay == null) return null;

            var days = data.BoxPlotDaily.PerDay;
            if (days.Count == 0) return null;

            StackPanel container = new StackPanel
            {
                Background = new SolidColorBrush(Color.FromRgb(10, 18, 12))
            };

            int[] centerIndexHolder = { Math.Max(0, days.Count - 1) };
            bool[] perDayHolder = { true };

            WpfPlot boxPlot  = new WpfPlot { Height = 407 };
            WpfPlot bellPlot = new WpfPlot { Height = 242 };
            bellPlot.IsHitTestVisible = false;

            var tooltip = new Border
            {
                Background      = new SolidColorBrush(Color.FromArgb(230, 8, 20, 12)),
                BorderBrush     = new SolidColorBrush(Color.FromRgb(40, 100, 60)),
                BorderThickness = new Thickness(1),
                CornerRadius    = new CornerRadius(6),
                Padding         = new Thickness(10, 7, 10, 7),
                Visibility      = Visibility.Collapsed,
                IsHitTestVisible = false,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment   = VerticalAlignment.Top
            };
            var tooltipText = new TextBlock
            {
                FontSize   = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(200, 240, 215)),
                LineHeight = 16
            };
            tooltip.Child = tooltipText;

            Grid plotOverlay = new Grid { Height = 407 };
            plotOverlay.Children.Add(boxPlot);
            plotOverlay.Children.Add(tooltip);

            List<DayBoxStats>[] visibleDaysRef = { null };
            int[] activeLiRef = { 0 };

            boxPlot.MouseMove += (s, e) =>
            {
                var vis = visibleDaysRef[0];
                if (vis == null || vis.Count == 0) { tooltip.Visibility = Visibility.Collapsed; return; }

                System.Windows.Point mousePos = e.GetPosition(plotOverlay);
                double relX = mousePos.X / plotOverlay.ActualWidth;
                double relY = mousePos.Y / plotOverlay.ActualHeight;

                var axLimits = boxPlot.Plot.Axes.GetLimits();
                double dataX = axLimits.Left + relX * (axLimits.Right - axLimits.Left);
                double dataY = axLimits.Top  - relY * (axLimits.Top - axLimits.Bottom);

                int dayIdx = (int)Math.Round(dataX);
                if (dayIdx < 0 || dayIdx >= vis.Count) { tooltip.Visibility = Visibility.Collapsed; return; }

                var d = vis[dayIdx];
                double boxHalfW = 0.3;
                bool overBox = Math.Abs(dataX - dayIdx) <= boxHalfW;

                string zone = "";
                string zoneDesc = "";

                if (overBox)
                {
                    if (dataY > d.Q3)           { zone = "Upper whisker";         zoneDesc = $"Max normal value: {d.WhiskerHigh:N0} ms\nValues above this are outliers (unusually slow)"; }
                    else if (dataY >= d.Median) { zone = "Upper box (Q2 – Q3)";   zoneDesc = $"Q3 = {d.Q3:N0} ms  (75% of responses faster)\nMedian = {d.Median:N0} ms  (50% faster, 50% slower)\nThis zone = upper 25% of typical responses"; }
                    else if (dataY >= d.Q1)     { zone = "Lower box (Q1 – Q2)";   zoneDesc = $"Median = {d.Median:N0} ms  (50th percentile)\nQ1 = {d.Q1:N0} ms  (25% of responses faster)\nThis zone = lower 25% of typical responses"; }
                    else                        { zone = "Lower whisker";          zoneDesc = $"Min normal value: {d.WhiskerLow:N0} ms\nValues below this are outliers (unusually fast)"; }
                }

                tooltipText.Text =
                    $"📅  {d.Date:dd. MMM yyyy}\n" +
                    $"──────────────────────\n" +
                    $"Median   {d.Median,6:N0} ms   50th percentile\n" +
                    $"Mean     {d.Mean,6:N0} ms   average\n" +
                    $"Q1       {d.Q1,6:N0} ms   25th percentile\n" +
                    $"Q3       {d.Q3,6:N0} ms   75th percentile\n" +
                    $"IQR      {(d.Q3-d.Q1),6:N0} ms   middle 50% spread\n" +
                    $"σ        {d.StdDev,6:N0} ms   std deviation\n" +
                    $"Min      {d.Min,6:N0} ms\n" +
                    $"Max      {d.Max,6:N0} ms\n" +
                    $"n        {d.Count,6:N0}     records\n" +
                    $"Outliers {d.Outliers.Count,6}" +
                    (string.IsNullOrEmpty(zone) ? "" : $"\n──────────────────────\n🔍  {zone}\n{zoneDesc}");

                tooltip.Visibility = Visibility.Visible;

                double tx = mousePos.X + 16;
                double ty = mousePos.Y - 10;
                tooltip.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                if (tx + tooltip.DesiredSize.Width > plotOverlay.ActualWidth)
                    tx = mousePos.X - tooltip.DesiredSize.Width - 16;
                if (ty + tooltip.DesiredSize.Height > plotOverlay.ActualHeight)
                    ty = plotOverlay.ActualHeight - tooltip.DesiredSize.Height - 4;

                tooltip.Margin = new Thickness(Math.Max(0, tx), Math.Max(0, ty), 0, 0);
            };

            boxPlot.MouseLeave += (s, e) => tooltip.Visibility = Visibility.Collapsed;

            TextBlock dateLabel = new TextBlock
            {
                FontSize   = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(160, 215, 175)),
                Margin     = new Thickness(14, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            Border btnFull   = null;
            Border btnPerDay = null;
            Border btnPrev   = null;
            Border btnNext   = null;
            WrapPanel statsPanel = null;

            void Refresh()
            {
                int ci = centerIndexHolder[0];
                bool pd = perDayHolder[0];

                if (pd)
                {
                    int lo = Math.Max(0, ci - VisibleDays / 2);
                    int hi = Math.Min(days.Count - 1, lo + VisibleDays - 1);
                    lo = Math.Max(0, hi - VisibleDays + 1);

                    var visible = days.Skip(lo).Take(hi - lo + 1).ToList();
                    var activeDay = days[ci];
                    visibleDaysRef[0] = visible;
                    activeLiRef[0] = ci - lo;

                    dateLabel.Text = activeDay.Date.ToString("dd. MMM yyyy");

                    RenderPerDay(visible, ci - lo, data.BoxPlotFull, boxPlot);
                    RenderBellCurveForDay(activeDay, data.BoxPlotFull, bellPlot);
                    UpdateStatsPanel(statsPanel, activeDay);

                    if (btnPrev != null) btnPrev.Opacity = ci > 0 ? 1.0 : 0.3;
                    if (btnNext != null) btnNext.Opacity = ci < days.Count - 1 ? 1.0 : 0.3;
                }
                else
                {
                    dateLabel.Text = "Full period  —  " +
                        days.First().Date.ToString("dd.MM.yyyy") + " – " +
                        days.Last().Date.ToString("dd.MM.yyyy");

                    RenderFullPeriod(data.BoxPlotFull, boxPlot);
                    RenderBellCurve(data.BoxPlotFull, bellPlot);
                    UpdateStatsPanel(statsPanel, null, data.BoxPlotFull);

                    if (btnPrev != null) btnPrev.Opacity = 0.3;
                    if (btnNext != null) btnNext.Opacity = 0.3;
                }
            }

            void SwitchTo(bool pd)
            {
                perDayHolder[0] = pd;
                SetSwitchActive(btnFull,   !pd);
                SetSwitchActive(btnPerDay,  pd);
                Refresh();
            }

            btnFull   = MakeSwitchBtn("Full period", active: false, onClick: () => SwitchTo(false));
            btnPerDay = MakeSwitchBtn("Per day",     active: true,  onClick: () => SwitchTo(true));

            btnPrev = MakeNavBtn("◀", () =>
            {
                if (!perDayHolder[0]) return;
                if (centerIndexHolder[0] > 0) { centerIndexHolder[0]--; Refresh(); }
            });
            btnNext = MakeNavBtn("▶", () =>
            {
                if (!perDayHolder[0]) return;
                if (centerIndexHolder[0] < days.Count - 1) { centerIndexHolder[0]++; Refresh(); }
            });

            Grid topRow = new Grid { Margin = new Thickness(12, 8, 12, 6) };
            topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            new TextBlock
            {
                Text = "Box Plot", FontSize = 15, FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(140, 190, 155)),
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0)
            }.Let(t => { Grid.SetColumn(t, 0); topRow.Children.Add(t); });

            Grid.SetColumn(btnPerDay, 1); topRow.Children.Add(btnPerDay);
            Grid.SetColumn(btnFull,   2); topRow.Children.Add(btnFull);
            Grid.SetColumn(dateLabel, 3); topRow.Children.Add(dateLabel);

            var dayCountLabel = new TextBlock
            {
                FontSize = 10, Foreground = new SolidColorBrush(Color.FromRgb(80, 120, 90)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 6, 0)
            };
            dayCountLabel.Text = days.Count + " days";
            Grid.SetColumn(dayCountLabel, 4); topRow.Children.Add(dayCountLabel);

            statsPanel = new WrapPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(12, 2, 12, 6)
            };

            var legend = BuildLegend();

            Grid plotWithNav = new Grid();
            plotWithNav.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(48) });
            plotWithNav.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            plotWithNav.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(48) });

            Grid.SetColumn(btnPrev,      0); plotWithNav.Children.Add(btnPrev);
            Grid.SetColumn(plotOverlay,  1); plotWithNav.Children.Add(plotOverlay);
            Grid.SetColumn(btnNext,      2); plotWithNav.Children.Add(btnNext);

            container.Children.Add(topRow);
            container.Children.Add(statsPanel);
            container.Children.Add(plotWithNav);
            container.Children.Add(legend);
            container.Children.Add(bellPlot);

            boxPlot.PreviewMouseWheel += (s, e) => e.Handled = true;
            bellPlot.PreviewMouseWheel += (s, e) => e.Handled = true;

            boxPlot.Loaded  += (s, e) => Refresh();
            bellPlot.Loaded += (s, e) => { };

            container.Tag = (Action<DateTime>)(date =>
            {
                int idx = days.FindIndex(d => d.Date.Date == date.Date);
                if (idx >= 0)
                {
                    centerIndexHolder[0] = idx;
                    perDayHolder[0] = true;
                    SetSwitchActive(btnFull, false);
                    SetSwitchActive(btnPerDay, true);
                    Refresh();
                }
            });

            return container;
        }

        public void NavigateToDay(DateTime date, ChartData data, WpfPlot boxPlot, WpfPlot bellPlot)
        {
        }

        private void RenderPerDay(List<DayBoxStats> visible, int activeLocalIndex, BoxPlotData full, WpfPlot wpfPlot)
        {
            var plt = wpfPlot.Plot;
            plt.Clear();
            plt.Benchmark.IsVisible = false;
            plt.FigureBackground.Color = ColorBg;
            plt.DataBackground.Color   = ColorBg;
            plt.Axes.Color(ColorAxis);
            plt.Grid.MajorLineColor    = ColorGrid;

            double globalMedian = full.FullMedian;

            var allBoxes = new List<ScottPlot.Box>();

            for (int i = 0; i < visible.Count; i++)
            {
                var d = visible[i];
                if (d.Median <= 0) continue;

                bool isActive = i == activeLocalIndex;
                bool isWorse  = d.Median > globalMedian * 1.2;
                bool isBetter = d.Median < globalMedian * 0.8;

                ScottPlot.Color fillColor = isActive  ? new ScottPlot.Color(0, 220, 255).WithAlpha(160)
                                          : isWorse   ? new ScottPlot.Color(220, 60, 60).WithAlpha(120)
                                          : isBetter  ? new ScottPlot.Color(60, 120, 220).WithAlpha(120)
                                          : new ScottPlot.Color(63, 185, 80).WithAlpha(80);

                ScottPlot.Color lineColor = isActive  ? new ScottPlot.Color(0, 240, 255)
                                          : isWorse   ? new ScottPlot.Color(220, 80, 80)
                                          : isBetter  ? new ScottPlot.Color(80, 140, 220)
                                          : new ScottPlot.Color(63, 185, 80);

                allBoxes.Add(new ScottPlot.Box
                {
                    Position   = i,
                    BoxMin     = d.Q1,
                    BoxMax     = d.Q3,
                    WhiskerMin = d.WhiskerLow,
                    WhiskerMax = d.WhiskerHigh,
                    BoxMiddle  = d.Median,
                    FillColor  = fillColor,
                    LineColor  = lineColor,
                    LineWidth  = isActive ? 2f : 1.5f
                });
            }

            if (allBoxes.Count == 0) return;

            double boxWidth = 0.6;
            for (int i = 0; i < allBoxes.Count; i++)
            {
                var b  = allBoxes[i];
                var d  = visible[i < visible.Count ? i : visible.Count - 1];
                bool isActive = i == activeLocalIndex;
                bool isWorse  = d.Median > globalMedian * 1.2;
                bool isBetter = d.Median < globalMedian * 0.8;

                // Base colors per category
                ScottPlot.Color baseColor = isActive  ? new ScottPlot.Color(0, 200, 255)
                                          : isWorse   ? new ScottPlot.Color(220, 70, 70)
                                          : isBetter  ? new ScottPlot.Color(70, 130, 220)
                                          : new ScottPlot.Color(50, 160, 80);

                float lw  = isActive ? 2.5f : 1.5f;
                double x  = b.Position;
                double hw = boxWidth / 2;

                // Lower half Q1→Median — darker fill
                var rectLo = plt.Add.Rectangle(x - hw, x + hw, b.BoxMin, b.BoxMiddle ?? b.BoxMin);
                rectLo.FillColor = baseColor.WithAlpha(isActive ? 100 : 70);
                rectLo.LineColor = baseColor.WithAlpha(isActive ? 220 : 160);
                rectLo.LineWidth = lw;

                // Upper half Median→Q3 — lighter fill with diagonal hatch lines
                var rectHi = plt.Add.Rectangle(x - hw, x + hw, b.BoxMiddle ?? b.BoxMax, b.BoxMax);
                rectHi.FillColor = baseColor.WithAlpha(isActive ? 55 : 35);
                rectHi.LineColor = baseColor.WithAlpha(isActive ? 220 : 160);
                rectHi.LineWidth = lw;

                // Hatch lines in upper half (simulate texture)
                if (b.BoxMiddle.HasValue)
                {
                    double hatchStep = (b.BoxMax - b.BoxMiddle.Value) / 4.0;
                    for (int h = 1; h <= 3; h++)
                    {
                        double hy = b.BoxMiddle.Value + hatchStep * h;
                        var hatch = plt.Add.Line(x - hw, hy, x + hw, hy);
                        hatch.Color     = baseColor.WithAlpha(40);
                        hatch.LineWidth = 0.5f;
                    }
                }

                // Median line
                if (b.BoxMiddle.HasValue)
                {
                    var medLine = plt.Add.Line(x - hw, b.BoxMiddle.Value, x + hw, b.BoxMiddle.Value);
                    medLine.Color     = isActive ? new ScottPlot.Color(255, 240, 0) : new ScottPlot.Color(200, 200, 60);
                    medLine.LineWidth = isActive ? 2.5f : 2f;
                }

                // Whiskers — vo farbe boxu, hrubšie
                ScottPlot.Color whiskerColor = baseColor.WithAlpha(220);
                float wlw = isActive ? 3f : 2f;

                if (b.WhiskerMax.HasValue)
                {
                    var wTop = plt.Add.Line(x, b.BoxMax, x, b.WhiskerMax.Value);
                    wTop.Color = whiskerColor; wTop.LineWidth = wlw;
                    wTop.LinePattern = ScottPlot.LinePattern.Dashed;
                    var wTopCap = plt.Add.Line(x - hw * 0.5, b.WhiskerMax.Value, x + hw * 0.5, b.WhiskerMax.Value);
                    wTopCap.Color = whiskerColor; wTopCap.LineWidth = wlw;
                }
                if (b.WhiskerMin.HasValue)
                {
                    var wBot = plt.Add.Line(x, b.BoxMin, x, b.WhiskerMin.Value);
                    wBot.Color = whiskerColor; wBot.LineWidth = wlw;
                    wBot.LinePattern = ScottPlot.LinePattern.Dashed;
                    var wBotCap = plt.Add.Line(x - hw * 0.5, b.WhiskerMin.Value, x + hw * 0.5, b.WhiskerMin.Value);
                    wBotCap.Color = whiskerColor; wBotCap.LineWidth = wlw;
                }

                // Labels on active box only
                if (isActive && b.BoxMiddle.HasValue)
                {
                    double labelX = x + hw + 0.05;

                    void AddBoxLabel(double y, string text, ScottPlot.Color c)
                    {
                        var lbl = plt.Add.Text(text, labelX, y);
                        lbl.LabelFontColor = c;
                        lbl.LabelFontSize  = 8;
                        lbl.LabelAlignment = ScottPlot.Alignment.MiddleLeft;
                        lbl.LabelBold      = false;
                    }

                    AddBoxLabel(b.BoxMax,            $"Q3 {d.Q3:N0}ms",     new ScottPlot.Color(140, 200, 160));
                    AddBoxLabel(b.BoxMiddle.Value,   $"Med {d.Median:N0}ms", new ScottPlot.Color(255, 235, 60));
                    AddBoxLabel(b.BoxMin,            $"Q1 {d.Q1:N0}ms",     new ScottPlot.Color(140, 200, 160));

                    if (b.WhiskerMax.HasValue)
                        AddBoxLabel(b.WhiskerMax.Value, $"↑ {d.WhiskerHigh:N0}ms", new ScottPlot.Color(100, 160, 120));
                    if (b.WhiskerMin.HasValue)
                        AddBoxLabel(b.WhiskerMin.Value, $"↓ {d.WhiskerLow:N0}ms",  new ScottPlot.Color(100, 160, 120));
                }

                // Mean marker — diamond na aktívnom, malá čiarka na ostatných
                if (isActive)
                {
                    var meanLine = plt.Add.Line(x - hw * 0.3, d.Mean, x + hw * 0.3, d.Mean);
                    meanLine.Color     = new ScottPlot.Color(80, 160, 255);
                    meanLine.LineWidth = 2f;
                    meanLine.LinePattern = ScottPlot.LinePattern.Solid;
                }

                // Outlier bodky — väčšie a výraznejšie
                if (d.Outliers.Count > 0)
                {
                    double[] oxs = d.Outliers.Select(_ => x).ToArray();
                    double[] oys = d.Outliers.ToArray();
                    var scatter = plt.Add.Scatter(oxs, oys);
                    scatter.Color      = new ScottPlot.Color(220, 80, 60).WithAlpha(isActive ? 240 : 160);
                    scatter.MarkerSize = isActive ? 8 : 5;
                    scatter.LineWidth  = 0;
                }
            }

            plt.Add.HorizontalLine(globalMedian, color: new ScottPlot.Color(255, 200, 0).WithAlpha(180), width: 2,
                pattern: ScottPlot.LinePattern.Dashed);

            string[] labels   = visible.Select(d => d.Date.ToString("dd.MM")).ToArray();
            double[] positions = Enumerable.Range(0, visible.Count).Select(i => (double)i).ToArray();
            plt.Axes.Bottom.SetTicks(positions, labels);
            plt.Axes.Bottom.TickLabelStyle.FontSize = 10;
            plt.Axes.Left.Label.Text = "Response Time (ms)";
            plt.Title("Distribution per Day");

            plt.Axes.SetLimitsX(-0.5, visible.Count - 0.5);

            var allWhiskerHighs = visible.Where(d => d.WhiskerHigh > 0).Select(d => d.WhiskerHigh).OrderBy(v => v).ToList();
            var allWhiskerLows  = visible.Where(d => d.WhiskerLow  > 0).Select(d => d.WhiskerLow).OrderBy(v => v).ToList();

            if (allWhiskerHighs.Count > 0)
            {
                double p90High  = allWhiskerHighs[(int)(allWhiskerHighs.Count * 0.90)];
                double yMin     = allWhiskerLows.FirstOrDefault();
                double range    = p90High - yMin;
                double minRange = Math.Max(range, globalMedian * 0.6);
                double yPad     = minRange * 0.15;
                plt.Axes.SetLimitsY(Math.Max(0, yMin - yPad), yMin + minRange + yPad);
            }

            plt.Axes.Left.TickLabelStyle.ForeColor = ColorText;
            wpfPlot.Refresh();
        }

        private void RenderFullPeriod(BoxPlotData s, WpfPlot wpfPlot)
        {
            var plt = wpfPlot.Plot;
            plt.Clear();
            plt.Benchmark.IsVisible = false;
            plt.FigureBackground.Color = ColorBg;
            plt.DataBackground.Color   = ColorBg;
            plt.Axes.Color(ColorAxis);
            plt.Grid.MajorLineColor    = ColorGrid;

            if (s.FullMedian <= 0) return;

            plt.Add.Box(new ScottPlot.Box
            {
                Position   = 0,
                BoxMin     = s.FullQ1,
                BoxMax     = s.FullQ3,
                WhiskerMin = s.FullWhiskerLow,
                WhiskerMax = s.FullWhiskerHigh,
                BoxMiddle  = s.FullMedian,
                FillColor  = ColorActive.WithAlpha(80),
                LineColor  = ColorWhisk,
                LineWidth  = 2
            });

            plt.Add.VerticalLine(0, color: ScottPlot.Color.FromARGB(0));

            AddAnnotation(plt, 0, s.FullQ1,  "Q1 " + s.FullQ1.ToString("N0"),  ColorSigma1, ScottPlot.Alignment.UpperRight);
            AddAnnotation(plt, 0, s.FullMedian, "Med " + s.FullMedian.ToString("N0"), ColorMedian, ScottPlot.Alignment.UpperRight);
            AddAnnotation(plt, 0, s.FullQ3,  "Q3 " + s.FullQ3.ToString("N0"),  ColorSigma1, ScottPlot.Alignment.LowerRight);
            AddAnnotation(plt, 0, s.FullMean, "μ " + s.FullMean.ToString("N0"),  ColorMean,  ScottPlot.Alignment.UpperLeft);

            plt.Add.HorizontalLine(s.FullMean, color: ColorMean.WithAlpha(150), width: 1,
                pattern: ScottPlot.LinePattern.Dashed);

            if (s.FullOutliers.Count > 0)
            {
                double[] xs = s.FullOutliers.Select(_ => 0.0).ToArray();
                double[] ys = s.FullOutliers.ToArray();
                var scatter = plt.Add.Scatter(xs, ys);
                scatter.Color      = ColorOutlier;
                scatter.MarkerSize = 5;
                scatter.LineWidth  = 0;
            }

            plt.Axes.SetLimitsX(-1, 1);
            plt.Axes.Bottom.IsVisible = false;
            plt.Axes.Left.Label.Text  = "Response Time (ms)";
            plt.Title("Distribution  —  Full Period");

            double yTop = s.FullWhiskerHigh * 1.25;
            double yBot = Math.Max(0, s.FullWhiskerLow * 0.75);
            plt.Axes.SetLimitsY(yBot, yTop);
            plt.Axes.Left.TickLabelStyle.ForeColor = ColorText;
            wpfPlot.Refresh();
        }

        private void RenderBellCurveForDay(DayBoxStats day, BoxPlotData full, WpfPlot wpfPlot)
        {
            RenderBellCurveInternal(day.Mean, day.StdDev, full.FullMean, full.FullStdDev, wpfPlot,
                "Frequency Distribution  —  " + day.Date.ToString("dd.MM.yyyy"));
        }

        private void RenderBellCurve(BoxPlotData s, WpfPlot wpfPlot)
        {
            RenderBellCurveInternal(s.FullMean, s.FullStdDev, s.FullMean, s.FullStdDev, wpfPlot,
                "Frequency Distribution  —  Full Period");
        }

        private void RenderBellCurveInternal(double mean, double stdDev, double refMean, double refStdDev,
            WpfPlot wpfPlot, string title)
        {
            var plt = wpfPlot.Plot;
            plt.Clear();
            plt.Benchmark.IsVisible = false;
            plt.FigureBackground.Color = ColorBg;
            plt.DataBackground.Color   = ColorBg;
            plt.Axes.Color(ColorAxis);
            plt.Grid.MajorLineColor    = ColorGrid;

            if (stdDev <= 0 || mean <= 0) return;

            double xMin = Math.Max(0, mean - 4 * stdDev);
            double xMax = mean + 4 * stdDev;

            const int points = 300;
            double[] xs = new double[points];
            double[] ys = new double[points];

            for (int i = 0; i < points; i++)
            {
                xs[i] = xMin + (xMax - xMin) * i / (points - 1);
                ys[i] = Gaussian(xs[i], mean, stdDev);
            }

            double yMax = ys.Max();

            double s1lo = Math.Max(xMin, mean - stdDev);
            double s1hi = Math.Min(xMax, mean + stdDev);
            AddShade(plt, xs, ys, s1lo, s1hi, ColorCurve.WithAlpha(50));

            double s2lo = Math.Max(xMin, mean - 2 * stdDev);
            double s2loEnd = Math.Min(xMax, mean - stdDev);
            if (s2lo < s2loEnd) AddShade(plt, xs, ys, s2lo, s2loEnd, ColorCurve.WithAlpha(25));

            double s2hi = Math.Max(xMin, mean + stdDev);
            double s2hiEnd = Math.Min(xMax, mean + 2 * stdDev);
            if (s2hi < s2hiEnd) AddShade(plt, xs, ys, s2hi, s2hiEnd, ColorCurve.WithAlpha(25));

            var curve = plt.Add.Scatter(xs, ys);
            curve.Color      = ColorCurve;
            curve.LineWidth  = 2;
            curve.MarkerSize = 0;

            if (Math.Abs(mean - refMean) > 1 && refStdDev > 0)
            {
                double rxMin = Math.Max(0, refMean - 4 * refStdDev);
                double rxMax = refMean + 4 * refStdDev;
                double[] rxs = new double[points];
                double[] rys = new double[points];
                for (int i = 0; i < points; i++)
                {
                    rxs[i] = rxMin + (rxMax - rxMin) * i / (points - 1);
                    rys[i] = Gaussian(rxs[i], refMean, refStdDev);
                }
                var refCurve = plt.Add.Scatter(rxs, rys);
                refCurve.Color      = ColorWhisk.WithAlpha(80);
                refCurve.LineWidth  = 1;
                refCurve.MarkerSize = 0;
                refCurve.LinePattern = ScottPlot.LinePattern.Dashed;
            }

            plt.Add.VerticalLine(mean,   color: ColorMedian, width: 2,  pattern: ScottPlot.LinePattern.Solid);
            if (mean + stdDev <= xMax)   plt.Add.VerticalLine(mean + stdDev,   color: ColorSigma1, width: 1, pattern: ScottPlot.LinePattern.Dashed);
            if (mean - stdDev >= xMin)   plt.Add.VerticalLine(mean - stdDev,   color: ColorSigma1, width: 1, pattern: ScottPlot.LinePattern.Dashed);
            if (mean + 2*stdDev <= xMax) plt.Add.VerticalLine(mean + 2*stdDev, color: ColorSigma2, width: 1, pattern: ScottPlot.LinePattern.Dashed);
            if (mean - 2*stdDev >= xMin) plt.Add.VerticalLine(mean - 2*stdDev, color: ColorSigma2, width: 1, pattern: ScottPlot.LinePattern.Dashed);

            AddText(plt, mean, yMax * 0.4, "68%");
            if (mean + 1.5*stdDev <= xMax) AddText(plt, mean + 1.5*stdDev, yMax * 0.15, "13.5%");
            if (mean - 1.5*stdDev >= xMin) AddText(plt, mean - 1.5*stdDev, yMax * 0.15, "13.5%");

            plt.Axes.Left.IsVisible    = false;
            plt.Axes.Bottom.Label.Text = "Response Time (ms)";
            plt.Axes.Bottom.TickLabelStyle.ForeColor = ColorText;
            plt.Axes.SetLimitsY(0, yMax * 1.15);
            plt.Axes.SetLimitsX(xMin, xMax);
            plt.Title(title);

            wpfPlot.Refresh();
        }

        private static void AddAnnotation(ScottPlot.Plot plt, double x, double y, string text,
            ScottPlot.Color color, ScottPlot.Alignment align)
        {
            var lbl = plt.Add.Text(text, x + 0.05, y);
            lbl.LabelFontColor = color;
            lbl.LabelFontSize  = 9;
            lbl.LabelAlignment = align;
        }

        private static void AddText(ScottPlot.Plot plt, double x, double y, string text)
        {
            var lbl = plt.Add.Text(text, x, y);
            lbl.LabelFontColor = new ScottPlot.Color(160, 210, 175);
            lbl.LabelFontSize  = 9;
            lbl.LabelAlignment = ScottPlot.Alignment.MiddleCenter;
        }

        private static void AddShade(ScottPlot.Plot plt, double[] xs, double[] ys,
            double x0, double x1, ScottPlot.Color color)
        {
            var rx = new List<double> { x0 };
            var ry = new List<double> { 0 };
            for (int i = 0; i < xs.Length; i++)
                if (xs[i] >= x0 && xs[i] <= x1) { rx.Add(xs[i]); ry.Add(ys[i]); }
            rx.Add(x1); ry.Add(0);
            var poly = plt.Add.Polygon(rx.ToArray(), ry.ToArray());
            poly.FillColor = color;
            poly.LineWidth = 0;
            poly.LineColor = ScottPlot.Color.FromARGB(0);
        }

        private static double Gaussian(double x, double mean, double stdDev)
            => (1.0 / (stdDev * Math.Sqrt(2 * Math.PI))) * Math.Exp(-0.5 * Math.Pow((x - mean) / stdDev, 2));

        private static void UpdateStatsPanel(WrapPanel panel, DayBoxStats day, BoxPlotData full = null)
        {
            if (panel == null) return;
            panel.Children.Clear();

            void Stat(string label, string value)
            {
                var item = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Margin = new Thickness(0, 0, 18, 4)
                };
                item.Children.Add(new TextBlock
                {
                    Text       = label,
                    FontSize   = 12,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Color.FromRgb(160, 210, 175))
                });
                item.Children.Add(new TextBlock
                {
                    Text       = "  " + value,
                    FontSize   = 12,
                    Foreground = new SolidColorBrush(Color.FromRgb(200, 235, 210))
                });
                panel.Children.Add(item);
            }

            if (day != null)
            {
                Stat("Median",   day.Median.ToString("N0") + " ms");
                Stat("Mean",     day.Mean.ToString("N0") + " ms");
                Stat("Q1",       day.Q1.ToString("N0") + " ms");
                Stat("Q3",       day.Q3.ToString("N0") + " ms");
                Stat("σ",        day.StdDev.ToString("N0") + " ms");
                Stat("IQR",      (day.Q3 - day.Q1).ToString("N0") + " ms");
                Stat("Min",      day.Min.ToString("N0") + " ms");
                Stat("Max",      day.Max.ToString("N0") + " ms");
                Stat("n",        day.Count.ToString("N0"));
                Stat("Outliers", day.Outliers.Count.ToString());
            }
            else if (full != null)
            {
                Stat("Median",   full.FullMedian.ToString("N0") + " ms");
                Stat("Mean",     full.FullMean.ToString("N0") + " ms");
                Stat("Q1",       full.FullQ1.ToString("N0") + " ms");
                Stat("Q3",       full.FullQ3.ToString("N0") + " ms");
                Stat("σ",        full.FullStdDev.ToString("N0") + " ms");
                Stat("IQR",      (full.FullQ3 - full.FullQ1).ToString("N0") + " ms");
                Stat("Min",      full.FullMin.ToString("N0") + " ms");
                Stat("Max",      full.FullMax.ToString("N0") + " ms");
                Stat("Outliers", full.FullOutliers.Count.ToString());
            }
        }

        private static void UpdateStatsText(TextBlock tb, DayBoxStats day, BoxPlotData full = null)
        {
            if (tb == null) return;
            if (day != null)
                tb.Text = $"Median {day.Median:N0} ms  ·  Mean {day.Mean:N0} ms  ·  Q1 {day.Q1:N0} ms  ·  Q3 {day.Q3:N0} ms  ·  σ {day.StdDev:N0} ms  ·  IQR {(day.Q3 - day.Q1):N0} ms  ·  Min {day.Min:N0} ms  ·  Max {day.Max:N0} ms  ·  n = {day.Count:N0}  ·  Outliers {day.Outliers.Count}";
            else if (full != null)
                tb.Text = $"Median {full.FullMedian:N0} ms  ·  Mean {full.FullMean:N0} ms  ·  Q1 {full.FullQ1:N0} ms  ·  Q3 {full.FullQ3:N0} ms  ·  σ {full.FullStdDev:N0} ms  ·  IQR {(full.FullQ3 - full.FullQ1):N0} ms  ·  Min {full.FullMin:N0} ms  ·  Max {full.FullMax:N0} ms  ·  Outliers {full.FullOutliers.Count}";
        }

        private static UIElement BuildLegend()
        {
            var outer = new StackPanel
            {
                Background = new SolidColorBrush(Color.FromRgb(8, 14, 10)),
                Margin = new Thickness(0)
            };

            // Riadok 1: farby dní
            var colorRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(16, 4, 16, 2)
            };

            void ColorItem(Color c, string label)
            {
                colorRow.Children.Add(new Border
                {
                    Width = 10, Height = 10, CornerRadius = new CornerRadius(2),
                    Background = new SolidColorBrush(c),
                    Margin = new Thickness(0, 0, 4, 0),
                    VerticalAlignment = VerticalAlignment.Center
                });
                colorRow.Children.Add(new TextBlock
                {
                    Text = label, FontSize = 11,
                    Foreground = new SolidColorBrush(Color.FromRgb(100, 140, 115)),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 14, 0)
                });
            }

            ColorItem(Color.FromRgb(0, 200, 255),   "Active day");
            ColorItem(Color.FromRgb(70, 130, 220),  "Better than avg (< −20%)");
            ColorItem(Color.FromRgb(50, 160, 80),   "Normal");
            ColorItem(Color.FromRgb(220, 70, 70),   "Worse than avg (> +20%)");

            colorRow.Children.Add(new Border
            {
                Width = 20, Height = 2,
                Background = new SolidColorBrush(Color.FromArgb(180, 255, 200, 0)),
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0)
            });
            colorRow.Children.Add(new TextBlock
            {
                Text = "Period median (reference)",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 140, 115)),
                VerticalAlignment = VerticalAlignment.Center
            });

            // Riadok 2: vysvetlenie boxplotu
            var boxExplainRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(16, 2, 16, 4)
            };

            void ExplainItem(string symbol, Color symColor, string text)
            {
                boxExplainRow.Children.Add(new TextBlock
                {
                    Text = symbol, FontSize = 11, FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(symColor),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 3, 0)
                });
                boxExplainRow.Children.Add(new TextBlock
                {
                    Text = text, FontSize = 11,
                    Foreground = new SolidColorBrush(Color.FromRgb(80, 115, 95)),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 14, 0)
                });
            }

            ExplainItem("─┤├─", Color.FromRgb(100, 160, 120), "Whiskers = normal range (Q1−1.5×IQR  to  Q3+1.5×IQR)");
            ExplainItem("█", Color.FromRgb(80, 140, 100), "Box = middle 50% of values (Q1 to Q3)");
            ExplainItem("─", Color.FromRgb(220, 210, 80), "Line in box = Median (Q2, 50th percentile)");
            ExplainItem("•", Color.FromRgb(220, 100, 60), "Dots = outliers (unusually slow responses)");

            outer.Children.Add(colorRow);
            outer.Children.Add(new Border
            {
                Height = 1, Margin = new Thickness(16, 0, 16, 0),
                Background = new SolidColorBrush(Color.FromRgb(22, 40, 28))
            });
            outer.Children.Add(boxExplainRow);

            return new Border
            {
                Child = outer,
                BorderBrush = new SolidColorBrush(Color.FromRgb(20, 40, 26)),
                BorderThickness = new Thickness(0, 1, 0, 1)
            };
        }

        private static Border MakeSwitchBtn(string label, bool active, Action onClick)
        {
            var btn = new Border
            {
                Padding = new Thickness(12, 5, 12, 5), Margin = new Thickness(0, 0, 4, 0),
                CornerRadius = new CornerRadius(4),
                Background   = new SolidColorBrush(active ? Color.FromRgb(22, 80, 40)  : Color.FromRgb(14, 28, 18)),
                BorderBrush  = new SolidColorBrush(active ? Color.FromRgb(63, 185, 80) : Color.FromRgb(30, 60, 38)),
                BorderThickness = new Thickness(1), Cursor = Cursors.Hand,
                Child = new TextBlock
                {
                    Text = label, FontSize = 12,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(active ? Color.FromRgb(63, 185, 80) : Color.FromRgb(90, 130, 100))
                }
            };
            btn.MouseLeftButtonUp += (s, e) => onClick();
            return btn;
        }

        private static Border MakeNavBtn(string label, Action onClick)
        {
            var btn = new Border
            {
                Width = 48,
                Padding = new Thickness(0),
                Margin = new Thickness(0),
                Background   = new SolidColorBrush(Color.FromRgb(14, 35, 20)),
                BorderBrush  = new SolidColorBrush(Color.FromRgb(30, 80, 44)),
                BorderThickness = new Thickness(0, 0, 1, 0),
                Cursor = Cursors.Hand,
                Child = new TextBlock
                {
                    Text = label, FontSize = 18,
                    Foreground = new SolidColorBrush(Color.FromRgb(63, 185, 80)),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment   = VerticalAlignment.Center
                }
            };
            btn.MouseLeftButtonUp += (s, e) => onClick();
            return btn;
        }

        private static void SetSwitchActive(Border btn, bool active)
        {
            if (btn == null) return;
            btn.Background  = new SolidColorBrush(active ? Color.FromRgb(22, 80, 40)  : Color.FromRgb(14, 28, 18));
            btn.BorderBrush = new SolidColorBrush(active ? Color.FromRgb(63, 185, 80) : Color.FromRgb(30, 60, 38));
            if (btn.Child is TextBlock tb)
                tb.Foreground = new SolidColorBrush(active ? Color.FromRgb(63, 185, 80) : Color.FromRgb(90, 130, 100));
        }
    }

    internal static class Extensions
    {
        internal static T Let<T>(this T obj, Action<T> action) { action(obj); return obj; }
    }
}