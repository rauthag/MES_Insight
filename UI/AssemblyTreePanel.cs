using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MESInsight.Assembly;
using MESInsight.Core;
using MESInsight;

namespace MESInsight.UI
{
    public static class AssemblyTreePanel
    {
        private static readonly Color CBack   = Color.FromRgb(13,  17,  23);
        private static readonly Color CCard   = Color.FromRgb(22,  27,  34);
        private static readonly Color CBorder = Color.FromRgb(36,  42,  52);
        private static readonly Color CText   = Color.FromRgb(201, 209, 217);
        private static readonly Color CDim    = Color.FromRgb(100, 110, 130);
        private static readonly Color CBlue   = Color.FromRgb(56,  182, 255);
        private static readonly Color CPurple = Color.FromRgb(155, 89,  182);
        private static readonly Color CGreen  = Color.FromRgb(50,  220, 80);
        private static readonly Color CRed    = Color.FromRgb(248, 81,  73);
        private static readonly Color CYellow = Color.FromRgb(240, 196, 48);
        private static readonly Color CGray   = Color.FromRgb(140, 160, 180);

        private const int MaxDepth = 8;
        private const int IndentPx = 20;
        private const int PageSize = 10;

        public static UIElement Build(AssemblyIndex index, List<ResponseRecord> allRecords, Action<string> onUidClick = null)
        {
            var root = new Grid { Background = new SolidColorBrush(CBack) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            root.Children.Add(BuildHeader(index));
            Grid.SetRow(root.Children[0], 0);

            var search = BuildSearchBox();
            Grid.SetRow(search.border, 1);
            root.Children.Add(search.border);

            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility   = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Margin = new Thickness(8, 4, 8, 8)
            };

            var treePanel = new StackPanel();
            scroll.Content = treePanel;
            Grid.SetRow(scroll, 2);
            root.Children.Add(scroll);

            var rootUids = index.GetRootUids();
            PopulateTree(treePanel, rootUids, index, allRecords, "", 0, onUidClick);

            string lastQuery = "";
            search.box.TextChanged += (s, e) =>
            {
                string q = search.box.Text.Trim();
                if (q == lastQuery) return;
                lastQuery = q;
                PopulateTree(treePanel, rootUids, index, allRecords, q, 0, onUidClick);
                scroll.ScrollToTop();
            };

            return root;
        }

        private static UIElement BuildHeader(AssemblyIndex index)
        {
            var border = new Border
            {
                Background      = new SolidColorBrush(Color.FromRgb(10, 14, 20)),
                BorderBrush     = new SolidColorBrush(Color.FromRgb(22, 60, 36)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding         = new Thickness(14, 10, 14, 10)
            };
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(new TextBlock
            {
                Text      = "ASSEMBLY TREE",
                FontSize  = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(CPurple),
                VerticalAlignment = VerticalAlignment.Center
            });
            row.Children.Add(new TextBlock
            {
                Text      = "  —  " + index.AllUids.Count + " units  ·  " + index.TotalRelations + " validations",
                FontSize  = 10,
                Foreground = new SolidColorBrush(CDim),
                VerticalAlignment = VerticalAlignment.Center
            });
            border.Child = row;
            return border;
        }

        private static (Border border, TextBox box) BuildSearchBox()
        {
            var box = new TextBox
            {
                Background      = new SolidColorBrush(Color.FromRgb(22, 27, 34)),
                Foreground      = new SolidColorBrush(CText),
                BorderBrush     = new SolidColorBrush(CBorder),
                BorderThickness = new Thickness(1),
                FontSize        = 11,
                Padding         = new Thickness(8, 6, 8, 6),
                CaretBrush      = new SolidColorBrush(CBlue)
            };

            var placeholder = new TextBlock
            {
                Text             = "Search UID...",
                FontSize         = 11,
                Foreground       = new SolidColorBrush(CDim),
                IsHitTestVisible = false,
                VerticalAlignment = VerticalAlignment.Center,
                Margin           = new Thickness(10, 0, 0, 0)
            };

            var grid = new Grid();
            grid.Children.Add(box);
            grid.Children.Add(placeholder);

            box.TextChanged += (s, e) =>
                placeholder.Visibility = string.IsNullOrEmpty(box.Text)
                    ? Visibility.Visible : Visibility.Collapsed;

            var border = new Border { Child = grid, Margin = new Thickness(8, 6, 8, 0) };
            return (border, box);
        }

        private static void PopulateTree(
            StackPanel panel,
            List<string> rootUids,
            AssemblyIndex index,
            List<ResponseRecord> allRecords,
            string query,
            int offset,
            Action<string> onUidClick = null)
        {
            panel.Children.Clear();

            List<string> filtered;
            if (string.IsNullOrEmpty(query))
                filtered = rootUids;
            else
            {
                string q = query.ToUpperInvariant();
                filtered = rootUids.Where(uid =>
                    uid.ToUpperInvariant().Contains(q) ||
                    ChildrenContain(uid, q, index, 0)).ToList();
            }

            if (filtered.Count == 0)
            {
                panel.Children.Add(new TextBlock
                {
                    Text = "No results.", FontSize = 11,
                    Foreground = new SolidColorBrush(CDim),
                    Margin = new Thickness(12, 20, 0, 0)
                });
                return;
            }

            var page = filtered.Skip(offset).Take(PageSize).ToList();

            if (offset > 0)
                panel.Children.Add(BuildPageButton(
                    "▲ Show previous " + Math.Min(offset, PageSize),
                    () => PopulateTree(panel, rootUids, index, allRecords, query, Math.Max(0, offset - PageSize), onUidClick)));

            foreach (var uid in page)
                panel.Children.Add(BuildNodeCard(uid, index, allRecords, 0, onUidClick));

            int shown     = offset + page.Count;
            int remaining = filtered.Count - shown;

            if (remaining > 0)
                panel.Children.Add(BuildPageButton(
                    "▼ Load next " + Math.Min(remaining, PageSize) + "  (" + remaining + " remaining)",
                    () => PopulateTree(panel, rootUids, index, allRecords, query, offset + PageSize, onUidClick)));

            if (filtered.Count > PageSize || offset > 0)
                panel.Children.Add(new TextBlock
                {
                    Text = "Showing " + (offset + 1) + "–" + shown + " of " + filtered.Count,
                    FontSize = 9, Foreground = new SolidColorBrush(CDim),
                    Margin = new Thickness(8, 4, 0, 0)
                });
        }

        private static UIElement BuildPageButton(string label, Action onClick)
        {
            var btn = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(22, 27, 34)),
                BorderBrush = new SolidColorBrush(CBorder),
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4),
                Padding = new Thickness(10, 6, 10, 6), Margin = new Thickness(0, 0, 0, 6),
                Cursor = Cursors.Hand,
                Child = new TextBlock { Text = label, FontSize = 10, Foreground = new SolidColorBrush(CBlue), HorizontalAlignment = HorizontalAlignment.Center }
            };
            btn.MouseLeftButtonUp += (s, e) => onClick();
            btn.MouseEnter += (s, e) => btn.Background = new SolidColorBrush(Color.FromRgb(30, 36, 46));
            btn.MouseLeave += (s, e) => btn.Background = new SolidColorBrush(Color.FromRgb(22, 27, 34));
            return btn;
        }

        private static bool ChildrenContain(string uid, string query, AssemblyIndex index, int depth)
        {
            if (depth > MaxDepth) return false;
            foreach (var child in index.GetChildren(uid))
            {
                if (child.ToUpperInvariant().Contains(query)) return true;
                if (ChildrenContain(child, query, index, depth + 1)) return true;
            }
            return false;
        }

        private static UIElement BuildNodeCard(string uid, AssemblyIndex index, List<ResponseRecord> allRecords, int depth, Action<string> onUidClick = null)
        {
            var node = index.GetNode(uid);
            if (node == null) return new UIElement();

            Border     card        = null;
            StackPanel cardContent = null;

            if (depth == 0)
            {
                card = new Border
                {
                    Background = new SolidColorBrush(CCard), BorderBrush = new SolidColorBrush(CBorder),
                    BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6),
                    Margin = new Thickness(0, 0, 0, 6), Padding = new Thickness(10, 8, 10, 8)
                };
                cardContent = new StackPanel();
                card.Child  = cardContent;
            }
            else
                cardContent = new StackPanel { Margin = new Thickness(IndentPx * depth, 0, 0, 0) };

            var children    = index.GetChildren(uid);
            bool hasChildren = children.Count > 0;

            var headerRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Cursor      = hasChildren ? Cursors.Hand : Cursors.Arrow,
                Margin      = new Thickness(0, depth == 0 ? 0 : 2, 0, 2)
            };

            var expandIcon = new TextBlock
            {
                Text = hasChildren ? "▶" : "·", FontSize = 9,
                Foreground = new SolidColorBrush(hasChildren ? CBlue : CDim),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0), Width = 12
            };
            headerRow.Children.Add(expandIcon);

            var procDir = GetProcDirForUid(uid, index);
            if (!string.IsNullOrEmpty(procDir))
                headerRow.Children.Add(BuildBadge(procDir, GetProcDirColor(procDir)));

            var uidText = new TextBlock
            {
                Text      = uid,
                FontSize  = depth == 0 ? 12 : 10,
                FontWeight = depth == 0 ? FontWeights.SemiBold : FontWeights.Normal,
                FontFamily = new FontFamily("Consolas"),
                Foreground = new SolidColorBrush(depth == 0 ? CBlue : CText),
                VerticalAlignment = VerticalAlignment.Center,
                Margin    = new Thickness(4, 0, 6, 0),
                Cursor    = Cursors.Hand,
                ToolTip   = "Click to copy UID"
            };
            uidText.MouseLeftButtonUp += (s, e) => { Clipboard.SetText(uid); e.Handled = true; };
            headerRow.Children.Add(uidText);

            if (!string.IsNullOrEmpty(node.UidType))
                headerRow.Children.Add(new TextBlock
                {
                    Text = node.UidType, FontSize = 9, Foreground = new SolidColorBrush(CDim),
                    VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0)
                });

            if (node.Components.Count > 0)
            {
                int y = node.Components.Count(c => c.ProcDir == "Y");
                int n = node.Components.Count(c => c.ProcDir == "N");
                int p = node.Components.Count(c => c.ProcDir == "P" || c.ProcDir == "R");
                headerRow.Children.Add(BuildChip(node.Components.Count + " comp", CGray));
                if (y > 0) headerRow.Children.Add(BuildChip("Y:" + y, CGreen));
                if (n > 0) headerRow.Children.Add(BuildChip("N:" + n, CRed));
                if (p > 0) headerRow.Children.Add(BuildChip("P:" + p, CYellow));
            }

            var subsetBtn = new Border
            {
                Background      = new SolidColorBrush(Color.FromArgb(0, 30, 50, 80)),
                BorderBrush     = new SolidColorBrush(Color.FromArgb(0, 56, 182, 255)),
                BorderThickness = new Thickness(1),
                CornerRadius    = new CornerRadius(3),
                Padding         = new Thickness(5, 1, 5, 1),
                Margin          = new Thickness(8, 0, 0, 0),
                Cursor          = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text      = "📜 Show History",
                    FontSize  = 9,
                    Foreground = new SolidColorBrush(Color.FromArgb(0, 56, 182, 255))
                }
            };
            subsetBtn.MouseLeftButtonUp += (s, e) =>
            {
                if (onUidClick != null) onUidClick(uid);
                else MESInsight.MainWindow.OpenSubsetHistory?.Invoke(uid);
                e.Handled = true;
            };
            subsetBtn.MouseEnter += (s, e) =>
            {
                subsetBtn.Background  = new SolidColorBrush(Color.FromArgb(30, 56, 182, 255));
                subsetBtn.BorderBrush = new SolidColorBrush(Color.FromArgb(100, 56, 182, 255));
                ((TextBlock)subsetBtn.Child).Foreground = new SolidColorBrush(Color.FromRgb(56, 182, 255));
            };
            subsetBtn.MouseLeave += (s, e) =>
            {
                subsetBtn.Background  = new SolidColorBrush(Color.FromArgb(0, 30, 50, 80));
                subsetBtn.BorderBrush = new SolidColorBrush(Color.FromArgb(0, 56, 182, 255));
                ((TextBlock)subsetBtn.Child).Foreground = new SolidColorBrush(Color.FromArgb(0, 56, 182, 255));
            };
            headerRow.Children.Add(subsetBtn);

            var histBtn = new TextBlock
            {
                Text = "Show History", FontSize = 9,
                Foreground = new SolidColorBrush(CDim),
                TextDecorations = TextDecorations.Underline,
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0)
            };
            headerRow.Children.Add(histBtn);

            cardContent.Children.Add(headerRow);

            var childrenPanel  = new StackPanel { Visibility = Visibility.Collapsed };
            if (hasChildren)
                foreach (var child in children)
                    if (depth + 1 <= MaxDepth)
                        childrenPanel.Children.Add(BuildNodeCard(child, index, allRecords, depth + 1, onUidClick));

            var historyPanel = new Border
            {
                Visibility = Visibility.Collapsed,
                Background = new SolidColorBrush(Color.FromRgb(10, 14, 20)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(36, 50, 80)),
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4),
                Margin = new Thickness(18, 4, 0, 6), Padding = new Thickness(10, 8, 10, 8)
            };
            bool historyLoaded = false;

            cardContent.Children.Add(childrenPanel);
            cardContent.Children.Add(historyPanel);

            bool expanded = false;
            if (hasChildren)
                headerRow.MouseLeftButtonUp += (s, e) =>
                {
                    expanded = !expanded;
                    childrenPanel.Visibility = expanded ? Visibility.Visible : Visibility.Collapsed;
                    expandIcon.Text = expanded ? "▼" : "▶";
                    e.Handled = true;
                };

            bool histShown = false;
            histBtn.MouseLeftButtonUp += (s, e) =>
            {
                histShown = !histShown;
                if (histShown && !historyLoaded)
                {
                    historyPanel.Child = BuildHistoryContent(uid, allRecords);
                    historyLoaded = true;
                }
                historyPanel.Visibility = histShown ? Visibility.Visible : Visibility.Collapsed;
                histBtn.Text = histShown ? "Hide History" : "Show History";
                histBtn.Foreground = new SolidColorBrush(histShown ? CBlue : CDim);
                e.Handled = true;
            };

            return depth == 0 ? (UIElement)card : cardContent;
        }

        private static UIElement BuildHistoryContent(string uid, List<ResponseRecord> allRecords)
        {
            var stack   = new StackPanel();
            var related = allRecords
                .Where(r => r.Uid == uid || r.UidIn == uid || r.UidOut == uid ||
                            r.UidAssy == uid ||
                            (!string.IsNullOrEmpty(r.AssyUids) && r.AssyUids.Split(',')
                                .Select(x => x.Trim())
                                .Contains(uid, StringComparer.OrdinalIgnoreCase)))
                .OrderBy(r => r.TimestampParsed).ToList();

            var headerRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
            headerRow.Children.Add(new TextBlock
            {
                Text = "History — " + uid, FontSize = 10, FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(CBlue), FontFamily = new FontFamily("Consolas"),
                VerticalAlignment = VerticalAlignment.Center
            });
            headerRow.Children.Add(BuildChip(related.Count + " records", CGray));
            stack.Children.Add(headerRow);

            if (related.Count == 0)
            {
                stack.Children.Add(new TextBlock { Text = "No records found for this UID.", FontSize = 10, Foreground = new SolidColorBrush(CDim) });
                return stack;
            }

            int shown = Math.Min(10, related.Count);
            for (int i = 0; i < shown; i++)
                stack.Children.Add(BuildHistoryRow(related[i]));

            if (related.Count > 10)
            {
                int loadedSoFar = shown;
                Border loadMoreBtn = null;
                loadMoreBtn = BuildPageButton(
                    "Load " + Math.Min(10, related.Count - loadedSoFar) + " more  (" + (related.Count - loadedSoFar) + " remaining)",
                    () =>
                    {
                        int next = Math.Min(loadedSoFar + 10, related.Count);
                        for (int i = loadedSoFar; i < next; i++)
                            stack.Children.Insert(stack.Children.IndexOf(loadMoreBtn), BuildHistoryRow(related[i]));
                        loadedSoFar = next;
                        if (loadedSoFar >= related.Count)
                            stack.Children.Remove(loadMoreBtn);
                        else
                            ((TextBlock)loadMoreBtn.Child).Text =
                                "Load " + Math.Min(10, related.Count - loadedSoFar) + " more  (" + (related.Count - loadedSoFar) + " remaining)";
                    }) as Border;
                stack.Children.Add(loadMoreBtn);
            }

            return stack;
        }

        private static UIElement BuildHistoryRow(ResponseRecord r)
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(18, 22, 30)),
                BorderBrush = new SolidColorBrush(CBorder), BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(3), Padding = new Thickness(8, 5, 8, 5),
                Margin = new Thickness(0, 0, 0, 3)
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(52) });

            var ts = new TextBlock { Text = r.TimestampParsed.ToString("dd.MM.yyyy HH:mm:ss"), FontSize = 9, Foreground = new SolidColorBrush(CDim), VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(ts, 0);

            var type = new TextBlock { Text = r.Type.ToString().Replace("_", " "), FontSize = 9, Foreground = new SolidColorBrush(CText), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 6, 0) };
            Grid.SetColumn(type, 1);

            string resultVal = !string.IsNullOrEmpty(r.ProcDirAssy) ? r.ProcDirAssy : r.Result;
            var result = new TextBlock { Text = resultVal ?? "", FontSize = 9, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(GetProcDirColor(resultVal)), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
            Grid.SetColumn(result, 2);

            var rt = new TextBlock { Text = r.ResponseTime + " ms", FontSize = 9, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(r.ResponseTime > 100 ? CRed : CBlue), VerticalAlignment = VerticalAlignment.Center, TextAlignment = TextAlignment.Right };
            Grid.SetColumn(rt, 3);

            grid.Children.Add(ts); grid.Children.Add(type); grid.Children.Add(result); grid.Children.Add(rt);
            border.Child = grid;
            return border;
        }

        private static string GetProcDirForUid(string uid, AssemblyIndex index)
        {
            foreach (var kv in index.UidToComponents)
            {
                var rel = kv.Value.FirstOrDefault(r => string.Equals(r.UidAssy, uid, StringComparison.OrdinalIgnoreCase));
                if (rel != null) return rel.ProcDir;
            }
            return null;
        }

        private static Color GetProcDirColor(string pd)
        {
            switch (pd)
            {
                case "Y": return CGreen;
                case "N": return CRed;
                case "P": case "R": return CYellow;
                default:  return CGray;
            }
        }

        private static Border BuildBadge(string text, Color col) => new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(40, col.R, col.G, col.B)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(120, col.R, col.G, col.B)),
            BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(3),
            Padding = new Thickness(4, 1, 4, 1), Margin = new Thickness(0, 0, 4, 0),
            Child = new TextBlock { Text = text, FontSize = 9, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(col) }
        };

        private static Border BuildChip(string text, Color col) => new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(25, col.R, col.G, col.B)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(70, col.R, col.G, col.B)),
            BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(3),
            Padding = new Thickness(5, 1, 5, 1), Margin = new Thickness(3, 0, 0, 0),
            Child = new TextBlock { Text = text, FontSize = 9, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Color.FromArgb(200, col.R, col.G, col.B)) }
        };
    }
}