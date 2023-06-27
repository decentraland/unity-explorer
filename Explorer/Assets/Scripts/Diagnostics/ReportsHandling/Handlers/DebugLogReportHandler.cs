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
        };

        // Redirect Logs to the default Unity logger
        private readonly ILogHandler unityLogHandler;

        public DebugLogReportHandler(ILogHandler unityLogHandler, ICategorySeverityMatrix matrix, bool debounceEnabled) : base(matrix, debounceEnabled)
        {
            this.unityLogHandler = unityLogHandler;
        }

        protected override void LogInternal(LogType logType, ReportData category, Object context, object message)
        {
            string color = GetCategoryColor(in category);
            unityLogHandler.LogFormat(logType, context, $"<color=#{color}>[{category.Category}]</color>: {message}");
        }

        protected override void LogFormatInternal(LogType logType, ReportData category, Object context, object message, params object[] args)
        {
            string color = GetCategoryColor(in category);
            unityLogHandler.LogFormat(logType, context, $"<color=#{color}>[{category.Category}]</color>: {message}", args);
        }

        protected override void LogExceptionInternal(EcsSystemException ecsSystemException)
        {
            unityLogHandler.LogException(ecsSystemException.InnerException, null);
        }

        protected override void LogExceptionInternal(Exception exception, Object context)
        {
            unityLogHandler.LogException(exception, context);
        }

        private static string GetCategoryColor(in ReportData reportData) =>
            CATEGORY_COLORS.TryGetValue(reportData.Category, out string color) ? color : DEFAULT_COLOR;
    }
}
