using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using MESInsight.Core;
using RTAnalyzer.Core;

namespace MESInsight
{
    public class StartupWindow : Window
    {
        public string SelectedPath { get; private set; }
        public List<string> SelectedPaths { get; private set; } = new List<string>();
        public StartupMode Mode { get; private set; }

        private static readonly Color BgColor = Color.FromRgb(22, 80, 45);
        private static readonly Color HexFill = Color.FromRgb(216, 115, 18);
        private static readonly Color HexHover = Color.FromRgb(240, 161, 48);
        private static readonly Color HexStroke = Color.FromRgb(22, 80, 45);
        private static readonly Color TextLight = Color.FromRgb(255, 245, 230);
        private static readonly Color TextSub = Color.FromRgb(255, 210, 160);
        private static readonly Color HexDimmed = Color.FromRgb(130, 70, 10);

        private static readonly string DefaultRemotePath =
            @"\\vt1.vitesco.com\fs\didv0952\06_MES_App_Logs";

        private Canvas _canvas;
        private Grid _expandedPanel;
        private bool _isExpanded = false;

        private static string ResolveRemotePath()
        {
            if (Directory.Exists(DefaultRemotePath)) return DefaultRemotePath;
            string tail = System.IO.Path.Combine("didv0952", "06_MES_App_Logs");
            foreach (char drive in new[] { 'F', 'T', 'Z', 'Y', 'X', 'W', 'V', 'S', 'R', 'Q' })
            {
                string candidate = drive + ":\\" + tail;
                if (Directory.Exists(candidate)) return candidate;
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
                System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? "",
                "SampleData");
        }

        public StartupWindow()
        {
            Title = "MES Insight";
            Width = 1100;
            Height = 980;
            ResizeMode = ResizeMode.CanResize;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = new SolidColorBrush(BgColor);
            FontFamily = new FontFamily(new Uri("pack://application:,,,/"), "./Fonts/#Inter 18pt");
            Content = BuildLayout();
        }

        private UIElement BuildLayout()
        {
            Grid root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(60) });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(34) });

            Border header = BuildHeader();
            Grid.SetRow(header, 0);
            root.Children.Add(header);

            Grid centerGrid = new Grid();
            _canvas = BuildHexCanvas();
            centerGrid.Children.Add(_canvas);

            _expandedPanel = new Grid
            {
                Visibility = Visibility.Collapsed,
                Background = new SolidColorBrush(Color.FromRgb(8, 22, 12))
            };
            centerGrid.Children.Add(_expandedPanel);

            Grid.SetRow(centerGrid, 1);
            root.Children.Add(centerGrid);

            Border footer = BuildFooter();
            Grid.SetRow(footer, 2);
            root.Children.Add(footer);

            return root;
        }

        private Border BuildHeader()
        {
            Border header = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(14, 55, 28)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(180, 80, 10)),
                BorderThickness = new Thickness(0, 0, 0, 5)
            };
            StackPanel hStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(28, 0, 0, 0)
            };
            hStack.Children.Add(new TextBlock
            {
                Text = "\U0001F4CA",
                FontSize = 32,
                Foreground = new SolidColorBrush(HexFill),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            });
            StackPanel ts = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
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
            return header;
        }

        private Border BuildFooter()
        {
            Border footer = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(14, 55, 28)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(180, 80, 10)),
                BorderThickness = new Thickness(0, 5, 0, 0)
            };
            Grid footerGrid = new Grid { Margin = new Thickness(28, 0, 28, 0) };
            footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            TextBlock left = new TextBlock
            {
                Text = "MES Insight v1.0 | \u00A9 2026",
                FontSize = 15,
                Foreground = new SolidColorBrush(Color.FromRgb(200, 130, 60)),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            TextBlock right = new TextBlock
            {
                Text = "Author: Lukas Paucin | lukas.paucin@mail.schaeffler.com",
                FontSize = 15,
                Foreground = new SolidColorBrush(Color.FromRgb(200, 130, 60)),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right,
                TextAlignment = TextAlignment.Right
            };
            Grid.SetColumn(right, 1);
            footerGrid.Children.Add(left);
            footerGrid.Children.Add(right);
            footer.Child = footerGrid;
            return footer;
        }

        private Canvas BuildHexCanvas()
        {
            const double r = 100;
            const double gap = 5;
            double W = Math.Sqrt(3) * r;
            double H = 2 * r;
            double stepX = W + gap;
            double stepY = H * 0.75 + gap;
            double rowOff = stepX / 2.0;

            bool sampleOk = Directory.Exists(SampleDataPath);

            Canvas canvas = new Canvas
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Width = 3 * stepX - gap + 0.1,
                Height = stepY + H
            };

            AddHex(canvas, W, H, r, "\U0001F4C2", "LOCAL FOLDER", "Local or network path", 0 * stepX, 0, false);
            AddHex(canvas, W, H, r, "\U0001F310", "REMOTE BACKUP LOGS", "MES Backup disc access needed", 1 * stepX, 0,
                false);
            AddHex(canvas, W, H, r, "\U0001F9EA", "SAMPLE DATA", sampleOk ? "Demo data ready" : "Not available",
                2 * stepX, 0, !sampleOk);
            AddHex(canvas, W, H, r, "\u21BB", "RECENT DATA", "Last loaded stations", rowOff + 0 * stepX, stepY, false);
            AddHex(canvas, W, H, r, "\u2715", "EXIT", "Close application", rowOff + 1 * stepX, stepY, false,
                isExit: true);

            return canvas;
        }

        private void AddHex(Canvas canvas,
            double W, double H, double r,
            string icon, string title, string sub,
            double left, double top,
            bool disabled, bool isExit = false)
        {
            Grid grid = new Grid
            {
                Width = W,
                Height = H,
                Cursor = disabled ? Cursors.Arrow : Cursors.Hand,
                Opacity = disabled ? 0.38 : 1.0,
                Tag = title
            };

            double cx = W / 2;
            double cy = H / 2;

            Polygon outer = new Polygon
            {
                Fill = new SolidColorBrush(HexFill),
                Stroke = new SolidColorBrush(HexFill),
                StrokeThickness = 0.3
            };
            Polygon inner = new Polygon
            {
                Fill = new SolidColorBrush(HexFill),
                Stroke = new SolidColorBrush(HexStroke),
                StrokeThickness = 5
            };

            for (int i = 0; i < 6; i++)
            {
                double angle = Math.PI / 180.0 * (60 * i - 90);
                double rIn = r * 0.93;
                outer.Points.Add(new Point(cx + r * Math.Cos(angle), cy + r * Math.Sin(angle)));
                inner.Points.Add(new Point(cx + rIn * Math.Cos(angle), cy + rIn * Math.Sin(angle)));
            }

            grid.Children.Add(outer);
            grid.Children.Add(inner);

            StackPanel stack = new StackPanel
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
                case "SAMPLE DATA":
                    SelectedPath = SampleDataPath;
                    Mode = StartupMode.Sample;
                    SaveRecentPath(SelectedPath);
                    WindowAnimations.FadeOutAndClose(this, true);
                    break;
                case "LOCAL FOLDER": ExpandHex(title, BuildLocalFolderContent()); break;
                case "REMOTE BACKUP LOGS": ExpandHex(title, BuildRemoteContent()); break;
                case "RECENT DATA": ExpandHex(title, BuildRecentContent()); break;
            }
        }

        private void ExpandHex(string title, UIElement content)
        {
            if (_isExpanded) return;
            _isExpanded = true;

            foreach (UIElement child in _canvas.Children)
                if (child is Grid g && g.Tag?.ToString() != title)
                    AnimateOpacity(g, 1.0, 0.15, 200);

            _expandedPanel.Children.Clear();
            _expandedPanel.Children.Add(content);
            _expandedPanel.Opacity = 0;
            _expandedPanel.Visibility = Visibility.Visible;
            AnimateOpacity(_expandedPanel, 0, 1, 280);
        }

        private void CollapseBack()
        {
            if (!_isExpanded) return;

            AnimateOpacity(_expandedPanel, 1, 0, 200, () =>
            {
                _expandedPanel.Visibility = Visibility.Collapsed;
                _expandedPanel.Children.Clear();
            });

            foreach (UIElement child in _canvas.Children)
                if (child is Grid g)
                    AnimateOpacity(g, g.Opacity, 1.0, 250);

            _isExpanded = false;
        }

        private static void AnimateOpacity(UIElement el, double from, double to, int ms, Action onComplete = null)
        {
            DoubleAnimation anim = new DoubleAnimation(from, to, TimeSpan.FromMilliseconds(ms))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };
            if (onComplete != null)
                anim.Completed += (s, e) => onComplete();
            el.BeginAnimation(UIElement.OpacityProperty, anim);
        }

        private UIElement BuildBackButton()
        {
            Border btn = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(14, 40, 20)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(180, 80, 10)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12, 6, 16, 6),
                Cursor = Cursors.Hand,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 0, 0, 16)
            };
            StackPanel row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(new TextBlock
            {
                Text = "\u2190",
                FontSize = 16,
                Foreground = new SolidColorBrush(Color.FromRgb(240, 160, 50)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            });
            row.Children.Add(new TextBlock
            {
                Text = "Back",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(240, 200, 140)),
                VerticalAlignment = VerticalAlignment.Center
            });
            btn.Child = row;
            btn.MouseEnter += (s, e) => btn.Background = new SolidColorBrush(Color.FromRgb(24, 60, 30));
            btn.MouseLeave += (s, e) => btn.Background = new SolidColorBrush(Color.FromRgb(14, 40, 20));
            btn.MouseLeftButtonUp += (s, e) => CollapseBack();
            return btn;
        }

        private UIElement WrapExpanded(string title, UIElement inner)
        {
            Grid root = new Grid { Margin = new Thickness(36, 28, 36, 28) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            Grid.SetRow(BuildBackButton(), 0);
            root.Children.Add(BuildBackButton());

            TextBlock titleBlock = new TextBlock
            {
                Text = title,
                FontSize = 20,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(255, 210, 140)),
                Margin = new Thickness(0, 0, 0, 20)
            };
            Grid.SetRow(titleBlock, 1);
            root.Children.Add(titleBlock);

            Grid.SetRow(inner, 2);
            root.Children.Add(inner);

            return root;
        }

        private UIElement BuildLocalFolderContent()
        {
            StackPanel stack = new StackPanel();

            Action<string> pick = startPath =>
            {
                System.Windows.Forms.FolderBrowserDialog browser = new System.Windows.Forms.FolderBrowserDialog
                {
                    Description = "Select logs folder",
                    SelectedPath = startPath,
                    ShowNewFolderButton = false
                };
                if (browser.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
                SelectedPath = browser.SelectedPath;
                Mode = StartupMode.Local;
                SaveRecentPath(SelectedPath);
                WindowAnimations.FadeOutAndClose(this, true);
            };

            stack.Children.Add(MakeTile("\U0001F4C1", "Browse Folder",
                "Select any local or mapped network folder",
                () => pick(Environment.GetFolderPath(Environment.SpecialFolder.Desktop))));

            stack.Children.Add(new TextBlock
            {
                Text = "\u2014 or enter path manually \u2014",
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(120, 150, 130)),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 16, 0, 10)
            });

            TextBox pathBox = new TextBox
            {
                Background = new SolidColorBrush(Color.FromRgb(12, 30, 18)),
                Foreground = new SolidColorBrush(Color.FromRgb(200, 240, 210)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(40, 100, 55)),
                BorderThickness = new Thickness(1),
                FontSize = 11,
                Padding = new Thickness(10, 8, 10, 8),
                Text = "",
                VerticalContentAlignment = VerticalAlignment.Center
            };
            stack.Children.Add(pathBox);

            Border btnGo = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(160, 80, 10)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(220, 120, 30)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(18, 8, 18, 8),
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 10, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            btnGo.Child = new TextBlock
            {
                Text = "Open  \u2192",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(255, 230, 180))
            };
            btnGo.MouseEnter += (s, e) => btnGo.Background = new SolidColorBrush(Color.FromRgb(190, 100, 15));
            btnGo.MouseLeave += (s, e) => btnGo.Background = new SolidColorBrush(Color.FromRgb(160, 80, 10));
            btnGo.MouseLeftButtonUp += (s, e) =>
            {
                string typed = pathBox.Text.Trim();
                if (!string.IsNullOrEmpty(typed) && Directory.Exists(typed))
                {
                    SelectedPath = typed;
                    Mode = StartupMode.Local;
                    SaveRecentPath(SelectedPath);
                    WindowAnimations.FadeOutAndClose(this, true);
                }
                else if (!string.IsNullOrEmpty(typed))
                    pathBox.BorderBrush = new SolidColorBrush(Color.FromRgb(220, 60, 40));
            };
            stack.Children.Add(btnGo);

            return WrapExpanded("\U0001F4C2  Local Folder", stack);
        }

        private UIElement BuildRemoteContent()
        {
            List<LineNode> allLines = LoadStationCache();
            bool hasCached = allLines.Count > 0;

            Grid root = new Grid { Margin = new Thickness(36, 28, 36, 28) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            UIElement backBtn = BuildBackButton();
            Grid.SetRow(backBtn, 0);
            root.Children.Add(backBtn);

            Grid titleRow = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            titleRow.Children.Add(new TextBlock
            {
                Text = "\U0001F310  Remote Backup Logs",
                FontSize = 20,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(255, 210, 140)),
                VerticalAlignment = VerticalAlignment.Center
            });

            TextBlock refreshSpinLabel = new TextBlock
            {
                Text = "\u21BB",
                FontSize = 13,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 220, 140)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0),
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = new System.Windows.Media.RotateTransform(0)
            };
            Border btnRefresh = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(18, 55, 30)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(50, 130, 75)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(12, 5, 14, 5),
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center
            };
            StackPanel btnContent = new StackPanel { Orientation = Orientation.Horizontal };
            btnContent.Children.Add(refreshSpinLabel);
            btnContent.Children.Add(new TextBlock
            {
                Text = "Refresh structure",
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(150, 230, 175)),
                VerticalAlignment = VerticalAlignment.Center
            });
            btnRefresh.Child = btnContent;
            btnRefresh.MouseEnter += (s, e) => btnRefresh.Background = new SolidColorBrush(Color.FromRgb(26, 80, 44));
            btnRefresh.MouseLeave += (s, e) => btnRefresh.Background = new SolidColorBrush(Color.FromRgb(18, 55, 30));
            Grid.SetColumn(btnRefresh, 1);
            titleRow.Children.Add(btnRefresh);
            Grid.SetRow(titleRow, 1);
            root.Children.Add(titleRow);

            Border browseTile = MakeTile("\U0001F4C2", "Browse folder",
                ResolveRemotePath(),
                () =>
                {
                    System.Windows.Forms.FolderBrowserDialog browser = new System.Windows.Forms.FolderBrowserDialog
                    {
                        Description = "Select a specific station folder (e.g. OHD0179N)",
                        SelectedPath = ResolveRemotePath(),
                        ShowNewFolderButton = false
                    };
                    if (browser.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
                    SelectedPath = browser.SelectedPath;
                    Mode = StartupMode.Remote;
                    SaveRecentPath(SelectedPath);
                    WindowAnimations.FadeOutAndClose(this, true);
                });
            Grid.SetRow(browseTile, 2);
            root.Children.Add(browseTile);

            TextBox searchBox = new TextBox
            {
                Background = new SolidColorBrush(Color.FromRgb(10, 28, 16)),
                Foreground = new SolidColorBrush(Color.FromRgb(200, 240, 210)),
                CaretBrush = new SolidColorBrush(Color.FromRgb(100, 220, 140)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(40, 100, 55)),
                BorderThickness = new Thickness(1),
                FontSize = 12,
                Padding = new Thickness(10, 7, 10, 7),
                Margin = new Thickness(0, 8, 0, 6),
                Text = ""
            };

            TextBlock searchPlaceholder = new TextBlock
            {
                Text = "  \uD83D\uDD0D  Search stations...",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(80, 120, 90)),
                IsHitTestVisible = false,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(10, 0, 0, 0)
            };

            Grid searchHost = new Grid { Margin = new Thickness(0, 8, 0, 6) };
            searchHost.Children.Add(searchBox);
            searchHost.Children.Add(searchPlaceholder);
            searchBox.TextChanged += (s, e) =>
                searchPlaceholder.Visibility = string.IsNullOrEmpty(searchBox.Text)
                    ? Visibility.Visible
                    : Visibility.Collapsed;

            Grid treeSection = new Grid();
            treeSection.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            treeSection.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(300) });

            ScrollViewer treeScroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Background = new SolidColorBrush(Color.FromRgb(8, 20, 12)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(30, 70, 40)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(0)
            };

            StackPanel treeStack = new StackPanel();
            treeScroll.Content = treeStack;
            Grid.SetColumn(treeScroll, 0);
            treeSection.Children.Add(treeScroll);

            ScrollViewer rawScrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                Background = new SolidColorBrush(Color.FromRgb(5, 14, 8)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(30, 70, 40)),
                BorderThickness = new Thickness(0, 1, 1, 1),
                Padding = new Thickness(0)
            };

            TextBlock rawText = new TextBlock
            {
                Foreground = new SolidColorBrush(Color.FromRgb(100, 160, 120)),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 10,
                TextWrapping = TextWrapping.NoWrap,
                Padding = new Thickness(10, 8, 10, 8),
                Text = hasCached ? "" : "Click \u21BB Refresh to scan..."
            };
            rawScrollViewer.Content = rawText;
            Grid.SetColumn(rawScrollViewer, 1);
            treeSection.Children.Add(rawScrollViewer);

            Grid bottomGrid = new Grid();
            bottomGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            bottomGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            Grid.SetRow(bottomGrid, 3);

            StackPanel searchAndStatus = new StackPanel();
            searchAndStatus.Children.Add(searchHost);

            StackPanel filterRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 4)
            };
            filterRow.Children.Add(new TextBlock
            {
                Text = "Show:",
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(80, 120, 90)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            });

            bool showLcs = false;
            bool showBackflush = false;

            Border MakeFilterToggle(string label, System.Action<bool> onToggle)
            {
                bool active = false;
                Border toggle = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(14, 35, 20)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(35, 80, 45)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(10, 3, 10, 3),
                    Margin = new Thickness(0, 0, 6, 0),
                    Cursor = Cursors.Hand
                };
                TextBlock toggleText = new TextBlock
                {
                    Text = label,
                    FontSize = 10,
                    Foreground = new SolidColorBrush(Color.FromRgb(70, 110, 80))
                };
                toggle.Child = toggleText;
                toggle.MouseEnter += (s, e) =>
                {
                    if (!active) toggle.Background = new SolidColorBrush(Color.FromRgb(20, 50, 28));
                };
                toggle.MouseLeave += (s, e) =>
                {
                    if (!active) toggle.Background = new SolidColorBrush(Color.FromRgb(14, 35, 20));
                };
                toggle.MouseLeftButtonUp += (s, e) =>
                {
                    active = !active;
                    toggle.Background =
                        new SolidColorBrush(active ? Color.FromRgb(22, 65, 35) : Color.FromRgb(14, 35, 20));
                    toggle.BorderBrush =
                        new SolidColorBrush(active ? Color.FromRgb(60, 160, 90) : Color.FromRgb(35, 80, 45));
                    toggleText.Foreground =
                        new SolidColorBrush(active ? Color.FromRgb(130, 220, 155) : Color.FromRgb(70, 110, 80));
                    onToggle(active);
                };
                return toggle;
            }

            searchAndStatus.Children.Add(filterRow);

            Grid.SetRow(searchAndStatus, 0);
            bottomGrid.Children.Add(searchAndStatus);

            Grid.SetRow(treeSection, 1);
            bottomGrid.Children.Add(treeSection);
            root.Children.Add(bottomGrid);

            HashSet<string> selectedPaths = new HashSet<string>();

            Border loadBtn = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(22, 100, 50)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(50, 180, 90)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(20, 8, 20, 8),
                Cursor = Cursors.Hand,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 6, 0, 6),
                Visibility = Visibility.Collapsed
            };
            TextBlock loadBtnText = new TextBlock
            {
                Text = "Load selected  \u2192",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(180, 245, 205))
            };
            loadBtn.Child = loadBtnText;
            loadBtn.MouseEnter += (s, e) => loadBtn.Background = new SolidColorBrush(Color.FromRgb(30, 130, 65));
            loadBtn.MouseLeave += (s, e) => loadBtn.Background = new SolidColorBrush(Color.FromRgb(22, 100, 50));
            loadBtn.MouseLeftButtonUp += (s, e) =>
            {
                if (selectedPaths.Count == 0) return;
                SelectedPath = selectedPaths.First();
                SelectedPaths = selectedPaths.ToList();
                Mode = StartupMode.Remote;

                string first = selectedPaths.First();

                if (selectedPaths.Count == 1)
                {
                    SaveRecentPath(first);
                }
                else
                {
                    string commonParent = first;
                    foreach (string p in selectedPaths)
                    {
                        while (!p.StartsWith(commonParent, StringComparison.OrdinalIgnoreCase)
                               && commonParent.Length > 3)
                            commonParent = System.IO.Path.GetDirectoryName(commonParent) ?? commonParent;
                    }

                    SaveRecentPath(commonParent);
                }

                WindowAnimations.FadeOutAndClose(this, true);
            };
            searchAndStatus.Children.Add(loadBtn);

            System.Action updateLoadBtn = () =>
            {
                loadBtn.Visibility = selectedPaths.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
                loadBtnText.Text = selectedPaths.Count == 1
                    ? "Load selected  \u2192"
                    : "Load " + selectedPaths.Count + " stations  \u2192";
            };

            System.Action<Border, TextBlock, string, bool> applySelection = (border, nameBlock, path, selected) =>
            {
                if (selected)
                {
                    border.Background = new SolidColorBrush(Color.FromRgb(20, 80, 35));
                    border.BorderBrush = new SolidColorBrush(Color.FromRgb(50, 180, 90));
                    border.BorderThickness = new Thickness(1);
                    nameBlock.Foreground = new SolidColorBrush(Color.FromRgb(140, 255, 170));
                    nameBlock.FontWeight = FontWeights.SemiBold;
                }
                else
                {
                    border.Background = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
                    border.BorderBrush = null;
                    border.BorderThickness = new Thickness(0);
                    nameBlock.Foreground = new SolidColorBrush(Color.FromRgb(190, 235, 205));
                    nameBlock.FontWeight = FontWeights.Normal;
                }
            };

            System.Action<List<LineNode>> renderTree = (lines) =>
            {
                treeStack.Children.Clear();
                string filter = searchBox.Text.Trim().ToLowerInvariant();

                foreach (LineNode line in lines)
                {
                    var matchingComps = line.Computers
                        .Select(c => new
                        {
                            Comp = c,
                            Stations = c.Stations.Where(st =>
                            {
                                if (st.Category == StationCategory.LCS && !showLcs) return false;
                                if (st.Category == StationCategory.Backflush && !showBackflush) return false;
                                return string.IsNullOrEmpty(filter) ||
                                       st.Name.ToLowerInvariant().Contains(filter) ||
                                       c.Name.ToLowerInvariant().Contains(filter) ||
                                       line.Name.ToLowerInvariant().Contains(filter);
                            }).ToList()
                        })
                        .Where(x => x.Stations.Count > 0)
                        .ToList();

                    if (matchingComps.Count == 0) continue;

                    List<(Border border, TextBlock nameBlock, string path)> allStBordersInLine
                        = new List<(Border, TextBlock, string)>();

                    StackPanel lineSection = new StackPanel { Margin = new Thickness(0, 0, 0, 2) };

                    Border lineHeader = new Border
                    {
                        Background = new SolidColorBrush(Color.FromRgb(25, 65, 38)),
                        BorderBrush = new SolidColorBrush(Color.FromRgb(50, 120, 70)),
                        BorderThickness = new Thickness(0, 0, 0, 1),
                        Padding = new Thickness(8, 5, 8, 5),
                        Cursor = Cursors.Hand
                    };
                    TextBlock lineNameBlock = new TextBlock
                    {
                        Text = "\u25B6  " + line.Name,
                        FontSize = 12,
                        FontWeight = FontWeights.Bold,
                        Foreground = new SolidColorBrush(Color.FromRgb(255, 210, 140))
                    };
                    lineHeader.Child = lineNameBlock;
                    lineHeader.MouseEnter += (s, e) =>
                        lineHeader.Background = new SolidColorBrush(Color.FromRgb(35, 90, 50));
                    lineHeader.MouseLeave += (s, e) =>
                        lineHeader.Background = new SolidColorBrush(Color.FromRgb(25, 65, 38));
                    lineHeader.MouseLeftButtonUp += (s, e) =>
                    {
                        bool anySelected = allStBordersInLine.Any(x => selectedPaths.Contains(x.path));
                        foreach ((Border b, TextBlock nb, string p) in allStBordersInLine)
                        {
                            if (anySelected) selectedPaths.Remove(p);
                            else selectedPaths.Add(p);
                            applySelection(b, nb, p, selectedPaths.Contains(p));
                        }

                        updateLoadBtn();
                    };
                    lineSection.Children.Add(lineHeader);

                    foreach (var item in matchingComps)
                    {
                        ComputerNode comp = item.Comp;
                        List<(Border, TextBlock, string)> compStBorders = new List<(Border, TextBlock, string)>();

                        Border compHeader = new Border
                        {
                            Padding = new Thickness(18, 3, 8, 3),
                            Cursor = Cursors.Hand,
                            Background = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0))
                        };
                        TextBlock compNameBlock = new TextBlock
                        {
                            Text = "  \u251C\u2500 \U0001F4BB  " + comp.Name,
                            FontSize = 11,
                            FontWeight = FontWeights.SemiBold,
                            Foreground = new SolidColorBrush(Color.FromRgb(120, 200, 150)),
                            Margin = new Thickness(0, 2, 0, 1)
                        };
                        compHeader.Child = compNameBlock;
                        compHeader.MouseEnter += (s, e) =>
                            compHeader.Background = new SolidColorBrush(Color.FromRgb(15, 40, 22));
                        compHeader.MouseLeave += (s, e) =>
                            compHeader.Background = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
                        compHeader.MouseLeftButtonUp += (s, e) =>
                        {
                            bool anySelected = compStBorders.Any(x => selectedPaths.Contains(x.Item3));
                            foreach ((Border b, TextBlock nb, string p) in compStBorders)
                            {
                                if (anySelected) selectedPaths.Remove(p);
                                else selectedPaths.Add(p);
                                applySelection(b, nb, p, selectedPaths.Contains(p));
                            }

                            updateLoadBtn();
                        };
                        lineSection.Children.Add(compHeader);

                        for (int si = 0; si < item.Stations.Count; si++)
                        {
                            StationNode st = item.Stations[si];
                            bool lastS = si == item.Stations.Count - 1;
                            string capturedPath = st.FullPath;
                            bool isSelected = selectedPaths.Contains(capturedPath);

                            Border stBorder = new Border
                            {
                                Padding = new Thickness(4, 2, 6, 2),
                                Margin = new Thickness(0, 0, 0, 1),
                                CornerRadius = new CornerRadius(3),
                                Background =
                                    new SolidColorBrush(isSelected
                                        ? Color.FromRgb(20, 80, 35)
                                        : Color.FromArgb(0, 0, 0, 0)),
                                BorderBrush = isSelected ? new SolidColorBrush(Color.FromRgb(50, 180, 90)) : null,
                                BorderThickness = isSelected ? new Thickness(1) : new Thickness(0),
                                Cursor = Cursors.Hand
                            };
                            StackPanel stRow = new StackPanel { Orientation = Orientation.Horizontal };
                            stRow.Children.Add(new TextBlock
                            {
                                Text = lastS ? "      \u2514\u2500 " : "      \u251C\u2500 ",
                                FontFamily = new FontFamily("Consolas"),
                                FontSize = 11,
                                Foreground = new SolidColorBrush(Color.FromRgb(50, 100, 65))
                            });
                            bool isSpecial = st.Category == StationCategory.LCS ||
                                             st.Category == StationCategory.Backflush;
                            Color normalColor = isSpecial
                                ? Color.FromRgb(130, 160, 140)
                                : Color.FromRgb(190, 235, 205);
                            TextBlock stName = new TextBlock
                            {
                                Text = st.Name,
                                FontSize = 11,
                                FontWeight = isSelected ? FontWeights.SemiBold : FontWeights.Normal,
                                Foreground =
                                    new SolidColorBrush(isSelected ? Color.FromRgb(140, 255, 170) : normalColor),
                                Opacity = isSpecial ? 0.65 : 1.0
                            };
                            stRow.Children.Add(stName);
                            if (isSpecial)
                                stRow.Children.Add(new TextBlock
                                {
                                    Text = "  [" + st.Category + "]",
                                    FontSize = 9,
                                    Foreground = new SolidColorBrush(Color.FromRgb(80, 130, 100)),
                                    VerticalAlignment = VerticalAlignment.Center,
                                    Opacity = 0.7
                                });
                            stRow.Children.Add(new TextBlock
                            {
                                Text = "  " + capturedPath,
                                FontSize = 9,
                                FontFamily = new FontFamily("Consolas"),
                                Foreground = new SolidColorBrush(Color.FromRgb(60, 100, 75)),
                                VerticalAlignment = VerticalAlignment.Center
                            });
                            stBorder.Child = stRow;

                            stBorder.MouseEnter += (s, e) =>
                            {
                                if (!selectedPaths.Contains(capturedPath))
                                    stBorder.Background = new SolidColorBrush(Color.FromRgb(18, 50, 28));
                            };
                            stBorder.MouseLeave += (s, e) =>
                            {
                                if (!selectedPaths.Contains(capturedPath))
                                {
                                    stBorder.Background = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
                                    stName.Foreground = new SolidColorBrush(normalColor);
                                }
                            };
                            stBorder.MouseLeftButtonUp += (s, e) =>
                            {
                                if (selectedPaths.Contains(capturedPath))
                                    selectedPaths.Remove(capturedPath);
                                else
                                    selectedPaths.Add(capturedPath);
                                applySelection(stBorder, stName, capturedPath, selectedPaths.Contains(capturedPath));
                                updateLoadBtn();
                            };

                            compStBorders.Add((stBorder, stName, capturedPath));
                            allStBordersInLine.Add((stBorder, stName, capturedPath));
                            lineSection.Children.Add(stBorder);
                        }
                    }

                    treeStack.Children.Add(lineSection);
                }
            };

            filterRow.Children.Add(MakeFilterToggle("LCS", v =>
            {
                showLcs = v;
                renderTree(allLines);
            }));
            filterRow.Children.Add(MakeFilterToggle("Backflush", v =>
            {
                showBackflush = v;
                renderTree(allLines);
            }));

            if (hasCached)
            {
                rawText.Text = BuildRawTreeText(ResolveRemotePath(), allLines);
                renderTree(allLines);
            }

            searchBox.TextChanged += (s, e) => renderTree(allLines);

            bool isRefreshing = false;
            btnRefresh.MouseLeftButtonUp += (s, e) =>
            {
                if (isRefreshing) return;
                isRefreshing = true;
                rawText.Text = "Scanning...";
                treeStack.Children.Clear();
                DoubleAnimation spinAnim = new DoubleAnimation(0, 360, TimeSpan.FromSeconds(1))
                    { RepeatBehavior = RepeatBehavior.Forever };
                refreshSpinLabel.RenderTransform.BeginAnimation(System.Windows.Media.RotateTransform.AngleProperty,
                    spinAnim);

                Task.Run(() =>
                {
                    string remotePath = ResolveRemotePath();
                    List<LineNode> freshLines = ScanLineStructure(remotePath);
                    string raw = BuildRawTreeText(remotePath, freshLines);
                    Dispatcher.Invoke(() =>
                    {
                        refreshSpinLabel.RenderTransform.BeginAnimation(
                            System.Windows.Media.RotateTransform.AngleProperty, null);
                        allLines.Clear();
                        allLines.AddRange(freshLines);
                        rawText.Text = raw;
                        renderTree(allLines);
                        SaveStationCache(allLines);
                        isRefreshing = false;
                    });
                });
            };

            return root;
        }

        private static List<RecentEntry> LoadRecentEntries()
        {
            List<RecentEntry> result = new List<RecentEntry>();
            try
            {
                if (!File.Exists(RecentPathFile)) return result;
                RecentEntry current = null;
                foreach (string line in File.ReadAllLines(RecentPathFile))
                {
                    if (line.StartsWith("P:"))
                    {
                        current = new RecentEntry { Path = line.Substring(2) };
                        result.Add(current);
                    }
                    else if (line.StartsWith("  S:") && current != null)
                    {
                        string[] parts = line.Substring(4).Split(new[] { '|' }, 2);
                        if (parts.Length == 2)
                            current.Stations.Add((parts[0], parts[1]));
                    }
                }
            }
            catch
            {
            }

            return result;
        }


        private UIElement BuildRecentContent()
        {
            List<RecentEntry> entries = LoadRecentEntries();
            StackPanel stack = new StackPanel();

            if (entries.Count == 0)
            {
                stack.Children.Add(new TextBlock
                {
                    Text = "No recent data found.\nLoad a folder first and it will appear here.",
                    FontSize = 12,
                    Foreground = new SolidColorBrush(Color.FromRgb(140, 160, 140)),
                    TextWrapping = TextWrapping.Wrap
                });
                return WrapExpanded("\u21BB  Recent Data", stack);
            }

            Dictionary<string, bool> existsMap = new Dictionary<string, bool>();
            System.Threading.Tasks.Parallel.ForEach(entries, e =>
            {
                bool ex = Directory.Exists(e.Path);
                lock (existsMap) existsMap[e.Path] = ex;
            });

            ScrollViewer scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
            StackPanel inner = new StackPanel();

            foreach (RecentEntry entry in entries)
            {
                bool exists = existsMap.ContainsKey(entry.Path) && existsMap[entry.Path];
                string folderName = System.IO.Path.GetFileName(entry.Path.TrimEnd('\\', '/'));

                Border row = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(12, 30, 18)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(30, 70, 40)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(14, 10, 14, 10),
                    Margin = new Thickness(0, 0, 0, 6)
                };

                StackPanel rowStack = new StackPanel();

                rowStack.Children.Add(new TextBlock
                {
                    Text = folderName,
                    FontSize = 12,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(exists
                        ? Color.FromRgb(200, 240, 210)
                        : Color.FromRgb(100, 110, 100))
                });
                rowStack.Children.Add(new TextBlock
                {
                    Text = entry.Path,
                    FontSize = 10,
                    FontFamily = new FontFamily("Consolas"),
                    Foreground = new SolidColorBrush(exists
                        ? Color.FromRgb(70, 120, 85)
                        : Color.FromRgb(70, 70, 70)),
                    TextWrapping = TextWrapping.Wrap
                });

                if (!exists)
                {
                    rowStack.Children.Add(new TextBlock
                    {
                        Text = "\u26A0  Path not accessible",
                        FontSize = 9,
                        Foreground = new SolidColorBrush(Color.FromRgb(160, 120, 50))
                    });
                }
                else if (entry.Stations.Count > 0)
                {
                    StackPanel stationPanel = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
                    List<(CheckBox cb, string folderPath)> checkboxes = new List<(CheckBox, string)>();

                    foreach ((string name, string folderPath) in entry.Stations)
                    {
                        CheckBox cb = new CheckBox
                        {
                            IsChecked = true,
                            Margin = new Thickness(0, 2, 0, 2)
                        };
                        cb.Content = new TextBlock
                        {
                            Text = name,
                            FontSize = 11,
                            Foreground = new SolidColorBrush(Color.FromRgb(160, 210, 175))
                        };
                        checkboxes.Add((cb, folderPath));
                        stationPanel.Children.Add(cb);
                    }

                    rowStack.Children.Add(stationPanel);

                    Button btnLoad = new Button
                    {
                        Content = "Load selected →",
                        FontSize = 11,
                        Padding = new Thickness(12, 6, 12, 6),
                        Margin = new Thickness(0, 8, 0, 0),
                        Background = new SolidColorBrush(Color.FromRgb(22, 100, 50)),
                        Foreground = new SolidColorBrush(Color.FromRgb(180, 245, 205)),
                        BorderBrush = new SolidColorBrush(Color.FromRgb(50, 180, 90)),
                        BorderThickness = new Thickness(1),
                        Cursor = Cursors.Hand
                    };
                    string capturedPath = entry.Path;
                    btnLoad.Click += (s, e) =>
                    {
                        List<string> selected = checkboxes
                            .Where(x => x.cb.IsChecked == true)
                            .Select(x => x.folderPath)
                            .ToList();

                        if (selected.Count == 0) return;

                        SelectedPaths = selected;
                        SelectedPath = capturedPath;
                        Mode = StartupMode.Local;
                        WindowAnimations.FadeOutAndClose(this, true);
                    };
                    rowStack.Children.Add(btnLoad);
                }
                else
                {
                    row.Cursor = Cursors.Hand;
                    row.MouseEnter += (s, e) => row.Background = new SolidColorBrush(Color.FromRgb(18, 50, 26));
                    row.MouseLeave += (s, e) => row.Background = new SolidColorBrush(Color.FromRgb(12, 30, 18));
                    string capturedPath = entry.Path;
                    row.MouseLeftButtonUp += (s, e) =>
                    {
                        SelectedPath = capturedPath;
                        Mode = StartupMode.Local;
                        WindowAnimations.FadeOutAndClose(this, true);
                    };
                }

                row.Child = rowStack;
                inner.Children.Add(row);
            }

            scroll.Content = inner;
            stack.Children.Add(scroll);
            return WrapExpanded("\u21BB  Recent Data", stack);
        }


        private UIElement BuildStationsContent()
        {
            Grid root = new Grid { Margin = new Thickness(36, 28, 36, 28) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            Grid.SetRow(BuildBackButton(), 0);
            root.Children.Add(BuildBackButton());

            Grid titleRow = new Grid { Margin = new Thickness(0, 0, 0, 12) };
            titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            TextBlock titleBlock = new TextBlock
            {
                Text = "🏭  Stations / Lines",
                FontSize = 20,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(255, 210, 140)),
                VerticalAlignment = VerticalAlignment.Center
            };
            titleRow.Children.Add(titleBlock);

            Border btnRefresh = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(22, 70, 40)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(60, 160, 90)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(14, 6, 14, 6),
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            StackPanel btnRefreshContent = new StackPanel { Orientation = Orientation.Horizontal };
            TextBlock refreshSpinLabel = new TextBlock
            {
                Text = "↻",
                FontSize = 14,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 220, 140)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0)
            };
            btnRefreshContent.Children.Add(refreshSpinLabel);
            btnRefreshContent.Children.Add(new TextBlock
            {
                Text = "Refresh",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(160, 240, 190)),
                VerticalAlignment = VerticalAlignment.Center
            });
            btnRefresh.Child = btnRefreshContent;
            btnRefresh.MouseEnter += (s, e) => btnRefresh.Background = new SolidColorBrush(Color.FromRgb(30, 100, 55));
            btnRefresh.MouseLeave += (s, e) => btnRefresh.Background = new SolidColorBrush(Color.FromRgb(22, 70, 40));
            Grid.SetColumn(btnRefresh, 1);
            titleRow.Children.Add(btnRefresh);

            Grid.SetRow(titleRow, 1);
            root.Children.Add(titleRow);

            Grid mainContent = new Grid();
            mainContent.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            mainContent.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(320) });
            mainContent.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0) });

            Border treeContainer = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(8, 20, 12)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(30, 70, 40)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8, 0, 0, 8),
                Margin = new Thickness(0, 0, 0, 0)
            };

            TextBlock loadingText = new TextBlock
            {
                Text = "⏳  Scanning network structure...",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(180, 160, 100)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 40, 0, 40)
            };
            treeContainer.Child = loadingText;
            Grid.SetColumn(treeContainer, 0);
            mainContent.Children.Add(treeContainer);

            Border rawTextBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(5, 14, 8)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(30, 70, 40)),
                BorderThickness = new Thickness(1, 1, 1, 1),
                CornerRadius = new CornerRadius(0, 8, 8, 0),
                Margin = new Thickness(-1, 0, 0, 0)
            };
            TextBox rawText = new TextBox
            {
                Background = System.Windows.Media.Brushes.Transparent,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 160, 120)),
                BorderThickness = new Thickness(0),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 10,
                IsReadOnly = true,
                TextWrapping = TextWrapping.NoWrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                Padding = new Thickness(10, 8, 10, 8),
                Text = "Click ↻ Refresh to scan the folder structure..."
            };
            rawTextBorder.Child = rawText;
            Grid.SetColumn(rawTextBorder, 1);
            mainContent.Children.Add(rawTextBorder);

            Grid.SetRow(mainContent, 2);
            root.Children.Add(mainContent);

            bool isRefreshing = false;

            System.Action doRefresh = null;
            doRefresh = () =>
            {
                if (isRefreshing) return;
                isRefreshing = true;

                refreshSpinLabel.Text = "↻";
                loadingText.Text = "⏳  Scanning...";
                treeContainer.Child = loadingText;
                rawText.Text = "Scanning...";

                DoubleAnimation anim =
                    new System.Windows.Media.Animation.DoubleAnimation(0, 360, TimeSpan.FromSeconds(1))
                        { RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever };
                refreshSpinLabel.RenderTransformOrigin = new Point(0.5, 0.5);
                refreshSpinLabel.RenderTransform = new System.Windows.Media.RotateTransform(0);
                refreshSpinLabel.RenderTransform.BeginAnimation(System.Windows.Media.RotateTransform.AngleProperty,
                    anim);

                Task.Run(() =>
                {
                    string remotePath = ResolveRemotePath();
                    List<LineNode> lines = ScanLineStructure(remotePath);
                    string raw = BuildRawTreeText(remotePath, lines);

                    Dispatcher.Invoke(() =>
                    {
                        refreshSpinLabel.RenderTransform.BeginAnimation(
                            System.Windows.Media.RotateTransform.AngleProperty, null);
                        refreshSpinLabel.Text = "↻";
                        rawText.Text = raw;

                        if (lines.Count == 0)
                            loadingText.Text = "⚠  Could not connect or no stations found.";
                        else
                            treeContainer.Child = BuildStationTree(lines);

                        isRefreshing = false;
                    });
                });
            };

            btnRefresh.MouseLeftButtonUp += (s, e) => doRefresh();

            doRefresh();

            return root;
        }

        private class LineNode
        {
            public string Name { get; set; }
            public string FullPath { get; set; }
            public List<ComputerNode> Computers { get; set; } = new List<ComputerNode>();
        }

        private class ComputerNode
        {
            public string Name { get; set; }
            public string FullPath { get; set; }
            public List<StationNode> Stations { get; set; } = new List<StationNode>();
        }

        private class StationNode
        {
            public string Name { get; set; }
            public string FullPath { get; set; }
            public StationCategory Category { get; set; } = StationCategory.GHP;
        }

        private static List<LineNode> ScanLineStructure(string rootPath)
        {
            Dictionary<string, LineNode> lineMap = new Dictionary<string, LineNode>();
            try
            {
                List<StationInfo> stations = DataLoader.FindStations(rootPath);
                foreach (var st in stations)
                {
                    string lineName = string.IsNullOrEmpty(st.LineName) ? "(No Line)" : st.LineName;
                    string compName = string.IsNullOrEmpty(st.ComputerName) ? "(Unknown)" : st.ComputerName;

                    if (!lineMap.TryGetValue(lineName, out LineNode lineNode))
                    {
                        lineNode = new LineNode { Name = lineName, FullPath = rootPath };
                        lineMap[lineName] = lineNode;
                    }

                    ComputerNode comp = lineNode.Computers.FirstOrDefault(c => c.Name == compName);
                    if (comp == null)
                    {
                        comp = new ComputerNode { Name = compName, FullPath = st.FolderPath };
                        lineNode.Computers.Add(comp);
                    }

                    comp.Stations.Add(new StationNode
                        { Name = st.StationName, FullPath = st.FolderPath, Category = st.Category });
                }
            }
            catch
            {
            }

            return lineMap.Values.OrderBy(l => l.Name).ToList();
        }


        private static IEnumerable<string> GetStationDirs(string compDir)
        {
            List<string> result = new List<string>();
            try
            {
                foreach (string d in Directory.GetDirectories(compDir))
                {
                    if (HasLogFiles(d)) result.Add(d);
                    else
                        foreach (string sub in Directory.GetDirectories(d))
                            if (HasLogFiles(sub))
                                result.Add(sub);
                }
            }
            catch
            {
            }

            return result;
        }

        private static bool HasLogFiles(string dir)
        {
            try
            {
                return Directory.GetFiles(dir).Any(f =>
                {
                    string ext = System.IO.Path.GetExtension(f).ToLowerInvariant();
                    return ext == ".txt" || ext == ".log" || ext == ".zip";
                });
            }
            catch
            {
                return false;
            }
        }

        private static string BuildRawTreeText(string rootPath, List<LineNode> lines)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine(rootPath);
            for (int li = 0; li < lines.Count; li++)
            {
                LineNode line = lines[li];
                bool lastL = li == lines.Count - 1;
                sb.AppendLine((lastL ? "└── " : "├── ") + line.Name);
                string linePrefix = lastL ? "    " : "│   ";
                for (int ci = 0; ci < line.Computers.Count; ci++)
                {
                    ComputerNode comp = line.Computers[ci];
                    bool lastC = ci == line.Computers.Count - 1;
                    sb.AppendLine(linePrefix + (lastC ? "└── " : "├── ") + comp.Name);
                    string compPrefix = linePrefix + (lastC ? "    " : "│   ");
                    for (int si = 0; si < comp.Stations.Count; si++)
                    {
                        StationNode st = comp.Stations[si];
                        bool lastS = si == comp.Stations.Count - 1;
                        sb.AppendLine(compPrefix + (lastS ? "└── " : "├── ") + st.Name);
                    }
                }
            }

            return sb.ToString();
        }

        private UIElement BuildStationTree(List<LineNode> lines)
        {
            ScrollViewer scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Padding = new Thickness(10)
            };
            StackPanel mainStack = new StackPanel();

            foreach (var line in lines)
            {
                StackPanel lineSection = new StackPanel { Margin = new Thickness(0, 0, 0, 4) };

                Border lineHeader = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(25, 65, 38)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(50, 120, 70)),
                    BorderThickness = new Thickness(0, 0, 0, 1),
                    Padding = new Thickness(8, 5, 8, 5)
                };
                lineHeader.Child = new TextBlock
                {
                    Text = "▶  " + line.Name,
                    FontSize = 12,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Color.FromRgb(255, 210, 140))
                };
                lineSection.Children.Add(lineHeader);

                foreach (var comp in line.Computers)
                {
                    Border compRow = new Border
                    {
                        Padding = new Thickness(18, 3, 8, 3),
                        BorderBrush = new SolidColorBrush(Color.FromRgb(30, 65, 40)),
                        BorderThickness = new Thickness(0, 0, 0, 0)
                    };

                    StackPanel compStack = new StackPanel();
                    compStack.Children.Add(new TextBlock
                    {
                        Text = "├─ 💻  " + comp.Name,
                        FontSize = 11,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = new SolidColorBrush(Color.FromRgb(120, 200, 150)),
                        Margin = new Thickness(0, 2, 0, 1)
                    });

                    for (int si = 0; si < comp.Stations.Count; si++)
                    {
                        StationNode st = comp.Stations[si];
                        bool lastS = si == comp.Stations.Count - 1;
                        string capturedPath = st.FullPath;

                        Border stBorder = new Border
                        {
                            Padding = new Thickness(4, 2, 6, 2),
                            Margin = new Thickness(0, 0, 0, 1),
                            CornerRadius = new CornerRadius(3),
                            Background = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0)),
                            Cursor = Cursors.Hand
                        };

                        StackPanel stRow = new StackPanel { Orientation = Orientation.Horizontal };
                        stRow.Children.Add(new TextBlock
                        {
                            Text = lastS ? "    └─ " : "    ├─ ",
                            FontSize = 11,
                            FontFamily = new FontFamily("Consolas"),
                            Foreground = new SolidColorBrush(Color.FromRgb(50, 100, 65))
                        });
                        TextBlock stNameBlock = new TextBlock
                        {
                            Text = st.Name,
                            FontSize = 11,
                            Foreground = new SolidColorBrush(Color.FromRgb(190, 235, 205))
                        };
                        stRow.Children.Add(stNameBlock);

                        TextBlock pathBlock = new TextBlock
                        {
                            Text = "  " + capturedPath,
                            FontSize = 9,
                            FontFamily = new FontFamily("Consolas"),
                            Foreground = new SolidColorBrush(Color.FromRgb(60, 100, 75)),
                            VerticalAlignment = VerticalAlignment.Center
                        };
                        stRow.Children.Add(pathBlock);

                        stBorder.Child = stRow;
                        stBorder.MouseEnter += (s, e) =>
                        {
                            stBorder.Background = new SolidColorBrush(Color.FromRgb(18, 50, 28));
                            stNameBlock.Foreground = new SolidColorBrush(Color.FromRgb(255, 255, 255));
                        };
                        stBorder.MouseLeave += (s, e) =>
                        {
                            stBorder.Background = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
                            stNameBlock.Foreground = new SolidColorBrush(Color.FromRgb(190, 235, 205));
                        };
                        stBorder.MouseLeftButtonUp += (s, e) =>
                        {
                            SelectedPath = capturedPath;
                            Mode = StartupMode.Remote;
                            SaveRecentPath(SelectedPath);
                            WindowAnimations.FadeOutAndClose(this, true);
                        };
                        compStack.Children.Add(stBorder);
                    }

                    compRow.Child = compStack;
                    lineSection.Children.Add(compRow);
                }

                mainStack.Children.Add(lineSection);
            }

            scroll.Content = mainStack;
            return scroll;
        }

        private Border MakeTile(string icon, string title, string sub, Action onClick)
        {
            Border tile = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(14, 40, 22)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(160, 80, 10)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(20, 14, 20, 14),
                Margin = new Thickness(0, 0, 0, 10),
                Cursor = Cursors.Hand,
                MaxWidth = 500
            };
            StackPanel row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(new TextBlock
            {
                Text = icon,
                FontSize = 28,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 14, 0)
            });
            StackPanel texts = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            texts.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(255, 230, 180))
            });
            texts.Children.Add(new TextBlock
            {
                Text = sub,
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(160, 130, 80)),
                TextWrapping = TextWrapping.Wrap
            });
            row.Children.Add(texts);
            tile.Child = row;
            tile.MouseEnter += (s, e) => tile.Background = new SolidColorBrush(Color.FromRgb(22, 60, 30));
            tile.MouseLeave += (s, e) => tile.Background = new SolidColorBrush(Color.FromRgb(14, 40, 22));
            tile.MouseLeftButtonUp += (s, e) => onClick();
            return tile;
        }

        private static string RecentPathFile =>
            System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MESInsight", "recent.txt");

        private static string StationCacheFile =>
            System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MESInsight", "station_cache.txt");

        private static void SaveStationCache(List<LineNode> lines)
        {
            try
            {
                string dir = System.IO.Path.GetDirectoryName(StationCacheFile) ?? "";
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                foreach (var line in lines)
                {
                    sb.AppendLine("L:" + line.Name + "|" + line.FullPath);
                    foreach (var comp in line.Computers)
                    {
                        sb.AppendLine("C:" + comp.Name + "|" + comp.FullPath);
                        foreach (var st in comp.Stations)
                            sb.AppendLine("S:" + st.Name + "|" + st.FullPath + "|" + (int)st.Category);
                    }
                }

                File.WriteAllText(StationCacheFile, sb.ToString(), System.Text.Encoding.UTF8);
            }
            catch
            {
            }
        }

        private static List<LineNode> LoadStationCache()
        {
            List<LineNode> result = new List<LineNode>();
            try
            {
                if (!File.Exists(StationCacheFile)) return result;
                LineNode currentLine = null;
                ComputerNode currentComp = null;
                foreach (string raw in File.ReadAllLines(StationCacheFile, System.Text.Encoding.UTF8))
                {
                    if (raw.StartsWith("L:"))
                    {
                        string[] parts = raw.Substring(2).Split(new[] { '|' }, 2);
                        currentLine = new LineNode { Name = parts[0], FullPath = parts.Length > 1 ? parts[1] : "" };
                        currentComp = null;
                        result.Add(currentLine);
                    }
                    else if (raw.StartsWith("C:") && currentLine != null)
                    {
                        string[] parts = raw.Substring(2).Split(new[] { '|' }, 2);
                        currentComp = new ComputerNode { Name = parts[0], FullPath = parts.Length > 1 ? parts[1] : "" };
                        currentLine.Computers.Add(currentComp);
                    }
                    else if (raw.StartsWith("S:") && currentComp != null)
                    {
                        string[] parts = raw.Substring(2).Split(new[] { '|' }, 3);
                        StationCategory cat = StationCategory.GHP;
                        if (parts.Length > 2 && int.TryParse(parts[2], out int catInt))
                            cat = (StationCategory)catInt;
                        currentComp.Stations.Add(new StationNode
                        {
                            Name = parts[0],
                            FullPath = parts.Length > 1 ? parts[1] : "",
                            Category = cat
                        });
                    }
                }
            }
            catch
            {
            }

            return result;
        }

        public static void SaveRecentPath(string path, List<StationInfo> stations = null)
        {
            try
            {
                string dir = System.IO.Path.GetDirectoryName(RecentPathFile) ?? "";
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                List<string> lines = File.Exists(RecentPathFile)
                    ? File.ReadAllLines(RecentPathFile).ToList()
                    : new List<string>();

                int i = 0;
                while (i < lines.Count)
                {
                    if (lines[i] == "P:" + path)
                    {
                        lines.RemoveAt(i);
                        while (i < lines.Count && lines[i].StartsWith("  S:"))
                            lines.RemoveAt(i);
                    }
                    else i++;
                }

                List<string> newEntry = new List<string> { "P:" + path };
                if (stations != null)
                    foreach (StationInfo st in stations)
                        newEntry.Add("  S:" + st.StationName + "|" + st.FolderPath);

                lines.InsertRange(0, newEntry);

                int pCount = 0;
                int cutAt = lines.Count;
                for (int j = 0; j < lines.Count; j++)
                {
                    if (lines[j].StartsWith("P:")) pCount++;
                    if (pCount > 10)
                    {
                        cutAt = j;
                        break;
                    }
                }

                lines = lines.Take(cutAt).ToList();

                File.WriteAllLines(RecentPathFile, lines);
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

                StackPanel root = new StackPanel { Margin = new Thickness(20, 16, 20, 16) };
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

                    Border row = new Border
                    {
                        Background = new SolidColorBrush(Color.FromRgb(12, 26, 16)),
                        BorderBrush = new SolidColorBrush(Color.FromRgb(26, 70, 38)),
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(5),
                        Padding = new Thickness(12, 8, 12, 8),
                        Margin = new Thickness(0, 0, 0, 5),
                        Cursor = exists ? Cursors.Hand : Cursors.Arrow
                    };

                    StackPanel stack = new StackPanel();
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
                        Foreground =
                            new SolidColorBrush(exists ? Color.FromRgb(70, 120, 85) : Color.FromRgb(80, 80, 80)),
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
                            WindowAnimations.FadeOutAndClose(this, true);
                        };
                    }

                    root.Children.Add(row);
                }

                Button btnCancel = new Button
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

                StackPanel root = new StackPanel { Margin = new Thickness(24, 20, 24, 20) };
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

                CheckBox cbLcs = new CheckBox
                {
                    Content = "LCS  (" + lcsCount + " station" + (lcsCount != 1 ? "s" : "") + ")",
                    FontSize = 11,
                    Foreground = new SolidColorBrush(Color.FromRgb(180, 225, 195)),
                    IsEnabled = lcsCount > 0,
                    IsChecked = false,
                    Margin = new Thickness(0, 0, 0, 10)
                };
                CheckBox cbBackflush = new CheckBox
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

                StackPanel btnRow = new StackPanel
                    { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
                Button btnConfirm = new Button
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
                    WindowAnimations.FadeOutAndClose(this, true);
                };
                btnRow.Children.Add(btnConfirm);
                root.Children.Add(btnRow);
                Content = root;
            }
        }

        private class RecentEntry
        {
            public string Path { get; set; }

            public List<(string Name, string FolderPath)> Stations { get; set; } = new List<(string, string)>();
        }
    }
}