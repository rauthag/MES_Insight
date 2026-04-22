using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace RTAnalyzer
{
    public class StartupWindow : Window
    {
        public string SelectedPath { get; private set; }
        public StartupMode Mode    { get; private set; }

        private static readonly Color BgColor     = Color.FromRgb(10, 30, 16);
        private static readonly Color HexFill     = Color.FromRgb(210, 100, 15);
        private static readonly Color HexHover    = Color.FromRgb(240, 130, 30);
        private static readonly Color HexStroke   = Color.FromRgb(255, 160, 60);
        private static readonly Color TextLight   = Color.FromRgb(255, 245, 230);
        private static readonly Color TextSub     = Color.FromRgb(255, 210, 160);

        private static readonly string DefaultRemotePath =
            @"\\vt1.vitesco.com\fs\didv0952\01_MES_APP_BACKUP";

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
            Title                 = "MES Insight";
            Width                 = 800;
            Height                = 560;
            ResizeMode            = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background            = new SolidColorBrush(BgColor);
            Content               = BuildLayout();
        }

        private UIElement BuildLayout()
        {
            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(60) });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(34) });

            // Header
            var header = new Border
            {
                Background      = new SolidColorBrush(Color.FromRgb(6, 20, 10)),
                BorderBrush     = new SolidColorBrush(Color.FromRgb(180, 80, 10)),
                BorderThickness = new Thickness(0, 0, 0, 1)
            };
            var hStack = new StackPanel
            {
                Orientation       = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                Margin            = new Thickness(28, 0, 0, 0)
            };
            hStack.Children.Add(new TextBlock
            {
                Text              = "🗠",
                FontSize          = 20,
                Foreground        = new SolidColorBrush(HexFill),
                VerticalAlignment = VerticalAlignment.Center,
                Margin            = new Thickness(0, 0, 10, 0)
            });
            var ts = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            ts.Children.Add(new TextBlock
            {
                Text       = "MES Insight",
                FontSize   = 15,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(255, 240, 220))
            });
            ts.Children.Add(new TextBlock
            {
                Text       = "Manufacturing Execution System  ·  Diagnostics & Analytics",
                FontSize   = 9,
                Foreground = new SolidColorBrush(Color.FromRgb(180, 120, 60)),
                Margin     = new Thickness(0, 1, 0, 0)
            });
            hStack.Children.Add(ts);
            header.Child = hStack;
            Grid.SetRow(header, 0);
            root.Children.Add(header);

            // Hex grid
            // Pointy-top hex: W = sqrt(3)*r, H = 2*r
            // Interlocking: stepX = W+gap, stepY = H*0.75+gap, odd row offset = W/2
            const double r    = 90;
            const double gap  = 5;
            double W          = Math.Sqrt(3) * r;   // ~155.9
            double H          = 2 * r;               // 180
            double stepX      = W + gap;
            double stepY      = H * 0.75 + gap;
            double rowOffset  = stepX / 2.0;

            var canvas = new Canvas
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment   = VerticalAlignment.Center
            };

            bool sampleOk = Directory.Exists(SampleDataPath);

            // Row 0 — 3 hexagons
            AddHex(canvas, W, H, r, "📂", "LOCAL FOLDER",   "Local or network path",   0 * stepX,              0,     false);
            AddHex(canvas, W, H, r, "🌐", "REMOTE BACKUP",  "Vitesco backup server",    1 * stepX,              0,     false);
            AddHex(canvas, W, H, r, "🧪", "SAMPLE DATA",    sampleOk ? "Demo data ready" : "Not available",
                                                                                          2 * stepX,              0,     !sampleOk);

            // Row 1 — 2 hexagons (offset by rowOffset so they nestle between row 0)
            AddHex(canvas, W, H, r, "📁", "RECENT FOLDER",  "Last used location",       rowOffset + 0 * stepX, stepY, false);
            AddHex(canvas, W, H, r, "✕",  "EXIT",           "Close application",        rowOffset + 1 * stepX, stepY, false, isExit: true);

            canvas.Width  = 3 * stepX - gap + 0.1;
            canvas.Height = stepY + H;

            Grid.SetRow(canvas, 1);
            root.Children.Add(canvas);

            // Footer
            var footer = new Border
            {
                Background      = new SolidColorBrush(Color.FromRgb(6, 20, 10)),
                BorderBrush     = new SolidColorBrush(Color.FromRgb(180, 80, 10)),
                BorderThickness = new Thickness(0, 1, 0, 0)
            };
            footer.Child = new TextBlock
            {
                Text              = "MES Insight  ·  Manufacturing Execution System Diagnostics",
                FontSize          = 9,
                Foreground        = new SolidColorBrush(Color.FromRgb(140, 80, 30)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin            = new Thickness(28, 0, 0, 0)
            };
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
                Width   = W,
                Height  = H,
                Cursor  = disabled ? Cursors.Arrow : Cursors.Hand,
                Opacity = disabled ? 0.38 : 1.0
            };

            // Pointy-top hexagon — 6 points
            double cx = W / 2;
            double cy = H / 2;

            var poly = new Polygon
            {
                Fill            = new SolidColorBrush(HexFill),
                Stroke          = new SolidColorBrush(HexStroke),
                StrokeThickness = 2
            };

            for (int i = 0; i < 6; i++)
            {
                // Pointy-top: start at top, every 60°
                double angle = Math.PI / 180.0 * (60 * i - 90);
                poly.Points.Add(new Point(cx + r * Math.Cos(angle), cy + r * Math.Sin(angle)));
            }

            grid.Children.Add(poly);

            // Content
            var stack = new StackPanel
            {
                VerticalAlignment   = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            stack.Children.Add(new TextBlock
            {
                Text                = icon,
                FontSize            = 28,
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground          = new SolidColorBrush(TextLight),
                Margin              = new Thickness(0, 0, 0, 6)
            });

            stack.Children.Add(new TextBlock
            {
                Text                = title,
                FontSize            = 10,
                FontWeight          = FontWeights.Bold,
                Foreground          = new SolidColorBrush(TextLight),
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment       = TextAlignment.Center,
                Margin              = new Thickness(0, 0, 0, 3)
            });

            stack.Children.Add(new TextBlock
            {
                Text                = sub,
                FontSize            = 8,
                Foreground          = new SolidColorBrush(TextSub),
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment       = TextAlignment.Center,
                TextWrapping        = TextWrapping.Wrap,
                MaxWidth            = W - 40
            });

            grid.Children.Add(stack);

            if (!disabled)
            {
                grid.MouseEnter += (s, e) => poly.Fill = new SolidColorBrush(HexHover);
                grid.MouseLeave += (s, e) => poly.Fill = new SolidColorBrush(HexFill);

                string capturedTitle = title;
                bool   capturedExit  = isExit;
                grid.MouseLeftButtonUp += (s, e) => HandleClick(capturedTitle, capturedExit);
            }

            Canvas.SetLeft(grid, left);
            Canvas.SetTop(grid,  top);
            canvas.Children.Add(grid);
        }

        private void HandleClick(string title, bool isExit)
        {
            if (isExit) { Application.Current.Shutdown(); return; }

            switch (title)
            {
                case "LOCAL FOLDER":
                    ShowPathDialog(isRemote: false);
                    break;

                case "REMOTE BACKUP":
                    ShowPathDialog(isRemote: true);
                    break;

                case "SAMPLE DATA":
                    SelectedPath = SampleDataPath;
                    Mode         = StartupMode.Sample;
                    DialogResult = true;
                    break;

                case "RECENT FOLDER":
                    string recent = LoadRecentPath();
                    if (!string.IsNullOrEmpty(recent) && Directory.Exists(recent))
                    {
                        SelectedPath = recent;
                        Mode         = StartupMode.Local;
                        DialogResult = true;
                    }
                    else
                    {
                        MessageBox.Show("No recent folder saved.", "Recent",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    break;
            }
        }

        private void ShowPathDialog(bool isRemote)
        {
            string defaultText   = isRemote ? DefaultRemotePath : "";
            string defaultBrowse = isRemote
                ? (Directory.Exists(DefaultRemotePath)
                    ? DefaultRemotePath
                    : Environment.GetFolderPath(Environment.SpecialFolder.Desktop))
                : Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

            var dlg = new PathInputDialog(
                isRemote ? "Remote Backup" : "Local Folder",
                defaultText,
                defaultBrowse);

            dlg.Owner = this;

            if (dlg.ShowDialog() == true && !string.IsNullOrEmpty(dlg.SelectedPath))
            {
                SelectedPath = dlg.SelectedPath;
                Mode         = isRemote ? StartupMode.Remote : StartupMode.Local;
                SaveRecentPath(SelectedPath);
                DialogResult = true;
            }
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
            try   { return File.Exists(RecentPathFile) ? File.ReadAllText(RecentPathFile).Trim() : ""; }
            catch { return ""; }
        }
    }

    public class PathInputDialog : Window
    {
        public string SelectedPath { get; private set; }

        public PathInputDialog(string title, string defaultText, string defaultBrowsePath)
        {
            Title                 = title;
            Width                 = 520;
            Height                = 190;
            ResizeMode            = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background            = new SolidColorBrush(Color.FromRgb(8, 14, 10));

            var root = new StackPanel { Margin = new Thickness(24, 20, 24, 20) };

            root.Children.Add(new TextBlock
            {
                Text       = title,
                FontSize   = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(255, 240, 220)),
                Margin     = new Thickness(0, 0, 0, 12)
            });

            var inputRow = new Grid { Margin = new Thickness(0, 0, 0, 12) };
            inputRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            inputRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var pathBox = new TextBox
            {
                Text            = defaultText,
                Background      = new SolidColorBrush(Color.FromRgb(6, 18, 10)),
                Foreground      = new SolidColorBrush(Color.FromRgb(220, 200, 170)),
                BorderBrush     = new SolidColorBrush(Color.FromRgb(180, 90, 20)),
                BorderThickness = new Thickness(1),
                Padding         = new Thickness(8, 6, 8, 6),
                FontSize        = 10,
                FontFamily      = new FontFamily("Consolas"),
                CaretBrush      = new SolidColorBrush(Color.FromRgb(220, 200, 170)),
                VerticalContentAlignment = VerticalAlignment.Center
            };

            var browseBtn = new Button
            {
                Content         = "Browse...",
                Margin          = new Thickness(8, 0, 0, 0),
                Padding         = new Thickness(12, 0, 12, 0),
                Height          = 32,
                Background      = new SolidColorBrush(Color.FromRgb(140, 70, 10)),
                Foreground      = new SolidColorBrush(Color.FromRgb(255, 230, 180)),
                BorderBrush     = new SolidColorBrush(Color.FromRgb(200, 110, 30)),
                BorderThickness = new Thickness(1),
                Cursor          = Cursors.Hand
            };

            browseBtn.Click += (s, e) =>
            {
                string start = !string.IsNullOrEmpty(pathBox.Text.Trim())
                    ? pathBox.Text.Trim()
                    : defaultBrowsePath;

                var dialog = new System.Windows.Forms.FolderBrowserDialog
                {
                    SelectedPath = start,
                    Description  = "Select folder"
                };

                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    pathBox.Text = dialog.SelectedPath;
            };

            Grid.SetColumn(pathBox,   0);
            Grid.SetColumn(browseBtn, 1);
            inputRow.Children.Add(pathBox);
            inputRow.Children.Add(browseBtn);
            root.Children.Add(inputRow);

            var btnRow = new StackPanel
            {
                Orientation         = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            var cancelBtn = new Button
            {
                Content         = "Cancel",
                Padding         = new Thickness(16, 7, 16, 7),
                Margin          = new Thickness(0, 0, 8, 0),
                Background      = new SolidColorBrush(Color.FromRgb(20, 30, 20)),
                Foreground      = new SolidColorBrush(Color.FromRgb(160, 140, 110)),
                BorderBrush     = new SolidColorBrush(Color.FromRgb(80, 60, 30)),
                BorderThickness = new Thickness(1),
                Cursor          = Cursors.Hand
            };
            cancelBtn.Click += (s, e) => { DialogResult = false; };

            var loadBtn = new Button
            {
                Content         = "Load →",
                Padding         = new Thickness(16, 7, 16, 7),
                FontWeight      = FontWeights.SemiBold,
                Background      = new SolidColorBrush(Color.FromRgb(160, 85, 15)),
                Foreground      = new SolidColorBrush(Color.FromRgb(255, 235, 180)),
                BorderBrush     = new SolidColorBrush(Color.FromRgb(220, 130, 30)),
                BorderThickness = new Thickness(1),
                Cursor          = Cursors.Hand
            };
            loadBtn.Click += (s, e) =>
            {
                string path = pathBox.Text.Trim();
                if (string.IsNullOrEmpty(path)) return;
                SelectedPath = path;
                DialogResult = true;
            };

            btnRow.Children.Add(cancelBtn);
            btnRow.Children.Add(loadBtn);
            root.Children.Add(btnRow);

            Content = root;
        }
    }

    public enum StartupMode { Local, Remote, Sample }
}