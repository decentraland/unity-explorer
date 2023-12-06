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
}
