using System.Windows.Controls;
using RTAnalyzer.Core;

namespace MESInsight
{
    internal class StationLoadEntry
    {
        public StationInfo Station { get; set; }
        public CheckBox EnabledBox { get; set; }
        public TextBlock NameLabel { get; set; }
    }
}