using Sentry;
using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Diagnostics.ReportsHandling
{
    public class SentryReportHandler : IReportHandler
    {
        public void Log(LogType logType, ReportData reportData, Object context, object message)
        {
            SentrySdk.CaptureMessage(message.ToString(), ToSentryLevel(logType));
        }

        public void LogFormat(LogType logType, ReportData reportData, Object context, object message, params object[] args)
        {
            var format = string.Format(message.ToString(), args);
            SentrySdk.CaptureMessage(format, ToSentryLevel(logType));
        }

        public void LogException<T>(T ecsSystemException) where T: Exception, IManagedEcsException
        {
            SentrySdk.CaptureException(ecsSystemException);
        }

        public void LogException(Exception exception, ReportData reportData, Object context)
        {
            SentrySdk.CaptureException(exception);
        }

        private SentryLevel ToSentryLevel(LogType logType)
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
