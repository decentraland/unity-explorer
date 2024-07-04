using Segment.Serialization;
using System;
using UnityEngine;

namespace DCL.PerformanceAndDiagnostics.Analytics
{
    public class CriticalLogsAnalytics
    {
        private readonly IAnalyticsController analytics;

        public CriticalLogsAnalytics(IAnalyticsController analytics)
        {
            this.analytics = analytics;
            Application.logMessageReceived += TrackCriticalLogs;
            AppDomain.CurrentDomain.UnhandledException += TrackUnhandledException;
        }

        private void TrackUnhandledException(object sender, UnhandledExceptionEventArgs args)
        {
            var e = (Exception)args.ExceptionObject;

            analytics.Track(AnalyticsEvents.General.CRITICAL_LOGS, new JsonObject
            {
                { "type", "unhandled exception" },
                { "log", e.Message },
            });
        }

        private void TrackCriticalLogs(string logString, string stackTrace, LogType type)
        {
            if (type is not (LogType.Error or LogType.Exception)) return;

            analytics.Track(AnalyticsEvents.General.CRITICAL_LOGS, new JsonObject
            {
                { "type", type.ToString() },
                { "log", logString },
            });
        }
    }
}
