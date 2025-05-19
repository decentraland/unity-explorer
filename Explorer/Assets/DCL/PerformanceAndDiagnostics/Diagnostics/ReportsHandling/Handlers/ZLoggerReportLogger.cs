using UnityEngine;
using System;
using ZLogger;
using Object = UnityEngine.Object;

//
// namespace DCL.Diagnostics
// {
//     public class ZLoggerReportHandler : IReportHandler
//     {
//         private readonly Microsoft.Extensions.Logging.ILogger _zLogger;
//
//         public ZLoggerReportHandler(Microsoft.Extensions.Logging.ILogger zLogger)
//         {
//             _zLogger = zLogger ?? throw new ArgumentNullException(nameof(zLogger));
//         }
//
//         private static Microsoft.Extensions.Logging.LogLevel ToZLoggerLogLevel(LogType logType)
//         {
//             switch (logType)
//             {
//                 case LogType.Error: return Microsoft.Extensions.Logging.LogLevel.Error;
//                 case LogType.Assert: return Microsoft.Extensions.Logging.LogLevel.Critical;
//                 case LogType.Warning: return Microsoft.Extensions.Logging.LogLevel.Warning;
//                 case LogType.Log: return Microsoft.Extensions.Logging.LogLevel.Information;
//                 case LogType.Exception: return Microsoft.Extensions.Logging.LogLevel.Error; // Or Critical
//                 default: return Microsoft.Extensions.Logging.LogLevel.Debug;
//             }
//         }
//
//         public void Log(LogType logType, ReportData reportData, Object context, object messageObj)
//         {
//             var zLogLevel = ToZLoggerLogLevel(logType);
//             if (!_zLogger.IsEnabled(zLogLevel)) return;
//
//             // Use ZLogger's structured logging. Pass ReportData and context info as parameters.
//             // The 'Category' from ReportData is a good candidate for a structured field.
//             // If messageObj is already a string, it will be logged as such.
//             // If it's another type, ZLogger will attempt to serialize it (or you can call .ToString()).
//             if (context != null)
//             {
//                 _zLogger.ZLog(zLogLevel, "[{Category}][Ctx:{ContextName}] {Message}",
//                     reportData.Category ?? "NULL_CATEGORY",
//                     context.name,
//                     messageObj);
//             }
//             else
//             {
//                 _zLogger.ZLog(zLogLevel, "[{Category}] {Message}",
//                     reportData.Category ?? "NULL_CATEGORY",
//                     messageObj);
//             }
//         }
//
//         // Inside ZLoggerReportHandler
//         public void LogFormat(LogType logType, ReportData reportData, Object context, object message, params object[] args)
//         {
//             var zLogLevel = ToZLoggerLogLevel(logType);
//             if (!_zLogger.IsEnabled(zLogLevel)) return;
//
//             string formatString = message?.ToString() ?? "null";
//
//             // Using ZLogger's scope to add contextual information without mangling the format string and args
//             // This is good for structured logging where "Category" and "ContextName" become separate fields.
//             using (_zLogger.BeginScope("Category: {Category}, ContextName: {ContextName}",
//                        reportData.Category ?? "UNSPECIFIED",
//                        context != null ? context.name : "N/A"))
//             {
//                 if (args != null && args.Length > 0)
//                 {
//                     // _zLogger.Log(zLogLevel, formatString, args); // This is the standard MEL call
//                     // For ZLogger's specific formatting with ZLog (if formatString is a ZLogger template):
//                     _zLogger.ZLog(zLogLevel, formatString, args);
//                 }
//                 else
//                 {
//                     _zLogger.ZLog(zLogLevel, formatString); // Log as a simple message
//                 }
//             }
//         }
//
//         // Overload for when ReportHubLogger (as ILogHandler) calls. It won't have ReportData.
//         public void LogFormat(LogType logType, Object context, string format, params object[] args)
//         {
//             LogFormat(logType, ReportData.UNSPECIFIED, context, format, args);
//         }
//
//         public void LogException<T>(T ecsSystemException) where T : Exception, IDecentralandException
//         {
//             if (!_zLogger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Error)) return;
//
//             string category = "EXCEPTION"; // Default category for exceptions
//             // If your IDecentralandException has ReportData or similar, extract it:
//             // if (ecsSystemException is IMyExceptionWithReportData withData) category = withData.ReportData.Category;
//
//             _zLogger.ZLogError(ecsSystemException, "[{Category}] Decentraland Exception: {ExceptionType}",
//                 category,
//                 typeof(T).Name);
//         }
//
//         public void LogException(Exception exception, ReportData reportData, Object context)
//         {
//             if (!_zLogger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Error)) return;
//
//             if (context != null)
//             {
//                 _zLogger.ZLogError(exception, "[{Category}][Ctx:{ContextName}] Exception: {ExceptionType}",
//                     reportData.Category ?? "NULL_CATEGORY",
//                     context.name,
//                     exception.GetType().Name);
//             }
//             else
//             {
//                 _zLogger.ZLogError(exception, "[{Category}] Exception: {ExceptionType}",
//                     reportData.Category ?? "NULL_CATEGORY",
//                     exception.GetType().Name);
//             }
//         }
//
//         // Overload for when ReportHubLogger (as ILogHandler) calls. It won't have ReportData.
//         public void LogException(Exception exception, Object context)
//         {
//             LogException(exception, ReportData.UNSPECIFIED, context);
//         }
//     }
// }