using DCL.Diagnostics;
using Segment.Analytics;
using Segment.Serialization;
using System;
using System.Collections.Generic;
using System.Text;

namespace DCL.PerformanceAndDiagnostics.Analytics
{
    public class DebugAnalyticsService : IAnalyticsService
    {
        private static readonly IEnumerable<KeyValuePair<string, JsonElement>> EMPTY = new Dictionary<string, JsonElement>();

        public void Identify(string userId, JsonObject? traits = null)
        {
            ReportHub.Log(ReportCategory.ANALYTICS, $"Identify: userId = {userId} | traits = {traits}");
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

        public void AddPlugin(Plugin plugin) { }

        public void Flush()
        {
            ReportHub.Log(ReportCategory.ANALYTICS, "Manual flush");
        }
    }
}
