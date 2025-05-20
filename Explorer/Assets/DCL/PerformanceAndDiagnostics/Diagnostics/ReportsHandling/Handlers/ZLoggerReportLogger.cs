using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using ZLogger;
using ZLogger.Unity;
using ILogger = Microsoft.Extensions.Logging.ILogger; // For ZLog extension methods if you use them
using Object = UnityEngine.Object;

namespace DCL.Diagnostics
{
    // TODO: extract this to a common place
    public static partial class ZLoggerReportHandlerExtensions
    {
        // 1) Pick the highest‐frequency combination of level + template
        // 2) Embed both your prefix and message as template parameters
        [ZLoggerMessage(LogLevel.Information,
            "{ReportDataPrefix}{Message}")]
        public static partial void LogReport(
            this ILogger logger,
            string ReportDataPrefix,
            string Message);

        // If you want to include context.name only on warnings/errors:
        [ZLoggerMessage(LogLevel.Warning,
            "[{Context}] {ReportDataPrefix}{Message}")]
        public static partial void LogReportWithContext(
            this ILogger logger,
            string Context,
            string ReportDataPrefix,
            string Message);
    }
    
    public class ZLoggerConsoleReportHandler : ReportHandlerBase
    {
        private ILogger zLogger;
        private static readonly string DEFAULT_COLOR = ColorUtility.ToHtmlStringRGB(new Color(189f / 255, 184f / 255, 172f / 255));

        // TODO: extract because it's used in other places
        private static readonly Dictionary<string, string> CATEGORY_COLORS = new ()
        {
            { ReportCategory.ASSETS_PROVISION, ColorUtility.ToHtmlStringRGB(new Color(0.616f, 0.875f, 0.89f)) },
            { ReportCategory.GENERIC_WEB_REQUEST, ColorUtility.ToHtmlStringRGB(new Color(0.902f, 0.886f, 0.082f)) },

            // Rooms
            { ReportCategory.COMMS_SCENE_HANDLER, ColorUtility.ToHtmlStringRGB(new Color(0.982f, 0.996f, 0.182f)) },
            { ReportCategory.LIVEKIT, ColorUtility.ToHtmlStringRGB(new Color(0.982f, 0.996f, 0.282f)) },
            { ReportCategory.SDK_LOCAL_SCENE_DEVELOPMENT, ColorUtility.ToHtmlStringRGB(new Color(0.982f, 0.996f, 0.282f)) },

            // Engine uses whitish tones
            { ReportCategory.ENGINE, ColorUtility.ToHtmlStringRGB(new Color(219f / 255, 214f / 255, 200f / 255)) },
            { ReportCategory.CRDT, ColorUtility.ToHtmlStringRGB(new Color(130f / 255, 148f / 255, 135f / 255)) },
            { ReportCategory.CRDT_ECS_BRIDGE, ColorUtility.ToHtmlStringRGB(new Color(130f / 255, 132f / 255, 148f / 255)) },

            // Scene Loading blueish
            { ReportCategory.SCENE_LOADING, ColorUtility.ToHtmlStringRGB(new Color(0.30f, 0.50f, 0.90f)) },
            { ReportCategory.SCENE_FACTORY, ColorUtility.ToHtmlStringRGB(new Color(0.15f, 0.35f, 0.75f)) },
            { ReportCategory.SCENE_UI, ColorUtility.ToHtmlStringRGB(new Color(0.10f, 0.30f, 0.45f)) },

            // JavaScript
            { ReportCategory.JAVASCRIPT, ColorUtility.ToHtmlStringRGB(new Color(0.90f, 0.30f, 0.35f)) },

            // ECS
            { ReportCategory.ECS, ColorUtility.ToHtmlStringRGB(new Color(0.99f, 0.61f, 0.15f)) },

            // Streamable Loading
            { ReportCategory.STREAMABLE_LOADING, ColorUtility.ToHtmlStringRGB(new Color(0.98f, 0.51f, 0.10f)) },
            { ReportCategory.TEXTURES, ColorUtility.ToHtmlStringRGB(new Color(0.98f, 0.51f, 0.10f)) },
            { ReportCategory.MATERIALS, ColorUtility.ToHtmlStringRGB(new Color(0.98f, 0.51f, 0.10f)) },
            { ReportCategory.GLTF_CONTAINER, ColorUtility.ToHtmlStringRGB(new Color(0.74f, 0.27f, 0.69f)) },
            { ReportCategory.ASSET_BUNDLES, ColorUtility.ToHtmlStringRGB(new Color(0.10f, 0.56f, 0.20f)) },
            { ReportCategory.PRIMITIVE_MESHES, ColorUtility.ToHtmlStringRGB(new Color(0.35f, 0.85f, 0.40f)) },
            { ReportCategory.PRIMITIVE_COLLIDERS, ColorUtility.ToHtmlStringRGB(new Color(0.35f, 0.85f, 0.40f)) },

            // Prioritisation
            { ReportCategory.PRIORITIZATION, ColorUtility.ToHtmlStringRGB(new Color(0.2f, 0.92f, 0.69f)) },

            { ReportCategory.MOTION, ColorUtility.ToHtmlStringRGB(new Color(0.792f, 0.463f, 0.812f)) },
            { ReportCategory.UI, ColorUtility.ToHtmlStringRGB(new Color(0.463f, 0.4f, 0.769f)) },
            { ReportCategory.INPUT, ColorUtility.ToHtmlStringRGB(new Color(0.776f, 0.851f, 0.357f)) },
            { ReportCategory.WEARABLE, ColorUtility.ToHtmlStringRGB(new Color(0.318f, 0.902f, 0.859f)) },
            { ReportCategory.RESTRICTED_ACTIONS, ColorUtility.ToHtmlStringRGB(new Color(0.42f, 0.92f, 0.63f)) },

            { ReportCategory.PLUGINS, ColorUtility.ToHtmlStringRGB(new Color(0.98f, 0.576f, 0.824f)) },
        };


        public ZLoggerConsoleReportHandler(
            ICategorySeverityMatrix matrix,
            bool debounceEnabled) : base(matrix, debounceEnabled)
        {
            zLogger = LoggerFactory.Create(logging =>
            {
                logging.SetMinimumLevel(LogLevel.Trace); // Or your desired level
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
                case LogType.Assert: return LogLevel.Critical; // Or Error, Assert is severe
                case LogType.Warning: return LogLevel.Warning;
                case LogType.Log: return LogLevel.Information;
                case LogType.Exception: return LogLevel.Error; // Exceptions are typically errors
                default: return LogLevel.Information;
            }
        }

        [HideInCallstack]
        internal override void LogInternal(LogType logType, ReportData reportData, Object context, object message)
        {
            // NOTE: Method one
            // var zLevel = MapLogTypeToZLogLevel(logType);
            // var prefix = GetReportDataPrefix(in reportData);
            // var msg = message?.ToString() ?? string.Empty;
            //
            // zLogger.ZLog(zLevel, $"{prefix}{msg}");
            
            // NOTE: method two
            var prefix = GetReportDataPrefix(in reportData);
            var msg = message?.ToString() ?? "";
            switch (logType)
            {
                case LogType.Warning:
                case LogType.Error:
                case LogType.Exception:
                    zLogger.LogReportWithContext(context?.name ?? "", prefix, msg);
                    break;

                default:
                    zLogger.LogReport(prefix, msg);
                    break;
            }
        }

        [HideInCallstack]
        internal override void LogFormatInternal(LogType logType, ReportData reportData, Object context, object message, params object[] args)
        {
            
        }

        [HideInCallstack]
        internal override void LogExceptionInternal<T>(T ecsSystemException) // where T : Exception, IDecentralandException
        {
            
        }

        [HideInCallstack]
        internal override void LogExceptionInternal(Exception exception, ReportData reportData, Object context)
        {
            
        }

        [HideInCallstack]
        private static string GetCategoryColor(in ReportData reportData) =>
            CATEGORY_COLORS.TryGetValue(reportData.Category, out string color) ? color : DEFAULT_COLOR;

        // NOTE: use ZStringBuilder instead of StringBuilder
        private static string GetReportDataPrefix(in ReportData reportData)
        {
            string color = GetCategoryColor(in reportData);
            var debugLogBuilder = new StringBuilder();
            debugLogBuilder.Append($"<color=#{color}>");
            debugLogBuilder.Append($"[{reportData.Category}]");

            if (reportData.SceneShortInfo.BaseParcel != Vector2Int.zero)
                debugLogBuilder.Append($" {reportData.SceneShortInfo.BaseParcel}");

            if (reportData.SceneTickNumber != null)
                debugLogBuilder.Append($" [T: {reportData.SceneTickNumber}]");

            debugLogBuilder.Append("</color>: ");
            return debugLogBuilder.ToString();
        }
    }
}