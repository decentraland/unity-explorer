using CommandTerminal;
using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DCL.Diagnostics
{
    public class SceneDedicatedReportHandler : ReportHandlerBase
    {
        private readonly SceneDebugTerminal sceneDebugTerminal;

        public SceneDedicatedReportHandler(ICategorySeverityMatrix matrix, bool debounceEnabled) : base(matrix, debounceEnabled)
        {
            sceneDebugTerminal = GameObject.Instantiate(Resources.Load<GameObject>("SceneDebugTerminal")).GetComponent<SceneDebugTerminal>();
        }

        internal override void LogInternal(LogType logType, ReportData reportData, Object context, object message)
        {
            sceneDebugTerminal.Log(message.ToString());
        }

        internal override void LogFormatInternal(LogType logType, ReportData category, Object context, object message, params object[] args)
        { }

        internal override void LogExceptionInternal<T>(T ecsSystemException)
        {
            sceneDebugTerminal.Log(ecsSystemException.Message, ecsSystemException.StackTrace, LogType.Exception);
        }

        internal override void LogExceptionInternal(Exception exception, ReportData reportData, Object context)
        {
            sceneDebugTerminal.Log(exception.Message, exception.StackTrace, LogType.Exception);
        }
    }
}
