using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DCL.Diagnostics
{
    public interface IReportHandler
    {
        void Log(LogType logType, ReportData reportData, Object context, object messageObj);

        void LogFormat(LogType logType, ReportData reportData, Object context, object message, params object[] args);

        void LogException<T>(T ecsSystemException) where T: Exception, IDecentralandException;

        void LogException(Exception exception, ReportData reportData, Object context);
    }
}
