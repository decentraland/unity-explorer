using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Diagnostics.ReportsHandling
{
    /// <summary>
    ///     Enriches and redirects logs to the default Unity logger
    /// </summary>
    public class DebugLogReportHandler : ReportHandlerBase
    {
        private static readonly string DEFAULT_COLOR = ColorUtility.ToHtmlStringRGB(new Color(189f / 255, 184f / 255, 172f / 255));

        private static readonly Dictionary<string, string> CATEGORY_COLORS = new ()
        {
            // Engine uses whitish tones
            { ReportCategory.ENGINE, ColorUtility.ToHtmlStringRGB(new Color(219f / 255, 214f / 255, 200f / 255)) },
            { ReportCategory.CRDT, ColorUtility.ToHtmlStringRGB(new Color(130f / 255, 148f / 255, 135f / 255)) },
            { ReportCategory.CRDT_ECS_BRIDGE, ColorUtility.ToHtmlStringRGB(new Color(130f / 255, 132f / 255, 148f / 255)) },

            // Scene Loading blueish
            { ReportCategory.SCENE_LOADING, ColorUtility.ToHtmlStringRGB(new Color(0.30f, 0.50f, 0.90f)) },
            { ReportCategory.SCENE_FACTORY, ColorUtility.ToHtmlStringRGB(new Color(0.15f, 0.35f, 0.75f)) },

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
        };

        // Redirect Logs to the default Unity logger
        private readonly ILogHandler unityLogHandler;

        public DebugLogReportHandler(ILogHandler unityLogHandler, ICategorySeverityMatrix matrix, bool debounceEnabled) : base(matrix, debounceEnabled)
        {
            this.unityLogHandler = unityLogHandler;
        }

        [HideInCallstack]
        internal override void LogInternal(LogType logType, ReportData category, Object context, object message)
        {
            unityLogHandler.LogFormat(logType, context, $"{GetReportDataPrefix(in category)}{message}");
        }

        [HideInCallstack]
        internal override void LogFormatInternal(LogType logType, ReportData category, Object context, object message, params object[] args)
        {
            unityLogHandler.LogFormat(logType, context, $"{GetReportDataPrefix(in category)}{message}", args);
        }

        [HideInCallstack]
        internal override void LogExceptionInternal<T>(T ecsSystemException)
        {
            // Can't override exceptions logging because Unity controls the stack trace, and how it is printed natively
            // But we have a custom exception type so signal it to log with a prefix
            ecsSystemException.MessagePrefix = GetReportDataPrefix(in ecsSystemException.ReportData);

            unityLogHandler.LogException(ecsSystemException, null);

            // Reset the hack
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

            // Can't override exceptions logging because Unity controls the stack trace, and how it is printed natively
            // So log a category as a separate entry
            unityLogHandler.LogFormat(LogType.Exception, context, GetReportDataPrefix(in reportData));
            unityLogHandler.LogException(exception, context);
        }

        [HideInCallstack]
        private static string GetCategoryColor(in ReportData reportData) =>
            CATEGORY_COLORS.TryGetValue(reportData.Category, out string color) ? color : DEFAULT_COLOR;

        private static string GetReportDataPrefix(in ReportData reportData)
        {
            string color = GetCategoryColor(in reportData);

            if (reportData.SceneShortInfo.BaseParcel != Vector2Int.zero)
                return $"<color=#{color}>[{reportData.Category}] {reportData.SceneShortInfo.BaseParcel}</color>: ";

            return $"<color=#{color}>[{reportData.Category}]</color>: ";
        }
    }
}
