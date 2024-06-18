namespace DCL.PerformanceAndDiagnostics.Analytics
{
    public class AnalyticsService
    {
        public AnalyticsService(AnalyticsConfiguration configuration)
        {
            var analytics = new Segment.Analytics.Analytics(configuration.SegmentConfiguration);
            Segment.Analytics.Analytics.Logger = new SegmentLogger();

            analytics.Identify("E@-Test");
            analytics.Track("track right after identify");
        }
    }
}
