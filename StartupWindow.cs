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

        private TextBox _localPathBox;
        private TextBox _remotePathBox;

        // Orange accent from paint-rack image: warm orange #E8721A
        private static readonly Color OrangeAccent   = Color.FromRgb(232, 114, 26);
        private static readonly Color OrangeHover    = Color.FromRgb(255, 140, 40);
        private static readonly Color OrangeFill     = Color.FromArgb(50, 232, 114, 26);
        private static readonly Color DarkBg         = Color.FromRgb(8, 14, 10);
        private static readonly Color CardBg         = Color.FromRgb(10, 20, 14);

        private static readonly string DefaultRemotePath =
            @"\\vt1.vitesco.com\fs\didv0952\01_MES_APP_BACKUP";

        private static readonly string SampleDataPath = FindSampleDataPath();

        private static string FindSampleDataPath()
        {
            // Walk up from exe location looking for SampleData folder
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
            Width                 = 860;
            Height                = 600;
            ResizeMode            = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background            = new SolidColorBrush(DarkBg);
            Content               = BuildLayout();
        }

        private UIElement BuildLayout()
        {
            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(70) });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(40) });

            // ── Header ──────────────────────────────────────────────────────
            var header = new Border
            {
                Background      = new SolidColorBrush(Color.FromRgb(5, 16, 9)),
                BorderBrush     = new SolidColorBrush(Color.FromRgb(20, 60, 32)),
                BorderThickness = new Thickness(0, 0, 0, 1)
            };
            var headerStack = new StackPanel
            {
                Orientation       = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                Margin            = new Thickness(28, 0, 0, 0)
            };
            headerStack.Children.Add(new TextBlock
            {
                Text              = "🗠",
                FontSize          = 22,
                Foreground        = new SolidColorBrush(OrangeAccent),
                VerticalAlignment = VerticalAlignment.Center,
                Margin            = new Thickness(0, 0, 10, 0)
            });
            var titleStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            titleStack.Children.Add(new TextBlock
            {
                Text       = "MES Insight",
                FontSize   = 17,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(220, 240, 225))
            });
            titleStack.Children.Add(new TextBlock
            {
                Text       = "Manufacturing Execution System  ·  Diagnostics & Analytics",
                FontSize   = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(70, 120, 85)),
                Margin     = new Thickness(0, 1, 0, 0)
            });
            headerStack.Children.Add(titleStack);
            header.Child = headerStack;
            Grid.SetRow(header, 0);
            root.Children.Add(header);

            // ── Hexagon grid (layout: row1=3, row2=2 centered like image) ──
            var hexContainer = new Canvas
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment   = VerticalAlignment.Center
            };

            // Hexagon size
            const double HW  = 180; // hex width
            const double HH  = 200; // hex height
            const double GAP = 6;   // gap between hexes
            const double OX  = HW * 0.5 + GAP * 0.5; // horizontal offset for row 2

            // Row 1: 3 hexagons
            var hex0 = BuildHexCard("📂", "Local Folder",   "Load from local or network path",        0, 0, true,  false);
            var hex1 = BuildHexCard("🌐", "Remote Backup",  "Vitesco remote backup server",           1, 0, false, false);
            var hex2 = BuildHexCard("🧪", "Sample Data",    Directory.Exists(SampleDataPath) ? "Demo data included" : "SampleData folder not found", 2, 0, false, false);

            // Row 2: 2 hexagons (offset)
            var hex3 = BuildHexCard("📁", "Recent Folder",  "Open last used folder",                  0, 1, false, false, offset: OX);
            var hex4 = BuildHexCard("✕",  "Exit",           "Close application",                      1, 1, false, true,  offset: OX);

            double row2top = HH + GAP;
            double row1left = 0;
            double row2left = OX;

            // Place row 1
            Canvas.SetLeft(hex0, 0 * (HW + GAP));
            Canvas.SetTop(hex0,  0);
            Canvas.SetLeft(hex1, 1 * (HW + GAP));
            Canvas.SetTop(hex1,  0);
            Canvas.SetLeft(hex2, 2 * (HW + GAP));
            Canvas.SetTop(hex2,  0);

            // Place row 2 (centered under row 1 gap)
            Canvas.SetLeft(hex3, OX + 0 * (HW + GAP));
            Canvas.SetTop(hex3,  row2top);
            Canvas.SetLeft(hex4, OX + 1 * (HW + GAP));
            Canvas.SetTop(hex4,  row2top);

            hexContainer.Width  = 3 * HW + 2 * GAP;
            hexContainer.Height = 2 * HH + GAP;

            hexContainer.Children.Add(hex0);
            hexContainer.Children.Add(hex1);
            hexContainer.Children.Add(hex2);
            hexContainer.Children.Add(hex3);
            hexContainer.Children.Add(hex4);

            Grid.SetRow(hexContainer, 1);
            root.Children.Add(hexContainer);

            // ── Footer ───────────────────────────────────────────────────────
            var footer = new Border
            {
                Background      = new SolidColorBrush(Color.FromRgb(5, 16, 9)),
                BorderBrush     = new SolidColorBrush(Color.FromRgb(20, 60, 32)),
                BorderThickness = new Thickness(0, 1, 0, 0)
            };
            footer.Child = new TextBlock
            {
                Text              = "MES Insight  ·  Manufacturing Execution System Diagnostics",
                FontSize          = 9,
                Foreground        = new SolidColorBrush(Color.FromRgb(40, 80, 52)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin            = new Thickness(28, 0, 0, 0)
            };
            Grid.SetRow(footer, 2);
            root.Children.Add(footer);

            return root;
        }

        private Grid BuildHexCard(string icon, string title, string subtitle,
            int col, int row, bool isLocal, bool isExit, double offset = 0)
        {
            const double W = 180;
            const double H = 200;

            var grid = new Grid { Width = W, Height = H, Cursor = Cursors.Hand };

            // Hexagon polygon (pointy-top flat-side)
            var hex = BuildHexPolygon(W / 2, H / 2, W * 0.46, OrangeFill, OrangeAccent);
            grid.Children.Add(hex);

            // Inner card (white square rounded, inside hex)
            const double cardW = 116;
            const double cardH = 116;
            var card = new Border
            {
                Width           = cardW,
                Height          = cardH,
                Background      = new SolidColorBrush(CardBg),
                BorderBrush     = new SolidColorBrush(Color.FromRgb(30, 70, 42)),
                BorderThickness = new Thickness(1),
                CornerRadius    = new CornerRadius(10),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment   = VerticalAlignment.Center
            };

            var cardContent = new StackPanel
            {
                VerticalAlignment   = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin              = new Thickness(8)
            };

            cardContent.Children.Add(new TextBlock
            {
                Text                = icon,
                FontSize            = 24,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin              = new Thickness(0, 0, 0, 6)
            });
            cardContent.Children.Add(new TextBlock
            {
                Text                = title,
                FontSize            = 11,
                FontWeight          = FontWeights.SemiBold,
                Foreground          = new SolidColorBrush(Color.FromRgb(220, 245, 230)),
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment       = TextAlignment.Center,
                TextWrapping        = TextWrapping.Wrap,
                Margin              = new Thickness(0, 0, 0, 4)
            });
            cardContent.Children.Add(new TextBlock
            {
                Text                = subtitle,
                FontSize            = 8,
                Foreground          = new SolidColorBrush(Color.FromRgb(80, 140, 100)),
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment       = TextAlignment.Center,
                TextWrapping        = TextWrapping.Wrap
            });

            card.Child = cardContent;
            grid.Children.Add(card);

            // Hover effects
            hex.MouseEnter += (s, e) =>
            {
                hex.Fill   = new SolidColorBrush(Color.FromArgb(90, OrangeHover.R, OrangeHover.G, OrangeHover.B));
                hex.Stroke = new SolidColorBrush(OrangeHover);
            };
            hex.MouseLeave += (s, e) =>
            {
                hex.Fill   = new SolidColorBrush(OrangeFill);
                hex.Stroke = new SolidColorBrush(OrangeAccent);
            };
            grid.MouseEnter += (s, e) =>
            {
                hex.Fill   = new SolidColorBrush(Color.FromArgb(90, OrangeHover.R, OrangeHover.G, OrangeHover.B));
                hex.Stroke = new SolidColorBrush(OrangeHover);
            };
            grid.MouseLeave += (s, e) =>
            {
                hex.Fill   = new SolidColorBrush(OrangeFill);
                hex.Stroke = new SolidColorBrush(OrangeAccent);
            };

            // Click action
            grid.MouseLeftButtonUp += (s, e) => HandleCardClick(title, isLocal, isExit);

            return grid;
        }

        private void HandleCardClick(string title, bool isLocal, bool isExit)
        {
            if (isExit)
            {
                Application.Current.Shutdown();
                return;
            }

            switch (title)
            {
                case "Local Folder":
                    ShowPathDialog(isLocal: true);
                    break;

                case "Remote Backup":
                    ShowPathDialog(isLocal: false);
                    break;

                case "Sample Data":
                    if (!Directory.Exists(SampleDataPath))
                    {
                        MessageBox.Show(
                            "SampleData folder not found.\nExpected at:\n" + SampleDataPath,
                            "Not available", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }
                    SelectedPath = SampleDataPath;
                    Mode         = StartupMode.Sample;
                    DialogResult = true;
                    break;

                case "Recent Folder":
                    string recent = LoadRecentPath();
                    if (!string.IsNullOrEmpty(recent) && Directory.Exists(recent))
                    {
                        SelectedPath = recent;
                        Mode         = StartupMode.Local;
                        DialogResult = true;
                    }
                    else
                    {
                        MessageBox.Show("No recent folder found.", "Recent", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    break;
            }
        }

        private void ShowPathDialog(bool isLocal)
        {
            string defaultPath;

            if (isLocal)
            {
                defaultPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            }
            else
            {
                // Try the remote path directly — FolderBrowserDialog supports UNC paths
                defaultPath = Directory.Exists(DefaultRemotePath)
                    ? DefaultRemotePath
                    : Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            }

            // Input dialog — show textbox with default + browse
            var dlg = new PathInputDialog(
                isLocal ? "Local Folder" : "Remote Backup",
                isLocal ? "" : DefaultRemotePath,
                defaultPath,
                isLocal);

            dlg.Owner = this;

            if (dlg.ShowDialog() == true && !string.IsNullOrEmpty(dlg.SelectedPath))
            {
                SelectedPath = dlg.SelectedPath;
                Mode         = isLocal ? StartupMode.Local : StartupMode.Remote;
                SaveRecentPath(SelectedPath);
                DialogResult = true;
            }
        }

        private static string RecentPathFile =>
            System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MESPulse", "recent.txt");

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

        private static Polygon BuildHexPolygon(double cx, double cy, double r,
            Color fill, Color stroke)
        {
            var poly = new Polygon
            {
                Fill            = new SolidColorBrush(fill),
                Stroke          = new SolidColorBrush(stroke),
                StrokeThickness = 1.5
            };
            for (int i = 0; i < 6; i++)
            {
                double angle = Math.PI / 180.0 * (60 * i - 30);
                poly.Points.Add(new Point(cx + r * Math.Cos(angle), cy + r * Math.Sin(angle)));
            }
            return poly;
        }
    }

    // ── Inline path input dialog ─────────────────────────────────────────────
    public class PathInputDialog : Window
    {
        public string SelectedPath { get; private set; }

        private readonly TextBox _pathBox;
        private readonly string  _defaultBrowsePath;

        public PathInputDialog(string title, string defaultText, string defaultBrowsePath, bool isLocal)
        {
            Title                 = title;
            Width                 = 520;
            Height                = 200;
            ResizeMode            = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background            = new SolidColorBrush(Color.FromRgb(8, 14, 10));
            _defaultBrowsePath    = defaultBrowsePath;

            var root = new StackPanel { Margin = new Thickness(24, 20, 24, 20) };

            root.Children.Add(new TextBlock
            {
                Text       = title,
                FontSize   = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(220, 240, 225)),
                Margin     = new Thickness(0, 0, 0, 12)
            });

            var inputRow = new Grid { Margin = new Thickness(0, 0, 0, 12) };
            inputRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            inputRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _pathBox = new TextBox
            {
                Text            = defaultText,
                Background      = new SolidColorBrush(Color.FromRgb(6, 18, 10)),
                Foreground      = new SolidColorBrush(Color.FromRgb(180, 220, 190)),
                BorderBrush     = new SolidColorBrush(Color.FromRgb(30, 90, 50)),
                BorderThickness = new Thickness(1),
                Padding         = new Thickness(8, 6, 8, 6),
                FontSize        = 10,
                FontFamily      = new FontFamily("Consolas"),
                CaretBrush      = new SolidColorBrush(Color.FromRgb(180, 220, 190)),
                VerticalContentAlignment = VerticalAlignment.Center
            };

            var browseBtn = new Button
            {
                Content         = "Browse...",
                Margin          = new Thickness(8, 0, 0, 0),
                Padding         = new Thickness(12, 0, 12, 0),
                Height          = 32,
                Background      = new SolidColorBrush(Color.FromRgb(14, 40, 22)),
                Foreground      = new SolidColorBrush(Color.FromRgb(100, 190, 130)),
                BorderBrush     = new SolidColorBrush(Color.FromRgb(30, 90, 50)),
                BorderThickness = new Thickness(1),
                Cursor          = Cursors.Hand
            };

            browseBtn.Click += (s, e) =>
            {
                string start = !string.IsNullOrEmpty(_pathBox.Text.Trim()) && Directory.Exists(_pathBox.Text.Trim())
                    ? _pathBox.Text.Trim()
                    : _defaultBrowsePath;

                var dialog = new System.Windows.Forms.FolderBrowserDialog
                {
                    SelectedPath = start,
                    Description  = "Select folder"
                };

                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    _pathBox.Text = dialog.SelectedPath;
            };

            Grid.SetColumn(_pathBox,   0);
            Grid.SetColumn(browseBtn,  1);
            inputRow.Children.Add(_pathBox);
            inputRow.Children.Add(browseBtn);
            root.Children.Add(inputRow);

            var btnRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };

            var cancelBtn = new Button
            {
                Content         = "Cancel",
                Padding         = new Thickness(16, 7, 16, 7),
                Margin          = new Thickness(0, 0, 8, 0),
                Background      = new SolidColorBrush(Color.FromRgb(20, 40, 26)),
                Foreground      = new SolidColorBrush(Color.FromRgb(100, 140, 110)),
                BorderBrush     = new SolidColorBrush(Color.FromRgb(30, 70, 42)),
                BorderThickness = new Thickness(1),
                Cursor          = Cursors.Hand
            };
            cancelBtn.Click += (s, e) => { DialogResult = false; };

            var loadBtn = new Button
            {
                Content         = "Load →",
                Padding         = new Thickness(16, 7, 16, 7),
                FontWeight      = FontWeights.SemiBold,
                Background      = new SolidColorBrush(Color.FromRgb(150, 85, 15)),
                Foreground      = new SolidColorBrush(Color.FromRgb(255, 220, 160)),
                BorderBrush     = new SolidColorBrush(Color.FromRgb(210, 130, 30)),
                BorderThickness = new Thickness(1),
                Cursor          = Cursors.Hand
            };
            loadBtn.Click += (s, e) =>
            {
                string path = _pathBox.Text.Trim();
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

    public enum StartupMode
    {
        Local,
        Remote,
        Sample
    }
}