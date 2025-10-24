using System;
using UnityEngine;

namespace DCL.Diagnostics
{
    public class ReportsHandlingSettingsWithOverride : IReportsHandlingSettings
    {
        private readonly IReportsHandlingSettings baseSettings;
        private readonly CategorySeverityMatrixOverride? debugLogMatrixOverride;
        private readonly CategorySeverityMatrixOverride? sentryMatrixOverride;

        public ReportsHandlingSettingsWithOverride(IReportsHandlingSettings baseSettings, CategorySeverityMatrixDto? jsonOverride)
        {
            this.baseSettings = baseSettings;

            if (jsonOverride != null)
            {
                if (jsonOverride.debugLogMatrix != null && jsonOverride.debugLogMatrix.Count > 0)
                {
                    debugLogMatrixOverride = new CategorySeverityMatrixOverride(
                        baseSettings.GetMatrix(ReportHandler.DebugLog), 
                        jsonOverride.debugLogMatrix);
                }

                if (jsonOverride.sentryMatrix != null && jsonOverride.sentryMatrix.Count > 0)
                {
                    sentryMatrixOverride = new CategorySeverityMatrixOverride(
                        baseSettings.GetMatrix(ReportHandler.Sentry), 
                        jsonOverride.sentryMatrix);
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
