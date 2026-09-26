using DCL.UI.DebugMenu.MessageBus;
using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DCL.Diagnostics
{
    public class SceneDebugConsoleReportHandler : ReportHandlerBase
    {
        private readonly DebugMenuConsoleLogEntryBus consoleLogEntryBus;

        public SceneDebugConsoleReportHandler(ICategorySeverityMatrix matrix, DebugMenuConsoleLogEntryBus consoleLogEntryBus, bool debounceEnabled) : base(ReportHandler.DebugLog, matrix, debounceEnabled)
        {
            this.consoleLogEntryBus = consoleLogEntryBus;
        }

        internal override void LogInternal(LogType logType, ReportData reportData, Object context, object message)
        {
            consoleLogEntryBus.Send(message.ToString(), logType);
        }

        internal override void LogFormatInternal(LogType logType, ReportData category, Object context, object message, params object[] args)
        {
            consoleLogEntryBus.Send(string.Format(message.ToString(), args), logType);
        }

        internal override void LogExceptionInternal<T>(T ecsSystemException)
        {
            consoleLogEntryBus.Send(ecsSystemException.Message, LogType.Exception);
            consoleLogEntryBus.Send(ecsSystemException.StackTrace, LogType.Exception);
        }

        internal override void LogExceptionInternal(Exception exception, ReportData reportData, Object? context)
        {
            consoleLogEntryBus.Send(exception.Message, LogType.Exception);
            consoleLogEntryBus.Send(exception.StackTrace, LogType.Exception);
        }
    }
}
