using DCL.Diagnostics;
using Segment.Serialization;
using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DCL.PerformanceAndDiagnostics.Analytics
{
    public class CriticalLogsAnalyticsHandler : IReportHandler
    {
        private readonly IAnalyticsController analytics;

        public CriticalLogsAnalyticsHandler(IAnalyticsController analytics)
        {
            this.analytics = analytics;
            AppDomain.CurrentDomain.UnhandledException += TrackUnhandledException;
        }

        ~CriticalLogsAnalyticsHandler()
        {
            AppDomain.CurrentDomain.UnhandledException -= TrackUnhandledException;
        }

        private void TrackUnhandledException(object sender, UnhandledExceptionEventArgs args)
        {
            var e = (Exception)args.ExceptionObject;

            analytics.Track(AnalyticsEvents.General.ERROR, new JsonObject
            {
                { "type", "unhandled exception" },
                { "category", "UNDEFINED" },
                { "scene_hash", "UNDEFINED" },
                { "message", e.Message },
            });
        }

        public void Log(LogType logType, ReportData reportData, Object context, object message)
        {
            if(logType != LogType.Error && logType != LogType.Exception) return;

            analytics.Track(AnalyticsEvents.General.ERROR, new JsonObject
            {
                { "type", logType.ToString() },
                { "category", reportData.Category },
                { "scene_hash", reportData.SceneShortInfo.Name },
                { "message", message.ToString() },
            });
        }

        public void LogFormat(LogType logType, ReportData reportData, Object context, object message, params object[] args)
        {
            if(logType != LogType.Error && logType != LogType.Exception) return;

            Log(logType, reportData, context, string.Format(message.ToString(), args));
        }

        public void LogException<T>(T ecsSystemException) where T : Exception, IDecentralandException
        {
            analytics.Track(AnalyticsEvents.General.ERROR, new JsonObject
            {
                { "type", "exception" },
                { "category", "ecs" },
                { "scene_hash", "UNDEFINED" },
                { "message", ecsSystemException.Message },
            });
        }

        public void LogException(Exception exception, ReportData reportData, Object context)
        {
            analytics.Track(AnalyticsEvents.General.ERROR, new JsonObject
            {
                { "type", "exception" },
                { "category", reportData.Category },
                { "scene_hash", reportData.SceneShortInfo.Name },
                { "message", exception.Message },
            });
        }
    }
}
