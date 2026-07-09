using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using MESInsight.Core;

namespace MESInsight.UI
{
    public static class SubsetHistoryTab
    {
        private static readonly Color CBack = Color.FromRgb(13, 17, 23);
        private static readonly Color CBorder = Color.FromRgb(36, 42, 52);
        private static readonly Color CText = Color.FromRgb(201, 209, 217);
        private static readonly Color CDim = Color.FromRgb(100, 110, 130);
        private static readonly Color CGreen = Color.FromRgb(63, 185, 80);
        private static readonly Color CRed = Color.FromRgb(248, 81, 73);
        private static readonly Color CArrow = Color.FromRgb(150, 175, 210);

        private const double ChevH = 52;
        private const double ChevPt = 16;
        private const double MiniChevH = 58;
        private const double MiniChevPt = 12;
        private const double VGap = 28;
        private const double HGap = 48;
        private const double StationH = 30;
        private const double DayLineH = 28;
        private const double MarginH = 32;

        private static readonly Color[] StationPalette = new[]
        {
            Color.FromRgb(200, 160, 60),
            Color.FromRgb(56, 182, 255),
            Color.FromRgb(155, 89, 182),
            Color.FromRgb(63, 185, 80),
            Color.FromRgb(255, 100, 60),
            Color.FromRgb(40, 220, 200),
            Color.FromRgb(255, 220, 40),
            Color.FromRgb(255, 70, 130)
        };

        private sealed class SubsetViewState
        {
            public string Uid;
            public List<ResponseRecord> Records;
            public Canvas DiagramCanvas;
            public Action<string> OnOpenUid;
            public Dictionary<string, string> EquipNames;
            public TextBlock ScanStatusText;
            public TextBlock FileSubText;
            public Canvas ScanHexStreamCanvas;
            public WrapPanel FoundStationsPanel;
            public TextBlock FoundStationsLabel;
            public HashSet<string> FoundStations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            public System.Windows.Threading.DispatcherTimer AnimationTimer;
            public double HexStreamPhase;
            public int BurstTicks;
        }

        private sealed class LoadingViewState
        {
            public TextBlock StationText;
            public TextBlock FileSubText;
            public Canvas HexCanvas;
            public System.Windows.Threading.DispatcherTimer AnimationTimer;
            public double HexStreamPhase;
        }


        public static UIElement BuildLoading(string uid)
        {
            var root = new Grid { Background = new SolidColorBrush(CBack) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var header = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(10, 14, 20)),
                BorderBrush = new SolidColorBrush(CBorder),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(16, 14, 16, 12)
            };

            var vstack = new StackPanel { Orientation = Orientation.Vertical };
            var titleRow = new StackPanel { Orientation = Orientation.Horizontal };
            titleRow.Children.Add(new TextBlock
            {
                Text = uid,
                FontSize = 22,
                FontWeight = FontWeights.SemiBold,
                FontFamily = new FontFamily("Consolas"),
                Foreground = new SolidColorBrush(Color.FromRgb(88, 226, 124))
            });
            titleRow.Children.Add(Chip("SUBSET HISTORY", Color.FromRgb(88, 226, 124), 11));
            vstack.Children.Add(titleRow);

            var statusRow = new StackPanel
                { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
            var hexCanvas = new Canvas
            {
                Width = 176, Height = 28, Margin = new Thickness(0, 0, 12, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            statusRow.Children.Add(hexCanvas);

            var statusStack = new StackPanel
                { Orientation = Orientation.Vertical, VerticalAlignment = VerticalAlignment.Center };
            var stationText = new TextBlock
            {
                Text = "Initializing...",
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                FontFamily = new FontFamily("Consolas"),
                Foreground = new SolidColorBrush(Color.FromRgb(144, 228, 162))
            };
            var fileSubText = new TextBlock
            {
                Text = "",
                FontSize = 9,
                FontFamily = new FontFamily("Consolas"),
                Foreground = new SolidColorBrush(Color.FromRgb(80, 110, 90)),
                Margin = new Thickness(0, 2, 0, 0)
            };
            statusStack.Children.Add(stationText);
            statusStack.Children.Add(fileSubText);
            statusRow.Children.Add(statusStack);
            vstack.Children.Add(statusRow);

            header.Child = vstack;
            root.Children.Add(header);
            Grid.SetRow(header, 0);

            var loadingState = new LoadingViewState
            {
                StationText = stationText,
                FileSubText = fileSubText,
                HexCanvas = hexCanvas,
                AnimationTimer = new System.Windows.Threading.DispatcherTimer
                    { Interval = TimeSpan.FromMilliseconds(70) },
                HexStreamPhase = 0.0
            };
            loadingState.AnimationTimer.Tick += (s, e) =>
            {
                loadingState.HexStreamPhase += 0.18;
                if (loadingState.HexStreamPhase > 100) loadingState.HexStreamPhase -= 100;
                RenderHexStreamFrame(loadingState.HexCanvas, loadingState.HexStreamPhase, false);
            };
            loadingState.AnimationTimer.Start();
            RenderHexStreamFrame(loadingState.HexCanvas, 0.0, false);

            root.Unloaded += (s, e) => loadingState.AnimationTimer?.Stop();
            root.Tag = loadingState;
            return root;
        }

        public static void UpdateLoadingStation(UIElement panel, string name)
        {
            if (panel is Grid g && g.Tag is LoadingViewState st)
                st.StationText.Text = string.IsNullOrWhiteSpace(name) ? "Scanning..." : name;
        }

        public static void UpdateLoadingFile(UIElement panel, string fileName)
        {
            if (panel is Grid g && g.Tag is LoadingViewState st)
                st.FileSubText.Text = fileName ?? "";
        }

        public static UIElement Build(
            string uid, List<ResponseRecord> records,
            Action<string> onOpenUid = null,
            Action<string> onSwitchStation = null,
            Func<Action<string>, List<ResponseRecord>> onScanFullLine = null,
            Dictionary<string, string> equipNames = null)
        {
            var root = new Grid { Background = new SolidColorBrush(CBack) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var safeRecords = DeduplicateRecords(records ?? new List<ResponseRecord>());
            Canvas diagramCanvas = null;

            TextBlock scanStatus;
            TextBlock fileSubText;
            TextBlock foundLabel;
            WrapPanel foundStations;
            Canvas hexStream;
            var header = BuildHeader(uid, safeRecords, onScanFullLine, equipNames, newRecs =>
            {
                safeRecords = DeduplicateRecords(newRecs ?? new List<ResponseRecord>());
                if (diagramCanvas != null)
                    AnimateDiagramRefresh(diagramCanvas, safeRecords, onOpenUid, equipNames);
            }, out scanStatus, out fileSubText, out foundLabel, out foundStations, out hexStream);

            root.Children.Add(header);
            Grid.SetRow(root.Children[0], 0);

            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                Background = new SolidColorBrush(CBack)
            };
            diagramCanvas = new Canvas { Background = new SolidColorBrush(CBack) };
            scroll.Content = diagramCanvas;
            Grid.SetRow(scroll, 1);
            root.Children.Add(scroll);

            diagramCanvas.Loaded += (s, e) => RenderDiagram(diagramCanvas, safeRecords, onOpenUid, equipNames);
            scroll.SizeChanged += (s, e) => RenderDiagram(diagramCanvas, safeRecords, onOpenUid, equipNames);

            var state = new SubsetViewState
            {
                Uid = uid,
                Records = safeRecords,
                DiagramCanvas = diagramCanvas,
                OnOpenUid = onOpenUid,
                EquipNames = equipNames,
                ScanStatusText = scanStatus,
                FileSubText = fileSubText,
                ScanHexStreamCanvas = hexStream,
                FoundStationsPanel = foundStations,
                FoundStationsLabel = foundLabel,
                AnimationTimer = new System.Windows.Threading.DispatcherTimer
                    { Interval = TimeSpan.FromMilliseconds(70) },
                HexStreamPhase = 0.0
            };

            state.AnimationTimer.Tick += (s, e) =>
            {
                state.HexStreamPhase += 0.18;
                if (state.HexStreamPhase > 100) state.HexStreamPhase -= 100;
                RenderHexStreamFrame(state.ScanHexStreamCanvas, state.HexStreamPhase, state.BurstTicks > 0);
                if (state.BurstTicks > 0) state.BurstTicks--;
            };
            state.AnimationTimer.Start();
            RenderHexStreamFrame(state.ScanHexStreamCanvas, 0.0, false);

            root.Unloaded += (s, e) => state.AnimationTimer?.Stop();
            root.Tag = state;
            return root;
        }

        public static void UpdateBackgroundScanStatus(UIElement panel, string stationName, string uid, int scanned,
            int total)
        {
            if (!(panel is Grid root) || !(root.Tag is SubsetViewState state)) return;
            state.ScanStatusText.Text = string.IsNullOrWhiteSpace(stationName) ? "Scanning..." : stationName;
            state.ScanStatusText.Foreground = new SolidColorBrush(Color.FromRgb(144, 228, 162));
            if (state.AnimationTimer != null && !state.AnimationTimer.IsEnabled)
                state.AnimationTimer.Start();
        }

        public static void UpdateScanFile(UIElement panel, string fileName)
        {
            if (!(panel is Grid root) || !(root.Tag is SubsetViewState state)) return;
            if (state.FileSubText != null) state.FileSubText.Text = fileName ?? "";
        }

        public static void MarkFoundStation(UIElement panel, string stationName, int foundCount)
        {
            if (!(panel is Grid root) || !(root.Tag is SubsetViewState state)) return;
            if (string.IsNullOrWhiteSpace(stationName)) return;
            if (!state.FoundStations.Add(stationName)) return;
            state.BurstTicks = 16;

            if (state.FoundStations.Count == 1 && state.FoundStationsLabel != null)
                state.FoundStationsLabel.Visibility = Visibility.Visible;

            var chip = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(28, 63, 185, 80)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(120, 63, 185, 80)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(9),
                Padding = new Thickness(8, 2, 8, 2),
                Margin = new Thickness(0, 0, 6, 4),
                Child = new TextBlock
                {
                    Text = stationName + "  +" + foundCount,
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(CGreen)
                }
            };
            state.FoundStationsPanel.Children.Add(chip);
        }

        public static void MergeRecordsAndRefresh(UIElement panel, List<ResponseRecord> incoming)
        {
            if (!(panel is Grid root) || !(root.Tag is SubsetViewState state)) return;
            if (incoming == null || incoming.Count == 0) return;

            var merged = state.Records.Concat(incoming).ToList();
            var deduped = DeduplicateRecords(merged);
            if (deduped.Count == state.Records.Count) return;

            state.Records = deduped;
            AnimateDiagramRefresh(state.DiagramCanvas, state.Records, state.OnOpenUid, state.EquipNames);
        }

        public static void CompleteBackgroundScan(UIElement panel, int totalEvents)
        {
            if (!(panel is Grid root) || !(root.Tag is SubsetViewState state)) return;
            state.ScanStatusText.Text = "✓  " + totalEvents + " events found";
            state.ScanStatusText.Foreground = new SolidColorBrush(CGreen);
            state.AnimationTimer?.Stop();
            state.ScanHexStreamCanvas.Visibility = Visibility.Collapsed;
            if (state.FileSubText != null) state.FileSubText.Text = "";
        }


        private static void RenderDiagram(Canvas canvas, List<ResponseRecord> records, Action<string> onOpenUid,
            Dictionary<string, string> equipNames)
        {
            canvas.Children.Clear();

            double availW = Math.Max(500, canvas.ActualWidth > 0 ? canvas.ActualWidth : 900);
            double chevW = Math.Min((availW - MarginH * 2) / 3.0, 240);
            double cx = availW / 2;
            double y = 20;

            var groups = BuildGroups(records);
            var stationColors = AssignStationColors(groups);
            var rows = BuildRows(groups);
            string lastDay = null;
            string lastSt = null;

            var rowBottomY = new List<double>();
            var rowLastCx = new List<double>();
            var timelinePoints = new List<System.Tuple<double, DateTime>>();

            for (int rowIdx = 0; rowIdx < rows.Count; rowIdx++)
            {
                var row = rows[rowIdx];
                var firstGrp = row[0];
                string day = firstGrp.Day;
                string st = firstGrp.Station;

                if (day != lastDay)
                {
                    if (lastDay != null) y += 6;
                    DrawDaySeparator(canvas, y, cx, availW, day);
                    y += DayLineH + 8;
                    lastDay = day;
                }

                if (st != lastSt)
                {
                    Color sc = stationColors.ContainsKey(st) ? stationColors[st] : StationPalette[0];
                    if (lastSt != null && rowBottomY.Count > 0)
                    {
                        double prevBot = rowBottomY[rowBottomY.Count - 1];
                        double gap = y - prevBot;
                        if (gap < 36)
                        {
                            y = prevBot + 36;
                        }

                        DrawStationConnector(canvas, cx, prevBot, y, CStation);
                    }

                    DrawStationBox(canvas, y, cx, availW - MarginH * 2, ResolveStation(st, equipNames), sc);
                    double connTop = y + 38;
                    y += 38 + 28;
                    DrawStationConnector(canvas, cx, connTop, y, CStation);
                    lastSt = st;
                }

                double thisCx;
                if (row.Count == 1 && row[0].IsSemi)
                    thisCx = cx;
                else
                {
                    double rw = row.Count * chevW + (row.Count - 1) * HGap;
                    thisCx = cx - rw / 2 + chevW / 2;
                }

                double thisLastCx;
                if (row.Count == 1 && row[0].IsSemi)
                    thisLastCx = cx;
                else
                {
                    double rw = row.Count * chevW + (row.Count - 1) * HGap;
                    thisLastCx = cx - rw / 2 + (row.Count - 1) * (chevW + HGap) + chevW / 2;
                }

                if (rowIdx > 0 && firstGrp.GapFromPrevSec.HasValue && firstGrp.GapFromPrevSec.Value > 0)
                {
                    double prevCx = rowLastCx[rowIdx - 1];
                    double prevBot = rowBottomY[rowIdx - 1];
                    double gapSec = firstGrp.GapFromPrevSec.Value;

                    if (Math.Abs(prevCx - thisCx) < 8)
                    {
                        double arrowH = GapToArrowH(gapSec);
                        DrawVerticalArrow(canvas, thisCx, prevBot, arrowH, gapSec);
                        y = Math.Max(y, prevBot + arrowH);
                    }
                    else
                    {
                        double minGap = GapToArrowH(gapSec);
                        y = Math.Max(y, prevBot + minGap);
                        DrawBentArrow(canvas, prevCx, prevBot, thisCx, y, gapSec);
                    }
                }

                rowLastCx.Add(thisLastCx);

                if (row.Count == 1 && row[0].IsSemi)
                {
                    var grp = row[0];
                    var parentEl = MakeChevron(grp.ParentNode, chevW, ChevH, ChevPt, onOpenUid, equipNames);
                    Canvas.SetLeft(parentEl, cx - chevW / 2);
                    Canvas.SetTop(parentEl, y);
                    Canvas.SetZIndex(parentEl, 2);
                    canvas.Children.Add(parentEl);
                    y += ChevH;

                    if (grp.Components.Count > 0)
                    {
                        double compVGap = 44;
                        const double S = 60;
                        const double SG = 20;
                        int perRow = Math.Max(1, (int)((availW - MarginH * 2 + SG) / (S + SG)));
                        perRow = Math.Min(perRow, grp.Components.Count);
                        double totalCW = perRow * S + (perRow - 1) * SG;
                        double startX2 = cx - totalCW / 2;
                        double compY = y + compVGap;
                        double splitY = y + compVGap / 2;

                        canvas.Children.Add(new Line
                        {
                            X1 = cx, Y1 = y, X2 = cx, Y2 = splitY,
                            Stroke = new SolidColorBrush(Color.FromArgb(100, CArrow.R, CArrow.G, CArrow.B)),
                            StrokeThickness = 2, IsHitTestVisible = false
                        });

                        for (int i = 0; i < perRow && i < grp.Components.Count; i++)
                        {
                            double compCx2 = startX2 + i * (S + SG) + S / 2;
                            if (Math.Abs(compCx2 - cx) > 2)
                                canvas.Children.Add(new Line
                                {
                                    X1 = cx, Y1 = splitY, X2 = compCx2, Y2 = splitY,
                                    Stroke = new SolidColorBrush(Color.FromArgb(70, CArrow.R, CArrow.G, CArrow.B)),
                                    StrokeThickness = 2, IsHitTestVisible = false
                                });
                            canvas.Children.Add(new Line
                            {
                                X1 = compCx2, Y1 = splitY, X2 = compCx2, Y2 = compY,
                                Stroke = new SolidColorBrush(Color.FromArgb(70, CArrow.R, CArrow.G, CArrow.B)),
                                StrokeThickness = 2, IsHitTestVisible = false
                            });
                        }

                        for (int i = 0; i < perRow - 1 && i < grp.Components.Count - 1; i++)
                        {
                            double wx1 = startX2 + i * (S + SG) + S;
                            double wx2 = startX2 + (i + 1) * (S + SG);
                            DrawWavyLine(canvas, wx1, wx2, compY + S / 2);
                        }

                        for (int i = 0; i < grp.Components.Count; i++)
                        {
                            int ci = i % perRow;
                            int ri2 = i / perRow;
                            double bx = startX2 + ci * (S + SG);
                            var comp = MakeMiniChevron(grp.Components[i], S, onOpenUid);
                            Canvas.SetLeft(comp, bx);
                            Canvas.SetTop(comp, compY + ri2 * (S + 12));
                            Canvas.SetZIndex(comp, 2);
                            canvas.Children.Add(comp);
                        }

                        int totalRows2 = (int)Math.Ceiling((double)grp.Components.Count / perRow);
                        y = compY + totalRows2 * (S + 12) - 12;
                    }

                    rowBottomY.Add(y);
                    timelinePoints.Add(System.Tuple.Create(y - ChevH / 2, row[0].ParentNode.Record.TimestampParsed));
                }
                else
                {
                    int n = row.Count;
                    double rowW = n * chevW + (n - 1) * HGap;
                    double startX = cx - rowW / 2;

                    for (int i = 0; i < n; i++)
                    {
                        double x = startX + i * (chevW + HGap);
                        var grp = row[i];
                        var el = MakeChevron(grp.ParentNode, chevW, ChevH, ChevPt, onOpenUid, equipNames);
                        Canvas.SetLeft(el, x);
                        Canvas.SetTop(el, y);
                        Canvas.SetZIndex(el, 2);
                        canvas.Children.Add(el);

                        if (i < n - 1)
                        {
                            double gapS = row[i + 1].GapFromPrevSec ?? 0;
                            DrawHorizontalArrow(canvas, x + chevW, y + ChevH / 2, HGap, gapS);
                        }
                    }

                    y += ChevH;
                    rowBottomY.Add(y);
                    timelinePoints.Add(System.Tuple.Create(y - ChevH / 2, row[0].ParentNode.Record.TimestampParsed));
                }
            }

            canvas.Width = availW + 75;
            canvas.Height = y + 40;

            double tlX = availW + 4;
            double lastLabelY = double.MinValue;
            DateTime? lastLabelTime = null;
            foreach (var pt in timelinePoints)
            {
                double ptY = pt.Item1;
                DateTime ptTs = pt.Item2;
                if (ptY - lastLabelY < 38) continue;
                if (lastLabelTime.HasValue && Math.Abs((ptTs - lastLabelTime.Value).TotalSeconds) < 20) continue;

                string fmt = (lastLabelTime.HasValue && ptTs.Date != lastLabelTime.Value.Date)
                    ? ptTs.ToString("dd.MM  HH:mm")
                    : ptTs.ToString("HH:mm");
                var tick = new Line
                {
                    X1 = tlX - 6, Y1 = ptY, X2 = tlX, Y2 = ptY,
                    Stroke = new SolidColorBrush(Color.FromArgb(60, 150, 175, 210)), StrokeThickness = 1,
                    IsHitTestVisible = false
                };
                canvas.Children.Add(tick);
                var lbl = new TextBlock
                {
                    Text = fmt, FontSize = 9, FontFamily = new FontFamily("Consolas"),
                    Foreground = new SolidColorBrush(Color.FromArgb(110, 150, 175, 210)), IsHitTestVisible = false
                };
                Canvas.SetLeft(lbl, tlX + 2);
                Canvas.SetTop(lbl, ptY - 7);
                canvas.Children.Add(lbl);
                lastLabelY = ptY;
                lastLabelTime = ptTs;
            }
        }

        private static double GapToArrowH(double gapSec)
        {
            if (gapSec < 2) return 64;
            if (gapSec < 10) return 88;
            if (gapSec < 60) return 120;
            if (gapSec < 300) return 160;
            if (gapSec < 1800) return 200;
            return 260;
        }

        private static List<List<RenderGroup>> BuildRows(List<RenderGroup> groups)
        {
            var rows = new List<List<RenderGroup>>();
            var current = new List<RenderGroup>();

            foreach (var grp in groups)
            {
                bool startNew = false;

                if (grp.IsSemi)
                    startNew = true;
                else if (current.Count >= 5)
                    startNew = true;
                else if (current.Count > 0 && grp.GapFromPrevSec.HasValue && grp.GapFromPrevSec.Value > 1800)
                    startNew = true;
                else if (current.Count > 0 && current[current.Count - 1].IsSemi)
                    startNew = true;

                if (startNew && current.Count > 0)
                {
                    rows.Add(current);
                    current = new List<RenderGroup>();
                }

                current.Add(grp);

                if (grp.IsSemi)
                {
                    rows.Add(current);
                    current = new List<RenderGroup>();
                }
            }

            if (current.Count > 0) rows.Add(current);
            return rows;
        }


        private static void DrawBentArrow(Canvas canvas, double fromCx, double fromY, double toCx, double toY,
            double gapSec)
        {
            Color col;
            if (gapSec < 2) col = Color.FromArgb(180, CArrow.R, CArrow.G, CArrow.B);
            else if (gapSec < 10) col = Color.FromArgb(220, 210, 200, 80);
            else if (gapSec < 60) col = Color.FromArgb(220, 255, 140, 40);
            else if (gapSec < 3600) col = Color.FromArgb(220, 248, 81, 73);
            else col = Color.FromArgb(220, 220, 60, 60);

            double midY = fromY + (toY - fromY) / 2;

            var geo = new StreamGeometry();
            using (var ctx = geo.Open())
            {
                ctx.BeginFigure(new Point(fromCx, fromY), false, false);
                ctx.LineTo(new Point(fromCx, midY), true, false);
                ctx.LineTo(new Point(toCx, midY), true, false);
                ctx.LineTo(new Point(toCx, toY - 12), true, false);
            }

            geo.Freeze();

            canvas.Children.Add(new System.Windows.Shapes.Path
            {
                Data = geo, Stroke = new SolidColorBrush(col),
                StrokeThickness = 6, Fill = Brushes.Transparent, IsHitTestVisible = false
            });
            canvas.Children.Add(new Polygon
            {
                Points = new PointCollection
                    { new Point(toCx - 11, toY - 12), new Point(toCx + 11, toY - 12), new Point(toCx, toY) },
                Fill = new SolidColorBrush(col), IsHitTestVisible = false
            });
            string label;
            if (gapSec < 1) label = ((int)(gapSec * 1000)) + "ms";
            else if (gapSec < 60) label = gapSec.ToString("0.#") + "s";
            else if (gapSec < 3600) label = ((int)(gapSec / 60)) + "m " + ((int)(gapSec % 60)) + "s";
            else label = ((int)(gapSec / 3600)) + "h " + ((int)((gapSec % 3600) / 60)) + "m";

            var labelBg = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(185, 10, 14, 20)),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(5, 2, 5, 2),
                IsHitTestVisible = false,
                Child = new TextBlock
                {
                    Text = label, FontSize = 11, FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromArgb(210, col.R, col.G, col.B))
                }
            };
            Canvas.SetLeft(labelBg, (fromCx + toCx) / 2 - 32);
            Canvas.SetTop(labelBg, midY - 24);
            Canvas.SetZIndex(labelBg, 5);
            canvas.Children.Add(labelBg);
        }

        private static void DrawHorizontalArrow(Canvas canvas, double fromX, double y, double w, double gapSec)
        {
            Color col;
            if (gapSec < 2) col = Color.FromArgb(180, CArrow.R, CArrow.G, CArrow.B);
            else if (gapSec < 10) col = Color.FromArgb(220, 210, 200, 80);
            else if (gapSec < 60) col = Color.FromArgb(220, 255, 140, 40);
            else col = Color.FromArgb(220, 248, 81, 73);

            double headW = 8;
            double lineEnd = fromX + w - headW;

            canvas.Children.Add(new Line
            {
                X1 = fromX, Y1 = y, X2 = lineEnd, Y2 = y, Stroke = new SolidColorBrush(col), StrokeThickness = 6,
                IsHitTestVisible = false
            });
            canvas.Children.Add(new Polygon
            {
                Points = new PointCollection
                    { new Point(lineEnd, y - 9), new Point(lineEnd, y + 9), new Point(lineEnd + headW, y) },
                Fill = new SolidColorBrush(col), IsHitTestVisible = false
            });

            if (gapSec > 0.5)
            {
                string label = gapSec < 1 ? ((int)(gapSec * 1000)) + "ms"
                    : gapSec < 60 ? gapSec.ToString("0.#") + "s"
                    : gapSec < 3600 ? ((int)(gapSec / 60)) + "m " + ((int)(gapSec % 60)) + "s"
                    : ((int)(gapSec / 3600)) + "h " + ((int)((gapSec % 3600) / 60)) + "m";
                var tb = new TextBlock
                {
                    Text = label, FontSize = 9, FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromArgb(200, col.R, col.G, col.B)), IsHitTestVisible = false
                };
                Canvas.SetLeft(tb, fromX + w / 2 - 14);
                Canvas.SetTop(tb, y - 14);
                Canvas.SetZIndex(tb, 1);
                canvas.Children.Add(tb);
            }
        }

        private static void DrawVerticalArrow(Canvas canvas, double cx, double y, double h, double gapSec)
        {
            Color col;
            if (gapSec < 2) col = Color.FromArgb(180, CArrow.R, CArrow.G, CArrow.B);
            else if (gapSec < 10) col = Color.FromArgb(220, 210, 200, 80);
            else if (gapSec < 60) col = Color.FromArgb(220, 255, 140, 40);
            else if (gapSec < 3600) col = Color.FromArgb(220, 248, 81, 73);
            else col = Color.FromArgb(220, 220, 60, 60);

            double lineEnd = y + h - 12;
            canvas.Children.Add(new Line
            {
                X1 = cx, Y1 = y, X2 = cx, Y2 = lineEnd, Stroke = new SolidColorBrush(col), StrokeThickness = 6,
                IsHitTestVisible = false
            });
            canvas.Children.Add(new Polygon
            {
                Points = new PointCollection
                    { new Point(cx - 11, lineEnd), new Point(cx + 11, lineEnd), new Point(cx, lineEnd + 14) },
                Fill = new SolidColorBrush(col), IsHitTestVisible = false
            });

            string label;
            if (gapSec < 1) label = ((int)(gapSec * 1000)) + "ms";
            else if (gapSec < 60) label = gapSec.ToString("0.#") + "s";
            else if (gapSec < 3600) label = ((int)(gapSec / 60)) + "m " + ((int)(gapSec % 60)) + "s";
            else label = ((int)(gapSec / 3600)) + "h " + ((int)((gapSec % 3600) / 60)) + "m";

            var tb = new TextBlock
            {
                Text = label, FontSize = 11, FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromArgb(210, col.R, col.G, col.B)), IsHitTestVisible = false
            };
            Canvas.SetLeft(tb, cx + 12);
            Canvas.SetTop(tb, y + h / 2 - 9);
            Canvas.SetZIndex(tb, 1);
            canvas.Children.Add(tb);
        }

        private static void DrawWavyLine(Canvas canvas, double x1, double x2, double y)
        {
            var col = Color.FromArgb(150, CArrow.R, CArrow.G, CArrow.B);
            double amp = 5;
            double freq = 8;
            var geo = new StreamGeometry();
            using (var ctx = geo.Open())
            {
                ctx.BeginFigure(new Point(x1, y), false, false);
                double x = x1;
                bool up = true;
                while (x < x2)
                {
                    double nx = Math.Min(x + freq, x2);
                    double mx = (x + nx) / 2;
                    ctx.QuadraticBezierTo(new Point(mx, y + (up ? -amp : amp)), new Point(nx, y), true, false);
                    x = nx;
                    up = !up;
                }
            }

            geo.Freeze();
            canvas.Children.Add(new System.Windows.Shapes.Path
                { Data = geo, Stroke = new SolidColorBrush(col), StrokeThickness = 2.5, IsHitTestVisible = false });
        }

        private static void DrawVerticalLine(Canvas canvas, double cx, double y, double h)
        {
            canvas.Children.Add(new Line
            {
                X1 = cx, Y1 = y, X2 = cx, Y2 = y + h,
                Stroke = new SolidColorBrush(Color.FromArgb(80, CArrow.R, CArrow.G, CArrow.B)), StrokeThickness = 2,
                IsHitTestVisible = false
            });
        }

        private static readonly Color CStation = Color.FromRgb(255, 159, 28);

        private static void DrawStationBox(Canvas canvas, double y, double cx, double w, string label, Color col)
        {
            double boxW = Math.Min(340, (w + MarginH) * 0.52);
            double boxH = 38;
            var b = new Border
            {
                Width = boxW, Height = boxH,
                Background = new SolidColorBrush(Color.FromArgb(55, CStation.R, CStation.G, CStation.B)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(210, CStation.R, CStation.G, CStation.B)),
                BorderThickness = new Thickness(1.5), CornerRadius = new CornerRadius(6),
                IsHitTestVisible = false,
                Child = new TextBlock
                {
                    Text = "⬡  " + label, FontSize = 12, FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromArgb(240, CStation.R, CStation.G, CStation.B)),
                    VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center
                }
            };
            Canvas.SetLeft(b, cx - boxW / 2);
            Canvas.SetTop(b, y);
            Canvas.SetZIndex(b, 1);
            canvas.Children.Add(b);
        }

        private static void DrawStationConnector(Canvas canvas, double cx, double fromY, double toY, Color col)
        {
            double len = toY - fromY;
            double lineEnd = toY - 9;
            var orange = CStation;
            canvas.Children.Add(new Line
            {
                X1 = cx - 3, Y1 = fromY, X2 = cx - 3, Y2 = lineEnd,
                Stroke = new SolidColorBrush(Color.FromArgb(140, orange.R, orange.G, orange.B)),
                StrokeThickness = 1.8, StrokeDashArray = new DoubleCollection { 5, 4 },
                IsHitTestVisible = false
            });
            canvas.Children.Add(new Line
            {
                X1 = cx + 3, Y1 = fromY, X2 = cx + 3, Y2 = lineEnd,
                Stroke = new SolidColorBrush(Color.FromArgb(90, orange.R, orange.G, orange.B)),
                StrokeThickness = 1.2, StrokeDashArray = new DoubleCollection { 5, 4 },
                IsHitTestVisible = false
            });
            canvas.Children.Add(new Polygon
            {
                Points = new PointCollection
                    { new Point(cx - 8, lineEnd), new Point(cx + 8, lineEnd), new Point(cx, toY) },
                Fill = new SolidColorBrush(Color.FromArgb(150, orange.R, orange.G, orange.B)),
                IsHitTestVisible = false
            });
        }

        private static void DrawDaySeparator(Canvas canvas, double y, double cx, double availW, string day)
        {
            var col = Color.FromRgb(110, 140, 190);
            double lineY = y + DayLineH / 2;
            var lbl = new TextBlock
            {
                Text = day, FontSize = 14, FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromArgb(200, col.R, col.G, col.B)), IsHitTestVisible = false
            };
            Canvas.SetLeft(lbl, MarginH);
            Canvas.SetTop(lbl, y + 2);
            Canvas.SetZIndex(lbl, 1);
            canvas.Children.Add(lbl);

            var line = new Line
            {
                X1 = MarginH + 120, Y1 = lineY, X2 = availW - MarginH, Y2 = lineY,
                Stroke = new SolidColorBrush(Color.FromArgb(100, col.R, col.G, col.B)), StrokeThickness = 1.5,
                StrokeDashArray = new DoubleCollection { 6, 4 }, IsHitTestVisible = false
            };
            Canvas.SetZIndex(line, 0);
            canvas.Children.Add(line);
        }


        private static UIElement MakeChevron(DiagramNode node, double w, double h, double pt, Action<string> onOpenUid,
            Dictionary<string, string> equipNames)
        {
            var r = node.Record;
            bool isErr = IsError(r);
            var col = isErr ? CRed : MessageColors.Get(r.Type);

            var container = new Grid { Width = w, Height = h };
            var cvs = new Canvas { Width = w, Height = h, IsHitTestVisible = false };

            var poly = new Polygon
            {
                Points = new PointCollection
                {
                    new Point(0, h / 2), new Point(pt, 0), new Point(w - pt, 0), new Point(w, h / 2),
                    new Point(w - pt, h), new Point(pt, h)
                },
                Fill = new SolidColorBrush(Color.FromArgb(isErr ? (byte)40 : (byte)20, col.R, col.G, col.B)),
                Stroke = new SolidColorBrush(Color.FromArgb(isErr ? (byte)220 : (byte)160, col.R, col.G, col.B)),
                StrokeThickness = isErr ? 2 : 1.5
            };
            cvs.Children.Add(poly);

            var inner = new Grid { Width = w - pt * 2, Height = h, IsHitTestVisible = false };
            inner.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            inner.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            inner.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var tsRow = new StackPanel
            {
                Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom
            };
            tsRow.Children.Add(new TextBlock
            {
                Text = r.TimestampParsed.ToString("dd.MM.yyyy  HH:mm:ss"), FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(160, 175, 195))
            });
            Grid.SetRow(tsRow, 0);
            inner.Children.Add(tsRow);

            var midRow = new StackPanel
            {
                Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            if (!isErr)
                midRow.Children.Add(new TextBlock
                {
                    Text = TypeIcon(r.Type), FontSize = 22, VerticalAlignment = VerticalAlignment.Center,
                    Foreground = new SolidColorBrush(Color.FromArgb(200, col.R, col.G, col.B)),
                    Margin = new Thickness(0, 0, 6, 0)
                });
            else
                midRow.Children.Add(new TextBlock
                {
                    Text = "⊗", FontSize = 22, VerticalAlignment = VerticalAlignment.Center,
                    Foreground = new SolidColorBrush(CRed), Margin = new Thickness(0, 0, 6, 0)
                });
            string typeName = isErr ? FormatType(r.Type) + " — ERROR" : FormatType(r.Type);
            midRow.Children.Add(new TextBlock
            {
                Text = typeName, FontSize = 11, FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromArgb(230, col.R, col.G, col.B)),
                Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center
            });
            string rv = GetResult(r);
            if (!string.IsNullOrEmpty(rv) && !isErr)
                midRow.Children.Add(new TextBlock
                {
                    Text = rv, FontSize = 11, FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(ResultColor(rv)), Margin = new Thickness(0, 0, 8, 0),
                    VerticalAlignment = VerticalAlignment.Center
                });
            if (r.ResponseTime > 0)
                midRow.Children.Add(new TextBlock
                {
                    Text = r.ResponseTime + "ms", FontSize = 10,
                    Foreground = new SolidColorBrush(r.ResponseTime > 100 ? CRed : Color.FromRgb(130, 148, 168)),
                    VerticalAlignment = VerticalAlignment.Center
                });
            Grid.SetRow(midRow, 1);
            inner.Children.Add(midRow);

            string stName = ResolveStation(r.EquipId, equipNames);
            if (!string.IsNullOrEmpty(stName))
            {
                var stRow = new StackPanel
                {
                    Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Top
                };
                stRow.Children.Add(new TextBlock
                {
                    Text = stName, FontSize = 11, FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromRgb(120, 140, 165)),
                    TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = w - pt * 2 - 8
                });
                Grid.SetRow(stRow, 2);
                inner.Children.Add(stRow);
            }

            Canvas.SetLeft(inner, pt);
            cvs.Children.Add(inner);
            container.Children.Add(cvs);

            Popup popup = null;
            bool sticky = false;
            var hit = new Border { Background = Brushes.Transparent, Cursor = Cursors.Hand };
            hit.MouseEnter += (s, e) =>
            {
                poly.Fill = new SolidColorBrush(Color.FromArgb(50, col.R, col.G, col.B));
                poly.Stroke = new SolidColorBrush(col);
                if (popup == null)
                    popup = BuildPopup(node, col, onOpenUid, equipNames, () =>
                    {
                        sticky = false;
                        popup.IsOpen = false;
                    });
                if (!sticky) popup.IsOpen = true;
            };
            hit.MouseLeave += (s, e) =>
            {
                poly.Fill = new SolidColorBrush(Color.FromArgb(isErr ? (byte)40 : (byte)20, col.R, col.G, col.B));
                poly.Stroke = new SolidColorBrush(Color.FromArgb(isErr ? (byte)220 : (byte)160, col.R, col.G, col.B));
                if (!sticky && popup != null) popup.IsOpen = false;
            };
            hit.MouseLeftButtonUp += (s, e) =>
            {
                sticky = true;
                if (popup == null)
                    popup = BuildPopup(node, col, onOpenUid, equipNames, () =>
                    {
                        sticky = false;
                        popup.IsOpen = false;
                    });
                popup.IsOpen = true;
                e.Handled = true;
            };
            container.Children.Add(hit);
            return container;
        }

        private static UIElement MakeMiniChevron(CompNode comp, double w, Action<string> onOpenUid)
        {
            const double S = 60;
            var cCol = comp.ProcDir == "Y" ? CGreen : comp.ProcDir == "N" ? CRed : Color.FromRgb(210, 153, 34);

            Popup popup = null;
            bool sticky = false;

            var border = new Border
            {
                Width = S, Height = S,
                Background = new SolidColorBrush(Color.FromArgb(30, cCol.R, cCol.G, cCol.B)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(170, cCol.R, cCol.G, cCol.B)),
                BorderThickness = new Thickness(1.5),
                CornerRadius = new CornerRadius(0),
                Cursor = onOpenUid != null ? Cursors.Hand : Cursors.Arrow
            };

            var inner = new StackPanel
                { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
            inner.Children.Add(new TextBlock
            {
                Text = "◈", FontSize = 13,
                Foreground = new SolidColorBrush(Color.FromArgb(150, cCol.R, cCol.G, cCol.B)),
                HorizontalAlignment = HorizontalAlignment.Center
            });
            inner.Children.Add(new TextBlock
            {
                Text = comp.ProcDir, FontSize = 12, FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(cCol), HorizontalAlignment = HorizontalAlignment.Center
            });
            inner.Children.Add(new TextBlock
            {
                Text = TruncUid(comp.UidAssy), FontSize = 7, FontFamily = new FontFamily("Consolas"),
                Foreground = new SolidColorBrush(Color.FromRgb(56, 182, 255)),
                HorizontalAlignment = HorizontalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = S - 6
            });
            border.Child = inner;

            border.MouseEnter += (s, e) =>
            {
                border.Background = new SolidColorBrush(Color.FromArgb(55, cCol.R, cCol.G, cCol.B));
                border.BorderBrush = new SolidColorBrush(cCol);
                if (popup == null)
                    popup = BuildCompPopup(comp, cCol, onOpenUid, () =>
                    {
                        sticky = false;
                        popup.IsOpen = false;
                    });
                if (!sticky) popup.IsOpen = true;
            };
            border.MouseLeave += (s, e) =>
            {
                border.Background = new SolidColorBrush(Color.FromArgb(30, cCol.R, cCol.G, cCol.B));
                border.BorderBrush = new SolidColorBrush(Color.FromArgb(170, cCol.R, cCol.G, cCol.B));
                if (!sticky && popup != null) popup.IsOpen = false;
            };
            border.MouseLeftButtonUp += (s, e) =>
            {
                if (onOpenUid != null && !string.IsNullOrEmpty(comp.UidAssy))
                {
                    sticky = true;
                    if (popup == null)
                        popup = BuildCompPopup(comp, cCol, onOpenUid, () =>
                        {
                            sticky = false;
                            popup.IsOpen = false;
                        });
                    popup.IsOpen = true;
                }

                e.Handled = true;
            };

            return border;
        }

        private static Popup BuildPopup(DiagramNode node, Color col, Action<string> onOpenUid,
            Dictionary<string, string> equipNames, Action onClose)
        {
            var r = node.Record;
            var stack = new StackPanel { Margin = new Thickness(12, 10, 12, 10), MinWidth = 300, MaxWidth = 440 };

            AddPopupTitle(stack, FormatType(r.Type), col, onClose);
            stack.Children.Add(new TextBlock
            {
                Text = "Click to stick this info", FontSize = 8,
                Foreground = new SolidColorBrush(Color.FromArgb(100, CDim.R, CDim.G, CDim.B)),
                Margin = new Thickness(0, 0, 0, 4)
            });

            string rv = GetResult(r);
            if (!string.IsNullOrEmpty(rv)) AddResultBadge(stack, rv, r.ResponseTime);
            stack.Children.Add(new TextBlock
            {
                Text = r.TimestampParsed.ToString("dd.MM.yyyy  HH:mm:ss"), FontSize = 9,
                Foreground = new SolidColorBrush(CDim), Margin = new Thickness(0, 0, 0, 8)
            });
            stack.Children.Add(new Border
                { Height = 1, Background = new SolidColorBrush(CBorder), Margin = new Thickness(0, 0, 0, 8) });

            AddTypeSpecificRows(stack, r, onOpenUid);

            AddDetailRow(stack, "Station", ResolveStation(r.EquipId, equipNames), false, null);
            AddDetailRow(stack, "File", r.FileName, false, null);

            return WrapPopup(stack, col);
        }

        private static void AddTypeSpecificRows(StackPanel stack, ResponseRecord r, Action<string> onOpenUid)
        {
            void Row(string lbl, string val, bool uid = false) => AddDetailRow(stack, lbl, val, uid, onOpenUid);

            switch (r.Type)
            {
                case MessageType.UNIT_INFO:
                    Row("Workcenter", r.Workcenter);
                    Row("Operation", r.Operation);
                    Row("Material", r.Material);
                    Row("UID", r.Uid ?? r.UidIn, true);
                    break;

                case MessageType.NEXT_OPERATION:
                    if (!string.IsNullOrEmpty(r.NextWorkcenter1))
                        Row("Next 1", r.NextWorkcenter1 + (r.NextOperation1 != null ? "  op." + r.NextOperation1 : ""));
                    if (!string.IsNullOrEmpty(r.NextWorkcenter2))
                        Row("Next 2", r.NextWorkcenter2 + (r.NextOperation2 != null ? "  op." + r.NextOperation2 : ""));
                    Row("UID", r.Uid ?? r.UidIn, true);
                    break;

                case MessageType.UNIT_CHECKIN:
                    Row("Directive", DescribeResultValue(r.Type, r.Result));
                    Row("Product Line", r.ProductLine?.Replace("_", " "));
                    Row("UID Type", r.UidType);
                    Row("UID In", r.UidIn, true);
                    Row("UID Out", r.UidOut, true);
                    break;

                case MessageType.SEMI_VALIDATION2:
                    Row("Parent UID", r.Uid ?? r.UidIn, true);
                    Row("Component", r.UidAssy, true);
                    Row("Comp Type", r.UidAssyType);
                    Row("ProcDir", r.ProcDirAssy);
                    break;

                case MessageType.UNIT_RESULT:
                    AddUnitResultRows(stack, r, onOpenUid);
                    break;

                case MessageType.REQ_SETUP_CHANGE2:
                    Row("Setup", r.Setup);
                    Row("Material", r.Material);
                    break;

                case MessageType.PANEL_CHECKIN:
                case MessageType.PANEL_RESULT:
                    Row("Panel ID", r.PanelId, true);
                    Row("Position UID", r.Uid, true);
                    Row("Result", DescribeResultValue(r.Type, r.Result));
                    if (r.Type == MessageType.PANEL_RESULT && !string.IsNullOrEmpty(r.MeasValuesRaw))
                        Row("Test file", r.MeasValuesRaw);
                    break;

                default:
                    Row("UID", r.Uid ?? r.UidIn, true);
                    Row("Result", r.Result);
                    break;
            }
        }

        private static void AddUnitResultRows(StackPanel stack, ResponseRecord r, Action<string> onOpenUid)
        {
            void Row(string lbl, string val, bool uid = false) => AddDetailRow(stack, lbl, val, uid, onOpenUid);

            Row("Result", r.Result);
            Row("UID In", r.UidIn, true);
            Row("UID Out", r.UidOut, true);
            Row("Assembled", r.UidAssyUnitResult, true);

            if (string.IsNullOrEmpty(r.MeasValuesRaw)) return;

            stack.Children.Add(new TextBlock
            {
                Text = "Measurements", FontSize = 9, FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(CDim), Margin = new Thickness(0, 6, 0, 4)
            });
            foreach (var p in r.MeasValuesRaw.Split('|'))
            {
                var kv = p.Split('=');
                if (kv.Length == 2) Row(kv[0].Replace("_val", "").Replace("_", " "), kv[1]);
            }
        }

        private static Popup BuildCompPopup(CompNode comp, Color col, Action<string> onOpenUid, Action onClose)
        {
            var stack = new StackPanel { Margin = new Thickness(12, 10, 12, 10), MinWidth = 260, MaxWidth = 380 };
            AddPopupTitle(stack, "SEMI VALIDATION — Component", col, onClose);
            stack.Children.Add(new TextBlock
            {
                Text = "Click to stick this info", FontSize = 8,
                Foreground = new SolidColorBrush(Color.FromArgb(100, CDim.R, CDim.G, CDim.B)),
                Margin = new Thickness(0, 0, 0, 6)
            });
            stack.Children.Add(new Border
                { Height = 1, Background = new SolidColorBrush(CBorder), Margin = new Thickness(0, 0, 0, 8) });
            AddDetailRow(stack, "UID", comp.UidAssy, true, onOpenUid);
            AddDetailRow(stack, "Type", comp.UidAssyType, false, null);
            AddDetailRow(stack, "ProcDir", comp.ProcDir, false, null);
            AddDetailRow(stack, "Timestamp", comp.Timestamp.ToString("dd.MM.yyyy HH:mm:ss"), false, null);
            return WrapPopup(stack, col);
        }

        private static void AddPopupTitle(StackPanel stack, string title, Color col, Action onClose)
        {
            var row = new Grid { Margin = new Thickness(0, 0, 0, 2) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.Children.Add(new TextBlock
                { Text = title, FontSize = 12, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(col) });
            var x = new TextBlock
            {
                Text = "✕", FontSize = 11, Foreground = new SolidColorBrush(CDim), Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0)
            };
            x.MouseLeftButtonUp += (s, e) =>
            {
                onClose();
                e.Handled = true;
            };
            x.MouseEnter += (s, e) => x.Foreground = new SolidColorBrush(CRed);
            x.MouseLeave += (s, e) => x.Foreground = new SolidColorBrush(CDim);
            Grid.SetColumn(x, 1);
            row.Children.Add(x);
            stack.Children.Add(row);
        }

        private static void AddResultBadge(StackPanel stack, string rv, int rt)
        {
            var rc = ResultColor(rv);
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
            row.Children.Add(new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(30, rc.R, rc.G, rc.B)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(100, rc.R, rc.G, rc.B)),
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(3),
                Padding = new Thickness(7, 1, 7, 1), Margin = new Thickness(0, 0, 8, 0),
                Child = new TextBlock
                    { Text = rv, FontSize = 10, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(rc) }
            });
            if (rt > 0)
                row.Children.Add(new TextBlock
                {
                    Text = rt + " ms", FontSize = 10, Foreground = new SolidColorBrush(rt > 100 ? CRed : CDim),
                    VerticalAlignment = VerticalAlignment.Center
                });
            stack.Children.Add(row);
        }

        private static void AddDetailRow(StackPanel stack, string label, string value, bool uid,
            Action<string> onOpenUid)
        {
            if (string.IsNullOrEmpty(value)) return;
            var row = new Grid { Margin = new Thickness(0, 0, 0, 4) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.Children.Add(new TextBlock { Text = label, FontSize = 9, Foreground = new SolidColorBrush(CDim) });
            var vt = new TextBlock
            {
                Text = value, FontSize = 9, Foreground = new SolidColorBrush(uid ? Color.FromRgb(56, 182, 255) : CText),
                TextWrapping = TextWrapping.Wrap,
                FontFamily =
                    uid ? new FontFamily("Consolas") : new FontFamily("pack://application:,,,/Fonts/#Inter 18pt"),
                Cursor = uid && onOpenUid != null ? Cursors.Hand : Cursors.Arrow,
                ToolTip = uid && onOpenUid != null ? "Click to open history" : null
            };
            if (uid && onOpenUid != null)
            {
                var cap = value;
                vt.MouseLeftButtonUp += (s, e) =>
                {
                    onOpenUid(cap);
                    e.Handled = true;
                };
            }

            Grid.SetColumn(vt, 1);
            row.Children.Add(vt);
            stack.Children.Add(row);
        }

        private static Popup WrapPopup(StackPanel stack, Color col) => new Popup
        {
            AllowsTransparency = true, Placement = PlacementMode.Mouse, StaysOpen = true, IsOpen = false,
            Child = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(248, 13, 17, 23)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(200, col.R, col.G, col.B)),
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6), Child = stack
            }
        };

        private static UIElement BuildHeader(string uid, List<ResponseRecord> records,
            Func<Action<string>, List<ResponseRecord>> onScanFullLine, Dictionary<string, string> equipNames,
            Action<List<ResponseRecord>> onRefresh, out TextBlock scanStatusText, out TextBlock fileSubTextOut,
            out TextBlock foundStationsLabelOut, out WrapPanel foundStationsPanel, out Canvas hexStreamCanvas)
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(10, 14, 20)),
                BorderBrush = new SolidColorBrush(CBorder),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(16, 14, 16, 10)
            };

            var root = new StackPanel { Orientation = Orientation.Vertical };
            var row = new StackPanel { Orientation = Orientation.Horizontal };

            var uidTb = new TextBlock
            {
                Text = uid, FontSize = 22, FontWeight = FontWeights.SemiBold, FontFamily = new FontFamily("Consolas"),
                Foreground = new SolidColorBrush(Color.FromRgb(88, 226, 124)),
                VerticalAlignment = VerticalAlignment.Center, Cursor = Cursors.Hand, ToolTip = "Click to copy"
            };
            uidTb.MouseLeftButtonUp += (s, e) =>
            {
                Clipboard.SetText(uid);
                uidTb.Foreground = new SolidColorBrush(CGreen);
                var t = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };
                t.Tick += (ts, te) =>
                {
                    uidTb.Foreground = new SolidColorBrush(Color.FromRgb(88, 226, 124));
                    t.Stop();
                };
                t.Start();
            };
            row.Children.Add(uidTb);

            var mat = records.FirstOrDefault(r => !string.IsNullOrEmpty(r.Material))?.Material;
            var line = records.FirstOrDefault(r => !string.IsNullOrEmpty(r.ProductLine))?.ProductLine;
            var type = records.FirstOrDefault(r => !string.IsNullOrEmpty(r.UidType))?.UidType;
            if (!string.IsNullOrEmpty(type)) row.Children.Add(Chip(type, Color.FromRgb(140, 160, 180), 11));
            if (!string.IsNullOrEmpty(mat)) row.Children.Add(Chip(mat, Color.FromRgb(56, 182, 255), 11));
            if (!string.IsNullOrEmpty(line))
                row.Children.Add(Chip(line.Replace("_", " "), Color.FromRgb(155, 89, 182), 11));
            row.Children.Add(Chip(records.Count + " events", Color.FromRgb(140, 160, 180), 11));

            if (onScanFullLine != null)
            {
                var scanLbl = new TextBlock
                {
                    Text = "🔎  Scan Full Line", FontSize = 11, FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromRgb(56, 182, 255))
                };
                var scanBtn = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(30, 56, 182, 255)),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(100, 56, 182, 255)),
                    BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(3),
                    Padding = new Thickness(10, 4, 10, 4), Margin = new Thickness(16, 0, 0, 0), Cursor = Cursors.Hand,
                    Child = scanLbl
                };
                scanBtn.MouseEnter += (s, e) =>
                    scanBtn.Background = new SolidColorBrush(Color.FromArgb(60, 56, 182, 255));
                scanBtn.MouseLeave += (s, e) =>
                    scanBtn.Background = new SolidColorBrush(Color.FromArgb(30, 56, 182, 255));
                scanBtn.MouseLeftButtonUp += (s, e) =>
                {
                    scanLbl.Text = "⏳  Scanning...";
                    scanBtn.IsHitTestVisible = false;
                    System.Threading.Tasks.Task.Run(() =>
                    {
                        var fr = onScanFullLine(st =>
                            System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                                scanLbl.Text = "⏳  " + st)));
                        System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            scanLbl.Text = "✓  " + fr.Count + " events";
                            scanBtn.Background = new SolidColorBrush(Color.FromArgb(30, 63, 185, 80));
                            scanBtn.BorderBrush = new SolidColorBrush(Color.FromArgb(100, 63, 185, 80));
                            scanLbl.Foreground = new SolidColorBrush(CGreen);
                            onRefresh(fr);
                        }));
                    });
                };
                row.Children.Add(scanBtn);
            }

            var statusRow = new StackPanel
                { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
            hexStreamCanvas = new Canvas
            {
                Width = 176, Height = 28, Margin = new Thickness(0, 0, 12, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            var statusStack = new StackPanel
                { Orientation = Orientation.Vertical, VerticalAlignment = VerticalAlignment.Center };
            scanStatusText = new TextBlock
            {
                Text = "Initializing...",
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                FontFamily = new FontFamily("Consolas"),
                Foreground = new SolidColorBrush(Color.FromRgb(144, 228, 162)),
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            fileSubTextOut = new TextBlock
            {
                Text = "",
                FontSize = 9,
                FontFamily = new FontFamily("Consolas"),
                Foreground = new SolidColorBrush(Color.FromRgb(80, 110, 90)),
                Margin = new Thickness(0, 2, 0, 0)
            };
            statusStack.Children.Add(scanStatusText);
            statusStack.Children.Add(fileSubTextOut);
            statusRow.Children.Add(hexStreamCanvas);
            statusRow.Children.Add(statusStack);

            foundStationsLabelOut = new TextBlock
            {
                Text = "Found on:  ",
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(CGreen),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 4, 4),
                Visibility = Visibility.Collapsed
            };
            foundStationsPanel = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
            var foundRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
            foundRow.Children.Add(foundStationsLabelOut);
            foundRow.Children.Add(foundStationsPanel);

            root.Children.Add(row);
            root.Children.Add(statusRow);
            root.Children.Add(foundRow);
            border.Child = root;
            return border;
        }

        private static void RenderHexStreamFrame(Canvas canvas, double phase, bool pulseGreen)
        {
            if (canvas == null) return;
            canvas.Children.Clear();

            int count = 7;
            double radius = 8.5;
            double spacing = 24.0;
            double centerY = 14.0;
            double leaderPos = phase % count;

            var greenBase = pulseGreen ? Color.FromRgb(88, 226, 124) : Color.FromRgb(63, 185, 80);
            var orangeLead = Color.FromRgb(255, 159, 28);

            for (int i = 0; i < count; i++)
            {
                double d = Math.Abs(i - leaderPos);
                d = Math.Min(d, count - d);

                double trail = Math.Max(0.0, 1.0 - d / 2.0);
                double pulse = 0.92 + trail * 0.28;
                double r = radius * pulse;

                byte cr = (byte)(greenBase.R + (orangeLead.R - greenBase.R) * trail);
                byte cg = (byte)(greenBase.G + (orangeLead.G - greenBase.G) * trail);
                byte cb = (byte)(greenBase.B + (orangeLead.B - greenBase.B) * trail);
                byte alpha = (byte)(105 + 130 * Math.Max(0.25, trail));

                var fill = new SolidColorBrush(Color.FromArgb(alpha, cr, cg, cb));
                var stroke = new SolidColorBrush(Color.FromArgb((byte)Math.Min(255, alpha + 70), cr, cg, cb));

                double cx = 10 + i * spacing;
                var hex = new Polygon
                {
                    Fill = fill,
                    Stroke = stroke,
                    StrokeThickness = 1.2,
                    Points = new PointCollection(new[]
                    {
                        new Point(cx + 0, centerY - r),
                        new Point(cx + r * 0.87, centerY - r * 0.5),
                        new Point(cx + r * 0.87, centerY + r * 0.5),
                        new Point(cx + 0, centerY + r),
                        new Point(cx - r * 0.87, centerY + r * 0.5),
                        new Point(cx - r * 0.87, centerY - r * 0.5)
                    })
                };

                canvas.Children.Add(hex);
            }
        }

        private static List<ResponseRecord> DeduplicateRecords(List<ResponseRecord> records)
        {
            return records
                .GroupBy(r =>
                    r.TimestampParsed.Ticks.ToString() + "|" + r.Type.ToString() + "|" + (r.EquipId ?? "") + "|" +
                    (r.FileName ?? ""))
                .Select(g => g.First())
                .OrderBy(r => r.TimestampParsed)
                .ToList();
        }

        private static void AnimateDiagramRefresh(Canvas canvas, List<ResponseRecord> records,
            Action<string> onOpenUid, Dictionary<string, string> equipNames)
        {
            RenderDiagram(canvas, records, onOpenUid, equipNames);

            var fade = new System.Windows.Media.Animation.DoubleAnimation
            {
                From = 0.30,
                To = 1.0,
                Duration = TimeSpan.FromMilliseconds(220),
                EasingFunction = new System.Windows.Media.Animation.QuadraticEase
                {
                    EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut
                }
            };
            canvas.BeginAnimation(UIElement.OpacityProperty, fade);
        }


        private static List<RenderGroup> BuildGroups(List<ResponseRecord> records)
        {
            var result = new List<RenderGroup>();
            var handled = new HashSet<ResponseRecord>();
            var ordered = records.OrderBy(r => r.TimestampParsed).ToList();
            ResponseRecord prevRecord = null;

            for (int i = 0; i < ordered.Count; i++)
            {
                var r = ordered[i];
                if (handled.Contains(r)) continue;

                if (r.Type == MessageType.PANEL_CHECKIN || r.Type == MessageType.PANEL_RESULT)
                {
                    var siblings = ordered
                        .Where(x => !handled.Contains(x)
                                    && x.Type == r.Type
                                    && x.PanelId == r.PanelId
                                    && Math.Abs((x.TimestampParsed - r.TimestampParsed).TotalSeconds) <= 5)
                        .OrderBy(x => x.TimestampParsed)
                        .ToList();

                    if (siblings.Count == 0) siblings.Add(r);
                    foreach (var sr in siblings) handled.Add(sr);

                    var comps = siblings.Select(sr => new CompNode
                    {
                        UidAssy = sr.Uid,
                        UidAssyType = sr.Type == MessageType.PANEL_RESULT ? "Result" : "Checkin",
                        ProcDir = sr.Result,
                        Timestamp = sr.TimestampParsed
                    }).ToList();

                    double? gap = prevRecord != null
                        ? Math.Abs((r.TimestampParsed - prevRecord.TimestampParsed).TotalSeconds)
                        : (double?)null;

                    result.Add(new RenderGroup
                    {
                        IsSemi = true,
                        ParentNode = new DiagramNode { Record = r, EquipId = r.EquipId ?? "" },
                        Components = comps,
                        Day = r.TimestampParsed.ToString("dd.MM.yyyy"),
                        Station = r.EquipId ?? "",
                        GapFromPrevSec = gap
                    });
                    prevRecord = siblings.Last();
                }
                else if (r.Type == MessageType.SEMI_VALIDATION2)
                {
                    var siblings = ordered
                        .Where(x => !handled.Contains(x)
                                    && x.Type == MessageType.SEMI_VALIDATION2
                                    && (x.Uid == r.Uid || x.UidIn == r.UidIn)
                                    && Math.Abs((x.TimestampParsed - r.TimestampParsed).TotalSeconds) <= 60)
                        .OrderBy(x => x.TimestampParsed)
                        .ToList();

                    foreach (var sr in siblings) handled.Add(sr);

                    var comps = siblings.Select(sr => new CompNode
                    {
                        UidAssy = sr.UidAssy,
                        UidAssyType = sr.UidAssyType,
                        ProcDir = sr.ProcDirAssy,
                        Timestamp = sr.TimestampParsed
                    }).ToList();

                    double? gap = prevRecord != null
                        ? Math.Abs((r.TimestampParsed - prevRecord.TimestampParsed).TotalSeconds)
                        : (double?)null;

                    result.Add(new RenderGroup
                    {
                        IsSemi = true,
                        ParentNode = new DiagramNode { Record = r, EquipId = r.EquipId ?? "" },
                        Components = comps,
                        Day = r.TimestampParsed.ToString("dd.MM.yyyy"),
                        Station = r.EquipId ?? "",
                        GapFromPrevSec = gap
                    });
                    prevRecord = siblings.Last();
                }
                else
                {
                    handled.Add(r);
                    double? gap = prevRecord != null
                        ? Math.Abs((r.TimestampParsed - prevRecord.TimestampParsed).TotalSeconds)
                        : (double?)null;
                    result.Add(new RenderGroup
                    {
                        IsSemi = false,
                        ParentNode = new DiagramNode { Record = r, EquipId = r.EquipId ?? "" },
                        Components = new List<CompNode>(),
                        Day = r.TimestampParsed.ToString("dd.MM.yyyy"),
                        Station = r.EquipId ?? "",
                        GapFromPrevSec = gap
                    });
                    prevRecord = r;
                }
            }

            return result;
        }

        private static string DescribeResultValue(MessageType type, string val)
        {
            if (string.IsNullOrEmpty(val)) return val;
            switch (type)
            {
                case MessageType.PANEL_CHECKIN:
                    switch (val.ToUpper())
                    {
                        case "Y": return "Y — Process";
                        case "N": return "N — Skip";
                        default: return val;
                    }
                case MessageType.PANEL_RESULT:
                    switch (val.ToUpper())
                    {
                        case "P": return "P — Pass";
                        case "F": return "F — Fail";
                        case "-": return "— — Not processed";
                        default: return val;
                    }
                case MessageType.UNIT_CHECKIN:
                    switch (val.ToUpper())
                    {
                        case "Y": return "Y — Process";
                        case "N": return "N — Reject";
                        case "G": return "G — Golden sample";
                        case "R": return "R — Rework";
                        case "T": return "T — Test";
                        default: return val;
                    }
                default:
                    return val;
            }
        }

        private static Dictionary<string, Color> AssignStationColors(List<RenderGroup> groups)
        {
            var result = new Dictionary<string, Color>();
            int idx = 0;
            foreach (var g in groups)
                if (!string.IsNullOrEmpty(g.Station) && !result.ContainsKey(g.Station))
                    result[g.Station] = StationPalette[idx++ % StationPalette.Length];
            return result;
        }


        private static string ResolveStation(string equipId, Dictionary<string, string> equipNames)
        {
            if (string.IsNullOrEmpty(equipId)) return "";
            if (equipNames != null && equipNames.TryGetValue(equipId, out var n)) return n;
            return equipId;
        }

        private static bool IsError(ResponseRecord r) =>
            r.Result != null && (
                r.Result.StartsWith("[ERR", StringComparison.OrdinalIgnoreCase) ||
                r.Result.StartsWith("ERR", StringComparison.OrdinalIgnoreCase) ||
                r.Result.Equals("ERROR", StringComparison.OrdinalIgnoreCase));

        private static Color ResultColor(string r)
        {
            switch (r)
            {
                case "Y":
                case "P":
                case "G": return CGreen;
                case "N":
                case "F": return CRed;
                default: return CDim;
            }
        }

        private static string GetResult(ResponseRecord r)
        {
            if (r.Type == MessageType.SEMI_VALIDATION2) return r.ProcDirAssy;
            return r.Result;
        }

        private static string FormatType(MessageType t)
        {
            switch (t)
            {
                case MessageType.UNIT_INFO: return "UNIT INFO";
                case MessageType.NEXT_OPERATION: return "NEXT OPERATION";
                case MessageType.UNIT_CHECKIN: return "UNIT CHECKIN";
                case MessageType.UNIT_RESULT: return "UNIT RESULT";
                case MessageType.SEMI_VALIDATION2: return "SEMI VALIDATION";
                case MessageType.REQ_SETUP_CHANGE2: return "SETUP CHANGE";
                case MessageType.LOAD_MATERIAL: return "LOAD MATERIAL";
                case MessageType.REQ_MATERIAL_INFO: return "MATERIAL INFO";
                case MessageType.REQ_LOADED_MATERIAL: return "LOADED MATERIAL";
                case MessageType.PANEL_CHECKIN: return "PANEL CHECKIN";
                case MessageType.PANEL_RESULT: return "PANEL RESULT";
                default: return t.ToString().Replace("_", " ");
            }
        }

        private static string TypeIcon(MessageType t)
        {
            switch (t)
            {
                case MessageType.UNIT_INFO: return "🛈";
                case MessageType.NEXT_OPERATION: return "❱❱";
                case MessageType.UNIT_CHECKIN: return "↪";
                case MessageType.UNIT_RESULT: return "✦";
                case MessageType.SEMI_VALIDATION2: return "⛓";
                case MessageType.REQ_SETUP_CHANGE2: return "⛭";
                case MessageType.LOAD_MATERIAL: return "↑";
                case MessageType.REQ_MATERIAL_INFO: return "▤";
                case MessageType.REQ_LOADED_MATERIAL: return "▤";
                case MessageType.PANEL_CHECKIN: return "⛶";
                case MessageType.PANEL_RESULT: return "✓";
                default: return "●";
            }
        }

        private static string TruncUid(string uid)
        {
            if (string.IsNullOrEmpty(uid)) return "";
            return uid.Length > 20 ? uid.Substring(0, 8) + "…" + uid.Substring(uid.Length - 6) : uid;
        }

        private static Border Chip(string text, Color col, int fs = 9) => new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(25, col.R, col.G, col.B)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(70, col.R, col.G, col.B)),
            BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(3),
            Padding = new Thickness(8, 3, 8, 3), Margin = new Thickness(10, 0, 0, 0),
            Child = new TextBlock
            {
                Text = text, FontSize = fs, FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromArgb(200, col.R, col.G, col.B)),
                VerticalAlignment = VerticalAlignment.Center
            }
        };


        private class RenderGroup
        {
            public bool IsSemi { get; set; }
            public DiagramNode ParentNode { get; set; }
            public List<CompNode> Components { get; set; }
            public string Day { get; set; }
            public string Station { get; set; }
            public double? GapFromPrevSec { get; set; }
        }

        private class DiagramNode
        {
            public ResponseRecord Record { get; set; }
            public string EquipId { get; set; }
        }

        private class CompNode
        {
            public string UidAssy { get; set; }
            public string UidAssyType { get; set; }
            public string ProcDir { get; set; }
            public DateTime Timestamp { get; set; }
        }
    }
}