using DCL.Diagnostics;
using Segment.Serialization;

namespace DCL.PerformanceAndDiagnostics.Analytics
{
    public class DebugAnalyticsService : IAnalyticsService
    {
        public void Identify(string userId, JsonObject traits = null)
        {
            ReportHub.Log(ReportCategory.ANALYTICS, $"Identify: userId = {userId} | traits = {traits}");
        }

        public void Track(string eventName, JsonObject properties = null)
        {
            var message = $"Track: {eventName}";

            foreach (var pair in properties.Content)
                message += $" \n {pair.Key} = {pair.Value}";

            message += "\n";

            ReportHub.Log(ReportCategory.ANALYTICS, message);
        }
    }
}
