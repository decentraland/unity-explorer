using Segment.Serialization;
using UnityEngine;

namespace DCL.PerformanceAndDiagnostics.Analytics
{
    public class AnalyticsService
    {
        public AnalyticsService(AnalyticsConfiguration configuration)
        {
            var analytics = new Segment.Analytics.Analytics(configuration.SegmentConfiguration);
            Segment.Analytics.Analytics.Logger = new SegmentLogger();

            analytics.Identify("testUser-123", new JsonObject {
                ["username"] = "Vitaly Popuzin",
                ["runtime"] = Application.isEditor? "editor" : "build",
            });
            analytics.Track("Test right after identify");
        }
    }
}
