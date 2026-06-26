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
using MESInsight.Core;
using ScottPlot.WPF;

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
        private ScottPlotTrendChartRenderer _scottPlotRenderer;

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

        private Dictionary<MessageType, WpfPlot> _scottPlotByMessageType =
            new Dictionary<MessageType, WpfPlot>();

        private Dictionary<MessageType, (Border container, StackPanel panel)> _timelineContainerByMessageType =
            new Dictionary<MessageType, (Border, StackPanel)>();

        private HashSet<MessageType> _tabsUserHasAlreadySeen = new HashSet<MessageType>();
        private List<StationInfo> _lazyLoadStations = new List<StationInfo>();
        private HashSet<string> _stationReadyGlow = new HashSet<string>();
        private bool _bgLoadingRunning = false;
        private System.Threading.CancellationTokenSource _bgCts = null;
        private Canvas _toastCanvas;

        private Dictionary<MessageType, CartesianChart> _trendChartByMessageType =
            new Dictionary<MessageType, CartesianChart>();

        private Dictionary<MessageType, LiveCharts.Wpf.AxisSection> _selectedDayHighlightByMessageType =
            new Dictionary<MessageType, LiveCharts.Wpf.AxisSection>();

        private Dictionary<MessageType, UIElement> _renderedChartCache = new Dictionary<MessageType, UIElement>();

        private readonly UidIndex _uidIndex = new UidIndex();
        private readonly MESInsight.Assembly.AssemblyIndex _assemblyIndex = new MESInsight.Assembly.AssemblyIndex();
        private bool _assemblyPanelBuilt = false;
        private bool _assemblyIndexReady = false;
        private bool _isCyclingTabs = false;

        public static Action<string> OpenSubsetHistory;

        public event PropertyChangedEventHandler PropertyChanged;

        #endregion

        #region Initialization

        public MainWindow()
        {
            InitializeComponent();
            ValidateLoadingControls();
            DataContext = this;
            OpenSubsetHistory = uid => OpenSubsetHistoryTab(uid);
            WindowState = WindowState.Maximized;

            _chartFactory = new ChartFactory(
                _dayRecordsPanelBuilder,
                _dayRecordsPanelByMessageType,
                _trendChartByMessageType,
                _selectedDayHighlightByMessageType,
                _scottPlotByMessageType,
                _timelineContainerByMessageType,
                _recordsGroupedByDay,
                _filteredRecords,
                OnShowAllRecordsRequested);
            _scottPlotRenderer = _chartFactory.GetRenderer(ChartType.Trend) as ScottPlotTrendChartRenderer;

            Loaded += (s, e) =>
            {
                InitToastCanvas();
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

        private async Task LoadAllStationsFromPaths(List<string> paths)
        {
            List<StationInfo> allStations = await ScanStationsFromPaths(paths);
            ClassifyStations(allStations);

            HideLoadingOverlay();

            (List<StationInfo> ghp, List<StationInfo> lcs, List<StationInfo> backflush, List<StationInfo> connectors)
                = SplitByCategory(allStations);

            string commonRoot = paths.Count == 1 ? paths[0] : System.IO.Path.GetDirectoryName(paths[0]) ?? paths[0];

            LoadOptionsDialog optDlg =
                await ShowLoadOptionsDialog(ghp, lcs, backflush, connectors, allStations, commonRoot: commonRoot);

            if (optDlg == null) return;

            ApplyDateFilter(optDlg);

            List<StationInfo> stations = FilterSelectedStations(allStations, optDlg);
            _lazyLoadStations = allStations.Where(s => optDlg.LazyLoadFolderPaths.Contains(s.FolderPath)).ToList();

            await RunLoadingLoop(stations, ghp, lcs, backflush, rootPath: commonRoot);
        }

        private async Task LoadAllStationsFromRoot(string rootPath)
        {
            ShowLoadingOverlay("Scanning...", "Looking for stations...", 0, detail: rootPath);
            await Task.Yield();

            List<StationInfo> allStations = await Task.Run(() => DataLoader.FindStations(rootPath));

            if (allStations.Count == 0)
                allStations.Add(new StationInfo
                    { FolderPath = rootPath, StationName = System.IO.Path.GetFileName(rootPath) });

            ClassifyStations(allStations);
            HideLoadingOverlay();

            (List<StationInfo> ghp, List<StationInfo> lcs, List<StationInfo> backflush, List<StationInfo> connectors)
                = SplitByCategory(allStations);

            LoadOptionsDialog optDlg = await ShowLoadOptionsDialog(
                ghp, lcs, backflush, connectors, allStations, commonRoot: rootPath);

            if (optDlg == null) return;

            ApplyDateFilter(optDlg);

            List<StationInfo> stations = FilterSelectedStations(allStations, optDlg);
            _lazyLoadStations = allStations.Where(s => optDlg.LazyLoadFolderPaths.Contains(s.FolderPath)).ToList();

            await RunLoadingLoop(stations, ghp, lcs, backflush, rootPath: rootPath);
        }

        private void InitToastCanvas()
        {
            _toastCanvas = new Canvas { IsHitTestVisible = false };
            Panel.SetZIndex(_toastCanvas, 2000);

            var rootGrid = this.Content as Grid;
            if (rootGrid == null) return;

            Grid.SetRowSpan(_toastCanvas, 3);
            rootGrid.Children.Add(_toastCanvas);
        }

        private async Task<List<StationInfo>> ScanStationsFromPaths(List<string> paths)
        {
            ShowLoadingOverlay("Scanning...", "Building station list from selection...", 0);
            await Task.Yield();

            return await Task.Run(() =>
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
        }

        private void ClassifyStations(List<StationInfo> stations)
        {
            System.Text.RegularExpressions.RegexOptions opts =
                System.Text.RegularExpressions.RegexOptions.IgnoreCase;

            foreach (StationInfo st in stations)
            {
                if (st.Category != StationCategory.GHP) continue;
                string n = st.StationName + " " + st.FolderPath;

                if (System.Text.RegularExpressions.Regex.IsMatch(n, @"(?:^|[ _-])LCS[0-9]+", opts))
                    st.Category = StationCategory.LCS;
                else if (System.Text.RegularExpressions.Regex.IsMatch(n, @"backflush", opts))
                    st.Category = StationCategory.Backflush;
                else if (System.Text.RegularExpressions.Regex.IsMatch(n, @"connector|comcell|ghpnetty", opts))
                    st.Category = StationCategory.Connector;
            }
        }

        private (List<StationInfo> ghp, List<StationInfo> lcs, List<StationInfo> backflush, List<StationInfo> connectors
            )
            SplitByCategory(List<StationInfo> all)
        {
            return (
                all.Where(s => s.Category == StationCategory.GHP || s.Category == StationCategory.Unknown).ToList(),
                all.Where(s => s.Category == StationCategory.LCS).ToList(),
                all.Where(s => s.Category == StationCategory.Backflush).ToList(),
                all.Where(s => s.Category == StationCategory.Connector).ToList()
            );
        }

        private async Task<LoadOptionsDialog> ShowLoadOptionsDialog(
            List<StationInfo> ghp, List<StationInfo> lcs,
            List<StationInfo> backflush, List<StationInfo> connectors,
            List<StationInfo> allStations, string commonRoot)
        {
            Window spinner = ShowScanningSpinner("Scanning stations...");

            Dictionary<int, MonthFileInfo> fileCounts = await Task.Run(() =>
                DataLoader.CountFilesByMonthCutoffs(commonRoot, LoadOptionsDialog.GetDayOption()));

            Dictionary<string, Dictionary<int, MonthFileInfo>> perStationCounts = await Task.Run(() =>
                DataLoader.CountFilesByStationAndDays(allStations, LoadOptionsDialog.GetDayOption()));

            spinner?.Close();

            LoadOptionsDialog dlg = new LoadOptionsDialog(ghp, lcs, backflush, connectors, fileCounts, perStationCounts)
                { Owner = this };

            if (dlg.ShowDialog() != true) return null;
            return dlg;
        }

        private void ApplyDateFilter(LoadOptionsDialog optDlg)
        {
            _dataLoader.DateFilter = optDlg.FilterByDate
                ? DateTime.Now.AddDays(-optDlg.MaxDays)
                : (DateTime?)null;
        }

        private List<StationInfo> FilterSelectedStations(List<StationInfo> all, LoadOptionsDialog optDlg)
        {
            return all.Where(s =>
                    ((s.Category == StationCategory.GHP || s.Category == StationCategory.Unknown) ||
                     (optDlg.IncludeLcs && s.Category == StationCategory.LCS) ||
                     (optDlg.IncludeBackflush && s.Category == StationCategory.Backflush) ||
                     (optDlg.IncludeConnectors && s.Category == StationCategory.Connector)) &&
                    !optDlg.ExcludedFolderPaths.Contains(s.FolderPath) &&
                    !optDlg.LazyLoadFolderPaths.Contains(s.FolderPath))
                .ToList();
        }

        private async Task RunLoadingLoop(
            List<StationInfo> stations, List<StationInfo> ghp, List<StationInfo> lcs, List<StationInfo> backflush,
            string rootPath = null)
        {
            _pendingOptionalStations = new List<StationInfo>();
            _isBackgroundLoading = true;
            _isOverlayMinimized = false;

            ShowLoadingOverlay("Loading...", "Preparing...", 0);
            await Task.Yield();

            _loadedStations = stations;
            _stationDataCache.Clear();
            _stationChartCache.Clear();
            _stationLogEntries.Clear();

            BuildStationLogEntries(stations, ghp, lcs, backflush);

            ShowLoadingOverlay(
                "Found " + stations.Count + " station" + (stations.Count != 1 ? "s" : ""),
                "Preparing to load...", 5, typeCount: stations.Count);

            await Task.Delay(150);

            int totalFiles = 0;

            for (int i = 0; i < stations.Count; i++)
                totalFiles = await LoadSingleStationInLoop(stations, i, totalFiles);

            int totalRecords = stations.Sum(s =>
                _stationDataCache.ContainsKey(s.FolderPath) && _stationDataCache[s.FolderPath].records != null
                    ? _stationDataCache[s.FolderPath].records.Count
                    : 0);

            ShowLoadingOverlay("All stations ready",
                stations.Count + " stations  ·  " + totalRecords.ToString("N0") + " records total",
                100, fileCount: totalFiles, recordCount: totalRecords, typeCount: stations.Count);

            await Task.Delay(150);

            var firstWithRecords = _loadedStations.FirstOrDefault(s =>
                _stationDataCache.ContainsKey(s.FolderPath) &&
                _stationDataCache[s.FolderPath].records != null &&
                _stationDataCache[s.FolderPath].records.Count > 0);

            if (firstWithRecords != null)
                await SwitchToStation(firstWithRecords);
            else if (_lazyLoadStations.Count > 0)
                StartBackgroundLoading();

            if (_pendingOptionalStations.Count > 0)
                RebuildStationBarWithOptionalButton();

            _isBackgroundLoading = false;
            HideLoadingOverlay();
            RebuildStationBar();

            if (!string.IsNullOrEmpty(rootPath))
                StartupWindow.SaveRecentPath(rootPath, _loadedStations.Concat(_lazyLoadStations).ToList());

            if (_lazyLoadStations.Count > 0)
                StartBackgroundLoading();

            StartupWindow.SaveRecentPath(rootPath, _loadedStations.Concat(_lazyLoadStations).ToList());
        }

        private void BuildStationLogEntries(
            List<StationInfo> stations,
            List<StationInfo> ghp, List<StationInfo> lcs, List<StationInfo> backflush)
        {
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

            AddSection("GHP STATIONS", ghp.Where(s => stations.Contains(s)).ToList());
            AddSection("LCS STATIONS", lcs.Where(s => stations.Contains(s)).ToList());
            AddSection("BACKFLUSH STATIONS", backflush.Where(s => stations.Contains(s)).ToList());
        }

        private async Task<int> LoadSingleStationInLoop(List<StationInfo> stations, int i, int totalFiles)
        {
            StationInfo st = stations[i];

            if (!await CheckRamAndProceed(stations, i))
                return totalFiles;

            UpdateStationBarLoadingState(st.FolderPath, isLoading: true);

            DataLoadResult loadResult = await LoadStationFiles(stations, i);

            string displayName = ResolveDisplayName(loadResult.StationName, st.StationName);
            st.StationName = displayName;

            totalFiles += StoreLoadResult(st, i, loadResult, displayName, totalFiles);

            await BuildAndCacheCharts(st, loadResult, displayName, stations, i, totalFiles);

            HandleEmptyStation(st, loadResult);

            UpdateStationBarLoadingState(st.FolderPath, isLoading: false);
            RebuildStationBarThrottled();

            await SwitchToFirstStationOrRebuild(stations, i, loadResult);

            return totalFiles;
        }

        private async Task<bool> CheckRamAndProceed(List<StationInfo> stations, int i)
        {
            if (i == 0 || CheckRamBeforeLoading(100000))
                return true;

            bool proceed = ShowRamWarningDialog();
            if (!proceed)
            {
                for (int j = i; j < stations.Count; j++)
                    _lazyLoadStations.Add(stations[j]);
            }

            return proceed;
        }

        private async Task<DataLoadResult> LoadStationFiles(List<StationInfo> stations, int i)
        {
            StationInfo st = stations[i];
            int liveFileCount = 0;

            return await Task.Run(() => _dataLoader.Load(st.FolderPath, (status, percent, extra) =>
            {
                if (status.StartsWith("Reading "))
                    System.Threading.Interlocked.Increment(ref liveFileCount);

                int fc = liveFileCount;
                int innerPct = 5 + (i * 88 / stations.Count) + (percent * 88 / 100 / stations.Count);
                long nowMs = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond;

                if (nowMs - _lastBeginInvokeMs < 150) return;
                _lastBeginInvokeMs = nowMs;

                Dispatcher.BeginInvoke(new Action(() =>
                {
                    string detail = BuildLoadingDetail(status, fc, extra);
                    ShowLoadingOverlay(
                        "Station " + (i + 1) + " / " + stations.Count,
                        st.StationName, innerPct,
                        detail: detail, fileCount: fc, typeCount: stations.Count);
                }));
            }));
        }

        private string BuildLoadingDetail(string status, int fileCount, string extra)
        {
            if (status.StartsWith("Reading "))
            {
                string fileName = status.Substring(8).TrimEnd('.', ' ');
                string detail = "File " + fileCount + (fileName.Length > 0 ? "  —  " + fileName : "");
                if (!string.IsNullOrEmpty(extra)) detail += Environment.NewLine + extra;
                return detail;
            }

            if (status.StartsWith("Scanning"))
                return "Scanning for log files...";

            return status;
        }

        private string ResolveDisplayName(string fromResult, string fallback)
        {
            return !string.IsNullOrEmpty(fromResult) ? fromResult : fallback;
        }

        private int StoreLoadResult(StationInfo st, int logIndex, DataLoadResult result, string displayName,
            int totalFiles)
        {
            _stationDataCache[st.FolderPath] = (result.Records, displayName);

            if (logIndex < _stationLogEntries.Count)
                UpdateStationLogEntry(logIndex, displayName, result.Records.Count);

            return totalFiles;
        }

        private void UpdateStationLogEntry(int index, string displayName, int recordCount)
        {
            bool hasRecords = recordCount > 0;
            _stationLogEntries[index].StatusIcon = hasRecords ? "✓" : "✕";
            _stationLogEntries[index].IconColor = hasRecords ? "#3FB950" : "#C03030";
            _stationLogEntries[index].Line1Color = hasRecords ? "#C9D1D9" : "#C03030";
            _stationLogEntries[index].Line1 = displayName + (hasRecords
                ? "  —  " + recordCount.ToString("N0") + " records"
                : "  —  no records");
        }

        private async Task BuildAndCacheCharts(
            StationInfo st, DataLoadResult result, string displayName,
            List<StationInfo> stations, int i, int totalFiles)
        {
            ShowLoadingOverlay(
                "Station " + (i + 1) + " / " + stations.Count + "  —  building charts",
                st.StationName,
                5 + ((i * 88 + 44) / stations.Count),
                detail: "Building charts for " + result.Records.Count.ToString("N0") + " records...",
                fileCount: totalFiles, recordCount: result.Records.Count, typeCount: stations.Count);

            await Task.Yield();

            var charts = await BuildChartsForRecords(result.Records, displayName);
            _stationChartCache[st.FolderPath] = charts;
        }

        private void HandleEmptyStation(StationInfo st, DataLoadResult result)
        {
            if (result.Records.Count > 0) return;
            _loadedStations.Remove(st);
        }

        private async Task SwitchToFirstStationOrRebuild(List<StationInfo> stations, int i, DataLoadResult result)
        {
            if (i != 0)
            {
                RebuildStationBar();
                return;
            }

            bool overlayWasVisible = LoadingOverlay.Visibility == Visibility.Visible;
            if (stations.Count > 0)
                await SwitchToStation(stations[0]);

            if (overlayWasVisible)
                ShowLoadingOverlay(
                    "Loading " + (i + 2) + " / " + stations.Count + "...", "",
                    5 + ((i + 1) * 88 / stations.Count),
                    typeCount: stations.Count);
        }

        private void StartBackgroundLoading()
        {
            if (_bgLoadingRunning) return;
            _bgLoadingRunning = true;

            _bgCts?.Dispose();
            _bgCts = new System.Threading.CancellationTokenSource();

            System.Threading.CancellationToken token = _bgCts.Token;

            List<StationInfo> ordered = _lazyLoadStations
                .OrderByDescending(s =>
                {
                    if (s.Category == StationCategory.GHP) return 1;
                    return 0;
                })
                .ToList();

            Task.Run(async () =>
            {
                foreach (StationInfo lazySt in ordered)
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
                    _bgCts?.Dispose();
                    _bgCts = null;
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
            _stationReadyGlow.Add(station.FolderPath);

            RebuildStationBar();
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
                    new Dictionary<MessageType, WpfPlot>(),
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

                await Task.Delay(1);

                var scottData = _chartFactory.BuildSingleScottPlot(input);
                if (scottData != null)
                {
                    if (result.ContainsKey((messageType, ChartType.Trend)))
                        result[(messageType, ChartType.Trend)].ScottPlotTrend = scottData.ScottPlotTrend;
                    else
                        result[(messageType, ChartType.Trend)] = scottData;
                }
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

            _stationReadyGlow.Remove(station.FolderPath);
            StationChevron.StopGlow(station.FolderPath);

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

            await Task.Run(() => _uidIndex.Build(_allRecords));

            string displayName = !string.IsNullOrEmpty(station.LineName)
                ? station.LineName + "  ·  " + cached.stationName
                : cached.stationName;

            TxtStationName.Text = displayName;

            _tabsUserHasAlreadySeen.Clear();
            _assemblyPanelBuilt = false;
            _assemblyIndexReady = false;
            _dayRecordsPanelByMessageType.Clear();
            _scottPlotByMessageType.Clear();
            _timelineContainerByMessageType.Clear();
            _recordsGroupedByDay.Clear();

            _chartFactory = new ChartFactory(
                _dayRecordsPanelBuilder,
                _dayRecordsPanelByMessageType,
                _trendChartByMessageType,
                _selectedDayHighlightByMessageType,
                _scottPlotByMessageType,
                _timelineContainerByMessageType,
                _recordsGroupedByDay,
                _filteredRecords,
                OnShowAllRecordsRequested);

            _scottPlotRenderer = _chartFactory.GetRenderer(ChartType.Trend) as ScottPlotTrendChartRenderer;

            _chartCache.Clear();

            if (_stationChartCache.TryGetValue(station.FolderPath, out var prebuiltCharts))
                foreach (var kv in prebuiltCharts)
                    _chartCache[kv.Key] = kv.Value;

            SetDatePickersToFullDataRange();
            await RefreshChartsAndStatsWithLoadingOverlay();

            var snapForIndex = _allRecords.ToList();
            Task.Run(() => _assemblyIndex.Build(snapForIndex))
                .ContinueWith(_ => Dispatcher.Invoke(() => _assemblyIndexReady = true));

            SwitchToTabWithMostRecordsIfNeeded();
        }

        private async Task PreRenderAllTabs()
        {
            foreach (var mt in GetAllSupportedMessageTypes())
            {
                if (!_renderedChartCache.ContainsKey(mt))
                {
                    PreRenderChartForMessageType(mt);
                    await Task.Delay(10);
                }
            }
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

        private void BtnSubsetSearch_Click(object sender, RoutedEventArgs e)
        {
            string uid = TxtSubsetUid.Text.Trim();
            if (string.IsNullOrEmpty(uid)) return;
            if (!_uidIndex.HasUid(uid))
            {
                MessageBox.Show("UID not found in current station records.",
                    "Not Found", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            OpenSubsetHistoryTab(uid);
        }

        private void TxtSubsetUid_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
                BtnSubsetSearch_Click(sender, null);
        }

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

            if (!(MainTabControl.SelectedItem is TabItem tab)) return;

            if (tab.Tag?.ToString() == "ASSEMBLY")
            {
                if (!_assemblyPanelBuilt) BuildAssemblyPanel();
                return;
            }

            if (tab.Tag?.ToString().StartsWith("SUBSET_") == true) return;

            var type = TryParseMessageType(tab.Tag?.ToString() ?? "");
            if (!type.HasValue) return;

            var panel = GetChartPanelForMessageType(type.Value);
            if (panel != null && panel.Children.Count == 0)
                RenderCachedChartForMessageType(type.Value);

            if (!_isCyclingTabs)
                _scottPlotRenderer?.InitializeTimelineWithFirstAvailableDay(type.Value);
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

            // await CycleThroughAllTabsToTriggerWpfLayoutRendering();


            ShowLoadingOverlay(station, "Updating records table and statistics...", 95);

            await Task.Yield();

            GridRecords.ItemsSource = _filteredRecords;
            UpdateSidebarStats();
            UpdateTabHighlightsForActiveFilter();

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
                    if (!string.IsNullOrEmpty(filterUid))
                    {
                        bool match =
                            (r.Uid != null && r.Uid.IndexOf(filterUid, StringComparison.OrdinalIgnoreCase) >= 0) ||
                            (r.UidIn != null && r.UidIn.IndexOf(filterUid, StringComparison.OrdinalIgnoreCase) >= 0) ||
                            (r.UidOut != null &&
                             r.UidOut.IndexOf(filterUid, StringComparison.OrdinalIgnoreCase) >= 0) ||
                            (r.UidAssy != null &&
                             r.UidAssy.IndexOf(filterUid, StringComparison.OrdinalIgnoreCase) >= 0);
                        if (!match) continue;
                    }

                    if (!string.IsNullOrEmpty(filterUidIn) &&
                        (r.UidIn == null || r.UidIn.IndexOf(filterUidIn, StringComparison.OrdinalIgnoreCase) < 0))
                        continue;
                    if (!string.IsNullOrEmpty(filterUidOut) &&
                        (r.UidOut == null || r.UidOut.IndexOf(filterUidOut, StringComparison.OrdinalIgnoreCase) < 0))
                        continue;
                    if (!string.IsNullOrEmpty(filterMaterial) &&
                        (r.Material == null ||
                         r.Material.IndexOf(filterMaterial, StringComparison.OrdinalIgnoreCase) < 0)) continue;
                    if (!string.IsNullOrEmpty(filterCarrierId) &&
                        (r.CarrierId == null ||
                         r.CarrierId.IndexOf(filterCarrierId, StringComparison.OrdinalIgnoreCase) < 0)) continue;
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

            _statsCalculator.InvalidateCache();
        }

        private async Task BuildAllChartDataFromFilteredRecords()
        {
            var messageTypes = GetAllSupportedMessageTypes();
            int totalSteps = messageTypes.Length * 3;
            int doneCount = 0;

            string station = TxtStationName.Text;
            bool hasPrebuilt = _chartCache.Count > 0;

            if (hasPrebuilt && _filteredRecords.Count == 0)
            {
                _chartCache.Clear();
                hasPrebuilt = false;
            }

            ShowLoadingOverlay(station,
                hasPrebuilt ? "Using pre-built charts from cache..." : "Clearing chart cache...",
                13,
                detail: hasPrebuilt
                    ? _chartCache.Count + " charts loaded from station cache"
                    : "Invalidating " + _chartCache.Count + " cached charts from previous state");

            await Task.Yield();

            if (!hasPrebuilt)
            {
                _chartCache.Clear();

                ShowLoadingOverlay(station, "Preparing data for all message types...", 15,
                    detail: string.Join("  ·  ", messageTypes.Select(t => t.ToString().Replace("_", " "))));

                var preparedInputs = await Task.Run(() =>
                    _chartFactory.PrepareAllInputs(_filteredRecords, messageTypes));

                int nonEmpty = preparedInputs.Count(kv => kv.Value.Records.Count > 0);

                var typeLines = preparedInputs
                    .OrderByDescending(kv => kv.Value.Records.Count)
                    .Select(kv =>
                        kv.Key.ToString().Replace("_", " ") + ":  " + kv.Value.Records.Count.ToString("N0") +
                        " records");

                string typeDetail = string.Join(Environment.NewLine, typeLines);

                ShowLoadingOverlay(station,
                    nonEmpty + " message types ready  —  " + _filteredRecords.Count.ToString("N0") + " records total",
                    20, detail: typeDetail);

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

            if (_filteredRecords.Count >= 2)
            {
                var allRecs = _filteredRecords.ToList();
                var sorted = allRecs.Select(r => r.ResponseTime).OrderBy(x => x).ToList();
                double avg = sorted.Average();
                var allInput = new MESInsight.Charts.ChartInputData
                {
                    Records = allRecs, MessageType = MessageType.ALL, Average = avg,
                    StdDev = Math.Sqrt(allRecs.Average(r => Math.Pow(r.ResponseTime - avg, 2))),
                    P95 = sorted[(int)(sorted.Count * 0.95)],
                    P99 = sorted[(int)(sorted.Count * 0.99)],
                    GroupedByDay = _recordsGroupedByDay
                };
                var allScott = await Task.Run(() => _chartFactory.BuildSingleScottPlot(allInput));
                if (allScott?.ScottPlotTrend != null)
                    _chartCache[(MessageType.ALL, ChartType.Trend)] = allScott;
                var allTimeline = _chartFactory.BuildSingle(ChartType.Timeline, allInput);
                if (allTimeline != null)
                    _chartCache[(MessageType.ALL, ChartType.Timeline)] = allTimeline;
            }
        }

        private async Task RenderAllCachedChartsToUI()
        {
            string station = TxtStationName.Text;
            var types = GetAllSupportedMessageTypes();

            _renderedChartCache.Clear();

            foreach (var mt in types)
            {
                var panel = GetChartPanelForMessageType(mt);
                panel?.Children.Clear();
            }

            ShowLoadingOverlay(station, "Rendering charts to UI...", 82,
                detail: "Writing " + _chartCache.Count + " charts into " + types.Length + " tabs");

            var activeType = GetActiveMessageType();
            if (activeType.HasValue)
                RenderCachedChartForMessageType(activeType.Value);

            Dispatcher.BeginInvoke(new Action(UpdateTabEmptyState),
                System.Windows.Threading.DispatcherPriority.Background);

            await Task.Delay(1);
        }

        private void PreRenderChartForMessageType(MessageType messageType)
        {
            try
            {
                double availableHeight = ActualHeight - 160;
                var context = new RenderContext
                    { AvailableHeightPixels = (int)availableHeight, MessageType = messageType };

                _chartCache.TryGetValue((messageType, ChartType.Trend), out ChartData trendData);
                _chartCache.TryGetValue((messageType, ChartType.Histogram), out ChartData histogramData);

                if (trendData?.ScottPlotTrend == null &&
                    (histogramData?.Charts == null || histogramData.Charts.Count == 0))
                    return;

                var wrapper = new StackPanel();

                if (trendData?.ScottPlotTrend != null)
                    wrapper.Children.Add(_chartFactory.RenderScottPlot(trendData, context));

                if (histogramData?.Charts != null && histogramData.Charts.Count > 0)
                    wrapper.Children.Add(_chartFactory.Render(ChartType.Histogram, histogramData, context));

                _renderedChartCache[messageType] = wrapper;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("PreRender error for " + messageType + ": " + ex.Message);
            }
        }

        private MessageType? GetActiveMessageType()
        {
            if (!(MainTabControl.SelectedItem is TabItem tab)) return null;
            return TryParseMessageType(tab.Tag?.ToString() ?? "");
        }

        private void BuildAssemblyPanel()
        {
            if (PanelAssembly == null) return;
            PanelAssembly.Children.Clear();

            if (_assemblyIndexReady)
            {
                RenderAssemblyTree();
                return;
            }

            PanelAssembly.Children.Add(new TextBlock
            {
                Text = "Building assembly index...",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(155, 89, 182)),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 40, 0, 0)
            });

            var snap = _allRecords.ToList();
            Task.Run(() => _assemblyIndex.Build(snap))
                .ContinueWith(_ => Dispatcher.Invoke(() =>
                {
                    _assemblyIndexReady = true;
                    if (_assemblyPanelBuilt) return;
                    PanelAssembly.Children.Clear();
                    RenderAssemblyTree();
                }));
        }

        private void RenderAssemblyTree()
        {
            if (_assemblyIndex.IsBuilt)
                PanelAssembly.Children.Add(
                    MESInsight.UI.AssemblyTreePanel.Build(
                        _assemblyIndex,
                        _allRecords,
                        uid => OpenSubsetHistoryTab(uid)));
            else
                PanelAssembly.Children.Add(new TextBlock
                {
                    Text = "No SEMI VALIDATION records found.",
                    FontSize = 11,
                    Foreground = new SolidColorBrush(Color.FromRgb(100, 110, 120)),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 40, 0, 0)
                });

            _assemblyPanelBuilt = true;
        }

        public void OpenSubsetHistoryTab(string uid)
        {
            if (string.IsNullOrEmpty(uid)) return;

            string tag = "SUBSET_" + uid;
            foreach (TabItem existing in MainTabControl.Items)
            {
                if (existing.Tag?.ToString() == tag)
                {
                    MainTabControl.SelectedItem = existing;
                    return;
                }
            }

            string shortUid = uid.Length > 16
                ? uid.Substring(0, 8) + "…" + uid.Substring(uid.Length - 6)
                : uid;

            var equipNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var st in _loadedStations.Concat(_lazyLoadStations))
            {
                if (string.IsNullOrEmpty(st.FolderPath)) continue;
                string folderName = System.IO.Path.GetFileName(st.FolderPath);
                // Kľúč 1: priamy názov priečinka (napr. MON0240_St1160...)
                if (!equipNames.ContainsKey(folderName))
                    equipNames[folderName] = st.StationName;
                // Kľúč 2: OR_MON0240 → hľadaj MON0240 v priečinku
                string monPart = System.Text.RegularExpressions.Regex.Match(folderName, @"MON\d+",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase).Value;
                if (!string.IsNullOrEmpty(monPart))
                {
                    if (!equipNames.ContainsKey("OR_" + monPart))
                        equipNames["OR_" + monPart] = st.StationName;
                    if (!equipNames.ContainsKey(monPart))
                        equipNames[monPart] = st.StationName;
                }

                // Kľúč 3: LCS, BFL varianty
                string lcsPart = System.Text.RegularExpressions.Regex.Match(folderName, @"LCS\d+",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase).Value;
                if (!string.IsNullOrEmpty(lcsPart) && !equipNames.ContainsKey("OR_" + lcsPart))
                    equipNames["OR_" + lcsPart] = st.StationName;
            }

            var loadingPanel = MESInsight.UI.SubsetHistoryTab.BuildLoading(uid);

            var closeBtn = new TextBlock
            {
                Text = "×", FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 110, 130)),
                Cursor = System.Windows.Input.Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 0, 0)
            };

            var header = new StackPanel
                { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            header.Children.Add(new TextBlock
            {
                Text = "🔍", FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(56, 182, 255)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 4, 0)
            });
            var headerStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            headerStack.Children.Add(new TextBlock
            {
                Text = "SUBSET HISTORY", FontSize = 7, FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 130, 170))
            });
            headerStack.Children.Add(new TextBlock
            {
                Text = shortUid, FontSize = 9, FontFamily = new FontFamily("Consolas"),
                Foreground = new SolidColorBrush(Color.FromRgb(56, 182, 255))
            });
            header.Children.Add(headerStack);
            header.Children.Add(closeBtn);

            var tab = new TabItem
            {
                Header = header, Tag = tag, Content = loadingPanel,
                Background = new SolidColorBrush(Colors.Transparent),
                BorderThickness = new Thickness(0), Padding = new Thickness(8, 4, 4, 4),
                Foreground = new SolidColorBrush(Color.FromRgb(201, 209, 217)),
                Cursor = System.Windows.Input.Cursors.Hand
            };

            closeBtn.MouseLeftButtonUp += (s, e) =>
            {
                MainTabControl.Items.Remove(tab);
                e.Handled = true;
            };
            closeBtn.MouseEnter += (s, e) => closeBtn.Foreground = new SolidColorBrush(Color.FromRgb(248, 81, 73));
            closeBtn.MouseLeave += (s, e) => closeBtn.Foreground = new SolidColorBrush(Color.FromRgb(100, 110, 130));

            MainTabControl.Items.Insert(0, tab);
            MainTabControl.SelectedItem = tab;

            string activePath = _activeStation?.FolderPath ?? "";
            string lineRoot = System.IO.Path.GetDirectoryName(activePath) ?? activePath;

            Task.Run(() =>
            {
                var allStations = DataLoader.FindStations(lineRoot);
                if (allStations.Count == 0)
                    allStations.Add(new StationInfo { FolderPath = lineRoot });

                var allRecords = new List<ResponseRecord>();

                foreach (var st in allStations)
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                        MESInsight.UI.SubsetHistoryTab.UpdateLoadingStation(loadingPanel, st.StationName)));

                    List<ResponseRecord> recs;

                    if (_stationDataCache.TryGetValue(st.FolderPath, out var cached) && cached.records != null)
                    {
                        // Použi cache pre rýchle výsledky
                        recs = cached.records.Where(r =>
                                r.Uid == uid || r.UidIn == uid || r.UidOut == uid ||
                                r.UidAssy == uid ||
                                (!string.IsNullOrEmpty(r.AssyUids) && r.AssyUids.Split(',')
                                    .Select(x => x.Trim())
                                    .Contains(uid, StringComparer.OrdinalIgnoreCase)))
                            .ToList();

                        // Ak cache má date filter, doplň aj scan z disku pre staršie záznamy
                        if (_dataLoader.DateFilter.HasValue)
                        {
                            var diskRecs = DataLoader.ScanForUid(st.FolderPath, uid);
                            var cacheKeys =
                                new HashSet<string>(recs.Select(r => r.TimestampParsed.Ticks + r.Type.ToString()));
                            foreach (var dr in diskRecs)
                                if (!cacheKeys.Contains(dr.TimestampParsed.Ticks + dr.Type.ToString()))
                                    recs.Add(dr);
                        }
                    }
                    else
                        recs = DataLoader.ScanForUid(st.FolderPath, uid);

                    allRecords.AddRange(recs);
                }

                var records = allRecords
                    .GroupBy(r => r.TimestampParsed.Ticks.ToString() + r.Type.ToString() + (r.EquipId ?? ""))
                    .Select(g => g.First())
                    .OrderBy(r => r.TimestampParsed)
                    .ToList();

                Dispatcher.Invoke(() =>
                {
                    tab.Content = MESInsight.UI.SubsetHistoryTab.Build(
                        uid, records,
                        onOpenUid: u => OpenSubsetHistoryTab(u),
                        onSwitchStation: null,
                        onScanFullLine: null,
                        equipNames: equipNames);
                });
            });
        }

        private List<ResponseRecord> ScanFullLineHistory(string uid, Action<string> progress = null)
        {
            var allRecords = new List<ResponseRecord>();
            string activePath = _activeStation?.FolderPath ?? "";
            string lineRoot = System.IO.Path.GetDirectoryName(activePath) ?? activePath;

            var allStations = DataLoader.FindStations(lineRoot);
            if (allStations.Count == 0)
                allStations.Add(new StationInfo { FolderPath = lineRoot });

            foreach (var st in allStations)
            {
                progress?.Invoke(st.StationName);

                List<ResponseRecord> recs = null;

                if (_stationDataCache.TryGetValue(st.FolderPath, out var cached))
                    recs = cached.records;

                if (recs == null)
                {
                    var loader = new DataLoader { DateFilter = null };
                    recs = loader.Load(st.FolderPath, null).Records;
                }

                if (recs == null) continue;

                foreach (var r in recs)
                {
                    if (r.Uid == uid || r.UidIn == uid || r.UidOut == uid ||
                        r.UidAssy == uid ||
                        (!string.IsNullOrEmpty(r.AssyUids) && r.AssyUids
                            .Split(',').Select(x => x.Trim())
                            .Contains(uid, StringComparer.OrdinalIgnoreCase)))
                        allRecords.Add(r);
                }
            }

            return allRecords.OrderBy(r => r.TimestampParsed).ToList();
        }

        private MessageType[] GetAllSupportedMessageTypes() => new[]
        {
            MessageType.UNIT_INFO, MessageType.NEXT_OPERATION, MessageType.UNIT_CHECKIN,
            MessageType.UNIT_RESULT, MessageType.LOAD_MATERIAL,
            MessageType.REQ_MATERIAL_INFO, MessageType.REQ_SETUP_CHANGE2, MessageType.SEMI_VALIDATION2
        };

        #endregion

        #region Chart Rendering

        private void RenderCachedChartForMessageType(MessageType messageType)
        {
            try
            {
                var targetPanel = GetChartPanelForMessageType(messageType);
                if (targetPanel == null) return;

                if (_renderedChartCache.ContainsKey(messageType))
                {
                    if (targetPanel.Children.Count == 0)
                        targetPanel.Children.Add(_renderedChartCache[messageType]);
                    _scottPlotRenderer?.InitializeTimelineWithFirstAvailableDay(messageType);
                    return;
                }

                targetPanel.Children.Clear();

                double availableHeight = ActualHeight - 160;
                var context = new RenderContext
                    { AvailableHeightPixels = (int)availableHeight, MessageType = messageType };

                _chartCache.TryGetValue((messageType, ChartType.Trend), out ChartData trendData);
                _chartCache.TryGetValue((messageType, ChartType.Histogram), out ChartData histogramData);

                var wrapper = new StackPanel();

                if (trendData?.ScottPlotTrend != null)
                    wrapper.Children.Add(_chartFactory.RenderScottPlot(trendData, context));

                if (histogramData?.Charts != null && histogramData.Charts.Count > 0)
                    wrapper.Children.Add(_chartFactory.Render(ChartType.Histogram, histogramData, context));

                _renderedChartCache[messageType] = wrapper;
                targetPanel.Children.Add(wrapper);

                var scrollViewer = FindParentScrollViewer(targetPanel);
                if (scrollViewer != null)
                {
                    scrollViewer.PreviewMouseWheel += (s, e) =>
                    {
                        if (IsMouseOverWpfPlot(targetPanel))
                            e.Handled = true;
                    };
                }

                _scottPlotRenderer?.InitializeTimelineWithFirstAvailableDay(messageType);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error displaying chart for " + messageType + ":\n\n" + ex.Message,
                    "Chart Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private static ScrollViewer FindParentScrollViewer(DependencyObject child)
        {
            var parent = System.Windows.Media.VisualTreeHelper.GetParent(child);
            if (parent == null) return null;
            if (parent is ScrollViewer sv) return sv;
            return FindParentScrollViewer(parent);
        }

        private bool IsMouseOverWpfPlot(StackPanel panel)
        {
            foreach (UIElement child in panel.Children)
            {
                if (child is StackPanel sp)
                    foreach (UIElement c in sp.Children)
                        if (c is WpfPlot w && w.IsMouseOver)
                            return true;
                if (child is WpfPlot wp && wp.IsMouseOver) return true;
            }

            return false;
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
                case MessageType.SEMI_VALIDATION2: return PanelSemiValidation2;
                case MessageType.ALL: return PanelAll;
                default: return null;
            }
        }

        #endregion

        #region Sidebar Stats

        private void UpdateSidebarStats()
        {
            if (!(MainTabControl.SelectedItem is TabItem selected) || selected.Tag == null) return;

            MessageType? parsedType = TryParseMessageType(selected.Tag.ToString());
            if (!parsedType.HasValue)
            {
                ClearSidebarStats();
                if (PassFailSection != null) PassFailSection.Visibility = Visibility.Collapsed;
                return;
            }

            MessageType type = parsedType.Value;
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

            UpdatePassFailChart(type);
        }

        private void UpdatePassFailChart(MessageType type)
        {
            if (PassFailSection == null) return;

            string GetResultField(ResponseRecord r)
            {
                if (type == MessageType.SEMI_VALIDATION2 || type == MessageType.SEMI_VALIDATION)
                    return r.ProcDirAssy;
                return r.Result;
            }

            var records = _filteredRecords.Where(r => r.Type == type).ToList();
            var withResult = records.Where(r => !string.IsNullOrEmpty(GetResultField(r))).ToList();

            if (withResult.Count == 0)
            {
                PassFailSection.Visibility = Visibility.Collapsed;
                return;
            }

            var passValues = new[] { "Y", "P", "G", "R" };
            int pass = withResult.Count(r => passValues.Contains(GetResultField(r)?.ToUpper()));
            int fail = withResult.Count - pass;

            double passRate = pass * 100.0 / withResult.Count;
            double failRate = fail * 100.0 / withResult.Count;

            PassFailPieChart.Series = new LiveCharts.SeriesCollection
            {
                new LiveCharts.Wpf.PieSeries
                {
                    Title = "Pass",
                    Values = new LiveCharts.ChartValues<double> { pass },
                    Fill = new SolidColorBrush(Color.FromRgb(46, 160, 67)),
                    DataLabels = false,
                    LabelPoint = p => pass + " (" + passRate.ToString("F1") + "%)"
                },
                new LiveCharts.Wpf.PieSeries
                {
                    Title = "Fail",
                    Values = new LiveCharts.ChartValues<double> { fail },
                    Fill = new SolidColorBrush(Color.FromRgb(248, 81, 73)),
                    DataLabels = false,
                    LabelPoint = p => fail + " (" + failRate.ToString("F1") + "%)"
                }
            };

            TxtPassRate.Text = "Pass: " + pass.ToString("N0") + "  (" + passRate.ToString("F1") + "%)";
            TxtFailRate.Text = "Fail: " + fail.ToString("N0") + "  (" + failRate.ToString("F1") + "%)";
            PassFailSection.Visibility = Visibility.Visible;
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
                if (tab.Tag.ToString() == "ALL" || tab.Tag.ToString() == "ASSEMBLY") continue;
                if (tab.Tag.ToString().StartsWith("SUBSET_")) continue;
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
                .Where(t => t.Tag != null && TryParseMessageType(t.Tag.ToString()) != null
                                          && t.Tag.ToString() != "ALL" && t.Tag.ToString() != "ASSEMBLY")
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
            if (tab.Tag?.ToString() == "ALL" || tab.Tag?.ToString() == "ASSEMBLY") return;
            if (tab.Tag?.ToString().StartsWith("SUBSET_") == true) return;
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
            RebuildStationDropdown();
            RebuildScrollButtons();
            RebuildStationChevrons();
            RebuildLazyChevrons();
            RestartGlowOnAllReadyChevrons();
        }

        private void RestartGlowOnAllReadyChevrons()
        {
            foreach (string folderPath in _stationReadyGlow.ToList())
                StartGlowOnReadyChevron(folderPath);
        }

        private void RebuildStationDropdown()
        {
            if (StationDropdownHost == null) return;

            var contextMenu = new ContextMenu { Background = new SolidColorBrush(Color.FromRgb(13, 30, 18)) };

            AddDropdownSection(contextMenu, "LOADED", _loadedStations.Where(s =>
                    _stationDataCache.ContainsKey(s.FolderPath) &&
                    _stationDataCache[s.FolderPath].records != null &&
                    _stationDataCache[s.FolderPath].records.Count > 0).ToList(),
                Color.FromRgb(46, 160, 67), Color.FromRgb(160, 240, 180));

            AddDropdownSection(contextMenu, "LAZY LOAD", _lazyLoadStations,
                Color.FromRgb(30, 95, 52), Color.FromRgb(100, 180, 120));

            AddDropdownSection(contextMenu, "NO RECORDS", _loadedStations.Where(s =>
                    !_stationDataCache.ContainsKey(s.FolderPath) ||
                    _stationDataCache[s.FolderPath].records == null ||
                    _stationDataCache[s.FolderPath].records.Count == 0).ToList(),
                Color.FromRgb(100, 30, 30), Color.FromRgb(200, 100, 100));

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

            dropdown.Click += (s, e) =>
            {
                contextMenu.PlacementTarget = dropdown;
                contextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
                contextMenu.IsOpen = true;
            };
            StationDropdownHost.Content = dropdown;
        }

        private void AddDropdownSection(ContextMenu menu, string header, List<StationInfo> stations, Color headerColor,
            Color itemColor)
        {
            if (stations.Count == 0) return;

            menu.Items.Add(new MenuItem
            {
                Header = "── " + header + " ──",
                FontSize = 9,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(headerColor),
                Background = new SolidColorBrush(Color.FromRgb(8, 20, 12)),
                IsEnabled = false
            });

            foreach (var st in stations)
            {
                var captured = st;
                var item = new MenuItem
                {
                    Header = st.StationName + (!string.IsNullOrEmpty(st.LineName) ? "  ·  " + st.LineName : ""),
                    FontSize = 11,
                    Foreground = new SolidColorBrush(itemColor),
                    Background = new SolidColorBrush(Color.FromRgb(13, 30, 18))
                };
                item.Click += async (s, e) => await EnqueueLazyLoad(captured);
                menu.Items.Add(item);
            }
        }

        private void RebuildScrollButtons()
        {
            if (BtnScrollLeftHost != null)
                BtnScrollLeftHost.Content = BuildScrollButton("◀", () =>
                {
                    double viewW = StationBarScrollViewer?.ActualWidth ?? 400;
                    double target = Math.Max(0, (StationBarScrollViewer?.HorizontalOffset ?? 0) - viewW * 0.75);
                    SmoothScrollTo(target);
                });

            if (BtnScrollRightHost != null)
                BtnScrollRightHost.Content = BuildScrollButton("▶", () =>
                {
                    double viewW = StationBarScrollViewer?.ActualWidth ?? 400;
                    double target = (StationBarScrollViewer?.HorizontalOffset ?? 0) + viewW * 0.75;
                    SmoothScrollTo(target);
                });
        }

        private void RebuildStationChevrons()
        {
            var withRecords = _loadedStations.Where(s =>
                _stationDataCache.ContainsKey(s.FolderPath) &&
                _stationDataCache[s.FolderPath].records != null &&
                _stationDataCache[s.FolderPath].records.Count > 0).ToList();

            var withoutRecords = _loadedStations.Where(s =>
                !_stationDataCache.ContainsKey(s.FolderPath) ||
                _stationDataCache[s.FolderPath].records == null ||
                _stationDataCache[s.FolderPath].records.Count == 0).ToList();

            for (int i = 0; i < withRecords.Count; i++)
            {
                Canvas chevron = BuildChevron(withRecords[i], i == 0, isEmpty: false);
                StationBarPanel.Children.Add(chevron);
            }

            foreach (var st in withoutRecords)
            {
                Canvas chevron = BuildChevron(st, isFirst: false, isEmpty: true);
                StationBarPanel.Children.Add(chevron);
            }
        }

        private void RebuildLazyChevrons()
        {
            foreach (var lazySt in _lazyLoadStations)
            {
                bool isLoadingNow = _stationLoadingState.ContainsKey(lazySt.FolderPath)
                                    && _stationLoadingState[lazySt.FolderPath];
                Canvas lazyChevron = BuildLazyStationChevron(lazySt, isLoadingNow);
                StationBarPanel.Children.Add(lazyChevron);
            }
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

        private Canvas BuildChevron(StationInfo station, bool isFirst, bool isEmpty = false)
        {
            return StationChevron.Build(station, isFirst, isEmpty, new ChevronCallbacks
            {
                OnClick = st => OnChevronClick(st),
                OnClose = fp => OnChevronClose(fp),
                IsActive = fp => _activeStation?.FolderPath == fp,
                IsLoading = fp => _stationLoadingState.ContainsKey(fp) && _stationLoadingState[fp],
                GetCacheName = fp => _stationDataCache.ContainsKey(fp) ? _stationDataCache[fp].stationName : null
            });
        }

        private async Task OnChevronClick(StationInfo station)
        {
            bool hasRecords = _stationDataCache.ContainsKey(station.FolderPath)
                              && _stationDataCache[station.FolderPath].records != null
                              && _stationDataCache[station.FolderPath].records.Count > 0;

            if (!hasRecords)
            {
                await EnqueueLazyLoad(station);
                return;
            }

            await SwitchToStation(station);
        }

        private void OnChevronClose(string folderPath)
        {
            string name = ResolveStationName(folderPath);

            DropRecordsFromCache(folderPath);
            _stationChartCache.Remove(folderPath);
            _stationReadyGlow.Remove(folderPath);
            _statsCalculator.InvalidateCache();

            var station = _loadedStations.FirstOrDefault(s => s.FolderPath == folderPath);
            if (station != null)
            {
                _loadedStations.Remove(station);
                if (!_lazyLoadStations.Any(s => s.FolderPath == folderPath))
                    _lazyLoadStations.Add(station);
            }

            SwitchAwayFromClosedStation(folderPath);

            GC.Collect(2, GCCollectionMode.Forced, blocking: false, compacting: true);
            RebuildStationBar();
            ToastNotification.Show(_toastCanvas, ToastKind.StationUnloaded, name);
        }

        private string ResolveStationName(string folderPath)
        {
            if (_stationDataCache.ContainsKey(folderPath))
                return _stationDataCache[folderPath].stationName;

            return _loadedStations
                .FirstOrDefault(s => s.FolderPath == folderPath)?.StationName ?? folderPath;
        }

        private void SwitchAwayFromClosedStation(string folderPath)
        {
            if (_activeStation?.FolderPath != folderPath) return;

            var next = _loadedStations.FirstOrDefault(s =>
                s.FolderPath != folderPath &&
                _stationDataCache.ContainsKey(s.FolderPath) &&
                _stationDataCache[s.FolderPath].records != null);

            if (next != null)
                _ = SwitchToStation(next);
            else
            {
                _activeStation = null;
                TxtStationName.Text = "";
            }
        }

        private Canvas BuildLazyStationChevron(StationInfo station, bool isLoadingNow)
        {
            return StationChevron.BuildLazy(station, new LazyChevronCallbacks
            {
                OnClick = async st => await EnqueueLazyLoad(st),
                IsReady = fp => _stationReadyGlow.Contains(fp),
                IsLoading = fp => _stationLoadingState.ContainsKey(fp) && _stationLoadingState[fp]
            });
        }

        private readonly System.Collections.Generic.Queue<StationInfo> _lazyLoadQueue =
            new System.Collections.Generic.Queue<StationInfo>();

        private bool _lazyLoadQueueRunning = false;

        private async Task EnqueueLazyLoad(StationInfo station)
        {
            if (_stationReadyGlow.Contains(station.FolderPath))
            {
                _stationReadyGlow.Remove(station.FolderPath);
                StationChevron.StopGlow(station.FolderPath);
                RebuildStationBar();
                await SwitchToStation(station);
                return;
            }

            if (_stationLoadingState.ContainsKey(station.FolderPath)
                && _stationLoadingState[station.FolderPath])
                return;

            if (_lazyLoadQueue.Contains(station))
                return;

            _lazyLoadQueue.Enqueue(station);
            RebuildStationBar();

            if (!_lazyLoadQueueRunning)
                await DrainLazyLoadQueue();
        }

        private async Task DrainLazyLoadQueue()
        {
            _lazyLoadQueueRunning = true;

            while (_lazyLoadQueue.Count > 0)
            {
                StationInfo station = _lazyLoadQueue.Dequeue();

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

                if (result.Records.Count > 0)
                    _stationReadyGlow.Add(station.FolderPath);

                await Dispatcher.InvokeAsync(() =>
                {
                    RebuildStationBar();
                    ToastNotification.Show(_toastCanvas, ToastKind.StationLoaded, displayName);
                });
            }

            _lazyLoadQueueRunning = false;
        }

        private void StartGlowOnReadyChevron(string folderPath)
        {
            foreach (UIElement child in StationBarPanel.Children)
            {
                if (!(child is Canvas canvas) || canvas.Tag?.ToString() != folderPath) continue;
                StationChevron.StartGlowOnReadyChevron(canvas, folderPath, fp => _stationReadyGlow.Contains(fp));
                return;
            }
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

            LoadingTitle.Text = title;
            LoadingProgress.Value = progress;
            LoadingPercentage.Text = progress + "%";

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

        private async Task CycleThroughAllTabsToTriggerWpfLayoutRendering()
        {
            _isCyclingTabs = true;
            var originalTab = MainTabControl.SelectedItem;

            foreach (var messageType in GetAllSupportedMessageTypes())
            {
                foreach (TabItem tab in MainTabControl.Items)
                    if (tab.Tag?.ToString() == messageType.ToString())
                    {
                        MainTabControl.SelectedItem = tab;
                        break;
                    }

                await Task.Delay(20);
            }

            _tabsUserHasAlreadySeen.Clear();
            MainTabControl.SelectedItem = originalTab;
            _isCyclingTabs = false;

            var activeType = GetActiveMessageType();
            if (activeType.HasValue)
                _scottPlotRenderer?.InitializeTimelineWithFirstAvailableDay(activeType.Value);
        }

        private void StartSkipButtonTimer()
        {
            _skipButtonTimer?.Stop();
            _skipButtonTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(5)
            };
            _skipButtonTimer.Tick += (s, e) => { _skipButtonTimer.Stop(); };
            _skipButtonTimer.Start();
        }

        private void HideLoadingOverlay()
        {
            if (LoadingOverlay == null) return;
            LoadingOverlay.Visibility = Visibility.Collapsed;
            _skipButtonTimer?.Stop();
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
                WindowAnimations.FadeOutAndClose(this, true);
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