using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using LiveCharts;
using LiveCharts.Defaults;
using LiveCharts.Wpf;
using MESInsight.Charts.Builders;
using MESInsight.Core;
using MESInsight.UI;

namespace MESInsight.Charts.Renderers
{
    public class TrendChartRenderer : ChartRenderer
    {
        private readonly DayRecordsPanelBuilder _dayRecordsPanelBuilder;

        private readonly Dictionary<MessageType, (Border panel, ColumnDefinition col, bool open)>
            _dayRecordsPanelByMessageType;

        private readonly Dictionary<MessageType, CartesianChart> _trendChartByMessageType;
        private readonly Dictionary<MessageType, LiveCharts.Wpf.AxisSection> _selectedDayHighlightByMessageType;
        private readonly Dictionary<MessageType, (Border container, StackPanel panel)> _timelineContainerByMessageType;
        private readonly ChartFactory _chartFactory;
        private readonly Dictionary<DateTime, List<ResponseRecord>> _recordsGroupedByDay;
        private readonly List<ResponseRecord> _filteredRecords;
        private readonly Action<MessageType> _onShowAllRecordsRequested;

        private static readonly Color[] MonthAccentColors =
        {
            Color.FromRgb(52, 152, 219), Color.FromRgb(46, 204, 113),
            Color.FromRgb(155, 89, 182), Color.FromRgb(241, 196, 15),
            Color.FromRgb(230, 126, 34), Color.FromRgb(231, 76, 60)
        };

        public TrendChartRenderer(
            DayRecordsPanelBuilder dayRecordsPanelBuilder,
            Dictionary<MessageType, (Border panel, ColumnDefinition col, bool open)> dayRecordsPanelByMessageType,
            Dictionary<MessageType, CartesianChart> trendChartByMessageType,
            Dictionary<MessageType, LiveCharts.Wpf.AxisSection> selectedDayHighlightByMessageType,
            Dictionary<MessageType, (Border container, StackPanel panel)> timelineContainerByMessageType,
            ChartFactory chartFactory,
            Dictionary<DateTime, List<ResponseRecord>> recordsGroupedByDay,
            List<ResponseRecord> filteredRecords,
            Action<MessageType> onShowAllRecordsRequested)
        {
            _dayRecordsPanelBuilder = dayRecordsPanelBuilder;
            _dayRecordsPanelByMessageType = dayRecordsPanelByMessageType;
            _trendChartByMessageType = trendChartByMessageType;
            _selectedDayHighlightByMessageType = selectedDayHighlightByMessageType;
            _timelineContainerByMessageType = timelineContainerByMessageType;
            _chartFactory = chartFactory;
            _recordsGroupedByDay = recordsGroupedByDay;
            _filteredRecords = filteredRecords;
            _onShowAllRecordsRequested = onShowAllRecordsRequested;
        }

        public override ChartType GetChartType() => ChartType.Trend;
        public override int GetMinimumHeight(RenderContext context) => (int)(context.AvailableHeightPixels * 0.60);

        public override UIElement Render(ChartData data, RenderContext context)
        {
            if (data?.TrendChart == null) return null;

            var trendChartSeries = data.TrendChart;
            var messageType = context.MessageType;
            int containerHeight = (int)(context.AvailableHeightPixels * 0.60);

            var trendChart = CreateConfiguredTrendChart(trendChartSeries);
            var titleBar = BuildTrendChartTitleBar(trendChartSeries.Name, messageType);
            var dayRecordsPanel = _dayRecordsPanelBuilder.BuildEmptyDayRecordsPanel();
            var reservedColumn = new ColumnDefinition { Width = new GridLength(0) };

            var chartArea = ArrangeChartWithOverlaysAndMonthBar(trendChart, trendChartSeries);
            var chartWithPanel = ArrangeChartNextToDayRecordsPanel(chartArea, dayRecordsPanel, reservedColumn);
            var outerGrid = ArrangeTitleBarAboveTrendChart(titleBar, chartWithPanel, containerHeight);

            AttachRevealAnimationOnFirstLoad(trendChart);
            AttachZoomAndPanControls(trendChart, titleBar);

            _trendChartByMessageType[messageType] = trendChart;
            _dayRecordsPanelByMessageType[messageType] = (dayRecordsPanel, reservedColumn, false);

            _dayRecordsPanelBuilder.WireClosePanelButton(dayRecordsPanel, () =>
            {
                _dayRecordsPanelByMessageType[messageType] = (dayRecordsPanel, reservedColumn, false);
                trendChart.AxisX[0].MinValue = double.NaN;
                trendChart.AxisX[0].MaxValue = double.NaN;
                ClearDayHighlight(trendChart, messageType);
                _dayRecordsPanelBuilder.AnimateSlideClose(dayRecordsPanel, reservedColumn);
            });

            WireDataPointClickHandler(trendChart, trendChartSeries, reservedColumn, dayRecordsPanel, messageType);

            var timelineSection = BuildTimelineSection(messageType);
            var wrapper = new StackPanel();
            wrapper.Children.Add(WrapInSectionBorder(outerGrid, isHistogram: false));
            wrapper.Children.Add(timelineSection);
            return wrapper;
        }

        public void UpdateTimelineForDay(MessageType messageType, DateTime selectedDay)
        {
            if (!_timelineContainerByMessageType.ContainsKey(messageType)) return;
            var (container, panel) = _timelineContainerByMessageType[messageType];
            panel.Children.Clear();

            if (!_recordsGroupedByDay.TryGetValue(selectedDay.Date, out List<ResponseRecord> allRecordsForDay)
                || allRecordsForDay.Count == 0)
            {
                panel.Children.Add(new TextBlock
                {
                    Text = "No records for " + selectedDay.ToString("dd.MM.yyyy"),
                    FontSize = 11,
                    Foreground = new SolidColorBrush(Color.FromRgb(100, 110, 120)),
                    Margin = new Thickness(4, 4, 0, 4)
                });
                return;
            }

            var timelineData = _chartFactory.Build(ChartType.Timeline, allRecordsForDay, MessageType.ALL);
            if (timelineData?.TimelineEvents == null) return;

            var renderer = new TimelineChartRenderer();
            panel.Children.Add(renderer.Render(timelineData, new RenderContext { MessageType = messageType }));
        }

        public void InitializeTimelineWithFirstAvailableDay(MessageType messageType)
        {
            if (_recordsGroupedByDay.Count == 0) return;
            if (!_timelineContainerByMessageType.ContainsKey(messageType)) return;
            UpdateTimelineForDay(messageType, _recordsGroupedByDay.Keys.OrderBy(d => d).First());
        }

        // ── Layout ──────────────────────────────────────────────────────────

        private Grid ArrangeChartWithOverlaysAndMonthBar(CartesianChart trendChart, ChartSeries trendChartSeries)
        {
            var container = new Grid();
            container.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            container.RowDefinitions.Add(new RowDefinition { Height = new GridLength(0) });

            var monthBar = BuildMonthColorBar(trendChartSeries);
            var gapOverlay = BuildGapHatchOverlay(trendChart, trendChartSeries);

            AddGapSectionsToAxis(trendChart.AxisX[0], trendChartSeries);

            Grid.SetRow(trendChart, 0);
            Grid.SetRow(monthBar, 1);
            container.Children.Add(trendChart);

            if (gapOverlay != null)
            {
                Grid.SetRow(gapOverlay, 0);
                container.Children.Add(gapOverlay);
                var capturedOverlay = gapOverlay;
                var capturedSeries = trendChartSeries;
                trendChart.Loaded += (s, e) =>
                    trendChart.Dispatcher.BeginInvoke(
                        new Action<CartesianChart, Canvas, ChartSeries>(RedrawGapOverlay),
                        System.Windows.Threading.DispatcherPriority.Loaded,
                        trendChart, capturedOverlay, capturedSeries);
            }

            container.Children.Add(monthBar);
            return container;
        }

        private static Grid ArrangeChartNextToDayRecordsPanel(Grid chartArea, Border dayRecordsPanel,
            ColumnDefinition reservedColumn)
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(reservedColumn);
            Grid.SetColumn(chartArea, 0);
            Grid.SetColumn(dayRecordsPanel, 1);
            grid.Children.Add(chartArea);
            grid.Children.Add(dayRecordsPanel);
            return grid;
        }

        private static Grid ArrangeTitleBarAboveTrendChart(Panel titleBar, Grid chartWithPanel, int totalHeightPixels)
        {
            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(34) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(totalHeightPixels - 34) });
            Grid.SetRow(titleBar, 0);
            Grid.SetRow(chartWithPanel, 1);
            grid.Children.Add(titleBar);
            grid.Children.Add(chartWithPanel);
            return grid;
        }

        private Border BuildTimelineSection(MessageType messageType)
        {
            var contentPanel = new StackPanel();
            var sectionBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(13, 17, 23)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(22, 60, 36)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Margin = new Thickness(0, 4, 0, 0),
                Padding = new Thickness(8, 6, 8, 6),
                Child = contentPanel
            };
            _timelineContainerByMessageType[messageType] = (sectionBorder, contentPanel);
            return sectionBorder;
        }

        // ── Data Click & Highlight ───────────────────────────────────────────

        private void WireDataPointClickHandler(
            CartesianChart trendChart,
            ChartSeries trendChartSeries,
            ColumnDefinition dayRecordsPanelColumn,
            Border dayRecordsPanel,
            MessageType messageType)
        {
            var xAxis = trendChart.AxisX[0];

            DateTime _lastClick = DateTime.MinValue;
            DateTime _lastRealDate = DateTime.MinValue;

            trendChart.DataClick += async (sender, clickedPoint) =>
            {
                if (clickedPoint == null) return;

                bool isDoubleClick = (DateTime.UtcNow - _lastClick).TotalMilliseconds < 400;
                _lastClick = DateTime.UtcNow;

                long clickedValue = (long)clickedPoint.X;
                DateTime realDate;
                if (trendChartSeries.CompressedAxisValueToCalendarDate == null ||
                    !trendChartSeries.CompressedAxisValueToCalendarDate.TryGetValue(clickedValue, out realDate))
                    realDate = new DateTime(clickedValue);

                if (isDoubleClick)
                {
                    OpenDayDetailWindow(realDate, messageType);
                    return;
                }

                _lastRealDate = realDate;

                xAxis.MinValue = clickedValue - TimeSpan.FromDays(20).Ticks;
                xAxis.MaxValue = clickedValue + TimeSpan.FromDays(20).Ticks;

                HighlightSelectedDay(trendChart, messageType, clickedValue);
                UpdateTimelineForDay(messageType, realDate);

                var recordsForDay = _recordsGroupedByDay.TryGetValue(realDate.Date, out var allForDay)
                    ? allForDay.Where(r => r.Type == messageType).ToList()
                    : new List<ResponseRecord>();

                var currentState = _dayRecordsPanelByMessageType[messageType];
                _dayRecordsPanelBuilder.ShowLoadingSpinner(dayRecordsPanel, realDate, recordsForDay.Count,
                    showingAllRecords: false);

                if (!currentState.open)
                {
                    _dayRecordsPanelByMessageType[messageType] = (dayRecordsPanel, dayRecordsPanelColumn, true);
                    _dayRecordsPanelBuilder.AnimateSlideOpen(dayRecordsPanel, dayRecordsPanelColumn);
                    await Task.Delay(480);
                }

                await Task.Run(() => System.Threading.Thread.Sleep(1));
                _dayRecordsPanelBuilder.PopulateWithDayRecords(dayRecordsPanel, realDate, recordsForDay,
                    showingAllRecords: false);
            };
        }

        private void OpenDayDetailWindow(DateTime day, MessageType messageType)
        {
            var records = _recordsGroupedByDay.TryGetValue(day.Date, out var allForDay)
                ? allForDay.Where(r => r.Type == messageType).ToList()
                : new List<ResponseRecord>();

            double avg = records.Count > 0 ? records.Average(r => r.ResponseTime) : 0;
            int p95 = records.Count > 0
                ? records.OrderBy(r => r.ResponseTime).ElementAt((int)(records.Count * 0.95)).ResponseTime
                : 0;
            int min = records.Count > 0 ? records.Min(r => r.ResponseTime) : 0;
            int max = records.Count > 0 ? records.Max(r => r.ResponseTime) : 0;

            var outerPanel = new StackPanel { Margin = new Thickness(0) };

            var headerBar = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(13, 17, 23)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(22, 60, 36)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(16, 12, 16, 12)
            };
            var headerContent = new StackPanel();
            headerContent.Children.Add(new TextBlock
            {
                Text = day.ToString("dddd, dd. MMMM yyyy", new System.Globalization.CultureInfo("en-US")),
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(63, 185, 80))
            });
            headerContent.Children.Add(new TextBlock
            {
                Text = messageType.ToString().Replace("_", " ") + "  ·  " + records.Count.ToString("N0") + " records",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(139, 148, 158)),
                Margin = new Thickness(0, 2, 0, 0)
            });
            headerBar.Child = headerContent;
            outerPanel.Children.Add(headerBar);

            var statsRow = new WrapPanel { Margin = new Thickness(16, 12, 16, 4) };
            statsRow.Children.Add(BuildDetailStatChip("AVG", avg.ToString("F0") + " ms", Color.FromRgb(56, 182, 255)));
            statsRow.Children.Add(BuildDetailStatChip("P95", p95 + " ms", Color.FromRgb(188, 140, 255)));
            statsRow.Children.Add(BuildDetailStatChip("Min", min + " ms", Color.FromRgb(46, 160, 67)));
            statsRow.Children.Add(BuildDetailStatChip("Max", max + " ms", Color.FromRgb(248, 81, 73)));
            statsRow.Children.Add(BuildDetailStatChip("Records", records.Count.ToString("N0"),
                Color.FromRgb(139, 148, 158)));
            outerPanel.Children.Add(statsRow);

            var timelineData = _chartFactory.Build(ChartType.Timeline, allForDay ?? new List<ResponseRecord>(),
                MessageType.ALL);
            if (timelineData?.TimelineEvents != null)
            {
                var timelineElement = TimelineChartRenderer.BuildTimelineCanvas(timelineData.TimelineEvents, day, 0);
                var timelineWrapper = new Border { Margin = new Thickness(12, 4, 12, 8), Child = timelineElement };
                outerPanel.Children.Add(timelineWrapper);
            }

            var recordsScroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Margin = new Thickness(12, 0, 12, 12)
            };
            var recordsList = new StackPanel();
            foreach (var r in records.OrderBy(x => x.TimestampParsed))
            {
                var card = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(22, 27, 34)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(33, 38, 45)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(12, 8, 12, 8),
                    Margin = new Thickness(0, 0, 0, 4)
                };

                var cardGrid = new Grid();
                cardGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                cardGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var leftStack = new StackPanel();
                leftStack.Children.Add(new TextBlock
                {
                    Text = r.TimestampParsed.ToString("HH:mm:ss.fff"),
                    FontSize = 11,
                    FontFamily = new FontFamily("Consolas"),
                    Foreground = new SolidColorBrush(Color.FromRgb(139, 148, 158))
                });
                if (!string.IsNullOrEmpty(r.Uid ?? r.UidIn))
                    leftStack.Children.Add(new TextBlock
                    {
                        Text = "UID: " + (r.Uid ?? r.UidIn),
                        FontSize = 11,
                        Foreground = new SolidColorBrush(Color.FromRgb(201, 209, 217)),
                        Margin = new Thickness(0, 2, 0, 0)
                    });
                if (!string.IsNullOrEmpty(r.Result))
                    leftStack.Children.Add(new TextBlock
                    {
                        Text = "Result: " + r.Result,
                        FontSize = 11,
                        Foreground = r.Result == "F"
                            ? new SolidColorBrush(Color.FromRgb(248, 81, 73))
                            : new SolidColorBrush(Color.FromRgb(46, 160, 67)),
                        Margin = new Thickness(0, 2, 0, 0)
                    });

                bool isSlow = r.ResponseTime > avg * 1.5;
                var rtColor = isSlow ? Color.FromRgb(248, 81, 73) : Color.FromRgb(56, 139, 253);
                var rtBadge = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(40, rtColor.R, rtColor.G, rtColor.B)),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(100, rtColor.R, rtColor.G, rtColor.B)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(8, 4, 8, 4),
                    VerticalAlignment = VerticalAlignment.Center,
                    Child = new TextBlock
                    {
                        Text = r.ResponseTime + " ms",
                        FontSize = 13,
                        FontWeight = FontWeights.Bold,
                        Foreground = new SolidColorBrush(Color.FromArgb(220, rtColor.R, rtColor.G, rtColor.B))
                    }
                };

                Grid.SetColumn(leftStack, 0);
                Grid.SetColumn(rtBadge, 1);
                cardGrid.Children.Add(leftStack);
                cardGrid.Children.Add(rtBadge);
                card.Child = cardGrid;
                recordsList.Children.Add(card);
            }

            recordsScroll.Content = recordsList;
            outerPanel.Children.Add(recordsScroll);

            var window = new Window
            {
                Title = day.ToString("dd.MM.yyyy") + "  —  " + messageType.ToString().Replace("_", " "),
                Width = 680,
                Height = 780,
                MinWidth = 500,
                MinHeight = 400,
                Background = new SolidColorBrush(Color.FromRgb(13, 17, 23)),
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Content = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(13, 17, 23)),
                    Child = outerPanel
                }
            };
            window.Show();
            window.Dispatcher.BeginInvoke(new Action(() =>
            {
                window.Activate();
                window.Topmost = true;
                window.Topmost = false;
            }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        }

        private static Border BuildDetailStatChip(string label, string value, Color color)
        {
            var chip = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(40, color.R, color.G, color.B)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(80, color.R, color.G, color.B)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(10, 5, 10, 5),
                Margin = new Thickness(0, 0, 8, 8)
            };
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(new TextBlock
            {
                Text = label + "  ",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(139, 148, 158))
            });
            row.Children.Add(new TextBlock
            {
                Text = value,
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromArgb(220, color.R, color.G, color.B))
            });
            chip.Child = row;
            return chip;
        }

        private void HighlightSelectedDay(CartesianChart chart, MessageType messageType, long compressedValue)
        {
            ClearDayHighlight(chart, messageType);
            var section = new LiveCharts.Wpf.AxisSection
            {
                Value = compressedValue - TimeSpan.FromHours(12).Ticks,
                SectionWidth = TimeSpan.FromDays(1).Ticks,
                Fill = new SolidColorBrush(Color.FromArgb(45, 79, 195, 247)),
                Stroke = new SolidColorBrush(Color.FromArgb(180, 79, 195, 247)),
                StrokeThickness = 2
            };
            chart.AxisX[0].Sections.Add(section);
            _selectedDayHighlightByMessageType[messageType] = section;
        }

        private void ClearDayHighlight(CartesianChart chart, MessageType messageType)
        {
            if (!_selectedDayHighlightByMessageType.TryGetValue(messageType, out var section)) return;
            _selectedDayHighlightByMessageType.Remove(messageType);
            try
            {
                if (chart.AxisX != null && chart.AxisX.Count > 0 && chart.AxisX[0].Sections != null
                    && chart.AxisX[0].Sections.Contains(section))
                    chart.AxisX[0].Sections.Remove(section);
            }
            catch
            {
            }
        }

        // ── Chart Configuration ─────────────────────────────────────────────

        private CartesianChart CreateConfiguredTrendChart(ChartSeries trendChartSeries)
        {
            var chart = new CartesianChart
            {
                Series = trendChartSeries.Series,
                LegendLocation = LegendLocation.Bottom,
                Margin = new Thickness(10, 0, 10, 10),
                DisableAnimations = true,
                Zoom = ZoomingOptions.X,
                Pan = PanningOptions.None,
                AnimationsSpeed = TimeSpan.FromMilliseconds(1),
                DataTooltip = BuildDarkStyledTooltip()
            };

            var dateAxis = CreateDateAxis(
                trendChartSeries.RemappedGaps,
                trendChartSeries.CompressedAxisValueToCalendarDate,
                trendChartSeries.GapLabels,
                trendChartSeries.GapCenterAxisValues);

            AddMonthSeparatorSections(dateAxis, trendChartSeries);
            AttachMonthColoredAxisLabelUpdater(chart);
            AttachDynamicPointSizeUpdater(chart, dateAxis);

            chart.AxisY.Add(CreateResponseTimeAxis(CalcMinimumYValue(trendChartSeries)));
            chart.AxisX.Add(dateAxis);
            return chart;
        }

        private Axis CreateDateAxis(
            List<(long From, long To)> gapRanges,
            Dictionary<long, DateTime> compressedToCalendarMap,
            List<string> gapLabels,
            List<long> gapCenterValues)
        {
            var axis = new Axis
            {
                Title = "Date", FontSize = 13, FontWeight = FontWeights.Normal,
                Foreground = new SolidColorBrush(Color.FromRgb(139, 148, 158)), LabelsRotation = 0
            };
            var enUS = new System.Globalization.CultureInfo("en-US");
            var sortedKeys = compressedToCalendarMap != null
                ? compressedToCalendarMap.Keys.OrderBy(k => k).ToList()
                : new List<long>();

            axis.LabelFormatter = tickValue =>
            {
                long ticks = (long)tickValue;
                if (gapRanges != null)
                    foreach (var gap in gapRanges)
                        if (ticks >= gap.From && ticks <= gap.To)
                            return "";

                DateTime displayDate = ResolveCalendarDate(ticks, compressedToCalendarMap, sortedKeys);

                if (axis.ActualMaxValue > 0 && axis.ActualMinValue > 0)
                {
                    double visibleDays = (axis.ActualMaxValue - axis.ActualMinValue) / TimeSpan.FromDays(1).Ticks;
                    if (visibleDays > 90) return displayDate.Day == 1 ? displayDate.ToString("MMM yyyy", enUS) : "";
                    if (visibleDays > 30) return displayDate.Day % 7 == 1 ? displayDate.ToString("dd.MM", enUS) : "";
                    return displayDate.ToString("dd.MM", enUS);
                }

                return displayDate.ToString("dd.MM", enUS);
            };

            axis.Separator = new LiveCharts.Wpf.Separator { Step = TimeSpan.FromDays(1).Ticks, IsEnabled = false };
            return axis;
        }

        private static DateTime ResolveCalendarDate(long compressedValue, Dictionary<long, DateTime> map,
            List<long> sortedKeys)
        {
            if (map == null || sortedKeys.Count == 0) return new DateTime(compressedValue);
            if (map.TryGetValue(compressedValue, out DateTime exact)) return exact;

            int lo = 0, hi = sortedKeys.Count - 1;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) / 2;
                if (sortedKeys[mid] <= compressedValue) lo = mid;
                else hi = mid - 1;
            }

            long nearest = sortedKeys[lo];
            long offset = (compressedValue - nearest + TimeSpan.FromDays(1).Ticks / 2) / TimeSpan.FromDays(1).Ticks;
            return map[nearest].AddDays(offset);
        }

        private static Axis CreateResponseTimeAxis(double minimumValue)
        {
            return new Axis
            {
                Title = "Response Time (ms)", MinValue = minimumValue, FontSize = 13,
                Foreground = new SolidColorBrush(Color.FromRgb(139, 148, 158)), FontWeight = FontWeights.Normal,
                LabelFormatter = v => v.ToString("N0"),
                Separator = new LiveCharts.Wpf.Separator
                    { StrokeThickness = 0.5, Stroke = new SolidColorBrush(Color.FromArgb(25, 255, 255, 255)) }
            };
        }

        private static double CalcMinimumYValue(ChartSeries trendChartSeries)
        {
            double lowest = double.MaxValue;
            foreach (var series in trendChartSeries.Series)
            {
                IChartValues values = null;
                if (series is LineSeries ls) values = ls.Values;
                if (series is ScatterSeries ss) values = ss.Values;
                if (values == null) continue;

                foreach (var pt in values)
                {
                    double? val = null;
                    if (pt is DateTimePoint dtp) val = dtp.Value;
                    if (pt is ObservablePoint op) val = op.Y;
                    if (val != null && val > 0 && val < lowest) lowest = val.Value;
                }
            }

            if (lowest == double.MaxValue || lowest <= 0) return 0;
            return Math.Max(0, Math.Floor((lowest * 0.85) / 5) * 5);
        }

        private static void AddGapSectionsToAxis(Axis dateAxis, ChartSeries trendChartSeries)
        {
            if (trendChartSeries.RemappedGaps == null) return;
            foreach (var gap in trendChartSeries.RemappedGaps)
                dateAxis.Sections.Add(new AxisSection
                {
                    Value = gap.From, SectionWidth = gap.To - gap.From,
                    Fill = new SolidColorBrush(Color.FromArgb(18, 200, 200, 200)),
                    Stroke = new SolidColorBrush(Color.FromArgb(60, 150, 150, 150)), StrokeThickness = 1
                });
        }

        private void AddMonthSeparatorSections(Axis dateAxis, ChartSeries trendChartSeries)
        {
            var firstSeries = trendChartSeries.Series[0] as LineSeries;
            if (firstSeries?.Values == null || firstSeries.Values.Count == 0) return;

            var allDates = firstSeries.Values.OfType<DateTimePoint>().Select(d => d.DateTime).OrderBy(d => d).ToList();
            if (allDates.Count == 0) return;

            var boundaries = new List<(long ticks, int month)>();
            int lastMonth = -1;
            long startTicks = 0;
            foreach (var date in allDates)
            {
                if (lastMonth != date.Month)
                {
                    if (lastMonth != -1) boundaries.Add((startTicks, lastMonth));
                    startTicks = date.Ticks;
                    lastMonth = date.Month;
                }
            }

            if (lastMonth != -1) boundaries.Add((startTicks, lastMonth));

            var sections = new LiveCharts.Wpf.SectionsCollection();
            for (int i = 1; i < boundaries.Count; i++)
            {
                var mc = GetMonthAccentColor(boundaries[i].month);
                sections.Add(new LiveCharts.Wpf.AxisSection
                {
                    Value = boundaries[i].ticks, SectionWidth = 0,
                    Stroke = new SolidColorBrush(Color.FromArgb(120, mc.R, mc.G, mc.B)),
                    StrokeThickness = 2, StrokeDashArray = new DoubleCollection { 4, 5 }
                });
            }

            dateAxis.Sections = sections;
        }

        private void AttachMonthColoredAxisLabelUpdater(CartesianChart chart)
        {
            var enUS = new System.Globalization.CultureInfo("en-US");
            var formats = new[] { "MMM yyyy", "dd.MM" };

            chart.UpdaterTick += _ =>
                chart.Dispatcher.BeginInvoke(new Action(() =>
                {
                    foreach (var tb in FindVisualChildren<TextBlock>(chart))
                    {
                        if (string.IsNullOrWhiteSpace(tb.Text)) continue;
                        foreach (var fmt in formats)
                        {
                            if (DateTime.TryParseExact(tb.Text.Trim(), fmt, enUS,
                                    System.Globalization.DateTimeStyles.None, out DateTime d))
                            {
                                tb.Foreground = new SolidColorBrush(GetMonthAccentColor(d.Month));
                                tb.FontSize = 15;
                                break;
                            }
                        }
                    }
                }), System.Windows.Threading.DispatcherPriority.Render);
        }

        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) yield break;
            int count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is T t) yield return t;
                foreach (var gc in FindVisualChildren<T>(child)) yield return gc;
            }
        }

        private void AttachDynamicPointSizeUpdater(CartesianChart chart, Axis dateAxis)
        {
            chart.UpdaterTick += delegate
            {
                chart.Dispatcher.BeginInvoke(
                    new Action<CartesianChart, Axis>(UpdatePointSizes),
                    System.Windows.Threading.DispatcherPriority.Background,
                    chart, dateAxis);
            };
        }

        private static void UpdatePointSizes(CartesianChart chart, Axis dateAxis)
        {
            double visMin = double.IsNaN(dateAxis.MinValue) ? double.NegativeInfinity : dateAxis.MinValue;
            double visMax = double.IsNaN(dateAxis.MaxValue) ? double.PositiveInfinity : dateAxis.MaxValue;

            int count = 0;
            foreach (var series in chart.Series)
            {
                var ls = series as LineSeries;
                if (ls?.Values == null) continue;
                foreach (var pt in ls.Values)
                {
                    var dtp = pt as DateTimePoint;
                    if (dtp != null && dtp.DateTime.Ticks >= visMin && dtp.DateTime.Ticks <= visMax) count++;
                }

                break;
            }

            if (count == 0)
                foreach (var s in chart.Series)
                {
                    var ls = s as LineSeries;
                    if (ls?.Values != null)
                    {
                        count = ls.Values.Count;
                        break;
                    }
                }

            double size = TrendChart.CalcPointSize(count);
            foreach (var s in chart.Series)
            {
                var ss = s as ScatterSeries;
                if (ss != null && (ss.Title == "AVG" || ss.Title == "P95"))
                {
                    ss.MinPointShapeDiameter = size;
                    ss.MaxPointShapeDiameter = size;
                }
            }
        }

        // ── Gap Overlay ──────────────────────────────────────────────────────

        private Canvas BuildGapHatchOverlay(CartesianChart chart, ChartSeries trendChartSeries)
        {
            if (trendChartSeries.RemappedGaps == null || trendChartSeries.RemappedGaps.Count == 0) return null;
            var overlay = new Canvas { IsHitTestVisible = false, Background = Brushes.Transparent };
            chart.UpdaterTick += delegate { RedrawGapOverlay(chart, overlay, trendChartSeries); };
            return overlay;
        }

        private static void RedrawGapOverlay(CartesianChart chart, Canvas overlay, ChartSeries trendChartSeries)
        {
            chart.Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    overlay.Children.Clear();
                    if (chart.AxisX.Count == 0) return;

                    var xAxis = chart.AxisX[0];
                    double visMin, visMax;
                    try
                    {
                        visMin = xAxis.ActualMinValue;
                        visMax = xAxis.ActualMaxValue;
                    }
                    catch
                    {
                        return;
                    }

                    if (double.IsNaN(visMin) || double.IsNaN(visMax) || visMin == visMax ||
                        (visMin == 0 && visMax == 0)) return;

                    double range = visMax - visMin;
                    double chartLeft = 60, chartRight = overlay.ActualWidth - 20;
                    double chartW = chartRight - chartLeft;
                    double chartTop = 10, chartBot = overlay.ActualHeight - 40;
                    double chartH = chartBot - chartTop;
                    if (chartW <= 0 || chartH <= 0) return;

                    var hatchBrush = new SolidColorBrush(Color.FromArgb(120, 110, 110, 110));
                    var fillBrush = new SolidColorBrush(Color.FromArgb(55, 130, 130, 130));

                    for (int gi = 0; gi < trendChartSeries.RemappedGaps.Count; gi++)
                    {
                        var gap = trendChartSeries.RemappedGaps[gi];
                        double x1 = chartLeft + ((gap.From - visMin) / range) * chartW;
                        double x2 = chartLeft + ((gap.To - visMin) / range) * chartW;
                        if (x2 < chartLeft || x1 > chartRight) continue;

                        x1 = Math.Max(x1, chartLeft);
                        x2 = Math.Min(x2, chartRight);
                        double inset = Math.Min(15, (x2 - x1) * 0.25);
                        double hx1 = x1 + inset, hx2 = x2 - inset;
                        double gapW = hx2 - hx1;
                        if (gapW <= 0) continue;

                        var bg = new Rectangle
                            { Width = gapW, Height = chartH, Fill = fillBrush, IsHitTestVisible = false };
                        Canvas.SetLeft(bg, hx1);
                        Canvas.SetTop(bg, chartTop);
                        overlay.Children.Add(bg);

                        for (double s = hx1 - chartH; s < hx2; s += 12)
                        {
                            double ax1 = Math.Max(s, hx1), ay1 = s < hx1 ? chartTop + (hx1 - s) : chartTop;
                            double ax2 = Math.Min(s + chartH, hx2),
                                ay2 = (s + chartH) > hx2 ? chartTop + (hx2 - s) : chartBot;
                            if (ax1 < ax2)
                                overlay.Children.Add(new Line
                                {
                                    X1 = ax1, Y1 = ay1, X2 = ax2, Y2 = ay2, Stroke = hatchBrush, StrokeThickness = 1,
                                    IsHitTestVisible = false
                                });

                            double bx1 = Math.Max(s, hx1), by1 = s < hx1 ? chartBot - (hx1 - s) : chartBot;
                            double bx2 = Math.Min(s + chartH, hx2),
                                by2 = (s + chartH) > hx2 ? chartBot - (hx2 - s) : chartTop;
                            if (bx1 < bx2)
                                overlay.Children.Add(new Line
                                {
                                    X1 = bx1, Y1 = by1, X2 = bx2, Y2 = by2, Stroke = hatchBrush, StrokeThickness = 1,
                                    IsHitTestVisible = false
                                });
                        }

                        if (trendChartSeries.GapLabels != null && gi < trendChartSeries.GapLabels.Count && gapW > 10)
                        {
                            var lbl = new TextBlock
                            {
                                Text = trendChartSeries.GapLabels[gi], FontSize = 11, FontWeight = FontWeights.SemiBold,
                                Foreground = new SolidColorBrush(Color.FromRgb(160, 160, 160)), IsHitTestVisible = false
                            };
                            lbl.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                            Canvas.SetLeft(lbl, hx1 + (gapW - lbl.DesiredSize.Width) / 2);
                            Canvas.SetTop(lbl, chartBot + 4);
                            overlay.Children.Add(lbl);
                        }
                    }
                }
                catch
                {
                }
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        // ── Month Color Bar ──────────────────────────────────────────────────

        private UIElement BuildMonthColorBar(ChartSeries trendChartSeries)
        {
            var container = new Grid { Margin = new Thickness(60, 0, 20, 0) };
            container.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
            var canvas = new Canvas { Height = 25, Background = new SolidColorBrush(Color.FromRgb(13, 17, 23)) };

            var firstSeries = trendChartSeries.Series[0] as LineSeries;
            if (firstSeries?.Values == null || firstSeries.Values.Count == 0)
            {
                container.Children.Add(canvas);
                return container;
            }

            var compressedDates = firstSeries.Values.OfType<DateTimePoint>().Select(d => d.DateTime).OrderBy(d => d)
                .ToList();
            if (compressedDates.Count == 0)
            {
                container.Children.Add(canvas);
                return container;
            }

            long earliestTicks = compressedDates[0].Ticks;
            long totalSpan = compressedDates[compressedDates.Count - 1].Ticks - earliestTicks;

            var monthStarts = new List<(long ticks, int month)>();
            int lastMonth = -1;
            foreach (var d in compressedDates)
            {
                DateTime real = trendChartSeries.CompressedAxisValueToCalendarDate != null &&
                                trendChartSeries.CompressedAxisValueToCalendarDate.TryGetValue(d.Ticks, out DateTime rd)
                    ? rd
                    : d;
                if (lastMonth != real.Month)
                {
                    monthStarts.Add((d.Ticks, real.Month));
                    lastMonth = real.Month;
                }
            }

            var enUS = new System.Globalization.CultureInfo("en-US");
            canvas.Loaded += (s, e) =>
                canvas.Dispatcher.BeginInvoke(new Action(() =>
                {
                    double w = canvas.ActualWidth;
                    if (w <= 0 || totalSpan <= 0) return;
                    for (int i = 0; i < monthStarts.Count; i++)
                    {
                        var (ticks, month) = monthStarts[i];
                        var color = GetMonthAccentColor(month);
                        double xPos = ((double)(ticks - earliestTicks) / totalSpan) * w;
                        if (double.IsNaN(xPos) || double.IsInfinity(xPos)) continue;

                        canvas.Children.Add(new Line
                        {
                            X1 = xPos, Y1 = 0, X2 = xPos, Y2 = 8, Stroke = new SolidColorBrush(color),
                            StrokeThickness = 3
                        });
                        var lbl = new TextBlock
                        {
                            Text = enUS.DateTimeFormat.GetMonthName(month), FontSize = 9,
                            Foreground = new SolidColorBrush(color), FontWeight = FontWeights.SemiBold
                        };
                        lbl.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                        Canvas.SetLeft(lbl, xPos - lbl.DesiredSize.Width / 2);
                        Canvas.SetTop(lbl, 10);
                        canvas.Children.Add(lbl);
                    }
                }), System.Windows.Threading.DispatcherPriority.Loaded);

            container.Children.Add(canvas);
            return container;
        }

        // ── Title Bar ────────────────────────────────────────────────────────

        private Panel BuildTrendChartTitleBar(string chartTitle, MessageType messageType)
        {
            var titleBar = new DockPanel
            {
                Margin = new Thickness(10, 4, 10, 4), VerticalAlignment = VerticalAlignment.Center,
                LastChildFill = false, Background = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0))
            };

            var showRecordsBtn = new Button
            {
                Content = "📋 Show Records", Padding = new Thickness(10, 4, 10, 4), Height = 26,
                Background = new SolidColorBrush(Color.FromRgb(35, 134, 54)), Foreground = Brushes.White,
                BorderThickness = new Thickness(1), BorderBrush = new SolidColorBrush(Color.FromRgb(46, 160, 67)),
                FontSize = 11, FontWeight = FontWeights.SemiBold, Cursor = System.Windows.Input.Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center, Tag = "ShowRecords"
            };
            showRecordsBtn.Click += (s, e) => _onShowAllRecordsRequested?.Invoke(messageType);

            DockPanel.SetDock(showRecordsBtn, Dock.Right);
            titleBar.Children.Add(showRecordsBtn);

            var panLeft = BuildArrowButton("PanLeft");
            var panRight = BuildArrowButton("PanRight");
            var resetBtn = new Button
            {
                Content = "↻ Reset View", Padding = new Thickness(10, 4, 10, 4), Height = 26,
                Background = new SolidColorBrush(Color.FromRgb(33, 38, 45)),
                Foreground = new SolidColorBrush(Color.FromRgb(139, 148, 158)),
                BorderThickness = new Thickness(1), BorderBrush = new SolidColorBrush(Color.FromRgb(48, 54, 61)),
                FontSize = 11, FontWeight = FontWeights.SemiBold, Cursor = System.Windows.Input.Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center, Tag = "Reset"
            };

            foreach (var el in new UIElement[]
                     {
                         new TextBlock
                         {
                             Text = chartTitle, FontSize = 13, FontWeight = FontWeights.SemiBold,
                             Foreground = new SolidColorBrush(Color.FromRgb(63, 185, 80)),
                             VerticalAlignment = VerticalAlignment.Center
                         },
                         new TextBlock
                         {
                             Text = " (scroll/zoom for details)", FontSize = 11, FontStyle = FontStyles.Italic,
                             Foreground = new SolidColorBrush(Color.FromRgb(110, 118, 129)),
                             VerticalAlignment = VerticalAlignment.Center
                         },
                         new TextBlock
                         {
                             Text = " ⓘ", FontSize = 14, Foreground = new SolidColorBrush(Color.FromRgb(52, 152, 219)),
                             VerticalAlignment = VerticalAlignment.Center, Cursor = System.Windows.Input.Cursors.Help,
                             ToolTip =
                                 "AVG (cyan): daily average\nP95 (green): 95th percentile\n7-Day AVG (coral): rolling average\nTarget (red): P99 threshold"
                         },
                         new Border { Width = 15, Margin = new Thickness(5, 0, 5, 0) },
                         panLeft, panRight, resetBtn
                     })
            {
                DockPanel.SetDock(el, Dock.Left);
                titleBar.Children.Add(el);
            }

            return titleBar;
        }

        private static Button BuildArrowButton(string tag)
        {
            bool isLeft = tag == "PanLeft";
            var path = new System.Windows.Shapes.Path
            {
                Stroke = Brushes.White, StrokeThickness = 2.5, StrokeLineJoin = PenLineJoin.Round,
                StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round,
                Fill = Brushes.Transparent, Width = 22, Height = 12, Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
                Data = Geometry.Parse(isLeft
                    ? "M 22,7 L 2,7 M 2,7 L 9,2 M 2,7 L 9,12"
                    : "M 2,7 L 22,7 M 22,7 L 15,2 M 22,7 L 15,12")
            };
            return new Button
            {
                Content = path, Width = 32, Height = 26, Padding = new Thickness(0),
                Background = new SolidColorBrush(Color.FromRgb(33, 38, 45)),
                Foreground = new SolidColorBrush(Color.FromRgb(139, 148, 158)),
                BorderThickness = new Thickness(1), BorderBrush = new SolidColorBrush(Color.FromRgb(48, 54, 61)),
                Cursor = System.Windows.Input.Cursors.Hand, Margin = new Thickness(0, 0, isLeft ? 4 : 8, 0),
                ToolTip = isLeft ? "Pan left" : "Pan right", VerticalAlignment = VerticalAlignment.Center, Tag = tag
            };
        }

        // ── Zoom & Pan ────────────────────────────────────────────────────────

        private void AttachZoomAndPanControls(CartesianChart chart, Panel titleBar)
        {
            double? origMin = null, origMax = null;
            bool captured = false;
            var xAxis = chart.AxisX[0];
            var buttons = FindZoomButtons(titleBar);

            void CaptureIfReady()
            {
                if (captured) return;
                try
                {
                    double mn, mx;
                    try
                    {
                        mn = chart.AxisX[0].ActualMinValue;
                        mx = chart.AxisX[0].ActualMaxValue;
                    }
                    catch
                    {
                        return;
                    }

                    if (mn == 0 && mx == 0) return;
                    origMin = mn;
                    origMax = mx;
                    captured = true;
                }
                catch
                {
                    captured = false;
                }
            }

            chart.Loaded += (s, e) =>
                chart.Dispatcher.BeginInvoke(new Action(CaptureIfReady),
                    System.Windows.Threading.DispatcherPriority.Loaded);
            chart.UpdaterTick += _ =>
            {
                if (!captured)
                {
                    CaptureIfReady();
                    return;
                }

                if (origMin == null || origMax == null || double.IsNaN(xAxis.MinValue) ||
                    double.IsNaN(xAxis.MaxValue)) return;

                double cMin = xAxis.MinValue, cMax = xAxis.MaxValue, cRange = cMax - cMin;
                double oRange = origMax.Value - origMin.Value;
                bool clamp = false;

                if (cMin < origMin.Value)
                {
                    cMin = origMin.Value;
                    clamp = true;
                }

                if (cMax > origMax.Value)
                {
                    cMax = origMax.Value;
                    clamp = true;
                }

                if (cRange >= oRange * 0.999)
                {
                    cMin = origMin.Value;
                    cMax = origMax.Value;
                    clamp = true;
                }

                double minRange = oRange * 0.03;
                if (cRange < minRange)
                {
                    double center = (cMin + cMax) / 2;
                    cMin = Math.Max(origMin.Value, center - minRange / 2);
                    cMax = Math.Min(origMax.Value, center + minRange / 2);
                    clamp = true;
                }

                if (clamp)
                {
                    xAxis.MinValue = cMin;
                    xAxis.MaxValue = cMax;
                }
            };

            if (buttons.reset != null)
                buttons.reset.Click += (s, e) =>
                {
                    xAxis.MinValue = double.NaN;
                    xAxis.MaxValue = double.NaN;
                };
            if (buttons.left != null)
                buttons.left.Click += (s, e) =>
                    Pan(xAxis, -0.2, ref origMin, ref origMax, ref captured, CaptureIfReady);
            if (buttons.right != null)
                buttons.right.Click += (s, e) =>
                    Pan(xAxis, +0.2, ref origMin, ref origMax, ref captured, CaptureIfReady);
        }

        private static void Pan(Axis xAxis, double fraction, ref double? origMin, ref double? origMax,
            ref bool captured, Action captureIfReady)
        {
            captureIfReady();

            if (!captured || origMin == null || origMax == null) return;

            if (double.IsNaN(xAxis.MinValue) || double.IsNaN(xAxis.MaxValue))
            {
                xAxis.MinValue = origMin.Value;
                xAxis.MaxValue = origMax.Value;
            }

            double range = xAxis.MaxValue - xAxis.MinValue;
            double shift = range * Math.Abs(fraction) * Math.Sign(fraction);
            
            xAxis.MinValue += shift;
            xAxis.MaxValue += shift;

            if (fraction < 0 && xAxis.MinValue < origMin.Value)
            {
                xAxis.MinValue = origMin.Value;
                xAxis.MaxValue = origMin.Value + range;
            }

            else if (fraction > 0 && xAxis.MaxValue > origMax.Value)
            {
                xAxis.MaxValue = origMax.Value;
                xAxis.MinValue = origMax.Value - range;
            }
        }

        private static (Button left, Button right, Button reset) FindZoomButtons(Panel panel)
        {
            Button left = null, right = null, reset = null;
            SearchButtons(panel.Children, ref left, ref right, ref reset);
            return (left, right, reset);
        }

        private static void SearchButtons(UIElementCollection children, ref Button left, ref Button right,
            ref Button reset)
        {
            foreach (UIElement child in children)
            {
                if (child is Button btn)
                {
                    if (btn.Tag?.ToString() == "PanLeft") left = btn;
                    else if (btn.Tag?.ToString() == "PanRight") right = btn;
                    else if (btn.Tag?.ToString() == "Reset") reset = btn;
                }
                else if (child is Panel p) SearchButtons(p.Children, ref left, ref right, ref reset);
            }
        }

        // ── Reveal Animation ─────────────────────────────────────────────────

        private static void AttachRevealAnimationOnFirstLoad(CartesianChart chart)
        {
            var clipRect = new System.Windows.Media.RectangleGeometry();
            chart.Clip = clipRect;
            chart.Loaded += (s, e) => PlayRevealAnimation(chart, clipRect, 2800);
        }

        public static void PlayRevealAnimation(CartesianChart chart, System.Windows.Media.RectangleGeometry clipRect,
            int durationMs)
        {
            double w = chart.ActualWidth > 0 ? chart.ActualWidth : 1200;
            double h = chart.ActualHeight > 0 ? chart.ActualHeight : 600;
            chart.Clip = clipRect;
            var anim = new System.Windows.Media.Animation.RectAnimation
            {
                From = new Rect(0, 0, 0, h), To = new Rect(0, 0, w, h),
                Duration = TimeSpan.FromMilliseconds(durationMs),
                BeginTime = TimeSpan.FromMilliseconds(80),
                EasingFunction = new System.Windows.Media.Animation.CubicEase
                    { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
            };
            anim.Completed += (_, __) => chart.Clip = null;
            clipRect.BeginAnimation(System.Windows.Media.RectangleGeometry.RectProperty, anim);
        }

        // ── Shared Color Utility ─────────────────────────────────────────────

        public static Color GetMonthAccentColor(int monthNumber)
        {
            return MonthAccentColors[(monthNumber - 1) % MonthAccentColors.Length];
        }
    }
}