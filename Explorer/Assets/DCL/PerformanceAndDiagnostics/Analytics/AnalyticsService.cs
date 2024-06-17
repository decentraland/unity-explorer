using Segment.Analytics;
using System;

namespace DCL.PerformanceAndDiagnostics.Analytics
{
    public class AnalyticsService
    {
        public AnalyticsService()
        {
            string writeKey = Environment.GetEnvironmentVariable("SEGMENT_WRITE_KEY");

            var configuration = new Configuration(writeKey, flushAt: 20, flushInterval: 30, exceptionHandler: new ErrorHandler());
            var analytics = new Segment.Analytics.Analytics(configuration);

            Segment.Analytics.Analytics.Logger = new SegmentLogger();

            analytics.Identify("E@-Test");
            analytics.Track("track right after identify");
        }
    }
}
