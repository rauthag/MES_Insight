using System;
using System.Collections.Generic;
using System.Linq;
using MESInsight.Charts.Interfaces;
using MESInsight.Core;

namespace MESInsight.Charts.Builders
{
    public class TimelineChart : IChartDataBuilder
    {
        private static readonly int MinGapMinutesForIdleBlock = 5;

        public ChartType GetChartType() => ChartType.Timeline;

        public bool CanBuild(List<ResponseRecord> records) => records.Count > 0;

        public ChartData Build(ChartInputData input)
        {
            var filteredRecords = input.Records
                .OrderBy(r => r.TimestampParsed)
                .ToList();

            var events = new List<TimelineEvent>();

            var checkinRecords  = filteredRecords.Where(r => r.Type == MessageType.UNIT_CHECKIN).ToList();
            var resultRecords   = filteredRecords.Where(r => r.Type == MessageType.UNIT_RESULT).ToList();
            var materialRecords = filteredRecords.Where(r => r.Type == MessageType.LOAD_MATERIAL).ToList();
            var setupRecords    = filteredRecords.Where(r => r.Type == MessageType.REQ_SETUP_CHANGE2).ToList();

            AddProductionCycleEvents(events, checkinRecords, resultRecords);
            AddMaterialChangeEvents(events, materialRecords);
            AddSetupChangeEvents(events, setupRecords);
            AddIdleGapEvents(events, filteredRecords);

            return new ChartData
            {
                TimelineEvents  = events.OrderBy(e => e.Start).ToList(),
                FilteredRecords = filteredRecords
            };
        }

        private static void AddProductionCycleEvents(
            List<TimelineEvent> events,
            List<ResponseRecord> checkins,
            List<ResponseRecord> results)
        {
            foreach (var checkin in checkins)
            {
                var matchingResult = results.FirstOrDefault(r =>
                    r.Uid == checkin.Uid &&
                    r.TimestampParsed >= checkin.TimestampParsed);

                bool isError = checkin.Result != null &&
                               (checkin.Result.StartsWith("ERR") || checkin.Result == "F");
                bool isFail  = matchingResult?.Result == "F";

                var eventType = isError ? TimelineEventType.Error :
                                isFail  ? TimelineEventType.ProductionFail :
                                          TimelineEventType.Production;

                events.Add(new TimelineEvent
                {
                    Start          = checkin.TimestampParsed,
                    End            = matchingResult?.TimestampParsed,
                    EventType      = eventType,
                    Label          = isFail ? "FAIL" : isError ? (checkin.Result ?? "ERR") : "OK",
                    Uid            = checkin.Uid ?? checkin.UidIn,
                    Detail         = BuildProductionDetail(checkin, matchingResult),
                    ErrorCode      = isError ? checkin.Result : null,
                    ResponseTimeMs = checkin.ResponseTime
                });
            }
        }

        private static string BuildProductionDetail(ResponseRecord checkin, ResponseRecord result)
        {
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(checkin.Uid ?? checkin.UidIn))
                parts.Add("UID: " + (checkin.Uid ?? checkin.UidIn));
            if (!string.IsNullOrEmpty(checkin.CarrierId))
                parts.Add("Carrier: " + checkin.CarrierId);
            if (!string.IsNullOrEmpty(checkin.Material))
                parts.Add("Material: " + checkin.Material);
            if (result != null)
                parts.Add("Result: " + (result.Result ?? "?"));
            if (result != null && result.ResponseTime > 0)
                parts.Add("Cycle: " + result.ResponseTime + "ms");
            return string.Join("\n", parts);
        }

        private static void AddMaterialChangeEvents(List<TimelineEvent> events, List<ResponseRecord> materialRecords)
        {
            foreach (var record in materialRecords)
                events.Add(new TimelineEvent
                {
                    Start     = record.TimestampParsed,
                    EventType = TimelineEventType.MaterialChange,
                    Label     = "MAT",
                    Detail    = "Material load" + (string.IsNullOrEmpty(record.Material) ? "" : ": " + record.Material)
                });
        }

        private static void AddSetupChangeEvents(List<TimelineEvent> events, List<ResponseRecord> setupRecords)
        {
            foreach (var record in setupRecords)
                events.Add(new TimelineEvent
                {
                    Start     = record.TimestampParsed,
                    EventType = TimelineEventType.SetupChange,
                    Label     = "SETUP",
                    Detail    = "Setup change" + (string.IsNullOrEmpty(record.Setup) ? "" : ": " + record.Setup)
                });
        }

        private static void AddIdleGapEvents(List<TimelineEvent> events, List<ResponseRecord> allRecords)
        {
            if (allRecords.Count < 2) return;
            DateTime? lastActivityTime = null;
            foreach (var record in allRecords)
            {
                if (lastActivityTime.HasValue)
                {
                    double gapMinutes = (record.TimestampParsed - lastActivityTime.Value).TotalMinutes;
                    if (gapMinutes >= MinGapMinutesForIdleBlock)
                        events.Add(new TimelineEvent
                        {
                            Start     = lastActivityTime.Value,
                            End       = record.TimestampParsed,
                            EventType = TimelineEventType.Idle,
                            Label     = "IDLE",
                            Detail    = $"No activity for {(int)gapMinutes} min"
                        });
                }
                lastActivityTime = record.TimestampParsed;
            }
        }
    }
}