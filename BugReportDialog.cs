using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace MESInsight
{
    public class BugReportDialog : Window
    {
        private const string ReportEmail = "lukas.paucin@mail.schaeffler.com";

        private static readonly Color ColBg = Color.FromRgb(10, 16, 13);
        private static readonly Color ColTitleBar = Color.FromRgb(11, 43, 22);
        private static readonly Color ColPanel = Color.FromRgb(17, 24, 20);
        private static readonly Color ColPanelAlt = Color.FromRgb(19, 29, 23);
        private static readonly Color ColBorder = Color.FromRgb(42, 72, 51);
        private static readonly Color ColBorderAlt = Color.FromRgb(60, 100, 72);
        private static readonly Color ColGreen = Color.FromRgb(76, 201, 110);
        private static readonly Color ColGreenSoft = Color.FromRgb(124, 232, 150);
        private static readonly Color ColTextPri = Color.FromRgb(233, 242, 236);
        private static readonly Color ColTextSec = Color.FromRgb(202, 219, 208);
        private static readonly Color ColTextDim = Color.FromRgb(142, 164, 150);
        private static readonly Color ColRed = Color.FromRgb(233, 104, 104);
        private static readonly Color ColCardHover = Color.FromRgb(25, 39, 31);

        private readonly TextBox _descriptionBox;
        private readonly TextBox _stackTraceBox;

        public BugReportDialog(Exception ex = null)
        {
            Title = "Report Bug — MES Insight";
            Width = 990;
            SizeToContent = SizeToContent.Height;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;

            string prefilledTrace = ex?.ToString() ?? string.Empty;

            var frame = new Border
            {
                Background = new SolidColorBrush(ColBg),
                BorderBrush = new SolidColorBrush(ColBorderAlt),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10)
            };

            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var titleBar = new Grid
            {
                Background = new SolidColorBrush(ColTitleBar),
                Height = 46
            };
            titleBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            titleBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            titleBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            titleBar.MouseLeftButtonDown += (s, e) => WindowResizer.DragMove(this);

            titleBar.Children.Add(new TextBlock
            {
                Text = "🐞",
                FontSize = 18,
                Margin = new Thickness(16, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center
            });

            var titleStack = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center
            };
            titleStack.Children.Add(new TextBlock
            {
                Text = "Report a Bug",
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(ColTextPri)
            });
            titleStack.Children.Add(new TextBlock
            {
                Text = ex != null
                    ? "An error was detected — please describe what happened"
                    : "MES Insight — Send feedback or report an issue",
                FontSize = 10,
                Foreground = new SolidColorBrush(ColTextDim)
            });
            Grid.SetColumn(titleStack, 1);
            titleBar.Children.Add(titleStack);

            var btnClose = MakeCloseButton();
            Grid.SetColumn(btnClose, 2);
            titleBar.Children.Add(btnClose);

            Grid.SetRow(titleBar, 0);
            root.Children.Add(titleBar);

            var separator = new Border
            {
                Height = 1,
                Background = new SolidColorBrush(ColBorder)
            };
            Grid.SetRow(separator, 1);
            root.Children.Add(separator);

            var body = new StackPanel
            {
                Margin = new Thickness(24, 18, 24, 18)
            };

            body.Children.Add(FieldLabel("Describe the issue"));
            _descriptionBox = MakeInputBox(108, multiline: true);
            body.Children.Add(_descriptionBox);
            body.Children.Add(new Border { Height = 14 });


            if (!string.IsNullOrEmpty(prefilledTrace))
            {
                body.Children.Add(new Border { Height = 14 });
                body.Children.Add(FieldLabel("Stack trace (auto-captured)"));
                _stackTraceBox = new TextBox
                {
                    Text = prefilledTrace,
                    IsReadOnly = true,
                    AcceptsReturn = true,
                    TextWrapping = TextWrapping.NoWrap,
                    Height = 158,
                    Margin = new Thickness(0, 4, 0, 0),
                    Background = new SolidColorBrush(Color.FromRgb(8, 20, 14)),
                    Foreground = new SolidColorBrush(ColGreenSoft),
                    BorderBrush = new SolidColorBrush(ColBorderAlt),
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(8, 6, 8, 6),
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 10,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    CaretBrush = new SolidColorBrush(ColGreen)
                };
                body.Children.Add(_stackTraceBox);
            }
            else
            {
                body.Children.Add(new Border { Height = 10 });
                var expander = new Expander
                {
                    Header = "Additional technical details (optional)",
                    IsExpanded = false,
                    Foreground = new SolidColorBrush(ColTextDim),
                    FontSize = 11,
                    Margin = new Thickness(0, 2, 0, 0)
                };
                _stackTraceBox = MakeInputBox(84, multiline: true, mono: true);
                expander.Content = _stackTraceBox;
                body.Children.Add(expander);
            }

            Grid.SetRow(body, 2);
            root.Children.Add(body);

            var sendSection = new Border
            {
                Background = new SolidColorBrush(ColPanelAlt),
                BorderBrush = new SolidColorBrush(ColBorder),
                BorderThickness = new Thickness(0, 1, 0, 0),
                CornerRadius = new CornerRadius(0, 0, 10, 10),
                Padding = new Thickness(24, 18, 24, 22)
            };

            var sendGrid = new Grid();
            sendGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            sendGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            sendGrid.Children.Add(new TextBlock
            {
                Text = "Choose how to send the report",
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(ColTextSec),
                Margin = new Thickness(0, 0, 0, 12)
            });

            var optionGrid = new UniformGrid
            {
                Columns = 4,
                Rows = 1
            };
            Grid.SetRow(optionGrid, 1);

            var mailAppTile = MakeSendTile(CreateMailAppIcon(), "Mail app",
                "Opens your default desktop mail application.", false);
            mailAppTile.MouseLeftButtonUp += (s, e) => Send(SendMode.DefaultApp);

            var outlookWebTile = MakeSendTile(CreateOutlookWebIcon(), "Outlook Web",
                "Recommended: opens web compose. Attachment must be added manually.", false);
            outlookWebTile.MouseLeftButtonUp += (s, e) => Send(SendMode.OutlookWeb);

            var gmailTile = MakeSendTile(CreateGmailIcon(), "Gmail", "Opens Gmail compose. Attachment must be added manually.", false);
            gmailTile.MouseLeftButtonUp += (s, e) => Send(SendMode.Gmail);

            var copyTile = MakeSendTile(CreateClipboardIcon(), "Copy report",
                "Copies the report text and screenshot to the clipboard.", false);
            copyTile.MouseLeftButtonUp += (s, e) => CopyToClipboard();

            optionGrid.Children.Add(mailAppTile);
            optionGrid.Children.Add(outlookWebTile);
            optionGrid.Children.Add(gmailTile);
            optionGrid.Children.Add(copyTile);

            sendGrid.Children.Add(optionGrid);
            sendSection.Child = sendGrid;

            Grid.SetRow(sendSection, 3);
            root.Children.Add(sendSection);

            frame.Child = root;
            Content = frame;
        }

        private enum SendMode
        {
            DefaultApp,
            OutlookWeb,
            Gmail
        }

        private void Send(SendMode mode)
        {
            string text = BuildReportText();
            string subject = Uri.EscapeDataString("Bug Report — MES Insight");
            string body = Uri.EscapeDataString(TruncateForUrl(text));
            string url;

            switch (mode)
            {
                case SendMode.OutlookWeb:
                    TryCopyReportToClipboardSilently();
                    url = "https://outlook.office.com/mail/deeplink/compose"
                          + $"?to={Uri.EscapeDataString(ReportEmail)}"
                          + $"&subject={subject}&body={body}";
                    break;
                case SendMode.Gmail:
                    TryCopyReportToClipboardSilently();
                    url = "https://mail.google.com/mail/?view=cm"
                          + $"&to={Uri.EscapeDataString(ReportEmail)}"
                          + $"&su={subject}&body={body}";
                    break;
                default:
                    url = $"mailto:{ReportEmail}?subject={subject}&body={body}";
                    break;
            }

            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "The selected mail option could not be opened.\n\n"
                    + ex.Message
                    + "\n\nPlease send the report manually to:\n"
                    + ReportEmail,
                    "MES Insight — Send Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        private void CopyToClipboard()
        {
            try
            {
                var data = new DataObject();
                data.SetText(BuildReportText());

                Clipboard.SetDataObject(data, true);

                MessageBox.Show(
                    "The report text was copied to the clipboard.\n\nPaste it into an email and send it to:\n" +
                    ReportEmail,
                    "Copied",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "The report could not be copied.\n\n" + ex.Message,
                    "MES Insight",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        private void TryCopyReportToClipboardSilently()
        {
            try
            {
                var data = new DataObject();
                data.SetText(BuildReportText());

                Clipboard.SetDataObject(data, true);
            }
            catch
            {
            }
        }


        private string BuildReportText()
        {
            string description = _descriptionBox?.Text.Trim() ?? string.Empty;
            string trace = _stackTraceBox?.Text.Trim() ?? string.Empty;
            var builder = new System.Text.StringBuilder();

            builder.AppendLine("=== MES Insight — Bug Report ===");
            builder.AppendLine();
            builder.AppendLine("Description:");
            builder.AppendLine(string.IsNullOrEmpty(description) ? "(no description provided)" : description);

            if (!string.IsNullOrEmpty(trace))
            {
                builder.AppendLine();
                builder.AppendLine("--- Stack Trace / Technical Details ---");
                builder.AppendLine(trace);
            }

            builder.AppendLine();
            builder.AppendLine("--- Environment ---");
            builder.AppendLine("App version : MES Insight v1.0");
            builder.AppendLine($"OS          : {Environment.OSVersion}");
            builder.AppendLine($"Date/Time   : {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            builder.AppendLine($"Machine     : {Environment.MachineName}");
            builder.AppendLine($"User        : {Environment.UserName}");


            return builder.ToString();
        }


        private static string TruncateForUrl(string text, int maxChars = 900)
        {
            if (text.Length <= maxChars)
                return text;

            return text.Substring(0, maxChars) + "\n\n[... truncated — attach the full details manually ...]";
        }

        private static TextBlock FieldLabel(string text)
        {
            return new TextBlock
            {
                Text = text,
                FontSize = 11,
                Foreground = new SolidColorBrush(ColTextDim),
                Margin = new Thickness(0, 0, 0, 4)
            };
        }

        private static TextBox MakeInputBox(double height, bool multiline = false, bool mono = false)
        {
            var box = new TextBox
            {
                AcceptsReturn = multiline,
                TextWrapping = multiline ? TextWrapping.Wrap : TextWrapping.NoWrap,
                Height = height,
                Background = new SolidColorBrush(ColPanel),
                Foreground = new SolidColorBrush(ColTextSec),
                BorderBrush = new SolidColorBrush(ColBorderAlt),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8, 6, 8, 6),
                FontSize = mono ? 10 : 12,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                CaretBrush = new SolidColorBrush(ColGreen)
            };

            if (mono)
                box.FontFamily = new FontFamily("Consolas");

            return box;
        }


        private static Border MakeSendTile(FrameworkElement icon, string title, string subtitle, bool recommended)
        {
            var titleBlock = new TextBlock
            {
                Text = title,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(ColTextPri),
                Margin = new Thickness(0, 10, 0, 5)
            };

            var subtitleBlock = new TextBlock
            {
                Text = subtitle,
                FontSize = 9.5,
                Foreground = new SolidColorBrush(ColTextDim),
                TextWrapping = TextWrapping.Wrap
            };

            var tag = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(46, ColGreen.R, ColGreen.G, ColGreen.B)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(85, ColGreen.R, ColGreen.G, ColGreen.B)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(7),
                Padding = new Thickness(6, 2, 6, 2),
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 10, 0, 0),
                Child = new TextBlock
                {
                    Text = "Recommended",
                    FontSize = 8.5,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(ColGreenSoft)
                },
                Visibility = recommended ? Visibility.Visible : Visibility.Collapsed
            };

            var stack = new StackPanel();
            stack.Children.Add(icon);
            stack.Children.Add(titleBlock);
            stack.Children.Add(subtitleBlock);
            stack.Children.Add(tag);

            var normalBackground = new SolidColorBrush(Color.FromRgb(18, 27, 22));
            var hoverBackground = new SolidColorBrush(ColCardHover);
            var normalBorder = new SolidColorBrush(recommended ? ColBorderAlt : ColBorder);
            var hoverBorder = new SolidColorBrush(recommended ? ColGreenSoft : Color.FromRgb(92, 136, 106));

            var tile = new Border
            {
                Margin = new Thickness(0, 0, 12, 0),
                Height = 176,
                Padding = new Thickness(14, 14, 14, 12),
                Background = normalBackground,
                BorderBrush = normalBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Cursor = Cursors.Hand,
                Child = stack
            };

            tile.MouseEnter += (s, e) =>
            {
                tile.Background = hoverBackground;
                tile.BorderBrush = hoverBorder;
            };
            tile.MouseLeave += (s, e) =>
            {
                tile.Background = normalBackground;
                tile.BorderBrush = normalBorder;
            };

            return tile;
        }

        private static FrameworkElement CreateOutlookDesktopIcon()
        {
            var grid = new Grid
            {
                Width = 48,
                Height = 48
            };

            var mailSheet = new Border
            {
                Width = 31,
                Height = 38,
                CornerRadius = new CornerRadius(8),
                Background = new SolidColorBrush(Color.FromRgb(87, 165, 255)),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };
            grid.Children.Add(mailSheet);

            grid.Children.Add(new TextBlock
            {
                Text = "✉",
                FontSize = 17,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0)
            });

            var front = new Border
            {
                Width = 28,
                Height = 36,
                CornerRadius = new CornerRadius(8),
                Background = new SolidColorBrush(Color.FromRgb(0, 89, 179)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center
            };
            front.Child = new TextBlock
            {
                Text = "O",
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center
            };
            grid.Children.Add(front);

            return grid;
        }

        private static FrameworkElement CreateOutlookWebIcon()
        {
            var root = new Grid
            {
                Width = 48,
                Height = 48
            };

            var back = new Border
            {
                Width = 31,
                Height = 38,
                CornerRadius = new CornerRadius(8),
                Background = new SolidColorBrush(Color.FromRgb(57, 138, 247)),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };
            root.Children.Add(back);

            var globe = new Grid
            {
                Width = 18,
                Height = 18,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 2, 2)
            };
            globe.Children.Add(new Ellipse
            {
                Stroke = Brushes.White,
                StrokeThickness = 1.4
            });
            globe.Children.Add(new Line
            {
                X1 = 9,
                Y1 = 2,
                X2 = 9,
                Y2 = 16,
                Stroke = Brushes.White,
                StrokeThickness = 1
            });
            globe.Children.Add(new Line
            {
                X1 = 3,
                Y1 = 9,
                X2 = 15,
                Y2 = 9,
                Stroke = Brushes.White,
                StrokeThickness = 1
            });
            root.Children.Add(globe);

            var front = new Border
            {
                Width = 28,
                Height = 36,
                CornerRadius = new CornerRadius(8),
                Background = new SolidColorBrush(Color.FromRgb(0, 99, 197)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center
            };
            front.Child = new TextBlock
            {
                Text = "O",
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center
            };
            root.Children.Add(front);

            return root;
        }

        private static FrameworkElement CreateMailAppIcon()
        {
            var icon = new Grid
            {
                Width = 48,
                Height = 48
            };

            icon.Children.Add(new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(27, 114, 214)),
                CornerRadius = new CornerRadius(10)
            });

            var envelope = new Canvas
            {
                Width = 32,
                Height = 22,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            envelope.Children.Add(new Rectangle
            {
                Width = 32,
                Height = 22,
                RadiusX = 4,
                RadiusY = 4,
                Stroke = Brushes.White,
                StrokeThickness = 2
            });
            envelope.Children.Add(new Line
                { X1 = 2, Y1 = 3, X2 = 16, Y2 = 13, Stroke = Brushes.White, StrokeThickness = 2 });
            envelope.Children.Add(new Line
                { X1 = 30, Y1 = 3, X2 = 16, Y2 = 13, Stroke = Brushes.White, StrokeThickness = 2 });
            icon.Children.Add(envelope);

            return icon;
        }

        private static FrameworkElement CreateGmailIcon()
        {
            var root = new Grid
            {
                Width = 48,
                Height = 48
            };

            root.Children.Add(new Border
            {
                Background = Brushes.White,
                CornerRadius = new CornerRadius(10),
                BorderBrush = new SolidColorBrush(Color.FromRgb(223, 228, 234)),
                BorderThickness = new Thickness(1)
            });

            var canvas = new Canvas
            {
                Width = 34,
                Height = 24,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            canvas.Children.Add(new Line
            {
                X1 = 2, Y1 = 20, X2 = 2, Y2 = 4, Stroke = new SolidColorBrush(Color.FromRgb(66, 133, 244)),
                StrokeThickness = 3
            });
            canvas.Children.Add(new Line
            {
                X1 = 32, Y1 = 20, X2 = 32, Y2 = 4, Stroke = new SolidColorBrush(Color.FromRgb(234, 67, 53)),
                StrokeThickness = 3
            });
            canvas.Children.Add(new Line
            {
                X1 = 2, Y1 = 4, X2 = 17, Y2 = 15, Stroke = new SolidColorBrush(Color.FromRgb(234, 67, 53)),
                StrokeThickness = 3
            });
            canvas.Children.Add(new Line
            {
                X1 = 32, Y1 = 4, X2 = 17, Y2 = 15, Stroke = new SolidColorBrush(Color.FromRgb(234, 67, 53)),
                StrokeThickness = 3
            });
            canvas.Children.Add(new Line
            {
                X1 = 2, Y1 = 20, X2 = 12, Y2 = 12, Stroke = new SolidColorBrush(Color.FromRgb(52, 168, 83)),
                StrokeThickness = 3
            });
            canvas.Children.Add(new Line
            {
                X1 = 32, Y1 = 20, X2 = 22, Y2 = 12, Stroke = new SolidColorBrush(Color.FromRgb(251, 188, 5)),
                StrokeThickness = 3
            });
            root.Children.Add(canvas);

            return root;
        }

        private static FrameworkElement CreateClipboardIcon()
        {
            var grid = new Grid
            {
                Width = 48,
                Height = 48
            };

            grid.Children.Add(new Border
            {
                Width = 34,
                Height = 38,
                CornerRadius = new CornerRadius(9),
                Background = new SolidColorBrush(Color.FromRgb(18, 64, 38)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(86, 179, 118)),
                BorderThickness = new Thickness(1),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom
            });

            grid.Children.Add(new Border
            {
                Width = 16,
                Height = 10,
                CornerRadius = new CornerRadius(5),
                Background = new SolidColorBrush(Color.FromRgb(86, 179, 118)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 5, 0, 0)
            });

            var lines = new StackPanel
            {
                Width = 18,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 5, 0, 0)
            };
            lines.Children.Add(
                new Border { Height = 2, Background = Brushes.White, Margin = new Thickness(0, 0, 0, 4) });
            lines.Children.Add(
                new Border { Height = 2, Background = Brushes.White, Margin = new Thickness(0, 0, 0, 4) });
            lines.Children.Add(new Border { Height = 2, Background = Brushes.White });
            grid.Children.Add(lines);

            return grid;
        }

        private Border MakeCloseButton()
        {
            var glyph = new TextBlock
            {
                Text = "✕",
                FontSize = 13,
                Foreground = new SolidColorBrush(ColRed),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            var button = new Border
            {
                Width = 36,
                Height = 36,
                Background = Brushes.Transparent,
                CornerRadius = new CornerRadius(4),
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 4, 8, 4),
                Child = glyph
            };

            button.MouseEnter += (s, e) =>
            {
                button.Background = new SolidColorBrush(Color.FromRgb(72, 28, 33));
                glyph.Foreground = new SolidColorBrush(Color.FromRgb(255, 194, 198));
            };
            button.MouseLeave += (s, e) =>
            {
                button.Background = Brushes.Transparent;
                glyph.Foreground = new SolidColorBrush(ColRed);
            };

            // Prevent title-bar drag handler from swallowing close clicks.
            button.PreviewMouseLeftButtonDown += (s, e) => e.Handled = true;
            button.MouseLeftButtonUp += (s, e) =>
            {
                e.Handled = true;
                Close();
            };

            return button;
        }
    }
}