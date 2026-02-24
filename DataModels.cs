using System.Collections.Generic;
using System.Windows.Media;
using LiveCharts;

namespace RTAnalyzer
{
    
    public class ResponseRecord
    {
        public string Timestamp { get; set; }
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
    }

    public class ChartSeries
    {
        public string Name { get; set; }
        public SeriesCollection Series { get; set; }
        public string[] Labels { get; set; }
    }

    public class ChartData
    {
        public List<ChartSeries> Charts { get; set; }
        public ChartSeries TrendChart { get; set; }
        public List<ResponseRecord> FilteredRecords { get; set; }
    }

    public class ChartBucket
    {
        public int Index { get; set; }
        public int BucketMs { get; set; }
        public double Count { get; set; }
        public double DisplayCount { get; set; }
        public bool IsScaled { get; set; }
        public string Label { get; set; }
        public int RangeStart { get; set; }
        public int RangeEnd   { get; set; }
        public SolidColorBrush BarColor { get; set; }
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
        OTHER
    }
}