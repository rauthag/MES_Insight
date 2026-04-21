using RTAnalyzer.Core;
using System;
using System.Collections.Generic;

namespace RTAnalyzer.Charts
{
    public class ChartInputData
    {
        public List<ResponseRecord>                        Records      { get; set; }
        public MessageType                                 MessageType  { get; set; }
        public double                                      Average      { get; set; }
        public double                                      StdDev       { get; set; }
        public int                                         P95          { get; set; }
        public int                                         P99          { get; set; }
        public Dictionary<DateTime, List<ResponseRecord>>  GroupedByDay { get; set; }
    }
}