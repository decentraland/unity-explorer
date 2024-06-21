using Segment.Serialization;
using UnityEngine;

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
        public void Identify(string userId, JsonObject traits = null) =>
            Debug.Log($"Identify: userId={userId}, traits={traits}");

        public void Track(string eventName, JsonObject properties = null) =>
            Debug.Log($"Track: eventName={eventName}, properties={properties}");
    }

    public class SegmentAnalyticsService : IAnalyticsService
    {
        private readonly Segment.Analytics.Analytics analytics;

        public SegmentAnalyticsService(AnalyticsConfiguration configuration)
        {
            analytics = new Segment.Analytics.Analytics(configuration.SegmentConfiguration);
            Segment.Analytics.Analytics.Logger = new SegmentLogger();

            // Identify("testUser-123", new JsonObject
            // {
            //     ["username"] = "Vitaly Popuzin",
            //     ["runtime"] = Application.isEditor ? "editor" : "build",
            // });
            //
            // Track("testEvent", new JsonObject
            // {
            //     ["runtime"] = Application.isEditor ? "editor" : "build",
            // });
        }

        public void Identify(string userId, JsonObject traits = null) =>
            analytics.Identify(userId, traits);

        public void Track(string eventName, JsonObject properties = null) =>
            analytics.Track(eventName, properties);
    }
}
