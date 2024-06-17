using Segment.Analytics;
using System;

namespace DCL.PerformanceAndDiagnostics.Analytics
{
    public class AnalyticsService
    {
        public AnalyticsService()
        {
            string writeKey = Environment.GetEnvironmentVariable("SEGMENT_WRITE_KEY");

            var configuration = new Configuration(writeKey, flushAt: 20, flushInterval: 30);
            var analytics = new Segment.Analytics.Analytics(configuration);

            analytics.Identify("E@-Test");
            analytics.Track("track right after identify");
        }
    }
}
