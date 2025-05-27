using Microsoft.Extensions.Logging;
using System;
using System.IO;
using Cysharp.Text;
using UnityEngine;
using ZLogger;
using ZLogger.Unity;
using ILogger = Microsoft.Extensions.Logging.ILogger;
using Object = UnityEngine.Object;

namespace DCL.Diagnostics
{
    public class ZLoggerReportLogger : ReportHandlerBase
    {
        private readonly ILogger zLogger;
        
        public ZLoggerReportLogger(
            ICategorySeverityMatrix matrix,
            bool debounceEnabled) : base(matrix, debounceEnabled)
        {
            
            zLogger = LoggerFactory.Create(logging =>
            {
                
#if !STANDALONE
                logging.SetMinimumLevel(LogLevel.Debug);
                logging.AddZLoggerUnityDebug(options =>
                {
                    options.PrettyStacktrace = false;
                });
#else
                logging.SetMinimumLevel(LogLevel.Debug);
                logging.AddZLoggerFile(Path.Combine(Application.persistentDataPath,"player2.log"), options =>
                {
                    options.UsePlainTextFormatter();
                    //options.InternalErrorLogger = exception => { };
                });

#endif
            }).CreateLogger("ZLoggerConsoleReportHandler");
        }

        [HideInCallstack]
        internal override void LogInternal(LogType logType, ReportData category, Object context, object message)
        {
            zLogger.ZLog(MapLogTypeToZLogLevel(logType), 
                $"{GetReportDataPrefix(in category)}{message}",
                context);
        }

        [HideInCallstack]
        internal override void LogFormatInternal(LogType logType, ReportData category, Object context, object message, params object[] args)
        {
            zLogger.ZLog(MapLogTypeToZLogLevel(logType),
                $"{GetReportDataPrefix(in category)}{message}", context);
        }

        [HideInCallstack]
        internal override void LogExceptionInternal<T>(T ecsSystemException)
        {
            ecsSystemException.MessagePrefix = GetReportDataPrefix(in ecsSystemException.ReportData);
            
#if !STANDALONE
            Debug.LogException(ecsSystemException, null);
#else
            // In standalone builds, we log the exception to the file logger
            zLogger.LogError(ecsSystemException, $"{GetReportDataPrefix(in ecsSystemException.ReportData)}");
#endif
            ecsSystemException.MessagePrefix = null;
        }

        [HideInCallstack]
        internal override void LogExceptionInternal(Exception exception, ReportData reportData, Object context)
        {
            if (exception is EcsSystemException ecsSystemException)
            {
                LogExceptionInternal(ecsSystemException);
                return;
            }
            
#if !STANDALONE
            Debug.LogException(exception, context);
#else
            // In standalone builds, we log the exception to the file logger
            zLogger.ZLogError(exception, $"{GetReportDataPrefix(in reportData)}", context);
#endif
        }

        [HideInCallstack]
        private static string GetCategoryColor(in ReportData reportData) =>
            ReportsColorMap.GetColor(reportData.Category);

        [HideInCallstack]
        private LogLevel MapLogTypeToZLogLevel(LogType unityLogType)
        {
            switch (unityLogType)
            {
                case LogType.Error: return LogLevel.Error;
                case LogType.Assert: return LogLevel.Critical;
                case LogType.Warning: return LogLevel.Warning;
                case LogType.Log: return LogLevel.Information;
                case LogType.Exception: return LogLevel.Error;
                default: return LogLevel.Information;
            }
        }
        
        /// <summary>
        /// GetReportDataPrefix is used to create a prefix for the report data.
        /// NOTE: use ZString StringBuilder to avoid allocations.
        /// </summary>
        /// <param name="reportData"></param>
        /// <returns></returns>
        [HideInCallstack]
        private static string GetReportDataPrefix(in ReportData reportData)
        {
            string color = GetCategoryColor(in reportData);

            using (var sb = ZString.CreateUtf8StringBuilder())
            {
                sb.Append($"<color=#{color}>");
                sb.Append($"[{reportData.Category}]");

                if (reportData.SceneShortInfo.BaseParcel != Vector2Int.zero)
                    sb.Append($" {reportData.SceneShortInfo.BaseParcel}");

                if (reportData.SceneTickNumber != null)
                    sb.Append($" [T: {reportData.SceneTickNumber}]");

                sb.Append("</color>: ");
                return sb.ToString();
            }
        }
    }
}