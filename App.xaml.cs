using System;
using System.Windows;

namespace MESInsight
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

        internal static void ShowErrorDialog(Exception ex)
        {
            try
            {
                var dlg = new BugReportDialog(ex);
                dlg.ShowDialog();
            }
            catch
            {
                // Absolute fallback if the custom dialog itself throws
                MessageBox.Show(
                    "An unexpected error occurred.\n\n" + ex?.Message + "\n\n" + ex?.StackTrace,
                    "MES Insight — Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }
}