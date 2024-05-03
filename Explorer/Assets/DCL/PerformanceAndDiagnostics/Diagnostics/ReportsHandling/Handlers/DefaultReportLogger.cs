using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DCL.Diagnostics
{
    /// <summary>
    ///     Default Unity implementation is used in case no instance is provided by the scripts
    /// </summary>
    public class DefaultReportLogger : IReportHandler
    {
        public void Log(LogType logType, ReportData reportData, Object context, object message)
        {
            Debug.unityLogger.Log(logType, message, context);
        }

        public void LogFormat(LogType logType, ReportData reportData, Object context, object message, params object[] args)
        {
            Debug.unityLogger.LogFormat(logType, context, message.ToString(), args);
        }

        public void LogException<T>(T ecsSystemException) where T: Exception, IDecentralandException
        {
            Debug.unityLogger.LogException(ecsSystemException);
        }

        public void LogException(Exception exception, ReportData reportData, Object context)
        {
            Debug.unityLogger.LogException(exception, context);
        }
    }
}
