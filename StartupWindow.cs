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
        public bool IncludeLcs { get; private set; } = false;
        public bool IncludeBackflush { get; private set; } = false;
        public bool IncludeConnectors { get; private set; } = false;
        public bool FilterByDate { get; private set; } = false;
        public int MaxMonths { get; private set; } = 6;

        public System.Collections.Generic.List<string> ExcludedFolderPaths { get; private set; }
            = new System.Collections.Generic.List<string>();

        public LoadOptionsDialog(
            List<StationInfo> ghpStations,
            List<StationInfo> lcsStations,
            List<StationInfo> backflushStations,
            List<StationInfo> connectorStations,
            Dictionary<int, int> fileCounts = null)
        {
            Title = "Load Options";
            Width = 620;
            Height = 680;
            ResizeMode = ResizeMode.CanResize;
            MinWidth = 500;
            MinHeight = 480;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(8, 14, 10));

            var stationCheckboxes =
                new System.Collections.Generic.List<(System.Windows.Controls.CheckBox cb, string path)>();

            var root = new System.Windows.Controls.Grid();
            root.RowDefinitions.Add(new System.Windows.Controls.RowDefinition
                { Height = System.Windows.GridLength.Auto });
            root.RowDefinitions.Add(new System.Windows.Controls.RowDefinition
                { Height = new System.Windows.GridLength(1, System.Windows.GridUnitType.Star) });
            root.RowDefinitions.Add(new System.Windows.Controls.RowDefinition
                { Height = System.Windows.GridLength.Auto });

            // ── Header ──────────────────────────────────────────────────────
            var headerBorder = new System.Windows.Controls.Border
            {
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(5, 18, 9)),
                BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(22, 70, 36)),
                BorderThickness = new System.Windows.Thickness(0, 0, 0, 1),
                Padding = new System.Windows.Thickness(24, 16, 24, 16)
            };
            var headerStack = new System.Windows.Controls.StackPanel();
            headerStack.Children.Add(new System.Windows.Controls.TextBlock
            {
                Text = "Select stations to load",
                FontSize = 16,
                FontWeight = System.Windows.FontWeights.SemiBold,
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(210, 245, 220))
            });
            headerStack.Children.Add(new System.Windows.Controls.TextBlock
            {
                Text = "Choose which stations and data to include. Unchecked stations will be skipped.",
                FontSize = 11,
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(90, 140, 105)),
                TextWrapping = System.Windows.TextWrapping.Wrap,
                Margin = new System.Windows.Thickness(0, 4, 0, 0)
            });
            headerBorder.Child = headerStack;
            System.Windows.Controls.Grid.SetRow(headerBorder, 0);
            root.Children.Add(headerBorder);

            // ── Scrollable content ───────────────────────────────────────────
            var scroll = new System.Windows.Controls.ScrollViewer
            {
                VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Disabled,
                Padding = new System.Windows.Thickness(20, 12, 20, 8)
            };
            var content = new System.Windows.Controls.StackPanel { Margin = new System.Windows.Thickness(0) };

            // ── Date filter ──────────────────────────────────────────────────
            var dateBorder = new System.Windows.Controls.Border
            {
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(12, 26, 16)),
                BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 80, 44)),
                BorderThickness = new System.Windows.Thickness(1),
                CornerRadius = new System.Windows.CornerRadius(6),
                Padding = new System.Windows.Thickness(14, 10, 14, 10),
                Margin = new System.Windows.Thickness(0, 0, 0, 10)
            };
            var dateStack = new System.Windows.Controls.StackPanel();

            var cbDateFilter = new System.Windows.Controls.CheckBox
            {
                IsChecked = false,
                Margin = new System.Windows.Thickness(0, 0, 0, 0)
            };
            var dateLabel = new System.Windows.Controls.TextBlock
            {
                Text = "🗓  Skip records older than",
                FontSize = 13,
                FontWeight = System.Windows.FontWeights.SemiBold,
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(180, 220, 195))
            };

            var dateRow = new System.Windows.Controls.StackPanel
                { Orientation = System.Windows.Controls.Orientation.Horizontal };
            cbDateFilter.Content = dateLabel;
            dateRow.Children.Add(cbDateFilter);

            var monthsPanel = new System.Windows.Controls.StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                Margin = new System.Windows.Thickness(28, 6, 0, 0),
                IsEnabled = false
            };
            cbDateFilter.Checked += (s, e) => monthsPanel.IsEnabled = true;
            cbDateFilter.Unchecked += (s, e) => monthsPanel.IsEnabled = false;

            monthsPanel.Children.Add(new System.Windows.Controls.TextBlock
            {
                Text = "Load last",
                FontSize = 12,
                Foreground =
                    new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(150, 200, 165)),
                VerticalAlignment = System.Windows.VerticalAlignment.Center,
                Margin = new System.Windows.Thickness(0, 0, 8, 0)
            });

            var monthsCombo = new System.Windows.Controls.ComboBox
            {
                Width = 60,
                FontSize = 12,
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(14, 36, 20)),
                Foreground =
                    new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(180, 225, 195)),
                BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(40, 110, 60)),
                VerticalAlignment = System.Windows.VerticalAlignment.Center
            };
            foreach (int m in new[] { 1, 2, 3, 6, 12, 24 })
            {
                string label = m + " months";
                if (fileCounts != null && fileCounts.ContainsKey(m))
                    label += "  (~" + fileCounts[m] + " files)";
                monthsCombo.Items.Add(label);
                if (m == 6) monthsCombo.SelectedIndex = monthsCombo.Items.Count - 1;
            }

            monthsPanel.Children.Add(monthsCombo);
            monthsPanel.Children.Add(new System.Windows.Controls.TextBlock
            {
                Text = "months",
                FontSize = 12,
                Foreground =
                    new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(150, 200, 165)),
                VerticalAlignment = System.Windows.VerticalAlignment.Center,
                Margin = new System.Windows.Thickness(8, 0, 0, 0)
            });

            dateStack.Children.Add(dateRow);
            dateStack.Children.Add(monthsPanel);
            dateBorder.Child = dateStack;
            content.Children.Add(dateBorder);

            // ── Station section builder ──────────────────────────────────────
            System.Windows.Controls.CheckBox AddStationSection(
                string icon, string title,
                System.Windows.Media.Color accentColor,
                List<StationInfo> stations,
                bool defaultChecked)
            {
                if (stations.Count == 0) return null;

                var sectionBorder = new System.Windows.Controls.Border
                {
                    Background =
                        new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(12, 26, 16)),
                    BorderBrush = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromArgb(180, accentColor.R, accentColor.G, accentColor.B)),
                    BorderThickness = new System.Windows.Thickness(1),
                    CornerRadius = new System.Windows.CornerRadius(6),
                    Margin = new System.Windows.Thickness(0, 0, 0, 10),
                    Padding = new System.Windows.Thickness(14, 10, 14, 10)
                };

                var sectionStack = new System.Windows.Controls.StackPanel();

                // Header row
                var headerRow = new System.Windows.Controls.StackPanel
                {
                    Orientation = System.Windows.Controls.Orientation.Horizontal
                };

                var cbSection = new System.Windows.Controls.CheckBox
                {
                    IsChecked = defaultChecked,
                    Margin = new System.Windows.Thickness(0, 0, 0, 0)
                };

                var sectionTitle = new System.Windows.Controls.StackPanel
                {
                    Orientation = System.Windows.Controls.Orientation.Horizontal
                };
                sectionTitle.Children.Add(new System.Windows.Controls.TextBlock
                {
                    Text = icon + "  ",
                    FontSize = 15,
                    VerticalAlignment = System.Windows.VerticalAlignment.Center
                });
                sectionTitle.Children.Add(new System.Windows.Controls.TextBlock
                {
                    Text = title,
                    FontSize = 13,
                    FontWeight = System.Windows.FontWeights.SemiBold,
                    Foreground = new System.Windows.Media.SolidColorBrush(accentColor)
                });
                sectionTitle.Children.Add(new System.Windows.Controls.TextBlock
                {
                    Text = "  (" + stations.Count + ")",
                    FontSize = 11,
                    Foreground = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(100, 140, 112)),
                    VerticalAlignment = System.Windows.VerticalAlignment.Center
                });
                cbSection.Content = sectionTitle;
                sectionStack.Children.Add(cbSection);

                // Expander for station list
                var expander = new System.Windows.Controls.Expander
                {
                    Header = "Choose stations ▾",
                    IsExpanded = false,
                    Margin = new System.Windows.Thickness(22, 6, 0, 0),
                    FontSize = 10,
                    Foreground = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(90, 150, 110)),
                    IsEnabled = defaultChecked
                };

                cbSection.Checked += (s, e) => expander.IsEnabled = true;
                cbSection.Unchecked += (s, e) => expander.IsEnabled = false;

                var stationList = new System.Windows.Controls.StackPanel
                    { Margin = new System.Windows.Thickness(8, 4, 0, 0) };

                foreach (var st in stations)
                {
                    var cbSt = new System.Windows.Controls.CheckBox
                    {
                        IsChecked = defaultChecked,
                        IsEnabled = defaultChecked,
                        Margin = new System.Windows.Thickness(0, 1, 0, 1)
                    };

                    var stLabel = new System.Windows.Controls.TextBlock
                    {
                        FontSize = 11,
                        TextWrapping = System.Windows.TextWrapping.NoWrap,
                        Foreground = new System.Windows.Media.SolidColorBrush(
                            System.Windows.Media.Color.FromRgb(175, 220, 190))
                    };
                    stLabel.Text = st.StationName +
                                   (string.IsNullOrEmpty(st.ComputerName) ? "" : "  ·  " + st.ComputerName);
                    cbSt.Content = stLabel;

                    cbSection.Checked += (s, e) =>
                    {
                        cbSt.IsEnabled = true;
                        cbSt.IsChecked = true;
                    };
                    cbSection.Unchecked += (s, e) =>
                    {
                        cbSt.IsEnabled = false;
                        cbSt.IsChecked = false;
                    };

                    stationCheckboxes.Add((cbSt, st.FolderPath));
                    stationList.Children.Add(cbSt);
                }

                expander.Content = stationList;
                sectionStack.Children.Add(expander);
                sectionBorder.Child = sectionStack;
                content.Children.Add(sectionBorder);

                return cbSection;
            }

            var cbGhp = AddStationSection("⚙", "GHP Stations", System.Windows.Media.Color.FromRgb(63, 185, 80),
                ghpStations, true);
            var cbLcs2 = AddStationSection("🔧", "LCS Stations", System.Windows.Media.Color.FromRgb(80, 160, 220),
                lcsStations, false);
            var cbBfl = AddStationSection("💧", "Backflush Stations", System.Windows.Media.Color.FromRgb(220, 160, 60),
                backflushStations, false);
            var cbCon = AddStationSection("🔌", "Connectors", System.Windows.Media.Color.FromRgb(180, 120, 220),
                connectorStations, false);

            scroll.Content = content;
            System.Windows.Controls.Grid.SetRow(scroll, 1);
            root.Children.Add(scroll);

            // ── Footer buttons ───────────────────────────────────────────────
            var footerBorder = new System.Windows.Controls.Border
            {
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(5, 18, 9)),
                BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(22, 70, 36)),
                BorderThickness = new System.Windows.Thickness(0, 1, 0, 0),
                Padding = new System.Windows.Thickness(20, 12, 20, 12)
            };

            var btnRow = new System.Windows.Controls.StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Right
            };

            var btnCancel = new System.Windows.Controls.Button
            {
                Content = "Cancel",
                Padding = new System.Windows.Thickness(18, 8, 18, 8),
                Margin = new System.Windows.Thickness(0, 0, 10, 0),
                FontSize = 12,
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(18, 36, 22)),
                Foreground =
                    new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(130, 160, 135)),
                BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(36, 70, 44)),
                BorderThickness = new System.Windows.Thickness(1),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            btnCancel.Click += (s, e) => { DialogResult = false; };

            var btnContinue = new System.Windows.Controls.Button
            {
                Content = "Load →",
                Padding = new System.Windows.Thickness(22, 8, 22, 8),
                FontSize = 13,
                FontWeight = System.Windows.FontWeights.SemiBold,
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(22, 100, 50)),
                Foreground =
                    new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(180, 245, 205)),
                BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(50, 180, 90)),
                BorderThickness = new System.Windows.Thickness(1),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            btnContinue.Click += (s, e) =>
            {
                IncludeLcs = cbLcs2?.IsChecked == true;
                IncludeBackflush = cbBfl?.IsChecked == true;
                IncludeConnectors = cbCon?.IsChecked == true;
                FilterByDate = cbDateFilter.IsChecked == true;
                MaxMonths = monthsCombo.SelectedItem is string sel
                    ? int.TryParse(sel.Split(' ')[0], out int pm) ? pm : 6
                    : 6;
                ExcludedFolderPaths = stationCheckboxes
                    .Where(x => x.cb.IsChecked != true)
                    .Select(x => x.path)
                    .ToList();
                DialogResult = true;
            };

            btnRow.Children.Add(btnCancel);
            btnRow.Children.Add(btnContinue);
            footerBorder.Child = btnRow;
            System.Windows.Controls.Grid.SetRow(footerBorder, 2);
            root.Children.Add(footerBorder);

            Content = root;
        }
    }
}