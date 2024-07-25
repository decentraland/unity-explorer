using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DCL.Diagnostics
{
    public class LocalSceneDevelopmentReportHandler : ReportHandlerBase
    {
        public LocalSceneDevelopmentReportHandler(ICategorySeverityMatrix matrix, bool debounceEnabled) : base(matrix, debounceEnabled)
        {

        }

        internal override void LogInternal(LogType logType, ReportData category, Object context, object message)
        {
            Debug.Log($"LogInternal: [{logType}][{category.Category}{message}]");
        }

        internal override void LogFormatInternal(LogType logType, ReportData category, Object context, object message, params object[] args)
        {
        }

        internal override void LogExceptionInternal<T>(T ecsSystemException)
        {
            Debug.Log($"LogInternal: [{typeof(T)}]{ecsSystemException}");
        }

        internal override void LogExceptionInternal(Exception exception, ReportData reportData, Object context)
        {
            Debug.Log($"LogInternal: [{reportData.Category}{exception.Message}]");
        }
    }
}
