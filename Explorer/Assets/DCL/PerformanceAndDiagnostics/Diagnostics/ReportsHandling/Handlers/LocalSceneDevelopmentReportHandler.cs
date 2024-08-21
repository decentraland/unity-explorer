using CommandTerminal;
using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DCL.Diagnostics
{
    public class LocalSceneDevelopmentReportHandler : ReportHandlerBase
    {
        private readonly LocalSceneTerminal localSceneTerminal;

        public LocalSceneDevelopmentReportHandler(ICategorySeverityMatrix matrix, bool debounceEnabled) : base(matrix, debounceEnabled)
        {
            localSceneTerminal = GameObject.Instantiate(Resources.Load<GameObject>("LocalSceneTerminal")).GetComponent<LocalSceneTerminal>();
        }

        internal override void LogInternal(LogType logType, ReportData reportData, Object context, object message)
        {
            localSceneTerminal.Log(message.ToString());
        }

        internal override void LogFormatInternal(LogType logType, ReportData category, Object context, object message, params object[] args)
        { }

        internal override void LogExceptionInternal<T>(T ecsSystemException)
        {
            localSceneTerminal.Log(ecsSystemException.Message, ecsSystemException.StackTrace, LogType.Exception);
        }

        internal override void LogExceptionInternal(Exception exception, ReportData reportData, Object context)
        {
            localSceneTerminal.Log(exception.Message, exception.StackTrace, LogType.Exception);
        }
    }
}
