using System;
using System.Collections.Generic;
using System.Linq;
using MESInsight.Charts.Interfaces;
using MESInsight.Core;

namespace MESInsight.Charts.Builders
{
    public class TimelineChart : IChartDataBuilder
    {
        public ChartType GetChartType() => ChartType.Timeline;
        public bool CanBuild(List<ResponseRecord> records) => records.Count > 0;

        public ChartData Build(ChartInputData input)
        {
            var records = input.Records
                .Where(r => r.TimestampParsed != DateTime.MinValue && r.Type != MessageType.OTHER)
                .OrderBy(r => r.TimestampParsed)
                .ToList();

            var events = new List<TimelineEvent>();

            foreach (var r in records)
            {
                bool isError = r.Result != null &&
                               (r.Result.StartsWith("[ERR") || r.Result == "ERROR");

                events.Add(new TimelineEvent
                {
                    Start          = r.TimestampParsed,
                    End            = null,
                    EventType      = isError ? TimelineEventType.Error : TimelineEventType.Production,
                    Label          = r.Type.ToString().Replace("_", " "),
                    Uid            = r.Uid ?? r.UidIn,
                    Detail         = BuildDetail(r),
                    ErrorCode      = isError ? r.Result : null,
                    ResponseTimeMs = r.ResponseTime,
                    SourceRecord   = r,
                    MessageKind    = r.Type
                });
            }

            int maxRt = records.Count > 0 ? records.Max(r => r.ResponseTime) : 1;

            return new ChartData
            {
                TimelineEvents  = events,
                FilteredRecords = records,
                MaxResponseTime = maxRt
            };
        }

        private static string BuildDetail(ResponseRecord r)
        {
            var parts = new List<string>();
            parts.Add(r.Type.ToString().Replace("_", " "));
            if (!string.IsNullOrEmpty(r.Uid ?? r.UidIn))
                parts.Add("UID: " + (r.Uid ?? r.UidIn));
            if (r.ResponseTime > 0)
                parts.Add("RT: " + r.ResponseTime + " ms");
            if (!string.IsNullOrEmpty(r.Result))
                parts.Add("Result: " + r.Result);
            if (!string.IsNullOrEmpty(r.Material))
                parts.Add("Material: " + r.Material);
            return string.Join("\n", parts);
        }
    }
}