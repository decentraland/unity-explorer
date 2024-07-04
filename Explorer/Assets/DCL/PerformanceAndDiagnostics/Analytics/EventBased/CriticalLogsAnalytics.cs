using Segment.Serialization;
using System;

namespace DCL.PerformanceAndDiagnostics.Analytics
{
    public class CriticalLogsAnalytics
    {
        private readonly IAnalyticsController analytics;

        public CriticalLogsAnalytics(IAnalyticsController analytics)
        {
            this.analytics = analytics;
            AppDomain.CurrentDomain.UnhandledException += TrackUnhandledException;
        }

        private void TrackUnhandledException(object sender, UnhandledExceptionEventArgs args)
        {
            var e = (Exception)args.ExceptionObject;

            analytics.Track(AnalyticsEvents.General.CRITICAL_LOGS, new JsonObject
            {
                { "type", "unhandled exception" },
                { "category", "UNDEFINED" },
                { "scene_hash", "UNDEFINED" },
                { "message", e.Message },
            });
        }
    }
}
