using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using LiveCharts;

namespace MESInsight.Core
{
    public class ResponseRecord
    {
        public string Timestamp { get; set; }
        public DateTime TimestampParsed { get; set; }
        public int ResponseTime { get; set; }
        public string FileName { get; set; }
        public MessageType Type { get; set; }
        public string Uid { get; set; }
        public string UidIn { get; set; }
        public string UidOut { get; set; }
        public string UidType { get; set; }
        public string Result { get; set; }
        public string CarrierId { get; set; }
        public string Material { get; set; }
        public string Setup { get; set; }
        public string UidAssy { get; set; }
        public string UidAssyType { get; set; }
        public string ProcDirAssy { get; set; }
    }

    public class ChartSeries
    {
        public string Name { get; set; }
        public SeriesCollection Series { get; set; }
        public string[] Labels { get; set; }
        public List<ChartBucket> Buckets { get; set; }
        public List<(long From, long To)> RemappedGaps { get; set; }
        public List<string> GapLabels { get; set; }
        public List<long> GapCenterAxisValues { get; set; }
        public Dictionary<long, DateTime> CompressedAxisValueToCalendarDate { get; set; }
        public bool IsWeeklyView { get; set; }
        public ChartSeries DailyVersion { get; set; }
    }

    public class ChartData
    {
        public List<ChartSeries> Charts { get; set; }
        public ChartSeries TrendChart { get; set; }
        public List<TimelineEvent> TimelineEvents { get; set; }
        public List<ResponseRecord> FilteredRecords { get; set; }
        public int MaxResponseTime { get; set; }
        public ChartSeries TrendChartDaily { get; set; }
    }

    public class ChartBucket
    {
        public int Index { get; set; }
        public int BucketMs { get; set; }
        public double Count { get; set; }
        public double DisplayCount { get; set; }
        public bool IsScaled { get; set; }
        public string Label { get; set; }
        public string BarLabel { get; set; }
        public int RangeStart { get; set; }
        public int RangeEnd { get; set; }
        public SolidColorBrush BarColor { get; set; }
    }

    public enum TimelineEventType
    {
        Production,
        ProductionFail,
        OeeStop,
        Error,
        MaterialChange,
        SetupChange,
        Idle
    }

    public class TimelineEvent
    {
        public DateTime Start { get; set; }
        public DateTime? End { get; set; }
        public TimelineEventType EventType { get; set; }
        public MessageType MessageKind { get; set; }
        public ResponseRecord SourceRecord { get; set; }
        public string Label { get; set; }
        public string Uid { get; set; }
        public string Detail { get; set; }
        public string ErrorCode { get; set; }
        public int ResponseTimeMs { get; set; }
        public List<ResponseRecord> GroupedRecords { get; set; }
        public bool IsGroup => GroupedRecords != null && GroupedRecords.Count > 1;
        public int GroupWidth => IsGroup ? GroupedRecords.Count : 1;
    }

    public enum ChartType
    {
        Trend,
        Histogram,
        Timeline
    }

    public enum MessageType
    {
        UNIT_INFO,
        NEXT_OPERATION,
        UNIT_CHECKIN,
        UNIT_RESULT,
        REQ_LOADED_MATERIAL,
        REQ_UNLOAD_MATERIAL,
        LOAD_MATERIAL,
        REQ_MATERIAL_INFO,
        REQ_SETUP_CHANGE2,
        SEMI_VALIDATION2,
        OTHER,
        ALL
    }

    public class UidIndex
    {
        private readonly Dictionary<string, List<int>> _index = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public void Build(List<ResponseRecord> records)
        {
            _index.Clear();
            _aliases.Clear();

            for (int i = 0; i < records.Count; i++)
            {
                var r = records[i];
                AddToIndex(r.Uid, i);
                AddToIndex(r.UidIn, i);
                AddToIndex(r.UidOut, i);
                AddToIndex(r.UidAssy, i);

                if (!string.IsNullOrEmpty(r.UidIn) && !string.IsNullOrEmpty(r.UidOut))
                    _aliases[r.UidOut] = r.UidIn;
            }
        }

        private void AddToIndex(string uid, int idx)
        {
            if (string.IsNullOrEmpty(uid)) return;
            if (!_index.TryGetValue(uid, out var list))
                _index[uid] = list = new List<int>();
            list.Add(idx);
        }

        public List<int> Find(string uid)
        {
            if (string.IsNullOrEmpty(uid)) return new List<int>();
            var result = new HashSet<int>();
            CollectAll(uid, result, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            return result.OrderBy(i => i).ToList();
        }

        private void CollectAll(string uid, HashSet<int> result, HashSet<string> visited)
        {
            if (string.IsNullOrEmpty(uid) || !visited.Add(uid)) return;
            if (_index.TryGetValue(uid, out var list))
                foreach (var i in list) result.Add(i);
            if (_aliases.TryGetValue(uid, out var prev))
                CollectAll(prev, result, visited);
        }

        public string GetAlias(string uid) =>
            _aliases.TryGetValue(uid, out var a) ? a : null;

        public bool HasUid(string uid) =>
            !string.IsNullOrEmpty(uid) && _index.ContainsKey(uid);
    }
}