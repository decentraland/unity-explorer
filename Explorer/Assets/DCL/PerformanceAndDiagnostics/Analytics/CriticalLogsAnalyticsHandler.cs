using DCL.Diagnostics;
using Segment.Serialization;
using System;
using System.Text;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DCL.PerformanceAndDiagnostics.Analytics
{
    public class CriticalLogsAnalyticsHandler : IReportHandler
    {
        private const int PAYLOAD_LIMIT = 30 * 1024; // Segment == 32 KB, leaving some room for headers
        private const string LONG_MESSAGE_PLACEHOLDER = "error message is too long";

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
                { "category", IAnalyticsController.UNDEFINED },
                { "scene_hash", IAnalyticsController.UNDEFINED },
                { "message", IsPayloadSizeValid(e.Message) ? e.Message : LONG_MESSAGE_PLACEHOLDER },
            });
        }

        public void Log(LogType logType, ReportData reportData, Object context, object messageObj)
        {
            if (logType != LogType.Error && logType != LogType.Exception) return;

            var message = messageObj.ToString();

            analytics.Track(AnalyticsEvents.General.ERROR, new JsonObject
            {
                { "type", logType.ToString() },
                { "category", reportData.Category },
                { "scene_hash", reportData.SceneShortInfo.Name },
                { "message", IsPayloadSizeValid(message) ? message : LONG_MESSAGE_PLACEHOLDER },
            });
        }

        public void LogFormat(LogType logType, ReportData reportData, Object context, object message, params object[] args)
        {
            if (logType != LogType.Error && logType != LogType.Exception) return;

            Log(logType, reportData, context, string.Format(message.ToString(), args));
        }

        public void LogException<T>(T ecsSystemException) where T: Exception, IDecentralandException
        {
            analytics.Track(AnalyticsEvents.General.ERROR, new JsonObject
            {
                { "type", "exception" },
                { "category", "ecs" },
                { "scene_hash", IAnalyticsController.UNDEFINED },
                { "message", IsPayloadSizeValid(ecsSystemException.Message) ? ecsSystemException.Message : LONG_MESSAGE_PLACEHOLDER },
            });
        }

        public void LogException(Exception exception, ReportData reportData, Object context)
        {
            analytics.Track(AnalyticsEvents.General.ERROR, new JsonObject
            {
                { "type", "exception" },
                { "category", reportData.Category },
                { "scene_hash", reportData.SceneShortInfo.Name },
                { "message", IsPayloadSizeValid(exception.Message) ? exception.Message : LONG_MESSAGE_PLACEHOLDER },
            });
        }

        private static bool IsPayloadSizeValid(string message) =>
            Encoding.UTF8.GetByteCount(message) <= PAYLOAD_LIMIT;
    }
}
