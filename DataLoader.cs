using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace RTAnalyzer
{
    public class DataLoadResult
    {
        public List<ResponseRecord> Records { get; set; } = new List<ResponseRecord>();
        public string StationName { get; set; } = "";
    }

    public class DataLoader
    {
        public DataLoadResult Load(string path, Action<string, int> progressCallback = null)
        {
            var result = new DataLoadResult();
            if (!Directory.Exists(path)) return result;

            progressCallback?.Invoke("Scanning files...", 0);

            string[] files = Directory.GetFiles(path, "*.*", SearchOption.AllDirectories);
            int totalFiles = files.Length;
            int processedFiles = 0;

            foreach (string file in files)
            {
                processedFiles++;
                int fileProgress = (processedFiles * 100) / totalFiles;
                progressCallback?.Invoke($"Reading {Path.GetFileName(file)}...", fileProgress);

                string ext = Path.GetExtension(file).ToLower();

                if (ext == ".txt")
                {
                    using (Stream fs = File.OpenRead(file))
                        ReadLines(fs, Path.GetFileName(file), result);
                }
                else if (ext == ".zip")
                {
                    LoadFromZip(file, result);
                }
            }

            progressCallback?.Invoke("Processing message types...", 100);

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

        private static void LoadFromZip(string zipFile, DataLoadResult result)
        {
            try
            {
                using (ZipArchive zip = ZipFile.OpenRead(zipFile))
                {
                    foreach (ZipArchiveEntry entry in zip.Entries)
                    {
                        if (entry.Name.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
                        {
                            using (Stream stream = entry.Open())
                                ReadLines(stream, Path.GetFileName(zipFile) + " > " + entry.Name, result);
                        }
                    }
                }
            }
            catch
            {
                /* ZIP error handle */
            }
        }

        private static void ReadLines(Stream dataStream, string sourceName, DataLoadResult result)
        {
            using (var reader = new StreamReader(dataStream))
            {
                string plcMsgLine = null;
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    TryExtractStationName(line, result);

                    if (line.Contains("[C->S"))
                    {
                        plcMsgLine = line;
                        continue;
                    }

                    TryParseRecord(line, sourceName, result, plcMsgLine);
                    plcMsgLine = null;
                }
            }
        }

        private static string GetRequestKey(string line)
        {
            int stx = line.IndexOf("<STX>", StringComparison.Ordinal);
            if (stx < 0) return null;
            stx += 5;

            int firstComma = line.IndexOf(',', stx);
            if (firstComma < 0) return null;
            int secondComma = line.IndexOf(',', firstComma + 1);
            if (secondComma < 0) return null;
            int thirdComma = line.IndexOf(',', secondComma + 1);
            if (thirdComma < 0) return null;

            string msgType = line.Substring(stx, firstComma - stx).Trim();
            string seqNum = line.Substring(secondComma + 1, thirdComma - secondComma - 1).Trim();

            return msgType + "_" + seqNum;
        }

        private static void TryExtractStationName(string line, DataLoadResult result)
        {
            if (!string.IsNullOrEmpty(result.StationName)) return;
            if (!line.Contains("productline=\"")) return;

            int start = line.IndexOf("productline=\"", StringComparison.Ordinal) + 13;
            int end = line.IndexOf("\"", start, StringComparison.Ordinal);

            if (end > start)
                result.StationName = line.Substring(start, end - start).Replace("_", " ");
        }

        private static void TryParseRecord(string line, string sourceName, DataLoadResult result, string plcMsgLine)
        {
            if (!line.Contains("[S") || !line.Contains("->C]")) return;

            string[] columns = line.Split('\t');
            if (columns.Length < 2) return;

            string lastCol = columns[columns.Length - 1];
            if (!int.TryParse(lastCol, out int timeValue)) return;

            string mesMsgText = columns.Length >= 4 ? columns[3] : line;
            string plcMsgText = plcMsgLine ?? "";

            result.Records.Add(new ResponseRecord
            {
                Timestamp = columns[0],
                ResponseTime = timeValue,
                FileName = sourceName,
                Type = GetMessageType(line),
                Uid = ExtractAttribute(mesMsgText, "uid=") ?? ExtractAttribute(plcMsgText, "uid="),
                UidIn = ExtractAttribute(mesMsgText, "uid_in=") ?? ExtractAttribute(plcMsgText, "uid_in="),
                UidOut = ExtractAttribute(mesMsgText, "uid_out=") ?? ExtractAttribute(plcMsgText, "uid_out="),
                UidType = ExtractAttribute(mesMsgText, "uid_type=") ?? ExtractAttribute(plcMsgText, "uid_type="),
                Result = ExtractAttribute(mesMsgText, "result=") ?? ExtractAttribute(plcMsgText, "result="),
                CarrierId = ExtractAttribute(mesMsgText, "Carrier_ID_val=") ??
                            ExtractAttribute(plcMsgText, "Carrier_ID_val="),
                Material = ExtractAttribute(mesMsgText, "material=") ?? ExtractAttribute(plcMsgText, "material="),
                Setup = ExtractAttribute(mesMsgText, "setup=") ?? ExtractAttribute(plcMsgText, "setup=")
            });
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

        private static MessageType GetMessageType(string text)
        {
            var start = text.IndexOf("<STX>", StringComparison.Ordinal);
            if (start < 0) return MessageType.OTHER;
            start += 5;

            var end = text.IndexOf(",", start, StringComparison.Ordinal);
            if (end < 0) return MessageType.OTHER;

            string typeName = text.Substring(start, end - start).Trim();

            switch (typeName)
            {
                case "REQ_UNIT_INFO": return MessageType.UNIT_INFO;
                case "REQ_NEXT_OPERATION": return MessageType.NEXT_OPERATION;
                case "UNIT_CHECKIN": return MessageType.UNIT_CHECKIN;
                case "UNIT_RESULT": return MessageType.UNIT_RESULT;
                case "REQ_LOADED_MATERIAL": return MessageType.REQ_LOADED_MATERIAL;
                case "REQ_UNLOAD_MATERIAL": return MessageType.REQ_UNLOAD_MATERIAL;
                case "LOAD_MATERIAL": return MessageType.LOAD_MATERIAL;
                case "REQ_MATERIAL_INFO": return MessageType.REQ_MATERIAL_INFO;
                case "REQ_SETUP_CHANGE2": return MessageType.REQ_SETUP_CHANGE2;
                default: return MessageType.OTHER;
            }
        }

        private static MessageType IdentifyTypeByKeyword(string text)
        {
            // NO STX CHECK 
            if (text.Contains("REQ_UNIT_INFO")) return MessageType.UNIT_INFO;
            if (text.Contains("REQ_NEXT_OPERATION")) return MessageType.NEXT_OPERATION;
            if (text.Contains("UNIT_CHECKIN")) return MessageType.UNIT_CHECKIN;
            if (text.Contains("UNIT_RESULT")) return MessageType.UNIT_RESULT;
            if (text.Contains("REQ_LOADED_MATERIAL")) return MessageType.REQ_LOADED_MATERIAL;
            if (text.Contains("REQ_UNLOAD_MATERIAL")) return MessageType.REQ_UNLOAD_MATERIAL;
            if (text.Contains("LOAD_MATERIAL")) return MessageType.LOAD_MATERIAL;
            if (text.Contains("REQ_MATERIAL_INFO")) return MessageType.REQ_MATERIAL_INFO;
            if (text.Contains("REQ_SETUP_CHANGE2")) return MessageType.REQ_SETUP_CHANGE2;
            return MessageType.OTHER;
        }
    }
}