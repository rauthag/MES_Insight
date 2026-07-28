using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using MESInsight.Charts.Interfaces;
using MESInsight.Core;
using MESInsight.UI;
using ScottPlot;
using ScottPlot.WPF;
using VerticalAlignment = System.Windows.VerticalAlignment;

namespace MESInsight.Charts.Renderers
{
    public class ScottPlotTrendChartRenderer : ChartRenderer
    {
        private readonly DayRecordsPanelBuilder _dayRecordsPanelBuilder;

        private readonly Dictionary<MessageType, (Border panel, ColumnDefinition col, bool open)>
            _dayRecordsPanelByMessageType;

        private readonly Dictionary<MessageType, WpfPlot> _plotByMessageType;
        private readonly Dictionary<MessageType, (Border container, StackPanel panel)> _timelineContainerByMessageType;
        private readonly ChartFactory _chartFactory;
        private readonly Dictionary<DateTime, List<ResponseRecord>> _recordsGroupedByDay;
        private readonly List<ResponseRecord> _filteredRecords;
        private readonly Action<MessageType> _onShowAllRecordsRequested;
        private readonly Action<DateTime, List<ResponseRecord>, MessageType> _onDaySelected;


        private static readonly ScottPlot.Color ColorAvg = new ScottPlot.Color(79, 195, 247);
        private static readonly ScottPlot.Color ColorP95 = new ScottPlot.Color(165, 214, 167);
        private static readonly ScottPlot.Color ColorP95Point = new ScottPlot.Color(56, 142, 60);
        private static readonly ScottPlot.Color ColorRolling = new ScottPlot.Color(255, 112, 67);
        private static readonly ScottPlot.Color ColorSla = new ScottPlot.Color(231, 76, 60);
        private static readonly ScottPlot.Color ColorViolation = new ScottPlot.Color(231, 76, 60, 180);
        private static readonly ScottPlot.Color ColorBackground = new ScottPlot.Color(13, 17, 23);
        private static readonly ScottPlot.Color ColorGrid = new ScottPlot.Color(255, 255, 255, 25);
        private static readonly ScottPlot.Color ColorAxis = new ScottPlot.Color(139, 148, 158);
        private static readonly ScottPlot.Color ColorAvgPoint = new ScottPlot.Color(2, 136, 209);
        private static readonly ScottPlot.Color ColorLegendBg = new ScottPlot.Color(22, 27, 34, 200);
        private static readonly ScottPlot.Color ColorLegendBorder = new ScottPlot.Color(139, 148, 158, 60);
        private static readonly ScottPlot.Color ColorGapFill = new ScottPlot.Color(200, 200, 200, 12);
        private static readonly ScottPlot.Color ColorGapHatch = new ScottPlot.Color(150, 160, 170, 75);

        private static readonly ScottPlot.Color[] MonthColors =
        {
            new ScottPlot.Color(52, 152, 219), new ScottPlot.Color(46, 204, 113),
            new ScottPlot.Color(155, 89, 182), new ScottPlot.Color(241, 196, 15),
            new ScottPlot.Color(230, 126, 34), new ScottPlot.Color(231, 76, 60)
        };

        private static ScottPlot.Color MonthColor(int month) => MonthColors[(month - 1) % MonthColors.Length];

        public ScottPlotTrendChartRenderer(
            DayRecordsPanelBuilder dayRecordsPanelBuilder,
            Dictionary<MessageType, (Border panel, ColumnDefinition col, bool open)> dayRecordsPanelByMessageType,
            Dictionary<MessageType, WpfPlot> plotByMessageType,
            Dictionary<MessageType, (Border container, StackPanel panel)> timelineContainerByMessageType,
            ChartFactory chartFactory,
            Dictionary<DateTime, List<ResponseRecord>> recordsGroupedByDay,
            List<ResponseRecord> filteredRecords,
            Action<MessageType> onShowAllRecordsRequested,
            Action<DateTime, List<ResponseRecord>, MessageType> onDaySelected = null)
        {
            _dayRecordsPanelBuilder = dayRecordsPanelBuilder;
            _dayRecordsPanelByMessageType = dayRecordsPanelByMessageType;
            _plotByMessageType = plotByMessageType;
            _timelineContainerByMessageType = timelineContainerByMessageType;
            _chartFactory = chartFactory;
            _recordsGroupedByDay = recordsGroupedByDay;
            _filteredRecords = filteredRecords;
            _onShowAllRecordsRequested = onShowAllRecordsRequested;
            _onDaySelected = onDaySelected;
        }

        public override ChartType GetChartType() => ChartType.Trend;
        public override int GetMinimumHeight(RenderContext context) => (int)(context.AvailableHeightPixels * 0.60);

        public override UIElement Render(ChartData data, RenderContext context)
        {
            if (data?.ScottPlotTrend == null) return null;

            var trend = data.ScottPlotTrend;
            var messageType = context.MessageType;
            int height = (int)(context.AvailableHeightPixels * 0.60);

            var wpfPlot = BuildPlot(trend, height, messageType);
            _plotByMessageType[messageType] = wpfPlot;

            var dayRecordsPanel = _dayRecordsPanelBuilder.BuildEmptyDayRecordsPanel();
            var reservedColumn = new ColumnDefinition { Width = new GridLength(0) };

            _dayRecordsPanelByMessageType[messageType] = (dayRecordsPanel, reservedColumn, false);
            _dayRecordsPanelBuilder.WireClosePanelButton(dayRecordsPanel, () =>
            {
                _dayRecordsPanelByMessageType[messageType] = (dayRecordsPanel, reservedColumn, false);
                _dayRecordsPanelBuilder.AnimateSlideClose(dayRecordsPanel, reservedColumn);
            });

            var chartGrid = new Grid();
            chartGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            chartGrid.ColumnDefinitions.Add(reservedColumn);

            var plotHost = new Grid();
            var overlay = new Canvas { IsHitTestVisible = false, ClipToBounds = true };
            plotHost.Children.Add(wpfPlot);
            plotHost.Children.Add(overlay);

            Grid.SetColumn(plotHost, 0);
            Grid.SetColumn(dayRecordsPanel, 1);
            chartGrid.Children.Add(plotHost);
            chartGrid.Children.Add(dayRecordsPanel);

            var resetCallback = WireAllMouseHandlers(wpfPlot, overlay, trend, reservedColumn, dayRecordsPanel, messageType);
            var titleBar = BuildTitleBar(trend.Name, messageType, wpfPlot, resetCallback);

            var outerGrid = new Grid();
            outerGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(34) });
            outerGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(height - 34) });
            Grid.SetRow(titleBar, 0);
            Grid.SetRow(chartGrid, 1);
            outerGrid.Children.Add(titleBar);
            outerGrid.Children.Add(chartGrid);

            var timelineSection = BuildTimelineSection(messageType);
            var wrapper = new StackPanel();
            wrapper.Children.Add(WrapInSectionBorder(outerGrid, isHistogram: false));
            wrapper.Children.Add(timelineSection);
            return wrapper;
        }

        private WpfPlot BuildPlot(ScottPlotTrendData trend, int height, MessageType messageType)
        {
            var wpfPlot = new WpfPlot
            {
                Height = height,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch
            };
            wpfPlot.MouseEnter += (s, e) => wpfPlot.Focus();

            var plt = wpfPlot.Plot;
            plt.FigureBackground.Color = ColorBackground;
            plt.DataBackground.Color = ColorBackground;
            plt.Axes.Color(ColorAxis);
            plt.Grid.MajorLineColor = ColorGrid;

            AddSeries(plt, trend);
            ConfigureAxes(plt, trend);

            wpfPlot.Loaded += (s, e) =>
            {
                var allY = trend.AvgY.Concat(trend.P95Y).Where(v => v > 0).OrderBy(v => v).ToArray();
                if (allY.Length == 0) return;

                double yMin = Math.Max(0, Math.Floor((allY[0] * 0.92) / 5) * 5);

                double dataMax = allY[allY.Length - 1];
                double yMax = Math.Ceiling(Math.Max(dataMax, trend.SlaThreshold) * 1.03 / 50) * 50;

                double xMin = 0, xMax = 0;
                if (trend.AvgX?.Length >= 2)
                {
                    double xPad = (trend.AvgX.Max() - trend.AvgX.Min()) * 0.01;
                    xMin = trend.AvgX.Min() - xPad;
                    xMax = trend.AvgX.Max() + xPad;
                    wpfPlot.Plot.Axes.SetLimitsX(left: xMin, right: xMax);
                }

                wpfPlot.Plot.Axes.SetLimitsY(bottom: yMin, top: yMax);

                wpfPlot.Tag = new double[] { xMin, xMax, yMin, yMax };
                wpfPlot.Refresh();
                UpdateMarkerSizesForZoom(wpfPlot, trend);
            };

            wpfPlot.Refresh();
            return wpfPlot;
        }

        private static void AddSeries(ScottPlot.Plot plt, ScottPlotTrendData trend)
        {
            if (trend.AvgX?.Length >= 2)
            {
                AddLine(plt, trend.AvgX, trend.AvgY, ColorAvg, 2.5f, "AVG",
                    ColorAvgPoint, ScottPlot.MarkerShape.FilledCircle, CalcMarkerSize(trend.AvgX.Length));
                AddLine(plt, trend.P95X, trend.P95Y, ColorP95, 2f, "P95",
                    ColorP95Point, ScottPlot.MarkerShape.FilledDiamond, CalcMarkerSize(trend.P95X?.Length ?? 0));
                AddLine(plt, trend.RollingAvgX, trend.RollingAvgY, ColorRolling, 2.5f, "7-Day AVG",
                    null, null, 0, ScottPlot.LinePattern.Dashed);

                var sla = plt.Add.HorizontalLine(trend.SlaThreshold);
                sla.Color = ColorSla;
                sla.LineWidth = 2;
                sla.LinePattern = ScottPlot.LinePattern.Dashed;
                sla.LegendText = "Target: " + trend.SlaThreshold + "ms (" + trend.SlaCompliancePct.ToString("F0") +
                                 "% OK)";
            }

            if (trend.ViolationX?.Length > 0)
            {

                var vOuter = plt.Add.ScatterPoints(trend.ViolationX, trend.ViolationY);
                vOuter.Color = new ScottPlot.Color(231, 76, 60, 60);
                vOuter.MarkerSize = 26;
                vOuter.MarkerShape = ScottPlot.MarkerShape.OpenCircle;
                vOuter.LegendText = "";

                var v = plt.Add.ScatterPoints(trend.ViolationX, trend.ViolationY);
                v.Color = ColorViolation;
                v.MarkerSize = 14;
                v.MarkerShape = ScottPlot.MarkerShape.FilledCircle;
                v.LegendText = "SLA Violations";
            }

            for (int gi = 0; gi < trend.Gaps.Count; gi++)
            {
                var gap = trend.Gaps[gi];

                var baseFill = plt.Add.Rectangle(gap.From, gap.To, -1e6, 1e6);
                baseFill.FillColor = ColorGapFill;
                baseFill.LineWidth = 0;

                var transparent = new ScottPlot.Color(0, 0, 0, 0);
                var up = plt.Add.Rectangle(gap.From, gap.To, -1e6, 1e6);
                up.FillColor = transparent;
                up.FillHatch = new ScottPlot.Hatches.Striped(ScottPlot.Hatches.StripeDirection.DiagonalUp);
                up.FillHatchColor = ColorGapHatch;
                up.LineWidth = 0;

                var down = plt.Add.Rectangle(gap.From, gap.To, -1e6, 1e6);
                down.FillColor = transparent;
                down.FillHatch = new ScottPlot.Hatches.Striped(ScottPlot.Hatches.StripeDirection.DiagonalDown);
                down.FillHatchColor = ColorGapHatch;
                down.LineWidth = 0;

                if (trend.GapLabels != null && gi < trend.GapLabels.Count)
                {
                    var cap = plt.Add.VerticalLine((gap.From + gap.To) / 2);
                    cap.LineWidth = 0;
                    cap.Color = new ScottPlot.Color(0, 0, 0, 0);
                    cap.LabelText = trend.GapLabels[gi];
                    cap.LabelFontColor = ColorAxis;
                    cap.LabelFontSize = 11;
                    cap.LabelOppositeAxis = false;
                    cap.ExcludeFromLegend = true;
                }
            }

            AddMonthMarkers(plt, trend);
        }

        private static void AddMonthMarkers(ScottPlot.Plot plt, ScottPlotTrendData trend)
        {
            if (trend.XToDate == null || trend.XToDate.Count == 0) return;

            var ordered = trend.XToDate.OrderBy(kv => kv.Key)
                .Select(kv => new { X = kv.Key, Date = kv.Value })
                .ToList();

            var segments = new List<(double startX, double endX, DateTime month)>();
            int curMonth = -1, curYear = -1;
            double segStart = 0, lastX = 0;
            DateTime segDate = default;
            foreach (var p in ordered)
            {
                if (p.Date.Month != curMonth || p.Date.Year != curYear)
                {
                    if (curMonth != -1) segments.Add((segStart, lastX, segDate));
                    segStart = p.X;
                    curMonth = p.Date.Month;
                    curYear = p.Date.Year;
                    segDate = p.Date;
                }

                lastX = p.X;
            }

            if (curMonth != -1) segments.Add((segStart, lastX, segDate));

            var enUS = new System.Globalization.CultureInfo("en-US");
            for (int i = 0; i < segments.Count; i++)
            {
                var seg = segments[i];
                var mc = MonthColor(seg.month.Month);

                if (i > 0)
                {
                    var sep = plt.Add.VerticalLine(seg.startX);
                    sep.Color = new ScottPlot.Color(mc.R, mc.G, mc.B, 120);
                    sep.LineWidth = 2;
                    sep.LinePattern = ScottPlot.LinePattern.Dashed;
                    sep.ExcludeFromLegend = true;
                }

                double centerX = (seg.startX + seg.endX) / 2;
                var lbl = plt.Add.VerticalLine(centerX);
                lbl.LineWidth = 0;
                lbl.Color = new ScottPlot.Color(0, 0, 0, 0);
                lbl.LabelText = seg.month.ToString("MMM yyyy", enUS);
                lbl.LabelFontColor = mc;
                lbl.LabelFontSize = 13;
                lbl.LabelBold = true;
                lbl.LabelOppositeAxis = false;
                lbl.ExcludeFromLegend = true;
            }
        }

        private static void AddLine(
            ScottPlot.Plot plt, double[] x, double[] y,
            ScottPlot.Color color, float width, string label,
            ScottPlot.Color? pointColor, ScottPlot.MarkerShape? shape, float pointSize,
            ScottPlot.LinePattern pattern = ScottPlot.LinePattern.Solid)
        {
            if (x == null || x.Length < 2) return;
            var line = plt.Add.ScatterLine(x, y);
            line.Color = color;
            line.LineWidth = width;
            line.LegendText = label;
            line.LinePattern = pattern;
            if (pointColor.HasValue && shape.HasValue && pointSize > 0)
            {
                var pts = plt.Add.ScatterPoints(x, y);
                pts.Color = pointColor.Value;
                pts.MarkerSize = pointSize;
                pts.MarkerShape = shape.Value;
            }
        }

        private static void ConfigureAxes(ScottPlot.Plot plt, ScottPlotTrendData trend)
        {
            plt.Axes.Bottom.Label.Text = "Date";
            plt.Axes.Left.Label.Text = "Response Time (ms)";
            plt.Axes.Bottom.Label.FontSize = 13;
            plt.Axes.Left.Label.FontSize = 13;

            plt.Axes.Bottom.MinimumSize = 64;
            plt.Axes.Bottom.TickGenerator = new ScottPlot.TickGenerators.NumericAutomatic
                { LabelFormatter = x => FormatDateTick(x, trend, plt) };
            plt.Legend.IsVisible = true;
            plt.Legend.Alignment = ScottPlot.Alignment.LowerRight;
            plt.Legend.BackgroundColor = ColorLegendBg;
            plt.Legend.FontColor = ColorAxis;
            plt.Legend.OutlineColor = ColorLegendBorder;
        }

        private Action WireAllMouseHandlers(
            WpfPlot wpfPlot, Canvas overlay, ScottPlotTrendData trend,
            ColumnDefinition reservedColumn, Border dayRecordsPanel,
            MessageType messageType)
        {

            var hoverBorder = new Border
            {
                Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(245, 18, 22, 30)),
                BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(56, 139, 253)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10, 8, 10, 8),
                IsHitTestVisible = false,
                Visibility = Visibility.Collapsed
            };
            overlay.Children.Add(hoverBorder);

            var glow = new System.Windows.Shapes.Ellipse
            {
                Width = 20,
                Height = 20,
                Fill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(60, 79, 195, 247)),
                Stroke = new SolidColorBrush(System.Windows.Media.Color.FromArgb(220, 79, 195, 247)),
                StrokeThickness = 2,
                IsHitTestVisible = false,
                Visibility = Visibility.Collapsed,
                RenderTransformOrigin = new Point(0.5, 0.5)
            };
            var glowScale = new ScaleTransform(1, 1);
            glow.RenderTransform = glowScale;
            overlay.Children.Add(glow);

            var ease = new System.Windows.Media.Animation.SineEase
                { EasingMode = System.Windows.Media.Animation.EasingMode.EaseInOut };
            var scalePulse = new System.Windows.Media.Animation.DoubleAnimation
            {
                From = 0.6, To = 1.4,
                Duration = TimeSpan.FromMilliseconds(750),
                AutoReverse = true,
                RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever,
                EasingFunction = ease
            };
            var opacityPulse = new System.Windows.Media.Animation.DoubleAnimation
            {
                From = 1.0, To = 0.35,
                Duration = TimeSpan.FromMilliseconds(750),
                AutoReverse = true,
                RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever,
                EasingFunction = ease
            };
            glowScale.BeginAnimation(ScaleTransform.ScaleXProperty, scalePulse);
            glowScale.BeginAnimation(ScaleTransform.ScaleYProperty, scalePulse);
            glow.BeginAnimation(UIElement.OpacityProperty, opacityPulse);

            var clickGlow = new System.Windows.Shapes.Ellipse
            {
                Width = 24, Height = 24,
                Fill   = new SolidColorBrush(System.Windows.Media.Color.FromArgb(80,  63, 185, 80)),
                Stroke = new SolidColorBrush(System.Windows.Media.Color.FromArgb(230, 63, 185, 80)),
                StrokeThickness = 2.5,
                IsHitTestVisible = false,
                Visibility = Visibility.Collapsed,
                RenderTransformOrigin = new Point(0.5, 0.5)
            };
            var clickGlowScale = new ScaleTransform(1, 1);
            clickGlow.RenderTransform = clickGlowScale;
            overlay.Children.Add(clickGlow);
            var clickPulseX = new System.Windows.Media.Animation.DoubleAnimation
            { From = 0.85, To = 1.15, Duration = TimeSpan.FromMilliseconds(900),
              AutoReverse = true, RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever,
              EasingFunction = ease };
            var clickPulseY = new System.Windows.Media.Animation.DoubleAnimation
            { From = 0.85, To = 1.15, Duration = TimeSpan.FromMilliseconds(900),
              AutoReverse = true, RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever,
              EasingFunction = ease };
            clickGlowScale.BeginAnimation(ScaleTransform.ScaleXProperty, clickPulseX);
            clickGlowScale.BeginAnimation(ScaleTransform.ScaleYProperty, clickPulseY);
            double clickedX = double.NaN;
            double clickedY = double.NaN;

            void UpdateClickGlowPosition()
            {
                if (double.IsNaN(clickedX) || clickGlow.Visibility != Visibility.Visible) return;
                try
                {
                    var px = wpfPlot.Plot.GetPixel(new ScottPlot.Coordinates(clickedX, clickedY));
                    Canvas.SetLeft(clickGlow, px.X - clickGlow.Width / 2);
                    Canvas.SetTop(clickGlow,  px.Y - clickGlow.Height / 2);
                } catch { }
            }

            void HideHover()
            {
                hoverBorder.Visibility = Visibility.Collapsed;
                glow.Visibility = Visibility.Collapsed;
            }

            string lastHoverKey = null;

            Point? panStart = null;
            double panLeft = 0, panRight = 0;
            bool isDragging = false;
            DateTime lastClickTime = DateTime.MinValue;

            wpfPlot.PreviewMouseWheel += (s, e) =>
            {
                e.Handled = true;
                var limits = wpfPlot.Tag as double[];
                if (limits == null) return;

                double factor = e.Delta > 0 ? 1.25 : 0.8;
                var pos = e.GetPosition(wpfPlot);
                var coords = wpfPlot.Plot.GetCoordinates((float)pos.X, (float)pos.Y);
                var cur = wpfPlot.Plot.Axes.GetLimits();
                double xRange = cur.Right - cur.Left;
                double newRange = Math.Min(xRange / factor, limits[1] - limits[0]);
                double ratio = xRange > 0 ? (coords.X - cur.Left) / xRange : 0.5;
                double newLeft = Math.Max(limits[0], coords.X - newRange * ratio);
                double newRight = Math.Min(limits[1], newLeft + newRange);
                if (newRight - newLeft < newRange) newLeft = newRight - newRange;

                wpfPlot.Plot.Axes.SetLimitsX(left: newLeft, right: newRight);
                wpfPlot.Refresh();
                UpdateMarkerSizesForZoom(wpfPlot, trend);
                UpdateClickGlowPosition();
            };

            wpfPlot.MouseLeftButtonDown += (s, e) =>
            {
                panStart = e.GetPosition(wpfPlot);
                var cur = wpfPlot.Plot.Axes.GetLimits();
                panLeft = cur.Left;
                panRight = cur.Right;
                isDragging = false;
            };

            wpfPlot.MouseMove += (s, e) =>
            {
                var pos = e.GetPosition(wpfPlot);

                if (panStart.HasValue && e.LeftButton == MouseButtonState.Released)
                {
                    if (wpfPlot.IsMouseCaptured) wpfPlot.ReleaseMouseCapture();
                    panStart = null;
                    isDragging = false;
                }

                if (panStart.HasValue)
                {
                    double dx = pos.X - panStart.Value.X;
                    if (Math.Abs(dx) > 9)
                    {
                        isDragging = true;
                        if (!wpfPlot.IsMouseCaptured) wpfPlot.CaptureMouse();
                    }

                    if (isDragging)
                    {
                        var limits = wpfPlot.Tag as double[];
                        if (limits != null)
                        {
                            double xRange = panRight - panLeft;
                            double dataDx = -dx / wpfPlot.ActualWidth * xRange;
                            double newLeft = panLeft + dataDx;
                            double newRight = panRight + dataDx;
                            if (newLeft < limits[0])
                            {
                                newLeft = limits[0];
                                newRight = limits[0] + xRange;
                            }

                            if (newRight > limits[1])
                            {
                                newRight = limits[1];
                                newLeft = limits[1] - xRange;
                            }

                            wpfPlot.Plot.Axes.SetLimitsX(left: newLeft, right: newRight);
                            wpfPlot.Refresh();
                            UpdateMarkerSizesForZoom(wpfPlot, trend);
                            UpdateClickGlowPosition();
                        }

                        HideHover();
                        lastHoverKey = null;
                    }
                }

                if (!isDragging && trend.AvgX?.Length >= 2)
                {
                    var coords = wpfPlot.Plot.GetCoordinates((float)pos.X, (float)pos.Y);
                    var cur = wpfPlot.Plot.Axes.GetLimits();
                    double vis = cur.Right - cur.Left;

                    int idx = 0;
                    double nearest = trend.AvgX[0];
                    double minDiff = Math.Abs(trend.AvgX[0] - coords.X);
                    for (int k = 0; k < trend.AvgX.Length; k++)
                    {
                        double d = Math.Abs(trend.AvgX[k] - coords.X);
                        if (d < minDiff)
                        {
                            minDiff = d;
                            nearest = trend.AvgX[k];
                            idx = k;
                        }
                    }

                    bool nearColumn = minDiff < vis * 0.05 && trend.XToDate.ContainsKey(nearest);
                    if (nearColumn)
                    {

                        var date = trend.XToDate[nearest];
                        double yVal = idx < trend.AvgY.Length ? trend.AvgY[idx] : coords.Y;
                        var px = wpfPlot.Plot.GetPixel(new ScottPlot.Coordinates(nearest, yVal));

                        SetGlowColor(glow, System.Windows.Media.Color.FromRgb(79, 195, 247));
                        Canvas.SetLeft(glow, px.X - glow.Width / 2);
                        Canvas.SetTop(glow, px.Y - glow.Height / 2);
                        glow.Visibility = Visibility.Visible;

                        string key = "D:" + nearest;
                        if (key != lastHoverKey)
                        {
                            lastHoverKey = key;
                            hoverBorder.BorderBrush =
                                new SolidColorBrush(System.Windows.Media.Color.FromRgb(56, 139, 253));
                            var stats = trend.DailyStats != null && idx < trend.DailyStats.Count
                                ? trend.DailyStats[idx]
                                : null;

                            var panel = new StackPanel { Margin = new Thickness(2) };
                            panel.Children.Add(new TextBlock
                            {
                                Text = date.ToString("dd.MM.yyyy"),
                                FontSize = 11,
                                FontWeight = FontWeights.Bold,
                                Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(63, 185, 80))
                            });
                            if (stats != null)
                            {
                                panel.Children.Add(HoverRow("AVG", stats.Avg.ToString("F0") + " ms",
                                    System.Windows.Media.Color.FromRgb(79, 195, 247)));
                                panel.Children.Add(HoverRow("P95", stats.P95 + " ms",
                                    System.Windows.Media.Color.FromRgb(165, 214, 167)));
                                panel.Children.Add(HoverRow("Records", stats.Count.ToString("N0"),
                                    System.Windows.Media.Color.FromRgb(139, 148, 158)));
                            }

                            hoverBorder.Child = panel;
                        }

                        PositionHoverBorder(hoverBorder, overlay, px.X, px.Y);
                        hoverBorder.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        HideHover();
                        lastHoverKey = null;
                    }
                }
            };

            wpfPlot.MouseLeftButtonUp += (s, e) =>
            {
                wpfPlot.ReleaseMouseCapture();
                HideHover();
                lastHoverKey = null;

                bool wasDragging = isDragging;
                isDragging = false;
                panStart = null;

                if (!wasDragging && trend.AvgX?.Length >= 2)
                {
                    var pos = e.GetPosition(wpfPlot);
                    var coords = wpfPlot.Plot.GetCoordinates((float)pos.X, (float)pos.Y);
                    var cur = wpfPlot.Plot.Axes.GetLimits();
                    double vis = cur.Right - cur.Left;

                    double nearest = trend.AvgX[0];
                    double minDiff = Math.Abs(trend.AvgX[0] - coords.X);
                    foreach (var x in trend.AvgX)
                    {
                        double d = Math.Abs(x - coords.X);
                        if (d < minDiff)
                        {
                            minDiff = d;
                            nearest = x;
                        }
                    }

                    if (minDiff < vis * 0.05 && trend.XToDate.TryGetValue(nearest, out DateTime realDate))
                    {
                        bool isDouble = (DateTime.UtcNow - lastClickTime).TotalMilliseconds < 400;
                        lastClickTime = DateTime.UtcNow;
                        
                        List<ResponseRecord> recordsForDay = null;
                        if (!isDouble)
                        {
                            UpdateTimelineForDay(messageType, realDate);

                            var limits = wpfPlot.Tag as double[];
                            if (limits != null)
                            {
                                double halfWindow = 14.0;
                                double zLeft  = Math.Max(limits[0], nearest - halfWindow);
                                double zRight = Math.Min(limits[1], nearest + halfWindow);
                                wpfPlot.Plot.Axes.SetLimitsX(left: zLeft, right: zRight);
                                UpdateMarkerSizesForZoom(wpfPlot, trend);
                                wpfPlot.Refresh();

                                clickedX = nearest;
                                int cidx = Array.IndexOf(trend.AvgX, nearest);
                                clickedY = (cidx >= 0 && cidx < trend.AvgY.Length) ? trend.AvgY[cidx] : 0;
                                UpdateClickGlowPosition();
                                clickGlow.Visibility = Visibility.Visible;
                            }
                            
                            if (_recordsGroupedByDay.TryGetValue(realDate.Date, out var allForDay))
                            {
                                recordsForDay = messageType == MessageType.ALL
                                    ? new List<ResponseRecord>(allForDay)
                                    : allForDay.Where(r => r.Type == messageType).ToList();
                            }
                            else
                            {
                                recordsForDay = _filteredRecords
                                    .Where(r => r.TimestampParsed.Date == realDate.Date)
                                    .ToList();
                            }

                            _onDaySelected?.Invoke(realDate, recordsForDay, messageType);

                            if (_dayRecordsPanelByMessageType.ContainsKey(messageType))
                            {
                                var state    = _dayRecordsPanelByMessageType[messageType];
                                bool showTyp = messageType == MessageType.ALL;
                                _dayRecordsPanelBuilder.ShowLoadingSpinner(state.panel, realDate,
                                    recordsForDay.Count, false);

                                if (!state.open)
                                {
                                    _dayRecordsPanelByMessageType[messageType] =
                                        (state.panel, state.col, true);
                                    _dayRecordsPanelBuilder.AnimateSlideOpen(state.panel, state.col);

                                    var capturedDate    = realDate;
                                    var capturedRecords = recordsForDay;
                                    var capturedPanel   = state.panel;
                                    wpfPlot.Dispatcher.BeginInvoke(new Action(() =>
                                    {
                                        _dayRecordsPanelBuilder.PopulateWithDayRecords(capturedPanel, capturedDate, capturedRecords, false, showTyp);
                                    }), System.Windows.Threading.DispatcherPriority.Background,
                                    null);
                                }
                                else
                                {
                                    _dayRecordsPanelBuilder.PopulateWithDayRecords(state.panel, realDate, recordsForDay, false, showTyp);
                                }
                            }
                        }
                    }
                }
            };

            wpfPlot.MouseLeave += (s, e) =>
            {
                HideHover();
                lastHoverKey = null;
                panStart = null;
                isDragging = false;
            };

            wpfPlot.LostMouseCapture += (s, e) =>
            {
                panStart = null;
                isDragging = false;
            };

            Action resetCallback = () =>
            {
                var limits = wpfPlot?.Tag as double[];
                if (limits == null || wpfPlot == null) return;
                wpfPlot.Plot.Axes.SetLimitsX(left: limits[0], right: limits[1]);
                wpfPlot.Plot.Axes.SetLimitsY(bottom: limits[2], top: limits[3]);
                wpfPlot.Refresh();
                clickGlow.Visibility = Visibility.Collapsed;
                clickedX = double.NaN;
            };
            return resetCallback;
        }

        private static (string name, string desc, System.Windows.Media.Color color, double yValue)? DetectLineHover(
            WpfPlot wpfPlot, ScottPlotTrendData trend, double nearestX, int idx, double cursorPxY)
        {
            const double thresholdPx = 11;

            var candidates = new List<(string name, string desc, System.Windows.Media.Color color, double y)>();
            if (idx >= 0 && idx < trend.AvgY.Length)
                candidates.Add(("AVG", "Daily average response time",
                    System.Windows.Media.Color.FromRgb(79, 195, 247), trend.AvgY[idx]));
            if (trend.P95Y != null && idx < trend.P95Y.Length)
                candidates.Add(("P95", "95th percentile — 95% of responses are faster",
                    System.Windows.Media.Color.FromRgb(165, 214, 167), trend.P95Y[idx]));
            if (trend.RollingAvgX != null && trend.RollingAvgY != null)
            {
                int ri = Array.IndexOf(trend.RollingAvgX, nearestX);
                if (ri >= 0 && ri < trend.RollingAvgY.Length)
                    candidates.Add(("7-Day AVG", "7-day rolling average trend",
                        System.Windows.Media.Color.FromRgb(255, 112, 67), trend.RollingAvgY[ri]));
            }

            candidates.Add(("Target", "SLA target threshold (" + trend.SlaThreshold + " ms)",
                System.Windows.Media.Color.FromRgb(231, 76, 60), trend.SlaThreshold));

            double best = double.MaxValue;
            (string name, string desc, System.Windows.Media.Color color, double yValue)? hit = null;
            foreach (var c in candidates)
            {
                double py = wpfPlot.Plot.GetPixel(new ScottPlot.Coordinates(nearestX, c.y)).Y;
                double dist = Math.Abs(py - cursorPxY);
                if (dist < thresholdPx && dist < best)
                {
                    best = dist;
                    hit = (c.name, c.desc, c.color, c.y);
                }
            }

            return hit;
        }

        private static void SetGlowColor(System.Windows.Shapes.Ellipse glow, System.Windows.Media.Color c)
        {
            glow.Fill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(60, c.R, c.G, c.B));
            glow.Stroke = new SolidColorBrush(System.Windows.Media.Color.FromArgb(220, c.R, c.G, c.B));
        }

        private static void PositionHoverBorder(Border hoverBorder, Canvas overlay, double pxX, double pxY)
        {
            hoverBorder.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double bw = hoverBorder.DesiredSize.Width;
            double bh = hoverBorder.DesiredSize.Height;
            double left = pxX - bw / 2;
            double maxLeft = Math.Max(0, overlay.ActualWidth - bw);
            if (left < 0) left = 0;
            if (left > maxLeft) left = maxLeft;
            double top = pxY - bh - 14;
            if (top < 0) top = pxY + 16;
            Canvas.SetLeft(hoverBorder, left);
            Canvas.SetTop(hoverBorder, top);
        }

        private static StackPanel HoverRow(string label, string value, System.Windows.Media.Color color)
        {
            var row = new StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                Margin = new Thickness(0, 2, 0, 0)
            };
            row.Children.Add(new TextBlock
            {
                Text = label + ":  ", FontSize = 10,
                Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(139, 148, 158))
            });
            row.Children.Add(new TextBlock
            {
                Text = value, FontSize = 10, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(color)
            });
            return row;
        }
        
        public void UpdateTimelineForDay(MessageType messageType, DateTime selectedDay)
        {
            if (!_timelineContainerByMessageType.ContainsKey(messageType)) return;
            var (container, panel) = _timelineContainerByMessageType[messageType];
            panel.Children.Clear();

            if (!_recordsGroupedByDay.TryGetValue(selectedDay.Date, out var allRecordsForDay) ||
                allRecordsForDay.Count == 0)
            {
                panel.Children.Add(new TextBlock
                {
                    Text = "No records for " + selectedDay.ToString("dd.MM.yyyy"),
                    FontSize = 11,
                    Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(100, 110, 120)),
                    Margin = new Thickness(4, 4, 0, 4)
                });
                return;
            }

            var timelineData = _chartFactory.Build(ChartType.Timeline, allRecordsForDay, MessageType.ALL);
            if (timelineData?.TimelineEvents == null) return;

            var renderer = new TimelineChartRenderer();
            var element = renderer.Render(timelineData, new RenderContext { MessageType = messageType });
            if (element != null)
                panel.Children.Add(element);
        }

        public void InitializeTimelineWithFirstAvailableDay(MessageType messageType)
        {
            if (_recordsGroupedByDay.Count == 0) return;
            if (!_timelineContainerByMessageType.ContainsKey(messageType)) return;
            var bestDay = _recordsGroupedByDay.OrderByDescending(kv => kv.Value.Count).First().Key;
            UpdateTimelineForDay(messageType, bestDay);

            var recordsForDay = messageType == MessageType.ALL
                ? new List<ResponseRecord>(_recordsGroupedByDay[bestDay])
                : _recordsGroupedByDay[bestDay].Where(r => r.Type == messageType).ToList();
            _onDaySelected?.Invoke(bestDay, recordsForDay, messageType);
        }

        private Border BuildTimelineSection(MessageType messageType)
        {
            var contentPanel = new StackPanel();
            var sectionBorder = new Border
            {
                Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(13, 17, 23)),
                BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(22, 60, 36)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Margin = new Thickness(0, 4, 0, 0),
                Padding = new Thickness(8, 6, 8, 6),
                Child = contentPanel
            };
            _timelineContainerByMessageType[messageType] = (sectionBorder, contentPanel);
            return sectionBorder;
        }

        private Panel BuildTitleBar(string title, MessageType messageType, WpfPlot wpfPlot = null, Action onReset = null)
        {
            var bar = new DockPanel
            {
                Margin = new Thickness(10, 4, 10, 4),
                VerticalAlignment = VerticalAlignment.Center,
                LastChildFill = false,
                Background = new SolidColorBrush(System.Windows.Media.Colors.Transparent)
            };

            var showBtn = MakeTitleBtn("📋 Show Records",
                System.Windows.Media.Color.FromRgb(35, 134, 54),
                System.Windows.Media.Color.FromRgb(46, 160, 67),
                System.Windows.Media.Colors.White);
            showBtn.Click += (s, e) => _onShowAllRecordsRequested?.Invoke(messageType);
            DockPanel.SetDock(showBtn, Dock.Right);
            bar.Children.Add(showBtn);

            var resetBtn = MakeTitleBtn("↺ Reset",
                System.Windows.Media.Color.FromRgb(33, 38, 45),
                System.Windows.Media.Color.FromRgb(48, 54, 61),
                System.Windows.Media.Color.FromRgb(139, 148, 158));
            resetBtn.Margin = new Thickness(0, 0, 6, 0);
            resetBtn.Click += (s, e) => onReset?.Invoke();
            DockPanel.SetDock(resetBtn, Dock.Right);
            bar.Children.Add(resetBtn);

            foreach (var el in new UIElement[]
                     {
                         new TextBlock
                         {
                             Text = title, FontSize = 13, FontWeight = FontWeights.SemiBold,
                             Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(63, 185, 80)),
                             VerticalAlignment = VerticalAlignment.Center
                         },
                         new TextBlock
                         {
                             Text = "  (scroll = zoom  ·  drag = pan)",
                             FontSize = 11, FontStyle = FontStyles.Italic,
                             Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(110, 118, 129)),
                             VerticalAlignment = VerticalAlignment.Center
                         }
                     })
            {
                DockPanel.SetDock(el, Dock.Left);
                bar.Children.Add(el);
            }

            return bar;
        }

        private static Button MakeTitleBtn(string content, System.Windows.Media.Color bg,
            System.Windows.Media.Color border, System.Windows.Media.Color fg)
        {
            return new Button
            {
                Content = content, Padding = new Thickness(10, 4, 10, 4), Height = 26,
                Background = new SolidColorBrush(bg), Foreground = new SolidColorBrush(fg),
                BorderThickness = new Thickness(1), BorderBrush = new SolidColorBrush(border),
                FontSize = 11, FontWeight = FontWeights.SemiBold, Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center
            };
        }

        private static string FormatDateTick(double x, ScottPlotTrendData trend, ScottPlot.Plot plt)
        {
            if (trend.XToDate == null || trend.XToDate.Count == 0) return "";

            double visibleDays;
            try
            {
                var lim = plt.Axes.GetLimits();
                visibleDays = lim.Right - lim.Left;
            }
            catch
            {
                visibleDays = 0;
            }

            if (visibleDays > 70) return "";

            var sortedKeys = trend.XToDate.Keys.OrderBy(k => k).ToList();
            double nearest = sortedKeys[0];
            double minDiff = Math.Abs(sortedKeys[0] - x);
            foreach (var k in sortedKeys)
            {
                double diff = Math.Abs(k - x);
                if (diff < minDiff)
                {
                    minDiff = diff;
                    nearest = k;
                }
            }

            if (!trend.XToDate.TryGetValue(nearest, out DateTime date)) return "";
            date = date.AddDays(x - nearest);
            return date.ToString("dd.MM");
        }

        private static float CalcMarkerSize(int count)
        {
            if (count <= 5)   return 18;
            if (count <= 10)  return 15;
            if (count <= 20)  return 13;
            if (count <= 40)  return 11;
            if (count <= 75)  return 9;
            if (count <= 120) return 8;
            if (count <= 250) return 7;
            if (count <= 500) return 5;
            return 4;
        }

        private static void UpdateMarkerSizesForZoom(WpfPlot wpfPlot, ScottPlotTrendData trend)
        {
            try
            {
                var lim = wpfPlot.Plot.Axes.GetLimits();
                double visibleDays = lim.Right - lim.Left;

                int visibleCount = trend.AvgX.Count(x => x >= lim.Left && x <= lim.Right);
                if (visibleCount < 1) visibleCount = 1;

                float newSize = CalcMarkerSize(visibleCount);

                var fixedSizes = new HashSet<float> { 26f, 14f };

                foreach (var item in wpfPlot.Plot.PlottableList)
                {
                    if (item is ScottPlot.Plottables.Scatter scatter
                        && scatter.MarkerSize > 0
                        && !fixedSizes.Contains(scatter.MarkerSize))
                    {
                        scatter.MarkerSize = newSize;
                    }
                }

                var visY = new List<double>();
                for (int i = 0; i < trend.AvgX.Length; i++)
                {
                    if (trend.AvgX[i] < lim.Left || trend.AvgX[i] > lim.Right) continue;
                    if (i < trend.AvgY.Length) visY.Add(trend.AvgY[i]);
                    if (trend.P95Y != null && i < trend.P95Y.Length) visY.Add(trend.P95Y[i]);
                }
                if (visY.Count > 0)
                {
                    visY.Add(trend.SlaThreshold);
                    double lo = visY.Min();
                    double hi = visY.Max();
                    double newYMin = Math.Max(0, Math.Floor(lo * 0.88 / 50) * 50);
                    double newYMax = Math.Ceiling(hi * 1.06 / 50) * 50;
                    if (newYMax > newYMin + 10)
                        wpfPlot.Plot.Axes.SetLimitsY(bottom: newYMin, top: newYMax);
                }
            }
            catch { }
        }
        
        public void ResetZoomForExport(MessageType messageType)
        {
            if (!_plotByMessageType.TryGetValue(messageType, out var wpfPlot) || wpfPlot == null) return;
            var limits = wpfPlot.Tag as double[];
            if (limits == null) return;

            wpfPlot.Plot.Axes.SetLimitsX(left: limits[0], right: limits[1]);
            wpfPlot.Plot.Axes.SetLimitsY(bottom: limits[2], top: limits[3]);
            wpfPlot.Refresh();
        }

        public void ZoomToDayForExport(MessageType messageType, ScottPlotTrendData trend, DateTime day)
        {
            if (!_plotByMessageType.TryGetValue(messageType, out var wpfPlot) || wpfPlot == null) return;
            var limits = wpfPlot.Tag as double[];
            if (limits == null || trend?.XToDate == null || trend.XToDate.Count == 0) return;

            double nearest = trend.XToDate.Keys
                .OrderBy(k => Math.Abs((trend.XToDate[k].Date - day.Date).TotalDays))
                .First();

            if (trend.XToDate[nearest].Date != day.Date)
            {
                ResetZoomForExport(messageType);
                return;
            }

            double halfWindow = 14.0;
            double zLeft  = Math.Max(limits[0], nearest - halfWindow);
            double zRight = Math.Min(limits[1], nearest + halfWindow);
            wpfPlot.Plot.Axes.SetLimitsX(left: zLeft, right: zRight);
            UpdateMarkerSizesForZoom(wpfPlot, trend);
            wpfPlot.Refresh();

            UpdateTimelineForDay(messageType, day);
        }
    }
}