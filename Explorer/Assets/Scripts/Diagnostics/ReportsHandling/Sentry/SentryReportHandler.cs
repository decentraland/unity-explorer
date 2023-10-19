using Sentry;
using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Diagnostics.ReportsHandling.Sentry
{
    public class SentryReportHandler : ReportHandlerBase
    {
        public SentryReportHandler(ICategorySeverityMatrix matrix, bool debounceEnabled)
            : base(matrix, debounceEnabled) { }

        internal override void LogInternal(LogType logType, ReportData category, Object context, object message)
        {
            SentrySdk.CaptureMessage(message.ToString(), AddSentryFilter, ToSentryLevel(in logType));
        }

        internal override void LogFormatInternal(LogType logType, ReportData category, Object context, object message, params object[] args)
        {
            var format = string.Format(message.ToString(), args);
            SentrySdk.CaptureMessage(format, AddSentryFilter, ToSentryLevel(in logType));
        }

        internal override void LogExceptionInternal<T>(T ecsSystemException)
        {
            SentrySdk.CaptureException(ecsSystemException, AddSentryFilter);
        }

        internal override void LogExceptionInternal(Exception exception, ReportData reportData, Object context)
        {
            SentrySdk.CaptureException(exception, AddSentryFilter);
        }

        private void AddSentryFilter(Scope scope) =>
            scope.SetTag("custom_reporting", "sentry");

        private SentryLevel ToSentryLevel(in LogType logType)
        {
            switch (logType)
            {
                case LogType.Assert:
                case LogType.Error:
                case LogType.Exception:
                    return SentryLevel.Error;
                case LogType.Log:
                default:
                    return SentryLevel.Info;
                case LogType.Warning:
                    return SentryLevel.Warning;
            }
        }
    }
}
