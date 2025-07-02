﻿using CommandTerminal;
using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DCL.Diagnostics
{
    public class SceneDebugConsoleReportHandler : ReportHandlerBase
    {
        private readonly SceneDebugConsole sceneDebugConsole;

        public SceneDebugConsoleReportHandler(ICategorySeverityMatrix matrix, bool debounceEnabled) : base(ReportHandler.DebugLog, matrix, debounceEnabled)
        {
            sceneDebugConsole = GameObject.Instantiate(Resources.Load<GameObject>("SceneDebugConsole")).GetComponent<SceneDebugConsole>();
        }

        internal override void LogInternal(LogType logType, ReportData reportData, Object context, object message)
        {
            sceneDebugConsole.Log(message.ToString());
        }

        internal override void LogFormatInternal(LogType logType, ReportData category, Object context, object message, params object[] args)
        { }

        internal override void LogExceptionInternal<T>(T ecsSystemException)
        {
            sceneDebugConsole.Log(ecsSystemException.Message, ecsSystemException.StackTrace, LogType.Exception);
        }

        internal override void LogExceptionInternal(Exception exception, ReportData reportData, Object? context)
        {
            sceneDebugConsole.Log(exception.Message, exception.StackTrace, LogType.Exception);
        }
    }
}
