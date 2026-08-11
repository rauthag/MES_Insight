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
using System.Windows.Interop;
using System.Runtime.InteropServices;
using MESInsight.Core;

namespace MESInsight
{
    public class StartupWindow : Window
    {
        private const int WM_GETMINMAXINFO = 0x0024;
        private const int MONITOR_DEFAULTTONEAREST = 0x00000002;

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MINMAXINFO
        {
            public POINT ptReserved;
            public POINT ptMaxSize;
            public POINT ptMaxPosition;
            public POINT ptMinTrackSize;
            public POINT ptMaxTrackSize;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private class MONITORINFO
        {
            public int cbSize = Marshal.SizeOf(typeof(MONITORINFO));
            public RECT rcMonitor;
            public RECT rcWork;
            public int dwFlags;
        }

        [DllImport("user32")]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, MONITORINFO lpmi);

        [DllImport("user32")]
        private static extern IntPtr MonitorFromWindow(IntPtr handle, int flags);

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

        private static string AppDir =>
            System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? "";

        private static string RecentPathFile => IOPath.Combine(AppDir, "recent.txt");
        private static string StationCacheFile => IOPath.Combine(AppDir, "station_cache.txt");
        private static string RemotePathFile => IOPath.Combine(AppDir, "remote_path.txt");
        private static string SamplePathFile => IOPath.Combine(AppDir, "sample_path.txt");
        private static readonly string SampleDataPath = FindSampleDataPath();

        private Canvas _canvas;
        private Grid _expandedPanel;
        private Border _rootBorder;
        private bool _isExpanded = false;
        private BugReportPanel _bugReportPanel;
        private int _expandedPanelTransitionVersion;


        public StartupWindow()
        {
            Title = "MES Insight";
            Width = 1100;
            Height = 980;
            MinWidth = 900;
            MinHeight = 700;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.Manual;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            FontFamily = new FontFamily(new Uri("pack://application:,,,/"), "./Fonts/#Inter 18pt");
            Content = BuildLayout();
            Icon = TryFindResource("MesAppIcon") as ImageSource;
            StateChanged += (s, e) => UpdateRootBorderForWindowState();
            SourceInitialized += (s, e) => UpdateRootBorderForWindowState();
            Loaded += (s, e) => FitAndCenterToMonitor();
        }


        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            HwndSource source = HwndSource.FromHwnd(hwnd);
            source?.AddHook(WindowProc);
        }

        private void FitAndCenterToMonitor()
        {
            IntPtr anchorHwnd = IntPtr.Zero;

            if (Owner != null)
            {
                anchorHwnd = new WindowInteropHelper(Owner).Handle;
            }

            if (anchorHwnd == IntPtr.Zero)
            {
                anchorHwnd = new WindowInteropHelper(this).Handle;
            }

            if (anchorHwnd == IntPtr.Zero) return;

            IntPtr monitor = MonitorFromWindow(anchorHwnd, MONITOR_DEFAULTTONEAREST);
            if (monitor == IntPtr.Zero) return;

            MONITORINFO monitorInfo = new MONITORINFO();
            if (!GetMonitorInfo(monitor, monitorInfo)) return;

            double scaleX = 1.0;
            double scaleY = 1.0;
            PresentationSource source = PresentationSource.FromVisual(this);
            if (source?.CompositionTarget != null)
            {
                Matrix transformFromDevice = source.CompositionTarget.TransformFromDevice;
                scaleX = transformFromDevice.M11;
                scaleY = transformFromDevice.M22;
            }

            double workLeft = monitorInfo.rcWork.left * scaleX;
            double workTop = monitorInfo.rcWork.top * scaleY;
            double workWidth = (monitorInfo.rcWork.right - monitorInfo.rcWork.left) * scaleX;
            double workHeight = (monitorInfo.rcWork.bottom - monitorInfo.rcWork.top) * scaleY;

            const double marginDip = 24;
            double maxWidth = Math.Max(480, workWidth - (marginDip * 2));
            double maxHeight = Math.Max(360, workHeight - (marginDip * 2));

            MinWidth = Math.Min(MinWidth, maxWidth);
            MinHeight = Math.Min(MinHeight, maxHeight);
            Width = Math.Min(Width, maxWidth);
            Height = Math.Min(Height, maxHeight);

            Left = workLeft + (workWidth - Width) / 2.0;
            Top = workTop + (workHeight - Height) / 2.0;
        }

        internal void ShowBugReportPanel(Exception ex = null)
        {
            int version = BeginExpandedPanelTransition();

            if (_bugReportPanel != null)
            {
                _bugReportPanel.RequestClose -= CloseBugReportPanel;
                _expandedPanel.Children.Remove(_bugReportPanel);
            }

            _bugReportPanel = new BugReportPanel(ex);
            _bugReportPanel.RequestClose += CloseBugReportPanel;
            if (version != _expandedPanelTransitionVersion) return;

            _expandedPanel.Children.Clear();
            _expandedPanel.Children.Add(_bugReportPanel);

            _canvas.Visibility = Visibility.Collapsed;
            _expandedPanel.Opacity = 1;
            _expandedPanel.Visibility = Visibility.Visible;
            _isExpanded = true;
        }

        private void CloseBugReportPanel()
        {
            if (_bugReportPanel != null)
            {
                _bugReportPanel.RequestClose -= CloseBugReportPanel;
                _expandedPanel.Children.Remove(_bugReportPanel);
                _bugReportPanel = null;
            }

            _expandedPanel.Visibility = Visibility.Collapsed;
            _canvas.Visibility = Visibility.Visible;
            _isExpanded = false;
        }

        private IntPtr WindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_GETMINMAXINFO)
            {
                WmGetMinMaxInfo(hwnd, lParam);
                handled = true;
            }

            return IntPtr.Zero;
        }

        private static void WmGetMinMaxInfo(IntPtr hwnd, IntPtr lParam)
        {
            if (lParam == IntPtr.Zero) return;

            MINMAXINFO mmi = Marshal.PtrToStructure<MINMAXINFO>(lParam);
            IntPtr monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);

            if (monitor != IntPtr.Zero)
            {
                MONITORINFO monitorInfo = new MONITORINFO();
                if (GetMonitorInfo(monitor, monitorInfo))
                {
                    RECT rcWorkArea = monitorInfo.rcWork;
                    RECT rcMonitorArea = monitorInfo.rcMonitor;

                    mmi.ptMaxPosition.x = Math.Abs(rcWorkArea.left - rcMonitorArea.left);
                    mmi.ptMaxPosition.y = Math.Abs(rcWorkArea.top - rcMonitorArea.top);
                    mmi.ptMaxSize.x = Math.Abs(rcWorkArea.right - rcWorkArea.left);
                    mmi.ptMaxSize.y = Math.Abs(rcWorkArea.bottom - rcWorkArea.top);
                }
            }

            Marshal.StructureToPtr(mmi, lParam, true);
        }


        private UIElement BuildLayout()
        {
            var outer = new Grid();

            _rootBorder = new Border
            {
                Background = new SolidColorBrush(BgColor),
                BorderBrush = new SolidColorBrush(Color.FromRgb(26, 58, 32)),
                BorderThickness = new Thickness(1),
                SnapsToDevicePixels = true
            };

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

            _rootBorder.Child = root;
            outer.Children.Add(_rootBorder);
            return outer;
        }

        private Border BuildHeader()
        {
            var header = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(14, 55, 28)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(180, 80, 10)),
                BorderThickness = new Thickness(0, 0, 0, 5),
                Cursor = Cursors.SizeAll
            };

            header.MouseLeftButtonDown += TitleBar_MouseLeftButtonDown;

            var headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var hStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(28, 0, 0, 0)
            };
            hStack.Children.Add(BuildAppLogo(34, new Thickness(0, 3, 12, 3)));
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

            Grid.SetColumn(hStack, 0);
            headerGrid.Children.Add(hStack);

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 0, 10, 0)
            };
            buttons.Children.Add(CreateWindowControlButton("🐛︎", 16,
                Color.FromRgb(201, 209, 217), Color.FromRgb(233, 242, 236), Color.FromRgb(26, 42, 50),
                () => ShowBugReportPanel(null)));
            buttons.Children.Add(CreateWindowControlButton("—", 18,
                Color.FromRgb(139, 148, 158), Color.FromRgb(201, 209, 217), Color.FromRgb(26, 42, 33),
                BtnMinimize_Click));
            buttons.Children.Add(CreateWindowControlButton("✕", 14,
                Color.FromRgb(224, 82, 82), Color.FromRgb(255, 179, 184), Color.FromRgb(58, 31, 36),
                BtnClose_Click));

            Grid.SetColumn(buttons, 1);
            headerGrid.Children.Add(buttons);

            header.Child = headerGrid;
            return header;
        }

        private Image BuildAppLogo(double size, Thickness margin)
        {
            ImageSource appLogo = TryFindResource("MesAppIconSmall") as ImageSource
                                  ?? TryFindResource("MesAppIcon") as ImageSource;

            return new Image
            {
                Source = appLogo,
                Width = size,
                Height = size,
                Margin = margin,
                Stretch = Stretch.Uniform,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
        }

        private Border CreateWindowControlButton(
            string glyph,
            double fontSize,
            Color normalForeground,
            Color hoverForeground,
            Color hoverBackground,
            Action onClick)
        {
            var glyphText = new TextBlock
            {
                Text = glyph,
                FontSize = fontSize,
                Foreground = new SolidColorBrush(normalForeground),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center
            };
            if (glyph.Contains("🐛"))
                glyphText.FontFamily = new FontFamily("Segoe UI Symbol");

            var button = new Border
            {
                Width = 40,
                Height = 40,
                Background = Brushes.Transparent,
                CornerRadius = new CornerRadius(4),
                Cursor = Cursors.Hand,
                Margin = new Thickness(2, 0, 0, 0),
                Child = glyphText
            };

            button.MouseLeftButtonDown += (s, e) => e.Handled = true;
            button.MouseLeftButtonUp += (s, e) =>
            {
                e.Handled = true;
                onClick();
            };
            button.MouseEnter += (s, e) =>
            {
                button.Background = new SolidColorBrush(hoverBackground);
                glyphText.Foreground = new SolidColorBrush(hoverForeground);
            };
            button.MouseLeave += (s, e) =>
            {
                button.Background = Brushes.Transparent;
                glyphText.Foreground = new SolidColorBrush(normalForeground);
            };

            return button;
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left) return;

            WindowResizer.DragMove(this);
        }

        private void BtnMinimize_Click()
        {
            SystemCommands.MinimizeWindow(this);
        }

        private void BtnClose_Click()
        {
            SystemCommands.CloseWindow(this);
        }

        private void UpdateRootBorderForWindowState()
        {
            if (_rootBorder == null) return;
            _rootBorder.BorderThickness = WindowState == WindowState.Maximized ? new Thickness(0) : new Thickness(1);
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

            AddHex(canvas, W, H, r, "📁", "LOCAL FOLDER", "Local or network path", 0 * stepX, 0, false);
            AddHex(canvas, W, H, r, "🌐", "REMOTE BACKUP LOGS", "MES Backup disc access needed", 1 * stepX, 0,
                false);
            AddHex(canvas, W, H, r, "📊", "SAMPLE DATA", sampleOk ? "Demo data ready" : "Not available",
                2 * stepX, 0, !sampleOk);
            AddHex(canvas, W, H, r, "🕒", "RECENT DATA", "Last loaded stations", rowOff, stepY, false);
            AddHex(canvas, W, H, r, "🐛", "REPORT BUG", "Send feedback / report issue", rowOff + stepX, stepY, false,
                isBugReport: true);

            return canvas;
        }

        private void AddHex(Canvas canvas,
            double W, double H, double r,
            string icon, string title, string sub,
            double left, double top,
            bool disabled, bool isExit = false, bool isBugReport = false)
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

            const double cornerRadius = 10;
            const double startAngleDeg = -90;

            var outer = new System.Windows.Shapes.Path
            {
                Fill = new SolidColorBrush(HexFill),
                Stroke = new SolidColorBrush(HexFill),
                StrokeThickness = 0.3,
                Data = CreateRoundedHexagon(cx, cy, r, cornerRadius, startAngleDeg)
            };
            var inner = new System.Windows.Shapes.Path
            {
                Fill = new SolidColorBrush(HexFill),
                Stroke = new SolidColorBrush(HexStroke),
                StrokeThickness = 5,
                Data = CreateRoundedHexagon(cx, cy, r * 0.93, cornerRadius * 0.93, startAngleDeg)
            };

            grid.Children.Add(outer);
            grid.Children.Add(inner);

            var stack = new StackPanel
                { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
            stack.Children.Add(new TextBlock
            {
                Text = icon, FontSize = 32, HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = new SolidColorBrush(TextLight), Margin = new Thickness(0, 0, 0, 6),
                FontFamily = new FontFamily("Segoe UI Symbol")
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
                    outer.Stroke = new SolidColorBrush(Color.FromRgb(255, 180, 60));
                    outer.StrokeThickness = 2.5;
                    outer.Effect = new System.Windows.Media.Effects.DropShadowEffect
                        { Color = Color.FromRgb(230, 140, 30), BlurRadius = 30, ShadowDepth = 0, Opacity = 0.8 };
                    inner.Effect = new System.Windows.Media.Effects.DropShadowEffect
                        { Color = Color.FromRgb(230, 140, 30), BlurRadius = 20, ShadowDepth = 0, Opacity = 0.5 };
                };
                grid.MouseLeave += (s, e) =>
                {
                    inner.Fill = new SolidColorBrush(HexFill);
                    outer.Fill = new SolidColorBrush(HexFill);
                    outer.Stroke = new SolidColorBrush(HexStroke);
                    outer.StrokeThickness = 1.5;
                    outer.Effect = null;
                    inner.Effect = null;
                };
                grid.MouseLeftButtonUp += (s, e) => HandleClick(title, isExit, isBugReport);
            }

            Canvas.SetLeft(grid, left);
            Canvas.SetTop(grid, top);
            canvas.Children.Add(grid);
        }

        private static Geometry CreateRoundedHexagon(double cx, double cy, double r, double cornerRadius,
            double startAngleDeg)
        {
            var pts = new Point[6];
            for (int i = 0; i < 6; i++)
            {
                double angle = Math.PI / 180.0 * (startAngleDeg + 60 * i);
                pts[i] = new Point(cx + r * Math.Cos(angle), cy + r * Math.Sin(angle));
            }

            var before = new Point[6];
            var after = new Point[6];
            for (int i = 0; i < 6; i++)
            {
                Point prev = pts[(i + 5) % 6];
                Point curr = pts[i];
                Point next = pts[(i + 1) % 6];

                Vector inDir = curr - prev;
                inDir.Normalize();
                Vector outDir = next - curr;
                outDir.Normalize();

                before[i] = curr - inDir * cornerRadius;
                after[i] = curr + outDir * cornerRadius;
            }

            var figure = new PathFigure { StartPoint = before[0], IsClosed = true, IsFilled = true };
            for (int i = 0; i < 6; i++)
            {
                figure.Segments.Add(new QuadraticBezierSegment(pts[i], after[i], true));
                int nextIdx = (i + 1) % 6;
                if (nextIdx != 0)
                    figure.Segments.Add(new LineSegment(before[nextIdx], true));
            }

            var geo = new PathGeometry();
            geo.Figures.Add(figure);
            geo.Freeze();
            return geo;
        }


        private void HandleClick(string title, bool isExit, bool isBugReport = false)
        {
            if (isExit)
            {
                Application.Current.Shutdown();
                return;
            }

            if (isBugReport)
            {
                ShowBugReportPanel(null);
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
                case "LOCAL FOLDER": ExpandHex(title, () => BuildLocalFolderContent()); break;
                case "REMOTE BACKUP LOGS": ExpandHex(title, () => BuildRemoteContent()); break;
                case "RECENT DATA": ExpandHex(title, () => BuildRecentContent()); break;
            }
        }

        private async void ExpandHex(string title, Func<UIElement> buildContent)
        {
            if (_isExpanded) return;
            _isExpanded = true;
            int version = BeginExpandedPanelTransition();

            foreach (UIElement child in _canvas.Children)
                if (child is Grid g && g.Tag?.ToString() != title)
                    AnimateOpacity(g, 1.0, 0.15, 200);

            var dotsLabel = new TextBlock
            {
                Text = "",
                FontSize = 22,
                Foreground = new SolidColorBrush(Color.FromRgb(180, 100, 20)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 8, 0, 0)
            };
            var loadingText = new TextBlock
            {
                Text = "Loading Remote Folders",
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(255, 210, 140)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 4)
            };
            var spinnerPanel = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            spinnerPanel.Children.Add(loadingText);
            spinnerPanel.Children.Add(dotsLabel);
            _expandedPanel.Children.Add(spinnerPanel);
            _expandedPanel.Opacity = 0;
            _expandedPanel.Visibility = Visibility.Visible;
            AnimateOpacity(_expandedPanel, 0, 1, 180);

            int dotCount = 0;
            var timer = new System.Windows.Threading.DispatcherTimer
                { Interval = TimeSpan.FromMilliseconds(400) };
            timer.Tick += (s, e) =>
            {
                dotCount = (dotCount % 3) + 1;
                dotsLabel.Text = new string('●', dotCount);
            };
            timer.Start();

            await Task.Delay(300);

            if (version != _expandedPanelTransitionVersion)
            {
                timer.Stop();
                return;
            }

            timer.Stop();
            var content = buildContent();

            if (version != _expandedPanelTransitionVersion) return;

            _expandedPanel.Children.Clear();
            _expandedPanel.Children.Add(content);
        }


        private void CollapseBack()
        {
            if (!_isExpanded) return;

            int version = ++_expandedPanelTransitionVersion;

            AnimateOpacity(_expandedPanel, 1, 0, 200, () =>
            {
                if (version != _expandedPanelTransitionVersion) return;
                _expandedPanel.Visibility = Visibility.Collapsed;
                _expandedPanel.Children.Clear();
                _expandedPanel.Opacity = 1;
            });

            foreach (UIElement child in _canvas.Children)
                if (child is Grid g)
                    AnimateOpacity(g, g.Opacity, 1.0, 250);

            _isExpanded = false;
        }

        private int BeginExpandedPanelTransition()
        {
            int version = ++_expandedPanelTransitionVersion;
            _expandedPanel.BeginAnimation(UIElement.OpacityProperty, null);
            _expandedPanel.Children.Clear();
            _expandedPanel.Opacity = 1;
            _expandedPanel.Visibility = Visibility.Visible;
            return version;
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
                Margin = new Thickness(0, 0, 0, 6)
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

            var inner = new StackPanel();
            var rows = new List<(Border row, RecentEntry entry)>();

            foreach (var entry in entries)
            {
                var row = BuildRecentEntryRowPending(entry);
                inner.Children.Add(row);
                rows.Add((row, entry));
            }

            var stack = new StackPanel();
            stack.Children.Add(new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = inner
            });

            var result = WrapExpanded("\u21BB  Recent Data", stack);

            System.Threading.Tasks.Task.Run(() =>
            {
                var paths = entries.Select(e => e.Path).ToList();
                var existsMap = CheckPathsExist(paths);
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    for (int i = 0; i < rows.Count; i++)
                    {
                        var (row, entry) = rows[i];
                        bool exists = existsMap.ContainsKey(entry.Path) && existsMap[entry.Path];
                        UpdateRecentEntryRow(row, entry, exists);
                    }
                }));
            });

            return result;
        }

        private Border BuildRecentEntryRowPending(RecentEntry entry)
        {
            string folderName = IOPath.GetFileName(entry.Path.TrimEnd('\\', '/'));

            var rowStack = new StackPanel();
            rowStack.Children.Add(new TextBlock
            {
                Text = folderName,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(160, 190, 170))
            });
            rowStack.Children.Add(new TextBlock
            {
                Text = entry.Path,
                FontSize = 10,
                FontFamily = new FontFamily("Consolas"),
                Foreground = new SolidColorBrush(Color.FromRgb(70, 100, 80)),
                TextWrapping = TextWrapping.NoWrap
            });
            rowStack.Children.Add(new TextBlock
            {
                Text = "⏳  Checking...",
                FontSize = 9,
                Foreground = new SolidColorBrush(Color.FromRgb(120, 140, 110)),
                Margin = new Thickness(0, 3, 0, 0)
            });

            return new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(12, 30, 18)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(30, 70, 40)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(14, 10, 14, 10),
                Margin = new Thickness(0, 0, 0, 6),
                Cursor = Cursors.Arrow,
                Child = rowStack,
                Tag = entry
            };
        }

        private void UpdateRecentEntryRow(Border row, RecentEntry entry, bool exists)
        {
            if (!(row.Child is StackPanel rowStack)) return;
            rowStack.Children.Clear();

            string folderName = IOPath.GetFileName(entry.Path.TrimEnd('\\', '/'));
            rowStack.Children.Add(BuildRecentEntryTitle(folderName, exists));
            rowStack.Children.Add(BuildRecentEntryPath(entry.Path, exists));

            if (!exists)
                rowStack.Children.Add(BuildRecentEntryWarning());
            else if (entry.Stations.Count > 0)
                rowStack.Children.Add(BuildRecentEntryStationInfo(entry));

            row.Background = new SolidColorBrush(exists ? Color.FromRgb(12, 30, 18) : Color.FromRgb(20, 14, 14));
            row.BorderBrush = new SolidColorBrush(exists ? Color.FromRgb(30, 70, 40) : Color.FromRgb(60, 30, 30));
            row.Cursor = exists ? Cursors.Hand : Cursors.Arrow;

            if (exists)
                WireRecentEntryClick(row, entry);
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
            Parallel.ForEach(paths, new ParallelOptions { MaxDegreeOfParallelism = 4 }, p =>
            {
                bool ex = false;
                try
                {
                    if (p.StartsWith(@"\\"))
                    {
                        // UNC paths need more time for first connection
                        var task = System.Threading.Tasks.Task.Run(() => Directory.Exists(p));
                        ex = task.Wait(8000) && task.Result;
                    }
                    else
                    {
                        ex = Directory.Exists(p);
                    }
                }
                catch { ex = false; }
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
            if (lines.Count > 0) text += "  ·  " + string.Join(", ", lines);
            if (computers.Count > 0) text += "  ·  " + string.Join(", ", computers);

            return new TextBlock
            {
                Text = text, FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 160, 120)),
                Margin = new Thickness(0, 3, 0, 0)
            };
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

            var root = new Grid { Margin = new Thickness(36, 12, 36, 12) };
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

            var pathBar = BuildRemotePathBar();
            Grid.SetRow(pathBar, 2);
            root.Children.Add(pathBar);

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
            StartRemoteConnectProbe(root, rawText, treeStack, allLines, renderTree, hasCached);
            return root;
        }

        private Border BuildRemotePathBar()
        {
            string currentPath = ResolveRemotePath();
            var pathLabel = new TextBlock
            {
                Text = currentPath, FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(139, 148, 158)),
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            var pathInput = new TextBox
            {
                Text = currentPath, FontSize = 11,
                Background = new SolidColorBrush(Color.FromRgb(22, 27, 34)),
                Foreground = new SolidColorBrush(Color.FromRgb(200, 210, 220)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(56, 139, 253)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(6, 4, 6, 4),
                VerticalAlignment = VerticalAlignment.Center,
                Visibility = Visibility.Collapsed
            };
            var editBtn = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(33, 38, 45)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(56, 68, 84)),
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8, 4, 8, 4),
                Cursor = System.Windows.Input.Cursors.Hand, Margin = new Thickness(6, 0, 0, 0),
                Child = new TextBlock
                {
                    Text = "✎ Edit path", FontSize = 11,
                    Foreground = new SolidColorBrush(Color.FromRgb(139, 148, 158))
                }
            };
            var confirmBtn = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(20, 60, 25)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(46, 160, 67)),
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8, 4, 8, 4),
                Cursor = System.Windows.Input.Cursors.Hand, Margin = new Thickness(6, 0, 0, 0),
                Visibility = Visibility.Collapsed,
                Child = new TextBlock
                {
                    Text = "✓ Save", FontSize = 11,
                    Foreground = new SolidColorBrush(Color.FromRgb(140, 255, 170))
                }
            };
            var cancelBtn = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(40, 22, 22)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(120, 40, 40)),
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8, 4, 8, 4),
                Cursor = System.Windows.Input.Cursors.Hand, Margin = new Thickness(6, 0, 0, 0),
                Visibility = Visibility.Collapsed,
                Child = new TextBlock
                {
                    Text = "✕", FontSize = 11,
                    Foreground = new SolidColorBrush(Color.FromRgb(240, 140, 140))
                }
            };
            var browseInlineBtn = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(33, 38, 45)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(56, 68, 84)),
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8, 4, 8, 4),
                Cursor = System.Windows.Input.Cursors.Hand, Margin = new Thickness(6, 0, 0, 0),
                Child = new TextBlock
                {
                    Text = "📂 Browse", FontSize = 11,
                    Foreground = new SolidColorBrush(Color.FromRgb(255, 200, 100))
                }
            };
            browseInlineBtn.MouseLeftButtonUp += (s, e) =>
            {
                var browser = new System.Windows.Forms.FolderBrowserDialog
                {
                    Description = "Select station folder", SelectedPath = ResolveRemotePath(),
                    ShowNewFolderButton = false
                };
                if (browser.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
                SaveRemotePathOverride(browser.SelectedPath);
                pathLabel.Text = browser.SelectedPath;
                pathInput.Text = browser.SelectedPath;
            };
            var row = new DockPanel { Margin = new Thickness(0, 4, 0, 4), LastChildFill = true };
            DockPanel.SetDock(cancelBtn, Dock.Right);
            DockPanel.SetDock(confirmBtn, Dock.Right);
            DockPanel.SetDock(editBtn, Dock.Right);
            DockPanel.SetDock(browseInlineBtn, Dock.Right);
            row.Children.Add(cancelBtn);
            row.Children.Add(confirmBtn);
            row.Children.Add(editBtn);
            row.Children.Add(browseInlineBtn);
            row.Children.Add(pathLabel);
            row.Children.Add(pathInput);
            Action commitEdit = () =>
            {
                string newPath = pathInput.Text.Trim();
                if (!string.IsNullOrEmpty(newPath))
                {
                    SaveRemotePathOverride(newPath);
                    pathLabel.Text = newPath;
                }

                pathLabel.Visibility = Visibility.Visible;
                pathInput.Visibility = Visibility.Collapsed;
                editBtn.Visibility = Visibility.Visible;
                confirmBtn.Visibility = Visibility.Collapsed;
                cancelBtn.Visibility = Visibility.Collapsed;
            };
            editBtn.MouseLeftButtonUp += (s, e) =>
            {
                pathLabel.Visibility = Visibility.Collapsed;
                pathInput.Visibility = Visibility.Visible;
                editBtn.Visibility = Visibility.Collapsed;
                confirmBtn.Visibility = Visibility.Visible;
                cancelBtn.Visibility = Visibility.Visible;
                pathInput.Focus();
                pathInput.SelectAll();
            };
            confirmBtn.MouseLeftButtonUp += (s, e) => commitEdit();
            cancelBtn.MouseLeftButtonUp += (s, e) =>
            {
                pathInput.Text = pathLabel.Text;
                pathLabel.Visibility = Visibility.Visible;
                pathInput.Visibility = Visibility.Collapsed;
                editBtn.Visibility = Visibility.Visible;
                confirmBtn.Visibility = Visibility.Collapsed;
                cancelBtn.Visibility = Visibility.Collapsed;
            };
            pathInput.KeyDown += (s, e) =>
            {
                if (e.Key == System.Windows.Input.Key.Enter) commitEdit();
            };
            return new Border { Child = row, Padding = new Thickness(0, 2, 0, 2) };
        }

        private void StartRemoteConnectProbe(
            Grid root, TextBlock rawText, StackPanel treeStack,
            List<LineNode> allLines, Action renderTree, bool hasCached)
        {
            if (Directory.Exists(ResolveRemotePath())) return;
            var connectingBar = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(28, 22, 14)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(180, 120, 30)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(14, 10, 14, 10),
                Margin = new Thickness(0, 6, 0, 6)
            };

            var content = new StackPanel { Orientation = Orientation.Horizontal };
            content.Children.Add(new TextBlock
            {
                Text = "⚠",
                FontSize = 18,
                Foreground = new SolidColorBrush(Color.FromRgb(210, 140, 30)),
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 0, 12, 0)
            });

            var textStack = new StackPanel();
            textStack.Children.Add(new TextBlock
            {
                Text = "Cannot connect to remote backup disc",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(240, 180, 80))
            });

            textStack.Children.Add(new TextBlock
            {
                Text = "Make sure you are connected to:  " + ResolveRemotePath(),
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(180, 155, 100)),
                Margin = new Thickness(0, 4, 0, 0),
                TextWrapping = TextWrapping.Wrap
            });

            textStack.Children.Add(new TextBlock
            {
                Text =
                    "If you cannot connect, open File Explorer and navigate to the remote disc manually — this will trigger the connection.",
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(140, 120, 80)),
                Margin = new Thickness(0, 6, 0, 0),
                TextWrapping = TextWrapping.Wrap
            });

            var statusText = new TextBlock
            {
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 90, 60)),
                Margin = new Thickness(0, 4, 0, 0)
            };

            textStack.Children.Add(statusText);
            content.Children.Add(textStack);
            connectingBar.Child = content;

            Grid.SetRow(connectingBar, 2);
            Grid.SetColumnSpan(connectingBar, 10);
            root.Children.Add(connectingBar);

            int attempt = 0;

            var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };

            timer.Tick += (s, e) =>
            {
                attempt++;
                string target = ResolveRemotePath();
                statusText.Text = "Trying to connect to: " + target +
                                  (attempt > 1 ? "  (attempt " + attempt + ")" : "");
                Task.Run(() =>
                {
                    if (attempt == 1)
                        try
                        {
                            var psi = new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = "explorer.exe",
                                Arguments = "\"" + target + "\"",
                                UseShellExecute = true
                            };
                            System.Diagnostics.Process.Start(psi);
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine("Explorer open failed: " + ex.Message);
                        }

                    bool ok = Directory.Exists(target);

                    Dispatcher.Invoke(() =>
                    {
                        if (!ok) return;
                        timer.Stop();
                        root.Children.Remove(connectingBar);

                        if (allLines.Count == 0)
                        {
                            var fresh = ScanLineStructure(target);
                            allLines.Clear();
                            allLines.AddRange(fresh);
                            rawText.Text = BuildRawTreeText(target, fresh);
                            SaveStationCache(allLines);
                        }

                        renderTree();
                    });
                });
            };
            timer.Start();
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
            var titleRow = new Grid { Margin = new Thickness(0, 0, 0, 4) };
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
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Background = new SolidColorBrush(Color.FromRgb(8, 20, 12)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(30, 70, 40)),
                BorderThickness = new Thickness(1),
                Content = treeStack,
                VerticalAlignment = VerticalAlignment.Stretch
            };

            var rawScroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                Background = new SolidColorBrush(Color.FromRgb(5, 14, 8)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(30, 70, 40)),
                BorderThickness = new Thickness(0, 1, 1, 1),
                Content = rawText,
                VerticalAlignment = VerticalAlignment.Stretch
            };

            Grid.SetColumn(treeScroll, 0);
            Grid.SetColumn(rawScroll, 1);
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
            string saved = LoadRemotePathOverride();
            if (!string.IsNullOrEmpty(saved) && Directory.Exists(saved)) return saved;
            if (Directory.Exists(DefaultRemotePath)) return DefaultRemotePath;
            string tail = IOPath.Combine("didv0952", "06_MES_App_Logs");
            foreach (char drive in new[] { 'F', 'T', 'Z', 'Y', 'X', 'W', 'V', 'S', 'R', 'Q' })
            {
                string candidate = drive + ":\\" + tail;
                if (Directory.Exists(candidate)) return candidate;
            }

            return DefaultRemotePath;
        }

        private static string LoadRemotePathOverride()
        {
            try
            {
                return File.Exists(RemotePathFile) ? File.ReadAllText(RemotePathFile).Trim() : null;
            }
            catch
            {
                return null;
            }
        }

        private static void SaveRemotePathOverride(string path)
        {
            try
            {
                File.WriteAllText(RemotePathFile, path.Trim());
            }
            catch
            {
            }
        }

        private static string FindSampleDataPath()
        {
            // 1) Explicit override from file next to exe (sample_path.txt)
            string configuredPath = LoadSamplePathOverride();
            if (!string.IsNullOrEmpty(configuredPath) && Directory.Exists(configuredPath))
                return configuredPath;

            // 2) Optional environment override
            string envPath = NormalizeConfiguredPath(Environment.GetEnvironmentVariable("MESINSIGHT_SAMPLE_DATA"));
            if (!string.IsNullOrEmpty(envPath) && Directory.Exists(envPath))
                return envPath;

            // 3) Auto-discovery: walk up from executable folder
            string dir = IOPath.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? "";
            for (int i = 0; i < 5; i++)
            {
                string candidate = IOPath.Combine(dir, "SampleData");
                if (Directory.Exists(candidate)) return candidate;
                string parent = IOPath.GetDirectoryName(dir);
                if (string.IsNullOrEmpty(parent)) break;
                dir = parent;
            }

            // 4) Final fallback: SampleData next to executable
            return IOPath.Combine(
                IOPath.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? "",
                "SampleData");
        }

        private static string LoadSamplePathOverride()
        {
            try
            {
                if (!File.Exists(SamplePathFile)) return null;
                return NormalizeConfiguredPath(File.ReadAllText(SamplePathFile));
            }
            catch
            {
                return null;
            }
        }

        private static string NormalizeConfiguredPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;

            string normalized = path.Trim().Trim('"');
            if (normalized.Length == 0) return null;

            if (!IOPath.IsPathRooted(normalized))
                normalized = IOPath.GetFullPath(IOPath.Combine(AppDir, normalized));
            else
                normalized = IOPath.GetFullPath(normalized);

            return normalized;
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

        private static readonly (string Drive, string UncRoot)[] DriveToUncMap =
        {
            (@"F:\", @"\\vt1.vitesco.com\smt\"),
            (@"T:\", @"\\vt1.vitesco.com\fst1\"),
        };

        private const string UncShareTail = "didv0952";

        private static string NormalizeRecentPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;
            path = path.Trim();

            foreach (var (drive, uncRoot) in DriveToUncMap)
            {
                if (path.StartsWith(drive, StringComparison.OrdinalIgnoreCase))
                    return uncRoot + path.Substring(drive.Length);
            }

            return path;
        }

        public static void SaveRecentPath(string path, List<StationInfo> stations = null)
        {
            try
            {
                path = NormalizeRecentPath(path);

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
                        entry.Add("  S:" + st.StationName + "|" + NormalizeRecentPath(st.FolderPath) + "|" + st.LineName + "|" +
                                  st.ComputerName);
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
                        current = new RecentEntry { Path = NormalizeRecentPath(line.Substring(2)) };
                        result.Add(current);
                    }
                    else if (line.StartsWith("  S:") && current != null)
                    {
                        var p = line.Substring(4).Split(new[] { '|' }, 4);

                        if (p.Length >= 2)
                            current.Stations.Add((
                                p[0],
                                NormalizeRecentPath(p[1]),
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

            public List<(string Name, string FolderPath, string LineName, string ComputerName)> Stations { get; set; } =
                new List<(string, string, string, string)>();
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
                WindowStyle = WindowStyle.None;
                AllowsTransparency = true;
                Background = Brushes.Transparent;

                var root = new Grid();
                root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                var frame = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(8, 14, 10)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(26, 70, 44)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(8)
                };

                var contentGrid = new Grid();
                contentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                contentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                var titleBar = new Grid
                {
                    Background = new SolidColorBrush(Color.FromRgb(14, 55, 28)),
                    Height = 38
                };
                titleBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                titleBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                titleBar.MouseLeftButtonDown += (s, e) => WindowResizer.DragMove(this);

                titleBar.Children.Add(new TextBlock
                {
                    Text = "Recent Data", FontSize = 13, FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromRgb(210, 245, 220)),
                    Margin = new Thickness(16, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center
                });

                var btnClose = new Border
                {
                    Width = 34,
                    Height = 34,
                    Background = Brushes.Transparent,
                    CornerRadius = new CornerRadius(4),
                    Cursor = Cursors.Hand,
                    Margin = new Thickness(0, 2, 6, 2),
                    Child = new TextBlock
                    {
                        Text = "✕",
                        FontSize = 13,
                        Foreground = new SolidColorBrush(Color.FromRgb(224, 82, 82)),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        TextAlignment = TextAlignment.Center
                    }
                };
                btnClose.MouseEnter += (s, e) =>
                {
                    btnClose.Background = new SolidColorBrush(Color.FromRgb(58, 31, 36));
                    ((TextBlock)btnClose.Child).Foreground = new SolidColorBrush(Color.FromRgb(255, 179, 184));
                };
                btnClose.MouseLeave += (s, e) =>
                {
                    btnClose.Background = Brushes.Transparent;
                    ((TextBlock)btnClose.Child).Foreground = new SolidColorBrush(Color.FromRgb(224, 82, 82));
                };
                btnClose.MouseLeftButtonUp += (s, e) => SystemCommands.CloseWindow(this);
                Grid.SetColumn(btnClose, 1);
                titleBar.Children.Add(btnClose);

                Grid.SetRow(titleBar, 0);
                contentGrid.Children.Add(titleBar);

                var body = new StackPanel { Margin = new Thickness(20, 16, 20, 16) };
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

                    body.Children.Add(row);
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
                body.Children.Add(btnCancel);

                Grid.SetRow(body, 1);
                contentGrid.Children.Add(body);
                frame.Child = contentGrid;
                root.Children.Add(frame);
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
                WindowStyle = WindowStyle.None;
                AllowsTransparency = true;
                Background = Brushes.Transparent;

                var root = new Grid();
                var frame = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(8, 14, 10)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(26, 70, 44)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(8)
                };

                var contentGrid = new Grid();
                contentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                contentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

                var titleBar = new Grid
                {
                    Background = new SolidColorBrush(Color.FromRgb(14, 55, 28)),
                    Height = 38
                };
                titleBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                titleBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                titleBar.MouseLeftButtonDown += (s, e) => WindowResizer.DragMove(this);
                titleBar.Children.Add(new TextBlock
                {
                    Text = "Station Types Found", FontSize = 13, FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromRgb(210, 245, 220)),
                    Margin = new Thickness(16, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center
                });

                var btnClose = new Border
                {
                    Width = 34,
                    Height = 34,
                    Background = Brushes.Transparent,
                    CornerRadius = new CornerRadius(4),
                    Cursor = Cursors.Hand,
                    Margin = new Thickness(0, 2, 6, 2),
                    Child = new TextBlock
                    {
                        Text = "✕",
                        FontSize = 13,
                        Foreground = new SolidColorBrush(Color.FromRgb(224, 82, 82)),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        TextAlignment = TextAlignment.Center
                    }
                };
                btnClose.MouseEnter += (s, e) =>
                {
                    btnClose.Background = new SolidColorBrush(Color.FromRgb(58, 31, 36));
                    ((TextBlock)btnClose.Child).Foreground = new SolidColorBrush(Color.FromRgb(255, 179, 184));
                };
                btnClose.MouseLeave += (s, e) =>
                {
                    btnClose.Background = Brushes.Transparent;
                    ((TextBlock)btnClose.Child).Foreground = new SolidColorBrush(Color.FromRgb(224, 82, 82));
                };
                btnClose.MouseLeftButtonUp += (s, e) => SystemCommands.CloseWindow(this);
                Grid.SetColumn(btnClose, 1);
                titleBar.Children.Add(btnClose);

                Grid.SetRow(titleBar, 0);
                contentGrid.Children.Add(titleBar);

                var body = new StackPanel { Margin = new Thickness(24, 20, 24, 20) };
                body.Children.Add(new TextBlock
                {
                    Text = "Additional station types detected", FontSize = 13, FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromRgb(210, 245, 220)), Margin = new Thickness(0, 0, 0, 6)
                });
                body.Children.Add(new TextBlock
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
                body.Children.Add(cbLcs);
                body.Children.Add(cbBackflush);
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
                body.Children.Add(btnRow);

                Grid.SetRow(body, 1);
                contentGrid.Children.Add(body);
                frame.Child = contentGrid;
                root.Children.Add(frame);
                Content = root;
            }
        }
    }
}