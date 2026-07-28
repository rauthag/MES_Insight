using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using MESInsight.Core;

namespace MESInsight
{
    public class LoadOptionsDialog : Window
    {
        public bool FilterByDate { get; private set; } = true;
        public int MaxDays { get; private set; } = 6;
        public bool IncludeLcs { get; private set; } = false;
        public bool IncludeBackflush { get; private set; } = false;
        public bool IncludeConnectors { get; private set; } = false;
        public List<string> ExcludedFolderPaths { get; private set; } = new List<string>();
        public List<string> LazyLoadFolderPaths { get; private set; } = new List<string>();
        public Dictionary<string, int> StationMonthOverrides { get; private set; } = new Dictionary<string, int>();
        public List<MessageType> EnabledMessageTypes { get; private set; } = new List<MessageType>();

        private static readonly int[] DayOptions =
        {
            7, 14, 21, 30, 45, 60, 90, 120, 150, 180, 270, 365, 548, 730
        };

        private static readonly HashSet<int> MilestoneTickIndices = new HashSet<int> { 0, 3, 6, 9, 11, 13 };

        private const long OptimalSizeMb = 800;
        private const long GoodSizeMb = 2000;
        private const long WarningSizeMb = 4000;
        private const long CriticalSizeMb = 7000;
        private const double RamEstimationFactor = 3.5;
        private const double RamReserveRatio = 0.15;
        private const double RamReserveRatioSoft = 0.08;
        private const double RamReserveRatioHard = 0.04;

        private static readonly MessageType[] AllMessageTypes =
        {
            MessageType.UNIT_CHECKIN, MessageType.UNIT_RESULT, MessageType.UNIT_INFO,
            MessageType.NEXT_OPERATION, MessageType.LOAD_MATERIAL, MessageType.REQ_LOADED_MATERIAL,
            MessageType.REQ_MATERIAL_INFO, MessageType.REQ_SETUP_CHANGE2
        };

        public static int[] GetDayOption() => DayOptions;

        private TextBlock _ramValueLabel;
        private TextBlock _autoAdjustNotice;
        private System.Windows.Threading.DispatcherTimer _ramTimer;
        private Border _ramEstimatedMarker;
        private Border _ramEstimatedFill;
        private TextBlock _ramEstimatedLabel;
        private Dictionary<MessageType, CheckBox> _messageTypeCheckboxes = new Dictionary<MessageType, CheckBox>();

        public LoadOptionsDialog(
            List<StationInfo> ghpStations, List<StationInfo> lcsStations,
            List<StationInfo> backflushStations, List<StationInfo> connectorStations,
            Dictionary<int, MonthFileInfo> globalFileCounts = null,
            Dictionary<string, Dictionary<int, MonthFileInfo>> perStationCounts = null)
        {
            SetupWindowChrome();

            int recommendedIndex = ResolveRecommendedIndex(globalFileCounts);
            int initDays = DayOptions[recommendedIndex];
            int recommendedMonths = DayOptions[recommendedIndex];
            int totalCount = ghpStations.Count + lcsStations.Count + backflushStations.Count + connectorStations.Count;

            List<StationLoadEntry> allEntries = new List<StationLoadEntry>();

            Slider globalSlider;
            TextBlock globalValueLabel, globalSizeLabel, globalWarningLabel, globalAutoAdjustNotice;

            StackPanel content = BuildDialogContent(
                ghpStations, lcsStations, backflushStations, connectorStations,
                globalFileCounts, perStationCounts,
                recommendedIndex, recommendedMonths, initDays, totalCount,
                allEntries,
                out globalSlider, out globalValueLabel, out globalSizeLabel,
                out globalWarningLabel, out globalAutoAdjustNotice);

            _autoAdjustNotice = globalAutoAdjustNotice;

            foreach (StationLoadEntry entry in allEntries)
                entry.EnabledBox.IsChecked = false;

            StationLoadEntry defaultEntry = SelectDefaultStationEntry(allEntries, perStationCounts, initDays);
            if (defaultEntry != null)
                defaultEntry.EnabledBox.IsChecked = true;

            ProgressBar loadBar;
            TextBlock totalSizeLabel, totalWarningLabel;
            Button btnLoad = BuildLoadButton();
            CheckBox cbDateFilter = new CheckBox { IsChecked = true };

            Content = BuildRootGrid(content, cbDateFilter, btnLoad,
                out loadBar, out totalSizeLabel, out totalWarningLabel);

            WireDialogEvents(new DialogWireContext
            {
                AllEntries = allEntries,
                GlobalSlider = globalSlider,
                FileCounts = globalFileCounts,
                PerStation = perStationCounts,
                ValueLabel = globalValueLabel,
                SizeLabel = globalSizeLabel,
                WarningLabel = globalWarningLabel,
                LoadBar = loadBar,
                TotalSizeLabel = totalSizeLabel,
                TotalWarningLabel = totalWarningLabel,
                BtnLoad = btnLoad,
                CbDateFilter = cbDateFilter,
                LcsStations = lcsStations,
                BackflushStations = backflushStations,
                ConnectorStations = connectorStations,
                RecommendedMonths = recommendedMonths,
                TotalCount = totalCount
            });
        }

        private void SetupWindowChrome()
        {
            Title = "Load Options";
            WindowStyle = WindowStyle.None;
            AllowsTransparency = false;
            ResizeMode = ResizeMode.CanResize;
            System.Windows.Shell.WindowChrome.SetWindowChrome(this, new System.Windows.Shell.WindowChrome
            {
                CaptionHeight = 0,
                ResizeBorderThickness = new Thickness(0),
                GlassFrameThickness = new Thickness(0),
                CornerRadius = new CornerRadius(0)
            });
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = new SolidColorBrush(Color.FromRgb(8, 14, 10));

            Rect screen = System.Windows.SystemParameters.WorkArea;
            Width = Math.Min(1600, screen.Width * 0.92);
            Height = Math.Min(1100, screen.Height * 0.92);
            MinWidth = Math.Min(900, screen.Width * 0.6);
            MinHeight = Math.Min(700, screen.Height * 0.6);

            SourceInitialized += (s, e) =>
                WindowResizer.FitToCurrentMonitor(this, maxWidthCap: 1600, maxHeightCap: 1100);
            LocationChanged += (s, e) =>
                WindowResizer.FitToCurrentMonitor(this, maxWidthCap: 1600, maxHeightCap: 1100);
        }

        private int ResolveRecommendedIndex(Dictionary<int, MonthFileInfo> fileCounts)
        {
            int recommended = CalculateRecommendedMonths(fileCounts);
            int idx = Array.IndexOf(DayOptions, recommended);
            if (idx < 0) idx = Array.IndexOf(DayOptions, 180);
            if (idx < 0) idx = 0;
            return idx;
        }

        private static StationLoadEntry SelectDefaultStationEntry(
            List<StationLoadEntry> allEntries,
            Dictionary<string, Dictionary<int, MonthFileInfo>> perStationCounts,
            int days)
        {
            if (allEntries.Count == 0) return null;

            return allEntries
                .OrderBy(e => GetStationSizeMb(e.Station, days, perStationCounts))
                .ThenBy(e => e.Station.StationName)
                .FirstOrDefault();
        }

        private static long ComputeRamBudgetMb()
        {
            long availMb = GetAvailableRamMb();
            long totalMb = GetTotalRamMb();
            if (availMb < 0 || totalMb <= 0) return -1;

            long reserveMb = (long)(totalMb * RamReserveRatio);
            return Math.Max(0, availMb - reserveMb);
        }

        private static long ComputeRamBudgetSoftMb()
        {
            long availMb = GetAvailableRamMb();
            long totalMb = GetTotalRamMb();
            if (availMb < 0 || totalMb <= 0) return -1;

            long reserveMb = (long)(totalMb * RamReserveRatioSoft);
            return Math.Max(0, availMb - reserveMb);
        }

        private static long ComputeRamBudgetHardMb()
        {
            long availMb = GetAvailableRamMb();
            long totalMb = GetTotalRamMb();
            if (availMb < 0 || totalMb <= 0) return -1;

            long reserveMb = (long)(totalMb * RamReserveRatioHard);
            return Math.Max(0, availMb - reserveMb);
        }

        private StackPanel BuildDialogContent(
            List<StationInfo> ghpStations, List<StationInfo> lcsStations,
            List<StationInfo> backflushStations, List<StationInfo> connectorStations,
            Dictionary<int, MonthFileInfo> globalFileCounts,
            Dictionary<string, Dictionary<int, MonthFileInfo>> perStationCounts,
            int recommendedIndex, int recommendedMonths, int initDays, int totalCount,
            List<StationLoadEntry> allEntries,
            out Slider slider, out TextBlock valueLabel, out TextBlock sizeLabel,
            out TextBlock warningLabel, out TextBlock autoAdjustNotice)
        {
            StackPanel content = new StackPanel();

            content.Children.Add(BuildGlobalSliderSection(
                globalFileCounts, recommendedIndex, recommendedMonths,
                out slider, out valueLabel, out sizeLabel, out warningLabel, out autoAdjustNotice));

            content.Children.Add(BuildLazyLoadInfoBanner());

            AddStationSection(content, allEntries, "Preload GHP Stations", ghpStations,
                Color.FromRgb(63, 185, 80), true, slider,
                globalFileCounts, initDays, totalCount, perStationCounts);

            AddStationSection(content, allEntries, "LCS Stations  ⚠ WIP", lcsStations,
                Color.FromRgb(80, 160, 220), false, slider,
                globalFileCounts, initDays, totalCount, perStationCounts);

            AddStationSection(content, allEntries, "Backflush Stations  ⚠ WIP", backflushStations,
                Color.FromRgb(220, 160, 60), false, slider,
                globalFileCounts, initDays, totalCount, perStationCounts);

            AddStationSection(content, allEntries, "Connectors", connectorStations,
                Color.FromRgb(180, 120, 220), false, slider,
                globalFileCounts, initDays, totalCount, perStationCounts);

            return content;
        }

        private static UIElement BuildLazyLoadInfoBanner()
        {
            return new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(8, 22, 14)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(30, 60, 38)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10, 5, 10, 5),
                Margin = new Thickness(0, 0, 0, 6),
                Child = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = "⏳",
                            FontSize = 13,
                            Margin = new Thickness(0, 0, 8, 0),
                            VerticalAlignment = VerticalAlignment.Center
                        },
                        new TextBlock
                        {
                            Text =
                                "Unchecked stations will be accessible via Lazy Load — load on demand after the main load completes.",
                            FontSize = 10,
                            Foreground = new SolidColorBrush(Color.FromRgb(90, 130, 100)),
                            TextWrapping = TextWrapping.Wrap,
                            VerticalAlignment = VerticalAlignment.Center
                        }
                    }
                }
            };
        }

        private static UIElement BuildLoadModePicker(List<StationLoadEntry> allEntries)
        {
            StackPanel row = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 6)
            };

            Button btnLazy = new Button
            {
                Content = "⏳  Lazy Load  (recommended)",
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Padding = new Thickness(14, 6, 14, 6),
                Margin = new Thickness(0, 0, 8, 0),
                Background = new SolidColorBrush(Color.FromRgb(12, 52, 26)),
                Foreground = new SolidColorBrush(Color.FromRgb(100, 210, 140)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(30, 120, 60)),
                BorderThickness = new Thickness(2),
                Cursor = Cursors.Hand
            };

            Button btnPreload = new Button
            {
                Content = "⚡  Load all selected stations",
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Padding = new Thickness(14, 6, 14, 6),
                Background = new SolidColorBrush(Color.FromRgb(40, 20, 8)),
                Foreground = new SolidColorBrush(Color.FromRgb(220, 160, 80)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(140, 80, 20)),
                BorderThickness = new Thickness(2),
                Cursor = Cursors.Hand
            };

            btnLazy.Click += (s, e) =>
            {
                foreach (StationLoadEntry entry in allEntries)
                    entry.EnabledBox.IsChecked = false;
            };

            btnPreload.Click += (s, e) =>
            {
                foreach (StationLoadEntry entry in allEntries)
                    entry.EnabledBox.IsChecked = true;
            };

            row.Children.Add(btnLazy);
            row.Children.Add(btnPreload);
            return row;
        }

        private Grid BuildRootGrid(
            StackPanel content, CheckBox cbDateFilter, Button btnLoad,
            out ProgressBar loadBar, out TextBlock totalSizeLabel, out TextBlock totalWarningLabel)
        {
            Grid outer = new Grid();

            Border rootBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(8, 14, 10)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(30, 80, 44)),
                BorderThickness = new Thickness(1)
            };

            Grid root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            UIElement titleBar = BuildTitleBar();
            Grid.SetRow(titleBar, 0);
            root.Children.Add(titleBar);

            UIElement header = BuildHeader();
            Grid.SetRow(header, 1);
            root.Children.Add(header);

            ScrollViewer scroll = new ScrollViewer
            {
                Content = content,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Padding = new Thickness(20, 14, 20, 8)
            };
            Grid.SetRow(scroll, 2);
            root.Children.Add(scroll);

            UIElement indicator = BuildLoadIndicator(out loadBar, out totalSizeLabel, out totalWarningLabel);
            Grid.SetRow(indicator, 3);
            root.Children.Add(indicator);


            UIElement msgTypeSection = BuildMessageTypeSection();
            Grid.SetRow(msgTypeSection, 4);
            root.Children.Add(msgTypeSection);

            UIElement footer = BuildFooter(cbDateFilter, btnLoad, onCancel: () =>
            {
                _ramTimer?.Stop();
                DialogResult = false;
            });
            Grid.SetRow(footer, 5);
            root.Children.Add(footer);

            rootBorder.Child = root;
            outer.Children.Add(rootBorder);
            outer.Children.Add(BuildResizeOverlay());

            return outer;
        }

        private void WireDialogEvents(DialogWireContext ctx)
        {
            Action recalculate = () => RecalculateTotalLoad(
                ctx.AllEntries, ctx.GlobalSlider, ctx.PerStation,
                ctx.LoadBar, ctx.TotalSizeLabel, ctx.TotalWarningLabel, ctx.BtnLoad, this);

            ctx.BtnLoad.Click += (s, e) => OnLoadButtonClicked(ctx);

            ctx.GlobalSlider.ValueChanged += (s, e) =>
            {
                UpdateSliderDisplay(ctx.GlobalSlider, ctx.ValueLabel, ctx.SizeLabel, ctx.WarningLabel,
                    ctx.FileCounts, ctx.RecommendedMonths);

                int newDays = DayOptions[(int)Math.Round(ctx.GlobalSlider.Value)];
                ApplyStationVisuals(ctx.AllEntries, newDays, ctx.PerStation);

                recalculate();
            };

            foreach (StationLoadEntry entry in ctx.AllEntries)
            {
                entry.EnabledBox.Checked += (s, e) =>
                {
                    AutoAdjustDateRangeIfNeeded(ctx.AllEntries, ctx.GlobalSlider, ctx.PerStation);
                    recalculate();
                };
                entry.EnabledBox.Unchecked += (s, e) =>
                {
                    AutoAdjustDateRangeIfNeeded(ctx.AllEntries, ctx.GlobalSlider, ctx.PerStation);
                    recalculate();
                };
            }

            UpdateSliderDisplay(ctx.GlobalSlider, ctx.ValueLabel, ctx.SizeLabel, ctx.WarningLabel,
                ctx.FileCounts, ctx.RecommendedMonths);

            int initDays = DayOptions[(int)Math.Round(ctx.GlobalSlider.Value)];
            ApplyStationVisuals(ctx.AllEntries, initDays, ctx.PerStation);

            AutoAdjustDateRangeIfNeeded(ctx.AllEntries, ctx.GlobalSlider, ctx.PerStation);
            recalculate();

            _ramTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _ramTimer.Tick += (s, e) => UpdateRamLabel(recalculate);
            _ramTimer.Start();
            Closed += (s, e) => _ramTimer.Stop();
        }

        private void OnLoadButtonClicked(DialogWireContext ctx)
        {
            int idx = (int)Math.Round(ctx.GlobalSlider.Value);
            FilterByDate = ctx.CbDateFilter.IsChecked == true;
            MaxDays = DayOptions[idx];

            IncludeLcs = ctx.AllEntries.Any(en =>
                ctx.LcsStations.Any(st => st.FolderPath == en.Station.FolderPath) &&
                en.EnabledBox.IsChecked == true);
            IncludeBackflush = ctx.AllEntries.Any(en =>
                ctx.BackflushStations.Any(st => st.FolderPath == en.Station.FolderPath) &&
                en.EnabledBox.IsChecked == true);
            IncludeConnectors = ctx.AllEntries.Any(en =>
                ctx.ConnectorStations.Any(st => st.FolderPath == en.Station.FolderPath) &&
                en.EnabledBox.IsChecked == true);

            ExcludedFolderPaths = new List<string>();

            LazyLoadFolderPaths = ctx.AllEntries
                .Where(stEntry => stEntry.EnabledBox.IsChecked != true)
                .Select(stEntry => stEntry.Station.FolderPath)
                .ToList();

            StationMonthOverrides = new Dictionary<string, int>();

            EnabledMessageTypes = _messageTypeCheckboxes
                .Where(kv => kv.Value.IsChecked == true)
                .Select(kv => kv.Key)
                .ToList();

            _ramTimer?.Stop();
            WindowAnimations.FadeOutAndClose(this, true);
        }

        private void AutoAdjustDateRangeIfNeeded(
            List<StationLoadEntry> allEntries, Slider globalSlider,
            Dictionary<string, Dictionary<int, MonthFileInfo>> perStationCounts)
        {
            List<StationLoadEntry> enabled = allEntries.Where(e => e.EnabledBox.IsChecked == true).ToList();

            if (enabled.Count == 0 || perStationCounts == null)
            {
                SetAutoAdjustNotice("");
                return;
            }

            long hardBudgetMb = ComputeRamBudgetHardMb();
            if (hardBudgetMb < 0) hardBudgetMb = 1000;

            long softBudgetMb = ComputeRamBudgetSoftMb();
            if (softBudgetMb < 0) softBudgetMb = 2500;
            if (softBudgetMb < hardBudgetMb) softBudgetMb = hardBudgetMb;

            if (EstimateTotalRamAtIndex(enabled, perStationCounts, 0) > hardBudgetMb)
            {
                globalSlider.Value = 0;
                SetAutoAdjustNotice("⚠  Even the smallest range (" + FormatDayCount(DayOptions[0]) +
                                    ") is heavy for the currently selected stations — free up memory or unselect some stations");
                return;
            }

            int bestIdx = 0;
            for (int idx = DayOptions.Length - 1; idx >= 0; idx--)
            {
                if (EstimateTotalRamAtIndex(enabled, perStationCounts, idx) <= softBudgetMb)
                {
                    bestIdx = idx;
                    break;
                }
            }

            int currentIdx = (int)Math.Round(globalSlider.Value);
            if (bestIdx == currentIdx)
            {
                SetAutoAdjustNotice("");
                return;
            }

            globalSlider.Value = bestIdx;

            SetAutoAdjustNotice(bestIdx < currentIdx
                ? "ℹ  Date range adjusted to " + FormatDayCount(DayOptions[bestIdx]) +
                  " to comfortably fit the selected stations in memory"
                : "ℹ  Date range expanded to " + FormatDayCount(DayOptions[bestIdx]) +
                  " — plenty of RAM available for the selected stations");
        }

        private void SetAutoAdjustNotice(string text)
        {
            if (_autoAdjustNotice != null)
                _autoAdjustNotice.Text = text;
        }

        private static long EstimateTotalRamAtIndex(
            List<StationLoadEntry> enabled, Dictionary<string, Dictionary<int, MonthFileInfo>> perStationCounts,
            int dayIndex)
        {
            int days = DayOptions[dayIndex];
            long sumDiskMb = enabled.Sum(e => GetStationSizeMb(e.Station, days, perStationCounts));
            return EstimateRamMb(sumDiskMb);
        }

        private void UpdateRamLabel(Action recalculate)
        {
            if (_ramValueLabel == null) return;

            long availMb = GetAvailableRamMb();
            long totalRamMb = GetTotalRamMb();

            if (availMb < 0)
            {
                _ramValueLabel.Text = "unknown";
                _ramValueLabel.Foreground = new SolidColorBrush(Color.FromRgb(100, 110, 100));
                return;
            }

            _ramValueLabel.Text = FormatAvailableRamText(availMb);
            _ramValueLabel.Foreground = new SolidColorBrush(GetRamStatusColor(availMb, totalRamMb));
        }

        private static string FormatAvailableRamText(long availMb)
        {
            return availMb >= 1024 ? (availMb / 1024.0).ToString("0.#") + " GB free" : availMb + " MB free";
        }

        private static Color GetRamStatusColor(long availMb, long totalRamMb)
        {
            double usedRatio = 1.0 - (availMb / (double)Math.Max(1, totalRamMb));
            if (usedRatio > 0.85) return Color.FromRgb(230, 100, 80);
            if (usedRatio > 0.65) return Color.FromRgb(210, 160, 50);
            return Color.FromRgb(80, 185, 120);
        }

        [StructLayout(LayoutKind.Sequential)]
        private class MEMORYSTATUSEX
        {
            public uint dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);

        private static long GetAvailableRamMb()
        {
            try
            {
                MEMORYSTATUSEX status = new MEMORYSTATUSEX();
                if (GlobalMemoryStatusEx(status)) return (long)(status.ullAvailPhys / 1024 / 1024);
            }
            catch
            {
            }

            return -1;
        }

        private static long GetTotalRamMb()
        {
            try
            {
                MEMORYSTATUSEX status = new MEMORYSTATUSEX();
                if (GlobalMemoryStatusEx(status)) return (long)(status.ullTotalPhys / 1024 / 1024);
            }
            catch
            {
            }

            return -1;
        }

        private static string FormatMb(long mb)
        {
            return mb >= 1024 ? (mb / 1024.0).ToString("0.#") + " GB" : mb + " MB";
        }

        private static long EstimateRamMb(long diskSizeMb)
        {
            return (long)Math.Round(diskSizeMb * RamEstimationFactor);
        }

        private static string FormatEstimatedSize(long diskSizeMb)
        {
            if (diskSizeMb <= 0) return "";
            long estimatedMb = EstimateRamMb(diskSizeMb);
            return estimatedMb >= 1024
                ? "~" + (estimatedMb / 1024.0).ToString("0.#") + " GB"
                : "~" + estimatedMb + " MB";
        }

        private static (Color textColor, Color barColor, string statusText) GetLoadStatus(long sizeMb)
        {
            if (sizeMb >= CriticalSizeMb)
                return (Color.FromRgb(180, 30, 20), Color.FromRgb(180, 30, 20), "✕  Danger — very likely to crash");
            if (sizeMb >= WarningSizeMb)
                return (Color.FromRgb(220, 60, 40), Color.FromRgb(220, 60, 40), "⚠  Risk — may run out of memory");
            if (sizeMb >= GoodSizeMb)
                return (Color.FromRgb(220, 140, 30), Color.FromRgb(220, 140, 30), "⚠  Heavy — loading will be slow");
            if (sizeMb >= OptimalSizeMb)
                return (Color.FromRgb(160, 200, 60), Color.FromRgb(160, 200, 60), "✓  Good");
            return (Color.FromRgb(46, 185, 80), Color.FromRgb(46, 185, 80), "✓  Optimal");
        }

        private static int CalculateRecommendedMonths(Dictionary<int, MonthFileInfo> fileCounts)
        {
            if (fileCounts == null) return 14;

            long budgetMb = ComputeRamBudgetSoftMb();
            if (budgetMb < 0) budgetMb = 2500;

            foreach (int days in DayOptions)
                if (fileCounts.TryGetValue(days, out MonthFileInfo info) && info.FileCount > 0 &&
                    EstimateRamMb(info.SizeMb) <= budgetMb)
                    return days;

            foreach (int days in DayOptions)
                if (fileCounts.TryGetValue(days, out MonthFileInfo info) && info.FileCount > 0)
                    return days;

            return DayOptions[0];
        }

        private static long GetStationSizeMb(
            StationInfo station, int currentDays,
            Dictionary<string, Dictionary<int, MonthFileInfo>> perStationCounts)
        {
            if (perStationCounts == null) return 0;
            if (!perStationCounts.TryGetValue(station.FolderPath, out Dictionary<int, MonthFileInfo> stationInfo))
                return 0;
            if (!stationInfo.TryGetValue(currentDays, out MonthFileInfo dayInfo)) return 0;
            return dayInfo.SizeMb;
        }

        private static void RecalculateTotalLoad(
            List<StationLoadEntry> entries, Slider globalSlider,
            Dictionary<string, Dictionary<int, MonthFileInfo>> perStationCounts,
            ProgressBar loadBar, TextBlock totalSizeLabel, TextBlock totalWarningLabel,
            Button btnLoad, LoadOptionsDialog dialog = null)
        {
            List<StationLoadEntry> enabled = entries.Where(e => e.EnabledBox.IsChecked == true).ToList();

            if (enabled.Count == 0 || perStationCounts == null)
            {
                ResetLoadIndicator(loadBar, totalSizeLabel, totalWarningLabel, btnLoad);
                ResetStationColors(entries);
                return;
            }

            int currentDays = DayOptions[(int)Math.Round(globalSlider.Value)];
            long totalDiskMb = enabled.Sum(e => GetStationSizeMb(e.Station, currentDays, perStationCounts));
            long availableRamMb = GetAvailableRamMb();

            UpdateLoadIndicator(loadBar, totalSizeLabel, totalWarningLabel, btnLoad,
                enabled.Count, totalDiskMb, availableRamMb);

            ApplyStationVisuals(entries, currentDays, perStationCounts);

            dialog?.UpdateRamEstimatedMarker(EstimateRamMb(totalDiskMb));
        }

        private static void ResetLoadIndicator(
            ProgressBar loadBar, TextBlock totalSizeLabel, TextBlock totalWarningLabel, Button btnLoad)
        {
            loadBar.Value = 0;
            totalSizeLabel.Text = "No stations selected";
            totalWarningLabel.Text = "";
            btnLoad.IsEnabled = true;
            btnLoad.ToolTip = null;
        }

        private static void UpdateLoadIndicator(
            ProgressBar loadBar, TextBlock totalSizeLabel, TextBlock totalWarningLabel, Button btnLoad,
            int enabledCount, long totalDiskMb, long availableRamMb)
        {
            long estimatedRamMb = EstimateRamMb(totalDiskMb);
            (Color textColor, Color barColor, string statusText) = GetLoadStatus(estimatedRamMb);

            string sizeText = estimatedRamMb >= 1024
                ? "~" + (estimatedRamMb / 1024.0).ToString("0.#") + " GB  estimated"
                : "~" + estimatedRamMb + " MB  estimated";

            loadBar.Value = Math.Min(100, estimatedRamMb * 100.0 / CriticalSizeMb);
            loadBar.Foreground = new SolidColorBrush(barColor);
            totalSizeLabel.Text = $"{enabledCount} stations  ·  {sizeText}";
            totalSizeLabel.Foreground = new SolidColorBrush(textColor);
            btnLoad.IsEnabled = true;
            btnLoad.ToolTip = null;

            bool ramLow = availableRamMb > 0 && estimatedRamMb > availableRamMb;

            totalWarningLabel.Text = ramLow
                ? "ℹ  Estimated RAM usage exceeds available — consider Lazy Load"
                : statusText;
            totalWarningLabel.Foreground = new SolidColorBrush(ramLow ? Color.FromRgb(160, 160, 160) : textColor);
        }

        private static void ApplyStationVisuals(
            List<StationLoadEntry> entries, int currentDays,
            Dictionary<string, Dictionary<int, MonthFileInfo>> perStationCounts)
        {
            long budgetMb = ComputeRamBudgetSoftMb();
            if (budgetMb < 0) budgetMb = long.MaxValue;
            long accumulated = 0;

            foreach (StationLoadEntry entry in entries)
            {
                long stationDiskMb = GetStationSizeMb(entry.Station, currentDays, perStationCounts);
                bool isEnabled = entry.EnabledBox.IsChecked == true;

                if (isEnabled) accumulated += EstimateRamMb(stationDiskMb);

                double ratio = isEnabled && budgetMb > 0 ? (double)accumulated / budgetMb : 0;
                bool wouldExceed = isEnabled && ratio > 1.0;

                Color color = GetSeverityColor(ratio);
                entry.EnabledBox.Opacity = wouldExceed ? 0.35 : 1.0;
                entry.EnabledBox.Foreground = new SolidColorBrush(wouldExceed ? Color.FromRgb(100, 100, 100) : color);

                UpdateStationNameLabel(entry, stationDiskMb, wouldExceed, color);
            }
        }

        private static Color GetSeverityColor(double ratio)
        {
            if (ratio <= 0.5) return Color.FromRgb(46, 185, 80);
            if (ratio <= 0.75) return Color.FromRgb(220, 180, 30);
            return Color.FromRgb(220, 80, 40);
        }

        private static void UpdateStationNameLabel(StationLoadEntry entry, long stationDiskMb, bool wouldExceed,
            Color color)
        {
            if (entry.NameLabel == null) return;

            entry.NameLabel.Inlines.Clear();
            entry.NameLabel.Inlines.Add(new Run(entry.Station.StationName)
            {
                FontSize = 12,
                Foreground = new SolidColorBrush(wouldExceed ? Color.FromRgb(100, 100, 100) : color)
            });

            string sizeText = FormatEstimatedSize(stationDiskMb);
            if (string.IsNullOrEmpty(sizeText)) return;

            entry.NameLabel.Inlines.Add(new Run("  [" + sizeText + "]")
            {
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(90, 130, 100))
            });
        }

        private static void ResetStationColors(List<StationLoadEntry> entries)
        {
            foreach (StationLoadEntry entry in entries)
            {
                entry.EnabledBox.Opacity = 1.0;
                if (entry.EnabledBox.Content is TextBlock tb)
                    tb.Foreground = new SolidColorBrush(Color.FromRgb(175, 220, 190));
            }
        }

        private void UpdateRamEstimatedMarker(long estimatedRamMb)
        {
            if (_ramEstimatedMarker == null) return;

            long totalRamMb = GetTotalRamMb();
            long availMb = GetAvailableRamMb();

            if (totalRamMb <= 0 || availMb < 0)
            {
                _ramEstimatedMarker.Opacity = 0;
                if (_ramEstimatedFill != null) _ramEstimatedFill.Opacity = 0;
                return;
            }

            long usedMb = totalRamMb - availMb;
            long afterLoadMb = usedMb + estimatedRamMb;
            double currentPct = Math.Min(1.0, usedMb / (double)totalRamMb);
            double afterPct = Math.Min(1.0, afterLoadMb / (double)totalRamMb);

            _ramEstimatedMarker.Opacity = 0.9;
            _ramEstimatedMarker.Tag = afterPct;

            UpdateRamEstimatedFill(currentPct, afterPct, estimatedRamMb);

            if (_ramEstimatedLabel != null)
                _ramEstimatedLabel.Text = "Estimated after load:  " + FormatMb(afterLoadMb);

            if (_ramEstimatedMarker.Parent is Grid parent && parent.ActualWidth > 0)
                _ramEstimatedMarker.Margin = new Thickness(Math.Max(0, afterPct * parent.ActualWidth - 5.0), 0, 0, 0);
        }

        private void UpdateRamEstimatedFill(double currentPct, double afterPct, long estimatedRamMb)
        {
            if (_ramEstimatedFill == null) return;

            _ramEstimatedFill.Opacity = estimatedRamMb > 0 ? 1.0 : 0.0;
            _ramEstimatedFill.Tag = new[] { currentPct, afterPct };

            if (_ramEstimatedFill.Parent is Grid fillParent && fillParent.ActualWidth > 0)
            {
                _ramEstimatedFill.Margin = new Thickness(currentPct * fillParent.ActualWidth, 0, 0, 0);
                _ramEstimatedFill.Width = Math.Max(0, (afterPct - currentPct) * fillParent.ActualWidth);
            }
        }

        private static UIElement BuildGlobalSliderSection(
            Dictionary<int, MonthFileInfo> fileCounts, int defaultIndex, int recommendedMonths,
            out Slider slider, out TextBlock valueLabel, out TextBlock sizeLabel,
            out TextBlock warningLabel, out TextBlock autoAdjustNotice)
        {
            Border border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(10, 22, 14)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(30, 80, 44)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(14, 8, 14, 8),
                Margin = new Thickness(0, 0, 0, 6)
            };
            StackPanel stack = new StackPanel();

            stack.Children.Add(new TextBlock
            {
                Text = "Set data range",
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(160, 210, 175)),
                Margin = new Thickness(0, 0, 0, 2)
            });

            AddDateRangeSummary(stack, fileCounts);

            slider = BuildSlider(defaultIndex);
            stack.Children.Add(WrapSliderWithLabels(slider));

            StackPanel infoRow = new StackPanel
                { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 1) };
            valueLabel = new TextBlock { FontSize = 16, FontWeight = FontWeights.Bold };
            sizeLabel = new TextBlock
                { FontSize = 11, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(10, 0, 0, 2) };
            infoRow.Children.Add(valueLabel);
            infoRow.Children.Add(sizeLabel);
            stack.Children.Add(infoRow);

            AddRecommendedSizeRow(stack, fileCounts, recommendedMonths);

            warningLabel = new TextBlock
                { FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 3, 0, 0) };
            stack.Children.Add(warningLabel);

            autoAdjustNotice = new TextBlock
            {
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(Color.FromRgb(120, 170, 210)),
                Margin = new Thickness(0, 4, 0, 0)
            };
            stack.Children.Add(autoAdjustNotice);

            border.Child = stack;
            return border;
        }

        private static void AddDateRangeSummary(StackPanel stack, Dictionary<int, MonthFileInfo> fileCounts)
        {
            if (fileCounts == null) return;

            DateTime minDate = DateTime.MaxValue;
            DateTime maxDate = DateTime.MinValue;

            foreach (MonthFileInfo fi in fileCounts.Values)
            {
                if (fi.MinDate < minDate) minDate = fi.MinDate;
                if (fi.MaxDate > maxDate) maxDate = fi.MaxDate;
            }

            string rangeText = minDate == DateTime.MaxValue
                ? "No files found"
                : minDate.ToString("d. MMM yyyy") + "  —  " + maxDate.ToString("d. MMM yyyy");

            stack.Children.Add(new TextBlock
            {
                Text = rangeText,
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(90, 130, 100)),
                Margin = new Thickness(0, 0, 0, 10)
            });
        }

        private static void AddRecommendedSizeRow(StackPanel stack, Dictionary<int, MonthFileInfo> fileCounts,
            int recommendedMonths)
        {
            if (fileCounts == null || !fileCounts.TryGetValue(recommendedMonths, out MonthFileInfo recInfo)) return;

            string recSize = FormatEstimatedSize(recInfo.SizeMb);

            StackPanel recRow = new StackPanel
                { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 4) };
            recRow.Children.Add(new TextBlock
            {
                Text = "Recommended: ",
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(70, 110, 82))
            });
            recRow.Children.Add(new TextBlock
            {
                Text = $"{recommendedMonths}d  ({recInfo.FileCount} files, {recSize})",
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(63, 185, 80))
            });
            stack.Children.Add(recRow);
        }

        private static void UpdateSliderDisplay(
            Slider slider, TextBlock valueLabel, TextBlock sizeLabel, TextBlock warningLabel,
            Dictionary<int, MonthFileInfo> fileCounts, int recommendedMonths)
        {
            int idx = (int)Math.Round(slider.Value);
            int days = DayOptions[idx];

            valueLabel.Foreground = new SolidColorBrush(GetSliderPositionColor(idx));
            valueLabel.Text = FormatDayCount(days);

            if (fileCounts == null || !fileCounts.TryGetValue(days, out MonthFileInfo info))
            {
                sizeLabel.Text = "";
                warningLabel.Text = "";
                return;
            }

            sizeLabel.Text = $"{info.FileCount} files  ·  {FormatEstimatedSize(info.SizeMb)}";

            long estimatedRamMb = EstimateRamMb(info.SizeMb);
            (Color textColor, _, string statusText) = GetLoadStatus(estimatedRamMb);

            valueLabel.Foreground = new SolidColorBrush(textColor);
            sizeLabel.Foreground = new SolidColorBrush(textColor);
            warningLabel.Text = days <= recommendedMonths && estimatedRamMb < OptimalSizeMb
                ? "✓  Recommended"
                : statusText;
            warningLabel.Foreground = new SolidColorBrush(textColor);
        }

        private static Color GetSliderPositionColor(int idx)
        {
            double colorRatio = (double)idx / Math.Max(1, DayOptions.Length - 1);
            byte r = (byte)(46 + colorRatio * (220 - 46));
            byte g = (byte)(185 - colorRatio * (185 - 140));
            byte b = (byte)(80 - colorRatio * 50);
            return Color.FromRgb(r, g, b);
        }

        private static string FormatDayCount(int days)
        {
            if (days < 7) return days + " day" + (days == 1 ? "" : "s");
            if (days < 30) return (days / 7) + " week" + (days / 7 == 1 ? "" : "s");
            if (days < 365) return (days / 30) + " month" + (days / 30 == 1 ? "" : "s");
            if (days == 365) return "1 year";
            if (days == 548) return "1.5 years";
            if (days == 730) return "2 years";
            return days + " days";
        }

        private static CheckBox AddStationSection(
            StackPanel parent, List<StationLoadEntry> allEntries, string title, List<StationInfo> stations,
            Color accentColor, bool defaultChecked, Slider slider,
            Dictionary<int, MonthFileInfo> fileCounts = null, int currentDays = 0, int totalStationCount = 1,
            Dictionary<string, Dictionary<int, MonthFileInfo>> perStationCounts = null)
        {
            if (stations.Count == 0) return null;

            List<StationLoadEntry> sectionEntries = BuildSectionEntries(allEntries, stations, defaultChecked);

            StackPanel stack = BuildSectionStack(title, stations.Count, accentColor, defaultChecked, sectionEntries,
                slider, fileCounts, currentDays, totalStationCount, perStationCounts);

            Border border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(10, 22, 14)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(180, accentColor.R, accentColor.G, accentColor.B)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12, 8, 12, 8),
                Margin = new Thickness(0, 0, 0, 6),
                Child = stack
            };

            parent.Children.Add(border);
            return stack.Children.OfType<CheckBox>().FirstOrDefault();
        }

        private static List<StationLoadEntry> BuildSectionEntries(
            List<StationLoadEntry> allEntries, List<StationInfo> stations, bool defaultChecked)
        {
            List<StationLoadEntry> sectionEntries = new List<StationLoadEntry>();

            foreach (StationInfo st in stations)
            {
                CheckBox cb = new CheckBox { IsChecked = false, IsEnabled = true };
                StationLoadEntry entry = new StationLoadEntry { Station = st, EnabledBox = cb };
                allEntries.Add(entry);
                sectionEntries.Add(entry);
            }

            return sectionEntries;
        }

        private static StackPanel BuildSectionStack(
            string title, int stationCount, Color accentColor, bool defaultChecked,
            List<StationLoadEntry> sectionEntries, Slider slider,
            Dictionary<int, MonthFileInfo> fileCounts, int currentDays, int totalStationCount,
            Dictionary<string, Dictionary<int, MonthFileInfo>> perStationCounts = null)
        {
            StackPanel stack = new StackPanel();
            CheckBox cbSection = new CheckBox { IsChecked = false };

            cbSection.Content = BuildSectionTitle(title, stationCount, accentColor);
            stack.Children.Add(cbSection);

            stack.Children.Add(new TextBlock
            {
                Text =
                    "⏳  Unchecked stations will be accessible via Lazy Load — available for on-demand loading after the main load completes.",
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(80, 120, 90)),
                Margin = new Thickness(0, 2, 0, 3),
                TextWrapping = TextWrapping.Wrap
            });

            WireSectionCheckbox(cbSection, sectionEntries);
            stack.Children.Add(BuildActionButtonRow(sectionEntries, slider, perStationCounts));
            stack.Children.Add(BuildStationGridSection(cbSection, sectionEntries, accentColor, defaultChecked,
                fileCounts, currentDays, totalStationCount, perStationCounts));

            return stack;
        }

        private static UIElement BuildSectionTitle(string title, int stationCount, Color accentColor)
        {
            StackPanel titlePanel = new StackPanel { Orientation = Orientation.Horizontal };

            titlePanel.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 15,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(accentColor)
            });
            titlePanel.Children.Add(new TextBlock
            {
                Text = "  (" + stationCount + ")",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 140, 112)),
                VerticalAlignment = VerticalAlignment.Center
            });

            return titlePanel;
        }

        private static void WireSectionCheckbox(CheckBox cbSection, List<StationLoadEntry> sectionEntries)
        {
            foreach (StationLoadEntry entry in sectionEntries)
            {
                CheckBox cb = entry.EnabledBox;
                cbSection.Checked += (s, e) =>
                {
                    cb.IsEnabled = true;
                    cb.IsChecked = true;
                };
                cbSection.Unchecked += (s, e) =>
                {
                    cb.IsEnabled = true;
                    cb.IsChecked = false;
                };
            }
        }

        private static StackPanel BuildActionButtonRow(
            List<StationLoadEntry> sectionEntries, Slider slider,
            Dictionary<string, Dictionary<int, MonthFileInfo>> perStationCounts)
        {
            Button btnCheckAll = MakeActionButton("✓ Check All", Color.FromRgb(100, 185, 130));
            Button btnUncheckAll = MakeActionButton("○ Uncheck All", Color.FromRgb(120, 140, 125));
            Button btnCheckMax = MakeActionButton("★ Check Maximum", Color.FromRgb(160, 200, 60));
            btnCheckMax.ToolTip =
                "Loads the maximum number of stations that fit within available RAM, smallest first (green zone only)";

            btnCheckAll.Click += (s, e) =>
            {
                foreach (StationLoadEntry ent in sectionEntries) ent.EnabledBox.IsChecked = true;
            };
            btnUncheckAll.Click += (s, e) =>
            {
                foreach (StationLoadEntry ent in sectionEntries)
                {
                    ent.EnabledBox.IsEnabled = true;
                    ent.EnabledBox.IsChecked = false;
                }
            };
            btnCheckMax.Click += (s, e) =>
            {
                int liveDays = DayOptions[(int)Math.Round(slider.Value)];
                ApplyCheckMaximum(sectionEntries, liveDays, perStationCounts);
            };

            StackPanel btnRow = new StackPanel
                { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 4) };
            btnRow.Children.Add(btnCheckAll);
            btnRow.Children.Add(btnUncheckAll);
            btnRow.Children.Add(btnCheckMax);
            return btnRow;
        }

        private static Button MakeActionButton(string label, Color foreground)
        {
            return new Button
            {
                Content = label,
                FontSize = 10,
                Padding = new Thickness(8, 3, 8, 3),
                Margin = new Thickness(0, 0, 6, 0),
                Background = new SolidColorBrush(Color.FromRgb(14, 40, 20)),
                Foreground = new SolidColorBrush(foreground),
                BorderBrush = new SolidColorBrush(Color.FromRgb(30, 80, 44)),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand
            };
        }

        private static void ApplyCheckMaximum(
            List<StationLoadEntry> sectionEntries, int currentDays,
            Dictionary<string, Dictionary<int, MonthFileInfo>> perStationCounts)
        {
            long availMb = GetAvailableRamMb();

            if (availMb <= 0)
            {
                int half = Math.Max(1, sectionEntries.Count / 2);
                for (int i = 0; i < sectionEntries.Count; i++)
                    sectionEntries[i].EnabledBox.IsChecked = i < half;
                return;
            }

            long budgetMb = ComputeRamBudgetSoftMb();
            if (budgetMb < 0) budgetMb = Math.Max(0, availMb - 500);

            List<(StationLoadEntry entry, long mb)> bySize = sectionEntries
                .Select(e => (entry: e, mb: EstimateRamMb(GetStationSizeMb(e.Station, currentDays, perStationCounts))))
                .OrderBy(x => x.mb)
                .ToList();

            HashSet<StationLoadEntry> selected = new HashSet<StationLoadEntry>();
            long accumulated = 0;

            foreach ((StationLoadEntry entry, long mb) in bySize)
            {
                long next = accumulated + mb;
                if (next > budgetMb && selected.Count > 0) break;
                accumulated = next;
                selected.Add(entry);
            }

            foreach (StationLoadEntry ent in sectionEntries)
                ent.EnabledBox.IsChecked = selected.Contains(ent);
        }

        private static UIElement BuildStationGridSection(
            CheckBox cbSection, List<StationLoadEntry> sectionEntries,
            Color accentColor, bool defaultChecked,
            Dictionary<int, MonthFileInfo> fileCounts, int currentDays, int totalStationCount,
            Dictionary<string, Dictionary<int, MonthFileInfo>> perStationCounts = null)
        {
            UIElement grid = BuildStationGrid(sectionEntries, fileCounts, currentDays, perStationCounts);

            if (defaultChecked)
                return new Border { Child = grid, Margin = new Thickness(0, 8, 0, 0) };

            Expander expander = new Expander
            {
                Header = "Choose stations ▾",
                IsExpanded = false,
                Margin = new Thickness(0, 6, 0, 0),
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(90, 150, 110)),
                IsEnabled = false,
                Content = new Border { Child = grid, Margin = new Thickness(0, 4, 0, 0) }
            };

            cbSection.Checked += (s, e) => expander.IsEnabled = true;
            cbSection.Unchecked += (s, e) => expander.IsEnabled = false;

            return expander;
        }

        private static UIElement BuildStationGrid(
            List<StationLoadEntry> entries,
            Dictionary<int, MonthFileInfo> fileCounts, int currentDays,
            Dictionary<string, Dictionary<int, MonthFileInfo>> perStationCounts)
        {
            int cols = entries.Count > 12 ? 3 : 2;
            Grid grid = new Grid();
            int rowCount = (entries.Count + cols - 1) / cols;

            for (int c = 0; c < cols; c++)
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            for (int r = 0; r < rowCount; r++)
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            for (int i = 0; i < entries.Count; i++)
                PlaceStationCheckbox(grid, entries[i], i % cols, i / cols, currentDays, perStationCounts);

            return grid;
        }

        private static void PlaceStationCheckbox(
            Grid grid, StationLoadEntry entry, int col, int row, int currentDays,
            Dictionary<string, Dictionary<int, MonthFileInfo>> perStationCounts)
        {
            TextBlock nameLabel = new TextBlock
            {
                TextWrapping = TextWrapping.NoWrap,
                TextTrimming = TextTrimming.CharacterEllipsis
            };

            nameLabel.Inlines.Add(new Run(entry.Station.StationName)
            {
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(175, 220, 190))
            });

            string stationSize = CalculateStationSize(entry.Station, currentDays, perStationCounts);
            if (!string.IsNullOrEmpty(stationSize))
                nameLabel.Inlines.Add(new Run("  [" + stationSize + "]")
                {
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromRgb(90, 130, 100))
                });

            entry.EnabledBox.Content = nameLabel;
            entry.EnabledBox.Foreground = new SolidColorBrush(Color.FromRgb(175, 220, 190));
            entry.NameLabel = nameLabel;

            StackPanel outer = new StackPanel { Margin = new Thickness(0, 2, 6, 2) };
            outer.Children.Add(entry.EnabledBox);
            Grid.SetColumn(outer, col);
            Grid.SetRow(outer, row);
            grid.Children.Add(outer);
        }

        private static string CalculateStationSize(
            StationInfo station, int currentDays,
            Dictionary<string, Dictionary<int, MonthFileInfo>> perStationCounts)
        {
            long diskMb = GetStationSizeMb(station, currentDays, perStationCounts);
            return FormatEstimatedSize(diskMb);
        }

        private UIElement BuildLoadIndicator(out ProgressBar loadBar, out TextBlock totalSizeLabel,
            out TextBlock totalWarningLabel)
        {
            Border border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(5, 18, 9)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(22, 70, 36)),
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(16, 8, 16, 8)
            };
            StackPanel stack = new StackPanel();

            AddLoadSummaryRow(stack, out loadBar, out totalSizeLabel, out totalWarningLabel);
            AddMemorySection(stack);

            border.Child = stack;
            return border;
        }

        private void AddLoadSummaryRow(StackPanel stack, out ProgressBar loadBar, out TextBlock totalSizeLabel,
            out TextBlock totalWarningLabel)
        {
            StackPanel loadRow = new StackPanel
                { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
            loadRow.Children.Add(new TextBlock
            {
                Text = "Estimated load:  ",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(70, 110, 82)),
                VerticalAlignment = VerticalAlignment.Center
            });
            totalSizeLabel = new TextBlock { FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
            loadRow.Children.Add(totalSizeLabel);
            stack.Children.Add(loadRow);

            loadBar = new ProgressBar { Height = 0, Visibility = Visibility.Collapsed };
            stack.Children.Add(loadBar);

            totalWarningLabel = new TextBlock
                { FontSize = 11, Margin = new Thickness(0, 2, 0, 6), TextWrapping = TextWrapping.Wrap };
            stack.Children.Add(totalWarningLabel);
        }

        private void AddMemorySection(StackPanel stack)
        {
            long availMb = GetAvailableRamMb();
            long totalRamMb = GetTotalRamMb();
            double usedPct = totalRamMb > 0 ? Math.Min(1.0, 1.0 - (availMb / (double)totalRamMb)) : 0.0;

            stack.Children.Add(BuildMemoryLabelsRow(availMb, totalRamMb));

            LinearGradientBrush ramFgBrush = BuildRamGradientBrush();
            Grid ramBarOuter = BuildRamBar(ramFgBrush, usedPct);
            stack.Children.Add(ramBarOuter);

            long usedMb = totalRamMb > 0 ? (long)((1.0 - availMb / (double)totalRamMb) * totalRamMb) : 0;
            stack.Children.Add(BuildMemoryLegend(ramFgBrush, usedMb));

            UpdateRamLabel(null);
        }

        private static Grid BuildMemoryLabelsRow(long availMb, long totalRamMb)
        {
            Grid ramLabels = new Grid { Margin = new Thickness(0, 0, 0, 3) };
            ramLabels.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            ramLabels.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            ramLabels.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            ramLabels.Children.Add(new TextBlock
            {
                Text = "Current RAM:",
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(70, 110, 82))
            });

            string totalRamText = totalRamMb >= 1024
                ? (totalRamMb / 1024.0).ToString("0.#") + " GB total"
                : totalRamMb + " MB total";
            TextBlock totalRamLabel = new TextBlock
            {
                Text = totalRamText,
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(60, 90, 70))
            };
            Grid.SetColumn(totalRamLabel, 2);
            ramLabels.Children.Add(totalRamLabel);

            return ramLabels;
        }

        private static LinearGradientBrush BuildRamGradientBrush()
        {
            var spectrumStops = new (double stop, byte r, byte g, byte b)[]
            {
                (0.00, 46, 185, 80), (0.25, 160, 200, 60), (0.50, 220, 180, 30), (0.75, 220, 80, 40),
                (1.00, 160, 30, 20)
            };

            LinearGradientBrush brush = new LinearGradientBrush
                { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0) };
            foreach (var (stop, r, g, b) in spectrumStops)
                brush.GradientStops.Add(new GradientStop(Color.FromRgb(r, g, b), stop));
            return brush;
        }

        private Grid BuildRamBar(LinearGradientBrush ramFgBrush, double usedPct)
        {
            Grid ramBarOuter = new Grid { Height = 12, Margin = new Thickness(0, 2, 0, 2) };

            LinearGradientBrush ramBgBrush = new LinearGradientBrush
                { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0) };
            foreach (GradientStop stop in ramFgBrush.GradientStops)
                ramBgBrush.GradientStops.Add(
                    new GradientStop(Color.FromArgb(35, stop.Color.R, stop.Color.G, stop.Color.B), stop.Offset));

            ramBarOuter.Children.Add(new System.Windows.Shapes.Rectangle
            {
                Fill = ramBgBrush,
                RadiusX = 6,
                RadiusY = 6,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Height = 20
            });

            ramBarOuter.Children.Add(new ProgressBar
            {
                Height = 20,
                Minimum = 0,
                Maximum = 100,
                Value = Math.Round(usedPct * 100),
                Foreground = ramFgBrush,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0)
            });

            _ramEstimatedFill = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(80, 0, 200, 255)),
                VerticalAlignment = VerticalAlignment.Stretch,
                HorizontalAlignment = HorizontalAlignment.Left,
                Opacity = 0.0,
                Width = 0
            };
            ramBarOuter.Children.Add(_ramEstimatedFill);

            _ramEstimatedMarker = new Border
            {
                Width = 10,
                Background = new SolidColorBrush(Color.FromRgb(0, 240, 255)),
                VerticalAlignment = VerticalAlignment.Stretch,
                HorizontalAlignment = HorizontalAlignment.Left,
                Opacity = 0.0,
                CornerRadius = new CornerRadius(3),
                ToolTip = "Estimated RAM usage after loading selected stations",
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = Color.FromRgb(0, 240, 255),
                    BlurRadius = 20,
                    Opacity = 1.0,
                    ShadowDepth = 0
                }
            };
            ramBarOuter.Children.Add(_ramEstimatedMarker);

            ramBarOuter.SizeChanged += (s, e) => RepositionRamMarkers(ramBarOuter);

            return ramBarOuter;
        }

        private void RepositionRamMarkers(Grid ramBarOuter)
        {
            if (ramBarOuter.ActualWidth <= 0) return;

            if (_ramEstimatedMarker?.Tag is double pct)
                _ramEstimatedMarker.Margin = new Thickness(Math.Max(0, pct * ramBarOuter.ActualWidth - 5.0), 0, 0, 0);

            if (_ramEstimatedFill?.Tag is double[] fillData)
            {
                _ramEstimatedFill.Margin = new Thickness(fillData[0] * ramBarOuter.ActualWidth, 0, 0, 0);
                _ramEstimatedFill.Width = Math.Max(0, (fillData[1] - fillData[0]) * ramBarOuter.ActualWidth);
            }
        }

        private StackPanel BuildMemoryLegend(LinearGradientBrush ramFgBrush, long usedMb)
        {
            StackPanel legend = new StackPanel
                { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };

            legend.Children.Add(new Border
            {
                Width = 14,
                Height = 14,
                Background = ramFgBrush,
                CornerRadius = new CornerRadius(3),
                Margin = new Thickness(0, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center
            });

            legend.Children.Add(new TextBlock
            {
                Text = "Current usage:  " + FormatMb(usedMb),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(140, 190, 155)),
                Margin = new Thickness(0, 0, 16, 0),
                VerticalAlignment = VerticalAlignment.Center
            });

            legend.Children.Add(new Border
            {
                Width = 5,
                Height = 14,
                Background = new SolidColorBrush(Color.FromRgb(0, 220, 255)),
                CornerRadius = new CornerRadius(2),
                Margin = new Thickness(0, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center
            });

            _ramEstimatedLabel = new TextBlock
            {
                Text = "Estimated after load:  —",
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(0, 200, 230)),
                VerticalAlignment = VerticalAlignment.Center
            };
            legend.Children.Add(_ramEstimatedLabel);

            return legend;
        }

        private UIElement BuildMessageTypeSection()
        {
            Border border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(5, 18, 9)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(22, 70, 36)),
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(20, 10, 20, 10)
            };
            StackPanel stack = new StackPanel();

            stack.Children.Add(new TextBlock
            {
                Text = "Message types to include:",
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(120, 170, 135)),
                Margin = new Thickness(0, 0, 0, 6)
            });

            WrapPanel wrapPanel = new WrapPanel { Orientation = Orientation.Horizontal };
            foreach (MessageType msgType in AllMessageTypes)
            {
                CheckBox cb = new CheckBox { IsChecked = true, Margin = new Thickness(0, 0, 16, 4) };
                cb.Content = new TextBlock
                {
                    Text = msgType.ToString().Replace("REQ_", "").Replace("_", " "),
                    FontSize = 10,
                    Foreground = new SolidColorBrush(Color.FromRgb(150, 200, 165))
                };
                _messageTypeCheckboxes[msgType] = cb;
                wrapPanel.Children.Add(cb);
            }

            stack.Children.Add(wrapPanel);
            border.Child = stack;
            return border;
        }

        private static Button BuildLoadButton()
        {
            return new Button
            {
                Content = "Load →",
                Padding = new Thickness(22, 8, 22, 8),
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Background = new SolidColorBrush(Color.FromRgb(22, 100, 50)),
                Foreground = new SolidColorBrush(Color.FromRgb(180, 245, 205)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(50, 180, 90)),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand
            };
        }

        private static Slider BuildSlider(int defaultIndex)
        {
            Slider slider = new Slider
            {
                Minimum = 0,
                Maximum = DayOptions.Length - 1,
                Value = defaultIndex,
                TickFrequency = 1,
                IsSnapToTickEnabled = true,
                SmallChange = 1,
                LargeChange = 1,
                VerticalAlignment = VerticalAlignment.Center
            };

            if (Application.Current?.Resources["MesRangeSlider"] is Style sliderStyle)
                slider.Style = sliderStyle;

            return slider;
        }

        private static UIElement WrapSliderWithLabels(Slider slider)
        {
            StackPanel outerStack = new StackPanel();
            outerStack.Children.Add(slider);
            outerStack.Children.Add(BuildSliderTickRow());
            return outerStack;
        }

        private static UIElement BuildSliderTickRow()
        {
            Canvas tickPanel = new Canvas { Height = 18 };
            Color tickColor = Color.FromRgb(70, 110, 82);

            tickPanel.SizeChanged += (s, e) => RedrawSliderTicks(tickPanel, tickColor);

            return tickPanel;
        }

        private static void RedrawSliderTicks(Canvas tickPanel, Color tickColor)
        {
            tickPanel.Children.Clear();
            double w = tickPanel.ActualWidth;
            if (w <= 0) return;

            double sliderPad = 8.0;
            double usableW = w - sliderPad * 2;
            int total = DayOptions.Length - 1;

            for (int idx = 0; idx < DayOptions.Length; idx++)
            {
                double pct = (double)idx / total;
                double x = sliderPad + pct * usableW;

                System.Windows.Shapes.Rectangle tick = new System.Windows.Shapes.Rectangle
                {
                    Width = 1,
                    Height = 5,
                    Fill = new SolidColorBrush(tickColor),
                    Opacity = 0.6
                };
                Canvas.SetLeft(tick, x - 0.5);
                Canvas.SetTop(tick, 0);
                tickPanel.Children.Add(tick);

                if (!MilestoneTickIndices.Contains(idx)) continue;

                string label = DayOptions[idx] < 45 ? DayOptions[idx] + "d" : FormatDayCount(DayOptions[idx]);
                TextBlock lbl = new TextBlock
                {
                    Text = label,
                    FontSize = 9,
                    Foreground = new SolidColorBrush(tickColor),
                    Opacity = 0.85
                };
                lbl.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                Canvas.SetLeft(lbl, x - lbl.DesiredSize.Width / 2);
                Canvas.SetTop(lbl, 7);
                tickPanel.Children.Add(lbl);
            }
        }

        private void ToggleMaximize()
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }

        private UIElement BuildTitleBar()
        {
            Border bar = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(5, 18, 9)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(22, 70, 36)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Height = 38,
                Cursor = Cursors.Arrow
            };

            bar.MouseLeftButtonDown += (s, e) =>
            {
                if (e.ClickCount == 2)
                {
                    ToggleMaximize();
                    return;
                }

                WindowResizer.DragMove(this);
            };

            Grid row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            row.Children.Add(new TextBlock
            {
                Text = "⏳  Load Options",
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(210, 245, 220)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(16, 0, 0, 0)
            });

            StackPanel buttons = new StackPanel { Orientation = Orientation.Horizontal };
            buttons.Children.Add(MakeTitleBarButton("—", () => SystemCommands.MinimizeWindow(this)));
            buttons.Children.Add(MakeTitleBarButton("▢", ToggleMaximize));
            buttons.Children.Add(MakeTitleBarButton("✕", () => Close()));
            Grid.SetColumn(buttons, 1);
            row.Children.Add(buttons);

            bar.Child = row;
            return bar;
        }

        private Border MakeTitleBarButton(string glyph, Action onClick)
        {
            TextBlock text = new TextBlock
            {
                Text = glyph,
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(180, 210, 190)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            Border btn = new Border
            {
                Width = 44,
                Height = 38,
                Background = Brushes.Transparent,
                Cursor = Cursors.Hand,
                Child = text
            };

            btn.MouseLeftButtonDown += (s, e) => e.Handled = true;
            btn.MouseLeftButtonUp += (s, e) =>
            {
                e.Handled = true;
                onClick();
            };
            btn.MouseEnter += (s, e) => btn.Background = new SolidColorBrush(Color.FromRgb(30, 60, 38));
            btn.MouseLeave += (s, e) => btn.Background = Brushes.Transparent;

            return btn;
        }

        private UIElement BuildResizeOverlay()
        {
            Grid overlay = new Grid();

            const double edge = 5;
            const double corner = 10;

            overlay.Children.Add(MakeResizeZone(edge, double.NaN, HorizontalAlignment.Left, VerticalAlignment.Stretch,
                Cursors.SizeWE, () => WindowResizer.ResizeLeft(this)));
            overlay.Children.Add(MakeResizeZone(edge, double.NaN, HorizontalAlignment.Right, VerticalAlignment.Stretch,
                Cursors.SizeWE, () => WindowResizer.ResizeRight(this)));
            overlay.Children.Add(MakeResizeZone(double.NaN, edge, HorizontalAlignment.Stretch, VerticalAlignment.Top,
                Cursors.SizeNS, () => WindowResizer.ResizeTop(this)));
            overlay.Children.Add(MakeResizeZone(double.NaN, edge, HorizontalAlignment.Stretch, VerticalAlignment.Bottom,
                Cursors.SizeNS, () => WindowResizer.ResizeBottom(this)));
            overlay.Children.Add(MakeResizeZone(corner, corner, HorizontalAlignment.Left, VerticalAlignment.Top,
                Cursors.SizeNWSE, () => WindowResizer.ResizeTopLeft(this)));
            overlay.Children.Add(MakeResizeZone(corner, corner, HorizontalAlignment.Right, VerticalAlignment.Top,
                Cursors.SizeNESW, () => WindowResizer.ResizeTopRight(this)));
            overlay.Children.Add(MakeResizeZone(corner, corner, HorizontalAlignment.Left, VerticalAlignment.Bottom,
                Cursors.SizeNESW, () => WindowResizer.ResizeBottomLeft(this)));
            overlay.Children.Add(MakeResizeZone(corner, corner, HorizontalAlignment.Right, VerticalAlignment.Bottom,
                Cursors.SizeNWSE, () => WindowResizer.ResizeBottomRight(this)));

            return overlay;
        }

        private static Border MakeResizeZone(double width, double height, HorizontalAlignment ha, VerticalAlignment va,
            Cursor cursor, Action onDown)
        {
            Border zone = new Border
            {
                Width = width,
                Height = height,
                HorizontalAlignment = ha,
                VerticalAlignment = va,
                Background = Brushes.Transparent,
                Cursor = cursor
            };
            zone.MouseLeftButtonDown += (s, e) => onDown();
            return zone;
        }

        private static UIElement BuildHeader()
        {
            Border border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(5, 18, 9)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(22, 70, 36)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(24, 16, 24, 16)
            };
            StackPanel stack = new StackPanel();
            stack.Children.Add(new TextBlock
            {
                Text = "Select stations and how much historical data to load.",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(90, 140, 105)),
                TextWrapping = TextWrapping.Wrap
            });
            border.Child = stack;
            return border;
        }

        private static UIElement BuildFooter(CheckBox cbDateFilter, Button btnLoad, Action onCancel)
        {
            Border border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(5, 18, 9)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(22, 70, 36)),
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(20, 12, 20, 12)
            };
            Grid row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            cbDateFilter.VerticalAlignment = VerticalAlignment.Center;
            cbDateFilter.Content = new TextBlock
            {
                Text = "Apply date range filter",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(150, 200, 165))
            };
            row.Children.Add(cbDateFilter);

            StackPanel btnRow = new StackPanel
                { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            Grid.SetColumn(btnRow, 1);

            Button btnCancel = new Button
            {
                Content = "Cancel",
                Padding = new Thickness(18, 8, 18, 8),
                Margin = new Thickness(0, 0, 10, 0),
                FontSize = 12,
                Background = new SolidColorBrush(Color.FromRgb(18, 36, 22)),
                Foreground = new SolidColorBrush(Color.FromRgb(130, 160, 135)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(36, 70, 44)),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand
            };
            btnCancel.Click += (s, e) => onCancel();
            btnRow.Children.Add(btnCancel);
            btnRow.Children.Add(btnLoad);
            row.Children.Add(btnRow);

            border.Child = row;
            return border;
        }
    }
}