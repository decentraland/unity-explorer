using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Diagnostics.ReportsHandling
{
    public interface IReportHandler
    {
        void Log(LogType logType, ReportData reportData, Object context, object message);

        void LogFormat(LogType logType, ReportData reportData, Object context, object message, params object[] args);

        void LogException(EcsSystemException ecsSystemException);

        void LogException(Exception exception, ReportData reportData, Object context);
    }
}
