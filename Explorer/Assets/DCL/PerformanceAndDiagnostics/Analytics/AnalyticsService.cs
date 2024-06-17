using Segment.Analytics;

namespace DCL.PerformanceAndDiagnostics.Analytics
{
    public class AnalyticsService
    {
        public AnalyticsService()
        {
            var configuration = new Configuration("<YOUR WRITE KEY>",
                flushAt: 20,
                flushInterval: 30);
            var analytics = new Segment.Analytics.Analytics(configuration);
        }
    }
}
