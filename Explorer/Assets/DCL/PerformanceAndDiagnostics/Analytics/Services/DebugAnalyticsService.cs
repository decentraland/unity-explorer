using DCL.Diagnostics;
using Newtonsoft.Json.Linq;
using System.Text;

namespace DCL.PerformanceAndDiagnostics.Analytics.Services
{
    public class DebugAnalyticsService : IAnalyticsService
    {
        public void Identify(string? userId, JObject? traits = null)
        {
            ReportHub.Log(ReportCategory.ANALYTICS, $"Identify: userId = {userId} | traits = {traits}");
        }

        public void Track(string eventName, JObject? properties = null)
        {
            var message = new StringBuilder($"Track: {eventName}");

            if (properties != null)
            {
                foreach ((string? key, JToken? value) in properties)
                    message.Append($" \n {key} = {value}");

                message.Append('\n');
            }

            ReportHub.Log(ReportCategory.ANALYTICS, message.ToString());
        }

        public void InstantTrackAndFlush(string eventName, JObject? properties = null) =>
            Track(eventName, properties);

        public void AddPlugin(IAnalyticsPlugin plugin) { }

        public void Flush()
        {
            ReportHub.Log(ReportCategory.ANALYTICS, "Manual flush");
        }
    }
}
