using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MESInsight.Core;

namespace MESInsight
{
    internal class ChevronCallbacks
    {
        public Func<StationInfo, Task> OnClick { get; set; }
        public Action<string> OnClose { get; set; }
        public Func<string, bool> IsActive { get; set; }
        public Func<string, bool> IsLoading { get; set; }
        public Func<string, string> GetCacheName { get; set; }
    }

    internal class LazyChevronCallbacks
    {
        public Func<StationInfo, Task> OnClick { get; set; }
        public Func<string, bool> IsReady { get; set; }
        public Func<string, bool> IsLoading { get; set; }
    }

    internal static class StationChevron
    {
        private const double H = 44;
        private const double TipWidth = 12;
        private const double DeleteW = 62;
        private const int AnimSteps = 14;
        private const int AnimMs = 16;

        private static readonly Dictionary<string, System.Windows.Threading.DispatcherTimer> _glowTimers
            = new Dictionary<string, System.Windows.Threading.DispatcherTimer>();

        public static Canvas Build(StationInfo station, bool isFirst, bool isEmpty, ChevronCallbacks cb)
        {
            string displayName = cb.GetCacheName(station.FolderPath) ?? station.StationName;
            bool isActive = cb.IsActive(station.FolderPath);
            bool isLoading = cb.IsLoading(station.FolderPath);
            var colors = ResolveColors(isActive, isEmpty);
            string subText = BuildSubText(station);
            bool hasSub = !string.IsNullOrEmpty(subText);
            double leftPad = isFirst ? 14 : 22;
            double canvasW = MeasureWidth(displayName, subText, isActive, leftPad);

            var canvas = BuildCanvas(canvasW, station.FolderPath, isFirst);
            var poly = BuildPolygon(canvasW, isFirst, colors.fill, colors.stroke);
            var nameBlock = BuildNameBlock(displayName, isActive, isLoading, colors.name, leftPad, hasSub);

            canvas.Children.Add(poly);
            canvas.Children.Add(nameBlock);

            if (hasSub)
                canvas.Children.Add(BuildSubBlock(subText, colors.sub, leftPad));

            if (isLoading)
                StartLoadingAnimation(nameBlock, displayName, colors.name, cb.IsLoading, station.FolderPath);

            if (isEmpty)
                WireEmptyChevronEvents(canvas, poly, colors);
            else
                WireChevronEvents(canvas, poly, nameBlock, station, colors, cb);

            return canvas;
        }

        public static Canvas BuildLazy(StationInfo station, LazyChevronCallbacks cb)
        {
            bool isLoading = cb.IsLoading(station.FolderPath);
            bool isReady = cb.IsReady(station.FolderPath);
            double canvasW = MeasureLazyWidth(station.StationName);

            var canvas = BuildCanvas(canvasW, station.FolderPath, isFirst: false);
            var poly = BuildLazyPolygon(canvasW, isLoading, isReady);
            var nameBlock = BuildLazyNameBlock(station.StationName, isLoading, isReady);

            canvas.Children.Add(poly);
            Canvas.SetLeft(nameBlock, 22);
            Canvas.SetTop(nameBlock, (H - 14) / 2.0);
            canvas.Children.Add(nameBlock);

            if (isLoading)
                StartShimmerAnimation(canvas, nameBlock, canvasW, station.FolderPath, cb.IsLoading);

            WireLazyHoverEvents(canvas, poly, isReady);
            WireLazyClickEvents(canvas, station, isLoading, cb);
            return canvas;
        }


        public static Canvas BuildDeleteChevron(Action onDelete)
        {
            var canvas = new Canvas
            {
                Width = 0,
                Height = H,
                ClipToBounds = true,
                IsHitTestVisible = false,
                Margin = new Thickness(-TipWidth, 0, 0, 0),
                Cursor = System.Windows.Input.Cursors.Hand
            };

            var poly = new System.Windows.Shapes.Polygon
            {
                Fill = new SolidColorBrush(Color.FromRgb(80, 40, 5)),
                Stroke = new SolidColorBrush(Color.FromRgb(160, 90, 20)),
                StrokeThickness = 1
            };
            RebuildDeletePoly(poly, DeleteW);

            var icon = new TextBlock
            {
                Text = "🗑",
                FontSize = 14,
                FontWeight = FontWeights.Light,
                IsHitTestVisible = false,
                Foreground = new SolidColorBrush(Color.FromRgb(220, 140, 50)),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Opacity = 0
            };
            Canvas.SetLeft(icon, TipWidth + (DeleteW - TipWidth) / 2.0 - 12);
            Canvas.SetTop(icon, (H - 16) / 2.0);

            canvas.Children.Add(poly);
            canvas.Children.Add(icon);

            canvas.MouseEnter += (s, e) =>
                poly.Fill = new SolidColorBrush(Color.FromRgb(110, 55, 8));
            canvas.MouseLeave += (s, e) =>
                poly.Fill = new SolidColorBrush(Color.FromRgb(80, 40, 5));
            canvas.MouseLeftButtonUp += (s, e) =>
            {
                e.Handled = true;
                onDelete();
            };

            return canvas;
        }

        public static void AnimateDeleteOpen(Canvas deleteCanvas)
        {
            deleteCanvas.IsHitTestVisible = true;
            AnimateDeleteWidth(deleteCanvas, from: deleteCanvas.Width, to: DeleteW, opening: true);
        }

        public static void AnimateDeleteClose(Canvas deleteCanvas)
        {
            deleteCanvas.IsHitTestVisible = false;
            AnimateDeleteWidth(deleteCanvas, from: deleteCanvas.Width, to: 0, opening: false);
        }


        private static Canvas BuildCanvas(double canvasW, string folderPath, bool isFirst)
        {
            return new Canvas
            {
                Width = canvasW,
                Height = H,
                Cursor = System.Windows.Input.Cursors.Hand,
                Margin = new Thickness(isFirst ? 0 : -1, 0, 0, 0),
                Tag = folderPath
            };
        }

        private static (Color fill, Color hover, Color stroke, Color name, Color sub) ResolveColors(
            bool isActive, bool isEmpty = false)
        {
            if (isEmpty)
                return (Color.FromRgb(60, 18, 18), Color.FromRgb(80, 25, 25),
                    Color.FromRgb(140, 50, 50), Color.FromRgb(210, 120, 120), Color.FromRgb(170, 90, 90));

            return isActive
                ? (Color.FromRgb(140, 80, 10), Color.FromRgb(170, 100, 15),
                    Color.FromRgb(220, 140, 40), Color.FromRgb(255, 220, 160), Color.FromRgb(220, 170, 100))
                : (Color.FromRgb(22, 110, 55), Color.FromRgb(30, 140, 70),
                    Color.FromRgb(56, 190, 100), Color.FromRgb(210, 245, 220), Color.FromRgb(130, 210, 155));
        }


        private static string BuildSubText(StationInfo station)
        {
            return string.Join("  ·  ", new[] { station.LineName, station.ComputerName }
                .Where(x => !string.IsNullOrEmpty(x)).ToArray());
        }

        private static double MeasureWidth(string displayName, string subText, bool isActive, double leftPad)
        {
            var nb = new TextBlock
            {
                Text = displayName,
                FontSize = 11,
                FontWeight = isActive ? FontWeights.SemiBold : FontWeights.Normal
            };
            nb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double nameW = nb.DesiredSize.Width;

            if (!string.IsNullOrEmpty(subText))
            {
                var sb = new TextBlock { Text = subText, FontSize = 9 };
                sb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                nameW = Math.Max(nameW, sb.DesiredSize.Width);
            }

            return nameW + leftPad + TipWidth + 14;
        }

        private static System.Windows.Shapes.Polygon BuildPolygon(
            double canvasW, bool isFirst, Color fill, Color stroke)
        {
            var poly = new System.Windows.Shapes.Polygon
            {
                Fill = new SolidColorBrush(fill), Stroke = new SolidColorBrush(stroke), StrokeThickness = 1
            };
            poly.Points.Add(new Point(0, 0));
            poly.Points.Add(new Point(canvasW - TipWidth, 0));
            poly.Points.Add(new Point(canvasW, H / 2));
            poly.Points.Add(new Point(canvasW - TipWidth, H));
            poly.Points.Add(new Point(0, H));
            if (!isFirst)
                poly.Points.Add(new Point(TipWidth, H / 2));
            return poly;
        }

        private static TextBlock BuildNameBlock(
            string displayName, bool isActive, bool isLoading,
            Color nameColor, double leftPad, bool hasSub)
        {
            var block = new TextBlock
            {
                Text = displayName,
                FontSize = 11,
                FontWeight = isActive ? FontWeights.SemiBold : FontWeights.Normal,
                Foreground = new SolidColorBrush(isLoading ? Color.FromRgb(180, 140, 60) : nameColor)
            };
            Canvas.SetLeft(block, leftPad);
            Canvas.SetTop(block, hasSub ? (H - 24) / 2.0 : (H - 14) / 2.0);
            return block;
        }

        private static TextBlock BuildSubBlock(string subText, Color subColor, double leftPad)
        {
            var block = new TextBlock
                { Text = subText, FontSize = 9, Foreground = new SolidColorBrush(subColor) };
            Canvas.SetLeft(block, leftPad);
            Canvas.SetTop(block, (H - 24) / 2.0 + 15);
            return block;
        }

        private static void WireEmptyChevronEvents(
            Canvas canvas, System.Windows.Shapes.Polygon poly,
            (Color fill, Color hover, Color stroke, Color name, Color sub) colors)
        {
            canvas.MouseEnter += (s, e) => poly.Fill = new SolidColorBrush(colors.hover);
            canvas.MouseLeave += (s, e) => poly.Fill = new SolidColorBrush(colors.fill);
            canvas.Cursor = System.Windows.Input.Cursors.Arrow;
        }

        private static void WireChevronEvents(
            Canvas canvas, System.Windows.Shapes.Polygon poly, TextBlock nameBlock,
            StationInfo station,
            (Color fill, Color hover, Color stroke, Color name, Color sub) colors,
            ChevronCallbacks cb)
        {
            Canvas deleteCanvas = null;

            canvas.MouseEnter += (s, e) =>
            {
                poly.Fill = new SolidColorBrush(colors.hover);

                if (deleteCanvas == null)
                {
                    deleteCanvas = BuildDeleteChevron(() => cb.OnClose(station.FolderPath));

                    var parent = canvas.Parent as StackPanel;

                    if (parent != null)
                    {
                        int idx = parent.Children.IndexOf(canvas);
                        if (idx >= 0)
                            parent.Children.Insert(idx + 1, deleteCanvas);

                        deleteCanvas.MouseLeave += (f, g) =>
                        {
                            if (IsMouseOverCanvas(canvas)) return;
                            poly.Fill = new SolidColorBrush(colors.fill);
                            AnimateDeleteClose(deleteCanvas);
                        };
                    }
                }

                AnimateDeleteOpen(deleteCanvas);
            };

            canvas.MouseLeave += (s, e) =>
            {
                if (IsMouseOverDeleteCanvas(deleteCanvas)) return;
                poly.Fill = new SolidColorBrush(colors.fill);
                if (deleteCanvas != null)
                    AnimateDeleteClose(deleteCanvas);
            };

            canvas.MouseLeftButtonUp += async (s, e) =>
            {
                if (cb.IsActive(station.FolderPath)) return;
                await cb.OnClick(station);
            };
            poly.MouseLeftButtonUp += async (s, e) =>
            {
                if (cb.IsActive(station.FolderPath)) return;
                await cb.OnClick(station);
            };
            nameBlock.MouseLeftButtonUp += async (s, e) =>
            {
                if (cb.IsActive(station.FolderPath)) return;
                await cb.OnClick(station);
            };
        }

        private static bool IsMouseOverCanvas(Canvas canvas)
        {
            var pos = System.Windows.Input.Mouse.GetPosition(canvas);
            return pos.X >= 0 && pos.X <= canvas.Width && pos.Y >= 0 && pos.Y <= H;
        }

        private static bool IsMouseOverDeleteCanvas(Canvas deleteCanvas)
        {
            if (deleteCanvas == null) return false;
            var pos = System.Windows.Input.Mouse.GetPosition(deleteCanvas);
            return pos.X >= 0 && pos.X <= deleteCanvas.Width && pos.Y >= 0 && pos.Y <= H;
        }

        private static void StartLoadingAnimation(
            TextBlock nameBlock, string displayName, Color nameColor,
            Func<string, bool> isLoading, string folderPath)
        {
            string[] frames = { "  ↻", "  ↺" };
            int frame = 0;
            var timer = new System.Windows.Threading.DispatcherTimer
                { Interval = TimeSpan.FromMilliseconds(150) };

            timer.Tick += (s, e) =>
            {
                frame = (frame + 1) % frames.Length;
                nameBlock.Text = displayName + frames[frame];
                if (!isLoading(folderPath))
                {
                    nameBlock.Text = displayName;
                    nameBlock.Foreground = new SolidColorBrush(nameColor);
                    timer.Stop();
                }
            };
            timer.Start();
        }

        private static void RebuildDeletePoly(System.Windows.Shapes.Polygon poly, double w)
        {
            poly.Points.Clear();
            poly.Points.Add(new Point(0, 0));
            poly.Points.Add(new Point(w - TipWidth, 0));
            poly.Points.Add(new Point(w, H / 2));
            poly.Points.Add(new Point(w - TipWidth, H));
            poly.Points.Add(new Point(0, H));
            poly.Points.Add(new Point(TipWidth, H / 2));
        }

        private static void AnimateDeleteWidth(Canvas canvas, double from, double to, bool opening)
        {
            var poly = canvas.Children[0] as System.Windows.Shapes.Polygon;
            var icon = canvas.Children[1] as TextBlock;
            int step = 0;

            var timer = new System.Windows.Threading.DispatcherTimer
                { Interval = TimeSpan.FromMilliseconds(AnimMs) };

            timer.Tick += (s, e) =>
            {
                step++;
                double t = (double)step / AnimSteps;
                double eased = t < 0.5 ? 2 * t * t : -1 + (4 - 2 * t) * t;
                double current = from + (to - from) * eased;

                canvas.Width = Math.Max(0, current);
                if (icon != null) icon.Opacity = opening ? eased : 1 - eased;
                if (poly != null && current > TipWidth + 2)
                    RebuildDeletePoly(poly, Math.Max(TipWidth + 2, current));

                if (step >= AnimSteps)
                {
                    canvas.Width = to;
                    if (icon != null) icon.Opacity = opening ? 1 : 0;
                    if (poly != null && !opening) RebuildDeletePoly(poly, DeleteW);
                    timer.Stop();
                }
            };
            timer.Start();
        }

        private static double MeasureLazyWidth(string stationName)
        {
            var m = new TextBlock { Text = stationName + "  ⧖⧖", FontSize = 11 };
            m.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            return m.DesiredSize.Width + 22 + TipWidth + 14;
        }

        private static System.Windows.Shapes.Polygon BuildLazyPolygon(
            double canvasW, bool isLoading, bool isReady)
        {
            Color fill = isLoading ? Color.FromRgb(16, 70, 36)
                : isReady ? Color.FromRgb(18, 90, 42)
                : Color.FromRgb(12, 52, 26);
            Color stroke = isLoading ? Color.FromRgb(50, 170, 90)
                : isReady ? Color.FromRgb(80, 220, 120)
                : Color.FromRgb(30, 95, 52);

            var poly = new System.Windows.Shapes.Polygon
            {
                Fill = new SolidColorBrush(fill), Stroke = new SolidColorBrush(stroke),
                StrokeThickness = isReady ? 1.5 : 1.0
            };
            poly.Points.Add(new Point(0, 0));
            poly.Points.Add(new Point(canvasW - TipWidth, 0));
            poly.Points.Add(new Point(canvasW, H / 2));
            poly.Points.Add(new Point(canvasW - TipWidth, H));
            poly.Points.Add(new Point(0, H));
            poly.Points.Add(new Point(TipWidth, H / 2));
            return poly;
        }

        private static TextBlock BuildLazyNameBlock(string stationName, bool isLoading, bool isReady)
        {
            Color color = isLoading ? Color.FromRgb(110, 200, 140)
                : isReady ? Color.FromRgb(160, 255, 190)
                : Color.FromRgb(80, 145, 105);
            return new TextBlock
            {
                Text = stationName + (isReady ? "  ✓" : "  ⧖"),
                FontSize = 11,
                Foreground = new SolidColorBrush(color)
            };
        }

        private static void WireLazyHoverEvents(
            Canvas canvas, System.Windows.Shapes.Polygon poly, bool isReady)
        {
            Color fill = isReady ? Color.FromRgb(18, 90, 42) : Color.FromRgb(12, 52, 26);
            Color hover = isReady ? Color.FromRgb(28, 120, 60) : Color.FromRgb(20, 80, 42);
            canvas.MouseEnter += (s, e) => poly.Fill = new SolidColorBrush(hover);
            canvas.MouseLeave += (s, e) => poly.Fill = new SolidColorBrush(fill);
        }

        private static void WireLazyClickEvents(
            Canvas canvas, StationInfo station, bool isLoading, LazyChevronCallbacks cb)
        {
            canvas.MouseLeftButtonUp += async (s, e) =>
            {
                if (isLoading) return;
                await cb.OnClick(station);
            };
        }

        private static void StartShimmerAnimation(
            Canvas canvas, TextBlock nameBlock, double canvasW,
            string folderPath, Func<string, bool> isLoading)
        {
            var shimmer = BuildShimmerRect(canvasW);
            canvas.Children.Add(shimmer);

            string[] frames = { "⧖", "⧗" };
            int frame = 0;
            double shimmerX = -canvasW * 0.35;
            double speed = canvasW * 1.6 / 30.0;
            int tick = 0;
            string baseName = nameBlock.Text.Split(new[] { ' ' }, 2)[0];

            var timer = new System.Windows.Threading.DispatcherTimer
                { Interval = TimeSpan.FromMilliseconds(30) };

            timer.Tick += (s, e) =>
            {
                if (!isLoading(folderPath))
                {
                    timer.Stop();
                    return;
                }

                shimmerX += speed;
                if (shimmerX > canvasW) shimmerX = -canvasW * 0.35;
                Canvas.SetLeft(shimmer, shimmerX);
                shimmer.Opacity = 1.0;
                tick++;
                if (tick % 10 == 0)
                {
                    frame = (frame + 1) % frames.Length;
                    nameBlock.Text = baseName + "  " + frames[frame];
                }
            };
            timer.Start();
            canvas.Unloaded += (s, e) => timer.Stop();
        }

        private static System.Windows.Shapes.Rectangle BuildShimmerRect(double canvasW)
        {
            var brush = new LinearGradientBrush { StartPoint = new Point(0, 0.5), EndPoint = new Point(1, 0.5) };
            brush.GradientStops.Add(new GradientStop(Color.FromArgb(0, 80, 220, 120), 0.0));
            brush.GradientStops.Add(new GradientStop(Color.FromArgb(120, 140, 255, 170), 0.5));
            brush.GradientStops.Add(new GradientStop(Color.FromArgb(0, 80, 220, 120), 1.0));

            var rect = new System.Windows.Shapes.Rectangle
                { Width = canvasW * 0.35, Height = H, Fill = brush, Opacity = 0, IsHitTestVisible = false };
            Canvas.SetTop(rect, 0);
            Canvas.SetLeft(rect, -canvasW * 0.35);
            return rect;
        }

        private static void StartGlowAnimation(
            System.Windows.Shapes.Polygon poly, TextBlock nameBlock,
            string folderPath, Func<string, bool> isReady)
        {
            double phase = 0;
            var timer = new System.Windows.Threading.DispatcherTimer
                { Interval = TimeSpan.FromMilliseconds(10) };

            timer.Tick += (s, e) =>
            {
                if (!isReady(folderPath))
                {
                    timer.Stop();
                    return;
                }

                phase += 0.08;
                double sin = (Math.Sin(phase) + 1.0) / 2.0;

                poly.Stroke = new SolidColorBrush(Color.FromRgb((byte)(40 + sin * 60), (byte)(160 + sin * 80),
                    (byte)(60 + sin * 40)));
                poly.StrokeThickness = 3.5 + sin * 3.5;

                nameBlock.Foreground = new SolidColorBrush(Color.FromRgb((byte)(130 + sin * 80), (byte)(220 + sin * 35),
                    (byte)(150 + sin * 60)));
            };
            timer.Start();
        }

        public static void StopGlow(string folderPath)
        {
            if (!_glowTimers.TryGetValue(folderPath, out var timer)) return;
            timer.Stop();
            _glowTimers.Remove(folderPath);
        }

        public static void StartGlowOnReadyChevron(Canvas canvas, string folderPath, Func<string, bool> isReady)
        {
            if (_glowTimers.TryGetValue(folderPath, out var existing))
            {
                existing.Stop();
                _glowTimers.Remove(folderPath);
            }

            var poly = canvas.Children.OfType<System.Windows.Shapes.Polygon>().FirstOrDefault();
            var nameBlock = canvas.Children.OfType<TextBlock>().FirstOrDefault();
            if (poly == null || nameBlock == null) return;

            double phase = 0;
            var timer = new System.Windows.Threading.DispatcherTimer
                { Interval = TimeSpan.FromMilliseconds(60) };

            timer.Tick += (s, e) =>
            {
                if (!isReady(folderPath))
                {
                    timer.Stop();
                    _glowTimers.Remove(folderPath);
                    return;
                }

                phase += 0.08;
                double sin = (Math.Sin(phase) + 1.0) / 2.0;
                poly.Stroke = new SolidColorBrush(Color.FromRgb(
                    (byte)(40 + sin * 60), (byte)(160 + sin * 80), (byte)(60 + sin * 40)));
                poly.StrokeThickness = 1.5 + sin * 1.5;
                nameBlock.Foreground = new SolidColorBrush(Color.FromRgb(
                    (byte)(130 + sin * 80), (byte)(220 + sin * 35), (byte)(150 + sin * 60)));
            };

            _glowTimers[folderPath] = timer;
            timer.Start();
        }
    }
}