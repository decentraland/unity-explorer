using Segment.Analytics;
using Segment.Serialization;
using System;
using UnityEngine;

namespace DCL.PerformanceAndDiagnostics.Analytics
{
    public class SegmentAnalyticsService : IAnalyticsService
    {
        private readonly Segment.Analytics.Analytics analytics;

        [Obsolete("Use RustSegmentAnalyticsService instead")]
        public SegmentAnalyticsService(Configuration segmentConfiguration)
        {
            analytics = new Segment.Analytics.Analytics(segmentConfiguration);
            Segment.Analytics.Analytics.Logger = new SegmentLogger();

            Application.quitting += () => analytics.Flush();
            AppDomain.CurrentDomain.ProcessExit += (_, _) => analytics.Flush();
        }

        ~SegmentAnalyticsService()
        {
            analytics.Flush();
        }

        public void Identify(string? userId, JsonObject? traits = null) =>
            analytics.Identify(userId!, traits!);

        public void Track(string eventName, JsonObject? properties = null) =>
            analytics.Track(eventName, properties!);

        public void AddPlugin(EventPlugin plugin) =>
            analytics.Add(plugin);

        public void Flush() =>
            analytics.Flush();
    }
}
