using RTAnalyzer.Core;
using RTAnalyzer.Charts;
using RTAnalyzer.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LiveCharts;
using LiveCharts.Wpf;

namespace RTAnalyzer.Charts.Renderers
{
    public class HistogramChartRenderer : ChartRenderer
    {
        private readonly DayRecordsPanelBuilder _dayRecordsPanelBuilder;

        public HistogramChartRenderer(DayRecordsPanelBuilder dayRecordsPanelBuilder)
        {
            _dayRecordsPanelBuilder = dayRecordsPanelBuilder;
        }

        public override ChartType GetChartType()                       => ChartType.Histogram;
        public override int        GetMinimumHeight(RenderContext context) => (int)(context.AvailableHeightPixels * 0.45);

        public override UIElement Render(ChartData data, RenderContext context)
        {
            if (data?.Charts == null || data.Charts.Count == 0) return null;

            var chart       = data.Charts[0];
            int chartHeight = (int)(context.AvailableHeightPixels * 0.45);

            return WrapInSectionBorder(BuildHistogramGrid(chart, chartHeight, data.FilteredRecords, context), isHistogram: true);
        }

        private Grid BuildHistogramGrid(ChartSeries histogramData, int chartHeightPixels, List<ResponseRecord> allRecords, RenderContext context)
        {
            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(36) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(chartHeightPixels) });

            // Title
            var titleBlock = new TextBlock
            {
                Text       = histogramData.Name,
                FontSize   = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(63, 185, 80)),
                Margin     = new Thickness(10, 8, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            // Chart
            int bucketCount = histogramData.Labels.Length;
            int labelStep   = bucketCount > 60 ? 5 : bucketCount > 30 ? 2 : 1;

            var cartesianChart = new CartesianChart
            {
                Series            = histogramData.Series,
                LegendLocation    = LegendLocation.None,
                Margin            = new Thickness(10, 0, 30, 10),
                DisableAnimations = false,
                DataTooltip       = BuildDarkStyledTooltip(),
                FontSize          = 11,
                FontWeight        = System.Windows.FontWeights.Normal
            };

            // Make AVG/P95 data labels bold and larger via UpdaterTick
            cartesianChart.UpdaterTick += _ =>
                cartesianChart.Dispatcher.BeginInvoke(new Action(() =>
                {
                    foreach (var tb in FindVisualChildren<System.Windows.Controls.TextBlock>(cartesianChart))
                    {
                        if (tb.Text != null && (tb.Text.Contains("AVG") || tb.Text.Contains("P₉₅")))
                        {
                            tb.FontSize   = 13;
                            tb.FontWeight = System.Windows.FontWeights.Bold;
                            tb.Foreground = tb.Text.Contains("AVG")
                                ? new SolidColorBrush(Color.FromRgb(56, 182, 255))
                                : new SolidColorBrush(Color.FromRgb(180, 80, 220));
                        }
                    }
                }), System.Windows.Threading.DispatcherPriority.Render);

            // Y axis — response time buckets
            // Use LabelFormatter so LiveCharts picks density itself — no missing labels
            var allLabels = histogramData.Labels;

            cartesianChart.AxisY.Add(new Axis
            {
                Title          = "Response Time (ms)",
                Labels         = allLabels,
                FontSize       = 10,
                Foreground     = new SolidColorBrush(Color.FromRgb(139, 148, 158)),
                Separator      = new LiveCharts.Wpf.Separator
                {
                    StrokeThickness = 0.3,
                    Stroke          = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255))
                }
            });

            // X axis — occurrences
            cartesianChart.AxisX.Add(new Axis
            {
                Title          = "Occurrences",
                MinValue       = 0,
                FontSize       = 11,
                Foreground     = new SolidColorBrush(Color.FromRgb(139, 148, 158)),
                LabelFormatter = v => v.ToString("N0"),
                Separator      = new LiveCharts.Wpf.Separator
                {
                    StrokeThickness = 0.3,
                    Stroke          = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255))
                }
            });

            // Click on bar → open day records panel with filtered records for that bucket
            cartesianChart.DataClick += (sender, point) =>
            {
                if (point == null || allRecords == null || histogramData.Buckets == null) return;

                int idx = (int)Math.Round(point.Y);
                if (idx < 0 || idx >= histogramData.Buckets.Count) return;

                var bucket = histogramData.Buckets[idx];
                var records = allRecords
                    .Where(r => r.ResponseTime >= bucket.RangeStart && r.ResponseTime <= bucket.RangeEnd)
                    .ToList();

                ShowBucketRecordsPopup(bucket.Label, records);
            };

            Grid.SetRow(titleBlock,      0);
            Grid.SetRow(cartesianChart,  1);
            grid.Children.Add(titleBlock);
            grid.Children.Add(cartesianChart);
            return grid;
        }

        private void ShowBucketRecordsPopup(string bucketLabel, List<ResponseRecord> records)
        {
            var panel = new StackPanel { Margin = new Thickness(8) };

            panel.Children.Add(new TextBlock
            {
                Text       = $"Records in bucket {bucketLabel} ms  ({records.Count} total)",
                FontSize   = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(63, 185, 80)),
                Margin     = new Thickness(0, 0, 0, 8)
            });

            var scroll = new ScrollViewer
            {
                MaxHeight        = 400,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };

            var list = new StackPanel();
            foreach (var r in records.OrderByDescending(r => r.ResponseTime).Take(200))
            {
                list.Children.Add(new Border
                {
                    BorderBrush     = new SolidColorBrush(Color.FromRgb(33, 38, 45)),
                    BorderThickness = new Thickness(0, 0, 0, 1),
                    Padding         = new Thickness(0, 4, 0, 4),
                    Child           = new TextBlock
                    {
                        Text       = $"{r.TimestampParsed:dd.MM.yyyy HH:mm:ss}   {r.ResponseTime} ms   {r.Uid ?? r.UidIn ?? ""}",
                        FontSize   = 11,
                        Foreground = new SolidColorBrush(Color.FromRgb(200, 210, 220))
                    }
                });
            }

            scroll.Content = list;
            panel.Children.Add(scroll);

            var window = new Window
            {
                Title           = $"Bucket: {bucketLabel} ms",
                Content         = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(13, 17, 23)),
                    Child      = panel
                },
                Width           = 600,
                Height          = 520,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Background      = new SolidColorBrush(Color.FromRgb(13, 17, 23))
            };
            window.ShowDialog();
        }
    }
}