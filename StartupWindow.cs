using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

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
            AddHex(canvas, W, H, r, "↻", "RECENT FOLDER", "Last used location", rowOffset + 0 * stepX, stepY, false);
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

                case "RECENT FOLDER":
                    string recent = LoadRecentPath();
                    if (!string.IsNullOrEmpty(recent) && Directory.Exists(recent))
                    {
                        SelectedPath = recent;
                        Mode = StartupMode.Local;
                        DialogResult = true;
                    }
                    else
                    {
                        MessageBox.Show(
                            "No recent folder saved yet.\nLoad a folder first and it will appear here.",
                            "Recent Folder",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }
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
                File.WriteAllText(RecentPathFile, path);
            }
            catch { }
        }

        private static string LoadRecentPath()
        {
            try { return File.Exists(RecentPathFile) ? File.ReadAllText(RecentPathFile).Trim() : ""; }
            catch { return ""; }
        }
    }

    public enum StartupMode { Local, Remote, Sample }

    public class StationTypeFilterDialog : Window
    {
        public bool IncludeLcs       { get; private set; } = false;
        public bool IncludeBackflush { get; private set; } = false;

        public StationTypeFilterDialog(int lcsCount, int backflushCount)
        {
            Title                 = "Station Types Found";
            Width                 = 420;
            Height                = 260;
            ResizeMode            = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background            = new SolidColorBrush(Color.FromRgb(8, 14, 10));

            var root = new StackPanel { Margin = new Thickness(24, 20, 24, 20) };

            root.Children.Add(new TextBlock
            {
                Text         = "Additional station types detected",
                FontSize     = 13,
                FontWeight   = FontWeights.SemiBold,
                Foreground   = new SolidColorBrush(Color.FromRgb(210, 245, 220)),
                Margin       = new Thickness(0, 0, 0, 6)
            });

            root.Children.Add(new TextBlock
            {
                Text         = "Select which types to include in the analysis:",
                FontSize     = 10,
                Foreground   = new SolidColorBrush(Color.FromRgb(110, 160, 125)),
                Margin       = new Thickness(0, 0, 0, 18),
                TextWrapping = TextWrapping.Wrap
            });

            var cbLcs = new CheckBox
            {
                Content     = "LCS  (" + lcsCount + " station" + (lcsCount != 1 ? "s" : "") + ")",
                FontSize    = 11,
                Foreground  = new SolidColorBrush(Color.FromRgb(180, 225, 195)),
                IsEnabled   = lcsCount > 0,
                IsChecked   = false,
                Margin      = new Thickness(0, 0, 0, 10)
            };

            var cbBackflush = new CheckBox
            {
                Content     = "Backflush  (" + backflushCount + " station" + (backflushCount != 1 ? "s" : "") + ")",
                FontSize    = 11,
                Foreground  = new SolidColorBrush(Color.FromRgb(180, 225, 195)),
                IsEnabled   = backflushCount > 0,
                IsChecked   = false,
                Margin      = new Thickness(0, 0, 0, 24)
            };

            root.Children.Add(cbLcs);
            root.Children.Add(cbBackflush);

            var btnRow = new StackPanel
            {
                Orientation         = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            var btnConfirm = new Button
            {
                Content         = "Confirm →",
                Padding         = new Thickness(18, 7, 18, 7),
                FontWeight      = FontWeights.SemiBold,
                Background      = new SolidColorBrush(Color.FromRgb(150, 85, 15)),
                Foreground      = new SolidColorBrush(Color.FromRgb(255, 235, 180)),
                BorderBrush     = new SolidColorBrush(Color.FromRgb(210, 130, 30)),
                BorderThickness = new Thickness(1),
                Cursor          = Cursors.Hand
            };

            btnConfirm.Click += (s, e) =>
            {
                IncludeLcs       = cbLcs.IsChecked == true;
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
        public bool IncludeLcs       { get; private set; } = false;
        public bool IncludeBackflush { get; private set; } = false;

        public LoadOptionsDialog(int ghpCount, int lcsCount, int backflushCount)
        {
            Title                 = "Select Stations to Load";
            Width                 = 460;
            SizeToContent         = SizeToContent.Height;
            ResizeMode            = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background            = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(8, 14, 10));

            var root = new System.Windows.Controls.StackPanel
            {
                Margin = new System.Windows.Thickness(24, 20, 24, 20)
            };

            root.Children.Add(new System.Windows.Controls.TextBlock
            {
                Text         = "Choose which station types to load",
                FontSize     = 13,
                FontWeight   = System.Windows.FontWeights.SemiBold,
                Foreground   = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(210, 245, 220)),
                Margin       = new System.Windows.Thickness(0, 0, 0, 6)
            });

            root.Children.Add(new System.Windows.Controls.TextBlock
            {
                Text         = "Scanned folder — select what to include:",
                FontSize     = 10,
                Foreground   = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(100, 150, 115)),
                Margin       = new System.Windows.Thickness(0, 0, 0, 16),
                TextWrapping = System.Windows.TextWrapping.Wrap
            });

            // GHP always included
            root.Children.Add(new System.Windows.Controls.TextBlock
            {
                Text       = "✓  GHP Stations  (" + ghpCount + ")  — always loaded",
                FontSize   = 11,
                Foreground = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(63, 185, 80)),
                Margin     = new System.Windows.Thickness(0, 0, 0, 10)
            });

            var cbLcs = new System.Windows.Controls.CheckBox
            {
                IsEnabled = lcsCount > 0,
                IsChecked = false,
                Margin    = new System.Windows.Thickness(0, 0, 0, 8)
            };
            cbLcs.Content = new System.Windows.Controls.TextBlock
            {
                Text       = "LCS Stations  (" + lcsCount + ")  — Line Control Service",
                FontSize   = 11,
                Foreground = new System.Windows.Media.SolidColorBrush(
                    lcsCount > 0
                        ? System.Windows.Media.Color.FromRgb(180, 225, 195)
                        : System.Windows.Media.Color.FromRgb(80, 100, 88))
            };

            var cbBackflush = new System.Windows.Controls.CheckBox
            {
                IsEnabled = backflushCount > 0,
                IsChecked = false,
                Margin    = new System.Windows.Thickness(0, 0, 0, 4)
            };
            cbBackflush.Content = new System.Windows.Controls.TextBlock
            {
                Text       = "Backflush Stations  (" + backflushCount + ")",
                FontSize   = 11,
                Foreground = new System.Windows.Media.SolidColorBrush(
                    backflushCount > 0
                        ? System.Windows.Media.Color.FromRgb(180, 225, 195)
                        : System.Windows.Media.Color.FromRgb(80, 100, 88))
            };

            root.Children.Add(cbLcs);
            root.Children.Add(cbBackflush);

            bool anyOptional = lcsCount > 0 || backflushCount > 0;
            if (anyOptional)
            {
                root.Children.Add(new System.Windows.Controls.TextBlock
                {
                    Text         = "⚠  LCS and Backflush stations may take significantly longer to load.",
                    FontSize     = 9,
                    Foreground   = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(200, 160, 60)),
                    Margin       = new System.Windows.Thickness(0, 8, 0, 16),
                    TextWrapping = System.Windows.TextWrapping.Wrap
                });
            }
            else
            {
                root.Children.Add(new System.Windows.Controls.TextBlock
                {
                    Text   = "",
                    Margin = new System.Windows.Thickness(0, 12, 0, 0)
                });
            }

            var btnRow = new System.Windows.Controls.StackPanel
            {
                Orientation         = System.Windows.Controls.Orientation.Horizontal,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Right
            };

            var btnCancel = new System.Windows.Controls.Button
            {
                Content         = "Cancel",
                Padding         = new System.Windows.Thickness(16, 7, 16, 7),
                Margin          = new System.Windows.Thickness(0, 0, 8, 0),
                Background      = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(20, 30, 20)),
                Foreground      = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(140, 160, 140)),
                BorderBrush     = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(40, 70, 44)),
                BorderThickness = new System.Windows.Thickness(1),
                Cursor          = System.Windows.Input.Cursors.Hand
            };
            btnCancel.Click += (s, e) => { DialogResult = false; };

            var btnContinue = new System.Windows.Controls.Button
            {
                Content         = "Continue →",
                Padding         = new System.Windows.Thickness(18, 7, 18, 7),
                FontWeight      = System.Windows.FontWeights.SemiBold,
                Background      = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(22, 90, 45)),
                Foreground      = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(180, 240, 200)),
                BorderBrush     = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(40, 160, 80)),
                BorderThickness = new System.Windows.Thickness(1),
                Cursor          = System.Windows.Input.Cursors.Hand
            };
            btnContinue.Click += (s, e) =>
            {
                IncludeLcs       = cbLcs.IsChecked == true;
                IncludeBackflush = cbBackflush.IsChecked == true;
                DialogResult = true;
            };

            btnRow.Children.Add(btnCancel);
            btnRow.Children.Add(btnContinue);
            root.Children.Add(btnRow);

            Content = root;
        }
    }
}