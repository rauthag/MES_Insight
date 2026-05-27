using System;
using System.IO;
using IOPath = System.IO.Path;
using System.Collections.Generic;
using System.Linq;
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

        private static readonly string DefaultRemotePath = @"\\vt1.vitesco.com\fs\didv0952\06_MES_App_Logs";
        private static readonly string SampleDataPath = FindSampleDataPath();

        private Canvas _canvas;
        private Grid _expandedPanel;
        private bool _isExpanded = false;


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
            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(60) });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(34) });

            var header = BuildHeader();
            Grid.SetRow(header, 0);
            root.Children.Add(header);

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

            var footer = BuildFooter();
            Grid.SetRow(footer, 2);
            root.Children.Add(footer);

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
            var grid = new Grid { Margin = new Thickness(28, 0, 28, 0) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

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
            grid.Children.Add(left);
            grid.Children.Add(right);
            footer.Child = grid;
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
                Width = 3 * stepX - gap + 0.1,
                Height = stepY + H
            };

            AddHex(canvas, W, H, r, "\U0001F4C2", "LOCAL FOLDER", "Local or network path", 0 * stepX, 0, false);
            AddHex(canvas, W, H, r, "\U0001F310", "REMOTE BACKUP LOGS", "MES Backup disc access needed", 1 * stepX, 0,
                false);
            AddHex(canvas, W, H, r, "\U0001F9EA", "SAMPLE DATA", sampleOk ? "Demo data ready" : "Not available",
                2 * stepX, 0, !sampleOk);
            AddHex(canvas, W, H, r, "\u21BB", "RECENT DATA", "Last loaded stations", rowOff, stepY, false);
            AddHex(canvas, W, H, r, "\u2715", "EXIT", "Close application", rowOff + stepX, stepY, false, isExit: true);

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
                { Fill = new SolidColorBrush(HexFill), Stroke = new SolidColorBrush(HexFill), StrokeThickness = 0.3 };
            var inner = new Polygon
                { Fill = new SolidColorBrush(HexFill), Stroke = new SolidColorBrush(HexStroke), StrokeThickness = 5 };

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
                { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
            stack.Children.Add(new TextBlock
            {
                Text = icon, FontSize = 32, HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = new SolidColorBrush(TextLight), Margin = new Thickness(0, 0, 0, 6)
            });
            stack.Children.Add(new TextBlock
            {
                Text = title, FontSize = 12, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(TextLight),
                HorizontalAlignment = HorizontalAlignment.Center, TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 0, 0, 3)
            });
            stack.Children.Add(new TextBlock
            {
                Text = sub, FontSize = 10, Foreground = new SolidColorBrush(TextSub),
                HorizontalAlignment = HorizontalAlignment.Center, TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap, MaxWidth = W - 50
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
                grid.MouseLeftButtonUp += (s, e) => HandleClick(title, isExit);
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
            var anim = new DoubleAnimation(from, to, TimeSpan.FromMilliseconds(ms))
                { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut } };
            if (onComplete != null) anim.Completed += (s, e) => onComplete();
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
                Text = "\u2190", FontSize = 16, Foreground = new SolidColorBrush(Color.FromRgb(240, 160, 50)),
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0)
            });
            row.Children.Add(new TextBlock
            {
                Text = "Back", FontSize = 12, Foreground = new SolidColorBrush(Color.FromRgb(240, 200, 140)),
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

            var backBtn = BuildBackButton();
            Grid.SetRow(backBtn, 0);
            root.Children.Add(backBtn);

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
                Text = icon, FontSize = 28, VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 14, 0)
            });
            var texts = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            texts.Children.Add(new TextBlock
            {
                Text = title, FontSize = 13, FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(255, 230, 180))
            });
            texts.Children.Add(new TextBlock
            {
                Text = sub, FontSize = 10, Foreground = new SolidColorBrush(Color.FromRgb(160, 130, 80)),
                TextWrapping = TextWrapping.Wrap
            });
            row.Children.Add(texts);
            tile.Child = row;
            tile.MouseEnter += (s, e) => tile.Background = new SolidColorBrush(Color.FromRgb(22, 60, 30));
            tile.MouseLeave += (s, e) => tile.Background = new SolidColorBrush(Color.FromRgb(14, 40, 22));
            tile.MouseLeftButtonUp += (s, e) => onClick();
            return tile;
        }


        private UIElement BuildRecentContent()
        {
            var entries = LoadRecentEntries();

            if (entries.Count == 0)
                return WrapExpanded("\u21BB  Recent Data", BuildRecentEmptyState());

            var existsMap = CheckPathsExist(entries.Select(e => e.Path).ToList());
            var inner = new StackPanel();
            foreach (var entry in entries)
                inner.Children.Add(BuildRecentEntryRow(entry, existsMap));

            var stack = new StackPanel();
            stack.Children.Add(new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = inner
            });
            return WrapExpanded("\u21BB  Recent Data", stack);
        }

        private static UIElement BuildRecentEmptyState()
        {
            return new TextBlock
            {
                Text = "No recent data found.\nLoad a folder first and it will appear here.",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(140, 160, 140)),
                TextWrapping = TextWrapping.Wrap
            };
        }

        private static Dictionary<string, bool> CheckPathsExist(List<string> paths)
        {
            var map = new Dictionary<string, bool>();
            Parallel.ForEach(paths, p =>
            {
                bool ex = Directory.Exists(p);
                lock (map) map[p] = ex;
            });
            return map;
        }

        private Border BuildRecentEntryRow(RecentEntry entry, Dictionary<string, bool> existsMap)
        {
            bool exists = existsMap.ContainsKey(entry.Path) && existsMap[entry.Path];
            string folderName = IOPath.GetFileName(entry.Path.TrimEnd('\\', '/'));

            var rowStack = new StackPanel();
            rowStack.Children.Add(BuildRecentEntryTitle(folderName, exists));
            rowStack.Children.Add(BuildRecentEntryPath(entry.Path, exists));
            if (!exists)
                rowStack.Children.Add(BuildRecentEntryWarning());
            else if (entry.Stations.Count > 0)
                rowStack.Children.Add(BuildRecentEntryStationInfo(entry));

            var row = new Border
            {
                Background = new SolidColorBrush(exists ? Color.FromRgb(12, 30, 18) : Color.FromRgb(20, 14, 14)),
                BorderBrush = new SolidColorBrush(exists ? Color.FromRgb(30, 70, 40) : Color.FromRgb(60, 30, 30)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(14, 10, 14, 10),
                Margin = new Thickness(0, 0, 0, 6),
                Cursor = exists ? Cursors.Hand : Cursors.Arrow,
                Child = rowStack
            };
            if (exists) WireRecentEntryClick(row, entry);
            return row;
        }

        private static TextBlock BuildRecentEntryTitle(string folderName, bool exists) => new TextBlock
        {
            Text = folderName,
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(exists ? Color.FromRgb(200, 240, 210) : Color.FromRgb(120, 100, 100))
        };

        private static TextBlock BuildRecentEntryPath(string path, bool exists) => new TextBlock
        {
            Text = path,
            FontSize = 10,
            FontFamily = new FontFamily("Consolas"),
            Foreground = new SolidColorBrush(exists ? Color.FromRgb(70, 120, 85) : Color.FromRgb(90, 60, 60)),
            TextWrapping = TextWrapping.NoWrap
        };

        private static TextBlock BuildRecentEntryWarning() => new TextBlock
        {
            Text = "\u26A0  Path not accessible",
            FontSize = 9,
            Foreground = new SolidColorBrush(Color.FromRgb(160, 100, 50)),
            Margin = new Thickness(0, 3, 0, 0)
        };

        private static TextBlock BuildRecentEntryStationInfo(RecentEntry entry)
        {
            var lines = entry.Stations
                .Select(s => s.LineName)
                .Where(x => !string.IsNullOrEmpty(x))
                .Distinct().ToList();

            var computers = entry.Stations
                .Select(s => s.ComputerName)
                .Where(x => !string.IsNullOrEmpty(x))
                .Distinct().ToList();

            string text = entry.Stations.Count + " station" + (entry.Stations.Count != 1 ? "s" : "");
            if (lines.Count > 0)     text += "  ·  " + string.Join(", ", lines);
            if (computers.Count > 0) text += "  ·  " + string.Join(", ", computers);

            return new TextBlock { Text = text, FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 160, 120)),
                Margin = new Thickness(0, 3, 0, 0) };
        }

        private void WireRecentEntryClick(Border row, RecentEntry entry)
        {
            string capturedPath = entry.Path;
            List<string> capturedPaths = entry.Stations.Select(s => s.FolderPath).ToList();
            row.MouseEnter += (s, e) => row.Background = new SolidColorBrush(Color.FromRgb(18, 50, 26));
            row.MouseLeave += (s, e) => row.Background = new SolidColorBrush(Color.FromRgb(12, 30, 18));
            row.MouseLeftButtonUp += (s, e) =>
            {
                try
                {
                    SelectedPath = capturedPath;
                    SelectedPaths = capturedPaths.Count > 1 ? capturedPaths : new List<string>();
                    Mode = StartupMode.Local;
                    WindowAnimations.FadeOutAndClose(this, true);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Could not load path:\n" + ex.Message, "Error", MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
            };
        }


        private UIElement BuildLocalFolderContent()
        {
            var stack = new StackPanel();
            stack.Children.Add(MakeTile("\U0001F4C1", "Browse Folder", "Select any local or mapped network folder",
                () => PickLocalFolder(Environment.GetFolderPath(Environment.SpecialFolder.Desktop))));
            stack.Children.Add(new TextBlock
            {
                Text = "\u2014 or enter path manually \u2014",
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(120, 150, 130)),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 16, 0, 10)
            });
            var pathBox = BuildPathTextBox();
            stack.Children.Add(pathBox);
            stack.Children.Add(BuildOpenButton(pathBox));
            return WrapExpanded("\U0001F4C2  Local Folder", stack);
        }

        private void PickLocalFolder(string startPath)
        {
            try
            {
                var browser = new System.Windows.Forms.FolderBrowserDialog
                    { Description = "Select logs folder", SelectedPath = startPath, ShowNewFolderButton = false };
                if (browser.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
                SelectedPath = browser.SelectedPath;
                Mode = StartupMode.Local;
                SaveRecentPath(SelectedPath);
                WindowAnimations.FadeOutAndClose(this, true);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not open folder browser:\n" + ex.Message, "Error", MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        private static TextBox BuildPathTextBox() => new TextBox
        {
            Background = new SolidColorBrush(Color.FromRgb(12, 30, 18)),
            Foreground = new SolidColorBrush(Color.FromRgb(200, 240, 210)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(40, 100, 55)),
            BorderThickness = new Thickness(1),
            FontSize = 11,
            Padding = new Thickness(10, 8, 10, 8),
            VerticalContentAlignment = VerticalAlignment.Center
        };

        private Border BuildOpenButton(TextBox pathBox)
        {
            var btn = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(160, 80, 10)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(220, 120, 30)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(18, 8, 18, 8),
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 10, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Left,
                Child = new TextBlock
                {
                    Text = "Open  \u2192", FontSize = 12, FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromRgb(255, 230, 180))
                }
            };
            btn.MouseEnter += (s, e) => btn.Background = new SolidColorBrush(Color.FromRgb(190, 100, 15));
            btn.MouseLeave += (s, e) => btn.Background = new SolidColorBrush(Color.FromRgb(160, 80, 10));
            btn.MouseLeftButtonUp += (s, e) => TryOpenTypedPath(pathBox);
            return btn;
        }

        private void TryOpenTypedPath(TextBox pathBox)
        {
            try
            {
                string typed = pathBox.Text.Trim();
                if (string.IsNullOrEmpty(typed)) return;
                if (Directory.Exists(typed))
                {
                    SelectedPath = typed;
                    Mode = StartupMode.Local;
                    SaveRecentPath(SelectedPath);
                    WindowAnimations.FadeOutAndClose(this, true);
                }
                else
                {
                    pathBox.BorderBrush = new SolidColorBrush(Color.FromRgb(220, 60, 40));
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not open path:\n" + ex.Message, "Error", MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }


        private UIElement BuildRemoteContent()
        {
            var allLines = LoadStationCache();
            bool hasCached = allLines.Count > 0;

            var root = new Grid { Margin = new Thickness(36, 28, 36, 28) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var backBtn = BuildBackButton();
            Grid.SetRow(backBtn, 0);
            root.Children.Add(backBtn);

            var (titleRow, spinLabel, btnRefresh) = BuildRemoteTitleRow();
            Grid.SetRow(titleRow, 1);
            root.Children.Add(titleRow);

            var browseTile = BuildRemoteBrowseTile();
            Grid.SetRow(browseTile, 2);
            root.Children.Add(browseTile);

            var treeStack = new StackPanel();
            var rawText = BuildRemoteRawText(hasCached);
            var searchBox = BuildRemoteSearchBox();

            bool showLcs = false, showBackflush = false;
            var selectedPaths = new HashSet<string>();
            var (loadBtn, loadBtnText) = BuildRemoteLoadButton(selectedPaths);

            Action renderTree = () => RenderRemoteTree(
                treeStack, allLines, searchBox, selectedPaths,
                showLcs, showBackflush, loadBtn, loadBtnText);

            var filterRow = BuildRemoteFilterRow(
                v =>
                {
                    showLcs = v;
                    renderTree();
                },
                v =>
                {
                    showBackflush = v;
                    renderTree();
                });

            var searchAndStatus = new StackPanel();
            searchAndStatus.Children.Add(BuildRemoteSearchHost(searchBox, renderTree));
            searchAndStatus.Children.Add(filterRow);
            searchAndStatus.Children.Add(loadBtn);

            var bottomGrid = new Grid();
            bottomGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            bottomGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            
            Grid.SetRow(searchAndStatus, 0);
            
            var treeSection = BuildRemoteTreeSection(treeStack, rawText);
            
            Grid.SetRow(treeSection, 1);
            
            bottomGrid.Children.Add(treeSection);
            bottomGrid.Children.Add(searchAndStatus);
            
            Grid.SetRow(bottomGrid, 3);
            root.Children.Add(bottomGrid);

            if (hasCached)
            {
                rawText.Text = BuildRawTreeText(ResolveRemotePath(), allLines);
                renderTree();
            }

            WireRemoteRefreshButton(btnRefresh, spinLabel, rawText, treeStack, allLines, renderTree);
            WireRemoteLoadButton(loadBtn, selectedPaths);
            return root;
        }

        private static TextBlock BuildRemoteRawText(bool hasCached) => new TextBlock
        {
            Foreground = new SolidColorBrush(Color.FromRgb(100, 160, 120)),
            FontFamily = new FontFamily("Consolas"),
            FontSize = 10,
            TextWrapping = TextWrapping.NoWrap,
            Padding = new Thickness(10, 8, 10, 8),
            Text = hasCached ? "" : "Click \u21BB Refresh to scan..."
        };

        private static TextBox BuildRemoteSearchBox() => new TextBox
        {
            Background = new SolidColorBrush(Color.FromRgb(10, 28, 16)),
            Foreground = new SolidColorBrush(Color.FromRgb(200, 240, 210)),
            CaretBrush = new SolidColorBrush(Color.FromRgb(100, 220, 140)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(40, 100, 55)),
            BorderThickness = new Thickness(1),
            FontSize = 12,
            Padding = new Thickness(10, 7, 10, 7)
        };

        private static Grid BuildRemoteSearchHost(TextBox searchBox, Action onChanged)
        {
            var placeholder = new TextBlock
            {
                Text = "  \uD83D\uDD0D  Search stations...",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(80, 120, 90)),
                IsHitTestVisible = false,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(10, 0, 0, 0)
            };
            var host = new Grid { Margin = new Thickness(0, 8, 0, 6) };
            host.Children.Add(searchBox);
            host.Children.Add(placeholder);
            searchBox.TextChanged += (s, e) =>
            {
                placeholder.Visibility =
                    string.IsNullOrEmpty(searchBox.Text) ? Visibility.Visible : Visibility.Collapsed;
                onChanged();
            };
            return host;
        }

        private static (Grid titleRow, TextBlock spinLabel, Border btnRefresh) BuildRemoteTitleRow()
        {
            var titleRow = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            titleRow.Children.Add(new TextBlock
            {
                Text = "\U0001F310  Remote Backup Logs", FontSize = 20, FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(255, 210, 140)),
                VerticalAlignment = VerticalAlignment.Center
            });

            var spinLabel = new TextBlock
            {
                Text = "\u21BB", FontSize = 13, Foreground = new SolidColorBrush(Color.FromRgb(100, 220, 140)),
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0),
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = new System.Windows.Media.RotateTransform(0)
            };
            var btnContent = new StackPanel { Orientation = Orientation.Horizontal };
            btnContent.Children.Add(spinLabel);
            btnContent.Children.Add(new TextBlock
            {
                Text = "Refresh structure", FontSize = 11, FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(150, 230, 175)),
                VerticalAlignment = VerticalAlignment.Center
            });

            var btnRefresh = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(18, 55, 30)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(50, 130, 75)),
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(5),
                Padding = new Thickness(12, 5, 14, 5),
                Cursor = Cursors.Hand, VerticalAlignment = VerticalAlignment.Center, Child = btnContent
            };
            btnRefresh.MouseEnter += (s, e) => btnRefresh.Background = new SolidColorBrush(Color.FromRgb(26, 80, 44));
            btnRefresh.MouseLeave += (s, e) => btnRefresh.Background = new SolidColorBrush(Color.FromRgb(18, 55, 30));
            Grid.SetColumn(btnRefresh, 1);
            titleRow.Children.Add(btnRefresh);
            return (titleRow, spinLabel, btnRefresh);
        }

        private Border BuildRemoteBrowseTile() => MakeTile("\U0001F4C2", "Browse folder", ResolveRemotePath(), () =>
        {
            var browser = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Select a specific station folder", SelectedPath = ResolveRemotePath(),
                ShowNewFolderButton = false
            };
            if (browser.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
            SelectedPath = browser.SelectedPath;
            Mode = StartupMode.Remote;
            SaveRecentPath(SelectedPath);
            WindowAnimations.FadeOutAndClose(this, true);
        });

        private static StackPanel BuildRemoteFilterRow(Action<bool> onLcs, Action<bool> onBackflush)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
            row.Children.Add(new TextBlock
            {
                Text = "Show:", FontSize = 10, Foreground = new SolidColorBrush(Color.FromRgb(80, 120, 90)),
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0)
            });
            row.Children.Add(BuildFilterToggle("LCS", onLcs));
            row.Children.Add(BuildFilterToggle("Backflush", onBackflush));
            return row;
        }

        private static Border BuildFilterToggle(string label, Action<bool> onToggle)
        {
            bool active = false;
            var toggleText = new TextBlock
                { Text = label, FontSize = 10, Foreground = new SolidColorBrush(Color.FromRgb(70, 110, 80)) };
            var toggle = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(14, 35, 20)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(35, 80, 45)),
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4),
                Padding = new Thickness(10, 3, 10, 3),
                Margin = new Thickness(0, 0, 6, 0), Cursor = Cursors.Hand, Child = toggleText
            };
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
                toggle.Background = new SolidColorBrush(active ? Color.FromRgb(22, 65, 35) : Color.FromRgb(14, 35, 20));
                toggle.BorderBrush =
                    new SolidColorBrush(active ? Color.FromRgb(60, 160, 90) : Color.FromRgb(35, 80, 45));
                toggleText.Foreground =
                    new SolidColorBrush(active ? Color.FromRgb(130, 220, 155) : Color.FromRgb(70, 110, 80));
                onToggle(active);
            };
            return toggle;
        }

        private static (Border btn, TextBlock btnText) BuildRemoteLoadButton(HashSet<string> selectedPaths)
        {
            var btnText = new TextBlock
            {
                Text = "Load selected  \u2192", FontSize = 12, FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(180, 245, 205))
            };
            var btn = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(22, 100, 50)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(50, 180, 90)),
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6),
                Padding = new Thickness(20, 8, 20, 8),
                Cursor = Cursors.Hand, HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 6, 0, 6),
                Visibility = Visibility.Collapsed, Child = btnText
            };
            btn.MouseEnter += (s, e) => btn.Background = new SolidColorBrush(Color.FromRgb(30, 130, 65));
            btn.MouseLeave += (s, e) => btn.Background = new SolidColorBrush(Color.FromRgb(22, 100, 50));
            return (btn, btnText);
        }

        private void WireRemoteLoadButton(Border loadBtn, HashSet<string> selectedPaths)
        {
            loadBtn.MouseLeftButtonUp += (s, e) =>
            {
                if (selectedPaths.Count == 0) return;
                SelectedPath = selectedPaths.First();
                SelectedPaths = selectedPaths.ToList();
                Mode = StartupMode.Remote;
                string common = selectedPaths.First();
                if (selectedPaths.Count > 1)
                    foreach (string p in selectedPaths)
                        while (!p.StartsWith(common, StringComparison.OrdinalIgnoreCase) && common.Length > 3)
                            common = IOPath.GetDirectoryName(common) ?? common;
                SaveRecentPath(selectedPaths.Count == 1 ? selectedPaths.First() : common);
                WindowAnimations.FadeOutAndClose(this, true);
            };
        }

        private static Grid BuildRemoteTreeSection(StackPanel treeStack, TextBlock rawText)
        {
            var section = new Grid();
            section.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            section.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(300) });

            var treeScroll = new ScrollViewer
            {
                VerticalScrollBarVisibility   = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Background      = new SolidColorBrush(Color.FromRgb(8, 20, 12)),
                BorderBrush     = new SolidColorBrush(Color.FromRgb(30, 70, 40)),
                BorderThickness = new Thickness(1),
                Content         = treeStack,
                VerticalAlignment = VerticalAlignment.Stretch   // ← pridaj
            };

            var rawScroll = new ScrollViewer
            {
                VerticalScrollBarVisibility   = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                Background      = new SolidColorBrush(Color.FromRgb(5, 14, 8)),
                BorderBrush     = new SolidColorBrush(Color.FromRgb(30, 70, 40)),
                BorderThickness = new Thickness(0, 1, 1, 1),
                Content         = rawText,
                VerticalAlignment = VerticalAlignment.Stretch   // ← pridaj
            };

            Grid.SetColumn(treeScroll, 0);
            Grid.SetColumn(rawScroll,  1);
            section.Children.Add(treeScroll);
            section.Children.Add(rawScroll);
            return section;
        }

        private void WireRemoteRefreshButton(
            Border btnRefresh, TextBlock spinLabel, TextBlock rawText,
            StackPanel treeStack, List<LineNode> allLines, Action renderTree)
        {
            bool isRefreshing = false;
            btnRefresh.MouseLeftButtonUp += (s, e) =>
            {
                if (isRefreshing) return;
                isRefreshing = true;
                rawText.Text = "Scanning...";
                treeStack.Children.Clear();
                var anim = new DoubleAnimation(0, 360, TimeSpan.FromSeconds(1))
                    { RepeatBehavior = RepeatBehavior.Forever };
                spinLabel.RenderTransform.BeginAnimation(System.Windows.Media.RotateTransform.AngleProperty, anim);

                Task.Run(() =>
                {
                    string remotePath = ResolveRemotePath();
                    List<LineNode> fresh = ScanLineStructure(remotePath);
                    string raw = BuildRawTreeText(remotePath, fresh);
                    Dispatcher.Invoke(() =>
                    {
                        spinLabel.RenderTransform.BeginAnimation(System.Windows.Media.RotateTransform.AngleProperty,
                            null);
                        allLines.Clear();
                        allLines.AddRange(fresh);
                        rawText.Text = raw;
                        renderTree();
                        SaveStationCache(allLines);
                        isRefreshing = false;
                    });
                });
            };
        }

        private static void RenderRemoteTree(
            StackPanel treeStack, List<LineNode> lines, TextBox searchBox,
            HashSet<string> selectedPaths, bool showLcs, bool showBackflush,
            Border loadBtn, TextBlock loadBtnText)
        {
            treeStack.Children.Clear();
            string filter = searchBox.Text.Trim().ToLowerInvariant();

            Action<Border, TextBlock, string, bool> applySelection = (border, nameBlock, path, selected) =>
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
            Action updateLoadBtn = () =>
            {
                loadBtn.Visibility = selectedPaths.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
                loadBtnText.Text = selectedPaths.Count == 1
                    ? "Load selected  \u2192"
                    : "Load " + selectedPaths.Count + " stations  \u2192";
            };

            foreach (var line in lines)
            {
                var matchingComps = line.Computers
                    .Select(c => new
                    {
                        Comp = c, Stations = c.Stations.Where(st =>
                        {
                            if (st.Category == StationCategory.LCS && !showLcs) return false;
                            if (st.Category == StationCategory.Backflush && !showBackflush) return false;
                            return string.IsNullOrEmpty(filter) || st.Name.ToLowerInvariant().Contains(filter) ||
                                   c.Name.ToLowerInvariant().Contains(filter) ||
                                   line.Name.ToLowerInvariant().Contains(filter);
                        }).ToList()
                    })
                    .Where(x => x.Stations.Count > 0).ToList();

                if (matchingComps.Count == 0) continue;

                var allSt = new List<(Border, TextBlock, string)>();
                var lineSection = new StackPanel { Margin = new Thickness(0, 0, 0, 2) };
                var lineHeader = BuildRemoteLineHeader(line.Name);
                lineHeader.MouseLeftButtonUp += (s, e) =>
                {
                    bool any = allSt.Any(x => selectedPaths.Contains(x.Item3));
                    foreach (var (b, nb, p) in allSt)
                    {
                        if (any) selectedPaths.Remove(p);
                        else selectedPaths.Add(p);
                        applySelection(b, nb, p, selectedPaths.Contains(p));
                    }

                    updateLoadBtn();
                };
                lineSection.Children.Add(lineHeader);

                foreach (var item in matchingComps)
                {
                    var compSt = new List<(Border, TextBlock, string)>();
                    var compHeader = BuildRemoteCompHeader(item.Comp.Name);
                    compHeader.MouseLeftButtonUp += (s, e) =>
                    {
                        bool any = compSt.Any(x => selectedPaths.Contains(x.Item3));
                        foreach (var (b, nb, p) in compSt)
                        {
                            if (any) selectedPaths.Remove(p);
                            else selectedPaths.Add(p);
                            applySelection(b, nb, p, selectedPaths.Contains(p));
                        }

                        updateLoadBtn();
                    };
                    lineSection.Children.Add(compHeader);

                    for (int si = 0; si < item.Stations.Count; si++)
                    {
                        var st = item.Stations[si];
                        string capturedPath = st.FullPath;
                        bool isSelected = selectedPaths.Contains(capturedPath);
                        bool isSpecial = st.Category == StationCategory.LCS || st.Category == StationCategory.Backflush;
                        var normalColor = isSpecial ? Color.FromRgb(130, 160, 140) : Color.FromRgb(190, 235, 205);
                        var stName = new TextBlock
                        {
                            Text = st.Name, FontSize = 11,
                            FontWeight = isSelected ? FontWeights.SemiBold : FontWeights.Normal,
                            Foreground = new SolidColorBrush(isSelected ? Color.FromRgb(140, 255, 170) : normalColor),
                            Opacity = isSpecial ? 0.65 : 1.0
                        };
                        var stBorder = BuildRemoteStationRow(st, si == item.Stations.Count - 1, isSelected, isSpecial,
                            stName, capturedPath);

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
                            if (selectedPaths.Contains(capturedPath)) selectedPaths.Remove(capturedPath);
                            else selectedPaths.Add(capturedPath);
                            applySelection(stBorder, stName, capturedPath, selectedPaths.Contains(capturedPath));
                            updateLoadBtn();
                        };
                        compSt.Add((stBorder, stName, capturedPath));
                        allSt.Add((stBorder, stName, capturedPath));
                        lineSection.Children.Add(stBorder);
                    }
                }

                treeStack.Children.Add(lineSection);
            }
        }

        private static Border BuildRemoteLineHeader(string lineName)
        {
            var h = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(25, 65, 38)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(50, 120, 70)),
                BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(8, 5, 8, 5), Cursor = Cursors.Hand,
                Child = new TextBlock
                {
                    Text = "\u25B6  " + lineName, FontSize = 12, FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Color.FromRgb(255, 210, 140))
                }
            };
            h.MouseEnter += (s, e) => h.Background = new SolidColorBrush(Color.FromRgb(35, 90, 50));
            h.MouseLeave += (s, e) => h.Background = new SolidColorBrush(Color.FromRgb(25, 65, 38));
            return h;
        }

        private static Border BuildRemoteCompHeader(string compName)
        {
            var h = new Border
            {
                Padding = new Thickness(18, 3, 8, 3), Cursor = Cursors.Hand,
                Background = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0)),
                Child = new TextBlock
                {
                    Text = "  \u251C\u2500 \U0001F4BB  " + compName, FontSize = 11, FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromRgb(120, 200, 150)), Margin = new Thickness(0, 2, 0, 1)
                }
            };
            h.MouseEnter += (s, e) => h.Background = new SolidColorBrush(Color.FromRgb(15, 40, 22));
            h.MouseLeave += (s, e) => h.Background = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
            return h;
        }

        private static Border BuildRemoteStationRow(
            StationNode st, bool lastS, bool isSelected, bool isSpecial,
            TextBlock stName, string capturedPath)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(new TextBlock
            {
                Text = lastS ? "      \u2514\u2500 " : "      \u251C\u2500 ", FontFamily = new FontFamily("Consolas"),
                FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(50, 100, 65))
            });
            row.Children.Add(stName);
            if (isSpecial)
                row.Children.Add(new TextBlock
                {
                    Text = "  [" + st.Category + "]", FontSize = 9,
                    Foreground = new SolidColorBrush(Color.FromRgb(80, 130, 100)),
                    VerticalAlignment = VerticalAlignment.Center, Opacity = 0.7
                });
            row.Children.Add(new TextBlock
            {
                Text = "  " + capturedPath, FontSize = 9, FontFamily = new FontFamily("Consolas"),
                Foreground = new SolidColorBrush(Color.FromRgb(60, 100, 75)),
                VerticalAlignment = VerticalAlignment.Center
            });
            return new Border
            {
                Padding = new Thickness(4, 2, 6, 2), Margin = new Thickness(0, 0, 0, 1),
                CornerRadius = new CornerRadius(3),
                Background = new SolidColorBrush(isSelected ? Color.FromRgb(20, 80, 35) : Color.FromArgb(0, 0, 0, 0)),
                BorderBrush = isSelected ? new SolidColorBrush(Color.FromRgb(50, 180, 90)) : null,
                BorderThickness = isSelected ? new Thickness(1) : new Thickness(0), Cursor = Cursors.Hand, Child = row
            };
        }


        private UIElement BuildStationTree(List<LineNode> lines)
        {
            var mainStack = new StackPanel();
            foreach (var line in lines)
            {
                var lineSection = new StackPanel { Margin = new Thickness(0, 0, 0, 4) };
                var lineHeader = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(25, 65, 38)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(50, 120, 70)),
                    BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(8, 5, 8, 5),
                    Child = new TextBlock
                    {
                        Text = "▶  " + line.Name, FontSize = 12, FontWeight = FontWeights.Bold,
                        Foreground = new SolidColorBrush(Color.FromRgb(255, 210, 140))
                    }
                };
                lineSection.Children.Add(lineHeader);

                foreach (var comp in line.Computers)
                {
                    var compStack = new StackPanel();
                    compStack.Children.Add(new TextBlock
                    {
                        Text = "├─ 💻  " + comp.Name, FontSize = 11, FontWeight = FontWeights.SemiBold,
                        Foreground = new SolidColorBrush(Color.FromRgb(120, 200, 150)),
                        Margin = new Thickness(0, 2, 0, 1)
                    });

                    for (int si = 0; si < comp.Stations.Count; si++)
                    {
                        var st = comp.Stations[si];
                        string capturedPath = st.FullPath;
                        var stNameBlock = new TextBlock
                        {
                            Text = st.Name, FontSize = 11,
                            Foreground = new SolidColorBrush(Color.FromRgb(190, 235, 205))
                        };
                        var stRow = new StackPanel { Orientation = Orientation.Horizontal };
                        stRow.Children.Add(new TextBlock
                        {
                            Text = si == comp.Stations.Count - 1 ? "    └─ " : "    ├─ ", FontSize = 11,
                            FontFamily = new FontFamily("Consolas"),
                            Foreground = new SolidColorBrush(Color.FromRgb(50, 100, 65))
                        });
                        stRow.Children.Add(stNameBlock);
                        stRow.Children.Add(new TextBlock
                        {
                            Text = "  " + capturedPath, FontSize = 9, FontFamily = new FontFamily("Consolas"),
                            Foreground = new SolidColorBrush(Color.FromRgb(60, 100, 75)),
                            VerticalAlignment = VerticalAlignment.Center
                        });
                        var stBorder = new Border
                        {
                            Padding = new Thickness(4, 2, 6, 2), Margin = new Thickness(0, 0, 0, 1),
                            CornerRadius = new CornerRadius(3),
                            Background = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0)), Cursor = Cursors.Hand,
                            Child = stRow
                        };
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

                    lineSection.Children.Add(new Border { Padding = new Thickness(18, 3, 8, 3), Child = compStack });
                }

                mainStack.Children.Add(lineSection);
            }

            return new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Padding = new Thickness(10),
                Content = mainStack
            };
        }


        private static string ResolveRemotePath()
        {
            if (Directory.Exists(DefaultRemotePath)) return DefaultRemotePath;
            string tail = IOPath.Combine("didv0952", "06_MES_App_Logs");
            foreach (char drive in new[] { 'F', 'T', 'Z', 'Y', 'X', 'W', 'V', 'S', 'R', 'Q' })
            {
                string candidate = drive + ":\\" + tail;
                if (Directory.Exists(candidate)) return candidate;
            }

            return DefaultRemotePath;
        }

        private static string FindSampleDataPath()
        {
            string dir = IOPath.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? "";
            for (int i = 0; i < 5; i++)
            {
                string candidate = IOPath.Combine(dir, "SampleData");
                if (Directory.Exists(candidate)) return candidate;
                string parent = IOPath.GetDirectoryName(dir);
                if (string.IsNullOrEmpty(parent)) break;
                dir = parent;
            }

            return IOPath.Combine(
                IOPath.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? "",
                "SampleData");
        }


        private static List<LineNode> ScanLineStructure(string rootPath)
        {
            var lineMap = new Dictionary<string, LineNode>();
            try
            {
                foreach (var st in DataLoader.FindStations(rootPath))
                {
                    string lineName = string.IsNullOrEmpty(st.LineName) ? "(No Line)" : st.LineName;
                    string compName = string.IsNullOrEmpty(st.ComputerName) ? "(Unknown)" : st.ComputerName;
                    if (!lineMap.TryGetValue(lineName, out var lineNode))
                        lineMap[lineName] = lineNode = new LineNode { Name = lineName, FullPath = rootPath };
                    var comp = lineNode.Computers.FirstOrDefault(c => c.Name == compName);
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

        private static string BuildRawTreeText(string rootPath, List<LineNode> lines)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine(rootPath);
            for (int li = 0; li < lines.Count; li++)
            {
                var line = lines[li];
                bool lastL = li == lines.Count - 1;
                sb.AppendLine((lastL ? "└── " : "├── ") + line.Name);
                string lp = lastL ? "    " : "│   ";
                for (int ci = 0; ci < line.Computers.Count; ci++)
                {
                    var comp = line.Computers[ci];
                    bool lastC = ci == line.Computers.Count - 1;
                    sb.AppendLine(lp + (lastC ? "└── " : "├── ") + comp.Name);
                    string cp = lp + (lastC ? "    " : "│   ");
                    for (int si = 0; si < comp.Stations.Count; si++)
                        sb.AppendLine(cp + (si == comp.Stations.Count - 1 ? "└── " : "├── ") + comp.Stations[si].Name);
                }
            }

            return sb.ToString();
        }

        private static bool HasLogFiles(string dir)
        {
            try
            {
                return Directory.GetFiles(dir).Any(f =>
                {
                    string e = IOPath.GetExtension(f).ToLowerInvariant();
                    return e == ".txt" || e == ".log" || e == ".zip";
                });
            }
            catch
            {
                return false;
            }
        }


        private static string RecentPathFile => IOPath.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MESInsight", "recent.txt");

        private static string StationCacheFile => IOPath.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MESInsight",
            "station_cache.txt");

        public static void SaveRecentPath(string path, List<StationInfo> stations = null)
        {
            try
            {
                string dir = IOPath.GetDirectoryName(RecentPathFile) ?? "";
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                var lines = File.Exists(RecentPathFile)
                    ? File.ReadAllLines(RecentPathFile).ToList()
                    : new List<string>();
                int i = 0;
                while (i < lines.Count)
                {
                    if (lines[i] == "P:" + path)
                    {
                        lines.RemoveAt(i);
                        while (i < lines.Count && lines[i].StartsWith("  S:")) lines.RemoveAt(i);
                    }
                    else i++;
                }

                var entry = new List<string> { "P:" + path };
                if (stations != null)
                    foreach (var st in stations)
                        entry.Add("  S:" + st.StationName + "|" + st.FolderPath + "|" + st.LineName + "|" + st.ComputerName);
                lines.InsertRange(0, entry);
                int pCount = 0, cutAt = lines.Count;
                for (int j = 0; j < lines.Count; j++)
                {
                    if (lines[j].StartsWith("P:")) pCount++;
                    if (pCount > 10)
                    {
                        cutAt = j;
                        break;
                    }
                }

                File.WriteAllLines(RecentPathFile, lines.Take(cutAt).ToList());
            }
            catch
            {
            }
        }

        private static List<RecentEntry> LoadRecentEntries()
        {
            var result = new List<RecentEntry>();
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
                        var p = line.Substring(4).Split(new[] { '|' }, 4);
                        
                        if (p.Length >= 2) current.Stations.Add((
                            p[0],
                            p[1],
                            p.Length > 2 ? p[2] : "",
                            p.Length > 3 ? p[3] : ""
                        ));
                    }
                }
            }
            catch
            {
            }

            return result;
        }

        private static void SaveStationCache(List<LineNode> lines)
        {
            try
            {
                string dir = IOPath.GetDirectoryName(StationCacheFile) ?? "";
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                var sb = new System.Text.StringBuilder();
                foreach (var l in lines)
                {
                    sb.AppendLine("L:" + l.Name + "|" + l.FullPath);
                    foreach (var c in l.Computers)
                    {
                        sb.AppendLine("C:" + c.Name + "|" + c.FullPath);
                        foreach (var s in c.Stations)
                            sb.AppendLine("S:" + s.Name + "|" + s.FullPath + "|" + (int)s.Category);
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
            var result = new List<LineNode>();
            try
            {
                if (!File.Exists(StationCacheFile)) return result;
                LineNode currentLine = null;
                ComputerNode currentComp = null;
                foreach (string raw in File.ReadAllLines(StationCacheFile, System.Text.Encoding.UTF8))
                {
                    if (raw.StartsWith("L:"))
                    {
                        var p = raw.Substring(2).Split(new[] { '|' }, 2);
                        currentLine = new LineNode { Name = p[0], FullPath = p.Length > 1 ? p[1] : "" };
                        currentComp = null;
                        result.Add(currentLine);
                    }
                    else if (raw.StartsWith("C:") && currentLine != null)
                    {
                        var p = raw.Substring(2).Split(new[] { '|' }, 2);
                        currentComp = new ComputerNode { Name = p[0], FullPath = p.Length > 1 ? p[1] : "" };
                        currentLine.Computers.Add(currentComp);
                    }
                    else if (raw.StartsWith("S:") && currentComp != null)
                    {
                        var p = raw.Substring(2).Split(new[] { '|' }, 3);
                        StationCategory cat = StationCategory.GHP;
                        if (p.Length > 2 && int.TryParse(p[2], out int ci)) cat = (StationCategory)ci;
                        currentComp.Stations.Add(new StationNode
                            { Name = p[0], FullPath = p.Length > 1 ? p[1] : "", Category = cat });
                    }
                }
            }
            catch
            {
            }

            return result;
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

        private class RecentEntry
        {
            public string Path { get; set; }
            public List<(string Name, string FolderPath, string LineName, string ComputerName)> Stations { get; set; } = new List<(string, string, string, string)>();
        }

        public enum StartupMode
        {
            Local,
            Remote,
            Sample
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
                    Text = "Recent Data", FontSize = 13, FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromRgb(210, 245, 220)), Margin = new Thickness(0, 0, 0, 12)
                });
                foreach (string path in paths)
                {
                    string captured = path;
                    bool exists = Directory.Exists(path);
                    var row = new Border
                    {
                        Background = new SolidColorBrush(Color.FromRgb(12, 26, 16)),
                        BorderBrush = new SolidColorBrush(Color.FromRgb(26, 70, 38)),
                        BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(5),
                        Padding = new Thickness(12, 8, 12, 8), Margin = new Thickness(0, 0, 0, 5),
                        Cursor = exists ? Cursors.Hand : Cursors.Arrow
                    };
                    var stack = new StackPanel();
                    stack.Children.Add(new TextBlock
                    {
                        Text = IOPath.GetFileName(path.TrimEnd((char)92, '/')), FontSize = 11,
                        FontWeight = FontWeights.SemiBold,
                        Foreground =
                            new SolidColorBrush(exists ? Color.FromRgb(180, 230, 195) : Color.FromRgb(100, 110, 100))
                    });
                    stack.Children.Add(new TextBlock
                    {
                        Text = path, FontSize = 9, FontFamily = new FontFamily("Consolas"),
                        Foreground =
                            new SolidColorBrush(exists ? Color.FromRgb(70, 120, 85) : Color.FromRgb(80, 80, 80))
                    });
                    if (!exists)
                        stack.Children.Add(new TextBlock
                        {
                            Text = "⚠  Path not accessible", FontSize = 9,
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

                var btnCancel = new Button
                {
                    Content = "Cancel", Padding = new Thickness(16, 7, 16, 7), Margin = new Thickness(0, 8, 0, 0),
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Background = new SolidColorBrush(Color.FromRgb(18, 36, 22)),
                    Foreground = new SolidColorBrush(Color.FromRgb(130, 160, 135)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(36, 70, 44)), BorderThickness = new Thickness(1),
                    Cursor = Cursors.Hand
                };
                btnCancel.Click += (s, e) => { DialogResult = false; };
                root.Children.Add(btnCancel);
                Content = root;
            }
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
                    Text = "Additional station types detected", FontSize = 13, FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromRgb(210, 245, 220)), Margin = new Thickness(0, 0, 0, 6)
                });
                root.Children.Add(new TextBlock
                {
                    Text = "Select which types to include in the analysis:", FontSize = 10,
                    Foreground = new SolidColorBrush(Color.FromRgb(110, 160, 125)), Margin = new Thickness(0, 0, 0, 18),
                    TextWrapping = TextWrapping.Wrap
                });
                var cbLcs = new CheckBox
                {
                    Content = "LCS  (" + lcsCount + " station" + (lcsCount != 1 ? "s" : "") + ")", FontSize = 11,
                    Foreground = new SolidColorBrush(Color.FromRgb(180, 225, 195)), IsEnabled = lcsCount > 0,
                    IsChecked = false, Margin = new Thickness(0, 0, 0, 10)
                };
                var cbBackflush = new CheckBox
                {
                    Content = "Backflush  (" + backflushCount + " station" + (backflushCount != 1 ? "s" : "") + ")",
                    FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(180, 225, 195)),
                    IsEnabled = backflushCount > 0, IsChecked = false, Margin = new Thickness(0, 0, 0, 24)
                };
                root.Children.Add(cbLcs);
                root.Children.Add(cbBackflush);
                var btnRow = new StackPanel
                    { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
                var btnConfirm = new Button
                {
                    Content = "Confirm →", Padding = new Thickness(18, 7, 18, 7), FontWeight = FontWeights.SemiBold,
                    Background = new SolidColorBrush(Color.FromRgb(150, 85, 15)),
                    Foreground = new SolidColorBrush(Color.FromRgb(255, 235, 180)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(210, 130, 30)), BorderThickness = new Thickness(1),
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
    }
}