using DCL.Optimization.Iterations;
using System;
using System.Collections.Generic;
using UnityEngine;
using Utility;
using Object = UnityEngine.Object;

namespace DCL.Diagnostics
{
    /// <summary>
    ///     Provides an extended way for logging, overrides default Unity DebugLogHandler.
    /// </summary>
    public class ReportHubLogger : ILogHandler
    {
        private readonly Action<Exception> emergencyLog = Debug.LogWarning;
        private readonly IReadOnlyList<(ReportHandler type, IReportHandler handler)> reportHandlers;

        public ReportHubLogger(IReadOnlyList<(ReportHandler, IReportHandler)> reportHandlers)
        {
            this.reportHandlers = reportHandlers;
        }

        /// <summary>
        ///     Provides a way to override default Unity DebugLogHandler
        /// </summary>
        void ILogHandler.LogFormat(LogType logType, Object context, string format, params object[] args)
        {
            // Report to all reports
            reportHandlers.ForeachNonAlloc(
                (logType, context, format, args),
                static (ctx, item) =>
                {
                    (ReportHandler _, IReportHandler handler) = item;
                    (LogType logType, Object? context, string? format, object[]? args) = ctx;
                    handler.LogFormat(logType, ReportData.UNSPECIFIED, context, format, args);
                }
            );
        }

        /// <summary>
        ///     Provides a way to override default Unity DebugLogHandler
        /// </summary>
        void ILogHandler.LogException(Exception exception, Object context)
        {
            reportHandlers.ForeachNonAlloc(
                (exception, context),
                static (ctx, item) =>
                {
                    (ReportHandler _, IReportHandler handler) = item;
                    (Exception? exception, Object? context) = ctx;
                    handler.LogException(exception, ReportData.UNSPECIFIED, context);
                }
            );
        }

        /// <summary>
        ///     Logs a message.
        /// </summary>
        /// <param name="logType">The type of the log message.</param>
        /// <param name="reportData">Report Data, try to provide as specific data as possible</param>
        /// <param name="message">Message</param>
        /// <param name="reportToHandlers">Handlers to report to, All by default</param>
        [HideInCallstack]
        public void Log(LogType logType, ReportData reportData, object message, ReportHandler reportToHandlers = ReportHandler.All)
        {
            reportHandlers.ForeachNonAlloc(
                (logType, reportData, message, reportToHandlers),
                static (ctx, item) =>
                {
                    (LogType logType, ReportData reportData, object? message, ReportHandler reportToHandlers) = ctx;
                    (ReportHandler type, IReportHandler? handler) = item;

                    if (EnumUtils.HasFlag(reportToHandlers, type))
                        handler.Log(logType, reportData, null, message);
                }
            );
        }

        /// <summary>
        ///     Logs a message.
        /// </summary>
        /// <param name="logType">The type of the log message.</param>
        /// <param name="reportData">Report Data, try to provide as specific data as possible</param>
        /// <param name="message">Message</param>
        /// <param name="context">Object to which the message applies.</param>
        /// <param name="reportToHandlers">Handlers to report to, All by default</param>
        [HideInCallstack]
        public void Log(LogType logType, ReportData reportData, object message, Object context, ReportHandler reportToHandlers = ReportHandler.All)
        {
            try
            {
                reportHandlers.ForeachNonAlloc(
                    (logType, reportData, message, context, reportToHandlers),
                    static (ctx, item) =>
                    {
                        (LogType logType, ReportData reportData, object? message, Object context, ReportHandler reportToHandlers) = ctx;
                        (ReportHandler type, IReportHandler? handler) = item;

                        if (EnumUtils.HasFlag(reportToHandlers, type))
                            handler.Log(logType, reportData, context, message);
                    }
                );
            }
            catch (Exception e) { emergencyLog(new Exception($"Some error while logging the message: '{message}'", e)); }
        }

        /// <summary>
        ///     Logs a formatted message.
        /// </summary>
        /// <param name="logType">The type of the log message.</param>
        /// <param name="reportData">Report Data, try to provide as specific data as possible</param>
        /// <param name="message">Message</param>
        /// <param name="reportHandler">Handlers to report to, All by default</param>
        /// <param name="args">Format arguments</param>
        [HideInCallstack]
        public void LogFormat(LogType logType, ReportData reportData, object message, ReportHandler reportHandler = ReportHandler.All, params object[] args)
        {
            reportHandlers.ForeachNonAlloc(
                (logType, reportData, message, reportHandler, args),
                static (ctx, item) =>
                {
                    (LogType logType, ReportData reportData, object? message, ReportHandler reportHandler, object[] args) = ctx;
                    (ReportHandler type, IReportHandler? handler) = item;

                    if (EnumUtils.HasFlag(reportHandler, type))
                        handler.LogFormat(logType, reportData, null, message, args);
                }
            );
        }

        /// <summary>
        ///     Logs a system exception message.
        /// </summary>
        /// <param name="ecsSystemException">ECS System Exception</param>
        /// <param name="reportHandler">Handlers to report to, All by default</param>
        [HideInCallstack]
        public void LogException<T>(T ecsSystemException, ReportHandler reportHandler = ReportHandler.All) where T: Exception, IDecentralandException
        {
            reportHandlers.ForeachNonAlloc(
                (ecsSystemException, reportHandler),
                static (ctx, item) =>
                {
                    (T ecsSystemException, ReportHandler reportHandler) = ctx;
                    (ReportHandler type, IReportHandler handler) = item;

                    if (EnumUtils.HasFlag(reportHandler, type))
                        handler.LogException(ecsSystemException);
                }
            );
        }

        /// <summary>
        ///     Logs a common exception
        /// </summary>
        /// <param name="exception">Exception</param>
        /// <param name="reportData">Report Data, try to provide as specific data as possible</param>
        /// <param name="reportHandler">Handlers to report to, All by default</param>
        [HideInCallstack]
        public void LogException(Exception exception, ReportData reportData, ReportHandler reportHandler = ReportHandler.All)
        {
            reportHandlers.ForeachNonAlloc(
                (exception, reportData, reportHandler),
                static (ctx, item) =>
                {
                    (Exception? exception, ReportData reportData, ReportHandler reportHandler) = ctx;
                    (ReportHandler type, IReportHandler handler) = item;

                    if (EnumUtils.HasFlag(reportHandler, type))
                        handler.LogException(exception, reportData, null);
                }
            );
        }
    }
}
