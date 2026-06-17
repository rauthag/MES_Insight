using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using MESInsight.Core;
using SkiaSharp;

namespace MESInsight.Charts.Renderers
{
    public class TimelineChartRenderer : ChartRenderer
    {
        private const float LabelWidth = 110f;
        private const float AxisHeight = 18f;
        private const float TitleHeight = 28f;
        private const float MinimumLaneHeight = 32f;
        private const float BlockWidth = 7f;
        private const float BlockMinimumWidth = 3f;
        private const float GapPixels = 12f;
        private const int GapThresholdMinutes = 10;
        private const double OtherCategoryThreshold = 0.02;
        private const int SplitThreshold = 150;
        private const float MinimapHeight = 16f;

        private readonly List<double[]> _hexPositions = new List<double[]>();
        private readonly Action<List<ResponseRecord>, string> _onErrorClicked;

        public TimelineChartRenderer(Action<List<ResponseRecord>, string> onErrorClicked = null)
        {
            _onErrorClicked = onErrorClicked;
        }

        public override ChartType GetChartType() => ChartType.Timeline;
        public override int GetMinimumHeight(RenderContext context) => 180;

        public override UIElement Render(ChartData chartData, RenderContext context)
        {
            if (chartData?.TimelineEvents == null || chartData.TimelineEvents.Count == 0) return null;

            var day = chartData.FilteredRecords?.Count > 0
                ? chartData.FilteredRecords[0].TimestampParsed.Date
                : DateTime.Today;

            var events = chartData.TimelineEvents
                .Where(e => e.Start.Date == day.Date)
                .OrderBy(e => e.Start)
                .ToList();

            if (events.Count == 0) return null;

            var wrapper = new StackPanel();
            foreach (var (splitEvents, label) in SplitTimelineEvents(events))
                wrapper.Children.Add(BuildGroupComponent(splitEvents, label, chartData.MaxResponseTime));

            return wrapper;
        }

        private UIElement BuildGroupComponent(List<TimelineEvent> splitEvents, string label, int maxRt)
        {
            var lanes    = BuildSwimLanes(splitEvents);
            var segments = BuildCompressedSegments(splitEvents);
            var maxResponseTime = maxRt > 0 ? maxRt : 1;
            var errorEvents = splitEvents.Where(e => e.EventType == TimelineEventType.Error).ToList();

            var outer = new Border
            {
                Background      = new SolidColorBrush(Color.FromRgb(13, 17, 23)),
                BorderBrush     = new SolidColorBrush(Color.FromRgb(22, 60, 36)),
                BorderThickness = new Thickness(1),
                CornerRadius    = new CornerRadius(6),
                Margin          = new Thickness(0, 4, 0, 4)
            };

            float canvasH  = lanes.Count * MinimumLaneHeight + AxisHeight + MinimapHeight;
            outer.Height   = canvasH + TitleHeight;

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(TitleHeight) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(canvasH) });

            var titleEl = BuildTitleRow(label, errorEvents);
            Grid.SetRow(titleEl, 0);
            grid.Children.Add(titleEl);

            var canvas = BuildSwimlaneCanvas(lanes, segments, maxResponseTime);
            canvas.Height = canvasH;
            Grid.SetRow(canvas, 1);
            grid.Children.Add(canvas);

            outer.Child = grid;
            return outer;
        }

        private static List<(List<TimelineEvent> events, string label)> SplitTimelineEvents(List<TimelineEvent> events)
        {
            if (events.Count <= SplitThreshold)
            {
                var t0 = events.First().Start;
                var t1 = events.Last().Start;
                return new List<(List<TimelineEvent>, string)>
                {
                    (events, "Timeline  " + t0.ToString("dd.MM.yyyy") + "  " + t0.ToString("HH:mm") + " – " + t1.ToString("HH:mm"))
                };
            }

            int mid   = events.Count / 2;
            var half1 = events.Take(mid).ToList();
            var half2 = events.Skip(mid).ToList();

            string Label(List<TimelineEvent> e) =>
                "Timeline  " + e.First().Start.ToString("dd.MM.yyyy") + "  " +
                e.First().Start.ToString("HH:mm") + " – " + e.Last().Start.ToString("HH:mm");

            return new List<(List<TimelineEvent>, string)> { (half1, Label(half1)), (half2, Label(half2)) };
        }

        private FrameworkElement BuildSwimlaneCanvas(List<TimelineLane> lanes, List<TimelineSegment> segments, int maxResponseTime)
        {
            var container    = InitializeMainContainer();
            var mainImage    = InitializeMainImage(container);
            var minimapImage = InitializeMinimapImage(container);
            var overlay      = InitializeOverlayCanvas(container);
            var tooltip      = InitializeTooltip(container);

            var hitAreas        = new List<(SKRect rect, TimelineEvent evt, int laneIdx)>();
            TimelineEvent selEvt = null;
            string        selUid = null;
            double        zFrom  = -1, zTo = -1;
            bool          zoomed = false;
            var           timers = new List<DispatcherTimer>();

            var zoomBadge   = InitializeZoomBadge(overlay);
            var scrollFlash = InitializeScrollFlash(overlay);
            var crossLine   = InitializeCrosshairLine(overlay);
            var crossLabel  = InitializeCrosshairLabel(overlay);

            void StopTimers()
            {
                foreach (var t in timers) t.Stop();
                timers.Clear();
                var polys = overlay.Children.OfType<Polygon>().ToList();
                foreach (var p in polys) overlay.Children.Remove(p);
            }

            void Redraw()
            {
                double w = container.ActualWidth, h = container.ActualHeight;
                if (w < 10 || h < 10) return;
                int iw = (int)w, ih = (int)h;

                UpdateZoomBadgeVisibility(zoomed, segments, zFrom, zTo, zoomBadge);

                var localHits = new List<(SKRect, TimelineEvent, int)>();
                Task.Run(() =>
                {
                    double rf = zoomed ? zFrom : -1, rt2 = zoomed ? zTo : -1;
                    var bmp  = RenderMainSkiaBitmap(lanes, segments, maxResponseTime, iw, ih, rf, rt2, selEvt, selUid, localHits);
                    var bs   = ConvertSkiaBitmapToWpfSource(bmp); bmp.Dispose();
                    var mbmp = RenderMinimapSkiaBitmap(lanes, segments, maxResponseTime, iw, (int)MinimapHeight, rf, rt2);
                    var mbs  = ConvertSkiaBitmapToWpfSource(mbmp); mbmp.Dispose();

                    Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        mainImage.Source    = bs;
                        minimapImage.Source = mbs;
                        hitAreas            = localHits;

                        if (!string.IsNullOrEmpty(selUid))
                        {
                            var newPos = ComputeUniqueIdHitPositions(selUid, segments, container, lanes, zoomed, zFrom, zTo, hitAreas);
                            for (int ii = 0; ii < Math.Min(_hexPositions.Count, newPos.Count); ii++)
                            {
                                _hexPositions[ii][0] = newPos[ii].x;
                                _hexPositions[ii][1] = newPos[ii].y;
                            }
                        }
                    }));
                });
            }

            void UpdateHexPositions(List<(float x, float y, SKColor col)> positions)
            {
                int count = Math.Min(_hexPositions.Count, positions.Count);
                for (int i = 0; i < count; i++)
                {
                    _hexPositions[i][0] = positions[i].x;
                    _hexPositions[i][1] = positions[i].y;
                }
            }

            void StartRipples(List<(float x, float y, SKColor col)> positions)
            {
                StopTimers();
                _hexPositions.Clear();
                foreach (var (px, py, col) in positions)
                {
                    var pos    = new double[] { px, py };
                    _hexPositions.Add(pos);
                    var wpfCol = Color.FromArgb(255, col.Red, col.Green, col.Blue);
                    double phase = 0;
                    var hex = new Polygon
                    {
                        Stroke = new SolidColorBrush(wpfCol), StrokeThickness = 1.5,
                        Fill = Brushes.Transparent, Opacity = 0.9, IsHitTestVisible = false
                    };
                    overlay.Children.Add(hex);
                    var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
                    timer.Tick += (s, e) =>
                    {
                        phase = (phase + 0.05) % 3.0;
                        double r = 5 + 16 * (phase / 3.0);
                        hex.Opacity = 1.0 - (phase / 3.0);
                        hex.Points  = CalculateHexagonPoints(r, pos[0], pos[1]);
                    };
                    timer.Start();
                    timers.Add(timer);
                }
            }

            container.Loaded      += (s, e) => Redraw();
            container.SizeChanged += (s, e) => { StopTimers(); Redraw(); };

            container.MouseWheel += (s, e) =>
            {
                HandleMouseWheel(e, container, segments, ref zoomed, ref zFrom, ref zTo, scrollFlash, Redraw);
                if (!string.IsNullOrEmpty(selUid) && zoomed)
                {
                    var am = CalculateAbsoluteSecondsMap(segments).ToDictionary(a => a.timelineEvent, a => a.absoluteSeconds);
                    var us = hitAreas.Where(h => h.evt.UniqueId == selUid)
                        .Select(h => { am.TryGetValue(h.evt, out double a); return a; })
                        .Where(a => a > 0).ToList();
                    if (us.Count >= 2)
                    {
                        double mg = (zTo - zFrom) * 0.15;
                        if (zFrom > us.Min() - mg) { zFrom = Math.Max(0, us.Min() - mg); Redraw(); }
                        if (zTo   < us.Max() + mg) { zTo   = Math.Min(segments.Where(sg => !sg.IsGap).Sum(sg => (sg.To - sg.From).TotalSeconds), us.Max() + mg); Redraw(); }
                    }
                }
            };
            container.MouseRightButtonUp += (s, e) => { zoomed = false; zFrom = -1; zTo = -1; selEvt = null; selUid = null; StopTimers(); Redraw(); };
            container.MouseLeftButtonUp  += (s, e) => HandleMouseClick(e, container, segments, lanes, hitAreas, overlay, timers, ref selEvt, ref selUid, ref zoomed, ref zFrom, ref zTo, StartRipples, Redraw);
            container.MouseMove          += (s, e) => HandleMouseMove(e, container, segments, lanes, hitAreas, tooltip, crossLine, crossLabel, zoomed, zFrom, zTo);
            container.MouseLeave         += (s, e) => { tooltip.IsOpen = false; crossLine.Visibility = Visibility.Collapsed; crossLabel.Visibility = Visibility.Collapsed; };

            return container;
        }

        private static void HandleMouseWheel(
            MouseWheelEventArgs e, FrameworkElement container, List<TimelineSegment> segments,
            ref bool zoomed, ref double zFrom, ref double zTo, Line scrollFlash, Action redraw)
        {
            e.Handled = true;
            var pos  = e.GetPosition(container);
            double usW = container.ActualWidth - LabelWidth;
            if (usW <= 0 || pos.X < LabelWidth) return;

            double totalS = segments.Where(s => !s.IsGap).Sum(s => (s.To - s.From).TotalSeconds);
            if (totalS <= 0) return;

            double curFrom = zoomed ? zFrom : 0;
            double curTo   = zoomed ? zTo   : totalS;
            double span    = curTo - curFrom;
            double relX    = Math.Max(0, Math.Min(1, (pos.X - LabelWidth) / usW));
            double pivot   = curFrom + relX * span;
            double factor  = e.Delta > 0 ? 0.80 : 1.2;
            double newSpan = span * factor;

            if (newSpan >= totalS) { zoomed = false; zFrom = -1; zTo = -1; redraw(); return; }
            if (newSpan < 10) newSpan = 10;

            double newFrom = pivot - relX * newSpan;
            double newTo   = pivot + (1 - relX) * newSpan;

            if (newFrom < 0) { newTo -= newFrom; newFrom = 0; }
            if (newTo > totalS) { newFrom -= (newTo - totalS); newTo = totalS; }
            newFrom = Math.Max(0, newFrom);

            zFrom = newFrom; zTo = newTo; zoomed = true;
            redraw();
            TriggerScrollFlashAnimation(scrollFlash, pos.X, container.ActualHeight);
        }

        private void HandleMouseClick(
            MouseButtonEventArgs e, FrameworkElement container, List<TimelineSegment> segments,
            List<TimelineLane> lanes, List<(SKRect rect, TimelineEvent evt, int laneIdx)> hitAreas,
            Canvas overlay, List<DispatcherTimer> timers,
            ref TimelineEvent selEvt, ref string selUid,
            ref bool zoomed, ref double zFrom, ref double zTo,
            Action<List<(float, float, SKColor)>> startRipples, Action redraw)
        {
            var pos  = e.GetPosition(container);
            var snap = hitAreas.ToList();
            float laneH = (float)(container.ActualHeight - AxisHeight - MinimapHeight) / Math.Max(1, lanes.Count);

            var hit = snap.FirstOrDefault(h =>
                pos.X >= h.rect.Left && pos.X <= h.rect.Right &&
                pos.Y >= h.laneIdx * laneH && pos.Y <= (h.laneIdx + 1) * laneH);

            if (hit.evt != null)
            {
                selEvt = hit.evt;
                selUid = hit.evt.UniqueId;

                var uidHits = ComputeUniqueIdHitPositions(selUid, segments, container, lanes, zoomed, zFrom, zTo, snap);
                ComputeZoomForUid(selUid, snap, segments, ref zoomed, ref zFrom, ref zTo);

                if (hit.evt.EventType == TimelineEventType.Error)
                    StartErrorPulseEffect(overlay, (float)pos.X, (float)(container.ActualHeight - AxisHeight), timers);
                else
                    startRipples(uidHits);
            }
            else
            {
                selEvt = null; selUid = null;
                foreach (var t in timers) t.Stop();
                timers.Clear();
                var polys = overlay.Children.OfType<Polygon>().ToList();
                foreach (var p in polys) overlay.Children.Remove(p);
            }

            redraw();
        }

        private static void ComputeZoomForUid(
            string uid, List<(SKRect rect, TimelineEvent evt, int laneIdx)> hitAreas,
            List<TimelineSegment> segments,
            ref bool zoomed, ref double zFrom, ref double zTo)
        {
            if (string.IsNullOrEmpty(uid)) return;
            var absMap   = CalculateAbsoluteSecondsMap(segments).ToDictionary(a => a.timelineEvent, a => a.absoluteSeconds);
            double totalS = segments.Where(s => !s.IsGap).Sum(s => (s.To - s.From).TotalSeconds);

            var absSecs = hitAreas
                .Where(h => h.evt.UniqueId == uid)
                .Select(h => { absMap.TryGetValue(h.evt, out double a); return a; })
                .Where(a => a > 0).ToList();

            if (absSecs.Count < 2) return;

            double span    = absSecs.Max() - absSecs.Min();
            double padding = Math.Max(120, span * 5.0);
            zFrom  = Math.Max(0, absSecs.Min() - padding);
            zTo    = Math.Min(totalS, absSecs.Max() + padding);
            zoomed = true;
        }

        private static void HandleMouseMove(
            MouseEventArgs e, FrameworkElement container, List<TimelineSegment> segments,
            List<TimelineLane> lanes, List<(SKRect rect, TimelineEvent evt, int laneIdx)> hitAreas,
            Popup tooltip, Line crossLine, Border crossLabel,
            bool zoomed, double zFrom, double zTo)
        {
            var pos  = e.GetPosition(container);
            var snap = hitAreas.ToList();
            float laneH = (float)(container.ActualHeight - AxisHeight - MinimapHeight) / Math.Max(1, lanes.Count);

            var hit = snap.FirstOrDefault(h =>
                pos.X >= h.rect.Left && pos.X <= h.rect.Right &&
                pos.Y >= h.laneIdx * laneH && pos.Y <= (h.laneIdx + 1) * laneH);

            if (hit.evt != null) { tooltip.Child = BuildTooltipContentStatic(hit.evt); tooltip.IsOpen = true; }
            else tooltip.IsOpen = false;

            if (pos.X >= LabelWidth)
            {
                double usW    = container.ActualWidth - LabelWidth;
                double relX   = (pos.X - LabelWidth) / usW;
                double totalS = segments.Where(s => !s.IsGap).Sum(s => (s.To - s.From).TotalSeconds);
                double curFrom = zoomed ? zFrom : 0;
                double curSpan = zoomed ? (zTo - zFrom) : totalS;
                var    time    = ConvertAbsoluteSecondsToDateTime(segments, curFrom + relX * curSpan);

                crossLine.X1 = pos.X; crossLine.Y1 = 0;
                crossLine.X2 = pos.X; crossLine.Y2 = container.ActualHeight - AxisHeight;
                crossLine.Visibility = Visibility.Visible;

                ((TextBlock)crossLabel.Child).Text = time.ToString("HH:mm:ss");
                double lx = pos.X + 6;
                if (lx + 60 > container.ActualWidth) lx = pos.X - 66;
                Canvas.SetLeft(crossLabel, lx);
                Canvas.SetTop(crossLabel, 4);
                crossLabel.Visibility = Visibility.Visible;
            }
            else
            {
                crossLine.Visibility  = Visibility.Collapsed;
                crossLabel.Visibility = Visibility.Collapsed;
            }
        }

        private Grid InitializeMainContainer()
        {
            var g = new Grid();
            g.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            g.RowDefinitions.Add(new RowDefinition { Height = new GridLength(AxisHeight) });
            g.RowDefinitions.Add(new RowDefinition { Height = new GridLength(MinimapHeight) });
            g.VerticalAlignment = VerticalAlignment.Stretch;
            return g;
        }

        private Image InitializeMainImage(Grid g)
        {
            var img = new Image { Stretch = Stretch.Fill, HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch, SnapsToDevicePixels = true };
            Grid.SetRowSpan(img, 2); g.Children.Add(img); return img;
        }

        private Image InitializeMinimapImage(Grid g)
        {
            var img = new Image { Stretch = Stretch.Fill, HorizontalAlignment = HorizontalAlignment.Stretch, SnapsToDevicePixels = true };
            Grid.SetRow(img, 2); g.Children.Add(img); return img;
        }

        private Canvas InitializeOverlayCanvas(Grid g)
        {
            var c = new Canvas { IsHitTestVisible = false, ClipToBounds = true };
            Grid.SetRowSpan(c, 2); g.Children.Add(c); return c;
        }

        private Popup InitializeTooltip(Grid g)
        {
            var p = new Popup { AllowsTransparency = true, Placement = PlacementMode.Mouse, StaysOpen = false, IsOpen = false };
            g.Children.Add(p); return p;
        }

        private Border InitializeZoomBadge(Canvas c)
        {
            var b = new Border { Background = new SolidColorBrush(Color.FromArgb(180, 13, 17, 23)), BorderBrush = new SolidColorBrush(Color.FromArgb(100, 80, 120, 180)), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4), Padding = new Thickness(6, 2, 6, 2), IsHitTestVisible = false, Visibility = Visibility.Collapsed, Child = new TextBlock { FontSize = 9, Foreground = new SolidColorBrush(Color.FromRgb(140, 170, 210)) } };
            c.Children.Add(b); return b;
        }

        private Line InitializeScrollFlash(Canvas c)
        {
            var l = new Line { Stroke = new SolidColorBrush(Color.FromArgb(200, 255, 220, 80)), StrokeThickness = 2, IsHitTestVisible = false, Visibility = Visibility.Collapsed };
            c.Children.Add(l); return l;
        }

        private Line InitializeCrosshairLine(Canvas c)
        {
            var l = new Line { Stroke = new SolidColorBrush(Color.FromArgb(80, 200, 220, 255)), StrokeThickness = 1, StrokeDashArray = new DoubleCollection { 3, 3 }, IsHitTestVisible = false, Visibility = Visibility.Collapsed };
            c.Children.Add(l); return l;
        }

        private Border InitializeCrosshairLabel(Canvas c)
        {
            var b = new Border { Background = new SolidColorBrush(Color.FromArgb(200, 18, 22, 30)), BorderBrush = new SolidColorBrush(Color.FromArgb(120, 100, 140, 200)), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(3), Padding = new Thickness(5, 2, 5, 2), IsHitTestVisible = false, Visibility = Visibility.Collapsed, Child = new TextBlock { FontSize = 9, Foreground = new SolidColorBrush(Color.FromRgb(180, 200, 220)) } };
            c.Children.Add(b); return b;
        }

        private static void TriggerScrollFlashAnimation(Line line, double x, double h)
        {
            line.X1 = x; line.X2 = x; line.Y1 = 0; line.Y2 = h - AxisHeight;
            line.Opacity = 1; line.Visibility = Visibility.Visible;
            var fa = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(400));
            fa.Completed += (s, e) => line.Visibility = Visibility.Collapsed;
            line.BeginAnimation(UIElement.OpacityProperty, fa);
        }

        private void UpdateZoomBadgeVisibility(bool isZoomed, List<TimelineSegment> segments, double zFrom, double zTo, Border badge)
        {
            if (!isZoomed) { badge.Visibility = Visibility.Collapsed; return; }
            var evts = segments.Where(s => !s.IsGap).SelectMany(s => s.Events).ToList();
            if (evts.Count == 0) return;
            var t0 = ConvertAbsoluteSecondsToDateTime(segments, zFrom);
            var t1 = ConvertAbsoluteSecondsToDateTime(segments, zTo);
            ((TextBlock)badge.Child).Text = t0.ToString("HH:mm:ss") + " – " + t1.ToString("HH:mm:ss") + "  (" + FormatTimeSpan(zTo - zFrom) + ")";
            Canvas.SetRight(badge, 8); Canvas.SetTop(badge, 4);
            badge.Visibility = Visibility.Visible;
        }

        private List<(float x, float y, SKColor col)> ComputeUniqueIdHitPositions(
            string uid, List<TimelineSegment> segments, FrameworkElement container,
            List<TimelineLane> lanes, bool zoomed, double zFrom, double zTo,
            List<(SKRect rect, TimelineEvent evt, int laneIdx)> hitAreas)
        {
            if (string.IsNullOrEmpty(uid)) return new List<(float, float, SKColor)>();

            var absMap = CalculateAbsoluteSecondsMap(segments).ToDictionary(a => a.timelineEvent, a => a.absoluteSeconds);
            double totalS = segments.Where(s => !s.IsGap).Sum(s => (s.To - s.From).TotalSeconds);
            double ef     = zoomed ? zFrom : 0;
            double et     = zoomed ? zTo   : totalS;
            double usW    = container.ActualWidth - LabelWidth;
            double laneH  = (container.ActualHeight - AxisHeight - MinimapHeight) / Math.Max(1, lanes.Count);

            return hitAreas
                .Where(h => h.evt.UniqueId == uid)
                .Select(h =>
                {
                    absMap.TryGetValue(h.evt, out double abs);
                    float wx = (float)(LabelWidth + (abs - ef) / Math.Max(1, et - ef) * usW);
                    float wy = (float)(h.laneIdx * laneH + laneH * 0.75);
                    return (wx, wy, GetSkiaColorForMessageType(h.evt.MessageKind));
                }).ToList();
        }

        private static void StartErrorPulseEffect(Canvas overlay, float x, float h, List<DispatcherTimer> timers)
        {
            var rect = new Rectangle { Width = 8, Height = h, Fill = new SolidColorBrush(Color.FromArgb(80, 220, 40, 40)), IsHitTestVisible = false };
            Canvas.SetLeft(rect, x - 4); Canvas.SetTop(rect, 0);
            overlay.Children.Add(rect);
            int tick = 0;
            var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(60) };
            t.Tick += (s, e) =>
            {
                tick++;
                byte a = (byte)(40 + 80 * Math.Abs(Math.Sin(tick * 0.15)));
                rect.Fill = new SolidColorBrush(Color.FromArgb(a, 220, 40, 40));
                if (tick > 60) { t.Stop(); overlay.Children.Remove(rect); }
            };
            t.Start(); timers.Add(t);
        }

        private static PointCollection CalculateHexagonPoints(double r, double cx = 0, double cy = 0)
        {
            var pts = new PointCollection();
            for (int i = 0; i < 6; i++)
            {
                double a = Math.PI / 180 * (60 * i - 30);
                pts.Add(new Point(cx + r * Math.Cos(a), cy + r * Math.Sin(a)));
            }
            return pts;
        }

        private SKBitmap RenderMainSkiaBitmap(
            List<TimelineLane> lanes, List<TimelineSegment> segments, int maxRt,
            int w, int h, double zFrom, double zTo,
            TimelineEvent selEvt, string selUid,
            List<(SKRect rect, TimelineEvent evt, int laneIdx)> hitAreas)
        {
            var bmp    = new SKBitmap(w, h, SKColorType.Bgra8888, SKAlphaType.Premul);
            var canvas = new SKCanvas(bmp);
            canvas.Clear(new SKColor(13, 17, 23));

            float totalH  = h - AxisHeight;
            float laneH   = Math.Max(MinimumLaneHeight, totalH / lanes.Count);
            float usW     = w - LabelWidth;
            if (usW < 10) { canvas.Dispose(); return bmp; }

            float totalSpan = (float)segments.Where(s => !s.IsGap).Sum(s => (s.To - s.From).TotalSeconds);
            if (totalSpan <= 0) { canvas.Dispose(); return bmp; }

            bool  isZ  = zFrom >= 0 && zTo > zFrom;
            float ef   = (float)zFrom, et = (float)zTo;

            var absMap = CalculateAbsoluteSecondsMap(segments).ToDictionary(a => a.timelineEvent, a => a.absoluteSeconds);

            var bgP1 = new SKPaint { Color = new SKColor(16, 22, 30) };
            var bgP2 = new SKPaint { Color = new SKColor(13, 17, 23) };
            var sepP = new SKPaint { Color = new SKColor(30, 38, 48), StrokeWidth = 1 };

            for (int li = 0; li < lanes.Count; li++)
            {
                float ly   = li * laneH;
                var   lane = lanes[li];
                var   col  = GetSkiaColorForLane(lane);
                canvas.DrawRect(LabelWidth, ly, usW, laneH, li % 2 == 0 ? bgP1 : bgP2);
                var lbg = new SKPaint { Color = new SKColor(10, 14, 20, 200) };
                canvas.DrawRect(0, ly, LabelWidth - 1, laneH, lbg); lbg.Dispose();
                var ltp = new SKPaint { Color = col.WithAlpha(180), TextSize = 10f, IsAntialias = true };
                canvas.DrawText(lane.IsOtherCategory ? "Other" : lane.TypeName, 8f, ly + laneH / 2 + 4f, ltp); ltp.Dispose();
                canvas.DrawLine(LabelWidth - 1, ly, LabelWidth - 1, ly + laneH, sepP);
                if (li > 0) canvas.DrawLine(0, ly, w, ly, sepP);
            }

            var lanePos = new Dictionary<int, List<(float x, float y, float bh, TimelineEvent evt)>>();
            for (int i = 0; i < lanes.Count; i++) lanePos[i] = new List<(float, float, float, TimelineEvent)>();

            for (int li = 0; li < lanes.Count; li++)
            {
                float ly = li * laneH;
                foreach (var evt in lanes[li].Events)
                {
                    if (!absMap.TryGetValue(evt, out double abs)) continue;
                    float x;
                    if (isZ)
                    {
                        if (abs < ef || abs > et) continue;
                        x = LabelWidth + (float)((abs - ef) / (et - ef)) * usW;
                    }
                    else x = LabelWidth + (float)(abs / totalSpan) * usW;

                    float rt  = maxRt > 0 ? Math.Min(1f, evt.ResponseTimeMs / (float)maxRt) : 0.5f;
                    float bh  = Math.Max(4f, laneH * 0.75f * rt + laneH * 0.1f);
                    float by  = ly + laneH - bh - 2f;
                    lanePos[li].Add((x, by, bh, evt));
                }
            }

            var errXs = lanePos.Values.SelectMany(v => v).Where(v => v.evt.EventType == TimelineEventType.Error).Select(v => v.x).ToList();
            foreach (var ex in errXs)
            {
                var ep = new SKPaint { Color = new SKColor(220, 40, 40, 35) };
                canvas.DrawRect(ex - 3, 0, 6, totalH, ep); ep.Dispose();
            }

            float laneHForLines = totalH / Math.Max(1, lanes.Count);

            var allUids = lanePos.Values.SelectMany(v => v)
                .Where(v => !string.IsNullOrEmpty(v.evt.UniqueId))
                .GroupBy(v => v.evt.UniqueId)
                .Where(g => g.Count() > 1);

            var dimSp = new SKPaint { Color = new SKColor(200, 170, 60, 90), StrokeWidth = 1.5f, IsAntialias = true, Style = SKPaintStyle.Stroke, PathEffect = SKPathEffect.CreateDash(new float[] { 5, 3 }, 0) };
            foreach (var uidGroup in allUids)
            {
                if (uidGroup.Key == selUid) continue;
                var grpPts = uidGroup.OrderBy(v => v.x).ToList();
                for (int gi = 1; gi < grpPts.Count; gi++)
                {
                    var srcCol = GetSkiaColorForMessageType(grpPts[gi-1].evt.MessageKind);
                    var segPaint = new SKPaint { Color = srcCol.WithAlpha(90), StrokeWidth = 1.5f, IsAntialias = true, Style = SKPaintStyle.Stroke, PathEffect = SKPathEffect.CreateDash(new float[] { 5, 3 }, 0) };
                    DrawUidLines(canvas, new List<(float,float)> { (grpPts[gi-1].x, grpPts[gi-1].y + grpPts[gi-1].bh / 2), (grpPts[gi].x, grpPts[gi].y + grpPts[gi].bh / 2) }, segPaint, laneHForLines, totalH);
                    segPaint.Dispose();
                }
            }
            dimSp.Dispose();

            if (!string.IsNullOrEmpty(selUid))
            {
                var ptsWithEvt = lanePos.Values.SelectMany(v => v)
                    .Where(v => v.evt.UniqueId == selUid)
                    .OrderBy(v => v.x).ToList();
                for (int si = 1; si < ptsWithEvt.Count; si++)
                {
                    var srcCol2 = GetSkiaColorForMessageType(ptsWithEvt[si-1].evt.MessageKind);
                    var selSp = new SKPaint { Color = srcCol2.WithAlpha(220), StrokeWidth = 2.5f, IsAntialias = true, Style = SKPaintStyle.Stroke, PathEffect = SKPathEffect.CreateDash(new float[] { 5, 3 }, 0) };
                    DrawUidLines(canvas, new List<(float,float)> { (ptsWithEvt[si-1].x, ptsWithEvt[si-1].y + ptsWithEvt[si-1].bh / 2), (ptsWithEvt[si].x, ptsWithEvt[si].y + ptsWithEvt[si].bh / 2) }, selSp, laneHForLines, totalH);
                    selSp.Dispose();
                }
            }

            for (int li = 0; li < lanes.Count; li++)
            {
                var col = GetSkiaColorForLane(lanes[li]);
                foreach (var (x, by, bh, evt) in lanePos[li])
                {
                    bool isSel  = evt == selEvt;
                    bool isUid  = !string.IsNullOrEmpty(selUid) && evt.UniqueId == selUid;
                    bool isErr  = evt.EventType == TimelineEventType.Error;
                    SKColor fc  = isErr ? new SKColor(220, 40, 40) : CalculateHeatmapColor(maxRt > 0 ? Math.Min(1f, evt.ResponseTimeMs / (float)maxRt) : 0f, col);
                    byte alpha  = (selEvt != null || !string.IsNullOrEmpty(selUid)) ? (isUid || isSel ? (byte)255 : (byte)50) : (byte)210;
                    float vs    = isZ ? (et - ef) : totalSpan;
                    float bw    = Math.Max(BlockMinimumWidth, Math.Min(28f, usW / (vs + 1) * 4f));
                    if (isUid || isSel) bw = Math.Min(24f, bw * 2f);
                    if (isErr) bw += 3;
                    var rect    = new SKRect(x - bw / 2, by, x + bw / 2, by + bh);
                    var fp      = new SKPaint { Color = fc.WithAlpha(alpha), IsAntialias = true };
                    canvas.DrawRoundRect(new SKRoundRect(rect, 2f), fp); fp.Dispose();

                    if (isErr) DrawHexagonOutlineOnCanvas(canvas, x, by + bh / 2, bw * 0.9f, new SKColor(255, 80, 80, (byte)(alpha > 50 ? 200 : 80)));

                    if (isSel)
                    {
                        var sp2 = new SKPaint { Color = new SKColor(255, 255, 255, 220), StrokeWidth = 1.5f, Style = SKPaintStyle.Stroke, IsAntialias = true };
                        canvas.DrawRoundRect(new SKRoundRect(new SKRect(rect.Left - 2, rect.Top - 2, rect.Right + 2, rect.Bottom + 2), 3f), sp2); sp2.Dispose();
                    }

                    float pad = Math.Max(4f, bw * 0.5f);
                    hitAreas.Add((new SKRect(rect.Left - pad, li * laneH, rect.Right + pad, (li + 1) * laneH), evt, li));
                }
            }

            DrawGapsOnCanvas(canvas, segments, totalSpan, usW, totalH, isZ, ef, et);
            DrawAxisOnCanvas(canvas, segments, totalSpan, usW, isZ, ef, et, totalH);

            bgP1.Dispose(); bgP2.Dispose(); sepP.Dispose();
            canvas.Dispose();
            return bmp;
        }

        private static void DrawUidLines(SKCanvas canvas, List<(float x, float y)> pts, SKPaint paint, float laneH, float totalH)
        {
            for (int i = 1; i < pts.Count; i++)
            {
                float x0 = pts[i-1].x, y0 = pts[i-1].y, x1 = pts[i].x, y1 = pts[i].y;
                float cpX = (x0 + x1) / 2;
                float maxOff = Math.Min(laneH * 0.4f, Math.Abs(x1 - x0) * 0.15f);
                float off = (i % 2 == 0 ? 1 : -1) * maxOff;
                float cp1Y = Math.Max(0, Math.Min(totalH, y0 + off));
                float cp2Y = Math.Max(0, Math.Min(totalH, y1 + off));
                var path = new SKPath();
                path.MoveTo(x0, y0);
                path.CubicTo(cpX, cp1Y, cpX, cp2Y, x1, y1);
                canvas.DrawPath(path, paint);
                path.Dispose();
            }
        }

        private static void DrawHexagonOutlineOnCanvas(SKCanvas canvas, float cx, float cy, float r, SKColor col)
        {
            var path = new SKPath();
            var p    = new SKPaint { Color = col, StrokeWidth = 1.2f, Style = SKPaintStyle.Stroke, IsAntialias = true };
            for (int i = 0; i < 6; i++)
            {
                double a = Math.PI / 180 * (60 * i - 30);
                float px = cx + r * (float)Math.Cos(a), py = cy + r * (float)Math.Sin(a);
                if (i == 0) path.MoveTo(px, py); else path.LineTo(px, py);
            }
            path.Close(); canvas.DrawPath(path, p);
            path.Dispose(); p.Dispose();
        }

        private SKBitmap RenderMinimapSkiaBitmap(List<TimelineLane> lanes, List<TimelineSegment> segments, int maxRt, int w, int h, double zFrom, double zTo)
        {
            var bmp    = new SKBitmap(w, h, SKColorType.Bgra8888, SKAlphaType.Premul);
            var canvas = new SKCanvas(bmp);
            canvas.Clear(new SKColor(10, 13, 18));

            float usW    = w - LabelWidth;
            float totalS = (float)segments.Where(s => !s.IsGap).Sum(s => (s.To - s.From).TotalSeconds);
            if (usW < 2 || totalS <= 0) { canvas.Dispose(); return bmp; }

            var absMap = CalculateAbsoluteSecondsMap(segments).ToDictionary(a => a.timelineEvent, a => a.absoluteSeconds);

            foreach (var lane in lanes)
            {
                var col = GetSkiaColorForLane(lane);
                foreach (var evt in lane.Events)
                {
                    if (!absMap.TryGetValue(evt, out double abs)) continue;
                    float x  = LabelWidth + (float)(abs / totalS) * usW;
                    float rt = maxRt > 0 ? Math.Min(1f, evt.ResponseTimeMs / (float)maxRt) : 0.5f;
                    var   fc = evt.EventType == TimelineEventType.Error ? new SKColor(220, 40, 40) : CalculateHeatmapColor(rt, col);
                    var   p  = new SKPaint { Color = fc.WithAlpha(180) };
                    canvas.DrawLine(x, 0, x, h - 2, p); p.Dispose();
                }
            }

            if (zFrom >= 0 && zTo > zFrom)
            {
                float x0 = LabelWidth + (float)(zFrom / totalS) * usW;
                float x1 = LabelWidth + (float)(zTo   / totalS) * usW;
                var bp = new SKPaint { Color = new SKColor(56, 139, 253, 90) };
                var bdp = new SKPaint { Color = new SKColor(100, 180, 255, 220), StrokeWidth = 2, Style = SKPaintStyle.Stroke };
                canvas.DrawRect(x0, 0, x1 - x0, h, bp);
                canvas.DrawRect(x0, 0, x1 - x0, h, bdp);
                bp.Dispose(); bdp.Dispose();
            }

            var lbg = new SKPaint { Color = new SKColor(10, 13, 18, 200) };
            var lt  = new SKPaint { Color = new SKColor(70, 85, 105), TextSize = 8f, IsAntialias = true };
            canvas.DrawRect(0, 0, LabelWidth - 1, h, lbg);
            canvas.DrawText("overview", 4f, h - 3f, lt);
            lbg.Dispose(); lt.Dispose();
            canvas.Dispose();
            return bmp;
        }

        private static void DrawGapsOnCanvas(SKCanvas canvas, List<TimelineSegment> segments, float totalS, float usW, float totalH, bool isZ, float ef, float et)
        {
            var bgP  = new SKPaint { Color = new SKColor(30, 36, 46, 180) };
            var bdP  = new SKPaint { Color = new SKColor(55, 65, 85, 160), StrokeWidth = 1, Style = SKPaintStyle.Stroke };
            var txtP = new SKPaint { Color = new SKColor(90, 105, 130), TextSize = 9f, IsAntialias = true, TextAlign = SKTextAlign.Center };
            float acc = 0;

            foreach (var seg in segments)
            {
                float ss = (float)(seg.To - seg.From).TotalSeconds;
                if (seg.IsGap)
                {
                    float x;
                    if (isZ)
                    {
                        if (acc < ef || acc > et) { acc += ss; continue; }
                        x = LabelWidth + (acc - ef) / (et - ef) * usW;
                    }
                    else x = LabelWidth + (totalS > 0 ? acc / totalS * usW : 0);

                    canvas.DrawRect(x, 0, GapPixels, totalH, bgP);
                    canvas.DrawRect(x, 0, GapPixels, totalH, bdP);
                    int min = (int)(seg.To - seg.From).TotalMinutes;
                    if (min > 0)
                    {
                        canvas.Save();
                        canvas.Translate(x + GapPixels / 2, totalH / 2);
                        canvas.RotateDegrees(-90);
                        canvas.DrawText(min + "min", 0, 3, txtP);
                        canvas.Restore();
                    }
                }
                acc += seg.IsGap ? 0 : ss;
            }
            bgP.Dispose(); bdP.Dispose(); txtP.Dispose();
        }

        private static void DrawAxisOnCanvas(SKCanvas canvas, List<TimelineSegment> segments, float totalS, float usW, bool isZ, float ef, float et, float axisY)
        {
            var tickP  = new SKPaint { Color = new SKColor(80, 100, 130), StrokeWidth = 1 };
            var lblP   = new SKPaint { Color = new SKColor(150, 170, 200), TextSize = 10f, IsAntialias = true, FakeBoldText = true };
            float xCur = LabelWidth, acc = 0, lastX = float.MinValue;

            foreach (var seg in segments)
            {
                if (seg.IsGap) { xCur += GapPixels; continue; }
                float ss   = (float)(seg.To - seg.From).TotalSeconds;
                float segW = totalS > 0 ? ss / totalS * usW : 0;
                int   spanM = (int)(seg.To - seg.From).TotalMinutes;
                int   step  = spanM <= 5 ? 1 : spanM <= 15 ? 2 : spanM <= 30 ? 5 : spanM <= 90 ? 15 : spanM <= 240 ? 30 : 60;
                var   cur   = RoundUpDateTimeToStep(seg.From, step);

                while (cur <= seg.To)
                {
                    float abs = acc + (float)(cur - seg.From).TotalSeconds;
                    float x   = isZ
                        ? (abs >= ef && abs <= et ? LabelWidth + (abs - ef) / (et - ef) * usW : -999f)
                        : xCur + (ss > 0 ? (float)(cur - seg.From).TotalSeconds / ss * segW : 0);

                    float minGap = isZ ? 30f : 50f;
                    if (x >= LabelWidth && x - lastX >= minGap)
                    {
                        canvas.DrawLine(x, 0, x, axisY + 5, tickP);
                        string lbl = isZ ? cur.ToString("HH:mm:ss") : cur.ToString("HH:mm");
                        canvas.DrawText(lbl, x - 16, axisY + 14, lblP);
                        lastX = x;
                    }
                    cur = cur.AddMinutes(step);
                }
                xCur += segW; acc += ss;
            }
            tickP.Dispose(); lblP.Dispose();
        }

        private static List<(TimelineEvent timelineEvent, double absoluteSeconds)> CalculateAbsoluteSecondsMap(List<TimelineSegment> segs)
        {
            var result = new List<(TimelineEvent, double)>();
            double acc = 0;
            foreach (var seg in segs)
            {
                if (seg.IsGap) continue;
                double ss = (seg.To - seg.From).TotalSeconds;
                foreach (var evt in seg.Events)
                    result.Add((evt, acc + (evt.Start - seg.From).TotalSeconds));
                acc += ss;
            }
            return result;
        }

        private static List<TimelineSegment> BuildCompressedSegments(List<TimelineEvent> events)
        {
            var result = new List<TimelineSegment>();
            var groups = new List<List<TimelineEvent>>();
            var cur    = new List<TimelineEvent> { events[0] };

            for (int i = 1; i < events.Count; i++)
            {
                if ((events[i].Start - events[i-1].Start).TotalMinutes > GapThresholdMinutes)
                { groups.Add(cur); cur = new List<TimelineEvent>(); }
                cur.Add(events[i]);
            }
            groups.Add(cur);

            for (int g = 0; g < groups.Count; g++)
            {
                var grp = groups[g];
                result.Add(new TimelineSegment { From = grp.First().Start.AddMinutes(-0.5), To = grp.Last().Start.AddMinutes(0.5), Events = new List<TimelineEvent>(grp), IsGap = false });
                if (g < groups.Count - 1)
                    result.Add(new TimelineSegment { From = grp.Last().Start, To = groups[g+1].First().Start, Events = new List<TimelineEvent>(), IsGap = true });
            }
            return result;
        }

        private static List<TimelineLane> BuildSwimLanes(List<TimelineEvent> events)
        {
            int total  = events.Count;
            var byType = events.GroupBy(e => e.MessageKind).ToDictionary(g => g.Key, g => g.ToList());
            var lanes  = new List<TimelineLane>();
            var others = new List<TimelineEvent>();

            foreach (var kv in byType.OrderByDescending(kv => kv.Value.Count))
            {
                if ((double)kv.Value.Count / total >= OtherCategoryThreshold)
                    lanes.Add(new TimelineLane { MessageType = kv.Key, TypeName = kv.Key.ToString().Replace("_", " "), Events = kv.Value, IsOtherCategory = false });
                else
                    others.AddRange(kv.Value);
            }

            if (others.Count > 0)
                lanes.Add(new TimelineLane { MessageType = MessageType.OTHER, TypeName = "Other", Events = others.OrderBy(e => e.Start).ToList(), IsOtherCategory = true });

            return lanes;
        }

        private static BitmapSource ConvertSkiaBitmapToWpfSource(SKBitmap bmp)
        {
            var bs = BitmapSource.Create(bmp.Width, bmp.Height, 96, 96, PixelFormats.Bgra32, null, bmp.Bytes, bmp.Width * 4);
            bs.Freeze();
            return bs;
        }

        private UIElement BuildTooltipContent(TimelineEvent evt) => BuildTooltipContentStatic(evt);

        private static UIElement BuildTooltipContentStatic(TimelineEvent evt)
        {
            bool isErr = evt.EventType == TimelineEventType.Error;
            var  panel = new StackPanel { Margin = new Thickness(10) };
            panel.Children.Add(new TextBlock { Text = isErr ? "ERROR  —  " + evt.Label : evt.Label, FontSize = 11, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(isErr ? Color.FromRgb(255, 80, 80) : GetWpfColorForMessageType(evt.MessageKind)), TextWrapping = TextWrapping.Wrap, MaxWidth = 360 });
            panel.Children.Add(new TextBlock { Text = evt.Start.ToString("HH:mm:ss.fff") + "   " + evt.ResponseTimeMs + " ms", FontSize = 10, Foreground = new SolidColorBrush(Color.FromRgb(140, 160, 150)), Margin = new Thickness(0, 3, 0, 0) });
            string uid = evt.UniqueId ?? evt.SourceRecord?.UidIn ?? evt.SourceRecord?.UidOut;
            if (!string.IsNullOrEmpty(uid))
                panel.Children.Add(new TextBlock { Text = "UID: " + uid, FontSize = 10, Foreground = new SolidColorBrush(Color.FromRgb(180, 190, 200)), Margin = new Thickness(0, 2, 0, 0) });
            if (!string.IsNullOrEmpty(evt.Detail))
                panel.Children.Add(new TextBlock { Text = evt.Detail, FontSize = 10, Foreground = new SolidColorBrush(Colors.LightGray), Margin = new Thickness(0, 4, 0, 0), TextWrapping = TextWrapping.Wrap, MaxWidth = 360 });
            return new Border { Background = new SolidColorBrush(Color.FromArgb(248, 18, 22, 30)), BorderBrush = new SolidColorBrush(isErr ? Color.FromRgb(220, 40, 40) : Color.FromRgb(56, 139, 253)), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6), Padding = new Thickness(10), Child = panel };
        }

        private Grid BuildTitleRow(string label, List<TimelineEvent> errors)
        {
            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.Children.Add(new TextBlock { Text = label, FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Color.FromRgb(63, 185, 80)), Margin = new Thickness(10, 3, 0, 0), VerticalAlignment = VerticalAlignment.Center });
            if (errors.Count > 0)
            {
                var badge = new Border { Background = new SolidColorBrush(Color.FromRgb(60, 14, 14)), BorderBrush = new SolidColorBrush(Color.FromRgb(180, 50, 50)), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4), Padding = new Thickness(7, 2, 7, 2), Margin = new Thickness(0, 3, 8, 3), VerticalAlignment = VerticalAlignment.Center, Cursor = Cursors.Hand, Child = new TextBlock { Text = "\u26a0  " + errors.Count + (errors.Count == 1 ? " error" : " errors"), FontSize = 9, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Color.FromRgb(248, 81, 73)) } };
                badge.MouseLeftButtonUp += (s, e) => _onErrorClicked?.Invoke(errors.Where(ev => ev.SourceRecord != null).Select(ev => ev.SourceRecord).ToList(), label);
                Grid.SetColumn(badge, 1);
                row.Children.Add(badge);
            }
            return row;
        }

        private static SKColor GetSkiaColorForLane(TimelineLane lane) =>
            lane.IsOtherCategory ? new SKColor(100, 110, 125) : GetSkiaColorForMessageType(lane.MessageType);

        private static SKColor GetSkiaColorForMessageType(MessageType t)
        {
            switch (t)
            {
                case MessageType.UNIT_INFO:         return new SKColor( 56, 182, 255);
                case MessageType.NEXT_OPERATION:    return new SKColor( 50, 230,  90);
                case MessageType.UNIT_CHECKIN:      return new SKColor(255, 160,  30);
                case MessageType.UNIT_RESULT:       return new SKColor(180, 100, 255);
                case MessageType.LOAD_MATERIAL:     return new SKColor(255,  70, 130);
                case MessageType.REQ_MATERIAL_INFO: return new SKColor( 40, 220, 200);
                case MessageType.REQ_SETUP_CHANGE2: return new SKColor(255, 220,  40);
                case MessageType.SEMI_VALIDATION2:  return new SKColor(255, 100,  60);
                default:                            return new SKColor(120, 130, 140);
            }
        }

        internal static Color GetWpfColorForMessageType(MessageType t)
        {
            switch (t)
            {
                case MessageType.UNIT_INFO:         return Color.FromRgb( 56, 182, 255);
                case MessageType.NEXT_OPERATION:    return Color.FromRgb( 50, 230,  90);
                case MessageType.UNIT_CHECKIN:      return Color.FromRgb(255, 160,  30);
                case MessageType.UNIT_RESULT:       return Color.FromRgb(180, 100, 255);
                case MessageType.LOAD_MATERIAL:     return Color.FromRgb(255,  70, 130);
                case MessageType.REQ_MATERIAL_INFO: return Color.FromRgb( 40, 220, 200);
                case MessageType.REQ_SETUP_CHANGE2: return Color.FromRgb(255, 220,  40);
                case MessageType.SEMI_VALIDATION2:  return Color.FromRgb(255, 100,  60);
                default:                            return Color.FromRgb(120, 130, 140);
            }
        }

        internal static Color GetMessageColor(MessageType t) => GetWpfColorForMessageType(t);

        private static SKColor CalculateHeatmapColor(float rt, SKColor col)
        {
            return new SKColor(
                (byte)(col.Red   + rt * (220 - col.Red)),
                (byte)(col.Green * (1 - rt * 0.85f)),
                (byte)(col.Blue  * (1 - rt * 0.85f)));
        }

        private static DateTime ConvertAbsoluteSecondsToDateTime(List<TimelineSegment> segs, double abs)
        {
            double acc = 0;
            foreach (var seg in segs)
            {
                if (seg.IsGap) continue;
                double ss = (seg.To - seg.From).TotalSeconds;
                if (abs <= acc + ss) return seg.From.AddSeconds(abs - acc);
                acc += ss;
            }
            return segs.Where(s => !s.IsGap).Last().To;
        }

        private static string FormatTimeSpan(double s)
        {
            if (s < 60)   return ((int)s) + "s";
            if (s < 3600) return ((int)(s/60)) + "min " + ((int)(s%60)) + "s";
            return ((int)(s/3600)) + "h " + ((int)((s%3600)/60)) + "min";
        }

        private static DateTime RoundUpDateTimeToStep(DateTime dt, int step)
        {
            int total = dt.Hour * 60 + dt.Minute;
            return dt.Date.AddMinutes((int)Math.Ceiling((double)total / step) * step);
        }

        public static UIElement BuildLegend()
        {
            var panel = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(4, 2, 4, 4) };
            var items = new[] { (MessageType.UNIT_INFO, "Unit Info"), (MessageType.NEXT_OPERATION, "Next Op"), (MessageType.UNIT_CHECKIN, "Checkin"), (MessageType.UNIT_RESULT, "Result"), (MessageType.LOAD_MATERIAL, "Material"), (MessageType.REQ_MATERIAL_INFO, "Mat Info"), (MessageType.REQ_SETUP_CHANGE2, "Setup"), (MessageType.SEMI_VALIDATION2, "Semi Val 2") };
            foreach (var (t, lbl) in items)
            {
                var item = new StackPanel { Orientation = Orientation.Horizontal };
                item.Children.Add(new Rectangle { Width = 10, Height = 10, Fill = new SolidColorBrush(GetWpfColorForMessageType(t)), RadiusX = 2, RadiusY = 2, Margin = new Thickness(0, 0, 4, 0), VerticalAlignment = VerticalAlignment.Center });
                item.Children.Add(new TextBlock { Text = lbl, FontSize = 9, Foreground = new SolidColorBrush(Color.FromRgb(100, 110, 120)), Margin = new Thickness(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center });
                panel.Children.Add(item);
            }
            return panel;
        }

        private class TimelineLane
        {
            public MessageType         MessageType    { get; set; }
            public string              TypeName       { get; set; }
            public List<TimelineEvent> Events         { get; set; }
            public bool                IsOtherCategory { get; set; }
        }

        private class TimelineSegment
        {
            public DateTime            From   { get; set; }
            public DateTime            To     { get; set; }
            public List<TimelineEvent> Events { get; set; }
            public bool                IsGap  { get; set; }
        }
    }
}