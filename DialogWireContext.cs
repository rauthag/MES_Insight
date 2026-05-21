using System.Collections.Generic;
using System.Windows.Controls;
using RTAnalyzer.Core;

namespace MESInsight
{
    internal class DialogWireContext
    {
        public List<StationLoadEntry> AllEntries { get; set; }
        public Slider GlobalSlider { get; set; }
        public Dictionary<int, MonthFileInfo> FileCounts { get; set; }
        public Dictionary<string, Dictionary<int, MonthFileInfo>> PerStation { get; set; }
        public TextBlock ValueLabel { get; set; }
        public TextBlock SizeLabel { get; set; }
        public TextBlock WarningLabel { get; set; }
        public ProgressBar LoadBar { get; set; }
        public TextBlock TotalSizeLabel { get; set; }
        public TextBlock TotalWarningLabel { get; set; }
        public Button BtnLoad { get; set; }
        public CheckBox CbDateFilter { get; set; }
        public List<StationInfo> LcsStations { get; set; }
        public List<StationInfo> BackflushStations { get; set; }
        public List<StationInfo> ConnectorStations { get; set; }
        public int RecommendedMonths { get; set; }
        public int TotalCount { get; set; }
    }
}