using DCL.Diagnostics;
using Segment.Serialization;

namespace DCL.PerformanceAndDiagnostics.Analytics
{
    /// <summary>
    /// For the events we use the convention of all lower cases and "_" instead of space
    /// </summary>
    public interface IAnalyticsService
    {
        void Identify(string userId, JsonObject traits = null);

        void Track(string eventName, JsonObject properties = null);
    }

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

    public class SegmentAnalyticsService : IAnalyticsService
    {
        private readonly Segment.Analytics.Analytics analytics;

        public SegmentAnalyticsService(AnalyticsConfiguration configuration)
        {
            analytics = new Segment.Analytics.Analytics(configuration.SegmentConfiguration);
            Segment.Analytics.Analytics.Logger = new SegmentLogger();
        }

        public void Identify(string userId, JsonObject traits = null) =>
            analytics.Identify(userId, traits);

        public void Track(string eventName, JsonObject properties = null) =>
            analytics.Track(eventName, properties);
    }
}
