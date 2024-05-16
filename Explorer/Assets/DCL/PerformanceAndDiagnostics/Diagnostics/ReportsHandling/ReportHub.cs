using System;
using System.Diagnostics;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DCL.Diagnostics
{
    /// <summary>
    ///     Provides static accessors to extended logging possibilities
    /// </summary>
    public static class ReportHub
    {
        public static ReportHubLogger Instance { get; set; } =
            new (new (ReportHandler, IReportHandler)[]
            {
                (ReportHandler.DebugLog, new DefaultReportLogger()),
            });

        /// <summary>
        ///     Logs a message.
        /// </summary>
        /// <param name="logType">The type of the log message.</param>
        /// <param name="reportData">Report Data, try to provide as specific data as possible</param>
        /// <param name="message">Message</param>
        /// <param name="reportToHandlers">Handlers to report to, All by default</param>
        [HideInCallstack]
        public static void Log(LogType logType, ReportData reportData, object message, ReportHandler reportToHandlers = ReportHandler.All)
        {
            Instance.Log(logType, reportData, message, null!, reportToHandlers);
        }

        /// <summary>
        ///     Logs a warning.
        /// </summary>
        /// <param name="reportData">Report Data, try to provide as specific data as possible</param>
        /// <param name="message">Message</param>
        /// <param name="reportToHandlers">Handlers to report to, All by default</param>
        [HideInCallstack]
        public static void LogWarning(ReportData reportData, object message, ReportHandler reportToHandlers = ReportHandler.All)
        {
            Instance.Log(LogType.Warning, reportData, message, null, reportToHandlers);
        }

        /// <summary>
        ///     Logs an error.
        /// </summary>
        /// <param name="reportData">Report Data, try to provide as specific data as possible</param>
        /// <param name="message">Message</param>
        /// <param name="reportToHandlers">Handlers to report to, All by default</param>
        [HideInCallstack]
        public static void LogError(ReportData reportData, object message, ReportHandler reportToHandlers = ReportHandler.All)
        {
            Instance.Log(LogType.Error, reportData, message, null, reportToHandlers);
        }

        /// <summary>
        ///     Logs verbose info.
        ///     This call will be stripped from the build, and therefore leads to zero overhead, unless "VERBOSE_LOGS" is defined
        /// </summary>
        /// <param name="reportData">Report Data, try to provide as specific data as possible</param>
        /// <param name="message">Message</param>
        /// <param name="reportToHandlers">Handlers to report to, All by default</param>
        [HideInCallstack]
        [Conditional("UNITY_EDITOR")] [Conditional("VERBOSE_LOGS")]
        public static void Log(ReportData reportData, object message, ReportHandler reportToHandlers = ReportHandler.All)
        {
            Instance.Log(LogType.Log, reportData, message, null, reportToHandlers);
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
        public static void Log(LogType logType, ReportData reportData, object message, Object context, ReportHandler reportToHandlers = ReportHandler.All)
        {
            Instance.Log(logType, reportData, message, context, reportToHandlers);
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
        public static void LogFormat(LogType logType, ReportData reportData, object message, ReportHandler reportHandler = ReportHandler.All, params object[] args)
        {
            Instance.LogFormat(logType, reportData, message, reportHandler, args);
        }

        /// <summary>
        ///     Logs a system exception message.
        /// </summary>
        /// <param name="ecsSystemException">ECS System Exception</param>
        /// <param name="reportHandler">Handlers to report to, All by default</param>
        [HideInCallstack]
        public static void LogException<T>(T ecsSystemException, ReportHandler reportHandler = ReportHandler.All) where T: Exception, IDecentralandException
        {
            Instance.LogException(ecsSystemException, reportHandler);
        }

        /// <summary>
        ///     Logs a common exception
        /// </summary>
        /// <param name="exception">Exception</param>
        /// <param name="reportData">Report Data, try to provide as specific data as possible</param>
        /// <param name="reportHandler">Handlers to report to, All by default</param>
        [HideInCallstack]
        public static void LogException(Exception exception, ReportData reportData, ReportHandler reportHandler = ReportHandler.All)
        {
            Instance.LogException(exception, reportData, reportHandler);
        }

        public static ReportedHub WithReport(ReportData reportData) =>
            new (reportData);

        public readonly struct ReportedHub
        {
            private readonly ReportData reportData;

            public ReportedHub(ReportData reportData)
            {
                this.reportData = reportData;
            }

            [HideInCallstack]
            public void Log(object message, ReportHandler reportToHandlers = ReportHandler.All)
            {
                ReportHub.Log(reportData, message, reportToHandlers);
            }

            [HideInCallstack]
            public void Log(string message)
            {
                ReportHub.Log(reportData, message);
            }
        }
    }
}
