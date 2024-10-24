﻿using DCL.Diagnostics;
using Segment.Serialization;
using System;
using System.Runtime.CompilerServices;
using System.Text;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DCL.PerformanceAndDiagnostics.Analytics
{
    public class CriticalLogsAnalyticsHandler : IReportHandler
    {
        private const int PAYLOAD_LIMIT = 28 * 1024; // Segment == 32 KB, leaving some room for headers
        private const int SAFE_CHAR_LIMIT = PAYLOAD_LIMIT / 4; // 7,680 characters

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
                { "message", TrimToPayloadLimit(e.Message) },
            });
        }

        public void Log(LogType logType, ReportData reportData, Object context, object messageObj)
        {
            if (logType != LogType.Error && logType != LogType.Exception) return;

            analytics.Track(AnalyticsEvents.General.ERROR, new JsonObject
            {
                { "type", logType.ToString() },
                { "category", reportData.Category },
                { "scene_hash", reportData.SceneShortInfo.Name },
                { "message", messageObj is string messageString ? TrimToPayloadLimit(messageString) : TrimToPayloadLimit(messageObj.ToString()) },
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
                { "message", TrimToPayloadLimit(ecsSystemException.Message) },
            });
        }

        public void LogException(Exception exception, ReportData reportData, Object context)
        {
            analytics.Track(AnalyticsEvents.General.ERROR, new JsonObject
            {
                { "type", "exception" },
                { "category", reportData.Category },
                { "scene_hash", reportData.SceneShortInfo.Name },
                { "message", TrimToPayloadLimit(exception.Message) },
            });
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static string TrimToPayloadLimit(string message)
        {
            if (string.IsNullOrEmpty(message))
                return "Reported message was null or empty";

            return message.Length <= SAFE_CHAR_LIMIT
                ? message
                : message[..SAFE_CHAR_LIMIT];
        }
    }
}
