using System;
using System.Windows;

namespace RTAnalyzer
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var culture = new System.Globalization.CultureInfo("sk-SK");
            System.Threading.Thread.CurrentThread.CurrentCulture = culture;
            System.Threading.Thread.CurrentThread.CurrentUICulture = culture;
            System.Globalization.CultureInfo.DefaultThreadCurrentCulture = culture;
            System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = culture;

            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            Current.DispatcherUnhandledException += OnDispatcherUnhandledException;
        }

        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var exception = e.ExceptionObject as Exception;
            ShowErrorDialog(exception);
        }

        private void OnDispatcherUnhandledException(object sender,
            System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            ShowErrorDialog(e.Exception);
            e.Handled = true;
        }

        private void ShowErrorDialog(Exception ex)
        {
            var errorWindow = new Window
            {
                Title = "Error",
                Width = 600,
                Height = 400,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Background = System.Windows.Media.Brushes.White
            };

            var grid = new System.Windows.Controls.Grid();
            grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition
                { Height = new GridLength(1, GridUnitType.Auto) });
            grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition
                { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition
                { Height = new GridLength(1, GridUnitType.Auto) });

            var messagePanel = new System.Windows.Controls.StackPanel
            {
                Margin = new Thickness(20),
                Orientation = System.Windows.Controls.Orientation.Vertical
            };

            var iconText = new System.Windows.Controls.TextBlock
            {
                Text = "⚠️",
                FontSize = 32,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 10)
            };

            var errorTitle = new System.Windows.Controls.TextBlock
            {
                Text = "An unexpected error occurred",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 10)
            };

            var userMessage = "An error occurred in the application.";

            if (ex?.Message.Contains("invalid range") == true || ex?.Message.Contains("axis") == true)
                userMessage = "Not enough data to display charts. Please adjust your filters and try again.";
            else if (ex?.Message != null) userMessage = ex.Message;

            var errorMessage = new System.Windows.Controls.TextBlock
            {
                Text = userMessage,
                TextWrapping = TextWrapping.Wrap,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 15)
            };

            messagePanel.Children.Add(iconText);
            messagePanel.Children.Add(errorTitle);
            messagePanel.Children.Add(errorMessage);

            var expander = new System.Windows.Controls.Expander
            {
                Header = "Show Technical Details",
                Margin = new Thickness(20, 0, 20, 10),
                IsExpanded = false
            };

            var stackTraceBox = new System.Windows.Controls.TextBox
            {
                Text = ex?.ToString() ?? "No stack trace available",
                TextWrapping = TextWrapping.Wrap,
                IsReadOnly = true,
                VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto,
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                FontSize = 11,
                Padding = new Thickness(10),
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(245, 245, 245))
            };

            expander.Content = stackTraceBox;

            var buttonPanel = new System.Windows.Controls.StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(20)
            };

            var copyButton = new System.Windows.Controls.Button
            {
                Content = "📋 Copy Error",
                Padding = new Thickness(15, 8, 15, 8),
                Margin = new Thickness(0, 0, 10, 0),
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(52, 152, 219)),
                Foreground = System.Windows.Media.Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand
            };

            copyButton.Click += (s, e) =>
            {
                try
                {
                    Clipboard.SetText(ex?.ToString() ?? "No error details available");
                    MessageBox.Show("Error details copied to clipboard", "Success", MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                catch
                {
                    MessageBox.Show("Failed to copy to clipboard", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };

            var okButton = new System.Windows.Controls.Button
            {
                Content = "OK",
                Padding = new Thickness(30, 8, 30, 8),
                Background =
                    new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(149, 165, 166)),
                Foreground = System.Windows.Media.Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand,
                IsDefault = true
            };

            okButton.Click += (s, e) => errorWindow.Close();

            buttonPanel.Children.Add(copyButton);
            buttonPanel.Children.Add(okButton);

            System.Windows.Controls.Grid.SetRow(messagePanel, 0);
            System.Windows.Controls.Grid.SetRow(expander, 1);
            System.Windows.Controls.Grid.SetRow(buttonPanel, 2);

            grid.Children.Add(messagePanel);
            grid.Children.Add(expander);
            grid.Children.Add(buttonPanel);

            errorWindow.Content = grid;
            errorWindow.ShowDialog();
        }
    }
}