using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Diagnostics.ReportsHandling
{
    public class SentryReportHandlerWithSettings : ReportHandlerBase
    {
        private readonly SentryReportHandler sentryReportHandler;

        public SentryReportHandlerWithSettings(SentryReportHandler sentryReportHandler, ICategorySeverityMatrix matrix, bool debounceEnabled)
            : base(matrix, debounceEnabled)
        {
            this.sentryReportHandler = sentryReportHandler;
        }

        internal override void LogInternal(LogType logType, ReportData category, Object context, object message)
        {
            sentryReportHandler.Log(logType, category, context, message);
        }

        internal override void LogFormatInternal(LogType logType, ReportData category, Object context, object message, params object[] args)
        {
            sentryReportHandler.LogFormat(logType, category, context, message, args);
        }

        internal override void LogExceptionInternal<T>(T ecsSystemException)
        {
            sentryReportHandler.LogException(ecsSystemException);
        }

        internal override void LogExceptionInternal(Exception exception, ReportData reportData, Object context)
        {
            sentryReportHandler.LogException(exception, reportData, context);
        }
    }
}
