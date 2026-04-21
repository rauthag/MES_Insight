using RTAnalyzer.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace RTAnalyzer.UI
{
    public class DayRecordsPanelBuilder
    {
        private const int DayRecordsPanelWidthPixels = 420;
        private const int SlideOpenAnimationDurationMs = 500;
        private const int SlideCloseAnimationDurationMs = 180;

        public Border BuildEmptyDayRecordsPanel()
        {
            var panelBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(22, 27, 34)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(48, 54, 61)),
                BorderThickness = new Thickness(1, 0, 0, 0),
                ClipToBounds = true,
                Tag = "DayRecordsPanel",
                Width = DayRecordsPanelWidthPixels,
                RenderTransform = new TranslateTransform(DayRecordsPanelWidthPixels, 0)
            };

            var layoutGrid = new Grid();
            layoutGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
            layoutGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
            layoutGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var headerSection = BuildPanelHeader();
            var summaryStatsSection = BuildSummaryStatsRow();
            var scrollableRecordsList = BuildScrollableRecordsList();

            Grid.SetRow(headerSection, 0);
            Grid.SetRow(summaryStatsSection, 1);
            Grid.SetRow(scrollableRecordsList, 2);

            layoutGrid.Children.Add(headerSection);
            layoutGrid.Children.Add(summaryStatsSection);
            layoutGrid.Children.Add(scrollableRecordsList);

            panelBorder.Child = layoutGrid;
            return panelBorder;
        }

        public void WireClosePanelButton(Border dayRecordsPanel, Action onCloseClicked)
        {
            var layoutGrid = dayRecordsPanel.Child as Grid;
            if (layoutGrid == null) return;

            foreach (UIElement row in layoutGrid.Children)
            {
                var headerBorder = row as Border;
                if (headerBorder == null) continue;
                var headerGrid = headerBorder.Child as Grid;
                if (headerGrid == null) continue;

                foreach (UIElement headerChild in headerGrid.Children)
                {
                    var closeButton = headerChild as Button;
                    if (closeButton?.Tag?.ToString() == "ClosePanelBtn")
                    {
                        closeButton.Click += (s, e) => onCloseClicked();
                        return;
                    }
                }
            }
        }

        public void ShowLoadingSpinner(Border dayRecordsPanel, DateTime selectedDate, int expectedRecordCount,
            bool showingAllRecords)
        {
            var layoutGrid = dayRecordsPanel.Child as Grid;
            if (layoutGrid == null) return;

            UpdateHeaderDateLabel(layoutGrid, selectedDate, showingAllRecords);
            ClearSummaryStats(layoutGrid);

            var recordsList = FindRecordsList(layoutGrid);
            if (recordsList == null) return;

            recordsList.Children.Clear();
            recordsList.Children.Add(BuildLoadingSpinner(expectedRecordCount));
        }

        public void PopulateWithDayRecords(Border dayRecordsPanel, DateTime selectedDate,
            List<ResponseRecord> recordsToDisplay, bool showingAllRecords)
        {
            var layoutGrid = dayRecordsPanel.Child as Grid;
            if (layoutGrid == null) return;

            UpdateHeaderDateLabel(layoutGrid, selectedDate, showingAllRecords);
            RebuildSummaryStats(layoutGrid, recordsToDisplay);
            RebuildRecordCardList(layoutGrid, recordsToDisplay);
        }

        public void AnimateSlideOpen(Border dayRecordsPanel, ColumnDefinition reservedPanelColumn)
        {
            reservedPanelColumn.Width = new GridLength(DayRecordsPanelWidthPixels);
            AnimateTranslateX(dayRecordsPanel, DayRecordsPanelWidthPixels, 0,
                SlideOpenAnimationDurationMs,
                System.Windows.Media.Animation.EasingMode.EaseOut);
        }

        public void AnimateSlideClose(Border dayRecordsPanel, ColumnDefinition reservedPanelColumn)
        {
            AnimateTranslateX(dayRecordsPanel, 0, DayRecordsPanelWidthPixels,
                SlideCloseAnimationDurationMs,
                System.Windows.Media.Animation.EasingMode.EaseIn,
                () => reservedPanelColumn.Width = new GridLength(0));
        }

        private static void AnimateTranslateX(Border panel, double fromX, double toX, int durationMs,
            System.Windows.Media.Animation.EasingMode easing, Action onCompleted = null)
        {
            var transform = panel.RenderTransform as TranslateTransform;
            if (transform == null) return;

            var animation = new System.Windows.Media.Animation.DoubleAnimation
            {
                From = fromX,
                To = toX,
                Duration = TimeSpan.FromMilliseconds(durationMs),
                EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = easing },
                FillBehavior = System.Windows.Media.Animation.FillBehavior.HoldEnd
            };

            if (onCompleted != null)
                animation.Completed += (s, e) => onCompleted();

            transform.BeginAnimation(TranslateTransform.XProperty, animation);
        }

        private static Border BuildPanelHeader()
        {
            var headerBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(13, 17, 23)),
                Padding = new Thickness(14, 10, 14, 10)
            };

            var headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });

            var dateTitleLabel = new TextBlock
            {
                Text = "Records",
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(230, 237, 243)),
                VerticalAlignment = VerticalAlignment.Center,
                Tag = "PanelDateLabel"
            };

            var closePanelButton = new Button
            {
                Content = "✕",
                Width = 24,
                Height = 24,
                Padding = new Thickness(0),
                Background = new SolidColorBrush(Color.FromRgb(48, 54, 61)),
                Foreground = new SolidColorBrush(Color.FromRgb(139, 148, 158)),
                BorderThickness = new Thickness(0),
                FontSize = 11,
                Cursor = System.Windows.Input.Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center,
                Tag = "ClosePanelBtn"
            };

            Grid.SetColumn(dateTitleLabel, 0);
            Grid.SetColumn(closePanelButton, 1);
            headerGrid.Children.Add(dateTitleLabel);
            headerGrid.Children.Add(closePanelButton);
            headerBorder.Child = headerGrid;
            return headerBorder;
        }

        private static Border BuildSummaryStatsRow()
        {
            var statsRowBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(22, 27, 34)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(48, 54, 61)),
                BorderThickness = new Thickness(0, 1, 0, 1),
                Padding = new Thickness(14, 8, 14, 8),
                Tag = "StatsRow"
            };
            statsRowBorder.Child = new WrapPanel { Orientation = Orientation.Horizontal };
            return statsRowBorder;
        }

        private static ScrollViewer BuildScrollableRecordsList()
        {
            var scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
            scrollViewer.Content = new StackPanel
            {
                Margin = new Thickness(8, 6, 8, 6),
                Tag = "RecordsList"
            };
            return scrollViewer;
        }

        private static StackPanel BuildLoadingSpinner(int expectedRecordCount)
        {
            var spinner = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 40, 0, 0)
            };
            spinner.Children.Add(new TextBlock
            {
                Text = "⏳",
                FontSize = 28,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 10)
            });
            spinner.Children.Add(new TextBlock
            {
                Text = "Loading " + expectedRecordCount + " records...",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(139, 148, 158)),
                HorizontalAlignment = HorizontalAlignment.Center
            });
            return spinner;
        }

        private static void UpdateHeaderDateLabel(Grid layoutGrid, DateTime selectedDate, bool showingAllRecords)
        {
            foreach (UIElement row in layoutGrid.Children)
            {
                var headerBorder = row as Border;
                if (headerBorder == null) continue;
                var headerGrid = headerBorder.Child as Grid;
                if (headerGrid == null) continue;

                foreach (UIElement headerChild in headerGrid.Children)
                {
                    var label = headerChild as TextBlock;
                    if (label?.Tag?.ToString() != "PanelDateLabel") continue;

                    label.Text = showingAllRecords
                        ? "All Records"
                        : new System.Globalization.CultureInfo("en-US").DateTimeFormat.GetDayName(
                              selectedDate.DayOfWeek)
                          + ", " + selectedDate.ToString("dd.MM.yyyy");
                    return;
                }
            }
        }

        private static void ClearSummaryStats(Grid layoutGrid)
        {
            foreach (UIElement row in layoutGrid.Children)
            {
                var statsBorder = row as Border;
                if (statsBorder?.Tag?.ToString() != "StatsRow") continue;
                (statsBorder.Child as WrapPanel)?.Children.Clear();
                return;
            }
        }

        private static void RebuildSummaryStats(Grid layoutGrid, List<ResponseRecord> records)
        {
            foreach (UIElement row in layoutGrid.Children)
            {
                var statsBorder = row as Border;
                if (statsBorder?.Tag?.ToString() != "StatsRow") continue;

                var chipsContainer = statsBorder.Child as WrapPanel;
                if (chipsContainer == null) return;

                chipsContainer.Children.Clear();
                if (records.Count == 0) return;

                var averageResponseTimeMs = records.Average(r => r.ResponseTime);
                var p95Index = (int)Math.Ceiling(records.Count * 0.95) - 1;
                var p95ResponseTimeMs = records.OrderBy(r => r.ResponseTime).ElementAt(p95Index).ResponseTime;

                chipsContainer.Children.Add(BuildStatChip("Records", records.Count.ToString(),
                    Color.FromRgb(56, 139, 253)));
                chipsContainer.Children.Add(BuildStatChip("AVG", averageResponseTimeMs.ToString("F0") + "ms",
                    Color.FromRgb(46, 160, 67)));
                chipsContainer.Children.Add(
                    BuildStatChip("P95", p95ResponseTimeMs + "ms", Color.FromRgb(188, 140, 255)));
                return;
            }
        }

        private static void RebuildRecordCardList(Grid layoutGrid, List<ResponseRecord> records)
        {
            var recordsList = FindRecordsList(layoutGrid);
            if (recordsList == null) return;

            recordsList.Children.Clear();
            foreach (var record in records.OrderBy(r => r.TimestampParsed))
                recordsList.Children.Add(BuildRecordCard(record));
        }

        private static StackPanel FindRecordsList(Grid layoutGrid)
        {
            foreach (UIElement row in layoutGrid.Children)
            {
                var scrollViewer = row as ScrollViewer;
                if (scrollViewer?.Content is StackPanel sp && sp.Tag?.ToString() == "RecordsList")
                    return sp;
            }

            return null;
        }

        private static Border BuildStatChip(string labelText, string valueText, Color accentColor)
        {
            var chipBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(40, accentColor.R, accentColor.G, accentColor.B)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(80, accentColor.R, accentColor.G, accentColor.B)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8, 4, 8, 4),
                Margin = new Thickness(0, 0, 6, 4)
            };
            var contentRow = new StackPanel { Orientation = Orientation.Horizontal };
            contentRow.Children.Add(new TextBlock
            {
                Text = labelText + " ",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(139, 148, 158))
            });
            contentRow.Children.Add(new TextBlock
            {
                Text = valueText,
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromArgb(220, accentColor.R, accentColor.G, accentColor.B))
            });
            chipBorder.Child = contentRow;
            return chipBorder;
        }

        private static Border BuildRecordCard(ResponseRecord record)
        {
            var responseTimeIsSlow = record.ResponseTime > 100;
            var accentColor = responseTimeIsSlow ? Color.FromRgb(248, 81, 73) : Color.FromRgb(56, 139, 253);

            var cardBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(30, 37, 46)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(60, accentColor.R, accentColor.G, accentColor.B)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12, 10, 12, 10),
                Margin = new Thickness(0, 0, 0, 6)
            };

            var twoColumnLayout = new Grid();
            twoColumnLayout.ColumnDefinitions.Add(new ColumnDefinition
                { Width = new GridLength(1, GridUnitType.Star) });
            twoColumnLayout.ColumnDefinitions.Add(new ColumnDefinition
                { Width = new GridLength(1, GridUnitType.Auto) });

            var leftColumn = new StackPanel();

            var timestampRow = new StackPanel
                { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
            timestampRow.Children.Add(new TextBlock
            {
                Text = record.TimestampParsed.ToString("HH:mm:ss.fff"),
                FontSize = 11,
                FontFamily = new FontFamily("Consolas"),
                Foreground = new SolidColorBrush(Color.FromRgb(139, 148, 158))
            });
            leftColumn.Children.Add(timestampRow);

            if (!string.IsNullOrEmpty(record.Uid)) leftColumn.Children.Add(BuildRecordFieldRow("UID", record.Uid));
            if (!string.IsNullOrEmpty(record.Material))
                leftColumn.Children.Add(BuildRecordFieldRow("Material", record.Material));
            if (!string.IsNullOrEmpty(record.CarrierId))
                leftColumn.Children.Add(BuildRecordFieldRow("Carrier", record.CarrierId));
            if (!string.IsNullOrEmpty(record.Result))
                leftColumn.Children.Add(BuildRecordFieldRow("Result", record.Result));

            var responseTimeBadge = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(40, accentColor.R, accentColor.G, accentColor.B)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(100, accentColor.R, accentColor.G, accentColor.B)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(8, 4, 8, 4),
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(8, 0, 0, 0)
            };
            var badgeContent = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            badgeContent.Children.Add(new TextBlock
            {
                Text = record.ResponseTime + "ms",
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromArgb(220, accentColor.R, accentColor.G, accentColor.B)),
                HorizontalAlignment = HorizontalAlignment.Center
            });
            responseTimeBadge.Child = badgeContent;

            Grid.SetColumn(leftColumn, 0);
            Grid.SetColumn(responseTimeBadge, 1);
            twoColumnLayout.Children.Add(leftColumn);
            twoColumnLayout.Children.Add(responseTimeBadge);
            cardBorder.Child = twoColumnLayout;
            return cardBorder;
        }

        private static StackPanel BuildRecordFieldRow(string fieldLabel, string fieldValue)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 0) };
            row.Children.Add(new TextBlock
            {
                Text = fieldLabel + ": ",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(110, 118, 129)),
                MinWidth = 50
            });
            row.Children.Add(new TextBlock
            {
                Text = fieldValue,
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(201, 209, 217)),
                TextWrapping = TextWrapping.NoWrap
            });
            return row;
        }
    }
}