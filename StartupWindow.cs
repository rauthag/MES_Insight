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

namespace MESInsight
{
    public class StartupWindow : Window
    {
        public string SelectedPath { get; private set; }
        public StartupMode Mode { get; private set; }

        private static readonly Color BgColor = Color.FromRgb(22, 80, 45);
        private static readonly Color HexOuterFill = Color.FromRgb(8, 15, 22);
        private static readonly Color HexOuterStroke = Color.FromRgb(18, 28, 40);
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
            Height = 760;
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
            AddHex(canvas, W, H, r, "🧪", "SAMPLE DATA", sampleOk ? "Demo data ready" : "Not available",
                2 * stepX, 0, !sampleOk);
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

            double innerScale = 0.93;
            double rInner = r * innerScale;

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

                grid.MouseLeave += (s, e) => { inner.Fill = new SolidColorBrush(HexFill); };

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
                case "LOCAL FOLDER":
                    ShowPathDialog(isRemote: false);
                    break;

                case "REMOTE BACKUP LOGS":
                    ShowPathDialog(isRemote: true);
                    break;

                case "SAMPLE DATA":
                    SelectedPath = SampleDataPath;
                    Mode = StartupMode.Sample;
                    SaveRecentPath(SelectedPath);
                    DialogResult = true;
                    break;

                case "RECENT DATA":
                    ShowRecentMenu();
                    break;
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
                    Description = isRemote
                        ? "Select a specific station folder (e.g. OHD0179N)"
                        : "Select logs folder",
                    SelectedPath = startPath,
                    ShowNewFolderButton = false
                };

                if (browser.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                    return;

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

            var dlg = new RecentDataDialog(paths);
            dlg.Owner = this;

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
            Background = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(8, 14, 10));

            var root = new System.Windows.Controls.StackPanel { Margin = new System.Windows.Thickness(20, 16, 20, 16) };

            root.Children.Add(new System.Windows.Controls.TextBlock
            {
                Text = "Recent Data",
                FontSize = 13,
                FontWeight = System.Windows.FontWeights.SemiBold,
                Foreground = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(210, 245, 220)),
                Margin = new System.Windows.Thickness(0, 0, 0, 12)
            });

            foreach (string path in paths)
            {
                var captured = path;

                var row = new System.Windows.Controls.Border
                {
                    Background = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(12, 26, 16)),
                    BorderBrush = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(26, 70, 38)),
                    BorderThickness = new System.Windows.Thickness(1),
                    CornerRadius = new System.Windows.CornerRadius(5),
                    Padding = new System.Windows.Thickness(12, 8, 12, 8),
                    Margin = new System.Windows.Thickness(0, 0, 0, 5),
                    Cursor = System.Windows.Input.Cursors.Hand
                };

                bool exists = Directory.Exists(path);

                var stack = new System.Windows.Controls.StackPanel();
                stack.Children.Add(new System.Windows.Controls.TextBlock
                {
                    Text = System.IO.Path.GetFileName(path.TrimEnd((char)92, '/')),
                    FontSize = 11,
                    FontWeight = System.Windows.FontWeights.SemiBold,
                    Foreground = new System.Windows.Media.SolidColorBrush(
                        exists
                            ? System.Windows.Media.Color.FromRgb(180, 230, 195)
                            : System.Windows.Media.Color.FromRgb(100, 110, 100)),
                    TextWrapping = System.Windows.TextWrapping.NoWrap
                });
                stack.Children.Add(new System.Windows.Controls.TextBlock
                {
                    Text = path,
                    FontSize = 9,
                    FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                    Foreground = new System.Windows.Media.SolidColorBrush(
                        exists
                            ? System.Windows.Media.Color.FromRgb(70, 120, 85)
                            : System.Windows.Media.Color.FromRgb(80, 80, 80)),
                    TextWrapping = System.Windows.TextWrapping.NoWrap
                });

                if (!exists)
                    stack.Children.Add(new System.Windows.Controls.TextBlock
                    {
                        Text = "⚠  Path not accessible",
                        FontSize = 9,
                        Foreground = new System.Windows.Media.SolidColorBrush(
                            System.Windows.Media.Color.FromRgb(160, 120, 50))
                    });

                row.Child = stack;

                if (exists)
                {
                    row.MouseEnter += (s, e) => row.Background =
                        new System.Windows.Media.SolidColorBrush(
                            System.Windows.Media.Color.FromRgb(18, 45, 24));
                    row.MouseLeave += (s, e) => row.Background =
                        new System.Windows.Media.SolidColorBrush(
                            System.Windows.Media.Color.FromRgb(12, 26, 16));
                    row.MouseLeftButtonUp += (s, e) =>
                    {
                        SelectedPath = captured;
                        DialogResult = true;
                    };
                }

                root.Children.Add(row);
            }

            var btnCancel = new System.Windows.Controls.Button
            {
                Content = "Cancel",
                Padding = new System.Windows.Thickness(16, 7, 16, 7),
                Margin = new System.Windows.Thickness(0, 8, 0, 0),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(18, 36, 22)),
                Foreground = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(130, 160, 135)),
                BorderBrush = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(36, 70, 44)),
                BorderThickness = new System.Windows.Thickness(1),
                Cursor = System.Windows.Input.Cursors.Hand
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
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

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
        public int MaxMonths { get; private set; } = 3;
        public bool IncludeLcs { get; private set; } = false;
        public bool IncludeBackflush { get; private set; } = false;
        public bool IncludeConnectors { get; private set; } = false;
        public List<string> ExcludedFolderPaths { get; private set; } = new List<string>();
        public Dictionary<string, int> StationMonthOverrides { get; private set; } = new Dictionary<string, int>();

        private static readonly int[] MonthOptions = { 1, 2, 3, 6, 12, 24 };

        private const long OptimalSizeMb = 800;
        private const long GoodSizeMb = 2000;
        private const long WarningSizeMb = 4000;
        private const long CriticalSizeMb = 7000;

        private class StationLoadEntry
        {
            public StationInfo Station { get; set; }
            public CheckBox EnabledBox { get; set; }
            public Slider MonthSlider { get; set; }
        }

        public LoadOptionsDialog(
            List<StationInfo> ghpStations,
            List<StationInfo> lcsStations,
            List<StationInfo> backflushStations,
            List<StationInfo> connectorStations,
            Dictionary<int, MonthFileInfo> globalFileCounts = null)
        {
            Title = "Load Options";
            Width = 700;
            Height = 760;
            ResizeMode = ResizeMode.CanResize;
            MinWidth = 540;
            MinHeight = 520;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = new SolidColorBrush(Color.FromRgb(8, 14, 10));

            int recommendedMonths = CalculateRecommendedMonths(globalFileCounts);
            int recommendedIndex = Array.IndexOf(MonthOptions, recommendedMonths);
            if (recommendedIndex < 0) recommendedIndex = 2;

            var allEntries = new List<StationLoadEntry>();

            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            root.Children.Add(BuildHeader());

            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Padding = new Thickness(20, 14, 20, 8)
            };
            Grid.SetRow(scroll, 1);

            var content = new StackPanel();

            TextBlock globalValueLabel, globalSizeLabel, globalWarningLabel;
            Slider globalSlider;
            content.Children.Add(BuildGlobalSliderSection(
                globalFileCounts, recommendedIndex, recommendedMonths,
                out globalSlider, out globalValueLabel, out globalSizeLabel, out globalWarningLabel));

            var cbGhp = AddStationSection(content, allEntries, "GHP Stations", ghpStations, Color.FromRgb(63, 185, 80),
                true, recommendedIndex);
            var cbLcs = AddStationSection(content, allEntries, "LCS Stations", lcsStations, Color.FromRgb(80, 160, 220),
                false, recommendedIndex);
            var cbBfl = AddStationSection(content, allEntries, "Backflush Stations", backflushStations,
                Color.FromRgb(220, 160, 60), false, recommendedIndex);
            var cbCon = AddStationSection(content, allEntries, "Connectors", connectorStations,
                Color.FromRgb(180, 120, 220), false, recommendedIndex);

            scroll.Content = content;
            root.Children.Add(scroll);

            TextBlock totalSizeLabel, totalWarningLabel;
            ProgressBar loadBar;
            var indicator = BuildLoadIndicator(out loadBar, out totalSizeLabel, out totalWarningLabel);
            Grid.SetRow(indicator, 2);
            root.Children.Add(indicator);

            var cbDateFilter = new CheckBox { IsChecked = true };
            var footer = BuildFooter(cbDateFilter,
                onLoad: () =>
                {
                    int idx = (int)Math.Round(globalSlider.Value);
                    FilterByDate = cbDateFilter.IsChecked == true;
                    MaxMonths = MonthOptions[idx];
                    IncludeLcs = cbLcs?.IsChecked == true;
                    IncludeBackflush = cbBfl?.IsChecked == true;
                    IncludeConnectors = cbCon?.IsChecked == true;

                    ExcludedFolderPaths = allEntries
                        .Where(e => e.EnabledBox.IsChecked != true)
                        .Select(e => e.Station.FolderPath)
                        .ToList();

                    StationMonthOverrides = new Dictionary<string, int>();
                    foreach (var entry in allEntries)
                    {
                        if (entry.EnabledBox.IsChecked != true) continue;
                        int stMonths = MonthOptions[(int)Math.Round(entry.MonthSlider.Value)];
                        if (stMonths != MaxMonths)
                            StationMonthOverrides[entry.Station.FolderPath] = stMonths;
                    }

                    DialogResult = true;
                },
                onCancel: () => { DialogResult = false; });
            Grid.SetRow(footer, 3);
            root.Children.Add(footer);

            Content = root;

            Action recalculate = () => RecalculateTotalLoad(
                allEntries, globalSlider, globalFileCounts, loadBar, totalSizeLabel, totalWarningLabel);

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
        }

        // ── Single source of truth for load status colors and labels ──────────

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
            if (fileCounts == null) return 3;
            foreach (int m in MonthOptions)
                if (fileCounts.TryGetValue(m, out var info) && info.SizeMb <= OptimalSizeMb)
                    return m;
            return MonthOptions[0];
        }

        private static void RecalculateTotalLoad(
            List<StationLoadEntry> entries,
            Slider globalSlider,
            Dictionary<int, MonthFileInfo> globalFileCounts,
            ProgressBar loadBar,
            TextBlock totalSizeLabel,
            TextBlock totalWarningLabel)
        {
            var enabled = entries.Where(e => e.EnabledBox.IsChecked == true).ToList();

            if (enabled.Count == 0 || globalFileCounts == null)
            {
                loadBar.Value = 0;
                totalSizeLabel.Text = "No stations selected";
                totalWarningLabel.Text = "";
                return;
            }

            int total = entries.Count > 0 ? entries.Count : 1;
            long totalMb = 0;

            foreach (var entry in enabled)
            {
                int months = MonthOptions[(int)Math.Round(entry.MonthSlider.Value)];
                if (globalFileCounts.TryGetValue(months, out var info))
                    totalMb += info.SizeMb / total;
            }

            loadBar.Value = Math.Min(100, totalMb * 100.0 / CriticalSizeMb);

            var (textColor, barColor, statusText) = GetLoadStatus(totalMb);

            string sizeText = totalMb >= 1024
                ? (totalMb / 1024.0).ToString("0.#") + " GB  estimated"
                : totalMb + " MB  estimated";

            totalSizeLabel.Text = $"{enabled.Count} stations  ·  {sizeText}";
            totalSizeLabel.Foreground = new SolidColorBrush(textColor);
            loadBar.Foreground = new SolidColorBrush(barColor);
            totalWarningLabel.Text = statusText;
            totalWarningLabel.Foreground = new SolidColorBrush(textColor);
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

        // ── Station section ───────────────────────────────────────────────────

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
                Padding = new Thickness(16, 12, 16, 12),
                Margin = new Thickness(0, 0, 0, 10)
            };

            var stack = new StackPanel();

            var cbSection = new CheckBox { IsChecked = defaultChecked };
            var titlePanel = new StackPanel { Orientation = Orientation.Horizontal };
            titlePanel.Children.Add(new TextBlock
            {
                Text = title, FontSize = 13, FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(accentColor)
            });
            titlePanel.Children.Add(new TextBlock
            {
                Text = "  (" + stations.Count + ")", FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 140, 112)),
                VerticalAlignment = VerticalAlignment.Center
            });
            cbSection.Content = titlePanel;
            stack.Children.Add(cbSection);

            var expander = new Expander
            {
                Header = "Choose stations ▾", IsExpanded = false,
                Margin = new Thickness(24, 6, 0, 0), FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(90, 150, 110)),
                IsEnabled = defaultChecked
            };
            cbSection.Checked += (s, e) => expander.IsEnabled = true;
            cbSection.Unchecked += (s, e) => expander.IsEnabled = false;

            var stationList = new StackPanel { Margin = new Thickness(4, 4, 0, 0) };

            foreach (var st in stations)
            {
                var cb = new CheckBox { IsChecked = defaultChecked, IsEnabled = defaultChecked };
                var slider = BuildSlider(defaultSliderIndex);
                slider.Width = 100;

                var entry = new StationLoadEntry { Station = st, EnabledBox = cb, MonthSlider = slider };
                allEntries.Add(entry);

                cbSection.Checked += (s, e) =>
                {
                    cb.IsEnabled = true;
                    cb.IsChecked = true;
                };
                cbSection.Unchecked += (s, e) =>
                {
                    cb.IsEnabled = false;
                    cb.IsChecked = false;
                };

                stationList.Children.Add(BuildStationRowUI(entry));
            }

            expander.Content = stationList;
            stack.Children.Add(expander);
            border.Child = stack;
            parent.Children.Add(border);
            return cbSection;
        }

        private static UIElement BuildStationRowUI(StationLoadEntry entry)
        {
            var row = new Grid { Margin = new Thickness(0, 3, 0, 3) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });

            entry.EnabledBox.Content = new TextBlock
            {
                Text = entry.Station.StationName, FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(175, 220, 190)),
                TextWrapping = TextWrapping.NoWrap
            };
            row.Children.Add(entry.EnabledBox);

            var sliderPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(sliderPanel, 1);

            var monthLabel = new TextBlock
            {
                FontSize = 10, Width = 38, TextAlignment = TextAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0)
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

            sliderPanel.Children.Add(monthLabel);
            sliderPanel.Children.Add(entry.MonthSlider);
            row.Children.Add(sliderPanel);
            return row;
        }

        // ── Load indicator ────────────────────────────────────────────────────

        private static UIElement BuildLoadIndicator(
            out ProgressBar loadBar,
            out TextBlock totalSizeLabel,
            out TextBlock totalWarningLabel)
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(5, 18, 9)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(22, 70, 36)),
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(20, 10, 20, 10)
            };

            var stack = new StackPanel();

            var labelRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 5) };
            labelRow.Children.Add(new TextBlock
            {
                Text = "Estimated load:  ", FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(70, 110, 82))
            });
            totalSizeLabel = new TextBlock { FontSize = 11 };
            labelRow.Children.Add(totalSizeLabel);
            stack.Children.Add(labelRow);

            loadBar = new ProgressBar
            {
                Height = 8, Minimum = 0, Maximum = 100, Value = 0,
                Background = new SolidColorBrush(Color.FromRgb(20, 40, 26)),
                BorderThickness = new Thickness(0)
            };
            stack.Children.Add(loadBar);

            totalWarningLabel = new TextBlock
                { FontSize = 10, Margin = new Thickness(0, 4, 0, 0), TextWrapping = TextWrapping.Wrap };
            stack.Children.Add(totalWarningLabel);

            border.Child = stack;
            return border;
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static Slider BuildSlider(int defaultIndex)
        {
            return new Slider
            {
                Minimum = 0, Maximum = MonthOptions.Length - 1, Value = defaultIndex,
                TickFrequency = 1, IsSnapToTickEnabled = true,
                TickPlacement = System.Windows.Controls.Primitives.TickPlacement.BottomRight,
                SmallChange = 1, LargeChange = 1,
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
                Text = "1 mo", FontSize = 10, Foreground = new SolidColorBrush(Color.FromRgb(70, 110, 82)),
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0)
            };
            var right = new TextBlock
            {
                Text = "24 mo", FontSize = 10, Foreground = new SolidColorBrush(Color.FromRgb(70, 110, 82)),
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0)
            };

            Grid.SetColumn(slider, 1);
            Grid.SetColumn(right, 2);
            grid.Children.Add(left);
            grid.Children.Add(slider);
            grid.Children.Add(right);
            return grid;
        }

        // ── Header ────────────────────────────────────────────────────────────

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
                Text = "Load Options", FontSize = 17, FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(210, 245, 220))
            });
            stack.Children.Add(new TextBlock
            {
                Text = "Select stations and how much historical data to load.", FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(90, 140, 105)), Margin = new Thickness(0, 4, 0, 0),
                TextWrapping = TextWrapping.Wrap
            });
            border.Child = stack;
            return border;
        }

        // ── Footer ────────────────────────────────────────────────────────────

        private static UIElement BuildFooter(CheckBox cbDateFilter, Action onLoad, Action onCancel)
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
                BorderBrush = new SolidColorBrush(Color.FromRgb(36, 70, 44)), BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand
            };
            btnCancel.Click += (s, e) => onCancel();

            var btnLoad = new Button
            {
                Content = "Load →", Padding = new Thickness(22, 8, 22, 8), FontSize = 13,
                FontWeight = FontWeights.SemiBold, Background = new SolidColorBrush(Color.FromRgb(22, 100, 50)),
                Foreground = new SolidColorBrush(Color.FromRgb(180, 245, 205)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(50, 180, 90)), BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand
            };
            btnLoad.Click += (s, e) => onLoad();

            btnRow.Children.Add(btnCancel);
            btnRow.Children.Add(btnLoad);
            row.Children.Add(btnRow);
            border.Child = row;
            return border;
        }
    }
}