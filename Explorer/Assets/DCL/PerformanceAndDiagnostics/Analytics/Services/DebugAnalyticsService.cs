﻿using DCL.Diagnostics;
using Segment.Analytics;
using Segment.Serialization;
using System.Collections.Generic;
using System.Text;

namespace DCL.PerformanceAndDiagnostics.Analytics.Services
{
    public class DebugAnalyticsService : IAnalyticsService
    {
        private static readonly IEnumerable<KeyValuePair<string, JsonElement>> EMPTY = new Dictionary<string, JsonElement>();

        public void Identify(string? userId, string? anonId, JsonObject? traits = null)
        {
            ReportHub.Log(ReportCategory.ANALYTICS, $"Identify: userId = {userId} | anonId = {anonId} | traits = {traits}");
        }

        public void Track(string eventName, JsonObject? properties = null)
        {
            var message = new StringBuilder($"Track: {eventName}");

            if (properties != null)
            {
                foreach (KeyValuePair<string, JsonElement> pair in properties.Content ?? EMPTY)
                    message.Append($" \n {pair.Key} = {pair.Value}");

                message.Append('\n');
            }

            ReportHub.Log(ReportCategory.ANALYTICS, message.ToString());
        }

        public void AddPlugin(EventPlugin plugin) { }

        public void Flush()
        {
            ReportHub.Log(ReportCategory.ANALYTICS, "Manual flush");
        }
    }
}
