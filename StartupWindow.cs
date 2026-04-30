using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using MESInsight.Core;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using RTAnalyzer.Core;
using System.Runtime.InteropServices;

namespace MESInsight
{
    public class StartupWindow : Window
    {
        public string SelectedPath { get; private set; }
        public StartupMode Mode { get; private set; }

        private static readonly Color BgColor = Color.FromRgb(22, 80, 45);
        private static readonly Color HexFill = Color.FromRgb(216, 115, 18);
        private static readonly Color HexHover = Color.FromRgb(240, 161, 48);
        private static readonly Color HexStroke = Color.FromRgb(22, 80, 45);
        private static readonly Color TextLight = Color.FromRgb(255, 245, 230);
        private static readonly Color TextSub = Color.FromRgb(255, 210, 160);

        private static readonly string DefaultRemotePath =
            @"\\vt1.vitesco.com\fs\didv0952\06_MES_App_Logs";

        private static string ResolveRemotePath()
        {
            if (Directory.Exists(DefaultRemotePath))
                return DefaultRemotePath;

            string tail = System.IO.Path.Combine("didv0952", "06_MES_App_Logs");

            foreach (char drive in new[] { 'F', 'T', 'Z', 'Y', 'X', 'W', 'V', 'S', 'R', 'Q' })
            {
                string candidate = drive + ":\\" + tail;
                if (Directory.Exists(candidate))
                    return candidate;
            }

            return DefaultRemotePath;
        }

        private static readonly string SampleDataPath = FindSampleDataPath();

        private static string FindSampleDataPath()
        {
            string dir = System.IO.Path.GetDirectoryName(
                System.Reflection.Assembly.GetExecutingAssembly().Location) ?? "";

            for (int i = 0; i < 5; i++)
            {
                string candidate = System.IO.Path.Combine(dir, "SampleData");
                if (Directory.Exists(candidate)) return candidate;
                string parent = System.IO.Path.GetDirectoryName(dir);
                if (string.IsNullOrEmpty(parent)) break;
                dir = parent;
            }

            return System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(
                    System.Reflection.Assembly.GetExecutingAssembly().Location) ?? "",
                "SampleData");
        }

        public StartupWindow()
        {
            Title = "MES Insight";
            Width = 1000;
            Height = 860;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = new SolidColorBrush(BgColor);
            Content = BuildLayout();
        }

        private UIElement BuildLayout()
        {
            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(60) });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(34) });

            var header = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(14, 55, 28)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(180, 80, 10)),
                BorderThickness = new Thickness(0, 0, 0, 5)
            };
            var hStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(28, 0, 0, 0)
            };
            hStack.Children.Add(new TextBlock
            {
                Text = "📊",
                FontSize = 32,
                Foreground = new SolidColorBrush(HexFill),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            });
            var ts = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            ts.Children.Add(new TextBlock
            {
                Text = "MES Insight",
                FontSize = 20,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(255, 240, 220))
            });
            ts.Children.Add(new TextBlock
            {
                Text = "Manufacturing Execution System  |  Diagnostics & Analytics",
                FontSize = 15,
                Foreground = new SolidColorBrush(Color.FromRgb(180, 120, 60)),
                Margin = new Thickness(0, 1, 0, 0)
            });
            hStack.Children.Add(ts);
            header.Child = hStack;
            Grid.SetRow(header, 0);
            root.Children.Add(header);

            const double r = 100;
            const double gap = 5;
            double W = Math.Sqrt(3) * r;
            double H = 2 * r;
            double stepX = W + gap;
            double stepY = H * 0.75 + gap;
            double rowOffset = stepX / 2.0;

            var canvas = new Canvas
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            bool sampleOk = Directory.Exists(SampleDataPath);

            AddHex(canvas, W, H, r, "📂", "LOCAL FOLDER", "Local or network path", 0 * stepX, 0, false);
            AddHex(canvas, W, H, r, "🌐", "REMOTE BACKUP LOGS", "MES Backup disc access needed", 1 * stepX, 0, false);
            AddHex(canvas, W, H, r, "🧪", "SAMPLE DATA", sampleOk ? "Demo data ready" : "Not available", 2 * stepX, 0,
                !sampleOk);
            AddHex(canvas, W, H, r, "↻", "RECENT DATA", "Last 10 loaded stations", rowOffset + 0 * stepX, stepY, false);
            AddHex(canvas, W, H, r, "✕", "EXIT", "Close application", rowOffset + 1 * stepX, stepY, false,
                isExit: true);

            canvas.Width = 3 * stepX - gap + 0.1;
            canvas.Height = stepY + H;

            Grid.SetRow(canvas, 1);
            root.Children.Add(canvas);

            var footer = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(14, 55, 28)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(180, 80, 10)),
                BorderThickness = new Thickness(0, 5, 0, 0)
            };

            var footerGrid = new Grid { Margin = new Thickness(28, 0, 28, 0) };
            footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var leftText = new TextBlock
            {
                Text = "MES Insight v1.0 | © 2026",
                FontSize = 15,
                Foreground = new SolidColorBrush(Color.FromRgb(200, 130, 60)),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            Grid.SetColumn(leftText, 0);

            var rightText = new TextBlock
            {
                Text = "Author: Lukas Paucin | lukas.paucin@mail.schaefller.com",
                FontSize = 15,
                Foreground = new SolidColorBrush(Color.FromRgb(200, 130, 60)),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right,
                TextAlignment = TextAlignment.Right
            };
            Grid.SetColumn(rightText, 1);

            footerGrid.Children.Add(leftText);
            footerGrid.Children.Add(rightText);
            footer.Child = footerGrid;
            Grid.SetRow(footer, 2);
            root.Children.Add(footer);

            return root;
        }

        private void AddHex(Canvas canvas,
            double W, double H, double r,
            string icon, string title, string sub,
            double left, double top,
            bool disabled, bool isExit = false)
        {
            var grid = new Grid
            {
                Width = W,
                Height = H,
                Cursor = disabled ? Cursors.Arrow : Cursors.Hand,
                Opacity = disabled ? 0.38 : 1.0
            };

            double cx = W / 2;
            double cy = H / 2;

            var outer = new Polygon
            {
                Fill = new SolidColorBrush(HexFill),
                Stroke = new SolidColorBrush(HexFill),
                StrokeThickness = 0.3
            };

            double rInner = r * 0.93;
            var inner = new Polygon
            {
                Fill = new SolidColorBrush(HexFill),
                Stroke = new SolidColorBrush(HexStroke),
                StrokeThickness = 5
            };

            for (int i = 0; i < 6; i++)
            {
                double angle = Math.PI / 180.0 * (60 * i - 90);
                outer.Points.Add(new Point(cx + r * Math.Cos(angle), cy + r * Math.Sin(angle)));
                inner.Points.Add(new Point(cx + rInner * Math.Cos(angle), cy + rInner * Math.Sin(angle)));
            }

            grid.Children.Add(outer);
            grid.Children.Add(inner);

            var stack = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            stack.Children.Add(new TextBlock
            {
                Text = icon,
                FontSize = 32,
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = new SolidColorBrush(TextLight),
                Margin = new Thickness(0, 0, 0, 6)
            });
            stack.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(TextLight),
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 0, 0, 3)
            });
            stack.Children.Add(new TextBlock
            {
                Text = sub,
                FontSize = 10,
                Foreground = new SolidColorBrush(TextSub),
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = W - 50
            });

            grid.Children.Add(stack);

            if (!disabled)
            {
                grid.MouseEnter += (s, e) =>
                {
                    inner.Fill = new SolidColorBrush(HexHover);
                    outer.Fill = new SolidColorBrush(HexHover);
                };
                grid.MouseLeave += (s, e) =>
                {
                    inner.Fill = new SolidColorBrush(HexFill);
                    outer.Fill = new SolidColorBrush(HexFill);
                };

                string capturedTitle = title;
                bool capturedExit = isExit;
                grid.MouseLeftButtonUp += (s, e) => HandleClick(capturedTitle, capturedExit);
            }

            Canvas.SetLeft(grid, left);
            Canvas.SetTop(grid, top);
            canvas.Children.Add(grid);
        }

        private void HandleClick(string title, bool isExit)
        {
            if (isExit)
            {
                Application.Current.Shutdown();
                return;
            }

            switch (title)
            {
                case "LOCAL FOLDER": ShowPathDialog(isRemote: false); break;
                case "REMOTE BACKUP LOGS": ShowPathDialog(isRemote: true); break;
                case "SAMPLE DATA":
                    SelectedPath = SampleDataPath;
                    Mode = StartupMode.Sample;
                    SaveRecentPath(SelectedPath);
                    DialogResult = true;
                    break;
                case "RECENT DATA": ShowRecentMenu(); break;
            }
        }

        private void ShowPathDialog(bool isRemote)
        {
            string startPath = isRemote
                ? ResolveRemotePath()
                : Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

            Window spinner = null;

            if (isRemote)
            {
                spinner = BuildSpinnerWindow("Connecting to server...");
                spinner.Show();
                System.Windows.Forms.Application.DoEvents();
                System.Threading.Thread.Sleep(300);
            }

            while (true)
            {
                spinner?.Close();
                spinner = null;

                var browser = new System.Windows.Forms.FolderBrowserDialog
                {
                    Description = isRemote ? "Select a specific station folder (e.g. OHD0179N)" : "Select logs folder",
                    SelectedPath = startPath,
                    ShowNewFolderButton = false
                };

                if (browser.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

                string chosen = browser.SelectedPath;

                if (IsRootBackupFolder(chosen, isRemote))
                {
                    MessageBox.Show(
                        "Please select a specific station folder, not the root backup folder." +
                        "\n\nNavigate into a subfolder — for example select a specific computer name like OHD0179N.",
                        "Select a Specific Station",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    startPath = chosen;
                    continue;
                }

                SelectedPath = chosen;
                Mode = isRemote ? StartupMode.Remote : StartupMode.Local;
                SaveRecentPath(SelectedPath);
                DialogResult = true;
                return;
            }
        }

        private bool IsRootBackupFolder(string path, bool isRemote)
        {
            if (!isRemote) return false;
            char[] sep = new char[] { (char)92, (char)47 };
            string resolved = ResolveRemotePath().TrimEnd(sep);
            string chosen = path.TrimEnd(sep);
            return string.Equals(chosen, resolved, StringComparison.OrdinalIgnoreCase);
        }

        private Window BuildSpinnerWindow(string message)
        {
            var win = new Window
            {
                Width = 260,
                Height = 90,
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = new SolidColorBrush(Color.FromArgb(230, 10, 28, 16)),
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                Topmost = true
            };

            var border = new Border
            {
                BorderBrush = new SolidColorBrush(Color.FromRgb(180, 80, 10)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Child = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(20, 0, 20, 0)
                }
            };

            var stack = (StackPanel)border.Child;
            var spin = new TextBlock
            {
                Text = "↻",
                FontSize = 22,
                Foreground = new SolidColorBrush(HexFill),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 12, 0)
            };

            stack.Children.Add(spin);
            stack.Children.Add(new TextBlock
            {
                Text = message,
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(220, 200, 170)),
                VerticalAlignment = VerticalAlignment.Center
            });

            win.Content = border;

            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(80)
            };
            int angle = 0;
            timer.Tick += (s, e) =>
            {
                angle = (angle + 30) % 360;
                spin.RenderTransform = new RotateTransform(angle, spin.ActualWidth / 2, spin.ActualHeight / 2);
            };
            timer.Start();
            win.Closed += (s, e) => timer.Stop();

            return win;
        }

        private static string RecentPathFile =>
            System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MESInsight", "recent.txt");

        private static void SaveRecentPath(string path)
        {
            try
            {
                string dir = System.IO.Path.GetDirectoryName(RecentPathFile) ?? "";
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                var paths = LoadRecentPaths();
                paths.Remove(path);
                paths.Insert(0, path);
                if (paths.Count > 10) paths = paths.Take(10).ToList();

                File.WriteAllLines(RecentPathFile, paths);
            }
            catch
            {
            }
        }

        private static List<string> LoadRecentPaths()
        {
            try
            {
                if (!File.Exists(RecentPathFile)) return new List<string>();
                return File.ReadAllLines(RecentPathFile)
                    .Where(l => !string.IsNullOrWhiteSpace(l))
                    .Distinct()
                    .ToList();
            }
            catch
            {
                return new List<string>();
            }
        }

        private void ShowRecentMenu()
        {
            var paths = LoadRecentPaths();

            if (paths.Count == 0)
            {
                MessageBox.Show(
                    "No recent data found." + Environment.NewLine +
                    "Load a folder first and it will appear here.",
                    "Recent Data",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            var dlg = new RecentDataDialog(paths) { Owner = this };

            if (dlg.ShowDialog() == true && !string.IsNullOrEmpty(dlg.SelectedPath))
            {
                SelectedPath = dlg.SelectedPath;
                Mode = StartupMode.Local;
                DialogResult = true;
            }
        }
    }

    public class RecentDataDialog : Window
    {
        public string SelectedPath { get; private set; }

        public RecentDataDialog(List<string> paths)
        {
            Title = "Recent Data";
            Width = 560;
            SizeToContent = SizeToContent.Height;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = new SolidColorBrush(Color.FromRgb(8, 14, 10));

            var root = new StackPanel { Margin = new Thickness(20, 16, 20, 16) };

            root.Children.Add(new TextBlock
            {
                Text = "Recent Data",
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(210, 245, 220)),
                Margin = new Thickness(0, 0, 0, 12)
            });

            foreach (string path in paths)
            {
                string captured = path;
                bool exists = Directory.Exists(path);

                var row = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(12, 26, 16)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(26, 70, 38)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(5),
                    Padding = new Thickness(12, 8, 12, 8),
                    Margin = new Thickness(0, 0, 0, 5),
                    Cursor = exists ? Cursors.Hand : Cursors.Arrow
                };

                var stack = new StackPanel();
                stack.Children.Add(new TextBlock
                {
                    Text = System.IO.Path.GetFileName(path.TrimEnd((char)92, '/')),
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                    Foreground =
                        new SolidColorBrush(exists ? Color.FromRgb(180, 230, 195) : Color.FromRgb(100, 110, 100)),
                    TextWrapping = TextWrapping.NoWrap
                });
                stack.Children.Add(new TextBlock
                {
                    Text = path,
                    FontSize = 9,
                    FontFamily = new FontFamily("Consolas"),
                    Foreground = new SolidColorBrush(exists ? Color.FromRgb(70, 120, 85) : Color.FromRgb(80, 80, 80)),
                    TextWrapping = TextWrapping.NoWrap
                });

                if (!exists)
                    stack.Children.Add(new TextBlock
                    {
                        Text = "⚠  Path not accessible",
                        FontSize = 9,
                        Foreground = new SolidColorBrush(Color.FromRgb(160, 120, 50))
                    });

                row.Child = stack;

                if (exists)
                {
                    row.MouseEnter += (s, e) => row.Background = new SolidColorBrush(Color.FromRgb(18, 45, 24));
                    row.MouseLeave += (s, e) => row.Background = new SolidColorBrush(Color.FromRgb(12, 26, 16));
                    row.MouseLeftButtonUp += (s, e) =>
                    {
                        SelectedPath = captured;
                        DialogResult = true;
                    };
                }

                root.Children.Add(row);
            }

            var btnCancel = new Button
            {
                Content = "Cancel",
                Padding = new Thickness(16, 7, 16, 7),
                Margin = new Thickness(0, 8, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Right,
                Background = new SolidColorBrush(Color.FromRgb(18, 36, 22)),
                Foreground = new SolidColorBrush(Color.FromRgb(130, 160, 135)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(36, 70, 44)),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand
            };
            btnCancel.Click += (s, e) => { DialogResult = false; };
            root.Children.Add(btnCancel);

            Content = root;
        }
    }

    public enum StartupMode
    {
        Local,
        Remote,
        Sample
    }

    public class StationTypeFilterDialog : Window
    {
        public bool IncludeLcs { get; private set; } = false;
        public bool IncludeBackflush { get; private set; } = false;

        public StationTypeFilterDialog(int lcsCount, int backflushCount)
        {
            Title = "Station Types Found";
            Width = 420;
            Height = 260;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = new SolidColorBrush(Color.FromRgb(8, 14, 10));

            var root = new StackPanel { Margin = new Thickness(24, 20, 24, 20) };

            root.Children.Add(new TextBlock
            {
                Text = "Additional station types detected",
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(210, 245, 220)),
                Margin = new Thickness(0, 0, 0, 6)
            });
            root.Children.Add(new TextBlock
            {
                Text = "Select which types to include in the analysis:",
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(110, 160, 125)),
                Margin = new Thickness(0, 0, 0, 18),
                TextWrapping = TextWrapping.Wrap
            });

            var cbLcs = new CheckBox
            {
                Content = "LCS  (" + lcsCount + " station" + (lcsCount != 1 ? "s" : "") + ")",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(180, 225, 195)),
                IsEnabled = lcsCount > 0,
                IsChecked = false,
                Margin = new Thickness(0, 0, 0, 10)
            };
            var cbBackflush = new CheckBox
            {
                Content = "Backflush  (" + backflushCount + " station" + (backflushCount != 1 ? "s" : "") + ")",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(180, 225, 195)),
                IsEnabled = backflushCount > 0,
                IsChecked = false,
                Margin = new Thickness(0, 0, 0, 24)
            };

            root.Children.Add(cbLcs);
            root.Children.Add(cbBackflush);

            var btnRow = new StackPanel
                { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var btnConfirm = new Button
            {
                Content = "Confirm →",
                Padding = new Thickness(18, 7, 18, 7),
                FontWeight = FontWeights.SemiBold,
                Background = new SolidColorBrush(Color.FromRgb(150, 85, 15)),
                Foreground = new SolidColorBrush(Color.FromRgb(255, 235, 180)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(210, 130, 30)),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand
            };
            btnConfirm.Click += (s, e) =>
            {
                IncludeLcs = cbLcs.IsChecked == true;
                IncludeBackflush = cbBackflush.IsChecked == true;
                DialogResult = true;
            };
            btnRow.Children.Add(btnConfirm);
            root.Children.Add(btnRow);

            Content = root;
        }
    }

    public class LoadOptionsDialog : Window
    {
        public bool FilterByDate { get; private set; } = true;
        public int MaxMonths { get; private set; } = 6;
        public bool IncludeLcs { get; private set; } = false;
        public bool IncludeBackflush { get; private set; } = false;
        public bool IncludeConnectors { get; private set; } = false;
        public List<string> ExcludedFolderPaths { get; private set; } = new List<string>();
        public List<string> LazyLoadFolderPaths { get; private set; } = new List<string>();
        public Dictionary<string, int> StationMonthOverrides { get; private set; } = new Dictionary<string, int>();
        public List<MessageType> EnabledMessageTypes { get; private set; } = new List<MessageType>();

        private static readonly int[] MonthOptions = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 };

        private const long OptimalSizeMb = 800;
        private const long GoodSizeMb = 2000;
        private const long WarningSizeMb = 4000;
        private const long CriticalSizeMb = 7000;

        private static readonly MessageType[] AllMessageTypes =
        {
            MessageType.UNIT_CHECKIN, MessageType.UNIT_RESULT, MessageType.UNIT_INFO,
            MessageType.NEXT_OPERATION, MessageType.LOAD_MATERIAL, MessageType.REQ_LOADED_MATERIAL,
            MessageType.REQ_MATERIAL_INFO, MessageType.REQ_SETUP_CHANGE2
        };

        private class StationLoadEntry
        {
            public StationInfo Station { get; set; }
            public CheckBox EnabledBox { get; set; }
            public Slider MonthSlider { get; set; }
        }

        // RAM label updated by timer — kept as field so timer can access it
        private TextBlock _ramValueLabel;
        private System.Windows.Threading.DispatcherTimer _ramTimer;

        public LoadOptionsDialog(
            List<StationInfo> ghpStations,
            List<StationInfo> lcsStations,
            List<StationInfo> backflushStations,
            List<StationInfo> connectorStations,
            Dictionary<int, MonthFileInfo> globalFileCounts = null)
        {
            Title = "Load Options";
            Width = 980;
            Height = 900;
            ResizeMode = ResizeMode.CanResize;
            MinWidth = 720;
            MinHeight = 640;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = new SolidColorBrush(Color.FromRgb(8, 14, 10));

            int recommendedMonths = CalculateRecommendedMonths(globalFileCounts);
            int recommendedIndex = Array.IndexOf(MonthOptions, recommendedMonths);
            if (recommendedIndex < 0) recommendedIndex = 5;

            var allEntries = new List<StationLoadEntry>();

            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.Children.Add(BuildHeader());

            var content = new StackPanel();

            TextBlock globalValueLabel, globalSizeLabel, globalWarningLabel;
            Slider globalSlider;
            content.Children.Add(BuildGlobalSliderSection(
                globalFileCounts, recommendedIndex, recommendedMonths,
                out globalSlider, out globalValueLabel, out globalSizeLabel, out globalWarningLabel));

            var cbGhp = AddStationSection(content, allEntries, "GHP Stations", ghpStations, Color.FromRgb(63, 185, 80),
                true, recommendedIndex);
            var cbLcs = AddStationSection(content, allEntries, "LCS Stations  ⚠ WIP", lcsStations,
                Color.FromRgb(80, 160, 220), false, recommendedIndex);
            var cbBfl = AddStationSection(content, allEntries, "Backflush Stations  ⚠ WIP", backflushStations,
                Color.FromRgb(220, 160, 60), false, recommendedIndex);
            var cbCon = AddStationSection(content, allEntries, "Connectors", connectorStations,
                Color.FromRgb(180, 120, 220), false, recommendedIndex);

            var scroll = new ScrollViewer
            {
                Content = content,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Padding = new Thickness(20, 14, 20, 8)
            };
            Grid.SetRow(scroll, 1);
            root.Children.Add(scroll);

            TextBlock totalSizeLabel, totalWarningLabel;
            ProgressBar loadBar;
            var indicator = BuildLoadIndicator(out loadBar, out totalSizeLabel, out totalWarningLabel);
            Grid.SetRow(indicator, 2);
            root.Children.Add(indicator);

            // Message type checkboxes
            var msgTypeSection = BuildMessageTypeSection();
            Grid.SetRow(msgTypeSection, 3);
            root.Children.Add(msgTypeSection);

            var btnLoad = BuildLoadButton();
            var cbDateFilter = new CheckBox { IsChecked = true };

            btnLoad.Click += (s, e) =>
            {
                int idx = (int)Math.Round(globalSlider.Value);
                FilterByDate = cbDateFilter.IsChecked == true;
                MaxMonths = MonthOptions[idx];
                IncludeLcs = cbLcs?.IsChecked == true;
                IncludeBackflush = cbBfl?.IsChecked == true;
                IncludeConnectors = cbCon?.IsChecked == true;

                ExcludedFolderPaths = new List<string>(); // all unchecked stations go to LazyLoad

                LazyLoadFolderPaths = allEntries
                    .Where(stEntry => stEntry.EnabledBox.IsChecked != true)
                    .Select(stEntry => stEntry.Station.FolderPath)
                    .ToList();

                StationMonthOverrides = new Dictionary<string, int>();
                foreach (var entry in allEntries)
                {
                    if (entry.EnabledBox.IsChecked != true) continue;
                    int stMonths = MonthOptions[(int)Math.Round(entry.MonthSlider.Value)];
                    if (stMonths != MaxMonths)
                        StationMonthOverrides[entry.Station.FolderPath] = stMonths;
                }

                EnabledMessageTypes = _messageTypeCheckboxes
                    .Where(kv => kv.Value.IsChecked == true)
                    .Select(kv => kv.Key)
                    .ToList();

                _ramTimer?.Stop();
                DialogResult = true;
            };

            var footer = BuildFooter(cbDateFilter, btnLoad, onCancel: () =>
            {
                _ramTimer?.Stop();
                DialogResult = false;
            });
            Grid.SetRow(footer, 4);
            root.Children.Add(footer);

            Content = root;

            Action recalculate = () => RecalculateTotalLoad(
                allEntries, globalSlider, globalFileCounts,
                loadBar, totalSizeLabel, totalWarningLabel, btnLoad);

            globalSlider.ValueChanged += (s, e) =>
            {
                UpdateSliderDisplay(globalSlider, globalValueLabel, globalSizeLabel,
                    globalWarningLabel, globalFileCounts, recommendedMonths);

                foreach (var entry in allEntries)
                    if ((int)Math.Round(entry.MonthSlider.Value) == (int)Math.Round(e.OldValue))
                        entry.MonthSlider.Value = globalSlider.Value;

                recalculate();
            };

            foreach (var entry in allEntries)
            {
                entry.EnabledBox.Checked += (s, e) => recalculate();
                entry.EnabledBox.Unchecked += (s, e) => recalculate();
                entry.MonthSlider.ValueChanged += (s, e) => recalculate();
            }

            UpdateSliderDisplay(globalSlider, globalValueLabel, globalSizeLabel,
                globalWarningLabel, globalFileCounts, recommendedMonths);
            recalculate();

            // RAM refresh every second
            _ramTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _ramTimer.Tick += (s, e) => UpdateRamLabel(recalculate);
            _ramTimer.Start();
            Closed += (s, e) => _ramTimer.Stop();
        }

        // ── RAM label refresh ─────────────────────────────────────────────────

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

            string availText = availMb >= 1024 ? (availMb / 1024.0).ToString("0.#") + " GB" : availMb + " MB";
            string totalText = totalRamMb >= 1024 ? (totalRamMb / 1024.0).ToString("0.#") + " GB" : totalRamMb + " MB";
            _ramValueLabel.Text = $"{availText}  /  {totalText} total";

            double usedRatio = 1.0 - (availMb / (double)Math.Max(1, totalRamMb));
            Color ramColor = usedRatio > 0.85
                ? Color.FromRgb(230, 100, 80)
                : usedRatio > 0.65
                    ? Color.FromRgb(210, 160, 50)
                    : Color.FromRgb(80, 185, 120);

            _ramValueLabel.Foreground = new SolidColorBrush(ramColor);
        }

        // ── RAM via Windows API ───────────────────────────────────────────────

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
                var status = new MEMORYSTATUSEX();
                if (GlobalMemoryStatusEx(status))
                    return (long)(status.ullAvailPhys / 1024 / 1024);
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
                var status = new MEMORYSTATUSEX();
                if (GlobalMemoryStatusEx(status))
                    return (long)(status.ullTotalPhys / 1024 / 1024);
            }
            catch
            {
            }

            return -1;
        }

        // ── Load status — single source of truth ──────────────────────────────

        private static (Color textColor, Color barColor, string statusText) GetLoadStatus(long sizeMb)
        {
            if (sizeMb >= CriticalSizeMb)
                return (Color.FromRgb(180, 30, 20), Color.FromRgb(180, 30, 20), "✕  Danger — very likely to crash");
            if (sizeMb >= WarningSizeMb)
                return (Color.FromRgb(220, 60, 40), Color.FromRgb(220, 60, 40), "⚠  Risk — may run out of memory");
            if (sizeMb >= GoodSizeMb)
                return (Color.FromRgb(220, 140, 30), Color.FromRgb(220, 140, 30), "⚠  Heavy — loading will be slow");
            if (sizeMb >= OptimalSizeMb) return (Color.FromRgb(160, 200, 60), Color.FromRgb(160, 200, 60), "✓  Good");
            return (Color.FromRgb(46, 185, 80), Color.FromRgb(46, 185, 80), "✓  Optimal");
        }

        private static int CalculateRecommendedMonths(Dictionary<int, MonthFileInfo> fileCounts)
        {
            if (fileCounts == null) return 6;
            foreach (int m in MonthOptions)
                if (fileCounts.TryGetValue(m, out var info) && info.SizeMb <= OptimalSizeMb)
                    return m;
            return MonthOptions[0];
        }

        // ── Recalculate load ──────────────────────────────────────────────────

        private static void RecalculateTotalLoad(
            List<StationLoadEntry> entries,
            Slider globalSlider,
            Dictionary<int, MonthFileInfo> globalFileCounts,
            ProgressBar loadBar,
            TextBlock totalSizeLabel,
            TextBlock totalWarningLabel,
            Button btnLoad)
        {
            var enabled = entries.Where(stEntry => stEntry.EnabledBox.IsChecked == true).ToList();

            if (enabled.Count == 0 || globalFileCounts == null)
            {
                loadBar.Value = 0;
                totalSizeLabel.Text = "No stations selected";
                totalWarningLabel.Text = "";
                btnLoad.IsEnabled = true;
                btnLoad.ToolTip = null;
                return;
            }

            int totalStations = entries.Count > 0 ? entries.Count : 1;
            long totalFileMb = 0;

            foreach (var entry in enabled)
            {
                int months = MonthOptions[(int)Math.Round(entry.MonthSlider.Value)];
                if (globalFileCounts.TryGetValue(months, out var info))
                    totalFileMb += info.SizeMb / totalStations;
            }

            long estimatedRamMb = totalFileMb * 4;
            long availableRamMb = GetAvailableRamMb();

            loadBar.Value = Math.Min(100, totalFileMb * 100.0 / CriticalSizeMb);

            var (textColor, barColor, statusText) = GetLoadStatus(totalFileMb);

            string sizeText = totalFileMb >= 1024
                ? (totalFileMb / 1024.0).ToString("0.#") + " GB  estimated"
                : totalFileMb + " MB  estimated";

            totalSizeLabel.Text = $"{enabled.Count} stations  ·  {sizeText}";
            totalSizeLabel.Foreground = new SolidColorBrush(textColor);
            loadBar.Foreground = new SolidColorBrush(barColor);

            btnLoad.IsEnabled = true;
            btnLoad.ToolTip = null;

            // Warn but never block — user decides
            bool ramLow = availableRamMb > 0 && estimatedRamMb > availableRamMb;

            if (ramLow)
            {
                totalWarningLabel.Text = "ℹ  Estimated RAM usage exceeds available — consider Lazy Load";
                totalWarningLabel.Foreground = new SolidColorBrush(Color.FromRgb(160, 160, 160));
            }
            else
            {
                totalWarningLabel.Text = statusText;
                totalWarningLabel.Foreground = new SolidColorBrush(textColor);
            }
        }

        // ── Global slider section ─────────────────────────────────────────────

        private static UIElement BuildGlobalSliderSection(
            Dictionary<int, MonthFileInfo> fileCounts,
            int defaultIndex, int recommendedMonths,
            out Slider slider, out TextBlock valueLabel,
            out TextBlock sizeLabel, out TextBlock warningLabel)
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(10, 22, 14)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(30, 80, 44)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(18, 14, 18, 14),
                Margin = new Thickness(0, 0, 0, 12)
            };

            var stack = new StackPanel();
            stack.Children.Add(new TextBlock
            {
                Text = "Default data range  —  applies to all stations",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(160, 210, 175)),
                Margin = new Thickness(0, 0, 0, 10)
            });

            slider = BuildSlider(defaultIndex);
            stack.Children.Add(WrapSliderWithLabels(slider));

            var infoRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 2) };
            valueLabel = new TextBlock { FontSize = 18, FontWeight = FontWeights.Bold };
            sizeLabel = new TextBlock
                { FontSize = 11, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(10, 0, 0, 2) };
            infoRow.Children.Add(valueLabel);
            infoRow.Children.Add(sizeLabel);
            stack.Children.Add(infoRow);

            if (fileCounts != null && fileCounts.TryGetValue(recommendedMonths, out var recInfo))
            {
                string recSize = recInfo.SizeMb >= 1024
                    ? (recInfo.SizeMb / 1024.0).ToString("0.#") + " GB"
                    : recInfo.SizeMb + " MB";
                string recText = recommendedMonths == 1 ? "1 month" : recommendedMonths + " months";

                var recRow = new StackPanel
                    { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 4) };
                recRow.Children.Add(new TextBlock
                {
                    Text = "Recommended: ", FontSize = 10, Foreground = new SolidColorBrush(Color.FromRgb(70, 110, 82))
                });
                recRow.Children.Add(new TextBlock
                {
                    Text = $"{recText}  ({recInfo.FileCount} files, {recSize})", FontSize = 10,
                    Foreground = new SolidColorBrush(Color.FromRgb(63, 185, 80))
                });
                stack.Children.Add(recRow);
            }

            warningLabel = new TextBlock
                { FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 3, 0, 0) };
            stack.Children.Add(warningLabel);

            border.Child = stack;
            return border;
        }

        private static void UpdateSliderDisplay(
            Slider slider, TextBlock valueLabel, TextBlock sizeLabel, TextBlock warningLabel,
            Dictionary<int, MonthFileInfo> fileCounts, int recommendedMonths)
        {
            int months = MonthOptions[(int)Math.Round(slider.Value)];
            valueLabel.Text = months == 1 ? "1 month" : months + " months";

            if (fileCounts == null || !fileCounts.TryGetValue(months, out var info))
            {
                sizeLabel.Text = "";
                warningLabel.Text = "";
                valueLabel.Foreground = new SolidColorBrush(Color.FromRgb(63, 185, 80));
                return;
            }

            string sizeText = info.SizeMb >= 1024
                ? (info.SizeMb / 1024.0).ToString("0.#") + " GB"
                : info.SizeMb + " MB";
            sizeLabel.Text = $"{info.FileCount} files  ·  {sizeText}";

            var (textColor, _, statusText) = GetLoadStatus(info.SizeMb);
            valueLabel.Foreground = new SolidColorBrush(textColor);
            sizeLabel.Foreground = new SolidColorBrush(textColor);
            warningLabel.Text = months <= recommendedMonths && info.SizeMb < OptimalSizeMb
                ? "✓  Recommended"
                : statusText;
            warningLabel.Foreground = new SolidColorBrush(textColor);
        }

        private static CheckBox AddStationSection(
            StackPanel parent, List<StationLoadEntry> allEntries,
            string title, List<StationInfo> stations,
            Color accentColor, bool defaultChecked, int defaultSliderIndex)
        {
            if (stations.Count == 0) return null;

            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(10, 22, 14)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(180, accentColor.R, accentColor.G, accentColor.B)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(16, 14, 16, 14),
                Margin = new Thickness(0, 0, 0, 10)
            };

            var stack = new StackPanel();
            var cbSection = BuildSectionHeaderCheckbox(stack, title, stations.Count, accentColor, defaultChecked);
            var sectionEntries = BuildSectionEntries(stack, allEntries, stations, cbSection, accentColor,
                defaultChecked, defaultSliderIndex);

            border.Child = stack;
            parent.Children.Add(border);
            return cbSection;
        }

// Builds the section title checkbox + lazy info label + check all buttons
        private static CheckBox BuildSectionHeaderCheckbox(
            StackPanel stack, string title, int stationCount,
            Color accentColor, bool defaultChecked)
        {
            var cbSection = new CheckBox { IsChecked = defaultChecked };
            var titlePanel = new StackPanel { Orientation = Orientation.Horizontal };
            titlePanel.Children.Add(new TextBlock
            {
                Text = title, FontSize = 15, FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(accentColor)
            });
            titlePanel.Children.Add(new TextBlock
            {
                Text = "  (" + stationCount + ")", FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 140, 112)),
                VerticalAlignment = VerticalAlignment.Center
            });
            cbSection.Content = titlePanel;
            stack.Children.Add(cbSection);

            var lazyInfoLabel = new TextBlock
            {
                Text = "⏳  Unchecked stations will be available for on-demand loading",
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(90, 110, 95)),
                Margin = new Thickness(0, 6, 0, 4),
                Visibility = Visibility.Collapsed
            };
            cbSection.Unchecked += (s, e) => lazyInfoLabel.Visibility = Visibility.Visible;
            cbSection.Checked += (s, e) => lazyInfoLabel.Visibility = Visibility.Collapsed;
            stack.Children.Add(lazyInfoLabel);

            stack.Children.Add(BuildCheckAllRow());

            return cbSection;
        }

// Builds Check All / Uncheck All buttons — wired up later after entries are created
        private static StackPanel BuildCheckAllRow()
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 4) };

            row.Children.Add(new Button
            {
                Content = "✓ Check All", FontSize = 10, Padding = new Thickness(8, 3, 8, 3),
                Margin = new Thickness(0, 0, 6, 0),
                Background = new SolidColorBrush(Color.FromRgb(14, 40, 20)),
                Foreground = new SolidColorBrush(Color.FromRgb(100, 185, 130)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(30, 80, 44)),
                BorderThickness = new Thickness(1), Cursor = Cursors.Hand,
                Tag = "CheckAll"
            });
            row.Children.Add(new Button
            {
                Content = "○ Uncheck All", FontSize = 10, Padding = new Thickness(8, 3, 8, 3),
                Background = new SolidColorBrush(Color.FromRgb(14, 40, 20)),
                Foreground = new SolidColorBrush(Color.FromRgb(120, 140, 125)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(30, 80, 44)),
                BorderThickness = new Thickness(1), Cursor = Cursors.Hand,
                Tag = "UncheckAll"
            });

            return row;
        }

// Creates all station entries and adds the station grid to stack
        private static List<StationLoadEntry> BuildSectionEntries(
            StackPanel stack, List<StationLoadEntry> allEntries,
            List<StationInfo> stations, CheckBox cbSection,
            Color accentColor, bool defaultChecked, int defaultSliderIndex)
        {
            var sectionEntries = new List<StationLoadEntry>();

            foreach (var st in stations)
            {
                var cb = new CheckBox { IsChecked = defaultChecked, IsEnabled = true };
                var slider = BuildSlider(defaultSliderIndex);
                slider.Width = 90;

                var entry = new StationLoadEntry { Station = st, EnabledBox = cb, MonthSlider = slider };
                allEntries.Add(entry);
                sectionEntries.Add(entry);

                cbSection.Checked += (s, e) => { cb.IsChecked = true; };
                cbSection.Unchecked += (s, e) => { cb.IsChecked = false; };
            }

            WireCheckAllButtons(stack, sectionEntries);
            AddStationGrid(stack, sectionEntries, accentColor, defaultChecked);

            return sectionEntries;
        }

// Wires the Check All / Uncheck All buttons to the section entries
        private static void WireCheckAllButtons(StackPanel stack, List<StationLoadEntry> sectionEntries)
        {
            var checkAllRow = stack.Children.OfType<StackPanel>()
                .FirstOrDefault(p => p.Children.OfType<Button>().Any(b => b.Tag?.ToString() == "CheckAll"));

            if (checkAllRow == null) return;

            var btnCheck = checkAllRow.Children.OfType<Button>().FirstOrDefault(b => b.Tag?.ToString() == "CheckAll");
            var btnUncheck = checkAllRow.Children.OfType<Button>()
                .FirstOrDefault(b => b.Tag?.ToString() == "UncheckAll");

            if (btnCheck != null)
                btnCheck.Click += (s, e) =>
                {
                    foreach (var ent in sectionEntries) ent.EnabledBox.IsChecked = true;
                };
            if (btnUncheck != null)
                btnUncheck.Click += (s, e) =>
                {
                    foreach (var ent in sectionEntries) ent.EnabledBox.IsChecked = false;
                };
        }

// Adds the 2-column station grid — GHP shows first 10 inline, rest in expander
        private static void AddStationGrid(
            StackPanel stack, List<StationLoadEntry> sectionEntries,
            Color accentColor, bool isGhp)
        {
            if (isGhp)
            {
                stack.Children.Add(new Border
                {
                    Child = BuildTwoColumnGrid(sectionEntries.Take(10).ToList(), accentColor),
                    Margin = new Thickness(0, 8, 0, 0)
                });

                if (sectionEntries.Count > 10)
                {
                    stack.Children.Add(new Expander
                    {
                        Header = $"Show {sectionEntries.Count - 10} more ▾",
                        IsExpanded = false,
                        Margin = new Thickness(0, 4, 0, 0),
                        FontSize = 11,
                        Foreground = new SolidColorBrush(Color.FromRgb(90, 150, 110)),
                        Content = BuildTwoColumnGrid(sectionEntries.Skip(10).ToList(), accentColor)
                    });
                }
            }
            else
            {
                stack.Children.Add(new Expander
                {
                    Header = "Choose stations ▾",
                    IsExpanded = false,
                    Margin = new Thickness(0, 6, 0, 0),
                    FontSize = 11,
                    Foreground = new SolidColorBrush(Color.FromRgb(90, 150, 110)),
                    Content = new Border
                    {
                        Child = BuildTwoColumnGrid(sectionEntries, accentColor),
                        Margin = new Thickness(0, 4, 0, 0)
                    }
                });
            }
        }

        private static UIElement BuildTwoColumnGrid(List<StationLoadEntry> entries, Color accentColor)
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            int rowCount = (entries.Count + 1) / 2;
            for (int r = 0; r < rowCount; r++)
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            for (int i = 0; i < entries.Count; i++)
            {
                int col = i % 2;
                int row = i / 2;
                var ui = BuildStationRowUI(entries[i], accentColor);
                Grid.SetColumn(ui, col);
                Grid.SetRow(ui, row);
                grid.Children.Add(ui);
            }

            return grid;
        }

        private static UIElement BuildStationRowUI(StationLoadEntry entry, Color accentColor)
        {
            var outer = new StackPanel { Margin = new Thickness(0, 3, 8, 3) };

            // Station name + checkbox row
            var nameRow = new Grid();
            nameRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            nameRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            entry.EnabledBox.Content = new TextBlock
            {
                Text = entry.Station.StationName,
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(175, 220, 190)),
                TextWrapping = TextWrapping.NoWrap,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            nameRow.Children.Add(entry.EnabledBox);

            // Month slider
            var monthLabel = new TextBlock
            {
                FontSize = 10,
                Width = 32,
                TextAlignment = TextAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 4, 0)
            };

            entry.MonthSlider.ValueChanged += (s, e) =>
            {
                int m = MonthOptions[(int)Math.Round(entry.MonthSlider.Value)];
                monthLabel.Text = m == 1 ? "1 mo" : m + " mo";
                var (color, _, _) = GetLoadStatus((long)m * 150);
                monthLabel.Foreground = new SolidColorBrush(color);
            };

            int initM = MonthOptions[(int)Math.Round(entry.MonthSlider.Value)];
            monthLabel.Text = initM == 1 ? "1 mo" : initM + " mo";
            monthLabel.Foreground = new SolidColorBrush(Color.FromRgb(100, 185, 130));

            var sliderRow = new StackPanel { Orientation = Orientation.Horizontal };

            sliderRow.Children.Add(monthLabel);
            sliderRow.Children.Add(entry.MonthSlider);

            Grid.SetColumn(sliderRow, 1);

            nameRow.Children.Add(sliderRow);
            outer.Children.Add(nameRow);

            return outer;
        }

        // ── Load indicator ────────────────────────────────────────────────────

        private UIElement BuildLoadIndicator(
            out ProgressBar loadBar,
            out TextBlock totalSizeLabel,
            out TextBlock totalWarningLabel)
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(5, 18, 9)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(22, 70, 36)),
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(20, 12, 20, 12)
            };

            var stack = new StackPanel();

            // Estimated load label
            var loadRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
            loadRow.Children.Add(new TextBlock
            {
                Text = "Estimated load:  ", FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(70, 110, 82)),
                VerticalAlignment = VerticalAlignment.Center
            });
            totalSizeLabel = new TextBlock { FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
            loadRow.Children.Add(totalSizeLabel);
            stack.Children.Add(loadRow);

            // Gradient progress bar — green to red spectrum, dimmed unfilled part
            var barContainer = new Grid { Height = 14, Margin = new Thickness(0, 0, 0, 6) };

            var bgBrush = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0) };
            var fgBrush = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0) };

            var spectrumStops = new (double stop, byte r, byte g, byte b)[]
            {
                (0.00, 46, 185, 80),
                (0.25, 160, 200, 60),
                (0.50, 220, 180, 30),
                (0.75, 220, 80, 40),
                (1.00, 160, 30, 20)
            };

            foreach (var (stop, r, g, b) in spectrumStops)
            {
                bgBrush.GradientStops.Add(new GradientStop(Color.FromArgb(45, r, g, b), stop));
                fgBrush.GradientStops.Add(new GradientStop(Color.FromRgb(r, g, b), stop));
            }

            var bgRect = new System.Windows.Shapes.Rectangle
            {
                Fill = bgBrush, RadiusX = 5, RadiusY = 5,
                HorizontalAlignment = HorizontalAlignment.Stretch, Height = 14
            };
            barContainer.Children.Add(bgRect);

            loadBar = new ProgressBar
            {
                Height = 14, Minimum = 0, Maximum = 100, Value = 0,
                Foreground = fgBrush,
                Background = System.Windows.Media.Brushes.Transparent,
                BorderThickness = new Thickness(0)
            };
            barContainer.Children.Add(loadBar);
            stack.Children.Add(barContainer);

            // Warning label
            totalWarningLabel = new TextBlock
            {
                FontSize = 11, Margin = new Thickness(0, 2, 0, 0), TextWrapping = TextWrapping.Wrap
            };
            stack.Children.Add(totalWarningLabel);

            // RAM row — dynamic, updated by timer
            long availMb = GetAvailableRamMb();
            long totalRamMb = GetTotalRamMb();

            var ramRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
            ramRow.Children.Add(new TextBlock
            {
                Text = "Available RAM:  ", FontSize = 13, FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(70, 110, 82)),
                VerticalAlignment = VerticalAlignment.Center
            });

            _ramValueLabel = new TextBlock
            {
                FontSize = 14, FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center
            };

            // Set initial value
            if (availMb < 0)
            {
                _ramValueLabel.Text = "unknown";
                _ramValueLabel.Foreground = new SolidColorBrush(Color.FromRgb(100, 110, 100));
            }
            else
            {
                string availText = availMb >= 1024 ? (availMb / 1024.0).ToString("0.#") + " GB" : availMb + " MB";
                string totalText = totalRamMb >= 1024
                    ? (totalRamMb / 1024.0).ToString("0.#") + " GB"
                    : totalRamMb + " MB";
                _ramValueLabel.Text = $"{availText}  /  {totalText} total";

                double usedRatio = 1.0 - (availMb / (double)Math.Max(1, totalRamMb));
                Color ramColor = usedRatio > 0.85
                    ? Color.FromRgb(230, 100, 80)
                    : usedRatio > 0.65
                        ? Color.FromRgb(210, 160, 50)
                        : Color.FromRgb(80, 185, 120);
                _ramValueLabel.Foreground = new SolidColorBrush(ramColor);
            }

            ramRow.Children.Add(_ramValueLabel);
            stack.Children.Add(ramRow);

            border.Child = stack;
            return border;
        }

        // ── Message type section ──────────────────────────────────────────────

        private Dictionary<MessageType, CheckBox> _messageTypeCheckboxes = new Dictionary<MessageType, CheckBox>();

        private UIElement BuildMessageTypeSection()
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(5, 18, 9)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(22, 70, 36)),
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(20, 10, 20, 10)
            };

            var stack = new StackPanel();
            stack.Children.Add(new TextBlock
            {
                Text = "Message types to include:",
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(120, 170, 135)),
                Margin = new Thickness(0, 0, 0, 6)
            });

            var wrapPanel = new WrapPanel { Orientation = Orientation.Horizontal };

            foreach (var msgType in AllMessageTypes)
            {
                var cb = new CheckBox
                {
                    IsChecked = true,
                    Margin = new Thickness(0, 0, 16, 4)
                };
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

        // ── Helpers ───────────────────────────────────────────────────────────

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
            return new Slider
            {
                Minimum = 0,
                Maximum = MonthOptions.Length - 1,
                Value = defaultIndex,
                TickFrequency = 1,
                IsSnapToTickEnabled = true,
                TickPlacement = System.Windows.Controls.Primitives.TickPlacement.BottomRight,
                SmallChange = 1,
                LargeChange = 1,
                VerticalAlignment = VerticalAlignment.Center
            };
        }

        private static UIElement WrapSliderWithLabels(Slider slider)
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var left = new TextBlock
            {
                Text = "1 mo", FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(70, 110, 82)),
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0)
            };
            var right = new TextBlock
            {
                Text = "12 mo", FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(70, 110, 82)),
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0)
            };

            Grid.SetColumn(slider, 1);
            Grid.SetColumn(right, 2);
            grid.Children.Add(left);
            grid.Children.Add(slider);
            grid.Children.Add(right);
            return grid;
        }

        private static UIElement BuildHeader()
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(5, 18, 9)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(22, 70, 36)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(24, 16, 24, 16)
            };
            var stack = new StackPanel();
            stack.Children.Add(new TextBlock
            {
                Text = "⏳  Load Options", FontSize = 17, FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(210, 245, 220))
            });
            stack.Children.Add(new TextBlock
            {
                Text = "Select stations and how much historical data to load.", FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(90, 140, 105)),
                Margin = new Thickness(0, 4, 0, 0), TextWrapping = TextWrapping.Wrap
            });
            border.Child = stack;
            return border;
        }

        private static UIElement BuildFooter(CheckBox cbDateFilter, Button btnLoad, Action onCancel)
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(5, 18, 9)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(22, 70, 36)),
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(20, 12, 20, 12)
            };

            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            cbDateFilter.VerticalAlignment = VerticalAlignment.Center;
            cbDateFilter.Content = new TextBlock
            {
                Text = "Apply date range filter", FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(150, 200, 165))
            };
            row.Children.Add(cbDateFilter);

            var btnRow = new StackPanel
                { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            Grid.SetColumn(btnRow, 1);

            var btnCancel = new Button
            {
                Content = "Cancel", Padding = new Thickness(18, 8, 18, 8), Margin = new Thickness(0, 0, 10, 0),
                FontSize = 12, Background = new SolidColorBrush(Color.FromRgb(18, 36, 22)),
                Foreground = new SolidColorBrush(Color.FromRgb(130, 160, 135)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(36, 70, 44)),
                BorderThickness = new Thickness(1), Cursor = Cursors.Hand
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