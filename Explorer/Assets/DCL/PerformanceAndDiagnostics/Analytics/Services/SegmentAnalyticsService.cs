using Segment.Serialization;

namespace DCL.PerformanceAndDiagnostics.Analytics
{
    public class SegmentAnalyticsService : IAnalyticsService
    {
        private readonly Segment.Analytics.Analytics analytics;
        private readonly AnalyticsConfiguration configuration;

        public SegmentAnalyticsService(AnalyticsConfiguration configuration)
        {
            this.configuration = configuration;
            analytics = new Segment.Analytics.Analytics(configuration.SegmentConfiguration);

            // Our ReportHub logger is causing "System.FormatException" for JsonObject because of "{{" and "}}" in resulting string. Solution is expensive - message.ToString().Replace("{", "{{").Replace("}", "}}");
            // Segment.Analytics.Analytics.Logger = new SegmentLogger();
        }

        public void Identify(string userId, JsonObject traits = null) =>
            analytics.Identify(userId, traits);

        public void Track(string eventName, JsonObject properties = null) =>
            analytics.Track(eventName, properties);
    }
}
