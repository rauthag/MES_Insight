using MESInsight.UI;
using MESInsight.Charts.Renderers;
using MESInsight.Charts.Builders;
using MESInsight.Core;
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
using MESInsight.Charts;
using RTAnalyzer.Core;

namespace MESInsight
{
    public class LoadingStationLogEntry : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;

        private string _statusIcon = "○";
        private string _iconColor = "#4E5A4E";
        private string _line1Color = "#8B949E";

        public string StatusIcon
        {
            get => _statusIcon;
            set
            {
                _statusIcon = value;
                PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(StatusIcon)));
            }
        }

        public string IconColor
        {
            get => _iconColor;
            set
            {
                _iconColor = value;
                PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IconColor)));
            }
        }

        public string Line1 { get; set; }
        public string Line2 { get; set; }

        public string Line1Color
        {
            get => _line1Color;
            set
            {
                _line1Color = value;
                PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Line1Color)));
            }
        }

        public bool IsHeader { get; set; } = false;

        public System.Windows.Visibility Line2Visibility =>
            string.IsNullOrEmpty(Line2) ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible;
    }

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

        private List<StationInfo> _lazyLoadStations = new List<StationInfo>();
        private HashSet<string> _stationReadyGlow = new HashSet<string>();
        private bool _bgLoadingRunning = false;
        private System.Threading.CancellationTokenSource _bgCts = null;

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
                if (LoadingStationLog != null)
                    LoadingStationLog.ItemsSource = _stationLogEntries;

                StartupWindow startup = new StartupWindow();
                bool? result = startup.ShowDialog();
                if (result == true && !string.IsNullOrEmpty(startup.SelectedPath))
                    Dispatcher.BeginInvoke(new Action(async () =>
                    {
                        if (startup.SelectedPaths != null && startup.SelectedPaths.Count > 1)
                            await LoadAllStationsFromPaths(startup.SelectedPaths);
                        else
                            await LoadAllStationsFromRoot(startup.SelectedPath);
                    }));
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

        private async Task LoadAllStationsFromPaths(List<string> paths)
        {
            ShowLoadingOverlay("Scanning...", "Building station list from selection...", 0);
            await Task.Yield();

            System.Text.RegularExpressions.RegexOptions rxOpts =
                System.Text.RegularExpressions.RegexOptions.IgnoreCase;

            List<StationInfo> allStations = await Task.Run(() =>
            {
                List<StationInfo> result = new List<StationInfo>();
                foreach (string path in paths)
                {
                    List<StationInfo> found = DataLoader.FindStations(path);
                    if (found.Count == 0)
                        found.Add(new StationInfo
                        {
                            FolderPath = path,
                            StationName = System.IO.Path.GetFileName(path)
                        });
                    result.AddRange(found);
                }

                return result;
            });

            foreach (StationInfo st in allStations)
            {
                if (st.Category != StationCategory.GHP) continue;
                string n = st.StationName + " " + st.FolderPath;
                if (System.Text.RegularExpressions.Regex.IsMatch(n, @"(?:^|[ _-])LCS[0-9]+", rxOpts))
                    st.Category = StationCategory.LCS;
                else if (System.Text.RegularExpressions.Regex.IsMatch(n, @"backflush", rxOpts))
                    st.Category = StationCategory.Backflush;
                else if (System.Text.RegularExpressions.Regex.IsMatch(n, @"connector|comcell|ghpnetty", rxOpts))
                    st.Category = StationCategory.Connector;
            }

            HideLoadingOverlay();

            List<StationInfo> ghpStations = allStations
                .Where(s => s.Category == StationCategory.GHP || s.Category == StationCategory.Unknown).ToList();
            List<StationInfo> lcsStations = allStations.Where(s => s.Category == StationCategory.LCS).ToList();
            List<StationInfo> backflushList = allStations.Where(s => s.Category == StationCategory.Backflush).ToList();
            List<StationInfo> connectorList = allStations.Where(s => s.Category == StationCategory.Connector).ToList();

            Window scanSpinner = ShowScanningSpinner("Counting files...");
            string commonRoot = paths.Count == 1
                ? paths[0]
                : System.IO.Path.GetDirectoryName(paths[0]) ?? paths[0];

            Dictionary<int, MonthFileInfo> fileCounts = await Task.Run(() =>
                DataLoader.CountFilesByMonthCutoffs(commonRoot,
                    new[] { 30, 60, 90, 120, 150, 180, 210, 240, 270, 300, 330, 365 }));
            scanSpinner?.Close();

            StartupWindow.LoadOptionsDialog optDlg =
                new StartupWindow.LoadOptionsDialog(ghpStations, lcsStations, backflushList, connectorList, fileCounts)
                    { Owner = this };
            if (optDlg.ShowDialog() != true) return;

            _dataLoader.DateFilter = optDlg.FilterByDate
                ? DateTime.Now.AddDays(-optDlg.MaxMonths)
                : (DateTime?)null;

            List<StationInfo> stations = allStations
                .Where(s =>
                    ((s.Category == StationCategory.GHP || s.Category == StationCategory.Unknown) ||
                     (optDlg.IncludeLcs && s.Category == StationCategory.LCS) ||
                     (optDlg.IncludeBackflush && s.Category == StationCategory.Backflush) ||
                     (optDlg.IncludeConnectors && s.Category == StationCategory.Connector)) &&
                    !optDlg.LazyLoadFolderPaths.Contains(s.FolderPath))
                .ToList();

            _lazyLoadStations = allStations
                .Where(s => optDlg.LazyLoadFolderPaths.Contains(s.FolderPath))
                .ToList();

            _pendingOptionalStations = new List<StationInfo>();
            _isBackgroundLoading = true;
            _isOverlayMinimized = false;
            ShowLoadingOverlay("Loading...", "Preparing...", 0);
            await Task.Yield();

            _loadedStations = stations;
            _stationDataCache.Clear();
            _stationChartCache.Clear();
            _stationLogEntries.Clear();

            void AddSection(string header, List<StationInfo> list)
            {
                if (list.Count == 0) return;
                _stationLogEntries.Add(new LoadingStationLogEntry
                {
                    StatusIcon = "", IconColor = "#3FB950",
                    Line1 = header, Line2 = "", Line1Color = "#3FB950", IsHeader = true
                });
                foreach (StationInfo st in list)
                    _stationLogEntries.Add(new LoadingStationLogEntry
                    {
                        StatusIcon = "○", IconColor = "#4E5A4E",
                        Line1 = st.StationName,
                        Line2 = string.Join("  ·  ", new[] { st.LineName, st.ComputerName }
                            .Where(x => !string.IsNullOrEmpty(x))),
                        Line1Color = "#8B949E"
                    });
            }

            AddSection("GHP STATIONS", ghpStations.Where(s => stations.Contains(s)).ToList());
            AddSection("LCS STATIONS", lcsStations.Where(s => stations.Contains(s)).ToList());
            AddSection("BACKFLUSH STATIONS", backflushList.Where(s => stations.Contains(s)).ToList());

            ShowLoadingOverlay(
                "Found " + stations.Count + " station" + (stations.Count != 1 ? "s" : ""),
                "Preparing to load...", 5, typeCount: stations.Count);
            await Task.Delay(300);
            RebuildStationBar();

            int totalFiles = 0;

            for (int i = 0; i < stations.Count; i++)
            {
                StationInfo st = stations[i];
                int liveFileCount = 0;

                if (i > 0 && !CheckRamBeforeLoading(100000))
                {
                    bool proceed = ShowRamWarningDialog();
                    if (!proceed)
                    {
                        for (int j = i; j < stations.Count; j++)
                            _lazyLoadStations.Add(stations[j]);
                        break;
                    }
                }

                UpdateStationBarLoadingState(st.FolderPath, isLoading: true);

                DataLoadResult loadResult = await Task.Run(() => _dataLoader.Load(st.FolderPath,
                    (status, percent, extra) =>
                    {
                        if (status.StartsWith("Reading "))
                            System.Threading.Interlocked.Increment(ref liveFileCount);

                        int fc = liveFileCount;
                        int innerPct = 5 + (i * 88 / stations.Count) + (percent * 88 / 100 / stations.Count);

                        long nowMs = System.DateTime.UtcNow.Ticks / System.TimeSpan.TicksPerMillisecond;
                        if (nowMs - _lastBeginInvokeMs >= 150)
                        {
                            _lastBeginInvokeMs = nowMs;
                            Dispatcher.BeginInvoke(new Action(() =>
                            {
                                bool isReading = status.StartsWith("Reading ");
                                string fileName = isReading ? status.Substring(8).TrimEnd('.', ' ') : null;
                                string detail;
                                if (isReading && fc > 0)
                                {
                                    detail = "File " + fc + (fileName != null ? "  —  " + fileName : "");
                                    if (!string.IsNullOrEmpty(extra)) detail += Environment.NewLine + extra;
                                }
                                else if (status.StartsWith("Scanning")) detail = "Scanning for log files...";
                                else detail = status;

                                ShowLoadingOverlay(
                                    "Station " + (i + 1) + " / " + stations.Count,
                                    st.StationName, innerPct,
                                    detail: detail, fileCount: fc, typeCount: stations.Count);
                            }));
                        }
                    }));

                string displayName = !string.IsNullOrEmpty(loadResult.StationName)
                    ? loadResult.StationName
                    : st.StationName;

                st.StationName = displayName;
                _stationDataCache[st.FolderPath] = (loadResult.Records, displayName);

                if (i < _stationLogEntries.Count)
                {
                    _stationLogEntries[i].StatusIcon = loadResult.Records.Count > 0 ? "✓" : "✕";
                    _stationLogEntries[i].IconColor = loadResult.Records.Count > 0 ? "#3FB950" : "#C03030";
                    _stationLogEntries[i].Line1Color = loadResult.Records.Count > 0 ? "#C9D1D9" : "#C03030";
                    _stationLogEntries[i].Line1 = displayName +
                                                  (loadResult.Records.Count > 0
                                                      ? "  —  " + loadResult.Records.Count.ToString("N0") + " records"
                                                      : "  —  no records");
                }

                totalFiles += liveFileCount;

                ShowLoadingOverlay(
                    "Station " + (i + 1) + " / " + stations.Count + "  —  building charts",
                    st.StationName, 5 + ((i * 88 + 44) / stations.Count),
                    detail: "Building charts for " + loadResult.Records.Count.ToString("N0") + " records...",
                    fileCount: totalFiles, recordCount: loadResult.Records.Count, typeCount: stations.Count);

                await Task.Yield();

                Dictionary<(MessageType, ChartType), ChartData> stationCharts =
                    await BuildChartsForRecords(loadResult.Records, displayName);
                _stationChartCache[st.FolderPath] = stationCharts;

                DropRecordsFromCache(st.FolderPath);

                UpdateStationBarLoadingState(st.FolderPath, isLoading: false);
                RebuildStationBarThrottled();

                if (i == 0)
                {
                    bool overlayWasVisible = LoadingOverlay.Visibility == Visibility.Visible;
                    await SwitchToStation(st);
                    if (overlayWasVisible)
                        ShowLoadingOverlay(
                            "Loading " + (i + 2) + " / " + stations.Count + "...", "",
                            5 + ((i + 1) * 88 / stations.Count), typeCount: stations.Count);
                }
            }

            int totalRecords = stations.Sum(s =>
                _stationDataCache.ContainsKey(s.FolderPath)
                    ? _stationDataCache[s.FolderPath].records.Count
                    : 0);

            ShowLoadingOverlay(
                "All stations ready",
                stations.Count + " stations  ·  " + totalRecords.ToString("N0") + " records total",
                100, fileCount: totalFiles, recordCount: totalRecords, typeCount: stations.Count);

            await Task.Delay(400);

            if (stations.Count > 0)
                await SwitchToStation(stations[0]);

            _isBackgroundLoading = false;
            HideLoadingOverlay();
            RebuildStationBar();

            if (_lazyLoadStations.Count > 0)
                StartBackgroundLoading();
        }

        private async Task LoadAllStationsFromRoot(string rootPath)
        {
            ShowLoadingOverlay("Scanning...", "Looking for stations...", 0, detail: rootPath);

            await Task.Yield();

            var allStations = await Task.Run(() => DataLoader.FindStations(rootPath));

            if (allStations.Count == 0)
                allStations.Add(new StationInfo
                    { FolderPath = rootPath, StationName = System.IO.Path.GetFileName(rootPath) });

            var rxOpts = System.Text.RegularExpressions.RegexOptions.IgnoreCase;
            foreach (var st in allStations)
            {
                if (st.Category != StationCategory.GHP) continue;
                string n = st.StationName + " " + st.FolderPath;
                if (System.Text.RegularExpressions.Regex.IsMatch(n, @"(?:^|[ _-])LCS[0-9]+", rxOpts))
                    st.Category = StationCategory.LCS;
                else if (System.Text.RegularExpressions.Regex.IsMatch(n, @"backflush", rxOpts))
                    st.Category = StationCategory.Backflush;
            }

            HideLoadingOverlay();

            // Detect connectors
            var rxOpts2 = System.Text.RegularExpressions.RegexOptions.IgnoreCase;
            foreach (var st in allStations)
            {
                if (st.Category != StationCategory.GHP) continue;
                string n = st.StationName + " " + st.FolderPath;
                if (System.Text.RegularExpressions.Regex.IsMatch(n, @"connector|comcell|ghpnetty", rxOpts2))
                    st.Category = StationCategory.Connector;
            }

            var ghpStations = allStations
                .Where(s => s.Category == StationCategory.GHP || s.Category == StationCategory.Unknown).ToList();
            var lcsStationsList = allStations.Where(s => s.Category == StationCategory.LCS).ToList();
            var backflushList = allStations.Where(s => s.Category == StationCategory.Backflush).ToList();
            var connectorList = allStations.Where(s => s.Category == StationCategory.Connector).ToList();

            var scanSpinner = ShowScanningSpinner("Scanning stations...");

            var fileCounts = await Task.Run(() =>
                DataLoader.CountFilesByMonthCutoffs(rootPath, new[] { 1, 2, 3, 6, 12, 24 }));

            scanSpinner?.Close();

            var optDlg = new StartupWindow.LoadOptionsDialog(ghpStations, lcsStationsList, backflushList, connectorList,
                fileCounts);
            optDlg.Owner = this;
            if (optDlg.ShowDialog() != true) return;

            DateTime? dateFilter = optDlg.FilterByDate
                ? DateTime.Now.AddMonths(-optDlg.MaxMonths)
                : (DateTime?)null;

            _dataLoader.DateFilter = dateFilter;

            List<StationInfo> stations = allStations
                .Where(s =>
                    ((s.Category == StationCategory.GHP || s.Category == StationCategory.Unknown) ||
                     (optDlg.IncludeLcs && s.Category == StationCategory.LCS) ||
                     (optDlg.IncludeBackflush && s.Category == StationCategory.Backflush) ||
                     (optDlg.IncludeConnectors && s.Category == StationCategory.Connector)) &&
                    !optDlg.ExcludedFolderPaths.Contains(s.FolderPath) &&
                    !optDlg.LazyLoadFolderPaths.Contains(s.FolderPath))
                .ToList();

            _lazyLoadStations = allStations
                .Where(s => optDlg.LazyLoadFolderPaths.Contains(s.FolderPath))
                .ToList();

            _pendingOptionalStations = new List<StationInfo>();

            _isBackgroundLoading = true;
            _isOverlayMinimized = false;
            ShowLoadingOverlay("Loading...", "Preparing...", 0);
            await Task.Yield();

            _loadedStations = stations;
            _stationDataCache.Clear();
            _stationChartCache.Clear();
            _stationLogEntries.Clear();

            var ghpLogList = stations
                .Where(s => s.Category == StationCategory.GHP || s.Category == StationCategory.Unknown).ToList();
            var lcsLogList = stations.Where(s => s.Category == StationCategory.LCS).ToList();
            var backflushLogList = stations.Where(s => s.Category == StationCategory.Backflush).ToList();

            void AddSection(string header, List<StationInfo> list)
            {
                if (list.Count == 0) return;
                _stationLogEntries.Add(new LoadingStationLogEntry
                {
                    StatusIcon = "",
                    IconColor = "#3FB950",
                    Line1 = header,
                    Line2 = "",
                    Line1Color = "#3FB950",
                    IsHeader = true
                });
                foreach (var st in list)
                {
                    _stationLogEntries.Add(new LoadingStationLogEntry
                    {
                        StatusIcon = "○",
                        IconColor = "#4E5A4E",
                        Line1 = st.StationName,
                        Line2 = string.Join("  ·  ", new[] { st.LineName, st.ComputerName }
                            .Where(x => !string.IsNullOrEmpty(x))),
                        Line1Color = "#8B949E"
                    });
                }
            }

            AddSection("GHP STATIONS", ghpLogList);
            AddSection("LCS STATIONS", lcsLogList);
            AddSection("BACKFLUSH STATIONS", backflushLogList);

            ShowLoadingOverlay(
                "Found " + stations.Count + " station" + (stations.Count != 1 ? "s" : ""),
                "Preparing to load...",
                5,
                typeCount: stations.Count);

            await Task.Delay(300);

            // Build station bar immediately with all stations showing as pending
            RebuildStationBar();

            int totalFiles = 0;

            for (int i = 0; i < stations.Count; i++)
            {
                var st = stations[i];

                // Mark current station as loading in bar
                UpdateStationBarLoadingState(st.FolderPath, isLoading: true);

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

                            string detail;
                            if (isReading && fc > 0)
                            {
                                detail = "File " + fc + (fileName != null ? "  —  " + fileName : "");
                                if (!string.IsNullOrEmpty(extra))
                                    detail += Environment.NewLine + extra;
                            }
                            else if (status.StartsWith("Scanning"))
                                detail = "Scanning for log files...";
                            else
                                detail = status;

                            ShowLoadingOverlay(
                                "Station " + (i + 1) + " / " + stations.Count,
                                st.StationName,
                                innerPct,
                                detail: detail,
                                fileCount: fc,
                                typeCount: stations.Count);
                        }));
                    }));


                // Priority: log content name > folder name already set by BuildStationInfo
                string displayName = !string.IsNullOrEmpty(loadResult.StationName)
                    ? loadResult.StationName
                    : st.StationName;

                st.StationName = displayName;
                _stationDataCache[st.FolderPath] = (loadResult.Records, displayName);

                if (i < _stationLogEntries.Count)
                {
                    _stationLogEntries[i].StatusIcon = "✓";
                    _stationLogEntries[i].IconColor = "#3FB950";
                    _stationLogEntries[i].Line1Color = "#C9D1D9";
                    _stationLogEntries[i].Line1 = displayName +
                                                  (loadResult.Records.Count > 0
                                                      ? "  —  " + loadResult.Records.Count.ToString("N0") + " records"
                                                      : "  —  no records");
                }

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

                DropRecordsFromCache(st.FolderPath);

                UpdateStationBarLoadingState(st.FolderPath, isLoading: false);

                // First station ready — switch to it immediately
                if (i == 0)
                {
                    bool overlayWasVisible = LoadingOverlay.Visibility == Visibility.Visible;
                    await SwitchToStation(st);
                    if (overlayWasVisible)
                        ShowLoadingOverlay("Loading " + (i + 2) + " / " + stations.Count + "...", "",
                            5 + ((i + 1) * 88 / stations.Count), typeCount: stations.Count);
                }

                RebuildStationBar();
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

            if (stations.Count > 0)
                await SwitchToStation(stations[0]);

            if (_pendingOptionalStations.Count > 0)
                RebuildStationBarWithOptionalButton();

            _isBackgroundLoading = false;
            HideLoadingOverlay();
            RebuildStationBar();

            if (_lazyLoadStations.Count > 0)
                StartBackgroundLoading();
        }

        private void StartBackgroundLoading()
        {
            if (_bgLoadingRunning) return;
            _bgLoadingRunning = true;
            _bgCts = new System.Threading.CancellationTokenSource();
            System.Threading.CancellationToken token = _bgCts.Token;

            Task.Run(async () =>
            {
                foreach (StationInfo lazySt in _lazyLoadStations.ToList())
                {
                    if (token.IsCancellationRequested) break;

                    DataLoader loader = new DataLoader { DateFilter = _dataLoader.DateFilter };
                    DataLoadResult result = loader.Load(lazySt.FolderPath, null);

                    if (token.IsCancellationRequested) break;

                    string displayName = !string.IsNullOrEmpty(result.StationName)
                        ? result.StationName
                        : lazySt.StationName;

                    Dictionary<(MessageType, ChartType), ChartData> charts =
                        await BuildChartsForRecords(result.Records, displayName);

                    await Dispatcher.InvokeAsync(() =>
                    {
                        lazySt.StationName = displayName;
                        _stationDataCache[lazySt.FolderPath] = (result.Records, displayName);
                        _stationChartCache[lazySt.FolderPath] = charts;

                        if (!_loadedStations.Any(s => s.FolderPath == lazySt.FolderPath))
                            _loadedStations.Add(lazySt);

                        _lazyLoadStations.Remove(lazySt);
                        _stationReadyGlow.Add(lazySt.FolderPath);
                        RebuildStationBar();
                    });
                }

                await Dispatcher.InvokeAsync(() =>
                {
                    _bgLoadingRunning = false;
                    RebuildStationBar();
                });
            });
        }

        private async Task LoadLazyStationOnDemand(StationInfo station)
        {
            _stationLoadingState[station.FolderPath] = true;
            RebuildStationBar();

            DataLoader loader = new DataLoader { DateFilter = _dataLoader.DateFilter };
            DataLoadResult result = await Task.Run(() => loader.Load(station.FolderPath, null));

            string displayName = !string.IsNullOrEmpty(result.StationName)
                ? result.StationName
                : station.StationName;

            station.StationName = displayName;
            _stationDataCache[station.FolderPath] = (result.Records, displayName);

            Dictionary<(MessageType, ChartType), ChartData> charts =
                await BuildChartsForRecords(result.Records, displayName);
            _stationChartCache[station.FolderPath] = charts;

            if (!_loadedStations.Any(s => s.FolderPath == station.FolderPath))
                _loadedStations.Add(station);

            _lazyLoadStations.Remove(station);
            _stationLoadingState[station.FolderPath] = false;
            DropRecordsFromCache(station.FolderPath);
            RebuildStationBar();
            await SwitchToStation(station);
        }

        private async Task LoadOptionalStations()
        {
            var toLoad = _pendingOptionalStations.ToList();
            _pendingOptionalStations.Clear();

            RebuildStationBar();

            int totalFiles = 0;

            for (int i = 0; i < toLoad.Count; i++)
            {
                var st = toLoad[i];

                int liveFileCount = 0;

                var loadResult = await Task.Run(() => _dataLoader.Load(st.FolderPath,
                    (status, percent, extra) =>
                    {
                        if (status.StartsWith("Reading "))
                            System.Threading.Interlocked.Increment(ref liveFileCount);

                        int fc = liveFileCount;
                        int innerPct = (i * 88 / toLoad.Count) + (percent * 88 / 100 / toLoad.Count);

                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            bool isReading = status.StartsWith("Reading ");
                            string fileName = isReading ? status.Substring(8).TrimEnd('.', ' ') : null;
                            string detail;
                            if (isReading && fc > 0)
                            {
                                detail = "File " + fc + (fileName != null ? "  —  " + fileName : "");
                                if (!string.IsNullOrEmpty(extra))
                                    detail += Environment.NewLine + extra;
                            }
                            else if (status.StartsWith("Scanning"))
                                detail = "Scanning for log files...";
                            else
                                detail = status;

                            ShowLoadingOverlay(
                                "Loading optional  " + (i + 1) + " / " + toLoad.Count,
                                st.StationName,
                                innerPct,
                                detail: detail,
                                fileCount: fc,
                                typeCount: toLoad.Count);
                        }));
                    }));

                string displayName = loadResult.StationName.Length > 0 ? loadResult.StationName : st.StationName;

                _stationDataCache[st.FolderPath] = (loadResult.Records, displayName);
                _loadedStations.Add(st);

                if (!string.IsNullOrEmpty(loadResult.StationName))
                    st.StationName = loadResult.StationName;

                totalFiles += liveFileCount;

                ShowLoadingOverlay(
                    "Building charts  " + (i + 1) + " / " + toLoad.Count,
                    st.StationName,
                    (i * 88 / toLoad.Count) + 44,
                    detail: "Building charts for " + loadResult.Records.Count.ToString("N0") + " records...",
                    fileCount: totalFiles,
                    recordCount: loadResult.Records.Count,
                    typeCount: toLoad.Count);

                await Task.Yield();

                var stationCharts = await BuildChartsForRecords(loadResult.Records, displayName);
                _stationChartCache[st.FolderPath] = stationCharts;
                DropRecordsFromCache(st.FolderPath);
            }

            // Remove stations with no records
            _loadedStations = _loadedStations
                .Where(s => _stationDataCache.ContainsKey(s.FolderPath) &&
                            _stationDataCache[s.FolderPath].records.Count > 0)
                .ToList();
            
            DataLoader.DeduplicateNames(_loadedStations);


            _isBackgroundLoading = false;
            HideLoadingOverlay();
            RebuildStationBar();
        }

        private readonly Dictionary<string, bool> _stationLoadingState = new Dictionary<string, bool>();

        private void UpdateStationBarLoadingState(string folderPath, bool isLoading)
        {
            _stationLoadingState[folderPath] = isLoading;
        }

        private Window ShowScanningSpinner(string message)
        {
            var win = new Window
            {
                Width = 280,
                Height = 70,
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = new SolidColorBrush(Color.FromArgb(235, 8, 20, 12)),
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Topmost = true
            };

            var border = new Border
            {
                BorderBrush = new SolidColorBrush(Color.FromRgb(30, 100, 55)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8)
            };

            var stack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            var spin = new TextBlock
            {
                Text = "↻",
                FontSize = 20,
                Foreground = new SolidColorBrush(Color.FromRgb(63, 185, 80)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0),
                RenderTransformOrigin = new System.Windows.Point(0.5, 0.5),
                RenderTransform = new RotateTransform(0)
            };

            stack.Children.Add(spin);
            stack.Children.Add(new TextBlock
            {
                Text = message,
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(180, 225, 195)),
                VerticalAlignment = VerticalAlignment.Center
            });

            border.Child = stack;
            win.Content = border;

            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(60)
            };
            double angle = 0;
            timer.Tick += (s, e) =>
            {
                angle = (angle + 18) % 360;
                ((RotateTransform)spin.RenderTransform).Angle = angle;
            };
            timer.Start();
            win.Closed += (s, e) => timer.Stop();
            win.Show();

            return win;
        }

        // WIP: Skip station — not functional yet
        // private void BtnSkipStation_Click(object sender, RoutedEventArgs e)
        // {
        //     _isOverlayMinimized = true;
        //     LoadingOverlay.Visibility = Visibility.Collapsed;
        // }

        private void BtnMinimizeLoadingOverlay_Click(object sender, RoutedEventArgs e)
        {
            _isOverlayMinimized = true;
            LoadingOverlay.Visibility = Visibility.Collapsed;
        }

        private void RebuildStationBarWithOptionalButton()
        {
            if (StationBarPanel == null) return;

            RebuildStationBar();

            bool hasLcs = _pendingOptionalStations.Any(s => s.Category == StationCategory.LCS);
            bool hasBackflush = _pendingOptionalStations.Any(s => s.Category == StationCategory.Backflush);

            string label = "＋ Load ";
            if (hasLcs && hasBackflush) label += "LCS & Backflush";
            else if (hasLcs) label += "LCS";
            else if (hasBackflush) label += "Backflush";

            label += "  (" + _pendingOptionalStations.Count + ")";

            Button btn = new Button
            {
                Content = label,
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(255, 210, 130)),
                Background = new SolidColorBrush(Color.FromRgb(80, 45, 8)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(180, 100, 20)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10, 0, 10, 0),
                Height = 44,
                Cursor = System.Windows.Input.Cursors.Hand,
                Margin = new Thickness(8, 0, 0, 0)
            };

            btn.Click += async (s, e) =>
            {
                btn.IsEnabled = false;
                await LoadOptionalStations();
            };

            StationBarPanel.Children.Add(btn);
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

                await Task.Yield();
            }

            return result;
        }

        [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GlobalMemoryStatusEx(
            [System.Runtime.InteropServices.In, System.Runtime.InteropServices.Out]
            MemoryStatusEx lpBuffer);

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private class MemoryStatusEx
        {
            public uint dwLength = (uint)System.Runtime.InteropServices.Marshal.SizeOf(typeof(MemoryStatusEx));
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }

        private static long GetAvailableRamMb()
        {
            try
            {
                MemoryStatusEx status = new MemoryStatusEx();
                if (GlobalMemoryStatusEx(status))
                    return (long)(status.ullAvailPhys / 1024 / 1024);
            }
            catch
            {
            }

            return -1;
        }

        private void DropRecordsFromCache(string folderPath)
        {
            if (!_stationDataCache.ContainsKey(folderPath)) return;
            (List<ResponseRecord> records, string stationName) cached = _stationDataCache[folderPath];
            _stationDataCache[folderPath] = (null, cached.stationName);
        }

        private async Task<(List<ResponseRecord> records, string stationName)> ReloadRecordsFromDisk(
            StationInfo station, string stationName)
        {
            ShowLoadingOverlay("Loading records", station.StationName, 0,
                detail: "Reading from disk...");
            await Task.Yield();

            DataLoader loader = new DataLoader { DateFilter = _dataLoader.DateFilter };
            DataLoadResult result = await Task.Run(() => loader.Load(station.FolderPath, null));

            List<ResponseRecord> records = result.Records;
            _stationDataCache[station.FolderPath] = (records, stationName);

            HideLoadingOverlay();
            return (records, stationName);
        }

        private bool CheckRamBeforeLoading(long estimatedRecordCount)
        {
            long availMb = GetAvailableRamMb();
            if (availMb < 0) return true;

            long estimatedMb = estimatedRecordCount / 1000 * 4;
            long safetyBufferMb = 500;

            return availMb - estimatedMb >= safetyBufferMb;
        }

        private bool ShowRamWarningDialog()
        {
            long availMb = GetAvailableRamMb();

            Window dialog = new Window
            {
                Title = "Low Memory Warning",
                Width = 520,
                Height = 320,
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = new SolidColorBrush(Color.FromArgb(245, 8, 18, 12)),
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Topmost = true,
                Owner = this
            };

            Border outer = new Border
            {
                BorderBrush = new SolidColorBrush(Color.FromRgb(180, 60, 40)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10)
            };

            StackPanel root = new StackPanel { Margin = new Thickness(28, 24, 28, 24) };

            root.Children.Add(new TextBlock
            {
                Text = "⚠  Low Memory",
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(220, 80, 60)),
                Margin = new Thickness(0, 0, 0, 8)
            });

            string availText = availMb >= 0
                ? (availMb / 1024.0).ToString("0.#") + " GB available"
                : "Available RAM unknown";

            root.Children.Add(new TextBlock
            {
                Text = "Loading another station may cause the program to crash." + availText + ".",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(200, 200, 190)),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 20)
            });

            root.Children.Add(new TextBlock
            {
                Text = "What would you like to do?",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(130, 160, 140)),
                Margin = new Thickness(0, 0, 0, 12)
            });

            bool result = false;

            Button btnTrim = new Button
            {
                Content = "Trim old records to free memory",
                FontSize = 12,
                Padding = new Thickness(0, 10, 0, 10),
                Margin = new Thickness(0, 0, 0, 8),
                Background = new SolidColorBrush(Color.FromRgb(18, 60, 30)),
                Foreground = new SolidColorBrush(Color.FromRgb(150, 220, 170)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(40, 120, 60)),
                BorderThickness = new Thickness(1),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            btnTrim.Click += (s, e) =>
            {
                dialog.Tag = "trim";
                dialog.Close();
            };

            Button btnLoad = new Button
            {
                Content = "Load anyway (risk of crash)",
                FontSize = 12,
                Padding = new Thickness(0, 10, 0, 10),
                Margin = new Thickness(0, 0, 0, 8),
                Background = new SolidColorBrush(Color.FromRgb(60, 18, 14)),
                Foreground = new SolidColorBrush(Color.FromRgb(220, 140, 130)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(140, 40, 30)),
                BorderThickness = new Thickness(1),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            btnLoad.Click += (s, e) =>
            {
                result = true;
                dialog.Close();
            };

            Button btnCancel = new Button
            {
                Content = "Cancel — do not load",
                FontSize = 12,
                Padding = new Thickness(0, 10, 0, 10),
                Background = new SolidColorBrush(Color.FromRgb(14, 30, 18)),
                Foreground = new SolidColorBrush(Color.FromRgb(100, 120, 108)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(30, 60, 38)),
                BorderThickness = new Thickness(1),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            btnCancel.Click += (s, e) => dialog.Close();

            root.Children.Add(btnTrim);
            root.Children.Add(btnLoad);
            root.Children.Add(btnCancel);

            outer.Child = root;
            dialog.Content = outer;
            dialog.ShowDialog();

            if (dialog.Tag?.ToString() == "trim")
            {
                TrimOldRecordsFromAllStations();
                return true;
            }

            return result;
        }

        private void TrimOldRecordsFromAllStations()
        {
            TrimRecordsDialog trimDialog = new TrimRecordsDialog { Owner = this };
            if (trimDialog.ShowDialog() != true) return;

            DateTime cutoff = DateTime.Now.AddMonths(-trimDialog.SelectedMonths);

            foreach (StationInfo st in _loadedStations)
            {
                if (!_stationDataCache.ContainsKey(st.FolderPath)) continue;
                (List<ResponseRecord> records, string name) entry = _stationDataCache[st.FolderPath];
                if (entry.records == null) continue;

                List<ResponseRecord> trimmed = entry.records
                    .Where(r => r.TimestampParsed >= cutoff)
                    .ToList();

                _stationDataCache[st.FolderPath] = (trimmed, entry.name);
            }

            GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
            GC.WaitForPendingFinalizers();
        }

        private async Task SwitchToStation(StationInfo station)
        {
            _activeStation = station;
            if (!_isBackgroundLoading)
                _isOverlayMinimized = false;

            UpdateActiveStationButton();

            if (!_stationDataCache.TryGetValue(station.FolderPath,
                    out (List<ResponseRecord> records, string stationName) cached))
            {
                HideLoadingOverlay();
                return;
            }

            if (cached.records == null || cached.records.Count == 0)
                cached = await ReloadRecordsFromDisk(station, cached.stationName);

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

            SwitchToTabWithMostRecordsIfNeeded();
        }

        private void SwitchToTabWithMostRecordsIfNeeded()
        {
            if (!(MainTabControl.SelectedItem is TabItem currentTab)) return;
            if (currentTab.Tag == null) return;

            var currentType = TryParseMessageType(currentTab.Tag.ToString());

            bool currentHasData = currentType.HasValue &&
                                  _filteredRecords.Any(r => r.Type == currentType.Value);

            if (currentHasData) return;

            TabItem bestTab = null;
            int bestCount = 0;

            foreach (TabItem tab in MainTabControl.Items)
            {
                if (tab.Tag == null) continue;
                var type = TryParseMessageType(tab.Tag.ToString());
                if (!type.HasValue) continue;
                int count = _filteredRecords.Count(r => r.Type == type.Value);
                if (count > bestCount)
                {
                    bestCount = count;
                    bestTab = tab;
                }
            }

            if (bestTab != null && bestCount > 0)
                MainTabControl.SelectedItem = bestTab;
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
            var startup = new StartupWindow();
            if (startup.ShowDialog() != true || string.IsNullOrEmpty(startup.SelectedPath)) return;
            if (startup.SelectedPaths != null && startup.SelectedPaths.Count > 1)
                await LoadAllStationsFromPaths(startup.SelectedPaths);
            else
                await LoadAllStationsFromRoot(startup.SelectedPath);
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
            DateTime startBase = DatePickerFrom.SelectedDate ?? DateTime.MinValue;
            DateTime endBase = DatePickerTo.SelectedDate ?? DateTime.MaxValue;
            DateTime start = startBase == DateTime.MinValue ? DateTime.MinValue : startBase.Add(ts1);
            DateTime end = endBase == DateTime.MaxValue ? DateTime.MaxValue : endBase.Add(ts2);

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
            bool hasPrebuilt = _chartCache.Count > 0;

            ShowLoadingOverlay(station,
                hasPrebuilt ? "Using pre-built charts from cache..." : "Clearing chart cache...",
                13,
                detail: hasPrebuilt
                    ? _chartCache.Count + " charts loaded from station cache"
                    : "Invalidating " + _chartCache.Count + " cached charts from previous state");

            await Task.Yield();

            if (hasPrebuilt)
            {
                ShowLoadingOverlay(station, "Using pre-built charts from cache...", 80,
                    detail: _chartCache.Count + " charts ready");
                await Task.Yield();
                return;
            }

            _chartCache.Clear();

            ShowLoadingOverlay(station, "Preparing data for all message types...", 15,
                detail: string.Join("  ·  ", messageTypes.Select(t => t.ToString().Replace("_", " "))));

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

            foreach (var messageType in messageTypes)
            {
                if (!preparedInputs.TryGetValue(messageType, out var input)) continue;
                if (input.Records.Count == 0) continue;

                string typeName = messageType.ToString().Replace("_", " ");

                foreach (var chartType in new[] { ChartType.Trend, ChartType.Histogram, ChartType.Timeline })
                {
                    int pct = 20 + (doneCount * 60 / totalSteps);
                    ShowLoadingOverlay(station,
                        "Building  " + typeName + "  —  " + chartType,
                        pct,
                        detail: "Chart " + (doneCount + 1) + " / " + totalSteps
                                + "   ·   " + input.Records.Count.ToString("N0") + " records"
                                + "   ·   " + typeName);

                    var data = _chartFactory.BuildSingle(chartType, input);
                    if (data != null)
                        _chartCache[(messageType, chartType)] = data;

                    doneCount++;
                }

                await Task.Yield();
            }

            ShowLoadingOverlay(station,
                "Chart cache built  —  " + doneCount + " charts ready",
                80,
                detail: "Cached:  " + _chartCache.Count + " charts  ·  " + nonEmpty + " message types");
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

            Dispatcher.BeginInvoke(new Action(UpdateTabEmptyState),
                System.Windows.Threading.DispatcherPriority.Background);
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

            UpdateTabEmptyState();
        }

        private void UpdateTabEmptyState()
        {
            var tabs = MainTabControl.Items.OfType<TabItem>()
                .Where(t => t.Tag != null && TryParseMessageType(t.Tag.ToString()) != null)
                .ToList();

            var withData = tabs.Where(t => HasRecordsForTab(t)).ToList();
            var withoutData = tabs.Where(t => !HasRecordsForTab(t)).ToList();

            foreach (var tab in withData)
            {
                tab.Opacity = 1.0;
                tab.Foreground = new SolidColorBrush(Color.FromRgb(201, 209, 217));
                tab.FontSize = tab.FontSize > 0 ? tab.FontSize : 11;
                if (MainTabControl.Items.IndexOf(tab) > withData.Count - 1)
                    MoveTabTo(tab, withData.Count - 1);
            }

            foreach (var tab in withoutData)
            {
                tab.Opacity = 0.38;
                tab.Foreground = new SolidColorBrush(Color.FromRgb(110, 118, 129));
                tab.FontSize = 10;
            }

            // Move empty tabs to end
            int targetIndex = withData.Count;
            foreach (var tab in withoutData)
            {
                int current = MainTabControl.Items.IndexOf(tab);
                if (current < targetIndex)
                    MoveTabTo(tab, Math.Min(targetIndex, MainTabControl.Items.Count - 1));
                targetIndex++;
            }
        }

        private bool HasRecordsForTab(TabItem tab)
        {
            var type = TryParseMessageType(tab.Tag?.ToString() ?? "");
            if (type == null) return false;
            return _allRecords.Any(r => r.Type == type.Value);
        }

        private void MoveTabTo(TabItem tab, int index)
        {
            int current = MainTabControl.Items.IndexOf(tab);
            if (current < 0 || current == index) return;
            MainTabControl.Items.RemoveAt(current);
            MainTabControl.Items.Insert(Math.Min(index, MainTabControl.Items.Count), tab);
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

        private void RebuildStationBarThrottled()
        {
            long nowMs = System.DateTime.UtcNow.Ticks / System.TimeSpan.TicksPerMillisecond;
            if (nowMs - _lastStationBarRebuildMs < 300) return;
            _lastStationBarRebuildMs = nowMs;
            RebuildStationBar();
        }

        private void RebuildStationBar()
        {
            if (StationBarPanel == null) return;

            StationBarPanel.Children.Clear();

            // ── Dropdown (fixed, outside scroll) ────────────────────────────
            if (StationDropdownHost != null)
            {
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
                    Margin = new Thickness(0, 0, 2, 0)
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

                StationDropdownHost.Content = dropdown;
            }

            // ── Scroll left (fixed) ──────────────────────────────────────────
            if (BtnScrollLeftHost != null)
            {
                BtnScrollLeftHost.Content = BuildScrollButton("◀", () =>
                {
                    double viewW = StationBarScrollViewer?.ActualWidth ?? 400;
                    double target = Math.Max(0, (StationBarScrollViewer?.HorizontalOffset ?? 0) - viewW * 0.75);
                    SmoothScrollTo(target);
                });
            }

            // ── Scroll right (fixed) ─────────────────────────────────────────
            if (BtnScrollRightHost != null)
            {
                BtnScrollRightHost.Content = BuildScrollButton("▶", () =>
                {
                    double viewW = StationBarScrollViewer?.ActualWidth ?? 400;
                    double target = (StationBarScrollViewer?.HorizontalOffset ?? 0) + viewW * 0.75;
                    SmoothScrollTo(target);
                });
            }

            // ── Chevrons (scrollable) ────────────────────────────────────────
            List<StationInfo> withRecords = _loadedStations.Where(s =>
                _stationDataCache.ContainsKey(s.FolderPath) &&
                _stationDataCache[s.FolderPath].records != null &&
                _stationDataCache[s.FolderPath].records.Count > 0).ToList();

            List<StationInfo> withoutRecords = _loadedStations.Where(s =>
                !_stationDataCache.ContainsKey(s.FolderPath) ||
                _stationDataCache[s.FolderPath].records == null ||
                _stationDataCache[s.FolderPath].records.Count == 0).ToList();

            List<StationInfo> ordered = withRecords.Concat(withoutRecords).ToList();

            for (int i = 0; i < ordered.Count; i++)
            {
                Canvas chevron = BuildChevron(ordered[i], i == 0);
                if (withoutRecords.Contains(ordered[i]))
                    chevron.Opacity = 0.35;
                StationBarPanel.Children.Add(chevron);
            }

            // ── Lazy load chevrons ───────────────────────────────────────────
            foreach (StationInfo lazySt in _lazyLoadStations)
            {
                bool isLoadingNow = _stationLoadingState.ContainsKey(lazySt.FolderPath)
                                    && _stationLoadingState[lazySt.FolderPath];
                Canvas lazyChevron = BuildLazyStationChevron(lazySt, isLoadingNow);
                StationBarPanel.Children.Add(lazyChevron);
            }
        }

        private Canvas BuildLazyStationChevron(StationInfo station, bool isLoadingNow)
        {
            const double h = 44;
            const double tip = 12;
            bool isReady = _stationReadyGlow.Contains(station.FolderPath);
            double canvasW = MeasureLazyChevronWidth(station.StationName, tip);

            Canvas canvas = BuildLazyChevronCanvas(canvasW, h, station.FolderPath);
            System.Windows.Shapes.Polygon poly = BuildLazyChevronPolygon(canvasW, h, tip, isLoadingNow, isReady);
            canvas.Children.Add(poly);

            TextBlock nameBlock = BuildLazyChevronNameBlock(station.StationName, isLoadingNow, isReady);
            Canvas.SetLeft(nameBlock, 22);
            Canvas.SetTop(nameBlock, (h - 14) / 2.0);
            canvas.Children.Add(nameBlock);

            if (isLoadingNow)
                StartShimmerAnimation(canvas, nameBlock, canvasW, h, station.FolderPath);
            else if (isReady)
                StartGlowAnimation(poly, nameBlock, station.FolderPath);

            WireLazyChevronEvents(canvas, poly, nameBlock, station, isLoadingNow, isReady);
            return canvas;
        }

        private double MeasureLazyChevronWidth(string stationName, double tip)
        {
            TextBlock measure = new TextBlock { Text = stationName + "  ⧖⧖", FontSize = 11 };
            measure.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            return measure.DesiredSize.Width + 22 + tip + 14;
        }

        private Canvas BuildLazyChevronCanvas(double canvasW, double h, string folderPath)
        {
            return new Canvas
            {
                Width = canvasW,
                Height = h,
                Cursor = System.Windows.Input.Cursors.Hand,
                Margin = new Thickness(-1, 0, 0, 0),
                Tag = folderPath
            };
        }

        private System.Windows.Shapes.Polygon BuildLazyChevronPolygon(
            double canvasW, double h, double tip, bool isLoadingNow, bool isReady)
        {
            Color fill = isLoadingNow ? Color.FromRgb(16, 70, 36)
                : isReady ? Color.FromRgb(18, 90, 42)
                : Color.FromRgb(12, 52, 26);
            Color stroke = isLoadingNow ? Color.FromRgb(50, 170, 90)
                : isReady ? Color.FromRgb(80, 220, 120)
                : Color.FromRgb(30, 95, 52);

            System.Windows.Shapes.Polygon poly = new System.Windows.Shapes.Polygon
            {
                Fill = new SolidColorBrush(fill),
                Stroke = new SolidColorBrush(stroke),
                StrokeThickness = isReady ? 1.5 : 1.0
            };
            poly.Points.Add(new Point(0, 0));
            poly.Points.Add(new Point(canvasW - tip, 0));
            poly.Points.Add(new Point(canvasW, h / 2));
            poly.Points.Add(new Point(canvasW - tip, h));
            poly.Points.Add(new Point(0, h));
            poly.Points.Add(new Point(tip, h / 2));
            return poly;
        }

        private TextBlock BuildLazyChevronNameBlock(string stationName, bool isLoadingNow, bool isReady)
        {
            Color color = isLoadingNow ? Color.FromRgb(110, 200, 140)
                : isReady ? Color.FromRgb(160, 255, 190)
                : Color.FromRgb(80, 145, 105);
            string icon = isLoadingNow ? "  ⧖" : isReady ? "  ✓" : "  ⧖";

            return new TextBlock
            {
                Text = stationName + icon,
                FontSize = 11,
                Foreground = new SolidColorBrush(color)
            };
        }

        private void StartShimmerAnimation(
            Canvas canvas, TextBlock nameBlock, double canvasW, double h, string folderPath)
        {
            System.Windows.Shapes.Rectangle shimmer = BuildShimmerRect(canvasW, h);
            canvas.Children.Add(shimmer);

            string[] frames = { "⧖", "⧗" };
            int frame = 0;
            double shimmerX = -canvasW * 0.35;
            double speed = canvasW * 1.6 / 30.0;
            int tick = 0;
            string stationName = nameBlock.Text.Split(' ')[0];

            System.Windows.Threading.DispatcherTimer timer =
                new System.Windows.Threading.DispatcherTimer
                    { Interval = TimeSpan.FromMilliseconds(30) };

            timer.Tick += (s, e) =>
            {
                if (!_stationLoadingState.ContainsKey(folderPath) ||
                    !_stationLoadingState[folderPath])
                {
                    timer.Stop();
                    return;
                }

                shimmerX += speed;
                if (shimmerX > canvasW) shimmerX = -canvasW * 0.35;
                Canvas.SetLeft(shimmer, shimmerX);
                shimmer.Opacity = 1.0;
                tick++;
                if (tick % 10 == 0)
                {
                    frame = (frame + 1) % frames.Length;
                    nameBlock.Text = stationName + "  " + frames[frame];
                }
            };
            timer.Start();
            canvas.Unloaded += (s, e) => timer.Stop();
        }

        private System.Windows.Shapes.Rectangle BuildShimmerRect(double canvasW, double h)
        {
            System.Windows.Media.LinearGradientBrush brush =
                new System.Windows.Media.LinearGradientBrush
                {
                    StartPoint = new System.Windows.Point(0, 0.5),
                    EndPoint = new System.Windows.Point(1, 0.5)
                };
            brush.GradientStops.Add(new System.Windows.Media.GradientStop(
                Color.FromArgb(0, 80, 220, 120), 0.0));
            brush.GradientStops.Add(new System.Windows.Media.GradientStop(
                Color.FromArgb(120, 140, 255, 170), 0.5));
            brush.GradientStops.Add(new System.Windows.Media.GradientStop(
                Color.FromArgb(0, 80, 220, 120), 1.0));

            System.Windows.Shapes.Rectangle rect = new System.Windows.Shapes.Rectangle
            {
                Width = canvasW * 0.35,
                Height = h,
                Fill = brush,
                Opacity = 0,
                IsHitTestVisible = false
            };
            Canvas.SetTop(rect, 0);
            Canvas.SetLeft(rect, -canvasW * 0.35);
            return rect;
        }

        private void StartGlowAnimation(
            System.Windows.Shapes.Polygon poly, TextBlock nameBlock, string folderPath)
        {
            double phase = 0;

            System.Windows.Threading.DispatcherTimer timer =
                new System.Windows.Threading.DispatcherTimer
                    { Interval = TimeSpan.FromMilliseconds(60) };

            timer.Tick += (s, e) =>
            {
                if (!_stationReadyGlow.Contains(folderPath))
                {
                    timer.Stop();
                    return;
                }

                phase += 0.08;
                double sin = (Math.Sin(phase) + 1.0) / 2.0;
                byte r = (byte)(40 + sin * 60);
                byte g = (byte)(160 + sin * 80);
                byte b = (byte)(60 + sin * 40);
                poly.Stroke = new SolidColorBrush(Color.FromRgb(r, g, b));
                poly.StrokeThickness = 1.5 + sin * 1.5;
                byte nr = (byte)(130 + sin * 80);
                byte ng = (byte)(220 + sin * 35);
                byte nb = (byte)(150 + sin * 60);
                nameBlock.Foreground = new SolidColorBrush(Color.FromRgb(nr, ng, nb));
            };
            timer.Start();
        }

        private void WireLazyChevronEvents(Canvas canvas, System.Windows.Shapes.Polygon poly, TextBlock nameBlock, StationInfo station, bool isLoadingNow, bool isReady)
        {
            Color fill = isReady ? Color.FromRgb(18, 90, 42) : Color.FromRgb(12, 52, 26);
            Color hoverFill = isReady ? Color.FromRgb(28, 120, 60) : Color.FromRgb(20, 80, 42);

            canvas.MouseEnter += (s, e) => poly.Fill = new SolidColorBrush(hoverFill);
            canvas.MouseLeave += (s, e) => poly.Fill = new SolidColorBrush(fill);
            poly.MouseEnter += (s, e) => poly.Fill = new SolidColorBrush(hoverFill);
            poly.MouseLeave += (s, e) => poly.Fill = new SolidColorBrush(fill);

            StationInfo captured = station;
            canvas.MouseLeftButtonUp += async (s, e) =>
            {
                if (isLoadingNow) return;

                if (_stationReadyGlow.Contains(captured.FolderPath))
                {
                    _stationReadyGlow.Remove(captured.FolderPath);
                    RebuildStationBar();
                    await SwitchToStation(captured);
                }
                else
                {
                    await LoadLazyStationOnDemand(captured);
                }
            };
        }

        private Button BuildScrollButton(string label, Action onClick)
        {
            Button btn = new Button
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
            // Use cached name (set after loading) if available, fallback to StationInfo name
            string stationDisplayName = _stationDataCache.ContainsKey(station.FolderPath)
                ? _stationDataCache[station.FolderPath].stationName
                : station.StationName;
            bool isActive = _activeStation?.FolderPath == station.FolderPath;

            const double h = 44;
            const double tip = 12;

            // Active = orange (7-day avg line colour), inactive = light green
            var fillColor = isActive ? Color.FromRgb(140, 80, 10) : Color.FromRgb(22, 110, 55);
            var hoverColor = isActive ? Color.FromRgb(170, 100, 15) : Color.FromRgb(30, 140, 70);
            var strokeColor = isActive ? Color.FromRgb(220, 140, 40) : Color.FromRgb(56, 190, 100);
            var nameColor = isActive ? Color.FromRgb(255, 220, 160) : Color.FromRgb(210, 245, 220);
            var subColor = isActive ? Color.FromRgb(220, 170, 100) : Color.FromRgb(130, 210, 155);

            // Use LineName + ComputerName, fallback to ComputerName alone
            string subText = string.Join("  ·  ", new[] { station.LineName, station.ComputerName }
                .Where(x => !string.IsNullOrEmpty(x)));

            bool hasSub = !string.IsNullOrEmpty(subText);

            // Measure text to size canvas
            var measureBlock = new TextBlock
            {
                Text = stationDisplayName, FontSize = 11,
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
                Margin = new Thickness(isFirst ? 0 : -1, 0, 0, 0),
                Tag = station.FolderPath
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

            bool isCurrentlyLoading = _stationLoadingState.ContainsKey(station.FolderPath) &&
                                      _stationLoadingState[station.FolderPath];

            var nameBlock = new TextBlock
            {
                Text = stationDisplayName,
                FontSize = 11,
                FontWeight = isActive ? FontWeights.SemiBold : FontWeights.Normal,
                Foreground = new SolidColorBrush(isCurrentlyLoading
                    ? Color.FromRgb(180, 140, 60)
                    : nameColor)
            };
            Canvas.SetLeft(nameBlock, leftPad);
            Canvas.SetTop(nameBlock, topPad);
            canvas.Children.Add(nameBlock);

            if (isCurrentlyLoading)
            {
                var loadingTimer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(150)
                };
                string[] frames = { "  ↻", "  ↺" };
                int frame = 0;
                loadingTimer.Tick += (s, e) =>
                {
                    frame = (frame + 1) % frames.Length;
                    nameBlock.Text = stationDisplayName + frames[frame];
                    if (!_stationLoadingState.ContainsKey(station.FolderPath) ||
                        !_stationLoadingState[station.FolderPath])
                    {
                        nameBlock.Text = stationDisplayName;
                        nameBlock.Foreground = new SolidColorBrush(nameColor);
                        loadingTimer.Stop();
                    }
                };
                loadingTimer.Start();
            }

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

            async void ClickHandler(object s, System.Windows.Input.MouseButtonEventArgs e)
            {
                if (_activeStation?.FolderPath == captured.FolderPath) return;
                await SwitchToStation(captured);
            }

            canvas.MouseLeftButtonUp += ClickHandler;
            poly.MouseLeftButtonUp += ClickHandler;
            nameBlock.MouseLeftButtonUp += ClickHandler;

            return canvas;
        }

        private void UpdateActiveStationButton()
        {
            if (StationBarPanel == null) return;

            RebuildStationBar();
            ScrollToActiveStation();
        }

        private void ScrollToActiveStation()
        {
            if (_activeStation == null || StationBarPanel == null || StationBarScrollViewer == null) return;

            Dispatcher.BeginInvoke(new Action(() =>
            {
                double offset = 0;
                foreach (UIElement child in StationBarPanel.Children)
                {
                    if (child is Canvas chevron)
                    {
                        var tag = chevron.Tag as string;
                        if (tag == _activeStation.FolderPath)
                        {
                            double center = offset + chevron.Width / 2;
                            double target = center - StationBarScrollViewer.ActualWidth / 2;
                            SmoothScrollTo(Math.Max(0, target));
                            return;
                        }

                        offset += chevron.Width + chevron.Margin.Left + chevron.Margin.Right;
                    }
                    else if (child is FrameworkElement fe)
                    {
                        offset += fe.ActualWidth + fe.Margin.Left + fe.Margin.Right;
                    }
                }
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private void SmoothScrollTo(double target)
        {
            if (StationBarScrollViewer == null) return;

            double start = StationBarScrollViewer.HorizontalOffset;
            double distance = target - start;
            if (Math.Abs(distance) < 1) return;

            int steps = 12;
            int step = 0;
            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(16)
            };
            timer.Tick += (s, e) =>
            {
                step++;
                double t = (double)step / steps;
                double eased = t < 0.5 ? 2 * t * t : -1 + (4 - 2 * t) * t;
                double current = start + distance * eased;
                StationBarScrollViewer.ScrollToHorizontalOffset(current);
                if (step >= steps)
                {
                    StationBarScrollViewer.ScrollToHorizontalOffset(target);
                    timer.Stop();
                }
            };
            timer.Start();
        }

        #endregion

        #region Loading Overlay

        private long _lastOverlayUpdateMs = 0;
        private long _lastStationBarRebuildMs = 0;
        private long _lastBeginInvokeMs = 0;
        private long _overlayShowStartMs = 0;
        private System.Windows.Threading.DispatcherTimer _skipButtonTimer;
        private List<StationInfo> _pendingOptionalStations = new List<StationInfo>();
        private bool _isOverlayMinimized = false;
        private bool _isBackgroundLoading = false;

        private readonly System.Collections.ObjectModel.ObservableCollection<LoadingStationLogEntry> _stationLogEntries
            = new System.Collections.ObjectModel.ObservableCollection<LoadingStationLogEntry>();

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

            if (!_isOverlayMinimized)
            {
                if (LoadingOverlay.Visibility != Visibility.Visible)
                {
                    _overlayShowStartMs = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond;
                    StartSkipButtonTimer();
                }

                LoadingOverlay.Visibility = Visibility.Visible;
            }
        }

        private void StartSkipButtonTimer()
        {
            _skipButtonTimer?.Stop();
            _skipButtonTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(5)
            };
            _skipButtonTimer.Tick += (s, e) =>
            {
                _skipButtonTimer.Stop();
                // WIP: BtnSkipStation not active yet
            };
            _skipButtonTimer.Start();
        }

        private void HideLoadingOverlay()
        {
            if (LoadingOverlay == null) return;
            LoadingOverlay.Visibility = Visibility.Collapsed;
            _skipButtonTimer?.Stop();
            // WIP: BtnSkipStation not active yet
        }

        private void BtnCloseLoadingOverlay_Click(object sender, RoutedEventArgs e)
        {
            HideLoadingOverlay();
        }

        #endregion
    }

    internal class TrimRecordsDialog : Window
    {
        public int SelectedMonths { get; private set; } = 3;

        public TrimRecordsDialog()
        {
            Width = 400;
            Height = 240;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = new SolidColorBrush(Color.FromArgb(245, 8, 18, 12));
            WindowStartupLocation = WindowStartupLocation.CenterScreen;

            Border outer = new Border
            {
                BorderBrush = new SolidColorBrush(Color.FromRgb(30, 100, 55)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10)
            };

            StackPanel root = new StackPanel { Margin = new Thickness(28, 24, 28, 24) };

            root.Children.Add(new TextBlock
            {
                Text = "Trim old records",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(140, 210, 160)),
                Margin = new Thickness(0, 0, 0, 8)
            });

            root.Children.Add(new TextBlock
            {
                Text = "Keep records from the last how many months?",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(160, 190, 170)),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 12)
            });

            int[] options = { 1, 2, 3, 6, 12 };
            ComboBox combo = new ComboBox
            {
                Margin = new Thickness(0, 0, 0, 16),
                FontSize = 12,
                Background = new SolidColorBrush(Color.FromRgb(12, 30, 18)),
                Foreground = new SolidColorBrush(Color.FromRgb(180, 230, 195)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(30, 80, 44)),
                BorderThickness = new Thickness(1)
            };
            foreach (int m in options)
                combo.Items.Add(m + (m == 1 ? " month" : " months"));
            combo.SelectedIndex = 2;

            root.Children.Add(combo);

            StackPanel btnRow = new StackPanel { Orientation = Orientation.Horizontal };

            Button btnOk = new Button
            {
                Content = "Trim  →",
                FontSize = 12,
                Padding = new Thickness(20, 8, 20, 8),
                Margin = new Thickness(0, 0, 10, 0),
                Background = new SolidColorBrush(Color.FromRgb(22, 100, 50)),
                Foreground = new SolidColorBrush(Color.FromRgb(180, 245, 205)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(50, 180, 90)),
                BorderThickness = new Thickness(1),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            btnOk.Click += (s, e) =>
            {
                SelectedMonths = options[combo.SelectedIndex];
                DialogResult = true;
            };

            Button btnCancel = new Button
            {
                Content = "Cancel",
                FontSize = 12,
                Padding = new Thickness(16, 8, 16, 8),
                Background = new SolidColorBrush(Color.FromRgb(14, 30, 18)),
                Foreground = new SolidColorBrush(Color.FromRgb(100, 130, 108)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(30, 60, 38)),
                BorderThickness = new Thickness(1),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            btnCancel.Click += (s, e) => { DialogResult = false; };

            btnRow.Children.Add(btnOk);
            btnRow.Children.Add(btnCancel);
            root.Children.Add(btnRow);

            outer.Child = root;
            Content = outer;
        }
    }
}