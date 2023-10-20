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
            SentrySdk.CaptureMessage(message.ToString(), scope => AddReportData(scope, in category), ToSentryLevel(in logType));
        }

        internal override void LogFormatInternal(LogType logType, ReportData category, Object context, object message, params object[] args)
        {
            var format = string.Format(message.ToString(), args);
            SentrySdk.CaptureMessage(format, scope => AddReportData(scope, in category), ToSentryLevel(in logType));
        }

        internal override void LogExceptionInternal<T>(T ecsSystemException)
        {
            SentrySdk.CaptureException(ecsSystemException);
        }

        internal override void LogExceptionInternal(Exception exception, ReportData reportData, Object context)
        {
            SentrySdk.CaptureException(exception, scope => AddReportData(scope, in reportData));
        }

        private void AddReportData(Scope scope, in ReportData data)
        {
            AddCategoryTag(scope, in data);
            AddSceneInfo(scope, in data);
        }

        private void AddCategoryTag(Scope scope, in ReportData data) =>
            scope.SetTag("category", data.Category);

        private void AddSceneInfo(Scope scope, in ReportData data)
        {
            if (data.SceneShortInfo.BaseParcel == Vector2Int.zero) return;
            scope.SetTag("scene.base_parcel", data.SceneShortInfo.BaseParcel.ToString());
            scope.SetTag("scene.name", data.SceneShortInfo.Name);
        }

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
