using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MESInsight.Core;

namespace MESInsight.UI
{
    public class DayRecordsPanelBuilder
    {
        private const int DayRecordsPanelWidthPixels    = 420;
        private const int SlideOpenAnimationDurationMs  = 500;
        private const int SlideCloseAnimationDurationMs = 180;
        private const int PageSize                      = 20;

        public Border BuildEmptyDayRecordsPanel()
        {
            var panelBorder = new Border
            {
                Background      = new SolidColorBrush(Color.FromRgb(22, 27, 34)),
                BorderBrush     = new SolidColorBrush(Color.FromRgb(48, 54, 61)),
                BorderThickness = new Thickness(1, 0, 0, 0),
                ClipToBounds    = true,
                Tag             = "DayRecordsPanel",
                Width           = DayRecordsPanelWidthPixels,
                RenderTransform = new TranslateTransform(DayRecordsPanelWidthPixels, 0)
            };

            var layoutGrid = new Grid();
            layoutGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            layoutGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            layoutGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            layoutGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var headerSection         = BuildPanelHeader();
            var summaryStatsSection   = BuildSummaryStatsRow();
            var searchSection         = BuildSearchRow();
            var scrollableRecordsList = BuildScrollableRecordsList();

            Grid.SetRow(headerSection,         0);
            Grid.SetRow(summaryStatsSection,   1);
            Grid.SetRow(searchSection,         2);
            Grid.SetRow(scrollableRecordsList, 3);

            layoutGrid.Children.Add(headerSection);
            layoutGrid.Children.Add(summaryStatsSection);
            layoutGrid.Children.Add(searchSection);
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

        public void ShowLoadingSpinner(Border dayRecordsPanel, DateTime selectedDate, int expectedRecordCount, bool showingAllRecords)
        {
            var layoutGrid = dayRecordsPanel.Child as Grid;
            if (layoutGrid == null) return;

            UpdateHeaderDateLabel(layoutGrid, selectedDate, showingAllRecords);
            ClearSummaryStats(layoutGrid);
            ClearSearchBox(layoutGrid);

            var recordsList = FindRecordsList(layoutGrid);
            if (recordsList == null) return;

            recordsList.Children.Clear();
            recordsList.Children.Add(BuildLoadingSpinner(expectedRecordCount));
        }

        public void PopulateWithDayRecords(Border dayRecordsPanel, DateTime selectedDate,
            List<ResponseRecord> recordsToDisplay, bool showingAllRecords, bool showType = false)
        {
            var layoutGrid = dayRecordsPanel.Child as Grid;
            if (layoutGrid == null) return;

            UpdateHeaderDateLabel(layoutGrid, selectedDate, showingAllRecords);
            RebuildSummaryStats(layoutGrid, recordsToDisplay);
            WireSearchBox(layoutGrid, recordsToDisplay, showType);
            RebuildRecordCardList(layoutGrid, recordsToDisplay, "", showType);
        }

        public void AnimateSlideOpen(Border dayRecordsPanel, ColumnDefinition reservedPanelColumn)
        {
            reservedPanelColumn.Width = new GridLength(DayRecordsPanelWidthPixels);
            AnimateTranslateX(dayRecordsPanel, DayRecordsPanelWidthPixels, 0,
                SlideOpenAnimationDurationMs, System.Windows.Media.Animation.EasingMode.EaseOut);
        }

        public void AnimateSlideClose(Border dayRecordsPanel, ColumnDefinition reservedPanelColumn)
        {
            AnimateTranslateX(dayRecordsPanel, 0, DayRecordsPanelWidthPixels,
                SlideCloseAnimationDurationMs, System.Windows.Media.Animation.EasingMode.EaseIn,
                () => reservedPanelColumn.Width = new GridLength(0));
        }

        private static void AnimateTranslateX(Border panel, double fromX, double toX, int durationMs,
            System.Windows.Media.Animation.EasingMode easing, Action onCompleted = null)
        {
            var transform = panel.RenderTransform as TranslateTransform;
            if (transform == null) return;

            var animation = new System.Windows.Media.Animation.DoubleAnimation
            {
                From           = fromX,
                To             = toX,
                Duration       = TimeSpan.FromMilliseconds(durationMs),
                EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = easing },
                FillBehavior   = System.Windows.Media.Animation.FillBehavior.HoldEnd
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
                Padding    = new Thickness(14, 10, 14, 10)
            };

            var headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var dateTitleLabel = new TextBlock
            {
                Text = "Records", FontSize = 14, FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(230, 237, 243)),
                VerticalAlignment = VerticalAlignment.Center, Tag = "PanelDateLabel"
            };

            var closePanelButton = new Button
            {
                Content = "✕", Width = 24, Height = 24, Padding = new Thickness(0),
                Background = new SolidColorBrush(Color.FromRgb(48, 54, 61)),
                Foreground = new SolidColorBrush(Color.FromRgb(139, 148, 158)),
                BorderThickness = new Thickness(0), FontSize = 11,
                Cursor = System.Windows.Input.Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center, Tag = "ClosePanelBtn"
            };

            Grid.SetColumn(dateTitleLabel,   0);
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
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(14, 8, 14, 8), Tag = "StatsRow"
            };
            statsRowBorder.Child = new WrapPanel { Orientation = Orientation.Horizontal };
            return statsRowBorder;
        }

        private static Border BuildSearchRow()
        {
            var searchBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(13, 17, 23)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(48, 54, 61)),
                BorderThickness = new Thickness(0, 1, 0, 1),
                Padding = new Thickness(10, 7, 10, 7), Tag = "SearchRow"
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var icon = new TextBlock { Text = "🔍", FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) };

            var searchBox = new TextBox
            {
                Background = new SolidColorBrush(Color.FromRgb(22, 27, 34)),
                Foreground = new SolidColorBrush(Color.FromRgb(201, 209, 217)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(48, 54, 61)),
                BorderThickness = new Thickness(1), FontSize = 11,
                Padding = new Thickness(6, 4, 6, 4), Tag = "SearchBox",
                ToolTip = "Search by UID, Material, Result, Carrier..."
            };

            var placeholder = new TextBlock
            {
                Text = "Search UID, material, result...", FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(80, 90, 100)),
                IsHitTestVisible = false, VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0), Tag = "SearchPlaceholder"
            };

            Grid.SetColumn(icon,        0);
            Grid.SetColumn(searchBox,   1);
            Grid.SetColumn(placeholder, 1);

            grid.Children.Add(icon);
            grid.Children.Add(searchBox);
            grid.Children.Add(placeholder);

            searchBox.TextChanged += (s, e) =>
                placeholder.Visibility = string.IsNullOrEmpty(searchBox.Text)
                    ? Visibility.Visible : Visibility.Collapsed;

            searchBorder.Child = grid;
            return searchBorder;
        }

        private static ScrollViewer BuildScrollableRecordsList()
        {
            var scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility   = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
            scrollViewer.Content = new StackPanel
            {
                Margin = new Thickness(8, 6, 8, 6), Tag = "RecordsList"
            };
            return scrollViewer;
        }

        private static StackPanel BuildLoadingSpinner(int expectedRecordCount)
        {
            var spinner = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment   = VerticalAlignment.Center,
                Margin              = new Thickness(0, 40, 0, 0)
            };
            spinner.Children.Add(new TextBlock { Text = "⏳", FontSize = 28, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 10) });
            spinner.Children.Add(new TextBlock { Text = "Loading " + expectedRecordCount + " records...", FontSize = 12, Foreground = new SolidColorBrush(Color.FromRgb(139, 148, 158)), HorizontalAlignment = HorizontalAlignment.Center });
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
                        : new System.Globalization.CultureInfo("en-US").DateTimeFormat.GetDayName(selectedDate.DayOfWeek)
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

        private static void ClearSearchBox(Grid layoutGrid)
        {
            foreach (UIElement row in layoutGrid.Children)
            {
                var searchBorder = row as Border;
                if (searchBorder?.Tag?.ToString() != "SearchRow") continue;
                var grid = searchBorder.Child as Grid;
                if (grid == null) continue;
                foreach (UIElement child in grid.Children)
                    if (child is TextBox tb) tb.Text = "";
                return;
            }
        }

        private static void WireSearchBox(Grid layoutGrid, List<ResponseRecord> allRecords, bool showType = false)
        {
            foreach (UIElement row in layoutGrid.Children)
            {
                var searchBorder = row as Border;
                if (searchBorder?.Tag?.ToString() != "SearchRow") continue;
                var grid = searchBorder.Child as Grid;
                if (grid == null) continue;

                foreach (UIElement child in grid.Children)
                {
                    if (!(child is TextBox tb)) continue;
                    tb.TextChanged += (s, e) =>
                        RebuildRecordCardList(layoutGrid, allRecords, tb.Text.Trim(), showType);
                    return;
                }
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

                var avg      = records.Average(r => r.ResponseTime);
                var p95Index = (int)Math.Ceiling(records.Count * 0.95) - 1;
                var p95      = records.OrderBy(r => r.ResponseTime).ElementAt(p95Index).ResponseTime;

                chipsContainer.Children.Add(BuildStatChip("Records", records.Count.ToString(),  Color.FromRgb(56,  139, 253)));
                chipsContainer.Children.Add(BuildStatChip("AVG",     avg.ToString("F0") + "ms", Color.FromRgb(46,  160,  67)));
                chipsContainer.Children.Add(BuildStatChip("P95",     p95 + "ms",                Color.FromRgb(188, 140, 255)));
                return;
            }
        }

        private static void RebuildRecordCardList(Grid layoutGrid, List<ResponseRecord> allRecords, string searchText, bool showType = false)
        {
            var recordsList = FindRecordsList(layoutGrid);
            if (recordsList == null) return;

            var filtered = string.IsNullOrEmpty(searchText)
                ? allRecords.OrderBy(r => r.TimestampParsed).ToList()
                : allRecords.Where(r =>
                    (r.Uid       != null && r.Uid.IndexOf(searchText,       StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (r.UidIn     != null && r.UidIn.IndexOf(searchText,     StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (r.Material  != null && r.Material.IndexOf(searchText,  StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (r.Result    != null && r.Result.IndexOf(searchText,    StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (r.CarrierId != null && r.CarrierId.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (showType    && r.Type.ToString().IndexOf(searchText,   StringComparison.OrdinalIgnoreCase) >= 0))
                    .OrderBy(r => r.TimestampParsed).ToList();

            recordsList.Children.Clear();
            RenderRecordPage(recordsList, filtered, 0, showType);
        }

        private static void RenderRecordPage(StackPanel recordsList, List<ResponseRecord> records, int offset, bool showType = false)
        {
            int total = records.Count;
            int end   = Math.Min(offset + PageSize, total);

            if (total == 0)
            {
                recordsList.Children.Add(new TextBlock
                {
                    Text = "No records match the search.", FontSize = 11,
                    Foreground = new SolidColorBrush(Color.FromRgb(139, 148, 158)),
                    Margin = new Thickness(8, 16, 8, 0)
                });
                return;
            }

            if (offset == 0 && total > PageSize)
                recordsList.Children.Add(new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(22, 27, 34)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(48, 54, 61)),
                    BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(12, 8, 12, 8), Margin = new Thickness(0, 0, 0, 8),
                    Child = new TextBlock
                    {
                        Text = "Showing " + end + " of " + total + " records",
                        FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(139, 148, 158)),
                        TextWrapping = TextWrapping.Wrap
                    }
                });

            for (int i = offset; i < end; i++)
                recordsList.Children.Add(BuildRecordCard(records[i], showType));

            if (end < total)
            {
                var loadMoreBtn = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(22, 40, 28)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(46, 160, 67)),
                    BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(12, 10, 12, 10), Margin = new Thickness(0, 4, 0, 8),
                    Cursor = System.Windows.Input.Cursors.Hand,
                    Child = new TextBlock
                    {
                        Text = "Load next " + Math.Min(PageSize, total - end) + "  (" + (total - end) + " remaining)",
                        FontSize = 11, FontWeight = FontWeights.SemiBold,
                        Foreground = new SolidColorBrush(Color.FromRgb(63, 185, 80)),
                        HorizontalAlignment = HorizontalAlignment.Center
                    }
                };

                var capturedOffset = end;
                loadMoreBtn.MouseLeftButtonUp += (s, e) =>
                {
                    recordsList.Children.Remove(loadMoreBtn);
                    RenderRecordPage(recordsList, records, capturedOffset, showType);
                };
                recordsList.Children.Add(loadMoreBtn);
            }
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
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8, 4, 8, 4), Margin = new Thickness(0, 0, 6, 4)
            };
            var contentRow = new StackPanel { Orientation = Orientation.Horizontal };
            contentRow.Children.Add(new TextBlock { Text = labelText + " ", FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(139, 148, 158)) });
            contentRow.Children.Add(new TextBlock { Text = valueText, FontSize = 11, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromArgb(220, accentColor.R, accentColor.G, accentColor.B)) });
            chipBorder.Child = contentRow;
            return chipBorder;
        }

        private static Border BuildRecordCard(ResponseRecord record, bool showType = false)
        {
            var responseTimeIsSlow = record.ResponseTime > 100;
            var accentColor = responseTimeIsSlow ? Color.FromRgb(248, 81, 73) : Color.FromRgb(56, 139, 253);

            var cardBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(30, 37, 46)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(60, accentColor.R, accentColor.G, accentColor.B)),
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12, 10, 12, 10), Margin = new Thickness(0, 0, 0, 6)
            };

            var twoColumnLayout = new Grid();
            twoColumnLayout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            twoColumnLayout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var leftColumn   = new StackPanel();
            var timestampRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
            timestampRow.Children.Add(new TextBlock
            {
                Text = record.TimestampParsed.ToString("HH:mm:ss.fff"), FontSize = 11,
                FontFamily = new FontFamily("Consolas"),
                Foreground = new SolidColorBrush(Color.FromRgb(139, 148, 158))
            });

            if (showType)
            {
                var typeColor = MessageColors.Get(record.Type);
                timestampRow.Children.Add(new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(30, typeColor.R, typeColor.G, typeColor.B)),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(120, typeColor.R, typeColor.G, typeColor.B)),
                    BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(3),
                    Padding = new Thickness(5, 1, 5, 1), Margin = new Thickness(8, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    Child = new TextBlock
                    {
                        Text = record.Type.ToString().Replace("_", " "), FontSize = 9,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = new SolidColorBrush(Color.FromArgb(200, typeColor.R, typeColor.G, typeColor.B))
                    }
                });
            }

            leftColumn.Children.Add(timestampRow);

            string primaryUid = record.Uid ?? record.UidIn;
            if (!string.IsNullOrEmpty(primaryUid))
                leftColumn.Children.Add(BuildRecordFieldRowWithHistory("UID", primaryUid));
            else if (!string.IsNullOrEmpty(record.UidIn))
                leftColumn.Children.Add(BuildRecordFieldRowWithHistory("UID In", record.UidIn));

            if (!string.IsNullOrEmpty(record.Material))  leftColumn.Children.Add(BuildRecordFieldRow("Material", record.Material));
            if (!string.IsNullOrEmpty(record.CarrierId)) leftColumn.Children.Add(BuildRecordFieldRow("Carrier",  record.CarrierId));
            if (!string.IsNullOrEmpty(record.Result))    leftColumn.Children.Add(BuildRecordFieldRow("Result",   record.Result));

            var responseTimeBadge = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(40, accentColor.R, accentColor.G, accentColor.B)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(100, accentColor.R, accentColor.G, accentColor.B)),
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6),
                Padding = new Thickness(8, 4, 8, 4), VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(8, 0, 0, 0),
                Child = new TextBlock
                {
                    Text = record.ResponseTime + "ms", FontSize = 13, FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Color.FromArgb(220, accentColor.R, accentColor.G, accentColor.B)),
                    HorizontalAlignment = HorizontalAlignment.Center
                }
            };

            Grid.SetColumn(leftColumn,        0);
            Grid.SetColumn(responseTimeBadge, 1);
            twoColumnLayout.Children.Add(leftColumn);
            twoColumnLayout.Children.Add(responseTimeBadge);

            var outerStack = new StackPanel();
            outerStack.Children.Add(twoColumnLayout);

            string uidForHistory = record.Uid ?? record.UidIn;
            if (!string.IsNullOrEmpty(uidForHistory))
            {
                var histBtn = new Border
                {
                    Background      = new SolidColorBrush(Color.FromArgb(20, 56, 182, 255)),
                    BorderBrush     = new SolidColorBrush(Color.FromArgb(60, 56, 182, 255)),
                    BorderThickness = new Thickness(0, 1, 0, 0),
                    Padding         = new Thickness(8, 5, 8, 5),
                    Margin          = new Thickness(-12, 8, -12, -10),
                    Cursor          = System.Windows.Input.Cursors.Hand,
                    Child = new TextBlock
                    {
                        Text      = "📜  Show History",
                        FontSize  = 10,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = new SolidColorBrush(Color.FromRgb(100, 140, 200)),
                        HorizontalAlignment = HorizontalAlignment.Center
                    }
                };
                var capturedUid = uidForHistory;
                histBtn.MouseLeftButtonUp += (s, e) =>
                {
                    MESInsight.MainWindow.OpenSubsetHistory?.Invoke(capturedUid);
                    e.Handled = true;
                };
                histBtn.MouseEnter += (s, e) =>
                {
                    histBtn.Background = new SolidColorBrush(Color.FromArgb(50, 56, 182, 255));
                    ((TextBlock)histBtn.Child).Foreground = new SolidColorBrush(Color.FromRgb(56, 182, 255));
                };
                histBtn.MouseLeave += (s, e) =>
                {
                    histBtn.Background = new SolidColorBrush(Color.FromArgb(20, 56, 182, 255));
                    ((TextBlock)histBtn.Child).Foreground = new SolidColorBrush(Color.FromRgb(100, 140, 200));
                };
                outerStack.Children.Add(histBtn);
            }

            cardBorder.Child = outerStack;
            return cardBorder;
        }

        private static StackPanel BuildRecordFieldRowWithHistory(string fieldLabel, string fieldValue)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 0) };
            row.Children.Add(new TextBlock
            {
                Text = fieldLabel + ": ", FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(110, 118, 129)), MinWidth = 55
            });
            row.Children.Add(new TextBlock
            {
                Text = fieldValue, FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(201, 209, 217)),
                TextWrapping = TextWrapping.NoWrap
            });

            var histBtn = new Border
            {
                Background      = new SolidColorBrush(Color.FromArgb(0, 30, 50, 80)),
                BorderBrush     = new SolidColorBrush(Color.FromArgb(0, 56, 182, 255)),
                BorderThickness = new Thickness(1),
                CornerRadius    = new CornerRadius(3),
                Padding         = new Thickness(5, 1, 5, 1),
                Margin          = new Thickness(8, 0, 0, 0),
                Cursor          = System.Windows.Input.Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text      = "📜 Show History",
                    FontSize  = 9,
                    Foreground = new SolidColorBrush(Color.FromArgb(0, 56, 182, 255))
                }
            };

            histBtn.MouseLeftButtonUp += (s, e) =>
            {
                MESInsight.MainWindow.OpenSubsetHistory?.Invoke(fieldValue);
                e.Handled = true;
            };
            histBtn.MouseEnter += (s, e) =>
            {
                histBtn.Background  = new SolidColorBrush(Color.FromArgb(30, 56, 182, 255));
                histBtn.BorderBrush = new SolidColorBrush(Color.FromArgb(100, 56, 182, 255));
                ((TextBlock)histBtn.Child).Foreground = new SolidColorBrush(Color.FromRgb(56, 182, 255));
            };
            histBtn.MouseLeave += (s, e) =>
            {
                histBtn.Background  = new SolidColorBrush(Color.FromArgb(0, 30, 50, 80));
                histBtn.BorderBrush = new SolidColorBrush(Color.FromArgb(0, 56, 182, 255));
                ((TextBlock)histBtn.Child).Foreground = new SolidColorBrush(Color.FromArgb(0, 56, 182, 255));
            };

            row.MouseEnter += (s, e) =>
            {
                histBtn.Background  = new SolidColorBrush(Color.FromArgb(20, 56, 182, 255));
                histBtn.BorderBrush = new SolidColorBrush(Color.FromArgb(80, 56, 182, 255));
                ((TextBlock)histBtn.Child).Foreground = new SolidColorBrush(Color.FromArgb(180, 56, 182, 255));
            };
            row.MouseLeave += (s, e) =>
            {
                histBtn.Background  = new SolidColorBrush(Color.FromArgb(0, 30, 50, 80));
                histBtn.BorderBrush = new SolidColorBrush(Color.FromArgb(0, 56, 182, 255));
                ((TextBlock)histBtn.Child).Foreground = new SolidColorBrush(Color.FromArgb(0, 56, 182, 255));
            };

            return row;
        }

        private static StackPanel BuildRecordFieldRow(string fieldLabel, string fieldValue)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 0) };
            row.Children.Add(new TextBlock
            {
                Text = fieldLabel + ": ", FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(110, 118, 129)), MinWidth = 55
            });
            row.Children.Add(new TextBlock
            {
                Text = fieldValue, FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(201, 209, 217)),
                TextWrapping = TextWrapping.NoWrap
            });
            return row;
        }
    }
}