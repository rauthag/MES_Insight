using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using MESInsight.Core;

namespace MESInsight.Charts.Renderers
{
    public class TimelineChartRenderer : ChartRenderer
    {
        private const double BarMaxH         = 52;
        private const double BarMinH         = 10;
        private const double CanvasH         = BarMaxH + 16;
        private const double AxisH           = 18;
        private const double TitleH          = 28;
        private const double MargH           = 4;
        private const double BarW            = 6;
        private const double BarWErr         = 9;
        private const int    GapThresholdMin = 10;
        private const double GapPixels       = 10;
        private const int    MaxLanes        = 3;

        private readonly Action<List<ResponseRecord>, string> _onErrorClicked;

        public TimelineChartRenderer(Action<List<ResponseRecord>, string> onErrorClicked = null)
        {
            _onErrorClicked = onErrorClicked;
        }

        public override ChartType GetChartType() => ChartType.Timeline;
        public override int GetMinimumHeight(RenderContext context) => (int)(TitleH + CanvasH + AxisH + 4);

        public override UIElement Render(ChartData data, RenderContext context)
        {
            if (data?.TimelineEvents == null || data.TimelineEvents.Count == 0) return null;

            var day    = data.FilteredRecords?.Count > 0 ? data.FilteredRecords[0].TimestampParsed.Date : DateTime.Today;
            var events = data.TimelineEvents.Where(e => e.Start.Date == day.Date).OrderBy(e => e.Start).ToList();
            if (events.Count == 0) return null;

            var errors  = events.Where(e => e.EventType == TimelineEventType.Error).ToList();
            int maxRt   = data.MaxResponseTime > 0 ? data.MaxResponseTime : 1;
            int numRows = events.Count <= 60 ? 1 : events.Count <= 200 ? 2 : 3;

            var segments = BuildCompressedSegments(events);
            var rows     = SplitSegmentsIntoRows(segments, numRows);

            var outer = new Border
            {
                Background      = new SolidColorBrush(Color.FromRgb(13, 17, 23)),
                BorderBrush     = new SolidColorBrush(Color.FromRgb(22, 60, 36)),
                BorderThickness = new Thickness(1),
                CornerRadius    = new CornerRadius(6),
                Margin          = new Thickness(0, 4, 0, 0)
            };

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(TitleH) });
            var titleRow = BuildTitleRow(day, errors);
            Grid.SetRow(titleRow, 0);
            grid.Children.Add(titleRow);

            int nextRowIdx = 1;
            for (int ri = 0; ri < rows.Count; ri++)
            {
                if (ri > 0)
                {
                    grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(3) });
                    var div = new Border { Height = 1, Background = new SolidColorBrush(Color.FromRgb(30, 36, 44)), Margin = new Thickness(MargH, 1, MargH, 1) };
                    Grid.SetRow(div, nextRowIdx);
                    grid.Children.Add(div);
                    nextRowIdx++;
                }
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(CanvasH) });
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(AxisH) });
                AddRow(grid, rows[ri], maxRt, nextRowIdx, nextRowIdx + 1);
                nextRowIdx += 2;
            }

            outer.Child = grid;
            return outer;
        }

        private class Seg
        {
            public DateTime            From   { get; set; }
            public DateTime            To     { get; set; }
            public List<TimelineEvent> Events { get; set; }
            public bool                IsGap  { get; set; }
        }

        private static List<Seg> BuildCompressedSegments(List<TimelineEvent> events)
        {
            var result  = new List<Seg>();
            var groups  = new List<List<TimelineEvent>>();
            var current = new List<TimelineEvent> { events[0] };

            for (int i = 1; i < events.Count; i++)
            {
                if ((events[i].Start - events[i - 1].Start).TotalMinutes > GapThresholdMin)
                { groups.Add(current); current = new List<TimelineEvent>(); }
                current.Add(events[i]);
            }
            groups.Add(current);

            for (int g = 0; g < groups.Count; g++)
            {
                var grp  = groups[g];
                var from = grp.First().Start.AddMinutes(-1);
                var to   = grp.Last().Start.AddMinutes(1);
                result.Add(new Seg { From = from, To = to, Events = grp, IsGap = false });
                if (g < groups.Count - 1)
                    result.Add(new Seg { From = to, To = groups[g + 1].First().Start.AddMinutes(-1), Events = new List<TimelineEvent>(), IsGap = true });
            }
            return result;
        }

        private static List<List<Seg>> SplitSegmentsIntoRows(List<Seg> segments, int numRows)
        {
            var rows = new List<List<Seg>>();
            if (numRows == 1) { rows.Add(segments); return rows; }

            int dataSeg = segments.Count(s => !s.IsGap);
            int perRow  = (int)Math.Ceiling((double)dataSeg / numRows);
            var cur     = new List<Seg>();
            int count   = 0;

            foreach (var seg in segments)
            {
                cur.Add(seg);
                if (!seg.IsGap) count++;
                if (count >= perRow && rows.Count < numRows - 1)
                { rows.Add(cur); cur = new List<Seg>(); count = 0; }
            }
            if (cur.Count > 0) rows.Add(cur);
            while (rows.Count < numRows) rows.Add(new List<Seg>());
            return rows;
        }

        private void AddRow(Grid grid, List<Seg> segs, int maxRt, int barIdx, int axisIdx)
        {
            var barCanvas  = new Canvas { Height = CanvasH, ClipToBounds = true, Background = new SolidColorBrush(Color.FromRgb(18, 23, 30)) };
            var axisCanvas = new Canvas { Height = AxisH };
            Grid.SetRow(barCanvas,  barIdx);
            Grid.SetRow(axisCanvas, axisIdx);
            grid.Children.Add(barCanvas);
            grid.Children.Add(axisCanvas);

            double zoomStart = -1, zoomEnd = -1;
            bool   zoomed    = false;

            void Redraw() => DrawRow(barCanvas, axisCanvas, segs, maxRt, barCanvas.ActualWidth,
                zoomed ? zoomStart : -1, zoomed ? zoomEnd : -1,
                () => { zoomed = false; zoomStart = -1; zoomEnd = -1; barCanvas.Tag = null; });

            barCanvas.Loaded      += (s, e) => { if (barCanvas.ActualWidth > 0) Redraw(); };
            barCanvas.SizeChanged += (s, e) => { barCanvas.Children.Clear(); axisCanvas.Children.Clear(); Redraw(); };

            barCanvas.MouseRightButtonUp += (s, e) =>
            {
                if (!zoomed) return;
                zoomed = false; zoomStart = -1; zoomEnd = -1;
                barCanvas.Children.Clear(); axisCanvas.Children.Clear();
                Redraw();
            };

            barCanvas.MouseWheel += (s, e) =>
            {
                e.Handled = true;
                var allEvts = segs.Where(sg => !sg.IsGap).SelectMany(sg => sg.Events).OrderBy(ev => ev.Start).ToList();
                if (allEvts.Count == 0) return;
                double totalS = (allEvts.Last().Start - allEvts.First().Start).TotalSeconds;
                if (totalS <= 0) return;

                double center  = zoomed ? (zoomStart + zoomEnd) / 2 : totalS / 2;
                double current = zoomed ? (zoomEnd - zoomStart) : totalS;
                double newSpan = Math.Max(30, Math.Min(totalS, current * (e.Delta > 0 ? 0.6 : 1.6)));

                zoomStart = Math.Max(0, center - newSpan / 2);
                zoomEnd   = Math.Min(totalS, center + newSpan / 2);
                zoomed    = true;

                barCanvas.Children.Clear(); axisCanvas.Children.Clear();
                Redraw();
            };

            barCanvas.MouseLeftButtonUp += (s, e) =>
            {
                if (e.Handled) return;
                double rel = (e.GetPosition(barCanvas).X - MargH) / (barCanvas.ActualWidth - MargH * 2);
                if (rel < 0 || rel > 1) return;

                var allEvts = segs.Where(sg => !sg.IsGap).SelectMany(sg => sg.Events).OrderBy(ev => ev.Start).ToList();
                if (allEvts.Count == 0) return;
                DateTime minT   = allEvts.First().Start;
                double   totalS = (allEvts.Last().Start - minT).TotalSeconds;
                if (totalS <= 0) return;

                var nearest    = allEvts.OrderBy(ev => Math.Abs((ev.Start - minT.AddSeconds(rel * totalS)).TotalSeconds)).First();
                double nSec    = (nearest.Start - minT).TotalSeconds;
                double winSec  = Math.Max(60, totalS * 0.20);
                double wStart  = nSec - winSec / 2;
                double wEnd    = nSec + winSec / 2;

                int attempts = 0;
                while (attempts++ < 10)
                {
                    if (allEvts.Count(ev => { double s2 = (ev.Start - minT).TotalSeconds; return s2 >= wStart && s2 <= wEnd; }) >= Math.Min(5, allEvts.Count)) break;
                    winSec *= 1.5; wStart = nSec - winSec / 2; wEnd = nSec + winSec / 2;
                }

                zoomStart = Math.Max(0, wStart);
                zoomEnd   = Math.Min(totalS, wEnd);
                zoomed    = true;
                barCanvas.Tag = nSec;

                barCanvas.Children.Clear(); axisCanvas.Children.Clear();
                Redraw();
            };
        }

        private void DrawRow(
            Canvas barCanvas, Canvas axisCanvas,
            List<Seg> segs, int maxRt, double totalWidth,
            double zoomFrom, double zoomTo,
            Action onUnzoom = null)
        {
            double usableW       = totalWidth - MargH * 2;
            double gapTotalW     = segs.Count(s => s.IsGap) * GapPixels;
            double dataW         = usableW - gapTotalW;
            double totalDataSpan = segs.Where(s => !s.IsGap).Sum(s => (s.To - s.From).TotalSeconds);
            if (totalDataSpan <= 0) return;

            bool isZoomed = zoomFrom >= 0 && zoomTo > zoomFrom;

            DrawBackground(barCanvas, usableW);

            var allEvtSecs = ComputeAbsoluteSeconds(segs);
            var (effFrom, effTo) = ResolveEffectiveZoom(allEvtSecs, totalDataSpan, isZoomed, zoomFrom, zoomTo);
            var positioned = ComputePositions(segs, allEvtSecs, totalDataSpan, dataW, usableW, isZoomed, effFrom, effTo);

            DrawConnectors(barCanvas, positioned, maxRt);
            DrawBars(barCanvas, positioned, maxRt);
            DrawZoomOverlay(barCanvas, axisCanvas, segs, maxRt, isZoomed, effFrom, effTo, totalDataSpan, onUnzoom, totalWidth);
            DrawAxis(axisCanvas, segs, allEvtSecs, totalDataSpan, dataW, usableW, isZoomed, effFrom, effTo);
        }

        private static void DrawBackground(Canvas barCanvas, double usableW)
        {
            var bg = new Rectangle { Width = usableW, Height = CanvasH, Fill = new SolidColorBrush(Color.FromRgb(22, 28, 36)), RadiusX = 2, RadiusY = 2 };
            Canvas.SetLeft(bg, MargH); Canvas.SetTop(bg, 0);
            barCanvas.Children.Add(bg);
            barCanvas.Children.Add(new Line { X1 = MargH, Y1 = CanvasH - 1, X2 = MargH + usableW, Y2 = CanvasH - 1, Stroke = new SolidColorBrush(Color.FromRgb(35, 42, 52)), StrokeThickness = 1 });
        }

        private static List<(TimelineEvent evt, double absSec)> ComputeAbsoluteSeconds(List<Seg> segs)
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

        private static (double effFrom, double effTo) ResolveEffectiveZoom(
            List<(TimelineEvent evt, double absSec)> allEvtSecs, double totalDataSpan,
            bool isZoomed, double zoomFrom, double zoomTo)
        {
            if (!isZoomed) return (zoomFrom, zoomTo);

            double center  = (zoomFrom + zoomTo) / 2;
            double span    = Math.Max(zoomTo - zoomFrom, 30);
            double effFrom = Math.Max(0, center - span / 2);
            double effTo   = Math.Min(totalDataSpan, center + span / 2);
            int    tries   = 0;

            while (tries++ < 12)
            {
                if (allEvtSecs.Count(x => x.absSec >= effFrom && x.absSec <= effTo) >= Math.Min(3, allEvtSecs.Count)) break;
                span   *= 1.6;
                effFrom = Math.Max(0, center - span / 2);
                effTo   = Math.Min(totalDataSpan, center + span / 2);
            }

            if (effFrom >= effTo) effTo = Math.Min(totalDataSpan, effFrom + 60);
            return (effFrom, effTo);
        }

        private static List<(TimelineEvent evt, double xPos)> ComputePositions(
            List<Seg> segs, List<(TimelineEvent evt, double absSec)> allEvtSecs,
            double totalDataSpan, double dataW, double usableW,
            bool isZoomed, double effFrom, double effTo)
        {
            var    result     = new List<(TimelineEvent, double)>();
            double xCursor    = MargH;

            foreach (var seg in segs)
            {
                if (seg.IsGap) { xCursor += GapPixels; continue; }
                double segSec = (seg.To - seg.From).TotalSeconds;
                double segW   = segSec / totalDataSpan * dataW;

                foreach (var evt in seg.Events)
                {
                    double xOff = segSec > 0 ? (evt.Start - seg.From).TotalSeconds / segSec * segW : 0;
                    double xPos = xCursor + xOff;
                    if (isZoomed)
                    {
                        double abs = allEvtSecs.FirstOrDefault(x => x.evt == evt).absSec;
                        if (abs < effFrom || abs > effTo) continue;
                        xPos = MargH + (abs - effFrom) / (effTo - effFrom) * usableW;
                    }
                    result.Add((evt, xPos));
                }
                xCursor += segW;
            }
            return result;
        }

        private static void DrawConnectors(Canvas canvas, List<(TimelineEvent evt, double xPos)> positioned, int maxRt)
        {
            double laneH = (CanvasH - 4) / MaxLanes;
            var checkinPos = new Dictionary<string, (double x, double y)>();

            for (int i = 0; i < positioned.Count; i++)
            {
                var (evt, xPos) = positioned[i];
                double ratio = maxRt > 0 ? Math.Min(1.0, (double)evt.ResponseTimeMs / maxRt) : 0.5;
                double barH  = BarMinH + ratio * (BarMaxH - BarMinH);
                double laneB = CanvasH - 2 - AssignLane(positioned, i) * (laneH + 1);
                double yMid  = laneB - barH / 2;

                if (evt.MessageKind == MessageType.UNIT_CHECKIN && !string.IsNullOrEmpty(evt.Uid))
                    checkinPos[evt.Uid] = (xPos, yMid);
                else if (evt.MessageKind == MessageType.UNIT_RESULT && !string.IsNullOrEmpty(evt.Uid)
                    && checkinPos.TryGetValue(evt.Uid, out var ci))
                {
                    canvas.Children.Add(new Line { X1 = ci.x, Y1 = ci.y, X2 = xPos, Y2 = yMid, Stroke = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)), StrokeThickness = 1, StrokeDashArray = new DoubleCollection { 3, 3 } });
                    checkinPos.Remove(evt.Uid);
                }
            }
        }

        private static int AssignLane(List<(TimelineEvent evt, double xPos)> positioned, int idx)
        {
            var laneEndX = new double[MaxLanes];
            for (int i = 0; i <= idx; i++)
            {
                double xPos = positioned[i].xPos;
                int lane = MaxLanes - 1;
                for (int l = 0; l < MaxLanes; l++)
                {
                    if (laneEndX[l] <= xPos - BarW - 1) { lane = l; break; }
                }
                laneEndX[lane] = xPos + BarW + 1;
                if (i == idx) return lane;
            }
            return 0;
        }

        private void DrawBars(Canvas canvas, List<(TimelineEvent evt, double xPos)> positioned, int maxRt)
        {
            double laneH     = (CanvasH - 4) / MaxLanes;
            var    laneEndX  = new double[MaxLanes];

            for (int i = 0; i < positioned.Count; i++)
            {
                var (evt, xPos) = positioned[i];
                double ratio = maxRt > 0 ? Math.Min(1.0, (double)evt.ResponseTimeMs / maxRt) : 0.5;
                double barH  = BarMinH + ratio * (BarMaxH - BarMinH);
                int    lane  = 0;
                for (int l = 0; l < MaxLanes; l++) { if (laneEndX[l] <= xPos - BarW - 1) { lane = l; break; } if (l == MaxLanes - 1) lane = l; }
                laneEndX[lane] = xPos + BarW + 1;
                double laneB = CanvasH - 2 - lane * (laneH + 1);
                double yTop  = laneB - barH;
                bool   isErr = evt.EventType == TimelineEventType.Error;
                Color  col   = isErr ? Color.FromRgb(220, 30, 30) : GetMessageColor(evt.MessageKind);
                double bw    = isErr ? BarWErr : BarW;

                var bar = new Rectangle
                {
                    Width = bw, Height = barH,
                    Fill  = new SolidColorBrush(Color.FromArgb(isErr ? (byte)255 : (byte)220, col.R, col.G, col.B)),
                    RadiusX = 1, RadiusY = 1,
                    ToolTip = BuildTooltip(evt),
                    Cursor  = System.Windows.Input.Cursors.Hand
                };
                Canvas.SetLeft(bar, xPos - bw / 2); Canvas.SetTop(bar, yTop);
                canvas.Children.Add(bar);

                if (isErr)
                {
                    var captured = evt;
                    bar.MouseLeftButtonUp += (s, e) => { e.Handled = true; FireErrorClicked(captured); };
                }
            }
        }

        private static void DrawZoomOverlay(
            Canvas barCanvas, Canvas axisCanvas,
            List<Seg> segs, int maxRt,
            bool isZoomed, double effFrom, double effTo, double totalDataSpan,
            Action onUnzoom, double totalWidth)
        {
            if (!isZoomed)
            {
                barCanvas.Children.Add(new TextBlock { Text = "click = zoom  ·  scroll = zoom in/out", FontSize = 8, Foreground = new SolidColorBrush(Color.FromArgb(70, 150, 160, 150)), Margin = new Thickness(MargH + 2, 2, 0, 0) });
                return;
            }

            var allEvts = segs.Where(sg => !sg.IsGap).SelectMany(sg => sg.Events).OrderBy(ev => ev.Start).ToList();
            DateTime minTZ = allEvts.Count > 0 ? allEvts.First().Start : DateTime.MinValue;
            DateTime fromDt = minTZ.AddSeconds(effFrom);
            DateTime toDt   = minTZ.AddSeconds(effTo);
            string   range  = fromDt.ToString("HH:mm:ss") + " – " + toDt.ToString("HH:mm:ss");

            var resetBtn = new Border
            {
                Background      = new SolidColorBrush(Color.FromArgb(200, 22, 40, 28)),
                BorderBrush     = new SolidColorBrush(Color.FromArgb(180, 63, 185, 80)),
                BorderThickness = new Thickness(1),
                CornerRadius    = new CornerRadius(4),
                Padding         = new Thickness(8, 2, 8, 2),
                Cursor          = System.Windows.Input.Cursors.Hand,
                Child           = new TextBlock { Text = "⟲  Reset view", FontSize = 9, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Color.FromRgb(63, 185, 80)) }
            };
            Canvas.SetLeft(resetBtn, MargH + 2); Canvas.SetTop(resetBtn, 2);
            barCanvas.Children.Add(resetBtn);
            resetBtn.MouseLeftButtonUp += (s2, e2) => { e2.Handled = true; onUnzoom?.Invoke(); };

            var rangeLabel = new TextBlock { Text = range, FontSize = 9, Foreground = new SolidColorBrush(Color.FromArgb(180, 63, 185, 80)) };
            Canvas.SetLeft(rangeLabel, totalWidth - 150 - MargH); Canvas.SetTop(rangeLabel, 4);
            barCanvas.Children.Add(rangeLabel);

            if (barCanvas.Tag is double clickedSec && clickedSec >= effFrom && clickedSec <= effTo)
            {
                double usableW2 = totalWidth - MargH * 2;
                double xHl      = MargH + (clickedSec - effFrom) / (effTo - effFrom) * usableW2;
                var highlight   = new Line
                {
                    X1 = xHl, Y1 = 0, X2 = xHl, Y2 = CanvasH,
                    Stroke          = new SolidColorBrush(Color.FromArgb(120, 63, 185, 80)),
                    StrokeThickness = 1.5,
                    StrokeDashArray = new DoubleCollection { 4, 3 }
                };
                barCanvas.Children.Add(highlight);

                var hlDot = new Ellipse { Width = 8, Height = 8, Fill = new SolidColorBrush(Color.FromArgb(180, 63, 185, 80)) };
                Canvas.SetLeft(hlDot, xHl - 4); Canvas.SetTop(hlDot, CanvasH / 2 - 4);
                barCanvas.Children.Add(hlDot);
            }
        }

        private static void DrawAxis(
            Canvas axisCanvas, List<Seg> segs,
            List<(TimelineEvent evt, double absSec)> allEvtSecs,
            double totalDataSpan, double dataW, double usableW,
            bool isZoomed, double effFrom, double effTo)
        {
            double lastLabelX = double.MinValue;
            const double MinSpacing = 38;
            double xCursor = MargH;
            double secAcc  = 0;

            foreach (var seg in segs)
            {
                if (seg.IsGap) { xCursor += GapPixels; continue; }
                double segSec = (seg.To - seg.From).TotalSeconds;
                double segW   = totalDataSpan > 0 ? segSec / totalDataSpan * dataW : 0;
                int    spanMin = (int)(seg.To - seg.From).TotalMinutes;
                int    step   = spanMin <= 5 ? 1 : spanMin <= 15 ? 2 : spanMin <= 30 ? 5 : spanMin <= 90 ? 15 : spanMin <= 240 ? 30 : 60;

                DateTime cursor = RoundUpToStep(seg.From, step);
                while (cursor <= seg.To)
                {
                    double abs = secAcc + (cursor - seg.From).TotalSeconds;
                    double x   = isZoomed
                        ? (abs >= effFrom && abs <= effTo ? MargH + (abs - effFrom) / (effTo - effFrom) * usableW : -999)
                        : xCursor + (segSec > 0 ? (cursor - seg.From).TotalSeconds / segSec * segW : 0);

                    if (x >= MargH && x - lastLabelX >= MinSpacing)
                    {
                        axisCanvas.Children.Add(new Line { X1 = x, Y1 = 0, X2 = x, Y2 = 5, Stroke = new SolidColorBrush(Color.FromRgb(50, 60, 70)), StrokeThickness = 1 });
                        axisCanvas.Children.Add(new TextBlock { Text = cursor.ToString("HH:mm"), FontSize = 9, Foreground = new SolidColorBrush(Color.FromRgb(90, 100, 110)), Margin = new Thickness(x - 12, 5, 0, 0) });
                        lastLabelX = x;
                    }
                    cursor = cursor.AddMinutes(step);
                }
                xCursor += segW;
                secAcc  += segSec;
            }
        }

        private void FireErrorClicked(TimelineEvent evt)
        {
            var recs = evt.SourceRecord != null ? new List<ResponseRecord> { evt.SourceRecord } : new List<ResponseRecord>();
            _onErrorClicked?.Invoke(recs, (evt.ErrorCode ?? "Error") + "  —  " + evt.Start.ToString("HH:mm:ss"));
        }

        private Grid BuildTitleRow(DateTime day, List<TimelineEvent> errorEvts)
        {
            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
            row.Children.Add(new TextBlock { Text = "Timeline  —  " + day.ToString("dd.MM.yyyy"), FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Color.FromRgb(63, 185, 80)), Margin = new Thickness(10, 3, 0, 0), VerticalAlignment = VerticalAlignment.Center });

            if (errorEvts.Count > 0)
            {
                var badge = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(60, 14, 14)), BorderBrush = new SolidColorBrush(Color.FromRgb(180, 50, 50)),
                    BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4), Padding = new Thickness(7, 2, 7, 2),
                    Margin = new Thickness(0, 3, 8, 3), VerticalAlignment = VerticalAlignment.Center, Cursor = System.Windows.Input.Cursors.Hand,
                    Child = new TextBlock { Text = "⚠  " + errorEvts.Count + (errorEvts.Count == 1 ? " error" : " errors"), FontSize = 9, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Color.FromRgb(248, 81, 73)) }
                };
                badge.MouseLeftButtonUp += (s, e) => { var recs = errorEvts.Where(ev => ev.SourceRecord != null).Select(ev => ev.SourceRecord).ToList(); _onErrorClicked?.Invoke(recs, "Errors  —  " + day.ToString("dd.MM.yyyy")); };
                Grid.SetColumn(badge, 1);
                row.Children.Add(badge);
            }
            return row;
        }

        private static ToolTip BuildTooltip(TimelineEvent evt)
        {
            var panel  = new StackPanel { Margin = new Thickness(4) };
            bool isErr = evt.EventType == TimelineEventType.Error;

            panel.Children.Add(new TextBlock { Text = isErr ? "ERROR  —  " + evt.Label : evt.Label, FontSize = 11, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(isErr ? Color.FromRgb(255, 60, 60) : GetMessageColor(evt.MessageKind)), TextWrapping = TextWrapping.Wrap, MaxWidth = 360 });
            panel.Children.Add(new TextBlock { Text = evt.Start.ToString("HH:mm:ss.fff"), FontSize = 10, Foreground = new SolidColorBrush(Color.FromRgb(140, 160, 150)), Margin = new Thickness(0, 2, 0, 0) });

            if (isErr && !string.IsNullOrEmpty(evt.ErrorCode))
                panel.Children.Add(new TextBlock { Text = evt.ErrorCode, FontSize = 10, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Color.FromRgb(255, 140, 120)), Margin = new Thickness(0, 4, 0, 0), TextWrapping = TextWrapping.Wrap, MaxWidth = 360 });

            if (!string.IsNullOrEmpty(evt.Detail))
                panel.Children.Add(new TextBlock { Text = evt.Detail, FontSize = 10, Foreground = new SolidColorBrush(isErr ? Color.FromRgb(200, 160, 150) : Colors.LightGray), Margin = new Thickness(0, 3, 0, 0), TextWrapping = TextWrapping.Wrap, MaxWidth = 360 });

            if (isErr)
                panel.Children.Add(new TextBlock { Text = "Click to view in records panel", FontSize = 9, Foreground = new SolidColorBrush(Color.FromRgb(100, 110, 120)), Margin = new Thickness(0, 5, 0, 0) });

            return new ToolTip { Content = panel, Background = new SolidColorBrush(Color.FromArgb(248, 18, 22, 30)), Foreground = Brushes.White, BorderBrush = new SolidColorBrush(isErr ? Color.FromRgb(220, 40, 40) : Color.FromRgb(56, 139, 253)), BorderThickness = new Thickness(1), Padding = new Thickness(10) };
        }

        internal static Color GetMessageColor(MessageType type)
        {
            switch (type)
            {
                case MessageType.UNIT_INFO:         return Color.FromRgb(52,  152, 219);
                case MessageType.NEXT_OPERATION:    return Color.FromRgb(26,  188, 156);
                case MessageType.UNIT_CHECKIN:      return Color.FromRgb(230, 140,  30);
                case MessageType.UNIT_RESULT:       return Color.FromRgb(50,  220,  80);
                case MessageType.LOAD_MATERIAL:     return Color.FromRgb(200,  80, 200);
                case MessageType.REQ_MATERIAL_INFO: return Color.FromRgb(155,  89, 182);
                case MessageType.REQ_SETUP_CHANGE2: return Color.FromRgb(231,  76, 120);
                default:                            return Color.FromRgb(100, 110, 120);
            }
        }

        private static DateTime RoundUpToStep(DateTime dt, int stepMin)
        {
            int total   = dt.Hour * 60 + dt.Minute;
            int rounded = (int)Math.Ceiling((double)total / stepMin) * stepMin;
            return dt.Date.AddMinutes(rounded);
        }

        public static UIElement BuildLegend()
        {
            var panel = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(MargH, 2, MargH, 4) };
            var types = new[] { (MessageType.UNIT_INFO, "Unit Info"), (MessageType.NEXT_OPERATION, "Next Op"), (MessageType.UNIT_CHECKIN, "Checkin"), (MessageType.UNIT_RESULT, "Result"), (MessageType.LOAD_MATERIAL, "Material"), (MessageType.REQ_MATERIAL_INFO, "Mat Info"), (MessageType.REQ_SETUP_CHANGE2, "Setup") };
            foreach (var (type, label) in types)
            {
                var item = new StackPanel { Orientation = Orientation.Horizontal };
                item.Children.Add(new Ellipse { Width = 7, Height = 7, Fill = new SolidColorBrush(GetMessageColor(type)), Margin = new Thickness(0, 0, 4, 0), VerticalAlignment = VerticalAlignment.Center });
                item.Children.Add(new TextBlock { Text = label, FontSize = 9, Foreground = new SolidColorBrush(Color.FromRgb(100, 110, 120)), Margin = new Thickness(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center });
                panel.Children.Add(item);
            }
            return panel;
        }
    }
}