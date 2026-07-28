using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ClosedXML.Excel;
using ClosedXML.Excel.Drawings;
using MESInsight.Core;
using Microsoft.Win32;

namespace MESInsight
{
    public enum ExportScope
    {
        FullPeriod,
        SelectedDay
    }

    public static class ExcelExporter
    {
        public static void Export(
            string stationName,
            Dictionary<(MessageType, ChartType), ChartData> chartCache,
            Dictionary<DateTime, List<ResponseRecord>> recordsGroupedByDay,
            List<ResponseRecord> filteredRecords,
            Func<MessageType, StackPanel> getPanelForMessageType,
            ExportScope scope,
            DateTime? selectedDay)
        {
            SaveFileDialog dlg = new SaveFileDialog
            {
                Title    = "Export to Excel",
                Filter   = "Excel Workbook (*.xlsx)|*.xlsx",
                FileName = stationName.Replace(" ", "_") + "_" + DateTime.Now.ToString("yyyy-MM-dd") + ".xlsx"
            };

            if (dlg.ShowDialog() != true) return;

            try
            {
                using (XLWorkbook workbook = new XLWorkbook())
                {
                    AddSummarySheet(workbook, stationName, filteredRecords, recordsGroupedByDay, scope, selectedDay);
                    AddDailyStatsSheet(workbook, recordsGroupedByDay);
                    AddMessageTypeSheets(workbook, chartCache, filteredRecords, getPanelForMessageType, scope);

                    workbook.SaveAs(dlg.FileName);
                }

                MessageBox.Show(
                    "Export completed:\n" + dlg.FileName,
                    "Export Complete",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Export failed:\n" + ex.Message,
                    "Export Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private static void AddSummarySheet(
            XLWorkbook wb, string stationName,
            List<ResponseRecord> allRecords,
            Dictionary<DateTime, List<ResponseRecord>> byDay,
            ExportScope scope,
            DateTime? selectedDay)
        {
            IXLWorksheet ws = wb.Worksheets.Add("Summary");
            StyleSheet(ws);

            ws.Cell("A1").Value = stationName;
            ws.Cell("A1").Style.Font.Bold      = true;
            ws.Cell("A1").Style.Font.FontSize  = 16;
            ws.Cell("A1").Style.Font.FontColor = XLColor.FromArgb(63, 185, 80);

            ws.Cell("A2").Value = "Exported: " + DateTime.Now.ToString("dd. MMM yyyy HH:mm");
            ws.Cell("A2").Style.Font.FontColor = XLColor.FromArgb(140, 140, 140);

            ws.Cell("A3").Value = byDay.Count > 0
                ? "Date range: " + byDay.Keys.Min().ToString("dd.MM.yyyy") + " – " + byDay.Keys.Max().ToString("dd.MM.yyyy")
                : "Date range: N/A";

            ws.Cell("A4").Value = scope == ExportScope.FullPeriod
                ? "Export scope: Full period"
                : "Export scope: Selected day (" + (selectedDay?.ToString("dd.MM.yyyy") ?? "N/A") + ")";
            ws.Cell("A4").Style.Font.FontColor = XLColor.FromArgb(120, 160, 200);

            ws.Row(5).Height = 8;
            WriteHeader(ws, 6, new[] { "Metric", "Value" });

            var sorted = allRecords
                .Where(r => r.ResponseTime > 0)
                .Select(r => (double)r.ResponseTime)
                .OrderBy(v => v)
                .ToList();

            int row = 7;

            if (sorted.Count > 0)
            {
                double mean   = sorted.Average();
                double median = Percentile(sorted, 50);
                double q1     = Percentile(sorted, 25);
                double q3     = Percentile(sorted, 75);
                double iqr    = q3 - q1;
                double stdDev = Math.Sqrt(sorted.Sum(v => Math.Pow(v - mean, 2)) / sorted.Count);
                int p95       = (int)Percentile(sorted, 95);
                int outliers  = sorted.Count(v => v > q3 + 1.5 * iqr || v < q1 - 1.5 * iqr);

                var stats = new[]
                {
                    ("Total records",     allRecords.Count.ToString("N0")),
                    ("Total days",        byDay.Count.ToString()),
                    ("Median",            median.ToString("N0") + " ms"),
                    ("Mean",              mean.ToString("N1") + " ms"),
                    ("P95",               p95 + " ms"),
                    ("Q1 (25th pct)",     q1.ToString("N0") + " ms"),
                    ("Q3 (75th pct)",     q3.ToString("N0") + " ms"),
                    ("IQR",               iqr.ToString("N0") + " ms"),
                    ("Std deviation (σ)", stdDev.ToString("N1") + " ms"),
                    ("Min",               sorted.First().ToString("N0") + " ms"),
                    ("Max",               sorted.Last().ToString("N0") + " ms"),
                    ("Outliers",          outliers.ToString("N0")),
                };

                foreach (var (label, value) in stats)
                {
                    ws.Cell(row, 1).Value = label;
                    ws.Cell(row, 2).Value = value;
                    ws.Cell(row, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                    row++;
                }
            }

            row += 2;
            WriteHeader(ws, row, new[] { "Message Type", "Records", "Median (ms)", "P95 (ms)" });
            row++;

            var byType = allRecords
                .Where(r => r.ResponseTime > 0)
                .GroupBy(r => r.Type)
                .OrderByDescending(g => g.Count());

            foreach (var g in byType)
            {
                var vals = g.Select(r => (double)r.ResponseTime).OrderBy(v => v).ToList();
                ws.Cell(row, 1).Value = g.Key.ToString().Replace("_", " ");
                ws.Cell(row, 2).Value = vals.Count;
                ws.Cell(row, 3).Value = Math.Round(Percentile(vals, 50), 0);
                ws.Cell(row, 4).Value = Math.Round(Percentile(vals, 95), 0);
                row++;
            }

            ws.Column(1).Width = 26;
            ws.Column(2).Width = 16;
            ws.Column(3).Width = 14;
            ws.Column(4).Width = 14;
        }

        private static void AddDailyStatsSheet(
            XLWorkbook wb,
            Dictionary<DateTime, List<ResponseRecord>> byDay)
        {
            IXLWorksheet ws = wb.Worksheets.Add("Daily Stats");
            StyleSheet(ws);

            ws.Cell("A1").Value = "Daily Statistics";
            ws.Cell("A1").Style.Font.Bold      = true;
            ws.Cell("A1").Style.Font.FontSize  = 13;
            ws.Cell("A1").Style.Font.FontColor = XLColor.FromArgb(63, 185, 80);

            string[] headers = { "Date", "Records", "Median (ms)", "Mean (ms)", "P95 (ms)", "Q1 (ms)", "Q3 (ms)", "IQR (ms)", "σ (ms)", "Min (ms)", "Max (ms)", "Outliers", "Slowest UID" };
            WriteHeader(ws, 3, headers);

            int row = 4;
            foreach (DateTime date in byDay.Keys.OrderBy(d => d))
            {
                var dayRecords = byDay[date].Where(r => r.ResponseTime > 0).ToList();
                var values = dayRecords.Select(r => (double)r.ResponseTime).OrderBy(v => v).ToList();

                if (values.Count == 0) continue;

                double mean   = values.Average();
                double median = Percentile(values, 50);
                double q1     = Percentile(values, 25);
                double q3     = Percentile(values, 75);
                double iqr    = q3 - q1;
                double stdDev = Math.Sqrt(values.Sum(v => Math.Pow(v - mean, 2)) / values.Count);
                int p95       = (int)Percentile(values, 95);
                int outliers  = values.Count(v => v > q3 + 1.5 * iqr || v < q1 - 1.5 * iqr);

                var slowest = dayRecords.OrderByDescending(r => r.ResponseTime).FirstOrDefault();
                string slowestUid = slowest != null
                    ? (slowest.UidIn ?? slowest.Uid ?? slowest.UidOut ?? "")
                    : "";

                ws.Cell(row, 1).Value  = date.ToString("dd.MM.yyyy");
                ws.Cell(row, 2).Value  = values.Count;
                ws.Cell(row, 3).Value  = Math.Round(median, 0);
                ws.Cell(row, 4).Value  = Math.Round(mean, 1);
                ws.Cell(row, 5).Value  = p95;
                ws.Cell(row, 6).Value  = Math.Round(q1, 0);
                ws.Cell(row, 7).Value  = Math.Round(q3, 0);
                ws.Cell(row, 8).Value  = Math.Round(iqr, 0);
                ws.Cell(row, 9).Value  = Math.Round(stdDev, 1);
                ws.Cell(row, 10).Value = Math.Round(values.First(), 0);
                ws.Cell(row, 11).Value = Math.Round(values.Last(), 0);
                ws.Cell(row, 12).Value = outliers;
                ws.Cell(row, 13).Value = slowestUid;

                if (row % 2 == 0)
                    ws.Row(row).Style.Fill.BackgroundColor = XLColor.FromArgb(15, 25, 18);

                row++;
            }

            for (int c = 1; c <= headers.Length; c++)
                ws.Column(c).AdjustToContents();
        }

        private static void AddMessageTypeSheets(
            XLWorkbook wb,
            Dictionary<(MessageType, ChartType), ChartData> chartCache,
            List<ResponseRecord> filteredRecords,
            Func<MessageType, StackPanel> getPanelForMessageType,
            ExportScope scope)
        {
            var messageTypes = chartCache.Keys
                .Select(k => k.Item1)
                .Distinct()
                .OrderBy(t => t.ToString())
                .ToList();

            foreach (MessageType mt in messageTypes)
            {
                string sheetName = mt == MessageType.ALL
                    ? "All Records"
                    : mt.ToString().Replace("REQ_", "").Replace("_", " ");
                if (sheetName.Length > 31) sheetName = sheetName.Substring(0, 31);

                IXLWorksheet ws = wb.Worksheets.Add(sheetName);
                StyleSheet(ws);

                ws.Cell("A1").Value = mt == MessageType.ALL ? "All Records" : mt.ToString().Replace("_", " ");
                ws.Cell("A1").Style.Font.Bold      = true;
                ws.Cell("A1").Style.Font.FontSize  = 13;
                ws.Cell("A1").Style.Font.FontColor = XLColor.FromArgb(63, 185, 80);
                ws.Cell("A2").Value = "Exported: " + DateTime.Now.ToString("dd. MMM yyyy HH:mm");
                ws.Cell("A2").Style.Font.FontColor = XLColor.FromArgb(120, 120, 120);

                var mtRecords = filteredRecords.Where(r => r.Type == mt).ToList();
                var mtValues = mtRecords
                    .Where(r => r.ResponseTime > 0)
                    .Select(r => (double)r.ResponseTime)
                    .OrderBy(v => v)
                    .ToList();

                int currentRow = 4;

                object[][] BuildStatsBlock()
                {
                    if (mtValues.Count == 0) return null;

                    double mean   = mtValues.Average();
                    double median = Percentile(mtValues, 50);
                    double q1     = Percentile(mtValues, 25);
                    double q3     = Percentile(mtValues, 75);
                    double iqr    = q3 - q1;
                    double stdDev = Math.Sqrt(mtValues.Sum(v => Math.Pow(v - mean, 2)) / mtValues.Count);
                    int p95       = (int)Percentile(mtValues, 95);
                    int outliers  = mtValues.Count(v => v > q3 + 1.5 * iqr || v < q1 - 1.5 * iqr);

                    return new[]
                    {
                        new object[] { "Records",       mtValues.Count.ToString("N0") },
                        new object[] { "Median",        median.ToString("N0") + " ms" },
                        new object[] { "Mean",          mean.ToString("N1") + " ms" },
                        new object[] { "P95",           p95 + " ms" },
                        new object[] { "Q1 (25th pct)", q1.ToString("N0") + " ms" },
                        new object[] { "Q3 (75th pct)", q3.ToString("N0") + " ms" },
                        new object[] { "IQR",           iqr.ToString("N0") + " ms" },
                        new object[] { "σ (std dev)",   stdDev.ToString("N1") + " ms" },
                        new object[] { "Min",           mtValues.First().ToString("N0") + " ms" },
                        new object[] { "Max",           mtValues.Last().ToString("N0") + " ms" },
                        new object[] { "Outliers",      outliers.ToString() },
                    };
                }

                void WriteStatsBlock()
                {
                    var stats = BuildStatsBlock();
                    if (stats == null) return;

                    WriteHeader(ws, currentRow, new[] { "Metric", "Value" });
                    currentRow++;

                    foreach (object[] stat in stats)
                    {
                        ws.Cell(currentRow, 1).Value = stat[0].ToString();
                        ws.Cell(currentRow, 2).Value = stat[1].ToString();
                        ws.Cell(currentRow, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                        currentRow++;
                    }

                    ws.Column(1).Width = 22;
                    ws.Column(2).Width = 16;
                    currentRow += 2;
                }

                void WriteTop10SlowestBlock()
                {
                    var top10 = mtRecords
                        .Where(r => r.ResponseTime > 0)
                        .OrderByDescending(r => r.ResponseTime)
                        .Take(10)
                        .ToList();

                    if (top10.Count == 0) return;

                    ws.Cell(currentRow, 1).Value = "Top 10 Slowest Records";
                    ws.Cell(currentRow, 1).Style.Font.Bold      = true;
                    ws.Cell(currentRow, 1).Style.Font.FontColor = XLColor.FromArgb(220, 130, 60);
                    currentRow++;

                    WriteHeader(ws, currentRow, new[] { "Date", "Time", "UID", "Response Time (ms)" });
                    currentRow++;

                    foreach (var r in top10)
                    {
                        string uid = r.UidIn ?? r.Uid ?? r.UidOut ?? r.UidAssy ?? "";
                        ws.Cell(currentRow, 1).Value = r.TimestampParsed.ToString("dd.MM.yyyy");
                        ws.Cell(currentRow, 2).Value = r.TimestampParsed.ToString("HH:mm:ss");
                        ws.Cell(currentRow, 3).Value = uid;
                        ws.Cell(currentRow, 4).Value = r.ResponseTime;
                        currentRow++;
                    }

                    currentRow += 2;
                }

                StackPanel panel = getPanelForMessageType(mt);

                if (panel == null || panel.ActualWidth <= 0)
                {
                    WriteStatsBlock();
                    WriteTop10SlowestBlock();
                    continue;
                }

                int chartIndex = 0;
                bool wroteAnyChart = false;

                foreach (UIElement child in panel.Children)
                {
                    if (!(child is FrameworkElement fe)) continue;
                    if (fe.ActualWidth <= 0 || fe.ActualHeight <= 0) continue;

                    string chartLabel = GetChartLabelForElement(fe, chartIndex);

                    bool isTimeline = chartLabel == "Timeline";
                    if (isTimeline && scope == ExportScope.FullPeriod)
                    {
                        chartIndex++;
                        continue;
                    }

                    ws.Cell(currentRow, 1).Value = chartLabel;
                    ws.Cell(currentRow, 1).Style.Font.Bold      = true;
                    ws.Cell(currentRow, 1).Style.Font.FontColor = XLColor.FromArgb(100, 160, 120);
                    currentRow++;

                    byte[] imageBytes = RenderElementToBytes(fe);
                    if (imageBytes != null)
                    {
                        int imgW = Math.Min((int)fe.ActualWidth,  1400);
                        int imgH = Math.Min((int)fe.ActualHeight, 2000);

                        using (MemoryStream ms = new MemoryStream(imageBytes))
                        {
                            ws.AddPicture(ms, XLPictureFormat.Png)
                              .MoveTo(ws.Cell(currentRow, 1))
                              .WithSize(imgW, imgH);
                        }

                        int rowsNeeded = (int)Math.Ceiling(imgH / 20.0) + 2;
                        currentRow += rowsNeeded;
                    }
                    else
                    {
                        currentRow++;
                    }

                    wroteAnyChart = true;

                    if (chartLabel == "Trend Chart" || chartLabel == "Box Plot")
                        WriteStatsBlock();

                    chartIndex++;
                }

                if (!wroteAnyChart)
                    WriteStatsBlock();

                WriteTop10SlowestBlock();

                if (mt == MessageType.ALL)
                {
                    ws.Cell(currentRow, 1).Value = "Records per Message Type";
                    ws.Cell(currentRow, 1).Style.Font.Bold      = true;
                    ws.Cell(currentRow, 1).Style.Font.FontColor = XLColor.FromArgb(63, 185, 80);
                    currentRow++;

                    WriteHeader(ws, currentRow, new[] { "Message Type", "Total Records", "With Response Time > 0" });
                    currentRow++;

                    var byType = filteredRecords.GroupBy(r => r.Type).OrderByDescending(g => g.Count());
                    foreach (var g in byType)
                    {
                        ws.Cell(currentRow, 1).Value = g.Key.ToString().Replace("_", " ");
                        ws.Cell(currentRow, 2).Value = g.Count();
                        ws.Cell(currentRow, 3).Value = g.Count(r => r.ResponseTime > 0);
                        currentRow++;
                    }
                }
            }
        }

        private static string GetChartLabelForElement(FrameworkElement fe, int fallbackIndex)
        {
            string tag = fe.Tag as string;
            switch (tag)
            {
                case "ChartAnchor:Trend": return "Trend Chart";
                case "ChartAnchor:Timeline": return "Timeline";
                case "ChartAnchor:BoxPlot": return "Box Plot";
                case "ChartAnchor:Histogram": return "Histogram";
            }

            string[] fallbackLabels = { "Trend Chart", "Timeline", "Box Plot", "Histogram" };
            return fallbackIndex < fallbackLabels.Length
                ? fallbackLabels[fallbackIndex]
                : "Chart " + (fallbackIndex + 1);
        }

        private static byte[] RenderElementToBytes(FrameworkElement element)
        {
            try
            {
                double dpi  = 96;
                int width   = (int)Math.Max(1, element.ActualWidth);
                int height  = (int)Math.Max(1, element.ActualHeight);

                RenderTargetBitmap rtb = new RenderTargetBitmap(
                    width, height, dpi, dpi, PixelFormats.Pbgra32);
                rtb.Render(element);

                PngBitmapEncoder encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(rtb));

                using (MemoryStream ms = new MemoryStream())
                {
                    encoder.Save(ms);
                    return ms.ToArray();
                }
            }
            catch
            {
                return null;
            }
        }

        private static void WriteHeader(IXLWorksheet ws, int row, string[] columns)
        {
            for (int c = 0; c < columns.Length; c++)
            {
                IXLCell cell = ws.Cell(row, c + 1);
                cell.Value = columns[c];
                cell.Style.Font.Bold      = true;
                cell.Style.Font.FontColor = XLColor.FromArgb(63, 185, 80);
                cell.Style.Fill.BackgroundColor = XLColor.FromArgb(10, 25, 15);
                cell.Style.Border.BottomBorder      = XLBorderStyleValues.Thin;
                cell.Style.Border.BottomBorderColor = XLColor.FromArgb(30, 80, 44);
            }
        }

        private static void StyleSheet(IXLWorksheet ws)
        {
            ws.Style.Font.FontName        = "Segoe UI";
            ws.Style.Font.FontSize        = 10;
            ws.Style.Font.FontColor       = XLColor.FromArgb(200, 220, 205);
            ws.Style.Fill.BackgroundColor = XLColor.FromArgb(13, 17, 23);
            ws.ShowGridLines              = false;
        }

        private static double Percentile(List<double> sorted, double pct)
        {
            if (sorted.Count == 0) return 0;
            double idx = (pct / 100.0) * (sorted.Count - 1);
            int lo     = (int)Math.Floor(idx);
            int hi     = Math.Min(lo + 1, sorted.Count - 1);
            return sorted[lo] + (idx - lo) * (sorted[hi] - sorted[lo]);
        }
    }
}