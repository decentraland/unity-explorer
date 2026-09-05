using System;
using UnityEngine;

namespace DCL.Diagnostics
{
    public class RuntimeReportsHandlingSettings : IReportsHandlingSettings
    {
        private readonly IReportsHandlingSettings baseSettings;
        private readonly RuntimeLogMatrix debugLogMatrix;
        private readonly RuntimeLogMatrix sentryMatrix;

        public RuntimeReportsHandlingSettings(IReportsHandlingSettings baseSettings)
        {
            this.baseSettings = baseSettings;
            this.debugLogMatrix = new RuntimeLogMatrix(baseSettings.GetMatrix(ReportHandler.DebugLog));
            this.sentryMatrix = new RuntimeLogMatrix(baseSettings.GetMatrix(ReportHandler.Sentry));
        }

        public bool DebounceEnabled => baseSettings.DebounceEnabled;

        public bool IsEnabled(ReportHandler handler) => baseSettings.IsEnabled(handler);

        public bool CategoryIsEnabled(string category, LogType logType) => baseSettings.CategoryIsEnabled(category, logType);

        public ICategorySeverityMatrix GetMatrix(ReportHandler handler)
        {
            return handler switch
            {
                ReportHandler.DebugLog => debugLogMatrix,
                ReportHandler.Sentry => sentryMatrix,
                _ => throw new ArgumentOutOfRangeException(nameof(handler), handler, null)
            };
        }

        public RuntimeLogMatrix GetDebugLogMatrix() => debugLogMatrix;
        public RuntimeLogMatrix GetSentryMatrix() => sentryMatrix;
    }
}
