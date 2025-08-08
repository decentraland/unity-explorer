using DCL.UI.DebugMenu.MessageBus;
using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DCL.Diagnostics
{
    public class SceneDebugConsoleReportHandler : ReportHandlerBase
    {
        private readonly DebugMenuLogEntryBus debugMenuLogEntryBus;

        public SceneDebugConsoleReportHandler(ICategorySeverityMatrix matrix, DebugMenuLogEntryBus debugMenuLogEntryBus, bool debounceEnabled) : base(ReportHandler.DebugLog, matrix, debounceEnabled)
        {
            this.debugMenuLogEntryBus = debugMenuLogEntryBus;
        }

        internal override void LogInternal(LogType logType, ReportData reportData, Object context, object message)
        {
            debugMenuLogEntryBus.Send(message.ToString(), logType);
        }

        internal override void LogFormatInternal(LogType logType, ReportData category, Object context, object message, params object[] args)
        {
            debugMenuLogEntryBus.Send(string.Format(message.ToString(), args), logType);
        }

        internal override void LogExceptionInternal<T>(T ecsSystemException)
        {
            debugMenuLogEntryBus.Send(ecsSystemException.Message, LogType.Exception);
            debugMenuLogEntryBus.Send(ecsSystemException.StackTrace, LogType.Exception);
        }

        internal override void LogExceptionInternal(Exception exception, ReportData reportData, Object? context)
        {
            debugMenuLogEntryBus.Send(exception.Message, LogType.Exception);
            debugMenuLogEntryBus.Send(exception.StackTrace, LogType.Exception);
        }
    }
}
