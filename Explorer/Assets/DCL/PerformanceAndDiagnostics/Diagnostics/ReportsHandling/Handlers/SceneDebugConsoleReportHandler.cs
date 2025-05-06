using System;
using DCL.UI.SceneDebugConsole.MessageBus;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DCL.Diagnostics
{
    public class SceneDebugConsoleReportHandler : ReportHandlerBase
    {
        private readonly SceneDebugConsoleMessageBus sceneDebugConsoleMessageBus;

        public SceneDebugConsoleReportHandler(ICategorySeverityMatrix matrix, SceneDebugConsoleMessageBus sceneDebugConsoleMessageBus, bool debounceEnabled) : base(matrix, debounceEnabled)
        {
            this.sceneDebugConsoleMessageBus = sceneDebugConsoleMessageBus;
        }

        internal override void LogInternal(LogType logType, ReportData reportData, Object context, object message)
        {
            sceneDebugConsoleMessageBus.Send(message.ToString(), logType);
        }

        internal override void LogFormatInternal(LogType logType, ReportData category, Object context, object message, params object[] args)
        {
            sceneDebugConsoleMessageBus.Send(string.Format(message.ToString(), args), logType);
        }

        internal override void LogExceptionInternal<T>(T ecsSystemException)
        {
            sceneDebugConsoleMessageBus.Send(ecsSystemException.Message, LogType.Exception);
            sceneDebugConsoleMessageBus.Send(ecsSystemException.StackTrace, LogType.Exception);
        }

        internal override void LogExceptionInternal(Exception exception, ReportData reportData, Object context)
        {
            sceneDebugConsoleMessageBus.Send(exception.Message, LogType.Exception);
            sceneDebugConsoleMessageBus.Send(exception.StackTrace, LogType.Exception);
        }
    }
}
