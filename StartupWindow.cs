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
            Width = 1000;
            Height = 860;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = new SolidColorBrush(BgColor);
            FontFamily = new FontFamily(new Uri("pack://application:,,,/"), "./Fonts/#Inter 18pt");
            Content = BuildLayout();
        }

        private UIElement BuildLayout()
        {
            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(60) });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(34) });

            Grid.SetRow(BuildHeader(), 0);
            root.Children.Add(BuildHeader());

            var centerGrid = new Grid();
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

            Grid.SetRow(BuildFooter(), 2);
            root.Children.Add(BuildFooter());

            return root;
        }

        private Border BuildHeader()
        {
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
                Text = "\U0001F4CA",
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
            return header;
        }

        private Border BuildFooter()
        {
            var footer = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(14, 55, 28)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(180, 80, 10)),
                BorderThickness = new Thickness(0, 5, 0, 0)
            };
            var footerGrid = new Grid { Margin = new Thickness(28, 0, 28, 0) };
            footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var left = new TextBlock
            {
                Text = "MES Insight v1.0 | \u00A9 2026",
                FontSize = 15,
                Foreground = new SolidColorBrush(Color.FromRgb(200, 130, 60)),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            var right = new TextBlock
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

            var canvas = new Canvas
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Width = rowOff + 2 * stepX + W + 0.1,
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
            var grid = new Grid
            {
                Width = W,
                Height = H,
                Cursor = disabled ? Cursors.Arrow : Cursors.Hand,
                Opacity = disabled ? 0.38 : 1.0,
                Tag = title
            };

            double cx = W / 2;
            double cy = H / 2;

            var outer = new Polygon
            {
                Fill = new SolidColorBrush(HexFill),
                Stroke = new SolidColorBrush(HexFill),
                StrokeThickness = 0.3
            };
            var inner = new Polygon
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
                case "SAMPLE DATA":
                    SelectedPath = SampleDataPath;
                    Mode = StartupMode.Sample;
                    SaveRecentPath(SelectedPath);
                    DialogResult = true;
                    break;
                case "LOCAL FOLDER": ExpandHex(title, BuildLocalFolderContent()); break;
                case "REMOTE BACKUP LOGS": ExpandHex(title, BuildRemoteContent()); break;
                case "RECENT DATA": ExpandHex(title, BuildRecentContent()); break;
                case "STATIONS / LINES": ExpandHex(title, BuildStationsContent()); break;
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
            var anim = new DoubleAnimation(from, to, TimeSpan.FromMilliseconds(ms))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };
            if (onComplete != null)
                anim.Completed += (s, e) => onComplete();
            el.BeginAnimation(UIElement.OpacityProperty, anim);
        }

        private UIElement BuildBackButton()
        {
            var btn = new Border
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
            var row = new StackPanel { Orientation = Orientation.Horizontal };
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
            var root = new Grid { Margin = new Thickness(36, 28, 36, 28) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            Grid.SetRow(BuildBackButton(), 0);
            root.Children.Add(BuildBackButton());

            var titleBlock = new TextBlock
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
            var stack = new StackPanel();

            Action<string> pick = startPath =>
            {
                var browser = new System.Windows.Forms.FolderBrowserDialog
                {
                    Description = "Select logs folder",
                    SelectedPath = startPath,
                    ShowNewFolderButton = false
                };
                if (browser.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
                SelectedPath = browser.SelectedPath;
                Mode = StartupMode.Local;
                SaveRecentPath(SelectedPath);
                DialogResult = true;
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

            var pathBox = new TextBox
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

            var btnGo = new Border
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
                    DialogResult = true;
                }
                else if (!string.IsNullOrEmpty(typed))
                    pathBox.BorderBrush = new SolidColorBrush(Color.FromRgb(220, 60, 40));
            };
            stack.Children.Add(btnGo);

            return WrapExpanded("\U0001F4C2  Local Folder", stack);
        }

        private UIElement BuildRemoteContent()
        {
            var stack = new StackPanel();
            stack.Children.Add(new TextBlock
            {
                Text = "Connecting to server...",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(180, 160, 120)),
                Margin = new Thickness(0, 0, 0, 12),
                TextWrapping = TextWrapping.Wrap
            });
            stack.Children.Add(MakeTile("\U0001F310", "Browse Remote Backup Logs",
                ResolveRemotePath(),
                () =>
                {
                    var browser = new System.Windows.Forms.FolderBrowserDialog
                    {
                        Description = "Select a specific station folder (e.g. OHD0179N)",
                        SelectedPath = ResolveRemotePath(),
                        ShowNewFolderButton = false
                    };
                    if (browser.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
                    SelectedPath = browser.SelectedPath;
                    Mode = StartupMode.Remote;
                    SaveRecentPath(SelectedPath);
                    DialogResult = true;
                }));

            return WrapExpanded("\U0001F310  Remote Backup Logs", stack);
        }

        private UIElement BuildRecentContent()
        {
            var paths = LoadRecentPaths();
            var stack = new StackPanel();

            if (paths.Count == 0)
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

            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
            var inner = new StackPanel();

            foreach (string path in paths)
            {
                string captured = path;
                bool exists = Directory.Exists(path);

                var row = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(12, 30, 18)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(30, 70, 40)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(14, 10, 14, 10),
                    Margin = new Thickness(0, 0, 0, 6),
                    Cursor = exists ? Cursors.Hand : Cursors.Arrow
                };

                var rowStack = new StackPanel();
                rowStack.Children.Add(new TextBlock
                {
                    Text = System.IO.Path.GetFileName(path.TrimEnd('\\', '/')),
                    FontSize = 12,
                    FontWeight = FontWeights.SemiBold,
                    Foreground =
                        new SolidColorBrush(exists ? Color.FromRgb(200, 240, 210) : Color.FromRgb(100, 110, 100))
                });
                rowStack.Children.Add(new TextBlock
                {
                    Text = path,
                    FontSize = 10,
                    FontFamily = new FontFamily("Consolas"),
                    Foreground = new SolidColorBrush(exists ? Color.FromRgb(70, 120, 85) : Color.FromRgb(70, 70, 70)),
                    TextWrapping = TextWrapping.Wrap
                });
                if (!exists)
                    rowStack.Children.Add(new TextBlock
                    {
                        Text = "\u26A0  Path not accessible",
                        FontSize = 9,
                        Foreground = new SolidColorBrush(Color.FromRgb(160, 120, 50))
                    });

                row.Child = rowStack;

                if (exists)
                {
                    row.MouseEnter += (s, e) => row.Background = new SolidColorBrush(Color.FromRgb(18, 50, 26));
                    row.MouseLeave += (s, e) => row.Background = new SolidColorBrush(Color.FromRgb(12, 30, 18));
                    row.MouseLeftButtonUp += (s, e) =>
                    {
                        SelectedPath = captured;
                        Mode = StartupMode.Local;
                        DialogResult = true;
                    };
                }

                inner.Children.Add(row);
            }

            scroll.Content = inner;
            stack.Children.Add(scroll);
            return WrapExpanded("\u21BB  Recent Data", stack);
        }


        private UIElement BuildStationsContent()
        {
            var root = new Grid { Margin = new Thickness(36, 28, 36, 28) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            Grid.SetRow(BuildBackButton(), 0);
            root.Children.Add(BuildBackButton());

            var titleRow = new Grid { Margin = new Thickness(0, 0, 0, 12) };
            titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var titleBlock = new TextBlock
            {
                Text = "🏭  Stations / Lines",
                FontSize = 20,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(255, 210, 140)),
                VerticalAlignment = VerticalAlignment.Center
            };
            titleRow.Children.Add(titleBlock);

            var btnRefresh = new Border
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
            var btnRefreshContent = new StackPanel { Orientation = Orientation.Horizontal };
            var refreshSpinLabel = new TextBlock
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

            var mainContent = new Grid();
            mainContent.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            mainContent.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(320) });
            mainContent.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0) });

            var treeContainer = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(8, 20, 12)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(30, 70, 40)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8, 0, 0, 8),
                Margin = new Thickness(0, 0, 0, 0)
            };

            var loadingText = new TextBlock
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

            var rawTextBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(5, 14, 8)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(30, 70, 40)),
                BorderThickness = new Thickness(1, 1, 1, 1),
                CornerRadius = new CornerRadius(0, 8, 8, 0),
                Margin = new Thickness(-1, 0, 0, 0)
            };
            var rawText = new TextBox
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

                var anim = new System.Windows.Media.Animation.DoubleAnimation(0, 360, TimeSpan.FromSeconds(1))
                    { RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever };
                refreshSpinLabel.RenderTransformOrigin = new Point(0.5, 0.5);
                refreshSpinLabel.RenderTransform = new System.Windows.Media.RotateTransform(0);
                refreshSpinLabel.RenderTransform.BeginAnimation(System.Windows.Media.RotateTransform.AngleProperty,
                    anim);

                Task.Run(() =>
                {
                    string remotePath = ResolveRemotePath();
                    var lines = ScanLineStructure(remotePath);
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
        }

        private static List<LineNode> ScanLineStructure(string rootPath)
        {
            var lines = new List<LineNode>();
            if (!Directory.Exists(rootPath)) return lines;
            try
            {
                foreach (string lineDir in Directory.GetDirectories(rootPath))
                {
                    var line = new LineNode { Name = System.IO.Path.GetFileName(lineDir), FullPath = lineDir };
                    foreach (string compDir in Directory.GetDirectories(lineDir))
                    {
                        var comp = new ComputerNode { Name = System.IO.Path.GetFileName(compDir), FullPath = compDir };
                        foreach (string stDir in GetStationDirs(compDir))
                            comp.Stations.Add(new StationNode
                                { Name = System.IO.Path.GetFileName(stDir).Replace("_", " "), FullPath = stDir });
                        if (comp.Stations.Count > 0)
                            line.Computers.Add(comp);
                    }

                    if (line.Computers.Count > 0)
                        lines.Add(line);
                }
            }
            catch
            {
            }

            return lines;
        }

        private static IEnumerable<string> GetStationDirs(string compDir)
        {
            var result = new List<string>();
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
            var sb = new System.Text.StringBuilder();
            sb.AppendLine(rootPath);
            for (int li = 0; li < lines.Count; li++)
            {
                var line = lines[li];
                bool lastL = li == lines.Count - 1;
                sb.AppendLine((lastL ? "└── " : "├── ") + line.Name);
                string linePrefix = lastL ? "    " : "│   ";
                for (int ci = 0; ci < line.Computers.Count; ci++)
                {
                    var comp = line.Computers[ci];
                    bool lastC = ci == line.Computers.Count - 1;
                    sb.AppendLine(linePrefix + (lastC ? "└── " : "├── ") + comp.Name);
                    string compPrefix = linePrefix + (lastC ? "    " : "│   ");
                    for (int si = 0; si < comp.Stations.Count; si++)
                    {
                        var st = comp.Stations[si];
                        bool lastS = si == comp.Stations.Count - 1;
                        sb.AppendLine(compPrefix + (lastS ? "└── " : "├── ") + st.Name);
                    }
                }
            }

            return sb.ToString();
        }

        private UIElement BuildStationTree(List<LineNode> lines)
        {
            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Padding = new Thickness(10)
            };
            var mainStack = new StackPanel();

            foreach (var line in lines)
            {
                var lineSection = new StackPanel { Margin = new Thickness(0, 0, 0, 4) };

                var lineHeader = new Border
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
                    var compRow = new Border
                    {
                        Padding = new Thickness(18, 3, 8, 3),
                        BorderBrush = new SolidColorBrush(Color.FromRgb(30, 65, 40)),
                        BorderThickness = new Thickness(0, 0, 0, 0)
                    };

                    var compStack = new StackPanel();
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
                        var st = comp.Stations[si];
                        bool lastS = si == comp.Stations.Count - 1;
                        string capturedPath = st.FullPath;

                        var stBorder = new Border
                        {
                            Padding = new Thickness(4, 2, 6, 2),
                            Margin = new Thickness(0, 0, 0, 1),
                            CornerRadius = new CornerRadius(3),
                            Background = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0)),
                            Cursor = Cursors.Hand
                        };

                        var stRow = new StackPanel { Orientation = Orientation.Horizontal };
                        stRow.Children.Add(new TextBlock
                        {
                            Text = lastS ? "    └─ " : "    ├─ ",
                            FontSize = 11,
                            FontFamily = new FontFamily("Consolas"),
                            Foreground = new SolidColorBrush(Color.FromRgb(50, 100, 65))
                        });
                        var stNameBlock = new TextBlock
                        {
                            Text = st.Name,
                            FontSize = 11,
                            Foreground = new SolidColorBrush(Color.FromRgb(190, 235, 205))
                        };
                        stRow.Children.Add(stNameBlock);

                        var pathBlock = new TextBlock
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
                            DialogResult = true;
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
            var tile = new Border
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
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(new TextBlock
            {
                Text = icon,
                FontSize = 28,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 14, 0)
            });
            var texts = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
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

            private static readonly int[] MonthOptions = { 30, 60, 90, 120, 150, 180, 210, 240, 270, 300, 330, 365 };

            private static readonly string[] SliderLabels =
                { "1m", "2m", "3m", "4m", "5m", "6m", "7m", "8m", "9m", "10m", "11m", "12m" };

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
            }

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
                ResizeMode = ResizeMode.CanResize;
                WindowStartupLocation = WindowStartupLocation.CenterOwner;
                Background = new SolidColorBrush(Color.FromRgb(8, 14, 10));

                var screen = System.Windows.SystemParameters.WorkArea;
                Width = Math.Min(1600, screen.Width * 0.92);
                Height = Math.Min(1100, screen.Height * 0.92);
                MinWidth = Math.Min(900, screen.Width * 0.6);
                MinHeight = Math.Min(700, screen.Height * 0.6);

                int recommendedMonths = CalculateRecommendedMonths(globalFileCounts);
                int recommendedIndex = Array.IndexOf(MonthOptions, recommendedMonths);
                if (recommendedIndex < 0) recommendedIndex = 11;

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

                content.Children.Add(new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(8, 22, 14)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(30, 60, 38)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(14, 8, 14, 8),
                    Margin = new Thickness(0, 0, 0, 10),
                    Child = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Children =
                        {
                            new TextBlock
                            {
                                Text = "⏳", FontSize = 13, Margin = new Thickness(0, 0, 8, 0),
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
                });

                var cbGhp = AddStationSection(content, allEntries, "GHP Stations", ghpStations,
                    Color.FromRgb(63, 185, 80), true, recommendedIndex);
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

                    ExcludedFolderPaths = new List<string>();

                    LazyLoadFolderPaths = allEntries
                        .Where(stEntry => stEntry.EnabledBox.IsChecked != true)
                        .Select(stEntry => stEntry.Station.FolderPath)
                        .ToList();

                    StationMonthOverrides = new Dictionary<string, int>();

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

                Action recalculate = () => RecalculateTotalLoad(allEntries, globalSlider, globalFileCounts, loadBar,
                    totalSizeLabel, totalWarningLabel, btnLoad, this);

                globalSlider.ValueChanged += (s, e) =>
                {
                    UpdateSliderDisplay(globalSlider, globalValueLabel, globalSizeLabel, globalWarningLabel,
                        globalFileCounts, recommendedMonths);
                    recalculate();
                };

                foreach (var entry in allEntries)
                {
                    entry.EnabledBox.Checked += (s, e) => recalculate();
                    entry.EnabledBox.Unchecked += (s, e) => recalculate();
                }

                UpdateSliderDisplay(globalSlider, globalValueLabel, globalSizeLabel, globalWarningLabel,
                    globalFileCounts, recommendedMonths);
                recalculate();

                _ramTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
                _ramTimer.Tick += (s, e) => UpdateRamLabel(recalculate);
                _ramTimer.Start();
                Closed += (s, e) => _ramTimer.Stop();
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

                string availText = availMb >= 1024
                    ? (availMb / 1024.0).ToString("0.#") + " GB free"
                    : availMb + " MB free";
                _ramValueLabel.Text = availText;
                double usedRatio = 1.0 - (availMb / (double)Math.Max(1, totalRamMb));
                Color ramColor = usedRatio > 0.85 ? Color.FromRgb(230, 100, 80) :
                    usedRatio > 0.65 ? Color.FromRgb(210, 160, 50) : Color.FromRgb(80, 185, 120);
                _ramValueLabel.Foreground = new SolidColorBrush(ramColor);
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
                    var s = new MEMORYSTATUSEX();
                    if (GlobalMemoryStatusEx(s)) return (long)(s.ullAvailPhys / 1024 / 1024);
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
                    var s = new MEMORYSTATUSEX();
                    if (GlobalMemoryStatusEx(s)) return (long)(s.ullTotalPhys / 1024 / 1024);
                }
                catch
                {
                }

                return -1;
            }

            private static string FormatMb(long mb) => mb >= 1024 ? (mb / 1024.0).ToString("0.#") + " GB" : mb + " MB";

            private static (Color textColor, Color barColor, string statusText) GetLoadStatus(long sizeMb)
            {
                if (sizeMb >= CriticalSizeMb)
                    return (Color.FromRgb(180, 30, 20), Color.FromRgb(180, 30, 20), "✕  Danger — very likely to crash");
                if (sizeMb >= WarningSizeMb)
                    return (Color.FromRgb(220, 60, 40), Color.FromRgb(220, 60, 40), "⚠  Risk — may run out of memory");
                if (sizeMb >= GoodSizeMb)
                    return (Color.FromRgb(220, 140, 30), Color.FromRgb(220, 140, 30),
                        "⚠  Heavy — loading will be slow");
                if (sizeMb >= OptimalSizeMb)
                    return (Color.FromRgb(160, 200, 60), Color.FromRgb(160, 200, 60), "✓  Good");
                return (Color.FromRgb(46, 185, 80), Color.FromRgb(46, 185, 80), "✓  Optimal");
            }

            private static int CalculateRecommendedMonths(Dictionary<int, MonthFileInfo> fileCounts)
            {
                if (fileCounts == null) return 180;
                foreach (int days in MonthOptions)
                    if (fileCounts.TryGetValue(days, out var info) && info.SizeMb <= OptimalSizeMb)
                        return days;
                return MonthOptions[0];
            }

            private static void RecalculateTotalLoad(
                List<StationLoadEntry> entries, Slider globalSlider,
                Dictionary<int, MonthFileInfo> globalFileCounts,
                ProgressBar loadBar, TextBlock totalSizeLabel, TextBlock totalWarningLabel,
                Button btnLoad, LoadOptionsDialog dialog = null)
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
                int globalMonths = MonthOptions[(int)Math.Round(globalSlider.Value)];
                if (globalFileCounts.TryGetValue(globalMonths, out var globalInfo))
                    totalFileMb = globalInfo.SizeMb * enabled.Count / Math.Max(1, totalStations);
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

                dialog?.UpdateRamEstimatedMarker(estimatedRamMb);
            }

            private Border _ramEstimatedMarker;
            private Border _ramEstimatedFill;
            private TextBlock _ramEstimatedLabel;

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
                if (_ramEstimatedFill != null)
                {
                    _ramEstimatedFill.Opacity = estimatedRamMb > 0 ? 1.0 : 0.0;
                    _ramEstimatedFill.Tag = new double[] { currentPct, afterPct };
                    var fillParent = _ramEstimatedFill.Parent as Grid;
                    if (fillParent != null && fillParent.ActualWidth > 0)
                    {
                        double startPx = currentPct * fillParent.ActualWidth;
                        double widthPx = Math.Max(0, (afterPct - currentPct) * fillParent.ActualWidth);
                        _ramEstimatedFill.Margin = new Thickness(startPx, 0, 0, 0);
                        _ramEstimatedFill.Width = widthPx;
                    }
                }

                if (_ramEstimatedLabel != null)
                    _ramEstimatedLabel.Text = "Estimated after load:  " + FormatMb(afterLoadMb);
                var parent = _ramEstimatedMarker.Parent as Grid;
                if (parent != null && parent.ActualWidth > 0)
                {
                    double leftPx = afterPct * parent.ActualWidth - 5.0;
                    _ramEstimatedMarker.Margin = new Thickness(Math.Max(0, leftPx), 0, 0, 0);
                }
            }

            private static UIElement BuildGlobalSliderSection(
                Dictionary<int, MonthFileInfo> fileCounts, int defaultIndex, int recommendedMonths,
                out Slider slider, out TextBlock valueLabel, out TextBlock sizeLabel, out TextBlock warningLabel)
            {
                var border = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(10, 22, 14)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(30, 80, 44)),
                    BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(18, 14, 18, 14), Margin = new Thickness(0, 0, 0, 12)
                };
                var stack = new StackPanel();
                stack.Children.Add(new TextBlock
                {
                    Text = "Default data range  —  applies to all stations", FontSize = 12,
                    FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Color.FromRgb(160, 210, 175)),
                    Margin = new Thickness(0, 0, 0, 10)
                });
                slider = BuildSlider(defaultIndex);
                stack.Children.Add(WrapSliderWithLabels(slider));
                var infoRow = new StackPanel
                    { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 2) };
                valueLabel = new TextBlock { FontSize = 18, FontWeight = FontWeights.Bold };
                sizeLabel = new TextBlock
                {
                    FontSize = 11, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(10, 0, 0, 2)
                };
                infoRow.Children.Add(valueLabel);
                infoRow.Children.Add(sizeLabel);
                stack.Children.Add(infoRow);
                if (fileCounts != null && fileCounts.TryGetValue(recommendedMonths, out var recInfo))
                {
                    string recSize = recInfo.SizeMb >= 1024
                        ? (recInfo.SizeMb / 1024.0).ToString("0.#") + " GB"
                        : recInfo.SizeMb + " MB";
                    int recDays = recommendedMonths;
                    var recRow = new StackPanel
                        { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 4) };
                    recRow.Children.Add(new TextBlock
                    {
                        Text = "Recommended: ", FontSize = 10,
                        Foreground = new SolidColorBrush(Color.FromRgb(70, 110, 82))
                    });
                    recRow.Children.Add(new TextBlock
                    {
                        Text = $"{recDays}d  ({recInfo.FileCount} files, {recSize})", FontSize = 10,
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
                int idx = (int)Math.Round(slider.Value);
                int days = MonthOptions[idx];
                string lbl = idx < 12
                    ? (new[] { "1m", "2m", "3m", "4m", "5m", "6m", "7m", "8m", "9m", "10m", "11m", "12m" })[idx]
                    : days + "d";
                double colorRatio = (double)idx / Math.Max(1, MonthOptions.Length - 1);
                byte vr = (byte)(46 + colorRatio * (220 - 46));
                byte vg = (byte)(185 - colorRatio * (185 - 140));
                byte vb = (byte)(80 - colorRatio * 50);
                valueLabel.Foreground = new SolidColorBrush(Color.FromRgb(vr, vg, vb));
                valueLabel.Text = lbl.EndsWith("w")
                    ? lbl.Replace("w", " week") + (lbl == "1w" ? "" : "s")
                    : lbl.Replace("m", " month") + (lbl == "1m" ? "" : "s");
                if (fileCounts == null || !fileCounts.TryGetValue(days, out var info))
                {
                    sizeLabel.Text = "";
                    warningLabel.Text = "";
                    return;
                }

                string sizeText = info.SizeMb >= 1024
                    ? (info.SizeMb / 1024.0).ToString("0.#") + " GB"
                    : info.SizeMb + " MB";
                sizeLabel.Text = $"{info.FileCount} files  ·  {sizeText}";
                var (textColor, _, statusText) = GetLoadStatus(info.SizeMb);
                valueLabel.Foreground = new SolidColorBrush(textColor);
                sizeLabel.Foreground = new SolidColorBrush(textColor);
                warningLabel.Text = days <= recommendedMonths && info.SizeMb < OptimalSizeMb
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
                    BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(16, 14, 16, 14), Margin = new Thickness(0, 0, 0, 10)
                };
                var stack = new StackPanel();
                var cbSection = new CheckBox { IsChecked = defaultChecked };
                var titlePanel = new StackPanel { Orientation = Orientation.Horizontal };
                titlePanel.Children.Add(new TextBlock
                {
                    Text = title, FontSize = 15, FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(accentColor)
                });
                titlePanel.Children.Add(new TextBlock
                {
                    Text = "  (" + stations.Count + ")", FontSize = 12,
                    Foreground = new SolidColorBrush(Color.FromRgb(100, 140, 112)),
                    VerticalAlignment = VerticalAlignment.Center
                });
                cbSection.Content = titlePanel;
                stack.Children.Add(cbSection);
                stack.Children.Add(new TextBlock
                {
                    Text =
                        "⏳  Unchecked stations will be accessible via Lazy Load — available for on-demand loading after the main load completes.",
                    FontSize = 12, Foreground = new SolidColorBrush(Color.FromRgb(80, 120, 90)),
                    Margin = new Thickness(0, 4, 0, 6), TextWrapping = TextWrapping.Wrap
                });

                var sectionEntries = new List<StationLoadEntry>();
                foreach (var st in stations)
                {
                    var cb = new CheckBox { IsChecked = defaultChecked, IsEnabled = defaultChecked };
                    var entry = new StationLoadEntry { Station = st, EnabledBox = cb };
                    allEntries.Add(entry);
                    sectionEntries.Add(entry);
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

                Button MakeBtn(string label, Color fg) => new Button
                {
                    Content = label, FontSize = 10, Padding = new Thickness(8, 3, 8, 3),
                    Margin = new Thickness(0, 0, 6, 0),
                    Background = new SolidColorBrush(Color.FromRgb(14, 40, 20)), Foreground = new SolidColorBrush(fg),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(30, 80, 44)), BorderThickness = new Thickness(1),
                    Cursor = Cursors.Hand
                };

                var btnCheckAll = MakeBtn("✓ Check All", Color.FromRgb(100, 185, 130));
                var btnUncheckAll = MakeBtn("○ Uncheck All", Color.FromRgb(120, 140, 125));
                var btnCheckMax = MakeBtn("★ Check Maximum", Color.FromRgb(160, 200, 60));
                btnCheckMax.ToolTip =
                    "Loads the maximum number of stations that fit within available RAM (green zone only)";

                btnCheckAll.Click += (s, e) =>
                {
                    foreach (var ent in sectionEntries) ent.EnabledBox.IsChecked = true;
                };
                btnUncheckAll.Click += (s, e) =>
                {
                    foreach (var ent in sectionEntries)
                    {
                        ent.EnabledBox.IsEnabled = true;
                        ent.EnabledBox.IsChecked = false;
                    }
                };
                btnCheckMax.Click += (s, e) =>
                {
                    long availMb = GetAvailableRamMb();
                    long totalRamMb = GetTotalRamMb();
                    long budgetMb = Math.Max(0, availMb - 500);
                    long perStation = sectionEntries.Count > 0 ? budgetMb / Math.Max(1, sectionEntries.Count) : 0;
                    long accumulated = 0;
                    int maxCount = 0;
                    foreach (var ent in sectionEntries)
                    {
                        accumulated += perStation;
                        if (accumulated > budgetMb) break;
                        maxCount++;
                    }

                    if (availMb <= 0) maxCount = Math.Max(1, sectionEntries.Count / 2);
                    maxCount = Math.Max(1, maxCount);
                    for (int idx = 0; idx < sectionEntries.Count; idx++)
                        sectionEntries[idx].EnabledBox.IsChecked = idx < maxCount;
                };

                var btnRow = new StackPanel
                    { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 4) };
                btnRow.Children.Add(btnCheckAll);
                btnRow.Children.Add(btnUncheckAll);
                btnRow.Children.Add(btnCheckMax);
                stack.Children.Add(btnRow);

                if (defaultChecked)
                {
                    stack.Children.Add(new Border
                    {
                        Child = BuildStationGrid(sectionEntries, accentColor, colorCode: true),
                        Margin = new Thickness(0, 8, 0, 0)
                    });
                }
                else
                {
                    var expander = new Expander
                    {
                        Header = "Choose stations ▾", IsExpanded = false, Margin = new Thickness(0, 6, 0, 0),
                        FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(90, 150, 110)),
                        IsEnabled = defaultChecked,
                        Content = new Border
                        {
                            Child = BuildStationGrid(sectionEntries, accentColor, false),
                            Margin = new Thickness(0, 4, 0, 0)
                        }
                    };
                    cbSection.Checked += (s, e) => expander.IsEnabled = true;
                    cbSection.Unchecked += (s, e) => expander.IsEnabled = false;
                    stack.Children.Add(expander);
                }

                border.Child = stack;
                parent.Children.Add(border);
                return cbSection;
            }

            private static UIElement BuildStationGrid(List<StationLoadEntry> entries, Color accentColor,
                bool colorCode = false)
            {
                int cols = entries.Count > 12 ? 3 : 2;
                var grid = new Grid();
                for (int c = 0; c < cols; c++)
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                int rowCount = (entries.Count + cols - 1) / cols;
                for (int r = 0; r < rowCount; r++)
                    grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                for (int i = 0; i < entries.Count; i++)
                {
                    int col = i % cols;
                    int row = i / cols;
                    Color? checkColor = null;
                    if (colorCode)
                    {
                        double ratio = (double)row / Math.Max(1, rowCount - 1);
                        checkColor = ratio <= 0.5 ? Color.FromRgb(46, 185, 80) :
                            ratio <= 0.75 ? Color.FromRgb(220, 180, 30) : Color.FromRgb(220, 80, 40);
                    }

                    var outer = new StackPanel { Margin = new Thickness(0, 2, 6, 2) };
                    entries[i].EnabledBox.Content = new TextBlock
                    {
                        Text = entries[i].Station.StationName, FontSize = 12,
                        Foreground = new SolidColorBrush(checkColor ?? Color.FromRgb(175, 220, 190)),
                        TextWrapping = TextWrapping.NoWrap, TextTrimming = TextTrimming.CharacterEllipsis
                    };
                    entries[i].EnabledBox.Foreground = new SolidColorBrush(checkColor ?? Color.FromRgb(175, 220, 190));
                    outer.Children.Add(entries[i].EnabledBox);
                    Grid.SetColumn(outer, col);
                    Grid.SetRow(outer, row);
                    grid.Children.Add(outer);
                }

                return grid;
            }

            private UIElement BuildLoadIndicator(out ProgressBar loadBar, out TextBlock totalSizeLabel,
                out TextBlock totalWarningLabel)
            {
                var border = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(5, 18, 9)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(22, 70, 36)),
                    BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(20, 14, 20, 14)
                };
                var stack = new StackPanel();
                var loadRow = new StackPanel
                    { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
                loadRow.Children.Add(new TextBlock
                {
                    Text = "Estimated load:  ", FontSize = 12,
                    Foreground = new SolidColorBrush(Color.FromRgb(70, 110, 82)),
                    VerticalAlignment = VerticalAlignment.Center
                });
                totalSizeLabel = new TextBlock { FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
                loadRow.Children.Add(totalSizeLabel);
                stack.Children.Add(loadRow);
                loadBar = new ProgressBar { Height = 0, Visibility = Visibility.Collapsed };
                stack.Children.Add(loadBar);

                var spectrumStops = new (double stop, byte r, byte g, byte b)[]
                {
                    (0.00, 46, 185, 80), (0.25, 160, 200, 60), (0.50, 220, 180, 30), (0.75, 220, 80, 40),
                    (1.00, 160, 30, 20)
                };
                totalWarningLabel = new TextBlock
                    { FontSize = 11, Margin = new Thickness(0, 2, 0, 14), TextWrapping = TextWrapping.Wrap };
                stack.Children.Add(totalWarningLabel);

                long availMb = GetAvailableRamMb();
                long totalRamMb = GetTotalRamMb();

                stack.Children.Add(new TextBlock
                {
                    Text = "Memory", FontSize = 11, FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromRgb(100, 150, 115)), Margin = new Thickness(0, 0, 0, 6)
                });

                var ramLabels = new Grid { Margin = new Thickness(0, 0, 0, 3) };
                ramLabels.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                ramLabels.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                ramLabels.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                ramLabels.Children.Add(new TextBlock
                {
                    Text = "Current RAM:", FontSize = 10, Foreground = new SolidColorBrush(Color.FromRgb(70, 110, 82))
                });
                _ramValueLabel = new TextBlock
                {
                    FontSize = 13, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center
                };
                Grid.SetColumn(_ramValueLabel, 1);
                ramLabels.Children.Add(_ramValueLabel);
                string totalRamText = totalRamMb >= 1024
                    ? (totalRamMb / 1024.0).ToString("0.#") + " GB total"
                    : totalRamMb + " MB total";
                var totalRamLabel = new TextBlock
                    { Text = totalRamText, FontSize = 10, Foreground = new SolidColorBrush(Color.FromRgb(60, 90, 70)) };
                Grid.SetColumn(totalRamLabel, 2);
                ramLabels.Children.Add(totalRamLabel);
                stack.Children.Add(ramLabels);

                var ramBarOuter = new Grid { Height = 20, Margin = new Thickness(0, 0, 0, 4) };
                var ramBgBrush = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0) };
                var ramFgBrush = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0) };
                foreach (var (stop, r, g, b) in spectrumStops)
                {
                    ramBgBrush.GradientStops.Add(new GradientStop(Color.FromArgb(35, r, g, b), stop));
                    ramFgBrush.GradientStops.Add(new GradientStop(Color.FromRgb(r, g, b), stop));
                }

                ramBarOuter.Children.Add(new System.Windows.Shapes.Rectangle
                {
                    Fill = ramBgBrush, RadiusX = 6, RadiusY = 6, HorizontalAlignment = HorizontalAlignment.Stretch,
                    Height = 20
                });
                double usedPct = totalRamMb > 0 ? Math.Min(1.0, 1.0 - (availMb / (double)totalRamMb)) : 0.0;
                ramBarOuter.Children.Add(new ProgressBar
                {
                    Height = 20, Minimum = 0, Maximum = 100, Value = Math.Round(usedPct * 100), Foreground = ramFgBrush,
                    Background = System.Windows.Media.Brushes.Transparent, BorderThickness = new Thickness(0)
                });
                _ramEstimatedFill = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(80, 0, 200, 255)),
                    VerticalAlignment = VerticalAlignment.Stretch, HorizontalAlignment = HorizontalAlignment.Left,
                    Opacity = 0.0, Width = 0
                };
                ramBarOuter.Children.Add(_ramEstimatedFill);
                _ramEstimatedMarker = new Border
                {
                    Width = 10, Background = new SolidColorBrush(Color.FromRgb(0, 240, 255)),
                    VerticalAlignment = VerticalAlignment.Stretch, HorizontalAlignment = HorizontalAlignment.Left,
                    Opacity = 0.0, CornerRadius = new CornerRadius(3),
                    ToolTip = "Estimated RAM usage after loading selected stations",
                    Effect = new System.Windows.Media.Effects.DropShadowEffect
                    {
                        Color = Color.FromRgb(0, 240, 255), BlurRadius = 20, Opacity = 1.0, ShadowDepth = 0
                    }
                };
                ramBarOuter.Children.Add(_ramEstimatedMarker);
                ramBarOuter.SizeChanged += (s, e) =>
                {
                    if (_ramEstimatedMarker?.Tag is double pct && ramBarOuter.ActualWidth > 0)
                        _ramEstimatedMarker.Margin =
                            new Thickness(Math.Max(0, pct * ramBarOuter.ActualWidth - 5.0), 0, 0, 0);
                    if (_ramEstimatedFill?.Tag is double[] fillData && ramBarOuter.ActualWidth > 0)
                    {
                        _ramEstimatedFill.Margin = new Thickness(fillData[0] * ramBarOuter.ActualWidth, 0, 0, 0);
                        _ramEstimatedFill.Width = Math.Max(0, (fillData[1] - fillData[0]) * ramBarOuter.ActualWidth);
                    }
                };
                stack.Children.Add(ramBarOuter);

                long usedMb = totalRamMb > 0 ? (long)((1.0 - availMb / (double)totalRamMb) * totalRamMb) : 0;
                var legend = new StackPanel
                    { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
                legend.Children.Add(new Border
                {
                    Width = 14, Height = 14, Background = ramFgBrush, CornerRadius = new CornerRadius(3),
                    Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center
                });
                legend.Children.Add(new TextBlock
                {
                    Text = "Current usage:  " + FormatMb(usedMb), FontSize = 13, FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromRgb(140, 190, 155)), Margin = new Thickness(0, 0, 28, 0),
                    VerticalAlignment = VerticalAlignment.Center
                });
                legend.Children.Add(new Border
                {
                    Width = 5, Height = 14, Background = new SolidColorBrush(Color.FromRgb(0, 220, 255)),
                    CornerRadius = new CornerRadius(2), Margin = new Thickness(0, 0, 6, 0),
                    VerticalAlignment = VerticalAlignment.Center
                });
                _ramEstimatedLabel = new TextBlock
                {
                    Text = "Estimated after load:  —", FontSize = 13, FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromRgb(0, 200, 230)),
                    VerticalAlignment = VerticalAlignment.Center
                };
                legend.Children.Add(_ramEstimatedLabel);
                stack.Children.Add(legend);

                if (availMb < 0)
                {
                    _ramValueLabel.Text = "unknown";
                    _ramValueLabel.Foreground = new SolidColorBrush(Color.FromRgb(100, 110, 100));
                }
                else
                {
                    string availText = availMb >= 1024
                        ? (availMb / 1024.0).ToString("0.#") + " GB free"
                        : availMb + " MB free";
                    _ramValueLabel.Text = availText;
                    Color ramColor = usedPct > 0.85 ? Color.FromRgb(230, 100, 80) :
                        usedPct > 0.65 ? Color.FromRgb(210, 160, 50) : Color.FromRgb(80, 185, 120);
                    _ramValueLabel.Foreground = new SolidColorBrush(ramColor);
                }

                border.Child = stack;
                return border;
            }

            private Dictionary<MessageType, CheckBox> _messageTypeCheckboxes = new Dictionary<MessageType, CheckBox>();

            private UIElement BuildMessageTypeSection()
            {
                var border = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(5, 18, 9)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(22, 70, 36)),
                    BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(20, 10, 20, 10)
                };
                var stack = new StackPanel();
                stack.Children.Add(new TextBlock
                {
                    Text = "Message types to include:", FontSize = 11, FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromRgb(120, 170, 135)), Margin = new Thickness(0, 0, 0, 6)
                });
                var wrapPanel = new WrapPanel { Orientation = Orientation.Horizontal };
                foreach (var msgType in AllMessageTypes)
                {
                    var cb = new CheckBox { IsChecked = true, Margin = new Thickness(0, 0, 16, 4) };
                    cb.Content = new TextBlock
                    {
                        Text = msgType.ToString().Replace("REQ_", "").Replace("_", " "), FontSize = 10,
                        Foreground = new SolidColorBrush(Color.FromRgb(150, 200, 165))
                    };
                    _messageTypeCheckboxes[msgType] = cb;
                    wrapPanel.Children.Add(cb);
                }

                stack.Children.Add(wrapPanel);
                border.Child = stack;
                return border;
            }

            private static Button BuildLoadButton() => new Button
            {
                Content = "Load →", Padding = new Thickness(22, 8, 22, 8), FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Background = new SolidColorBrush(Color.FromRgb(22, 100, 50)),
                Foreground = new SolidColorBrush(Color.FromRgb(180, 245, 205)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(50, 180, 90)), BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand
            };

            private static Slider BuildSlider(int defaultIndex) => new Slider
            {
                Minimum = 0, Maximum = MonthOptions.Length - 1, Value = defaultIndex,
                TickFrequency = 1, IsSnapToTickEnabled = true,
                TickPlacement = System.Windows.Controls.Primitives.TickPlacement.BottomRight,
                SmallChange = 1, LargeChange = 1, VerticalAlignment = VerticalAlignment.Center
            };

            private static UIElement WrapSliderWithLabels(Slider slider)
            {
                var outerStack = new StackPanel();
                var sliderGrid = new Grid();
                sliderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                sliderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                sliderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var left = new TextBlock
                {
                    Text = "1m", FontSize = 10, Foreground = new SolidColorBrush(Color.FromRgb(46, 185, 80)),
                    VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0)
                };
                var right = new TextBlock
                {
                    Text = "12m", FontSize = 10, Foreground = new SolidColorBrush(Color.FromRgb(220, 140, 30)),
                    VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 0, 0)
                };
                Grid.SetColumn(slider, 1);
                Grid.SetColumn(right, 2);
                sliderGrid.Children.Add(left);
                sliderGrid.Children.Add(slider);
                sliderGrid.Children.Add(right);
                outerStack.Children.Add(sliderGrid);
                outerStack.Children.Add(BuildSliderTickRow());
                return outerStack;
            }

            private static UIElement BuildSliderTickRow()
            {
                var tickPanel = new Canvas { Height = 20 };
                var labels = new (int idx, string label)[]
                {
                    (0, "1m"), (1, "2m"), (2, "3m"), (3, "4m"), (4, "5m"), (5, "6m"), (6, "7m"), (7, "8m"), (8, "9m"),
                    (9, "10m"), (10, "11m"), (11, "12m")
                };
                int total = MonthOptions.Length - 1;
                tickPanel.SizeChanged += (s, e) =>
                {
                    tickPanel.Children.Clear();
                    double w = tickPanel.ActualWidth;
                    if (w <= 0) return;
                    foreach (var (idx, label) in labels)
                    {
                        double pct = (double)idx / total;
                        double x = pct * w;
                        double ratio = (double)idx / total;
                        byte r = (byte)(46 + ratio * (220 - 46));
                        byte g = (byte)(185 - ratio * (185 - 140));
                        byte b = (byte)(80 - ratio * (80 - 30));
                        var color = Color.FromRgb(r, g, b);
                        var tick = new System.Windows.Shapes.Rectangle
                            { Width = 1.5, Height = 8, Fill = new SolidColorBrush(color), Opacity = 0.8 };
                        Canvas.SetLeft(tick, x - 0.75);
                        Canvas.SetTop(tick, 0);
                        tickPanel.Children.Add(tick);
                        var lbl = new TextBlock
                            { Text = label, FontSize = 9, Foreground = new SolidColorBrush(color), Opacity = 0.85 };
                        lbl.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                        Canvas.SetLeft(lbl, x - lbl.DesiredSize.Width / 2);
                        Canvas.SetTop(lbl, 9);
                        tickPanel.Children.Add(lbl);
                    }
                };
                return tickPanel;
            }

            private static UIElement BuildHeader()
            {
                var border = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(5, 18, 9)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(22, 70, 36)),
                    BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(24, 16, 24, 16)
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
                    Foreground = new SolidColorBrush(Color.FromRgb(90, 140, 105)), Margin = new Thickness(0, 4, 0, 0),
                    TextWrapping = TextWrapping.Wrap
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
                    BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(20, 12, 20, 12)
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
                btnRow.Children.Add(btnCancel);
                btnRow.Children.Add(btnLoad);
                row.Children.Add(btnRow);
                border.Child = row;
                return border;
            }
        }
    }
}