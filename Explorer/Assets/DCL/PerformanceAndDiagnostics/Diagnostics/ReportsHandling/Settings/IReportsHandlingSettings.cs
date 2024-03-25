using System.Linq;
using System.Reflection;
using UnityEngine;

namespace DCL.Diagnostics
{
    public interface IReportsHandlingSettings
    {
        /// <summary>
        ///     If true, reports will be debounced on a higher level
        /// </summary>
        bool DebounceEnabled { get; }

        bool IsEnabled(ReportHandler handler);

        /// <summary>
        ///     Category is enabled for any reporter
        /// </summary>
        bool CategoryIsEnabled(string category, LogType logType);

        ICategorySeverityMatrix GetMatrix(ReportHandler handler);
    }

    public static class ReportsHandlingSettingsExtensions
    {
        public static void NotifyErrorDebugLogDisabled(this IReportsHandlingSettings reportsHandlingSettings)
        {
            var matrix = reportsHandlingSettings.GetMatrix(ReportHandler.DebugLog);

            var categories = typeof(ReportCategory)
                            .GetFields(BindingFlags.Static | BindingFlags.Public)
                            .Where(f => f.FieldType == typeof(string))
                            .Select(f => f.GetValue(null!)!.ToString());

            var disabledCategories = categories
                                    .Where(c =>
                                         matrix.IsEnabled(c!, LogType.Error) == false
                                         || matrix.IsEnabled(c!, LogType.Exception) == false
                                     )
                                    .ToList();

            if (disabledCategories.Count == 0)
                return;

            ReportHub.Log(
                ReportData.UNSPECIFIED,
                $"Keep in mind - error/exception log are disabled for: {string.Join(", ", disabledCategories)}"
            );
        }
    }
}
