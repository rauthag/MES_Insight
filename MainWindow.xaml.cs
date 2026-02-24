using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LiveCharts;
using LiveCharts.Wpf;
using RTAnalyzer.Builders;

namespace RTAnalyzer
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        #region Fields

        private readonly DataLoader _dataLoader = new DataLoader();
        private readonly ChartBuilder _chartBuilder = new ChartBuilder();
        private readonly StatsCalculator _statsCalculator = new StatsCalculator();

        private List<ResponseRecord> _allRecords = new List<ResponseRecord>();
        private List<ResponseRecord> _filteredRecords = new List<ResponseRecord>();
        private Dictionary<MessageType, ChartData> _chartCache = new Dictionary<MessageType, ChartData>();

        public event PropertyChangedEventHandler PropertyChanged;

        #endregion

        #region Initialization

        public MainWindow()
        {
            InitializeComponent();
            ValidateLoadingControls();
            DataContext = this;
            ConfigureWindow();

            Loaded += async (s, e) =>
            {
                await Task.Delay(100);
                LoadStationData(@"C:\Users\lukas\source\repos\VS_Projects\MON0182_St1060_Automatic_screwing");
            };
        }

        private void ValidateLoadingControls()
        {
            if (LoadingOverlay == null) throw new Exception("LoadingOverlay not found");
            if (LoadingTitle == null) throw new Exception("LoadingTitle not found");
            if (LoadingStatus == null) throw new Exception("LoadingStatus not found");
            if (LoadingProgress == null) throw new Exception("LoadingProgress not found");
            if (LoadingPercentage == null) throw new Exception("LoadingPercentage not found");
        }

        private void ConfigureWindow()
        {
            double screenWidth = SystemParameters.PrimaryScreenWidth;
            double screenHeight = SystemParameters.PrimaryScreenHeight;
            Width = screenWidth * 0.92;
            Height = screenHeight * 0.94;
            Left = (screenWidth - Width) / 2;
            Top = (screenHeight - Height) / 2;
            MinWidth = Width;
            MinHeight = Height;
        }

        #endregion

        #region Data Loading

        private async void LoadStationData(string folderPath)
        {
            await LoadStationDataAsync(folderPath);
        }

        private async Task LoadStationDataAsync(string folderPath)
        {
            if (!Directory.Exists(folderPath)) return;

            ShowLoadingOverlay("Loading Station Data", "Reading files...", 0);
            await Task.Yield();

            DataLoadResult result = await Task.Run(() => _dataLoader.Load(folderPath));
            _allRecords = result.Records;

            TxtStationName.Text = string.IsNullOrEmpty(result.StationName)
                ? "Station Statistics"
                : result.StationName;

            SetDefaultDateTimeRange();
            await RefreshDisplayWithLoading();

            HideLoadingOverlay();
        }

        private void SetDefaultDateTimeRange()
        {
            if (_allRecords.Count == 0) return;

            DateTime minDate = DateTime.MaxValue;
            DateTime maxDate = DateTime.MinValue;

            foreach (var r in _allRecords)
            {
                if (!DateTime.TryParse(r.Timestamp, out DateTime d)) continue;
                if (d < minDate) minDate = d;
                if (d > maxDate) maxDate = d;
            }

            if (minDate == DateTime.MaxValue) return;

            DatePickerFrom.DisplayDateStart = minDate.Date;
            DatePickerFrom.DisplayDateEnd = maxDate.Date;
            DatePickerTo.DisplayDateStart = minDate.Date;
            DatePickerTo.DisplayDateEnd = maxDate.Date;
            DatePickerFrom.SelectedDate = minDate.Date;
            DatePickerTo.SelectedDate = maxDate.Date;
            DatePickerFrom.DisplayDate = minDate.Date;
            DatePickerTo.DisplayDate = maxDate.Date;
        }

        #endregion

        #region Event Handlers

        private async void BtnSelectFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                await LoadStationDataAsync(dialog.SelectedPath);
        }

        private async void BtnResetFilter_Click(object sender, RoutedEventArgs e)
        {
            ClearFilters();
            SetDefaultDateTimeRange();
            await RefreshDisplayWithLoading();
        }

        private async void BtnApplyFilter_Click(object sender, RoutedEventArgs e)
        {
            await RefreshDisplayWithLoading();
        }

        private void MainTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateSideStats();
        }

        private void CmbFilterMessageType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CmbFilterMessageType.SelectedItem is ComboBoxItem item && item.Tag != null)
            {
                string tag = item.Tag.ToString();
                foreach (TabItem tab in MainTabControl.Items)
                {
                    if (tab.Tag != null && tab.Tag.ToString() == tag)
                    {
                        MainTabControl.SelectedItem = tab;
                        break;
                    }
                }
            }

            RefreshDisplay();
        }

        #endregion

        #region Filter Management

        private void ClearFilters()
        {
            TxtFilterUid.Text = "";
            TxtFilterUidIn.Text = "";
            TxtFilterUidOut.Text = "";
            TxtFilterMaterial.Text = "";
            TxtFilterCarrierId.Text = "";
            TxtTimeFrom.Text = "00:00";
            TxtTimeTo.Text = "23:59";
            CmbFilterResult.SelectedIndex = 0;
            CmbFilterMessageType.SelectedIndex = 0;
        }

        private bool HasActiveFilter()
        {
            if (!string.IsNullOrWhiteSpace(TxtFilterUid.Text)) return true;
            if (!string.IsNullOrWhiteSpace(TxtFilterUidIn.Text)) return true;
            if (!string.IsNullOrWhiteSpace(TxtFilterUidOut.Text)) return true;
            if (!string.IsNullOrWhiteSpace(TxtFilterMaterial.Text)) return true;
            if (!string.IsNullOrWhiteSpace(TxtFilterCarrierId.Text)) return true;

            var filterResult = (CmbFilterResult.SelectedItem as ComboBoxItem)?.Content.ToString();
            return !string.IsNullOrEmpty(filterResult) && filterResult != "All";
        }

        #endregion

        #region Display Refresh

        private async void RefreshDisplay()
        {
            await RefreshDisplayWithLoading();
        }

        private async Task RefreshDisplayWithLoading()
        {
            if (_allRecords.Count == 0) return;

            ShowLoadingOverlay("Processing Data", "Applying filters...", 0);
            await Task.Delay(100);

            await ApplyFilters();

            LoadingProgress.Value = 10;
            LoadingPercentage.Text = "10%";
            await Task.Delay(50);

            await GenerateAllCharts();
            await RenderAllCharts();

            ShowLoadingOverlay("Finalizing...", "Finalizing...", 95);
            await Task.Delay(100);

            GridRecords.ItemsSource = _filteredRecords;
            UpdateSideStats();
            UpdateTabHeaders();

            ShowLoadingOverlay("Complete", "Complete", 100);
            await Task.Delay(500);
            HideLoadingOverlay();
        }

        private async Task ApplyFilters()
        {
            TimeSpan.TryParse(TxtTimeFrom.Text, out TimeSpan ts1);
            TimeSpan.TryParse(TxtTimeTo.Text, out TimeSpan ts2);
            DateTime start = (DatePickerFrom.SelectedDate ?? DateTime.MinValue).Add(ts1);
            DateTime end = (DatePickerTo.SelectedDate ?? DateTime.MaxValue).Add(ts2);

            string filterUid = TxtFilterUid.Text.Trim();
            string filterUidIn = TxtFilterUidIn.Text.Trim();
            string filterUidOut = TxtFilterUidOut.Text.Trim();
            string filterMaterial = TxtFilterMaterial.Text.Trim();
            string filterCarrierId = TxtFilterCarrierId.Text.Trim();
            string filterResult = (CmbFilterResult.SelectedItem as ComboBoxItem)?.Content.ToString();
            if (filterResult == "All") filterResult = null;

            await Task.Run(() =>
            {
                _filteredRecords.Clear();
                foreach (var r in _allRecords)
                {
                    if (!DateTime.TryParse(r.Timestamp, out DateTime d)) continue;
                    if (d < start || d > end) continue;
                    if (!string.IsNullOrEmpty(filterUid) && (r.Uid == null || !r.Uid.Contains(filterUid))) continue;
                    if (!string.IsNullOrEmpty(filterUidIn) &&
                        (r.UidIn == null || !r.UidIn.Contains(filterUidIn))) continue;
                    if (!string.IsNullOrEmpty(filterUidOut) &&
                        (r.UidOut == null || !r.UidOut.Contains(filterUidOut))) continue;
                    if (!string.IsNullOrEmpty(filterMaterial) &&
                        (r.Material == null || !r.Material.Contains(filterMaterial))) continue;
                    if (!string.IsNullOrEmpty(filterCarrierId) &&
                        (r.CarrierId == null || !r.CarrierId.Contains(filterCarrierId))) continue;
                    if (!string.IsNullOrEmpty(filterResult) && r.Result != filterResult) continue;

                    _filteredRecords.Add(r);
                }
            });
        }

        private async Task GenerateAllCharts()
        {
            var messageTypes = GetAllMessageTypes();
            _chartCache.Clear();

            for (int i = 0; i < messageTypes.Length; i++)
            {
                var type = messageTypes[i];
                string typeName = type.ToString().Replace("_", " ");
                int progress = 10 + ((i + 1) * 70 / messageTypes.Length);

                ShowLoadingOverlay($"Generating {typeName} charts...", $"Generating {typeName} charts...", progress);
                await Task.Delay(50);

                var chartData = _chartBuilder.Build(_filteredRecords, type);
                if (chartData != null)
                {
                    _chartCache[type] = chartData;
                }

                await Task.Yield();
            }
        }

        private async Task RenderAllCharts()
        {
            ShowLoadingOverlay("Rendering charts...", "Rendering charts...", 85);
            await Task.Delay(100);

            var messageTypes = GetAllMessageTypes();
            foreach (var type in messageTypes)
            {
                RenderChartFromCache(type);
                await Task.Yield();
            }
        }

        private MessageType[] GetAllMessageTypes()
        {
            return new[]
            {
                MessageType.UNIT_INFO,
                MessageType.NEXT_OPERATION,
                MessageType.UNIT_CHECKIN,
                MessageType.UNIT_RESULT,
                MessageType.LOAD_MATERIAL,
                MessageType.REQ_MATERIAL_INFO,
                MessageType.REQ_SETUP_CHANGE2
            };
        }

        #endregion

        #region Chart Rendering

        private void RenderChartFromCache(MessageType type)
        {
            try
            {
                if (!_chartCache.ContainsKey(type)) return;

                var data = _chartCache[type];
                var panel = GetChartPanel(type);
                if (panel == null) return;

                panel.Children.Clear();

                double availableHeight = ActualHeight - 76 - 30;
                int totalCharts = data.Charts.Count;
                int mainChartHeight = totalCharts > 0 ? (int)((availableHeight * 0.45) / totalCharts) : 300;
                int trendChartHeight = (int)(availableHeight * 0.40);

                AddHistogramCharts(panel, data.Charts, mainChartHeight);

                if (data.TrendChart != null)
                {
                    var trendElement = BuildTrendChartContainer(data.TrendChart, trendChartHeight);
                    panel.Children.Add(trendElement);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error displaying chart for {type}:\n\n{ex.Message}", "Chart Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void AddHistogramCharts(StackPanel panel, List<ChartSeries> charts, int chartHeight)
        {
            if (charts.Count == 0) return;

            if (charts.Count == 1)
            {
                var element = BuildHistogramChart(charts[0], chartHeight, true);
                panel.Children.Add(element);
            }
            else
            {
                var sharedContainer = new StackPanel();
                for (int i = 0; i < charts.Count; i++)
                {
                    var chartElement = BuildHistogramChart(charts[i], chartHeight, false);
                    sharedContainer.Children.Add(chartElement);

                    if (i < charts.Count - 1)
                    {
                        sharedContainer.Children.Add(CreateChartSeparator());
                    }
                }

                var sharedBorder = CreateBorderedContainer(sharedContainer);
                panel.Children.Add(sharedBorder);
            }
        }

        private StackPanel GetChartPanel(MessageType type)
        {
            switch (type)
            {
                case MessageType.UNIT_INFO: return PanelUnitInfo;
                case MessageType.NEXT_OPERATION: return PanelNextOperation;
                case MessageType.UNIT_CHECKIN: return PanelUnitCheckin;
                case MessageType.UNIT_RESULT: return PanelUnitResult;
                case MessageType.LOAD_MATERIAL: return PanelLoadMaterial;
                case MessageType.REQ_MATERIAL_INFO: return PanelReqMaterialInfo;
                case MessageType.REQ_SETUP_CHANGE2: return PanelReqSetupChange2;
                default: return null;
            }
        }

        #endregion

        #region Histogram Chart Building

        private UIElement BuildHistogramChart(ChartSeries chartSeries, int chartHeight, bool addBorder)
        {
            var grid = CreateChartGrid(chartSeries.Name, chartHeight);
            var chart = CreateHistogramCartesianChart(chartSeries);

            Grid.SetRow(chart, 1);
            grid.Children.Add(chart);

            return addBorder ? (UIElement)CreateBorderedContainer(grid) : (UIElement)grid;
        }

        private Grid CreateChartGrid(string title, int chartHeight)
        {
            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(30) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(chartHeight) });

            var titleBlock = new TextBlock
            {
                Text = title,
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(52, 73, 94)),
                Margin = new Thickness(10, 5, 0, 5)
            };

            Grid.SetRow(titleBlock, 0);
            grid.Children.Add(titleBlock);

            return grid;
        }

        private CartesianChart CreateHistogramCartesianChart(ChartSeries chartSeries)
        {
            var chart = new CartesianChart
            {
                Series = chartSeries.Series,
                LegendLocation = LegendLocation.None,
                Margin = new Thickness(10, 0, 10, 10),
                DisableAnimations = false
            };

            int labelCount = chartSeries.Labels.Length;
            int step = labelCount > 80 ? 4 : (labelCount > 50 ? 2 : 1);

            var axisY = new Axis
            {
                Title = "Occurrences",
                MinValue = 0,
                FontSize = 13,
                LabelFormatter = value => value.ToString("N0")
            };

            var axisX = new Axis
            {
                Labels = chartSeries.Labels,
                Title = "Response Time (ms)",
                FontSize = 13,
                Separator = new LiveCharts.Wpf.Separator { Step = step }
            };

            chart.AxisY.Add(axisY);
            chart.AxisX.Add(axisX);

            return chart;
        }

        #endregion

        #region Trend Chart Building

        private UIElement BuildTrendChartContainer(ChartSeries chartSeries, int chartHeight)
        {
            var mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(34) });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(chartHeight - 4) });

            var titlePanel = CreateTrendChartTitlePanel(chartSeries.Name);
            var chart = CreateTrendCartesianChart(chartSeries);

            SetupTrendChartAnimation(chart);
            SetupTrendChartZoomControls(chart, titlePanel);

            Grid.SetRow(titlePanel, 0);
            Grid.SetRow(chart, 1);
            mainGrid.Children.Add(titlePanel);
            mainGrid.Children.Add(chart);

            return CreateBorderedContainer(mainGrid);
        }

        private StackPanel CreateTrendChartTitlePanel(string title)
        {
            var titlePanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(10, 4, 10, 4),
                VerticalAlignment = VerticalAlignment.Center
            };

            titlePanel.Children.Add(CreateTitleBlock(title));
            titlePanel.Children.Add(CreateInfoIcon());
            titlePanel.Children.Add(new Border { Width = 15, Margin = new Thickness(5, 0, 5, 0) });
            titlePanel.Children.Add(CreatePanButton("⬅", "Pan left", "PanLeft"));
            titlePanel.Children.Add(CreatePanButton("➡", "Pan right", "PanRight"));
            titlePanel.Children.Add(CreateResetButton());

            return titlePanel;
        }

        private CartesianChart CreateTrendCartesianChart(ChartSeries chartSeries)
        {
            var chart = new CartesianChart
            {
                Series = chartSeries.Series,
                LegendLocation = LegendLocation.Bottom,
                Margin = new Thickness(10, 0, 10, 10),
                DisableAnimations = true,
                Zoom = ZoomingOptions.X,
                Pan = PanningOptions.None,
                AnimationsSpeed = TimeSpan.FromMilliseconds(2400),
                DataTooltip = CreateTrendChartTooltip()
            };

            chart.AxisY.Add(CreateResponseTimeAxis());
            chart.AxisY.Add(CreateVolumeAxis());
            chart.AxisX.Add(CreateDateAxis());

            return chart;
        }

        private void SetupTrendChartAnimation(CartesianChart chart)
        {
            var clipRect = new System.Windows.Media.RectangleGeometry();
            chart.Clip = clipRect;

            chart.Loaded += (s, e) =>
            {
                var drawAnimation = new System.Windows.Media.Animation.RectAnimation
                {
                    From = new Rect(0, 0, 0, chart.ActualHeight),
                    To = new Rect(0, 0, chart.ActualWidth, chart.ActualHeight),
                    Duration = TimeSpan.FromMilliseconds(2400),
                    BeginTime = TimeSpan.FromMilliseconds(100),
                    EasingFunction = new System.Windows.Media.Animation.CubicEase
                    {
                        EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut
                    }
                };

                drawAnimation.Completed += (sender, args) => chart.Clip = null;
                clipRect.BeginAnimation(System.Windows.Media.RectangleGeometry.RectProperty, drawAnimation);
            };
        }

        private void SetupTrendChartZoomControls(CartesianChart chart, StackPanel titlePanel)
        {
            double? originalMin = null;
            double? originalMax = null;
            bool boundsInitialized = false;
            var axisX = chart.AxisX[0];

            var buttons = FindZoomButtons(titlePanel);

            void EnsureBounds()
            {
                if (boundsInitialized) return;
                try
                {
                    if (chart.AxisX.Count > 0 && chart.AxisX[0].ActualMinValue != 0 &&
                        chart.AxisX[0].ActualMaxValue != 0)
                    {
                        originalMin = chart.AxisX[0].ActualMinValue;
                        originalMax = chart.AxisX[0].ActualMaxValue;
                        boundsInitialized = true;
                    }
                }
                catch
                {
                    boundsInitialized = false;
                }
            }

            chart.Loaded += (s, e) => Dispatcher.BeginInvoke(new Action(EnsureBounds),
                System.Windows.Threading.DispatcherPriority.Loaded);

            if (buttons.reset != null)
                buttons.reset.Click += (s, e) =>
                {
                    axisX.MinValue = double.NaN;
                    axisX.MaxValue = double.NaN;
                    boundsInitialized = false;
                };

            if (buttons.left != null)
                buttons.left.Click += (s, e) =>
                    PanAxis(axisX, -0.2, ref originalMin, ref originalMax, ref boundsInitialized, EnsureBounds);

            if (buttons.right != null)
                buttons.right.Click += (s, e) =>
                    PanAxis(axisX, 0.2, ref originalMin, ref originalMax, ref boundsInitialized, EnsureBounds);
        }

        private void PanAxis(Axis axisX, double direction, ref double? originalMin, ref double? originalMax,
            ref bool boundsInitialized, Action ensureBounds)
        {
            ensureBounds();
            if (!boundsInitialized || originalMin == null || originalMax == null) return;

            if (double.IsNaN(axisX.MinValue) || double.IsNaN(axisX.MaxValue))
            {
                axisX.MinValue = originalMin.Value;
                axisX.MaxValue = originalMax.Value;
            }

            double range = axisX.MaxValue - axisX.MinValue;
            double shift = range * Math.Abs(direction);

            axisX.MinValue += shift * Math.Sign(direction);
            axisX.MaxValue += shift * Math.Sign(direction);

            if (direction < 0 && axisX.MinValue < originalMin.Value)
            {
                axisX.MinValue = originalMin.Value;
                axisX.MaxValue = originalMin.Value + range;
            }
            else if (direction > 0 && axisX.MaxValue > originalMax.Value)
            {
                axisX.MaxValue = originalMax.Value;
                axisX.MinValue = originalMax.Value - range;
            }
        }

        private (Button left, Button right, Button reset) FindZoomButtons(StackPanel panel)
        {
            Button left = null, right = null, reset = null;
            foreach (var child in panel.Children)
            {
                if (child is Button btn)
                {
                    if (btn.Tag?.ToString() == "PanLeft") left = btn;
                    else if (btn.Tag?.ToString() == "PanRight") right = btn;
                    else if (btn.Tag?.ToString() == "Reset") reset = btn;
                }
            }

            return (left, right, reset);
        }

        #endregion

        #region UI Element Factories

        private TextBlock CreateTitleBlock(string text)
        {
            return new TextBlock
            {
                Text = text,
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(52, 73, 94)),
                VerticalAlignment = VerticalAlignment.Center
            };
        }

        private TextBlock CreateInfoIcon()
        {
            return new TextBlock
            {
                Text = " ⓘ",
                FontSize = 14,
                Foreground = new SolidColorBrush(Color.FromRgb(52, 152, 219)),
                VerticalAlignment = VerticalAlignment.Center,
                Cursor = System.Windows.Input.Cursors.Help,
                ToolTip = "═══ MES Response Time Metrics ═══\n\n" +
                          "AVG (blue): Average MES response time per day\n\n" +
                          "P95 (purple): 95% of requests are faster than this value\n" +
                          "  → Shows guaranteed speed for most requests\n\n" +
                          "7-Day AVG (yellow): Rolling 7-day average\n" +
                          "  → Shows long-term trend\n\n" +
                          "Target (red): Maximum acceptable response time\n" +
                          "  → Calculated as P99 (99% of requests must be faster)\n\n" +
                          "Volume (gray bars): Number of requests per day (right axis)\n\n" +
                          "💡 Use controls or mouse wheel to zoom"
            };
        }

        private Button CreatePanButton(string content, string tooltip, string tag)
        {
            return new Button
            {
                Content = content,
                FontSize = 14,
                Width = 32,
                Height = 26,
                Padding = new Thickness(0),
                Background = new SolidColorBrush(Color.FromRgb(52, 152, 219)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Color.FromRgb(41, 128, 185)),
                Cursor = System.Windows.Input.Cursors.Hand,
                Margin = new Thickness(0, 0, tag == "PanLeft" ? 0 : 8, 0),
                ToolTip = tooltip,
                VerticalAlignment = VerticalAlignment.Center,
                Tag = tag
            };
        }

        private Button CreateResetButton()
        {
            return new Button
            {
                Content = "↻ Reset View",
                Padding = new Thickness(10, 4, 10, 4),
                Height = 26,
                Background = new SolidColorBrush(Color.FromRgb(149, 165, 166)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Color.FromRgb(127, 140, 141)),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Cursor = System.Windows.Input.Cursors.Hand,
                ToolTip = "Reset zoom to full view",
                VerticalAlignment = VerticalAlignment.Center,
                Tag = "Reset"
            };
        }

        private Axis CreateResponseTimeAxis()
        {
            return new Axis
            {
                Title = "Response Time (ms)",
                MinValue = 0,
                FontSize = 13,
                LabelFormatter = value => value.ToString("N0")
            };
        }

        private Axis CreateVolumeAxis()
        {
            return new Axis
            {
                Title = "Volume (requests)",
                MinValue = 0,
                FontSize = 13,
                Position = AxisPosition.RightTop,
                LabelFormatter = value => value.ToString("N0")
            };
        }

        private Axis CreateDateAxis()
        {
            return new Axis
            {
                Title = "Date",
                FontSize = 13,
                LabelFormatter = value =>
                {
                    var date = new DateTime((long)value);
                    return date.ToString("dd.MM");
                }
            };
        }

        private LiveCharts.Wpf.DefaultTooltip CreateTrendChartTooltip()
        {
            return new LiveCharts.Wpf.DefaultTooltip
            {
                Background = new SolidColorBrush(Color.FromArgb(240, 50, 50, 50)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(52, 152, 219)),
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(5),
                FontSize = 12,
                Padding = new Thickness(10),
                SelectionMode = TooltipSelectionMode.OnlySender
            };
        }

        private Border CreateChartSeparator()
        {
            return new Border
            {
                Height = 1,
                Background = new SolidColorBrush(Color.FromRgb(149, 165, 166)),
                Margin = new Thickness(20, 5, 20, 5),
                Opacity = 0.5
            };
        }

        private Border CreateBorderedContainer(UIElement child)
        {
            return new Border
            {
                Child = child,
                BorderBrush = new SolidColorBrush(Color.FromRgb(189, 195, 199)),
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(5),
                Margin = new Thickness(0, 5, 0, 5)
            };
        }

        #endregion

        #region Stats & Tab Management

        private void UpdateSideStats()
        {
            if (!(MainTabControl.SelectedItem is TabItem selected) || selected.Tag == null) return;

            MessageType type = (MessageType)Enum.Parse(typeof(MessageType), selected.Tag.ToString());
            StatsResult stats = _statsCalculator.Calculate(_filteredRecords, type);

            if (stats == null)
            {
                ClearStats();
                return;
            }

            TxtTabRecords.Text = "Message Records: " + stats.Count;
            TxtTabAvg.Text = Math.Round(stats.Average, 1) + " ms";
            TxtTabP95.Text = stats.P95 + " ms";
            TxtTabMin.Text = "Min Time: " + stats.Min + " ms";
            TxtTabMax.Text = "Max Time: " + stats.Max + " ms";
            TxtTabStability.Text = stats.StabilityLabel + " (" + Math.Round(stats.CV, 1) + "%)";
            TxtTabStability.Foreground = new SolidColorBrush(stats.StabilityColor);
        }

        private void ClearStats()
        {
            TxtTabRecords.Text = "Records: 0";
            TxtTabAvg.Text = "0 ms";
            TxtTabP95.Text = "0 ms";
            TxtTabMin.Text = "Min: 0 ms";
            TxtTabMax.Text = "Max: 0 ms";
            TxtTabStability.Text = "N/A";
            TxtTabStability.Foreground = Brushes.Gray;
        }

        private void UpdateTabHeaders()
        {
            bool hasActiveFilter = HasActiveFilter();
            foreach (TabItem tab in MainTabControl.Items)
            {
                UpdateSingleTabHeader(tab, hasActiveFilter);
            }
        }

        private void UpdateSingleTabHeader(TabItem tab, bool hasActiveFilter)
        {
            if (tab.Tag == null) return;
            var type = TryParseMessageType(tab.Tag.ToString());
            if (type == null) return;

            bool hasRecords = HasRecordsOfType(type.Value);
            bool shouldHighlight = hasActiveFilter && hasRecords;

            tab.FontWeight = shouldHighlight ? FontWeights.Bold : FontWeights.Normal;
            tab.FontSize = shouldHighlight ? 13 : 11;
        }

        private bool HasRecordsOfType(MessageType type)
        {
            foreach (var r in _filteredRecords)
            {
                if (r.Type == type) return true;
            }

            return false;
        }

        private MessageType? TryParseMessageType(string tagValue)
        {
            try
            {
                return (MessageType)Enum.Parse(typeof(MessageType), tagValue);
            }
            catch
            {
                return null;
            }
        }

        #endregion

        #region Loading Overlay

        private void ShowLoadingOverlay(string title, string status, int progress)
        {
            if (LoadingOverlay == null) return;
            LoadingTitle.Text = title;
            LoadingStatus.Text = status;
            LoadingProgress.Value = progress;
            LoadingPercentage.Text = $"{progress}%";
            LoadingOverlay.Visibility = Visibility.Visible;
        }

        private void HideLoadingOverlay()
        {
            if (LoadingOverlay == null) return;
            LoadingOverlay.Visibility = Visibility.Collapsed;
        }

        #endregion
    }
}