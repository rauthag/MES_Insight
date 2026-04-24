using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using MESInsight.Core;

namespace MESInsight.Charts.Renderers
{
    public class TimelineChartRenderer : ChartRenderer
    {
        public override ChartType GetChartType() => ChartType.Timeline;
        public override int GetMinimumHeight(RenderContext context) => 80;

        public override UIElement Render(ChartData data, RenderContext context)
        {
            if (data?.TimelineEvents == null) return null;
            var day = data.FilteredRecords?.Count > 0
                ? data.FilteredRecords[0].TimestampParsed.Date
                : DateTime.Today;
            return BuildTimelineCanvas(data.TimelineEvents, day, 0);
        }

        public static UIElement BuildTimelineCanvas(List<TimelineEvent> events, DateTime day, double canvasWidth)
        {
            var outerBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(13, 17, 23)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(22, 60, 36)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Margin = new Thickness(0, 4, 0, 0)
            };

            var outerGrid = new Grid();
            outerGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(22) });
            outerGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(36) });
            outerGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(18) });

            var titleBar = new TextBlock
            {
                Text = "Daily Timeline  —  " + day.ToString("dd.MM.yyyy"),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(63, 185, 80)),
                Margin = new Thickness(10, 4, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            var timelineCanvas = new Canvas
            {
                Height = 36,
                Background = new SolidColorBrush(Color.FromRgb(22, 27, 34)),
                ClipToBounds = true
            };

            var hourAxisCanvas = new Canvas { Height = 18 };

            Grid.SetRow(titleBar, 0);
            Grid.SetRow(timelineCanvas, 1);
            Grid.SetRow(hourAxisCanvas, 2);
            outerGrid.Children.Add(titleBar);
            outerGrid.Children.Add(timelineCanvas);
            outerGrid.Children.Add(hourAxisCanvas);
            outerBorder.Child = outerGrid;

            timelineCanvas.Loaded += (s, e) =>
            {
                double width = timelineCanvas.ActualWidth > 0 ? timelineCanvas.ActualWidth : canvasWidth;
                DrawTimelineBlocks(timelineCanvas, events, day, width);
                DrawHourAxis(hourAxisCanvas, width);
            };

            timelineCanvas.SizeChanged += (s, e) =>
            {
                timelineCanvas.Children.Clear();
                hourAxisCanvas.Children.Clear();
                DrawTimelineBlocks(timelineCanvas, events, day, timelineCanvas.ActualWidth);
                DrawHourAxis(hourAxisCanvas, timelineCanvas.ActualWidth);
            };

            return outerBorder;
        }

        private static void DrawTimelineBlocks(Canvas canvas, List<TimelineEvent> events, DateTime day, double width)
        {
            var dayStart = day.Date;
            var dayEnd = day.Date.AddDays(1);
            double totalSeconds = (dayEnd - dayStart).TotalSeconds;
            double marginLeft = 8;
            double drawWidth = Math.Max(0, width - marginLeft - 8);
            double blockHeight = 28;
            double blockTop = 4;

            if (drawWidth <= 0) return;

            var backgroundRect = new Rectangle
            {
                Width = drawWidth, Height = blockHeight,
                Fill = new SolidColorBrush(Color.FromRgb(30, 37, 46)),
                RadiusX = 3, RadiusY = 3
            };
            Canvas.SetLeft(backgroundRect, marginLeft);
            Canvas.SetTop(backgroundRect, blockTop);
            canvas.Children.Add(backgroundRect);

            foreach (var evt in events)
            {
                if (evt.Start.Date != day.Date && (evt.End == null || evt.End.Value.Date != day.Date))
                    continue;

                DateTime clampedStart = evt.Start < dayStart ? dayStart : evt.Start;
                DateTime clampedEnd = (evt.End ?? evt.Start.AddMinutes(1));
                clampedEnd = clampedEnd > dayEnd ? dayEnd : clampedEnd;
                if (clampedEnd <= clampedStart) clampedEnd = clampedStart.AddMinutes(1);

                double xStart = marginLeft + ((clampedStart - dayStart).TotalSeconds / totalSeconds) * drawWidth;
                double blockW = Math.Max(2, ((clampedEnd - clampedStart).TotalSeconds / totalSeconds) * drawWidth);

                var color = GetEventColor(evt.EventType);
                var block = new Border
                {
                    Width = blockW,
                    Height = blockHeight,
                    Background = new SolidColorBrush(Color.FromArgb(200, color.R, color.G, color.B)),
                    BorderBrush = new SolidColorBrush(color),
                    BorderThickness = new Thickness(0, 1, 0, 1),
                    CornerRadius = new CornerRadius(1),
                    ToolTip = BuildBlockTooltip(evt),
                    Cursor = System.Windows.Input.Cursors.Hand
                };

                if (blockW > 20)
                    block.Child = new TextBlock
                    {
                        Text = evt.Label,
                        FontSize = 9,
                        FontWeight = FontWeights.Bold,
                        Foreground = Brushes.White,
                        VerticalAlignment = VerticalAlignment.Center,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        TextTrimming = TextTrimming.CharacterEllipsis,
                        Width = blockW - 2,
                        Margin = new Thickness(1, 0, 0, 0)
                    };

                Canvas.SetLeft(block, xStart);
                Canvas.SetTop(block, blockTop);
                canvas.Children.Add(block);
            }
        }

        private static void DrawHourAxis(Canvas canvas, double width)
        {
            double marginLeft = 8;
            double drawWidth = width - marginLeft - 8;

            for (int hour = 0; hour <= 23; hour += 2)
            {
                double xPos = marginLeft + (hour / 24.0) * drawWidth;

                canvas.Children.Add(new Line
                {
                    X1 = xPos, Y1 = 0, X2 = xPos, Y2 = 5,
                    Stroke = new SolidColorBrush(Color.FromRgb(60, 70, 80)),
                    StrokeThickness = 1
                });

                canvas.Children.Add(new TextBlock
                {
                    Text = hour.ToString("D2"),
                    FontSize = 9,
                    Foreground = new SolidColorBrush(Color.FromRgb(100, 110, 120)),
                    Margin = new Thickness(xPos - 6, 5, 0, 0)
                });
            }
        }

        private static ToolTip BuildBlockTooltip(TimelineEvent evt)
        {
            var panel = new StackPanel { Margin = new Thickness(4) };

            string timeText = evt.End.HasValue
                ? $"{evt.Start:HH:mm:ss} – {evt.End.Value:HH:mm:ss}  ({(int)(evt.End.Value - evt.Start).TotalMinutes}m)"
                : evt.Start.ToString("HH:mm:ss");

            panel.Children.Add(new TextBlock
            {
                Text = timeText,
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(GetEventColor(evt.EventType))
            });

            if (!string.IsNullOrEmpty(evt.Detail))
                panel.Children.Add(new TextBlock
                {
                    Text = evt.Detail,
                    FontSize = 10,
                    Foreground = Brushes.LightGray,
                    Margin = new Thickness(0, 3, 0, 0)
                });

            if (!string.IsNullOrEmpty(evt.ErrorCode))
                panel.Children.Add(new TextBlock
                {
                    Text = "Error: " + evt.ErrorCode,
                    FontSize = 10,
                    Foreground = new SolidColorBrush(Color.FromRgb(248, 81, 73)),
                    Margin = new Thickness(0, 2, 0, 0)
                });

            return new ToolTip
            {
                Content = panel,
                Background = new SolidColorBrush(Color.FromArgb(245, 22, 27, 34)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(56, 139, 253)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8)
            };
        }

        private static Color GetEventColor(TimelineEventType type)
        {
            switch (type)
            {
                case TimelineEventType.Production: return Color.FromRgb(46, 160, 67);
                case TimelineEventType.ProductionFail: return Color.FromRgb(248, 81, 73);
                case TimelineEventType.OeeStop: return Color.FromRgb(230, 126, 34);
                case TimelineEventType.Error: return Color.FromRgb(200, 50, 50);
                case TimelineEventType.MaterialChange: return Color.FromRgb(52, 152, 219);
                case TimelineEventType.SetupChange: return Color.FromRgb(155, 89, 182);
                case TimelineEventType.Idle: return Color.FromRgb(80, 90, 100);
                default: return Color.FromRgb(80, 90, 100);
            }
        }
    }
}