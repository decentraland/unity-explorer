using Microsoft.Extensions.Logging;
using System;
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
                logging.SetMinimumLevel(LogLevel.Trace);
                logging.AddZLoggerUnityDebug(options =>
                {
                    options.UsePlainTextFormatter();
                });
            }).CreateLogger("ZLoggerConsoleReportHandler");
        }

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

        [HideInCallstack]
        internal override void LogInternal(LogType logType, ReportData reportData, Object context, object message)
        {
            var prefix = GetReportDataPrefix(in reportData);
            var msg    = message as string ?? message?.ToString() ?? "";

            // NOTE: can be improved by using extension methods
            zLogger.ZLog(MapLogTypeToZLogLevel(logType), $"{prefix}{msg}", context);
        }

        [HideInCallstack]
        internal override void LogFormatInternal(LogType logType, ReportData reportData, Object context, object message, params object[] args)
        {
            var prefix = GetReportDataPrefix(in reportData);
            var fmt    = message?.ToString() ?? "";
            var text   = string.Format(fmt, args);

            // NOTE: can be improved by using extension methods
            zLogger.ZLog(MapLogTypeToZLogLevel(logType), $"{prefix}{text}", context);
        }

        [HideInCallstack]
        internal override void LogExceptionInternal<T>(T ecsSystemException)
        {
            var prefix = GetReportDataPrefix(in ecsSystemException.ReportData);
            zLogger.LogError(ecsSystemException, $"{prefix}{ecsSystemException.Message}");
        }

        [HideInCallstack]
        internal override void LogExceptionInternal(Exception exception, ReportData reportData, Object context)
        {
            var prefix = GetReportDataPrefix(in reportData);
            zLogger.LogError(exception, $"{prefix}{exception.Message}");
        }

        [HideInCallstack]
        private static string GetCategoryColor(in ReportData reportData) =>
            ReportsColorMap.GetColor(reportData.Category);

        /// <summary>
        /// GetReportDataPrefix is used to create a prefix for the report data.
        /// NOTE: use ZString StringBuilder to avoid allocations.
        /// </summary>
        /// <param name="reportData"></param>
        /// <returns></returns>
        private static string GetReportDataPrefix(in ReportData reportData)
        {
            string color = GetCategoryColor(in reportData);

            using (var sb = ZString.CreateStringBuilder())
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