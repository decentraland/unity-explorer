using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using Utility;
using Object = UnityEngine.Object;

namespace DCL.Diagnostics
{
    public abstract class ReportHandlerBase : IReportHandler
    {
        private readonly bool debounceEnabled;
        private readonly ReportHandler type;
        private readonly ICategorySeverityMatrix matrix;

        private readonly HashSet<object> staticMessages;

        protected ReportHandlerBase(ReportHandler type, ICategorySeverityMatrix matrix, bool debounceEnabled)
        {
            this.type = type;
            this.matrix = matrix;
            this.debounceEnabled = debounceEnabled;

            staticMessages = new HashSet<object>(1000);
        }

        [HideInCallstack]
        public void Log(LogType logType, ReportData reportData, Object context, object messageObj)
        {
            if (IsLogMessageAllowed(messageObj, reportData, logType))
                LogInternal(logType, reportData, context, messageObj);
        }

        [HideInCallstack]
        public void LogFormat(LogType logType, ReportData reportData, Object context, object message, params object[] args)
        {
            if (IsLogMessageAllowed(message, reportData, logType))
                LogFormatInternal(logType, reportData, context, message, args);
        }

        [HideInCallstack]
        public void LogException<T>(T ecsSystemException) where T: Exception, IDecentralandException
        {
            if (IsLogMessageAllowed(ecsSystemException, in ecsSystemException.ReportData, LogType.Exception))
                LogExceptionInternal(ecsSystemException);
            else
                HandleSuppressedException(ecsSystemException, ecsSystemException.ReportData);
        }

        [HideInCallstack]
        public void LogException(Exception exception, ReportData reportData, Object? context)
        {
            if (IsLogMessageAllowed(exception, reportData, LogType.Exception))
                LogExceptionInternal(exception, reportData, context);
            else
                HandleSuppressedException(exception, reportData);
        }

        internal abstract void LogInternal(LogType logType, ReportData category, Object context, object message);

        internal abstract void LogFormatInternal(LogType logType, ReportData category, Object context, object message, params object[] args);

        internal abstract void LogExceptionInternal<T>(T ecsSystemException) where T: Exception, IDecentralandException;

        internal abstract void LogExceptionInternal(Exception exception, ReportData reportData, Object? context);

        internal virtual void HandleSuppressedException(Exception exception, ReportData reportData) { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [HideInCallstack]
        private bool IsLogMessageAllowed(in object message, in ReportData reportData, LogType logType) =>
            matrix.IsEnabled(reportData.Category, logType) && !Debounce(in message, in reportData, logType);

        private bool IsLogMessageAllowed(in Exception message, in ReportData reportData, LogType logType) =>
            matrix.IsEnabled(reportData.Category, logType) && !Debounce(in message, in reportData, logType);

        private bool Debounce(in Exception message, in ReportData reportData, LogType logType)
        {
            if (!debounceEnabled) return false;

            // Don't call DebounceInternal as exceptions can't be compared

            return TryGetQualifiedDebouncer(in reportData, out IReportsDebouncer? debouncer)
                   && debouncer!.Debounce(new ReportMessageFingerprint(new ExceptionFingerprint(message, reportData.Debounce.CallStackHint)), reportData, logType);
        }

        private bool Debounce(in object message, in ReportData reportData, LogType logType)
        {
            if (!debounceEnabled) return false;

            if (message is not string stringMessage) // Can't make assumptions about the message type
                return false;

            return TryGetQualifiedDebouncer(in reportData, out IReportsDebouncer? debouncer)
                   && debouncer!.Debounce(new ReportMessageFingerprint(stringMessage), reportData, logType);
        }

        [HideInCallstack]
        private bool TryGetQualifiedDebouncer(in ReportData reportData, out IReportsDebouncer? debouncer)
        {
            debouncer = reportData.Debounce.Debouncer;
            return debouncer != null && EnumUtils.HasFlag(debouncer.AppliedTo, type);
        }
    }
}
