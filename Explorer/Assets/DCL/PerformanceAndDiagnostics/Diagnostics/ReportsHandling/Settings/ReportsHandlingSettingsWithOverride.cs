using System;
using UnityEngine;

namespace DCL.Diagnostics
{
    public class ReportsHandlingSettingsWithOverride : IReportsHandlingSettings
    {
        private readonly IReportsHandlingSettings baseSettings;
        private readonly ICategorySeverityMatrix? debugLogMatrixOverride;
        private readonly CategorySeverityMatrixOverride? sentryMatrixOverride;

        public ReportsHandlingSettingsWithOverride(IReportsHandlingSettings baseSettings, CategorySeverityMatrixDto? jsonOverride)
        {
            this.baseSettings = baseSettings;

            if (jsonOverride != null)
            {
                // Sentry is deliberately left untouched: everything goes to the log file only
                if (jsonOverride.allOverride)
                {
                    debugLogMatrixOverride = new AllEnabledCategorySeverityMatrix();
                    ReportHub.LogProductionInfo(LogMatrixConstants.LOG_MATRIX_ALL_OVERRIDE);
                }
                else if (jsonOverride.debugLogMatrix != null && jsonOverride.debugLogMatrix.Count > 0)
                {
                    debugLogMatrixOverride = new CategorySeverityMatrixOverride(
                        baseSettings.GetMatrix(ReportHandler.DebugLog), 
                        jsonOverride.debugLogMatrix,
                        jsonOverride.isOverride);
                }

                if (jsonOverride.sentryMatrix != null && jsonOverride.sentryMatrix.Count > 0)
                {
                    sentryMatrixOverride = new CategorySeverityMatrixOverride(
                        baseSettings.GetMatrix(ReportHandler.Sentry), 
                        jsonOverride.sentryMatrix,
                        jsonOverride.isOverride);
                }
            }
        }

        public bool DebounceEnabled => baseSettings.DebounceEnabled;

        public bool IsEnabled(ReportHandler handler) => baseSettings.IsEnabled(handler);

        public bool CategoryIsEnabled(string category, LogType logType) => baseSettings.CategoryIsEnabled(category, logType);

        public ICategorySeverityMatrix GetMatrix(ReportHandler handler)
        {
            return handler switch
            {
                ReportHandler.DebugLog => debugLogMatrixOverride ?? baseSettings.GetMatrix(ReportHandler.DebugLog),
                ReportHandler.Sentry => sentryMatrixOverride ?? baseSettings.GetMatrix(ReportHandler.Sentry),
                _ => throw new ArgumentOutOfRangeException(nameof(handler), handler, null)
            };
        }
    }
}
