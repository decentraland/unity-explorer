using System;
using DCL.UI.SceneDebugConsole.MessageBus;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DCL.Diagnostics
{
    public class SceneDebugConsoleReportHandler : ReportHandlerBase
    {
        private readonly SceneDebugConsoleLogEntryBus sceneDebugConsoleLogEntryBus;

        public SceneDebugConsoleReportHandler(ICategorySeverityMatrix matrix, SceneDebugConsoleLogEntryBus sceneDebugConsoleLogEntryBus, bool debounceEnabled) : base(matrix, debounceEnabled)
        {
            this.sceneDebugConsoleLogEntryBus = sceneDebugConsoleLogEntryBus;
        }

        internal override void LogInternal(LogType logType, ReportData reportData, Object context, object message)
        {
            sceneDebugConsoleLogEntryBus.Send(message.ToString(), logType);
        }

        internal override void LogFormatInternal(LogType logType, ReportData category, Object context, object message, params object[] args)
        {
            sceneDebugConsoleLogEntryBus.Send(string.Format(message.ToString(), args), logType);
        }

        internal override void LogExceptionInternal<T>(T ecsSystemException)
        {
            sceneDebugConsoleLogEntryBus.Send(ecsSystemException.Message, LogType.Exception);
            sceneDebugConsoleLogEntryBus.Send(ecsSystemException.StackTrace, LogType.Exception);
        }

        internal override void LogExceptionInternal(Exception exception, ReportData reportData, Object context)
        {
            sceneDebugConsoleLogEntryBus.Send(exception.Message, LogType.Exception);
            sceneDebugConsoleLogEntryBus.Send(exception.StackTrace, LogType.Exception);
        }
    }
}
