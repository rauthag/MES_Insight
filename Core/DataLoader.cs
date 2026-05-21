using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MESInsight.Core;

namespace RTAnalyzer.Core
{
    public enum StationCategory
    {
        GHP,
        LCS,
        Backflush,
        Connector,
        Unknown
    }

    public class StationInfo
    {
        public string FolderPath   { get; set; }
        public string StationName  { get; set; } = "";
        public string LineName     { get; set; } = "";
        public string ComputerName { get; set; } = "";
        public StationCategory Category { get; set; } = StationCategory.GHP;

        public string DisplayTitle =>
            !string.IsNullOrEmpty(LineName) && !string.IsNullOrEmpty(ComputerName)
                ? ComputerName + "  /  " + StationName
                : StationName;

        public string FullLabel =>
            !string.IsNullOrEmpty(LineName) ? LineName + "  ·  " + DisplayTitle : DisplayTitle;
    }

    public class DataLoadResult
    {
        public List<ResponseRecord> Records     { get; set; } = new List<ResponseRecord>();
        public string               StationName { get; set; } = "";
        public string               LineName    { get; set; } = "";
        public string               ComputerName { get; set; } = "";
    }

    public class MonthFileInfo
    {
        public int      FileCount { get; set; }
        public long     SizeBytes { get; set; }  
        public long     SizeMb    => SizeBytes / 1024 / 1024;  
        public DateTime MinDate   { get; set; } = DateTime.MaxValue;
        public DateTime MaxDate   { get; set; } = DateTime.MinValue;
    }


    public class DataLoader
    {
        public DateTime? DateFilter { get; set; } = null;

        #region Compiled Regex

        private static readonly Regex RxYyyyMmDd   = new Regex(@"(\d{4})(\d{2})(\d{2})",                           RegexOptions.Compiled);
        private static readonly Regex RxMmYyyy      = new Regex(@"^(\d{2})[_.](\d{4})",                             RegexOptions.Compiled);
        private static readonly Regex RxZipMonthYear = new Regex(@"^(\d{2})\.(\d{4})\.zip$",                        RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex RxTracerFile  = new Regex(@"^\d{8}_\d+_tracer",                               RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex RxMsgFile     = new Regex(@"^\d{8}_\d+_messages",                             RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex RxLineCode    = new Regex(@"^L\d{3}[^0-9]",                                   RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex RxLineCodeSmt = new Regex(@"^(?:SMT|THT|AOI|ICT|SMD|FCT|TRT|SPI)\d+",        RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex RxComputerCode = new Regex(@"^[A-Z]{2,4}\d{3,}[A-Z0-9]*$",                   RegexOptions.Compiled);
        private static readonly Regex RxMonCode     = new Regex(@"^MON\d+",                                         RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex RxLcsCode     = new Regex(@"^LCS\d+",                                         RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex RxMonFolder   = new Regex(@"(?:^|[_\s])MON\d+",                               RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex RxOvnFolder   = new Regex(@"(?:^|[_\s])OVN\d+",                               RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex RxOrFolder    = new Regex(@"^OR_[A-Z]{2,4}\d+",                               RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex RxRoutePrefix = new Regex(@"^(?:OR_|OP\d+_OR_)",                              RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex RxPlaceholder = new Regex(@"^[A-Z]+XXX",                                      RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex RxGenericLine = new Regex(@"^L\d{3}[A-Z0-9]*$",                               RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex RxLcsPath     = new Regex(@"[\\/]lcs",                                        RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex RxLcsName     = new Regex(@"(?:^|[ _-])LCS[0-9]+",                            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        #endregion

        #region Constants

        private static readonly string[] BlockedExtensions =
        {
            ".dll", ".exe", ".config", ".xml", ".json", ".db", ".ini",
            ".bat", ".ps1", ".msi", ".pdb", ".manifest", ".resx", ".cs", ".csproj", ".sln"
        };

        private static readonly string[] TimestampFormats =
        {
            "dd.MM.yyyy HH:mm:ss.ffff",
            "dd.MM.yyyy HH:mm:ss.fff",
            "dd.MM.yyyy HH:mm:ss",
            "dd/MM/yyyy HH:mm:ss.fff",
            "dd/MM/yyyy HH:mm:ss",
            "yyyy-MM-dd HH:mm:ss.fff",
            "yyyy-MM-dd HH:mm:ss",
            "yyyy-MM-ddTHH:mm:ss.fff",
            "yyyy-MM-ddTHH:mm:ss"
        };

        #endregion

        #region Public API

        public static Dictionary<int, MonthFileInfo> CountFilesByMonthCutoffs(string rootPath, int[] days)
        {
            Dictionary<int, MonthFileInfo> result = InitMonthResult(days);

            try
            {
                List<string> files = GetCountableFiles(rootPath);

                foreach (string file in files) AccumulateFileIntoMonthBuckets(file, days, result);
            }
            catch { }

            return result;
        }

        public static List<StationInfo> FindStations(string rootPath)
        {
            List<StationInfo> stations = new List<StationInfo>();

            if (!Directory.Exists(rootPath)) return stations;

            ScanForStations(rootPath, rootPath, stations, depth: 0);

            if (stations.Count == 0)
                stations.Add(BuildStationInfo(rootPath, rootPath));

            DeduplicateNames(stations);
            return stations;
        }

        public static void DeduplicateNames(List<StationInfo> stations)
        {
            foreach (IGrouping<string, StationInfo> group in stations.GroupBy(s => s.StationName).Where(g => g.Count() > 1))
            {
                int idx = 1;
                foreach (StationInfo st in group)
                    st.StationName = st.StationName + " " + idx++;
            }
        }

        public static bool IsGenericPlaceholderName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return true;
            string t = name.Trim();
            return RxPlaceholder.IsMatch(t) || RxGenericLine.IsMatch(t);
        }

        public DataLoadResult Load(string path, Action<string, int, string> progressCallback = null)
        {
            DataLoadResult result = new DataLoadResult();

            if (!Directory.Exists(path)) return result;

            StationInfo info = BuildStationInfo(path, path);
            result.LineName     = info.LineName;
            result.ComputerName = info.ComputerName;

            progressCallback?.Invoke("Scanning files...", 0, null);

            string[] files       = Directory.GetFiles(path, "*.*", SearchOption.AllDirectories);
            ConcurrentBag<ResponseRecord> bag = new ConcurrentBag<ResponseRecord>();
            string[] nameHolder  = new string[1];
            long[]   lastUiTick  = new long[1];
            int      processed   = 0;

            ParallelOptions opts = new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1) };

            Parallel.ForEach(files, opts, file =>
            {
                int    pct      = (Interlocked.Increment(ref processed) * 95) / Math.Max(1, files.Length);
                string fileName = Path.GetFileName(file);
                string ext      = Path.GetExtension(file).ToLower();

                DataLoadResult local = LoadSingleFile(file, fileName, ext, pct, lastUiTick, progressCallback);

                if (local == null) return;

                foreach (ResponseRecord r in local.Records)
                    bag.Add(r);

                TryUpdateStationNameHolder(nameHolder, local.StationName);
                ReportFileProgress(file, path, fileName, pct, lastUiTick, progressCallback);
            });

            result.Records     = bag.OrderBy(r => r.TimestampParsed).ToList();
            result.StationName = nameHolder[0] ?? "";

            progressCallback?.Invoke("Processing message types...", 100, result.StationName);

            return result;
        }

        #endregion

        #region File Discovery

        private static Dictionary<int, MonthFileInfo> InitMonthResult(int[] days)
        {
            Dictionary<int, MonthFileInfo> result = new Dictionary<int, MonthFileInfo>();

            foreach (int d in days)
                result[d] = new MonthFileInfo();

            return result;
        }

        private static List<string> GetCountableFiles(string rootPath)
        {
            return Directory.GetFiles(rootPath, "*", SearchOption.AllDirectories)
                .Where(f =>
                {
                    string ext = Path.GetExtension(f).ToLowerInvariant();
                    return ext == ".zip" || ext == ".txt" || ext == ".log" || ext == "";
                })
                .ToList();
        }

        private static void AccumulateFileIntoMonthBuckets(string file, int[] days, Dictionary<int, MonthFileInfo> result)
        {
            DateTime fileDate  = EstimateFileDate(file);
            long     fileBytes = new FileInfo(file).Length;

            foreach (int d in days)
            {
                if (fileDate >= DateTime.Now.AddDays(-d))
                {
                    result[d].FileCount++;
                    result[d].SizeBytes += fileBytes;
                    if (fileDate < result[d].MinDate) result[d].MinDate = fileDate;
                    if (fileDate > result[d].MaxDate) result[d].MaxDate = fileDate;
                }
            }
        }

        private static void ScanForStations(string rootPath, string currentPath, List<StationInfo> stations, int depth)
        {
            if (depth > 8) return;

            foreach (string dir in Directory.GetDirectories(currentPath))
            {
                string name = Path.GetFileName(dir);

                if (IsStationFolder(name, dir))
                    stations.Add(BuildStationInfo(rootPath, dir));
                else
                    ScanForStations(rootPath, dir, stations, depth + 1);
            }
        }

        private static bool IsStationFolder(string name, string dirPath)
        {
            return RxMonFolder.IsMatch(name)
                || RxOvnFolder.IsMatch(name)
                || RxOrFolder.IsMatch(name)
                || HasDirectLogFiles(dirPath);
        }

        private static bool HasDirectLogFiles(string dirPath)
        {
            try
            {
                foreach (string f in Directory.GetFiles(dirPath))
                {
                    string fname = Path.GetFileName(f);
                    string ext   = Path.GetExtension(fname).ToLowerInvariant();

                    if ((ext == ".txt" || ext == ".log" || ext == "" || ext == ".zip") && ShouldProcessFile(fname))
                        return true;
                }
            }
            catch { }

            return false;
        }

        #endregion

        #region Station Metadata

        private static StationInfo BuildStationInfo(string rootPath, string stationPath)
        {
            string   relative  = stationPath.Replace(rootPath, "").TrimStart('\\', '/');
            string[] parts     = relative.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
            string[] rootParts = rootPath.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
            string[] allParts  = rootParts.Concat(parts).ToArray();

            string          name     = ExtractStationNameFromFolderName(Path.GetFileName(stationPath));
            string          line     = ExtractLineName(allParts);
            string          computer = ExtractComputerName(allParts);
            StationCategory category = DetermineCategory(stationPath, name);

            return new StationInfo
            {
                FolderPath   = stationPath,
                StationName  = name,
                LineName     = line,
                ComputerName = computer,
                Category     = category
            };
        }

        private static string ExtractLineName(string[] parts)
        {
            foreach (string p in parts)
                if (RxLineCode.IsMatch(p) || RxLineCodeSmt.IsMatch(p))
                    return p;

            return "";
        }

        private static string ExtractComputerName(string[] parts)
        {
            foreach (string p in parts)
                if (RxComputerCode.IsMatch(p) && !RxMonCode.IsMatch(p) && !RxLcsCode.IsMatch(p)
                    && !RxLineCode.IsMatch(p) && !RxLineCodeSmt.IsMatch(p))
                    return p;

            return "";
        }

        private static StationCategory DetermineCategory(string stationPath, string stationName)
        {
            if (RxLcsPath.IsMatch(stationPath) || RxLcsName.IsMatch(stationName))
                return StationCategory.LCS;

            if (stationPath.ToLowerInvariant().Contains("backflush") ||
                stationName.ToLowerInvariant().Contains("backflush"))
                return StationCategory.Backflush;

            return StationCategory.GHP;
        }

        private static string ExtractStationNameFromFolderName(string folderName)
        {
            return RxRoutePrefix.Replace(folderName, "").Replace("_", " ").Trim();
        }
        
        public static Dictionary<string, Dictionary<int, MonthFileInfo>> CountFilesByStationAndDays(
            List<StationInfo> stations, int[] days)
        {
            Dictionary<string, Dictionary<int, MonthFileInfo>> result =
                new Dictionary<string, Dictionary<int, MonthFileInfo>>();

            foreach (StationInfo station in stations)
            {
                Dictionary<int, MonthFileInfo> stationResult = InitMonthResult(days);

                try
                {
                    List<string> files = GetCountableFiles(station.FolderPath);

                    foreach (string file in files) AccumulateFileIntoMonthBuckets(file, days, stationResult);
                }
                catch { }

                result[station.FolderPath] = stationResult;
            }

            return result;
        }

        private static void TryUpdateStationNameHolder(string[] holder, string candidate)
        {
            if (string.IsNullOrEmpty(candidate) || IsGenericPlaceholderName(candidate)) return;

            string current;
            do
            {
                current = Volatile.Read(ref holder[0]);
                if (!string.IsNullOrEmpty(current) && !IsGenericPlaceholderName(current)) return;
            }
            while (Interlocked.CompareExchange(ref holder[0], candidate, current) != current);
        }

        #endregion

        #region File Filtering

        private static bool ShouldProcessFile(string fileName)
        {
            string ext   = Path.GetExtension(fileName).ToLowerInvariant();
            string lower = fileName.ToLowerInvariant();

            foreach (string b in BlockedExtensions)
                if (ext == b) return false;

            if (IsBlockedFileName(fileName)) return false;

            return IsWhitelistedFileName(fileName, lower);
        }

        private static bool IsBlockedFileName(string fileName)
        {
            string[] blocked =
            {
                "FraMES", "frames", "GHPEquipmentConnector", "FujiConnector",
                "VitescoTHTAssemblyApi", "LineControlServer", "AOIService", "AOIViscom",
                "EquipmentConnector", "SMDAssemblyAPI", "OIBConnector", "LaserService",
                "VitescoComcell", "FRESH_Log", "FRESH Error", "FraMES Client Error",
                "Communications", "PLCmessages"
            };

            foreach (string b in blocked)
                if (fileName.StartsWith(b, StringComparison.OrdinalIgnoreCase))
                    return true;

            return false;
        }

        private static bool IsWhitelistedFileName(string fileName, string lower)
        {
            if (fileName.StartsWith("GHP", StringComparison.OrdinalIgnoreCase)) return true;
            if (fileName.StartsWith("VitescoAppMonitoringService", StringComparison.OrdinalIgnoreCase)) return true;
            if (RxMsgFile.IsMatch(fileName)) return true;
            if (RxTracerFile.IsMatch(fileName)) return true;
            if (lower == "messages.txt" || lower == "tracer.txt") return true;
            if (lower.Contains("message")) return true;
            if (fileName.StartsWith("Logging", StringComparison.OrdinalIgnoreCase)) return true;

            return false;
        }

        private static bool IsGhpLogFile(string fileName)
        {
            return fileName.StartsWith("GHP", StringComparison.OrdinalIgnoreCase)
                || fileName.StartsWith("VitescoAppMonitoringService", StringComparison.OrdinalIgnoreCase);
        }

        private static bool NeedsProbe(string fileName)
        {
            return fileName.StartsWith("Logging", StringComparison.OrdinalIgnoreCase);
        }

        private static bool ZipIsInDateRange(string zipFile, DateTime? cutoff)
        {
            if (cutoff == null) return true;

            Match m = RxZipMonthYear.Match(Path.GetFileName(zipFile));

            if (!m.Success) return true;
            if (!int.TryParse(m.Groups[1].Value, out int month)) return true;
            if (!int.TryParse(m.Groups[2].Value, out int year)) return true;

            return new DateTime(year, month, DateTime.DaysInMonth(year, month)) >= cutoff.Value;
        }

        #endregion

        #region File Probing

        private static bool ProbeFileForMesData(string filePath)
        {
            try
            {
                using (FileStream fs = File.OpenRead(filePath))
                {
                    long len = fs.Length;

                    if (len == 0) return false;
                    if (ProbeAt(fs, 0, 800)) return true;
                    if (len > 1600 && ProbeAt(fs, len / 2, 800)) return true;
                    if (len > 2400 && ProbeAt(fs, len - 800, 800)) return true;
                }
            }
            catch { }

            return false;
        }

        private static bool ProbeAt(FileStream fs, long offset, int size)
        {
            byte[] buf = new byte[size];
            fs.Seek(Math.Max(0, offset), SeekOrigin.Begin);
            int n = fs.Read(buf, 0, size);
            return ContainsMesSignature(System.Text.Encoding.UTF8.GetString(buf, 0, n));
        }

        private static bool ContainsMesSignature(string text)
        {
            return (text.Contains("[S") && text.Contains("->C]"))
                || text.Contains("VitescoComcell")
                || text.Contains("<STX>");
        }

        private static bool ProbeZipEntryForMesData(ZipArchiveEntry entry)
        {
            try
            {
                using (Stream stream = entry.Open())
                {
                    byte[] buf = new byte[800];
                    int    n   = stream.Read(buf, 0, buf.Length);
                    return ContainsMesSignature(System.Text.Encoding.UTF8.GetString(buf, 0, n));
                }
            }
            catch
            {
                return false;
            }
        }

        #endregion

        #region File Loading

        private DataLoadResult LoadSingleFile(string file, string fileName, string ext, int pct,
            long[] lastUiTick, Action<string, int, string> progressCallback)
        {
            if (ext == ".zip")
            {
                if (!ZipIsInDateRange(file, DateFilter)) return null;

                progressCallback?.Invoke($"Reading {fileName}", pct, "Opening ZIP archive...");

                DataLoadResult zipResult = new DataLoadResult();
                LoadFromZip(file, zipResult, DateFilter);
                return zipResult;
            }

            if (!ShouldProcessFile(fileName)) return null;
            if (NeedsProbe(fileName) && !ProbeFileForMesData(file)) return null;

            return LoadTextFile(file, fileName, ext, pct, lastUiTick, progressCallback);
        }

        private DataLoadResult LoadTextFile(string file, string fileName, string ext, int pct,
            long[] lastUiTick, Action<string, int, string> progressCallback)
        {
            DataLoadResult local = new DataLoadResult();

            if (ext == ".log" && IsGhpLogFile(fileName))
            {
                progressCallback?.Invoke($"Reading {fileName}", pct, "GHP format — scanning...");

                using (Stream fs = File.OpenRead(file))
                    ReadGhpFormatLines(fs, fileName, local, DateFilter,
                        MakeGhpProgressCallback(fileName, pct, lastUiTick, progressCallback));

                return local;
            }

            int before = local.Records.Count;

            using (Stream fs = File.OpenRead(file))
                ReadOldFormatLines(fs, fileName, local, DateFilter,
                    MakeOldFormatProgressCallback(fileName, pct, lastUiTick, progressCallback));

            if (local.Records.Count == before)
            {
                progressCallback?.Invoke($"Reading {fileName}", pct, "Trying GHP format...");

                using (Stream fs = File.OpenRead(file))
                    ReadGhpFormatLines(fs, fileName, local, DateFilter,
                        MakeGhpProgressCallback(fileName, pct, lastUiTick, progressCallback));
            }

            return local;
        }

        private static Action<int, int, int> MakeOldFormatProgressCallback(string fileName, int pct,
            long[] lastUiTick, Action<string, int, string> progressCallback)
        {
            return (lineNum, recCount, filePct) =>
            {
                long t  = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond;
                long pt = Interlocked.Read(ref lastUiTick[0]);

                if (t - pt >= 120 && Interlocked.CompareExchange(ref lastUiTick[0], t, pt) == pt)
                    progressCallback?.Invoke($"Reading {fileName}", pct,
                        $"Line {lineNum:N0}  ·  {recCount:N0} records  ·  {filePct}% of file");
            };
        }

        private static Action<int, int, int> MakeGhpProgressCallback(string fileName, int pct,
            long[] lastUiTick, Action<string, int, string> progressCallback)
        {
            return (ln, rc, fp) =>
            {
                long t  = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond;
                long pt = Interlocked.Read(ref lastUiTick[0]);

                if (t - pt >= 120 && Interlocked.CompareExchange(ref lastUiTick[0], t, pt) == pt)
                    progressCallback?.Invoke($"Reading {fileName}", pct,
                        $"GHP  ·  Line {ln:N0}  ·  {rc:N0} records  ·  {fp}% of file");
            };
        }

        private static void ReportFileProgress(string file, string path, string fileName, int pct,
            long[] lastUiTick, Action<string, int, string> progressCallback)
        {
            long now  = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond;
            long prev = Interlocked.Read(ref lastUiTick[0]);

            if (now - prev >= 80 && Interlocked.CompareExchange(ref lastUiTick[0], now, prev) == prev)
            {
                string sub = Path.GetDirectoryName(file)?.Replace(path, "").TrimStart('\\', '/');
                progressCallback?.Invoke($"Reading {fileName}", pct, string.IsNullOrEmpty(sub) ? "/" : sub);
            }
        }

        private static void LoadFromZip(string zipFile, DataLoadResult result, DateTime? cutoff = null)
        {
            try
            {
                using (ZipArchive zip = ZipFile.OpenRead(zipFile))
                    foreach (ZipArchiveEntry entry in zip.Entries)
                        TryLoadZipEntry(entry, Path.GetFileName(zipFile), result, cutoff);
            }
            catch { }
        }

        private static void TryLoadZipEntry(ZipArchiveEntry entry, string zipName, DataLoadResult result,
            DateTime? cutoff = null)
        {
            string entryName  = entry.Name;
            string sourceName = zipName + " > " + entryName;
            string ext        = Path.GetExtension(entryName).ToLowerInvariant();

            if (!ShouldProcessFile(entryName)) return;

            if (ext == ".log" && IsGhpLogFile(entryName))
            {
                using (Stream stream = entry.Open())
                    ReadGhpFormatLines(stream, sourceName, result, cutoff);

                return;
            }

            if (ext != ".txt" && ext != "" && ext != ".log") return;
            if (NeedsProbe(entryName) && !ProbeZipEntryForMesData(entry)) return;

            DataLoadResult temp = new DataLoadResult();

            using (Stream stream = entry.Open())
                ReadOldFormatLines(stream, sourceName, temp, cutoff);

            if (temp.Records.Count == 0)
                using (Stream stream = entry.Open())
                    ReadGhpFormatLines(stream, sourceName, temp, cutoff);

            foreach (ResponseRecord r in temp.Records)
                result.Records.Add(r);

            MergeStationName(result, temp.StationName);
        }

        private static void MergeStationName(DataLoadResult target, string candidate)
        {
            if (!string.IsNullOrEmpty(candidate) && !IsGenericPlaceholderName(candidate) &&
                (string.IsNullOrEmpty(target.StationName) || IsGenericPlaceholderName(target.StationName)))
                target.StationName = candidate;
        }

        #endregion

        #region Old Format Parsing

        private static void ReadOldFormatLines(Stream dataStream, string sourceName, DataLoadResult result,
            DateTime? cutoff = null, Action<int, int, int> lineProgress = null)
        {
            long   totalBytes = dataStream.CanSeek ? dataStream.Length : 0;
            long   readBytes  = 0;
            int    lineNum    = 0;
            int    recCount   = 0;
            string plcLine    = null;

            using (StreamReader reader = new StreamReader(dataStream))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    lineNum++;
                    readBytes += line.Length + 2;

                    TryExtractStationName(line, result);

                    if (line.Contains("[C->S"))
                    {
                        plcLine = line;
                        continue;
                    }

                    int before = result.Records.Count;
                    TryParseOldRecord(line, sourceName, result, plcLine, cutoff);
                    if (result.Records.Count > before) recCount++;
                    plcLine = null;

                    if (lineNum % 2000 == 0)
                        lineProgress?.Invoke(lineNum, recCount, ProgressPct(readBytes, totalBytes));
                }
            }
        }

        private static void TryExtractStationName(string line, DataLoadResult result)
        {
            if (!string.IsNullOrEmpty(result.StationName) && !IsGenericPlaceholderName(result.StationName)) return;
            if (!line.Contains("productline=\"")) return;

            string extracted = ExtractQuotedValue(line, "productline=\"");

            if (extracted != null && !IsGenericPlaceholderName(extracted))
                result.StationName = extracted.Replace("_", " ").Trim();
        }

        private static void TryParseOldRecord(string line, string sourceName, DataLoadResult result,
            string plcLine, DateTime? cutoff)
        {
            if (!line.Contains("[S") || !line.Contains("->C]")) return;

            string[] cols = line.Split('\t');
            if (cols.Length < 2) return;
            if (!int.TryParse(cols[cols.Length - 1], out int rt)) return;

            string mes = cols.Length >= 4 ? cols[3] : line;
            string plc = plcLine ?? "";

            TryParseTimestampFlexible(cols[0], out DateTime ts);
            if (cutoff.HasValue && ts != DateTime.MinValue && ts < cutoff.Value) return;

            result.Records.Add(BuildOldRecord(cols[0], ts, rt, sourceName, mes, plc));
        }

        private static ResponseRecord BuildOldRecord(string rawTs, DateTime ts, int rt,
            string source, string mes, string plc)
        {
            return new ResponseRecord
            {
                Timestamp       = rawTs,
                TimestampParsed = ts,
                ResponseTime    = rt,
                FileName        = source,
                Type            = ParseMessageType(mes),
                Uid             = Attr(mes, "uid=")             ?? Attr(plc, "uid="),
                UidIn           = Attr(mes, "uid_in=")          ?? Attr(plc, "uid_in="),
                UidOut          = Attr(mes, "uid_out=")         ?? Attr(plc, "uid_out="),
                UidType         = Attr(mes, "uid_type=")        ?? Attr(plc, "uid_type="),
                Result          = Attr(mes, "result=")          ?? Attr(plc, "result="),
                CarrierId       = Attr(mes, "Carrier_ID_val=")  ?? Attr(plc, "Carrier_ID_val="),
                Material        = Attr(mes, "material=")        ?? Attr(plc, "material="),
                Setup           = Attr(mes, "setup=")           ?? Attr(plc, "setup=")
            };
        }

        #endregion

        #region GHP Format Parsing

        private static void ReadGhpFormatLines(Stream dataStream, string sourceName, DataLoadResult result,
            DateTime? cutoff = null, Action<int, int, int> lineProgress = null)
        {
            Dictionary<string, string> pendingRequests = new Dictionary<string, string>();
            long totalBytes = dataStream.CanSeek ? dataStream.Length : 0;
            long readBytes  = 0;
            int  lineNum    = 0;
            int  recCount   = 0;

            using (StreamReader reader = new StreamReader(dataStream))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    lineNum++;
                    readBytes += line.Length + 2;

                    if (line.Contains("productline=\"") && string.IsNullOrEmpty(result.StationName))
                    {
                        int ps = line.IndexOf("productline=\"", StringComparison.Ordinal) + 13;
                        int pe = line.IndexOf("\"", ps, StringComparison.Ordinal);
                        if (pe > ps)
                            result.StationName = line.Substring(ps, pe - ps).Replace("_", " ");
                    }

                    bool isRequest  = line.Contains("=>[VitescoComcell]");
                    bool isResponse = line.Contains("<=[VitescoComcell]");

                    if (!isRequest && !isResponse) continue;

                    int stxPos = line.IndexOf('\x02');
                    int etxPos = line.LastIndexOf('\x03');

                    if (stxPos < 0 || etxPos <= stxPos) continue;

                    string body    = line.Substring(stxPos + 1, etxPos - stxPos - 1);
                    string pairKey = ExtractGhpPairKey(body);

                    if (pairKey == null) continue;

                    if (isRequest)
                    {
                        pendingRequests[pairKey] = body;
                        continue;
                    }

                    string afterEtx = line.Substring(etxPos + 1).TrimStart(',').Trim();
                    if (!int.TryParse(afterEtx, out int responseTime)) continue;

                    string timestampRaw        = line.Length >= 23 ? line.Substring(0, 23) : "";
                    string timestampNormalized = timestampRaw.Replace(',', '.');
                    DateTimeHelper.TryParseTimestamp(timestampNormalized, out DateTime parsedTimestamp);

                    if (cutoff.HasValue && parsedTimestamp != DateTime.MinValue && parsedTimestamp < cutoff.Value)
                        continue;

                    pendingRequests.TryGetValue(pairKey, out string reqBody);
                    string mergedBody = (reqBody ?? "") + " " + body;

                    result.Records.Add(new ResponseRecord
                    {
                        Timestamp       = timestampRaw,
                        TimestampParsed = parsedTimestamp,
                        ResponseTime    = responseTime,
                        FileName        = sourceName,
                        Type            = ParseGhpMessageType(body),
                        Uid             = ExtractAttribute(mergedBody, "uid="),
                        UidIn           = ExtractAttribute(mergedBody, "uid_in="),
                        UidOut          = ExtractAttribute(mergedBody, "uid_out="),
                        UidType         = ExtractAttribute(mergedBody, "uid_type="),
                        Result          = ExtractAttribute(mergedBody, "result="),
                        CarrierId       = ExtractAttribute(mergedBody, "Carrier_ID_val="),
                        Material        = ExtractAttribute(mergedBody, "material="),
                        Setup           = ExtractAttribute(mergedBody, "setup=")
                    });

                    pendingRequests.Remove(pairKey);
                    recCount++;

                    if (lineNum % 1000 == 0)
                    {
                        int pct = totalBytes > 0 ? (int)(readBytes * 100 / totalBytes) : 0;
                        lineProgress?.Invoke(lineNum, recCount, pct);
                    }
                }
            }
        }

        private static string ExtractGhpPairKey(string body)
        {
            int a = body.IndexOf(',');
            if (a < 0) return null;
            int b = body.IndexOf(',', a + 1);
            if (b < 0) return null;
            int c = body.IndexOf(',', b + 1);
            if (c < 0) return null;
            return body.Substring(0, c);
        }

        #endregion

        #region Message Type Parsing

        private static MessageType ParseMessageType(string text)
        {
            int start = text.IndexOf("<STX>", StringComparison.Ordinal);
            if (start < 0) return MessageType.OTHER;
            start += 5;
            int end = text.IndexOf(",", start, StringComparison.Ordinal);
            if (end < 0) return MessageType.OTHER;
            return MapMessageTypeName(text.Substring(start, end - start).Trim());
        }

        private static MessageType ParseGhpMessageType(string body)
        {
            int comma = body.IndexOf(',');
            return MapMessageTypeName(comma > 0 ? body.Substring(0, comma).Trim() : body.Trim());
        }

        private static MessageType MapMessageTypeName(string name)
        {
            switch (name)
            {
                case "REQ_UNIT_INFO":       return MessageType.UNIT_INFO;
                case "REQ_NEXT_OPERATION":  return MessageType.NEXT_OPERATION;
                case "UNIT_CHECKIN":        return MessageType.UNIT_CHECKIN;
                case "UNIT_RESULT":         return MessageType.UNIT_RESULT;
                case "REQ_LOADED_MATERIAL": return MessageType.REQ_LOADED_MATERIAL;
                case "REQ_UNLOAD_MATERIAL": return MessageType.REQ_UNLOAD_MATERIAL;
                case "LOAD_MATERIAL":       return MessageType.LOAD_MATERIAL;
                case "REQ_MATERIAL_INFO":   return MessageType.REQ_MATERIAL_INFO;
                case "REQ_SETUP_CHANGE2":   return MessageType.REQ_SETUP_CHANGE2;
                default:                    return MessageType.OTHER;
            }
        }

        #endregion

        #region Timestamp Parsing

        private static bool TryParseTimestampFlexible(string raw, out DateTime result)
        {
            result = DateTime.MinValue;
            if (string.IsNullOrWhiteSpace(raw)) return false;

            string s = (raw.Length > 26 ? raw.Substring(0, 26) : raw).Replace(',', '.');

            foreach (string fmt in TimestampFormats)
                if (DateTime.TryParseExact(s, fmt,
                        System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.None, out result))
                    return true;

            return false;
        }

        private static DateTime EstimateFileDate(string filePath)
        {
            string fileName = Path.GetFileName(filePath);

            Match nm = RxYyyyMmDd.Match(fileName);
            if (nm.Success &&
                int.TryParse(nm.Groups[1].Value, out int y)  &&
                int.TryParse(nm.Groups[2].Value, out int mo) &&
                int.TryParse(nm.Groups[3].Value, out int d)  &&
                mo >= 1 && mo <= 12 && d >= 1 && d <= 31)
                return new DateTime(y, mo, d);

            Match mm = RxMmYyyy.Match(fileName);
            if (mm.Success &&
                int.TryParse(mm.Groups[1].Value, out int mo2) &&
                int.TryParse(mm.Groups[2].Value, out int y2)  &&
                mo2 >= 1 && mo2 <= 12)
                return new DateTime(y2, mo2, 1);

            try
            {
                if (Path.GetExtension(fileName).ToLowerInvariant() == ".zip")
                    return File.GetLastWriteTime(filePath);

                using (StreamReader reader = new StreamReader(filePath))
                {
                    string first = reader.ReadLine();
                    if (first != null && TryParseTimestampFlexible(first, out DateTime parsed))
                        return parsed;
                }
            }
            catch { }

            return File.GetLastWriteTime(filePath);
        }

        #endregion

        #region Attribute Extraction

        private static string Attr(string text, string key)
        {
            int start = text.IndexOf(key, StringComparison.Ordinal);
            if (start < 0) return null;
            start += key.Length;
            if (start >= text.Length) return null;

            if (text[start] == '"')
            {
                start++;
                int end = text.IndexOf('"', start);
                return end < 0 ? null : text.Substring(start, end - start);
            }

            int endSpace = text.IndexOf(' ', start);
            return text.Substring(start, (endSpace < 0 ? text.Length : endSpace) - start);
        }

        private static string ExtractAttribute(string text, string key)
        {
            return Attr(text, key);
        }

        private static string ExtractQuotedValue(string line, string key)
        {
            int start = line.IndexOf(key, StringComparison.Ordinal);
            if (start < 0) return null;
            start += key.Length;
            int end = line.IndexOf('"', start);
            return end <= start ? null : line.Substring(start, end - start);
        }

        #endregion

        #region Utilities

        private static int ProgressPct(long read, long total)
        {
            return total > 0 ? (int)(read * 100 / total) : 0;
        }

        #endregion
    }
}