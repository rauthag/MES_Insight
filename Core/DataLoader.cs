using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RTAnalyzer.Core
{
    public class StationInfo
    {
        public string FolderPath   { get; set; }
        public string StationName  { get; set; } = "";
        public string LineName     { get; set; } = "";
        public string ComputerName { get; set; } = "";

        public string DisplayTitle =>
            !string.IsNullOrEmpty(LineName) && !string.IsNullOrEmpty(ComputerName)
                ? ComputerName + "  /  " + StationName
                : StationName;

        public string FullLabel =>
            !string.IsNullOrEmpty(LineName)
                ? LineName + "  ·  " + DisplayTitle
                : DisplayTitle;
    }

    public class DataLoadResult
    {
        public List<ResponseRecord> Records { get; set; } = new List<ResponseRecord>();
        public string StationName  { get; set; } = "";
        public string LineName     { get; set; } = "";
        public string ComputerName { get; set; } = "";
    }

    public class DataLoader
    {
        public static List<StationInfo> FindStations(string rootPath)
        {
            var stations = new List<StationInfo>();
            if (!Directory.Exists(rootPath)) return stations;

            ScanForStations(rootPath, rootPath, stations, depth: 0);

            if (stations.Count == 0)
                stations.Add(BuildStationInfo(rootPath, rootPath));

            return stations;
        }

        private static void ScanForStations(string rootPath, string currentPath, List<StationInfo> stations, int depth)
        {
            if (depth > 6) return;

            foreach (string dir in Directory.GetDirectories(currentPath))
            {
                string name = Path.GetFileName(dir);
                if (System.Text.RegularExpressions.Regex.IsMatch(name, @"^MON\d+", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                {
                    stations.Add(BuildStationInfo(rootPath, dir));
                }
                else
                {
                    ScanForStations(rootPath, dir, stations, depth + 1);
                }
            }
        }

        private static StationInfo BuildStationInfo(string rootPath, string stationPath)
        {
            string relativePath = stationPath.Replace(rootPath, "").TrimStart('\\', '/');
            string[] parts      = relativePath.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);

            string stationName  = Path.GetFileName(stationPath).Replace("_", " ");
            string lineName     = "";
            string computerName = "";

            foreach (string part in parts)
            {
                if (System.Text.RegularExpressions.Regex.IsMatch(part, @"^L\d{3}"))
                    lineName = part;
                else if (System.Text.RegularExpressions.Regex.IsMatch(part, @"^[A-Z]{2,4}\d{3,}[A-Z0-9]*$") &&
                         !System.Text.RegularExpressions.Regex.IsMatch(part, @"^MON\d+", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                    computerName = part;
            }

            if (string.IsNullOrEmpty(stationName) || stationName == relativePath)
                stationName = Path.GetFileName(stationPath).Replace("_", " ");

            return new StationInfo
            {
                FolderPath   = stationPath,
                StationName  = stationName,
                LineName     = lineName,
                ComputerName = computerName
            };
        }

        public DataLoadResult Load(string path, Action<string, int, string> progressCallback = null)
        {
            var result = new DataLoadResult();
            if (!Directory.Exists(path)) return result;

            var stationInfo = BuildStationInfo(path, path);
            result.LineName     = stationInfo.LineName;
            result.ComputerName = stationInfo.ComputerName;

            progressCallback?.Invoke("Scanning files...", 0, null);

            string[] files = Directory.GetFiles(path, "*.*", SearchOption.AllDirectories);
            int totalFiles     = files.Length;
            int processedFiles = 0;

            var bag               = new ConcurrentBag<ResponseRecord>();
            var stationNameHolder = new string[1];
            var options           = new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1) };
            long lastUiTick       = 0;

            Parallel.ForEach(files, options, file =>
            {
                int current = Interlocked.Increment(ref processedFiles);
                int pct     = (current * 95) / Math.Max(1, totalFiles);

                string ext      = Path.GetExtension(file).ToLower();
                string fileName = Path.GetFileName(file);

                var localResult = new DataLoadResult();

                if (ext == ".txt")
                {
                    using (Stream fs = File.OpenRead(file))
                        ReadOldFormatLines(fs, fileName, localResult);
                }
                else if (ext == ".log" && IsGhpLogFile(fileName))
                {
                    using (Stream fs = File.OpenRead(file))
                        ReadGhpFormatLines(fs, fileName, localResult);
                }
                else if (ext == ".zip")
                {
                    LoadFromZip(file, localResult);
                }

                foreach (var r in localResult.Records)
                    bag.Add(r);

                if (!string.IsNullOrEmpty(localResult.StationName))
                    Interlocked.CompareExchange(ref stationNameHolder[0], localResult.StationName, null);

                long now  = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond;
                long prev = Interlocked.Read(ref lastUiTick);
                if (now - prev >= 80 && Interlocked.CompareExchange(ref lastUiTick, now, prev) == prev)
                {
                    string subFolder = Path.GetDirectoryName(file)?.Replace(path, "").TrimStart('\\', '/');
                    string location  = string.IsNullOrEmpty(subFolder) ? "/" : subFolder;
                    progressCallback?.Invoke($"Reading {fileName}...", pct, location);
                }
            });

            result.Records     = bag.OrderBy(r => r.TimestampParsed).ToList();
            result.StationName = stationNameHolder[0] ?? "";

            progressCallback?.Invoke("Processing message types...", 100, result.StationName);

            var byStx = new Dictionary<string, int>();
            foreach (var r in result.Records)
            {
                string stxType = r.Type.ToString();
                if (!byStx.ContainsKey(stxType)) byStx[stxType] = 0;
                byStx[stxType]++;
            }

            System.Diagnostics.Debug.WriteLine("=== IdentifyType (STX) ===");
            foreach (var kv in byStx.OrderByDescending(x => x.Value))
                System.Diagnostics.Debug.WriteLine($"  {kv.Key}: {kv.Value}");
            System.Diagnostics.Debug.WriteLine($"  TOTAL: {result.Records.Count}");

            return result;
        }

        private static bool IsGhpLogFile(string fileName)
        {
            return fileName.StartsWith("GHP", StringComparison.OrdinalIgnoreCase);
        }

        private static void LoadFromZip(string zipFile, DataLoadResult result)
        {
            try
            {
                using (ZipArchive zip = ZipFile.OpenRead(zipFile))
                {
                    foreach (ZipArchiveEntry entry in zip.Entries)
                    {
                        string entryName = entry.Name;
                        string sourceName = Path.GetFileName(zipFile) + " > " + entryName;

                        if (entryName.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
                        {
                            using (Stream stream = entry.Open())
                                ReadOldFormatLines(stream, sourceName, result);
                        }
                        else if (entryName.EndsWith(".log", StringComparison.OrdinalIgnoreCase)
                                 && IsGhpLogFile(entryName))
                        {
                            using (Stream stream = entry.Open())
                                ReadGhpFormatLines(stream, sourceName, result);
                        }
                    }
                }
            }
            catch { }
        }

        // ── Old format: tab-separated, [S->C] / [C->S] markers ──────────────

        private static void ReadOldFormatLines(Stream dataStream, string sourceName, DataLoadResult result)
        {
            using (var reader = new StreamReader(dataStream))
            {
                string plcMsgLine = null;
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    TryExtractStationNameOldFormat(line, result);

                    if (line.Contains("[C->S"))
                    {
                        plcMsgLine = line;
                        continue;
                    }

                    TryParseOldFormatRecord(line, sourceName, result, plcMsgLine);
                    plcMsgLine = null;
                }
            }
        }

        private static void TryExtractStationNameOldFormat(string line, DataLoadResult result)
        {
            if (!string.IsNullOrEmpty(result.StationName)) return;
            if (!line.Contains("productline=\"")) return;

            int start = line.IndexOf("productline=\"", StringComparison.Ordinal) + 13;
            int end   = line.IndexOf("\"", start, StringComparison.Ordinal);

            if (end > start)
                result.StationName = line.Substring(start, end - start).Replace("_", " ");
        }

        private static void TryParseOldFormatRecord(string line, string sourceName, DataLoadResult result, string plcMsgLine)
        {
            if (!line.Contains("[S") || !line.Contains("->C]")) return;

            string[] columns = line.Split('\t');
            if (columns.Length < 2) return;

            string lastCol = columns[columns.Length - 1];
            if (!int.TryParse(lastCol, out int timeValue)) return;

            string mesMsgText = columns.Length >= 4 ? columns[3] : line;
            string plcMsgText = plcMsgLine ?? "";

            DateTimeHelper.TryParseTimestamp(columns[0], out DateTime parsedTimestamp);

            result.Records.Add(new ResponseRecord
            {
                Timestamp       = columns[0],
                TimestampParsed = parsedTimestamp,
                ResponseTime    = timeValue,
                FileName        = sourceName,
                Type            = ParseMessageType(line),
                Uid             = ExtractAttribute(mesMsgText, "uid=")            ?? ExtractAttribute(plcMsgText, "uid="),
                UidIn           = ExtractAttribute(mesMsgText, "uid_in=")         ?? ExtractAttribute(plcMsgText, "uid_in="),
                UidOut          = ExtractAttribute(mesMsgText, "uid_out=")        ?? ExtractAttribute(plcMsgText, "uid_out="),
                UidType         = ExtractAttribute(mesMsgText, "uid_type=")       ?? ExtractAttribute(plcMsgText, "uid_type="),
                Result          = ExtractAttribute(mesMsgText, "result=")         ?? ExtractAttribute(plcMsgText, "result="),
                CarrierId       = ExtractAttribute(mesMsgText, "Carrier_ID_val=") ?? ExtractAttribute(plcMsgText, "Carrier_ID_val="),
                Material        = ExtractAttribute(mesMsgText, "material=")       ?? ExtractAttribute(plcMsgText, "material="),
                Setup           = ExtractAttribute(mesMsgText, "setup=")          ?? ExtractAttribute(plcMsgText, "setup=")
            });
        }

        // ── GHP Robust format: GHPNetty logger, <=[VitescoComcell] responses ─

        private static void ReadGhpFormatLines(Stream dataStream, string sourceName, DataLoadResult result)
        {
            // GHP format pairs => (request, has uid/result) with <= (response, has response time)
            // Pairing key: "TYPE,STATION,SEQNUM" e.g. "UNIT_CHECKIN,OR_MON0250,5"
            var pendingRequests = new Dictionary<string, string>();

            using (var reader = new StreamReader(dataStream))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
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

                    string body = line.Substring(stxPos + 1, etxPos - stxPos - 1);
                    string pairKey = ExtractGhpPairKey(body);
                    if (pairKey == null) continue;

                    if (isRequest)
                    {
                        pendingRequests[pairKey] = body;
                        continue;
                    }

                    // Response line — extract response time
                    string afterEtx = line.Substring(etxPos + 1).TrimStart(',').Trim();
                    if (!int.TryParse(afterEtx, out int responseTime)) continue;

                    string timestampRaw        = line.Length >= 23 ? line.Substring(0, 23) : "";
                    string timestampNormalized  = timestampRaw.Replace(',', '.');
                    DateTimeHelper.TryParseTimestamp(timestampNormalized, out DateTime parsedTimestamp);

                    // Merge request body (has uid, result) with response body (has material from ACK)
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
                }
            }
        }

        private static string ExtractGhpPairKey(string body)
        {
            // body = "UNIT_CHECKIN,OR_MON0250,5,20260407..." → key = "UNIT_CHECKIN,OR_MON0250,5"
            int first  = body.IndexOf(',');
            if (first < 0) return null;
            int second = body.IndexOf(',', first + 1);
            if (second < 0) return null;
            int third  = body.IndexOf(',', second + 1);
            if (third < 0) return null;
            return body.Substring(0, third);
        }

        private static MessageType ParseGhpMessageType(string msgBody)
        {
            int commaPos = msgBody.IndexOf(',');
            string typeName = commaPos > 0 ? msgBody.Substring(0, commaPos).Trim() : msgBody.Trim();
            return MapMessageTypeName(typeName);
        }

        // ── Shared helpers ───────────────────────────────────────────────────

        private static MessageType ParseMessageType(string text)
        {
            var start = text.IndexOf("<STX>", StringComparison.Ordinal);
            if (start < 0) return MessageType.OTHER;
            start += 5;

            var end = text.IndexOf(",", start, StringComparison.Ordinal);
            if (end < 0) return MessageType.OTHER;

            return MapMessageTypeName(text.Substring(start, end - start).Trim());
        }

        private static MessageType MapMessageTypeName(string typeName)
        {
            switch (typeName)
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

        private static string ExtractAttribute(string text, string key)
        {
            int start = text.IndexOf(key, StringComparison.Ordinal);
            if (start < 0) return null;

            start += key.Length;
            if (start >= text.Length) return null;

            if (text[start] == '"')
            {
                start++;
                int end = text.IndexOf('"', start);
                if (end < 0) return null;
                return text.Substring(start, end - start);
            }

            int endSpace = text.IndexOf(' ', start);
            if (endSpace < 0) endSpace = text.Length;
            return text.Substring(start, endSpace - start);
        }
    }
}