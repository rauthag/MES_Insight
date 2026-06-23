using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MESInsight.Assembly;
using MESInsight.Core;

namespace MESInsight.UI
{
    public class SubsetHistoryWindow : Window
    {
        public SubsetHistoryWindow(string uid, AssemblyNode node, List<ResponseRecord> allRecords)
        {
            Title  = "UID History — " + uid;
            Width  = 680;
            Height = 620;
            Background = new SolidColorBrush(Color.FromRgb(13, 17, 23));
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(40) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            
            var header = new Border
            {
                Background  = new SolidColorBrush(Color.FromRgb(10, 14, 20)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(36, 42, 52)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding     = new Thickness(14, 0, 14, 0)
            };
            var headerText = new TextBlock
            {
                Text              = uid,
                FontSize          = 12,
                FontWeight        = FontWeights.SemiBold,
                Foreground        = new SolidColorBrush(Color.FromRgb(56, 182, 255)),
                VerticalAlignment = VerticalAlignment.Center,
                FontFamily        = new FontFamily("Consolas")
            };
            header.Child = headerText;
            Grid.SetRow(header, 0);
            grid.Children.Add(header);
            
            var tabControl = new TabControl
            {
                Background = new SolidColorBrush(Color.FromRgb(13, 17, 23)),
                BorderThickness = new Thickness(0)
            };
            
            var assyTab = new TabItem { Header = "🔗 Assembly Tree" };
            assyTab.Content = BuildAssemblyTab(node);
            tabControl.Items.Add(assyTab);
            
            var histTab = new TabItem { Header = "📋 Full History" };
            histTab.Content = BuildHistoryTab(uid, allRecords);
            tabControl.Items.Add(histTab);

            Grid.SetRow(tabControl, 1);
            grid.Children.Add(tabControl);

            Content = grid;
        }

        private UIElement BuildAssemblyTab(AssemblyNode node)
        {
            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility   = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Background = new SolidColorBrush(Color.FromRgb(13, 17, 23))
            };

            var stack = new StackPanel { Margin = new Thickness(12) };
            
            stack.Children.Add(BuildNodeHeader(node));
            
            var unique = node.Components
                .GroupBy(c => c.UidAssy)
                .Select(g => g.OrderByDescending(c => c.Timestamp).First())
                .OrderBy(c => c.Timestamp)
                .ToList();

            foreach (var comp in unique)
                stack.Children.Add(BuildCompRow(comp));

            if (unique.Count == 0)
                stack.Children.Add(new TextBlock
                {
                    Text       = "No assembly components found.",
                    FontSize   = 11,
                    Foreground = new SolidColorBrush(Color.FromRgb(100, 110, 120)),
                    Margin     = new Thickness(0, 12, 0, 0)
                });

            scroll.Content = stack;
            return scroll;
        }

        private UIElement BuildNodeHeader(AssemblyNode node)
        {
            var border = new Border
            {
                Background      = new SolidColorBrush(Color.FromRgb(22, 27, 40)),
                BorderBrush     = new SolidColorBrush(Color.FromRgb(56, 80, 140)),
                BorderThickness = new Thickness(1),
                CornerRadius    = new CornerRadius(6),
                Padding         = new Thickness(12, 10, 12, 10),
                Margin          = new Thickness(0, 0, 0, 10)
            };

            var stack = new StackPanel();
            stack.Children.Add(new TextBlock
            {
                Text       = node.Uid,
                FontSize   = 13,
                FontWeight = FontWeights.Bold,
                FontFamily = new FontFamily("Consolas"),
                Foreground = new SolidColorBrush(Color.FromRgb(56, 182, 255))
            });
            if (!string.IsNullOrEmpty(node.UidType))
                stack.Children.Add(new TextBlock
                {
                    Text       = node.UidType,
                    FontSize   = 10,
                    Foreground = new SolidColorBrush(Color.FromRgb(100, 120, 160)),
                    Margin     = new Thickness(0, 2, 0, 0)
                });

            int y = node.Components.Count(c => c.ProcDir == "Y");
            int n = node.Components.Count(c => c.ProcDir == "N");
            var statsRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
            statsRow.Children.Add(new TextBlock { Text = node.Components.Count + " validations  ", FontSize = 10, Foreground = new SolidColorBrush(Color.FromRgb(100, 110, 130)) });
            if (y > 0) statsRow.Children.Add(new TextBlock { Text = "Y:" + y + "  ", FontSize = 10, Foreground = new SolidColorBrush(Color.FromRgb(50, 220, 80)) });
            if (n > 0) statsRow.Children.Add(new TextBlock { Text = "N:" + n, FontSize = 10, Foreground = new SolidColorBrush(Color.FromRgb(248, 81, 73)) });
            stack.Children.Add(statsRow);

            border.Child = stack;
            return border;
        }

        private UIElement BuildCompRow(AssyRelation rel)
        {
            Color col = GetColor(rel.ProcDir);
            var border = new Border
            {
                Background      = new SolidColorBrush(Color.FromRgb(18, 22, 30)),
                BorderBrush     = new SolidColorBrush(Color.FromArgb(60, col.R, col.G, col.B)),
                BorderThickness = new Thickness(1),
                CornerRadius    = new CornerRadius(4),
                Padding         = new Thickness(10, 7, 10, 7),
                Margin          = new Thickness(16, 0, 0, 4)
            };

            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var badge = new TextBlock
            {
                Text       = rel.ProcDir,
                FontSize   = 10,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(col),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(badge, 0);

            var mid = new StackPanel();
            mid.Children.Add(new TextBlock { Text = rel.UidAssy, FontSize = 11, FontFamily = new FontFamily("Consolas"), Foreground = new SolidColorBrush(Color.FromRgb(200, 210, 220)) });
            if (!string.IsNullOrEmpty(rel.UidAssyType))
                mid.Children.Add(new TextBlock { Text = rel.UidAssyType, FontSize = 9, Foreground = new SolidColorBrush(Color.FromRgb(90, 100, 120)) });
            Grid.SetColumn(mid, 1);

            var ts = new TextBlock
            {
                Text      = rel.Timestamp.ToString("dd.MM HH:mm:ss"),
                FontSize  = 9,
                Foreground = new SolidColorBrush(Color.FromRgb(70, 80, 100)),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(ts, 2);

            row.Children.Add(badge);
            row.Children.Add(mid);
            row.Children.Add(ts);
            border.Child = row;
            return border;
        }

        private UIElement BuildHistoryTab(string uid, List<ResponseRecord> allRecords)
        {
            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility   = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Background = new SolidColorBrush(Color.FromRgb(13, 17, 23))
            };

            var stack = new StackPanel { Margin = new Thickness(12) };

            var related = allRecords
                .Where(r => r.Uid == uid || r.UidIn == uid || r.UidOut == uid ||
                            r.UidAssy == uid ||
                            (!string.IsNullOrEmpty(r.AssyUids) && r.AssyUids.Split(',').Contains(uid)))
                .OrderBy(r => r.TimestampParsed)
                .ToList();

            if (related.Count == 0)
            {
                stack.Children.Add(new TextBlock
                {
                    Text       = "No records found for this UID.",
                    FontSize   = 11,
                    Foreground = new SolidColorBrush(Color.FromRgb(100, 110, 120))
                });
            }
            else
            {
                stack.Children.Add(new TextBlock
                {
                    Text       = related.Count + " records found",
                    FontSize   = 10,
                    Foreground = new SolidColorBrush(Color.FromRgb(100, 110, 130)),
                    Margin     = new Thickness(0, 0, 0, 8)
                });

                foreach (var r in related)
                    stack.Children.Add(BuildHistoryRow(r));
            }

            scroll.Content = stack;
            return scroll;
        }

        private UIElement BuildHistoryRow(ResponseRecord r)
        {
            var border = new Border
            {
                Background      = new SolidColorBrush(Color.FromRgb(18, 22, 30)),
                BorderBrush     = new SolidColorBrush(Color.FromRgb(36, 42, 52)),
                BorderThickness = new Thickness(1),
                CornerRadius    = new CornerRadius(4),
                Padding         = new Thickness(10, 7, 10, 7),
                Margin          = new Thickness(0, 0, 0, 4)
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var left = new StackPanel();
            left.Children.Add(new TextBlock
            {
                Text       = r.TimestampParsed.ToString("dd.MM.yyyy HH:mm:ss") + "  —  " + r.Type.ToString().Replace("_", " "),
                FontSize   = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(180, 190, 200))
            });
            if (!string.IsNullOrEmpty(r.Uid ?? r.UidIn))
                left.Children.Add(new TextBlock { Text = "UID: " + (r.Uid ?? r.UidIn), FontSize = 9, Foreground = new SolidColorBrush(Color.FromRgb(100, 120, 160)) });
            if (!string.IsNullOrEmpty(r.UidAssy))
                left.Children.Add(new TextBlock { Text = "ASSY: " + r.UidAssy, FontSize = 9, Foreground = new SolidColorBrush(Color.FromRgb(155, 89, 182)) });
            if (!string.IsNullOrEmpty(r.Result))
                left.Children.Add(new TextBlock { Text = "Result: " + r.Result, FontSize = 9, Foreground = new SolidColorBrush(Color.FromRgb(120, 140, 120)) });

            Grid.SetColumn(left, 0);

            var rt = new TextBlock
            {
                Text      = r.ResponseTime + " ms",
                FontSize  = 11,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(r.ResponseTime > 100
                    ? Color.FromRgb(248, 81, 73)
                    : Color.FromRgb(56, 139, 253)),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(rt, 1);

            grid.Children.Add(left);
            grid.Children.Add(rt);
            border.Child = grid;
            return border;
        }

        private static Color GetColor(string procDir)
        {
            switch (procDir)
            {
                case "Y": return Color.FromRgb(50, 220, 80);
                case "N": return Color.FromRgb(248, 81, 73);
                case "P":
                case "R": return Color.FromRgb(240, 196, 48);
                default:  return Color.FromRgb(140, 160, 180);
            }
        }
    }
}