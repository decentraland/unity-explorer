using Segment.Analytics;
using Segment.Serialization;

namespace DCL.PerformanceAndDiagnostics.Analytics
{
    public class SegmentAnalyticsService : IAnalyticsService
    {
        private readonly Segment.Analytics.Analytics analytics;

        public SegmentAnalyticsService(Configuration segmentConfiguration)
        {
            analytics = new Segment.Analytics.Analytics(segmentConfiguration);
            Segment.Analytics.Analytics.Logger = new SegmentLogger();
        }

        public void Identify(string userId, JsonObject traits = null) =>
            analytics.Identify(userId, traits);

        public void Track(string eventName, JsonObject properties = null) =>
            analytics.Track(eventName, properties);

        public void AddPlugin(Plugin plugin) =>
            analytics.Add(plugin);
    }
}
