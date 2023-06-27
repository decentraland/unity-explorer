using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Diagnostics.ReportsHandling
{
    public abstract class ReportHandlerBase : IReportHandler
    {
        private readonly bool debounceEnabled;
        private readonly ICategorySeverityMatrix matrix;

        private readonly HashSet<object> staticMessages;

        protected ReportHandlerBase(ICategorySeverityMatrix matrix, bool debounceEnabled)
        {
            this.matrix = matrix;
            this.debounceEnabled = debounceEnabled;

            staticMessages = new HashSet<object>(1000);
        }

        public void Log(LogType logType, ReportData reportData, Object context, object message)
        {
            if (IsLogMessageAllowed(message, reportData, logType))
                LogInternal(logType, reportData, context, message);
        }

        public void LogFormat(LogType logType, ReportData reportData, Object context, object message, params object[] args)
        {
            if (IsLogMessageAllowed(message, reportData, logType))
                LogFormatInternal(logType, reportData, context, message, args);
        }

        public void LogException(EcsSystemException ecsSystemException)
        {
            if (IsLogMessageAllowed(ecsSystemException.ToString(), ecsSystemException.ReportData, LogType.Exception))
                LogExceptionInternal(ecsSystemException);
        }

        public void LogException(Exception exception, ReportData reportData, Object context)
        {
            if (IsLogMessageAllowed(exception.ToString(), reportData, LogType.Exception))
                LogExceptionInternal(exception, context);
        }

        protected abstract void LogInternal(LogType logType, ReportData category, Object context, object message);

        protected abstract void LogFormatInternal(LogType logType, ReportData category, Object context, object message, params object[] args);

        protected abstract void LogExceptionInternal(EcsSystemException ecsSystemException);

        protected abstract void LogExceptionInternal(Exception exception, Object context);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsLogMessageAllowed(in object message, in ReportData reportData, LogType logType) =>
            matrix.IsEnabled(reportData.Category, logType) && Debounce(in message, in reportData, logType);

        private bool Debounce(in object message, in ReportData reportData, LogType logType)
        {
            if (!debounceEnabled) return false;

            return DebounceInternal(in message, in reportData, logType);
        }

        protected virtual bool DebounceInternal(in object message, in ReportData reportData, LogType logType)
        {
            return reportData.Hint switch
                   {
                       ReportHint.AssemblyStatic => !staticMessages.Add(message),
                       ReportHint.SessionStatic => !staticMessages.Add(message),
                       _ => false,
                   };
        }
    }
}
