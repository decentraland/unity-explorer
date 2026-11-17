using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DCL.Diagnostics
{
    /// <summary>
    ///     Forwards every report its matrix admits to a sink, keeping the report's category and scene so the sink
    ///     can tell a scene's own output from the client's.
    /// </summary>
    public class ReportSinkHandler : ReportHandlerBase
    {
        private readonly IReportSink sink;

        public ReportSinkHandler(ICategorySeverityMatrix matrix, IReportSink sink) : base(ReportHandler.DebugLog, matrix, debounceEnabled: false)
        {
            this.sink = sink;
        }

        internal override void LogInternal(LogType logType, ReportData reportData, Object context, object message)
        {
            sink.Receive(new ReportLogEntry(logType, reportData.Category, reportData.SceneShortInfo, message.ToString(), null));
        }

        internal override void LogFormatInternal(LogType logType, ReportData reportData, Object context, object message, params object[] args)
        {
            sink.Receive(new ReportLogEntry(logType, reportData.Category, reportData.SceneShortInfo, string.Format(message.ToString(), args), null));
        }

        internal override void LogExceptionInternal<T>(T ecsSystemException)
        {
            sink.Receive(new ReportLogEntry(LogType.Exception, ecsSystemException.ReportData.Category, ecsSystemException.ReportData.SceneShortInfo, ecsSystemException.Message, ecsSystemException.StackTrace));
        }

        internal override void LogExceptionInternal(Exception exception, ReportData reportData, Object? context)
        {
            sink.Receive(new ReportLogEntry(LogType.Exception, reportData.Category, reportData.SceneShortInfo, exception.Message, exception.StackTrace));
        }
    }
}
