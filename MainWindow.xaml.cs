using RTAnalyzer.UI;
using RTAnalyzer.Charts.Renderers;
using RTAnalyzer.Charts.Builders;
using RTAnalyzer.Core;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LiveCharts.Wpf;
using RTAnalyzer.Charts;

namespace RTAnalyzer
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        #region Fields

        private readonly DataLoader _dataLoader = new DataLoader();
        private readonly StatsCalculator _statsCalculator = new StatsCalculator();
        private readonly DayRecordsPanelBuilder _dayRecordsPanelBuilder = new DayRecordsPanelBuilder();
        private ChartFactory _chartFactory;
        private TrendChartRenderer _trendChartRenderer;

        private List<ResponseRecord> _allRecords = new List<ResponseRecord>();
        private List<ResponseRecord> _filteredRecords = new List<ResponseRecord>();

        private List<StationInfo> _loadedStations = new List<StationInfo>();
        private StationInfo _activeStation = null;

        private Dictionary<string, (List<ResponseRecord> records, string stationName)> _stationDataCache
            = new Dictionary<string, (List<ResponseRecord>, string)>();

        private Dictionary<string, Dictionary<(MessageType, ChartType), ChartData>> _stationChartCache
            = new Dictionary<string, Dictionary<(MessageType, ChartType), ChartData>>();

        private Dictionary<(MessageType, ChartType), ChartData> _chartCache =
            new Dictionary<(MessageType, ChartType), ChartData>();

        private Dictionary<DateTime, List<ResponseRecord>> _recordsGroupedByDay =
            new Dictionary<DateTime, List<ResponseRecord>>();

        private Dictionary<MessageType, (Border panel, ColumnDefinition col, bool open)> _dayRecordsPanelByMessageType =
            new Dictionary<MessageType, (Border, ColumnDefinition, bool)>();

        private Dictionary<MessageType, CartesianChart> _trendChartByMessageType =
            new Dictionary<MessageType, CartesianChart>();

        private Dictionary<MessageType, LiveCharts.Wpf.AxisSection> _selectedDayHighlightByMessageType =
            new Dictionary<MessageType, LiveCharts.Wpf.AxisSection>();

        private Dictionary<MessageType, (Border container, StackPanel panel)> _timelineContainerByMessageType =
            new Dictionary<MessageType, (Border, StackPanel)>();

        private HashSet<MessageType> _tabsUserHasAlreadySeen = new HashSet<MessageType>();

        public event PropertyChangedEventHandler PropertyChanged;

        #endregion

        #region Initialization

        public MainWindow()
        {
            InitializeComponent();
            ValidateLoadingControls();
            DataContext = this;
            WindowState = WindowState.Maximized;

            _chartFactory = new ChartFactory(
                _dayRecordsPanelBuilder,
                _dayRecordsPanelByMessageType,
                _trendChartByMessageType,
                _selectedDayHighlightByMessageType,
                _timelineContainerByMessageType,
                _recordsGroupedByDay,
                _filteredRecords,
                OnShowAllRecordsRequested);

            _trendChartRenderer = _chartFactory.GetRenderer(ChartType.Trend) as TrendChartRenderer;

            Loaded += (s, e) =>
            {
                var startup = new StartupWindow();
                bool? result = startup.ShowDialog();
                if (result == true && !string.IsNullOrEmpty(startup.SelectedPath))
                    Dispatcher.BeginInvoke(new Action(async () =>
                        await LoadAllStationsFromRoot(startup.SelectedPath)));
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

        #endregion

        #region Data Loading

        private async void LoadStationData(string folderPath)
        {
            await LoadAllStationsFromRoot(folderPath);
        }

        private async Task LoadAllStationsFromRoot(string rootPath)
        {
            ShowLoadingOverlay("Scanning...", "Looking for stations...", 0, detail: rootPath);

            await Task.Yield();

            var stations = await Task.Run(() => DataLoader.FindStations(rootPath));

            if (stations.Count == 0)
                stations.Add(new StationInfo
                    { FolderPath = rootPath, StationName = System.IO.Path.GetFileName(rootPath) });

            _loadedStations = stations;
            _stationDataCache.Clear();
            _stationChartCache.Clear();

            ShowLoadingOverlay(
                "Found " + stations.Count + " station" + (stations.Count != 1 ? "s" : ""),
                "Preparing to load...",
                5,
                typeCount: stations.Count);

            await Task.Delay(400);

            RebuildStationBar();

            int totalFiles = 0;

            for (int i = 0; i < stations.Count; i++)
            {
                var st = stations[i];

                int liveFileCount = 0;

                var loadResult = await Task.Run(() => _dataLoader.Load(st.FolderPath,
                    (status, percent, extra) =>
                    {
                        if (status.StartsWith("Reading "))
                            System.Threading.Interlocked.Increment(ref liveFileCount);

                        int fc = liveFileCount;
                        int innerPct = 5 + (i * 88 / stations.Count) + (percent * 88 / 100 / stations.Count);

                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            bool isReading = status.StartsWith("Reading ");
                            string fileName = isReading ? status.Substring(8).TrimEnd('.', ' ') : null;

                            string detail = isReading && fc > 0
                                ? "Loading file " + fc + (fileName != null ? "  —  " + fileName : "")
                                : status.StartsWith("Scanning")
                                    ? "Scanning files..."
                                    : "Processing...";

                            ShowLoadingOverlay(
                                "Station " + (i + 1) + " / " + stations.Count,
                                st.StationName,
                                innerPct,
                                detail: detail,
                                fileCount: fc,
                                typeCount: stations.Count);
                        }));
                    }));

                string displayName = loadResult.StationName.Length > 0 ? loadResult.StationName : st.StationName;

                _stationDataCache[st.FolderPath] = (loadResult.Records, displayName);

                if (!string.IsNullOrEmpty(loadResult.StationName))
                    st.StationName = loadResult.StationName;

                totalFiles += liveFileCount;

                ShowLoadingOverlay(
                    "Station " + (i + 1) + " / " + stations.Count + "  —  building charts",
                    st.StationName,
                    5 + ((i * 88 + 44) / stations.Count),
                    detail: "Building charts for " + loadResult.Records.Count.ToString("N0") + " records...",
                    fileCount: totalFiles,
                    recordCount: loadResult.Records.Count,
                    typeCount: stations.Count);

                await Task.Yield();

                var stationCharts = await BuildChartsForRecords(loadResult.Records, displayName);

                _stationChartCache[st.FolderPath] = stationCharts;

                UpdateActiveStationButton();
            }

            int totalRecords = stations.Sum(s => _stationDataCache.ContainsKey(s.FolderPath)
                ? _stationDataCache[s.FolderPath].records.Count
                : 0);

            ShowLoadingOverlay(
                "All stations ready",
                stations.Count + " stations  ·  " + totalRecords.ToString("N0") + " records total",
                100,
                fileCount: totalFiles,
                recordCount: totalRecords,
                typeCount: stations.Count);

            await Task.Delay(400);

            await SwitchToStation(stations[0]);
        }

        private async Task<Dictionary<(MessageType, ChartType), ChartData>> BuildChartsForRecords(
            List<ResponseRecord> records,
            string stationName)
        {
            var result = new Dictionary<(MessageType, ChartType), ChartData>();
            var messageTypes = GetAllSupportedMessageTypes();

            var preparedInputs = await Task.Run(() =>
            {
                var tempFactory = new ChartFactory(
                    _dayRecordsPanelBuilder,
                    new Dictionary<MessageType, (Border, ColumnDefinition, bool)>(),
                    new Dictionary<MessageType, CartesianChart>(),
                    new Dictionary<MessageType, LiveCharts.Wpf.AxisSection>(),
                    new Dictionary<MessageType, (Border, StackPanel)>(),
                    new Dictionary<DateTime, List<ResponseRecord>>(),
                    new List<ResponseRecord>(),
                    _ => { });

                return tempFactory.PrepareAllInputs(records, messageTypes);
            });

            foreach (var messageType in messageTypes)
            {
                if (!preparedInputs.TryGetValue(messageType, out var input)) continue;
                if (input.Records.Count == 0) continue;

                foreach (var chartType in new[] { ChartType.Trend, ChartType.Histogram, ChartType.Timeline })
                {
                    var data = _chartFactory.BuildSingle(chartType, input);

                    if (data != null)
                        result[(messageType, chartType)] = data;
                }
            }

            return result;
        }

        private async Task SwitchToStation(StationInfo station)
        {
            _activeStation = station;

            UpdateActiveStationButton();

            if (!_stationDataCache.TryGetValue(station.FolderPath, out var cached))
            {
                HideLoadingOverlay();
                return;
            }

            _allRecords = cached.records;

            string displayName = !string.IsNullOrEmpty(station.LineName)
                ? station.LineName + "  ·  " + cached.stationName
                : cached.stationName;

            TxtStationName.Text = displayName;

            _tabsUserHasAlreadySeen.Clear();
            _dayRecordsPanelByMessageType.Clear();
            _trendChartByMessageType.Clear();
            _selectedDayHighlightByMessageType.Clear();
            _timelineContainerByMessageType.Clear();
            _recordsGroupedByDay.Clear();

            _chartFactory = new ChartFactory(
                _dayRecordsPanelBuilder,
                _dayRecordsPanelByMessageType,
                _trendChartByMessageType,
                _selectedDayHighlightByMessageType,
                _timelineContainerByMessageType,
                _recordsGroupedByDay,
                _filteredRecords,
                OnShowAllRecordsRequested);

            _trendChartRenderer = _chartFactory.GetRenderer(ChartType.Trend) as TrendChartRenderer;

            // Restore pre-built charts from station cache if available
            _chartCache.Clear();

            if (_stationChartCache.TryGetValue(station.FolderPath, out var prebuiltCharts))
            {
                foreach (var kv in prebuiltCharts)
                    _chartCache[kv.Key] = kv.Value;
            }

            SetDatePickersToFullDataRange();

            await RefreshChartsAndStatsWithLoadingOverlay();
        }

        private void SetDatePickersToFullDataRange()
        {
            if (_allRecords.Count == 0) return;

            DateTime earliest = DateTime.MaxValue;
            DateTime latest = DateTime.MinValue;

            foreach (var r in _allRecords)
            {
                if (r.TimestampParsed == DateTime.MinValue) continue;
                if (r.TimestampParsed < earliest) earliest = r.TimestampParsed;
                if (r.TimestampParsed > latest) latest = r.TimestampParsed;
            }

            if (earliest == DateTime.MaxValue) return;

            DatePickerFrom.DisplayDateStart = earliest.Date;
            DatePickerFrom.DisplayDateEnd = latest.Date;
            DatePickerTo.DisplayDateStart = earliest.Date;
            DatePickerTo.DisplayDateEnd = latest.Date;
            DatePickerFrom.SelectedDate = earliest.Date;
            DatePickerTo.SelectedDate = latest.Date;
            DatePickerFrom.DisplayDate = earliest.Date;
            DatePickerTo.DisplayDate = latest.Date;
        }

        #endregion

        #region Event Handlers

        private async void BtnSelectFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
            await LoadAllStationsFromRoot(dialog.SelectedPath);
        }

        private async void BtnResetFilter_Click(object sender, RoutedEventArgs e)
        {
            ClearAllFilters();
            SetDatePickersToFullDataRange();
            await RefreshChartsAndStatsWithLoadingOverlay();
        }

        private async void BtnApplyFilter_Click(object sender, RoutedEventArgs e)
        {
            await RefreshChartsAndStatsWithLoadingOverlay();
        }

        private void MainTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateSidebarStats();
            PlayChartRevealAnimationFirstTimeUserVisitsTab();
        }

        private void PlayChartRevealAnimationFirstTimeUserVisitsTab()
        {
            if (!(MainTabControl.SelectedItem is TabItem tab) || tab.Tag == null) return;
            MessageType? type = TryParseMessageType(tab.Tag.ToString());
            if (type == null || !_trendChartByMessageType.ContainsKey(type.Value)) return;
            if (_tabsUserHasAlreadySeen.Contains(type.Value)) return;

            _tabsUserHasAlreadySeen.Add(type.Value);
            var chart = _trendChartByMessageType[type.Value];
            var clipRect = new System.Windows.Media.RectangleGeometry();
            TrendChartRenderer.PlayRevealAnimation(chart, clipRect, 2000);
        }

        private void CmbFilterMessageType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CmbFilterMessageType.SelectedItem is ComboBoxItem item && item.Tag != null)
            {
                string tag = item.Tag.ToString();
                foreach (TabItem t in MainTabControl.Items)
                    if (t.Tag?.ToString() == tag)
                    {
                        MainTabControl.SelectedItem = t;
                        break;
                    }
            }

            RefreshChartsWithoutLoadingOverlay();
        }

        private async void OnShowAllRecordsRequested(MessageType messageType)
        {
            if (!_dayRecordsPanelByMessageType.ContainsKey(messageType)) return;
            var state = _dayRecordsPanelByMessageType[messageType];

            var records = _filteredRecords.Where(r => r.Type == messageType).ToList();
            if (records.Count == 0)
            {
                MessageBox.Show("No records to display", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            _dayRecordsPanelBuilder.ShowLoadingSpinner(state.panel, DateTime.Today, records.Count,
                showingAllRecords: true);

            if (!state.open)
            {
                _dayRecordsPanelByMessageType[messageType] = (state.panel, state.col, true);
                _dayRecordsPanelBuilder.AnimateSlideOpen(state.panel, state.col);
                await Task.Delay(480);
            }

            _dayRecordsPanelBuilder.PopulateWithDayRecords(state.panel, DateTime.Today, records,
                showingAllRecords: true);
        }

        #endregion

        #region Filter Management

        private void ClearAllFilters()
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

        private bool AnyNonDateFilterIsActive()
        {
            if (!string.IsNullOrWhiteSpace(TxtFilterUid.Text)) return true;
            if (!string.IsNullOrWhiteSpace(TxtFilterUidIn.Text)) return true;
            if (!string.IsNullOrWhiteSpace(TxtFilterUidOut.Text)) return true;
            if (!string.IsNullOrWhiteSpace(TxtFilterMaterial.Text)) return true;
            if (!string.IsNullOrWhiteSpace(TxtFilterCarrierId.Text)) return true;
            var result = (CmbFilterResult.SelectedItem as ComboBoxItem)?.Content.ToString();
            return !string.IsNullOrEmpty(result) && result != "All";
        }

        #endregion

        #region Display Refresh

        private async void RefreshChartsWithoutLoadingOverlay()
        {
            await RefreshChartsAndStatsWithLoadingOverlay();
        }

        private async Task RefreshChartsAndStatsWithLoadingOverlay()
        {
            if (_allRecords.Count == 0) return;

            string station = TxtStationName.Text;

            ShowLoadingOverlay(station, "Applying filters to " + _allRecords.Count.ToString("N0") + " records...", 0);

            await Task.Yield();

            await ApplyActiveFiltersToAllRecords();

            string filterDetail = _filteredRecords.Count < _allRecords.Count
                ? (_allRecords.Count - _filteredRecords.Count).ToString("N0") + " records excluded by active filters"
                : "No active filters — showing all records";

            ShowLoadingOverlay(station,
                "Filters applied  —  " + _filteredRecords.Count.ToString("N0") + " records match",
                10,
                detail: filterDetail);

            await BuildAllChartDataFromFilteredRecords();

            await RenderAllCachedChartsToUI();

            ShowLoadingOverlay(station, "Updating records table and statistics...", 95);

            await Task.Yield();

            GridRecords.ItemsSource = _filteredRecords;
            UpdateSidebarStats();
            UpdateTabHighlightsForActiveFilter();

            ShowLoadingOverlay(station, "Preloading charts for all tabs...", 100,
                detail: "Cycling through " + GetAllSupportedMessageTypes().Length + " message type tabs");

            await CycleThroughAllTabsToTriggerWpfLayoutRendering();

            HideLoadingOverlay();
        }

        private async Task ApplyActiveFiltersToAllRecords()
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
                    if (r.TimestampParsed == DateTime.MinValue) continue;
                    if (r.TimestampParsed < start || r.TimestampParsed > end) continue;
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

                _recordsGroupedByDay.Clear();
                foreach (var r in _filteredRecords)
                {
                    if (r.TimestampParsed == DateTime.MinValue) continue;
                    DateTime key = r.TimestampParsed.Date;
                    if (!_recordsGroupedByDay.ContainsKey(key))
                        _recordsGroupedByDay[key] = new List<ResponseRecord>();
                    _recordsGroupedByDay[key].Add(r);
                }
            });

            // Filters changed → stats cache is stale
            _statsCalculator.InvalidateCache();
        }

        private async Task BuildAllChartDataFromFilteredRecords()
        {
            var messageTypes = GetAllSupportedMessageTypes();
            int totalSteps = messageTypes.Length * 3;
            int doneCount = 0;

            string station = TxtStationName.Text;

            ShowLoadingOverlay(station, "Clearing chart cache...", 13,
                detail: "Invalidating " + _chartCache.Count + " cached charts from previous state");
            await Task.Yield();
            _chartCache.Clear();

            ShowLoadingOverlay(station, "Preparing data for all message types...", 15,
                detail: string.Join("  ·  ", messageTypes.Select(t => t.ToString().Replace("_", " "))));
            await Task.Delay(400);

            var preparedInputs = await Task.Run(() =>
                _chartFactory.PrepareAllInputs(_filteredRecords, messageTypes));

            int nonEmpty = preparedInputs.Count(kv => kv.Value.Records.Count > 0);

            var typeLines = preparedInputs
                .OrderByDescending(kv => kv.Value.Records.Count)
                .Select(kv =>
                    kv.Key.ToString().Replace("_", " ") + ":  " + kv.Value.Records.Count.ToString("N0") + " records");

            string typeDetail = string.Join(Environment.NewLine, typeLines);

            ShowLoadingOverlay(station,
                nonEmpty + " message types ready  —  " + _filteredRecords.Count.ToString("N0") + " records total",
                20,
                detail: typeDetail);
            await Task.Delay(900);

            foreach (var messageType in messageTypes)
            {
                if (!preparedInputs.TryGetValue(messageType, out var input)) continue;
                if (input.Records.Count == 0) continue;

                string typeName = messageType.ToString().Replace("_", " ");

                foreach (var chartType in new[] { ChartType.Trend, ChartType.Histogram, ChartType.Timeline })
                {
                    int pct = 20 + (doneCount * 60 / totalSteps);
                    ShowLoadingOverlay(
                        station,
                        "Building  " + typeName + "  —  " + chartType,
                        pct,
                        detail: "Chart " + (doneCount + 1) + " / " + totalSteps
                                + "   ·   " + input.Records.Count.ToString("N0") + " records"
                                + "   ·   " + typeName);
                    await Task.Delay(160);

                    var data = _chartFactory.BuildSingle(chartType, input);
                    if (data != null)
                        _chartCache[(messageType, chartType)] = data;

                    doneCount++;
                }
            }

            ShowLoadingOverlay(station,
                "Chart cache built  —  " + doneCount + " charts ready",
                80,
                detail: "Cached:  " + _chartCache.Count + " charts  ·  " + nonEmpty + " message types");
            await Task.Delay(300);
        }

        private async Task RenderAllCachedChartsToUI()
        {
            string station = TxtStationName.Text;
            var types = GetAllSupportedMessageTypes();

            ShowLoadingOverlay(station, "Rendering charts to UI...", 82,
                detail: "Writing " + _chartCache.Count + " charts into " + types.Length + " tabs");
            await Task.Delay(40);

            for (int i = 0; i < types.Length; i++)
            {
                var mt = types[i];
                ShowLoadingOverlay(station,
                    "Rendering  " + mt.ToString().Replace("_", " "),
                    82 + (i * 5 / types.Length),
                    detail: "Tab " + (i + 1) + " / " + types.Length + "  —  " + mt.ToString().Replace("_", " "));
                RenderCachedChartForMessageType(mt);
                await Task.Delay(8);
            }

            ShowLoadingOverlay(station, "Initializing timelines...", 87,
                detail: "Setting first available day for each tab");
            foreach (var mt in types)
                _trendChartRenderer.InitializeTimelineWithFirstAvailableDay(mt);
        }

        private async Task CycleThroughAllTabsToTriggerWpfLayoutRendering()
        {
            var originalTab = MainTabControl.SelectedItem;

            foreach (var messageType in GetAllSupportedMessageTypes())
            {
                foreach (TabItem tab in MainTabControl.Items)
                    if (tab.Tag?.ToString() == messageType.ToString())
                    {
                        MainTabControl.SelectedItem = tab;
                        break;
                    }

                await Task.Delay(80);
            }

            _tabsUserHasAlreadySeen.Clear();
            MainTabControl.SelectedItem = originalTab;
        }

        private MessageType[] GetAllSupportedMessageTypes() => new[]
        {
            MessageType.UNIT_INFO, MessageType.NEXT_OPERATION, MessageType.UNIT_CHECKIN,
            MessageType.UNIT_RESULT, MessageType.LOAD_MATERIAL,
            MessageType.REQ_MATERIAL_INFO, MessageType.REQ_SETUP_CHANGE2
        };

        #endregion

        #region Chart Rendering

        private void RenderCachedChartForMessageType(MessageType messageType)
        {
            try
            {
                var targetPanel = GetChartPanelForMessageType(messageType);
                if (targetPanel == null) return;
                targetPanel.Children.Clear();

                double availableHeight = ActualHeight - 160;
                var context = new RenderContext
                    { AvailableHeightPixels = (int)availableHeight, MessageType = messageType };

                _chartCache.TryGetValue((messageType, ChartType.Trend), out ChartData trendData);
                _chartCache.TryGetValue((messageType, ChartType.Histogram), out ChartData histogramData);

                if (trendData?.TrendChart != null)
                    targetPanel.Children.Add(_chartFactory.Render(ChartType.Trend, trendData, context));

                if (histogramData?.Charts != null && histogramData.Charts.Count > 0)
                    targetPanel.Children.Add(_chartFactory.Render(ChartType.Histogram, histogramData, context));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error displaying chart for {messageType}:\n\n{ex.Message}",
                    "Chart Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private StackPanel GetChartPanelForMessageType(MessageType type)
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

        #region Sidebar Stats

        private void UpdateSidebarStats()
        {
            if (!(MainTabControl.SelectedItem is TabItem selected) || selected.Tag == null) return;

            MessageType type = (MessageType)Enum.Parse(typeof(MessageType), selected.Tag.ToString());
            StatsResult stats = _statsCalculator.Calculate(_filteredRecords, type);

            if (stats == null)
            {
                ClearSidebarStats();
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

        private void ClearSidebarStats()
        {
            TxtTabRecords.Text = "Records: 0";
            TxtTabAvg.Text = "0 ms";
            TxtTabP95.Text = "0 ms";
            TxtTabMin.Text = "Min: 0 ms";
            TxtTabMax.Text = "Max: 0 ms";
            TxtTabStability.Text = "N/A";
            TxtTabStability.Foreground = Brushes.Gray;
        }

        private void UpdateTabHighlightsForActiveFilter()
        {
            bool anyActive = AnyNonDateFilterIsActive();
            foreach (TabItem tab in MainTabControl.Items)
            {
                if (tab.Tag == null) continue;
                var type = TryParseMessageType(tab.Tag.ToString());
                if (type == null) continue;
                bool highlight = anyActive && _filteredRecords.Any(r => r.Type == type.Value);
                tab.FontWeight = highlight ? FontWeights.Bold : FontWeights.Normal;
                tab.FontSize = highlight ? 13 : 11;
            }
        }

        private MessageType? TryParseMessageType(string tag)
        {
            try
            {
                return (MessageType)Enum.Parse(typeof(MessageType), tag);
            }
            catch
            {
                return null;
            }
        }

        #endregion

        #region Station Bar

        private int _stationScrollOffset = 0;

        private void RebuildStationBar()
        {
            if (StationBarPanel == null) return;

            StationBarPanel.Children.Clear();

            // Dropdown button
            var dropdown = new Button
            {
                Content = "▾  Stations",
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 190, 130)),
                Background = new SolidColorBrush(Color.FromRgb(8, 32, 18)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(22, 80, 44)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(12, 0, 12, 0),
                Height = 44,
                Cursor = System.Windows.Input.Cursors.Hand,
                Margin = new Thickness(0, 0, 4, 0)
            };

            var contextMenu = new ContextMenu { Background = new SolidColorBrush(Color.FromRgb(13, 30, 18)) };

            foreach (var st in _loadedStations)
            {
                var captured = st;
                var item = new MenuItem
                {
                    Header = st.StationName + (!string.IsNullOrEmpty(st.LineName) ? "  ·  " + st.LineName : ""),
                    FontSize = 11,
                    Foreground = new SolidColorBrush(Color.FromRgb(180, 230, 195)),
                    Background = new SolidColorBrush(Color.FromRgb(13, 30, 18))
                };

                item.Click += async (s, e) => await SwitchToStation(captured);

                contextMenu.Items.Add(item);
            }

            dropdown.Click += (s, e) =>
            {
                contextMenu.PlacementTarget = dropdown;
                contextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
                contextMenu.IsOpen = true;
            };

            StationBarPanel.Children.Add(dropdown);

            // Scroll left button
            var btnLeft = BuildScrollButton("◀", () =>
            {
                if (_stationScrollOffset > 0)
                {
                    _stationScrollOffset--;
                    RebuildStationBar();
                }
            });

            StationBarPanel.Children.Add(btnLeft);

            // Visible chevrons — determined by available width after dropdown+buttons
            // Show from _stationScrollOffset, fitting as many as possible
            int visibleCount = Math.Min(8, _loadedStations.Count - _stationScrollOffset);

            for (int i = _stationScrollOffset;
                 i < _stationScrollOffset + visibleCount && i < _loadedStations.Count;
                 i++)
            {
                var station = _loadedStations[i];
                bool isFirst = i == _stationScrollOffset;

                StationBarPanel.Children.Add(BuildChevron(station, isFirst));
            }

            // Scroll right button
            var btnRight = BuildScrollButton("▶", () =>
            {
                if (_stationScrollOffset + 8 < _loadedStations.Count)
                {
                    _stationScrollOffset++;
                    RebuildStationBar();
                }
            });

            StationBarPanel.Children.Add(btnRight);
        }

        private Button BuildScrollButton(string label, Action onClick)
        {
            var btn = new Button
            {
                Content = label,
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 190, 130)),
                Background = new SolidColorBrush(Color.FromRgb(8, 32, 18)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(22, 80, 44)),
                BorderThickness = new Thickness(1),
                Width = 28,
                Height = 44,
                Padding = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand,
                Margin = new Thickness(2, 0, 2, 0)
            };

            btn.Click += (s, e) => onClick();

            return btn;
        }

        private Canvas BuildChevron(StationInfo station, bool isFirst)
        {
            bool isActive = _activeStation?.FolderPath == station.FolderPath;

            const double h = 44;
            const double tip = 12;

            // Active = orange (7-day avg line colour), inactive = light green
            var fillColor = isActive ? Color.FromRgb(140, 80, 10) : Color.FromRgb(22, 110, 55);
            var hoverColor = isActive ? Color.FromRgb(170, 100, 15) : Color.FromRgb(30, 140, 70);
            var strokeColor = isActive ? Color.FromRgb(220, 140, 40) : Color.FromRgb(56, 190, 100);
            var nameColor = isActive ? Color.FromRgb(255, 220, 160) : Color.FromRgb(210, 245, 220);
            var subColor = isActive ? Color.FromRgb(220, 170, 100) : Color.FromRgb(130, 210, 155);

            bool hasSub = !string.IsNullOrEmpty(station.LineName) || !string.IsNullOrEmpty(station.ComputerName);

            string subText = hasSub
                ? string.Join("  ·  ", new[] { station.LineName, station.ComputerName }
                    .Where(x => !string.IsNullOrEmpty(x)))
                : "";

            // Measure text to size canvas
            var measureBlock = new TextBlock
            {
                Text = station.StationName, FontSize = 11,
                FontWeight = isActive ? FontWeights.SemiBold : FontWeights.Normal
            };
            measureBlock.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double nameW = measureBlock.DesiredSize.Width;

            if (hasSub)
            {
                var subMeasure = new TextBlock { Text = subText, FontSize = 9 };
                subMeasure.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                nameW = Math.Max(nameW, subMeasure.DesiredSize.Width);
            }

            double leftPad = isFirst ? 14 : 22;
            double canvasW = nameW + leftPad + tip + 14;

            var canvas = new Canvas
            {
                Width = canvasW,
                Height = h,
                Cursor = System.Windows.Input.Cursors.Hand,
                Margin = new Thickness(isFirst ? 0 : -1, 0, 0, 0)
            };

            var poly = new System.Windows.Shapes.Polygon
            {
                Fill = new SolidColorBrush(fillColor),
                Stroke = new SolidColorBrush(strokeColor),
                StrokeThickness = 1
            };

            if (isFirst)
            {
                poly.Points.Add(new Point(0, 0));
                poly.Points.Add(new Point(canvasW - tip, 0));
                poly.Points.Add(new Point(canvasW, h / 2));
                poly.Points.Add(new Point(canvasW - tip, h));
                poly.Points.Add(new Point(0, h));
            }
            else
            {
                poly.Points.Add(new Point(0, 0));
                poly.Points.Add(new Point(canvasW - tip, 0));
                poly.Points.Add(new Point(canvasW, h / 2));
                poly.Points.Add(new Point(canvasW - tip, h));
                poly.Points.Add(new Point(0, h));
                poly.Points.Add(new Point(tip, h / 2));
            }

            canvas.Children.Add(poly);

            double topPad = hasSub ? (h - 24) / 2.0 : (h - 14) / 2.0;

            var nameBlock = new TextBlock
            {
                Text = station.StationName,
                FontSize = 11,
                FontWeight = isActive ? FontWeights.SemiBold : FontWeights.Normal,
                Foreground = new SolidColorBrush(nameColor)
            };
            Canvas.SetLeft(nameBlock, leftPad);
            Canvas.SetTop(nameBlock, topPad);
            canvas.Children.Add(nameBlock);

            if (hasSub)
            {
                var subBlock = new TextBlock
                {
                    Text = subText,
                    FontSize = 9,
                    Foreground = new SolidColorBrush(subColor)
                };
                Canvas.SetLeft(subBlock, leftPad);
                Canvas.SetTop(subBlock, topPad + 15);
                canvas.Children.Add(subBlock);
            }

            poly.MouseEnter += (s, e) => poly.Fill = new SolidColorBrush(hoverColor);
            poly.MouseLeave += (s, e) => poly.Fill = new SolidColorBrush(fillColor);
            canvas.MouseEnter += (s, e) => poly.Fill = new SolidColorBrush(hoverColor);
            canvas.MouseLeave += (s, e) => poly.Fill = new SolidColorBrush(fillColor);

            var captured = station;
            canvas.MouseLeftButtonUp += async (s, e) =>
            {
                if (_activeStation?.FolderPath == captured.FolderPath) return;

                await SwitchToStation(captured);
            };

            return canvas;
        }

        private void UpdateActiveStationButton()
        {
            if (StationBarPanel == null) return;

            RebuildStationBar();
        }

        #endregion

        #region Loading Overlay

        private long _lastOverlayUpdateMs = 0;

        private void ShowLoadingOverlay(string title, string status, int progress,
            string detail = null, int? fileCount = null, int? recordCount = null, int? typeCount = null)
        {
            if (LoadingOverlay == null) return;

            long nowMs = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond;
            bool isThrottled = (nowMs - _lastOverlayUpdateMs) < 200;

            // Always update progress and title
            LoadingTitle.Text = title;
            LoadingProgress.Value = progress;
            LoadingPercentage.Text = progress + "%";

            // Throttle status/detail to avoid fast flickering
            if (!isThrottled)
            {
                _lastOverlayUpdateMs = nowMs;

                LoadingStatus.Text = status;

                if (LoadingDetail != null)
                    LoadingDetail.Text = detail ?? "";

                if (LoadingFileCount != null && fileCount.HasValue)
                    LoadingFileCount.Text = fileCount.Value.ToString("N0");

                if (LoadingRecordCount != null && recordCount.HasValue)
                    LoadingRecordCount.Text = recordCount.Value.ToString("N0");

                if (LoadingTypeCount != null && typeCount.HasValue)
                    LoadingTypeCount.Text = typeCount.Value.ToString();
            }

            LoadingOverlay.Visibility = Visibility.Visible;
        }

        private void HideLoadingOverlay()
        {
            if (LoadingOverlay == null) return;
            LoadingOverlay.Visibility = Visibility.Collapsed;
        }

        private void BtnCloseLoadingOverlay_Click(object sender, RoutedEventArgs e)
        {
            HideLoadingOverlay();
        }

        #endregion
    }
}