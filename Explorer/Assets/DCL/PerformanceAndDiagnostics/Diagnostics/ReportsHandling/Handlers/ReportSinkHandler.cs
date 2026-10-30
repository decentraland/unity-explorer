using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DCL.Diagnostics
{
    /// <summary>
    ///     One report as a sink receives it: the message text plus the category and scene the reporter attached,
    ///     which the Unity log line itself does not carry.
    /// </summary>
    public readonly struct ReportLogEntry
    {
        public readonly LogType LogType;
        public readonly string Category;
        public readonly SceneShortInfo Scene;
        public readonly string Message;
        public readonly string? StackTrace;

        public ReportLogEntry(LogType logType, string category, SceneShortInfo scene, string message, string? stackTrace)
        {
            LogType = logType;
            Category = category;
            Scene = scene;
            Message = message;
            StackTrace = stackTrace;
        }
    }

    /// <summary>
    ///     Receives the reports a <see cref="ReportSinkHandler" /> lets through. Called from whichever thread reported.
    /// </summary>
    public interface IReportSink
    {
        void Receive(in ReportLogEntry entry);
    }

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
