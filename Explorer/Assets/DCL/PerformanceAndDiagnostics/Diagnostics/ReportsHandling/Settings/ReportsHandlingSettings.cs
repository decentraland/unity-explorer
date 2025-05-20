using System;
using UnityEngine;

namespace DCL.Diagnostics
{
    [CreateAssetMenu(fileName = "ReportsHandlingSettings", menuName = "DCL/Diagnostics/Reports Handling Settings")]
    public class ReportsHandlingSettings : ScriptableObject, IReportsHandlingSettings
    {
        [SerializeField] private CategorySeverityMatrix debugLogMatrix;
        [SerializeField] private CategorySeverityMatrix sentryMatrix;
        [SerializeField] private bool debounceEnabled;
        [SerializeField] private bool useOptimizedLogger;
        [SerializeField] private bool isSentryEnabled = true;
        [SerializeField] private bool isDebugLogEnabled = true;

        public bool DebounceEnabled => debounceEnabled;
        public bool UseOptimizedLogger => useOptimizedLogger;

        public bool IsEnabled(ReportHandler handler)
        {
            if ((handler & ReportHandler.Sentry) != 0)
                return isSentryEnabled;

            if ((handler & ReportHandler.DebugLog) != 0)
                return isDebugLogEnabled;

            return false;
        }

        public bool CategoryIsEnabled(string category, LogType logType) =>
            debugLogMatrix.IsEnabled(category, logType) || sentryMatrix.IsEnabled(category, logType);

        public ICategorySeverityMatrix GetMatrix(ReportHandler handler)
        {
            return handler switch
                   {
                       ReportHandler.Sentry => sentryMatrix,
                       ReportHandler.DebugLog => debugLogMatrix,
                       _ => throw new ArgumentOutOfRangeException(nameof(handler), handler, null),
                   };
        }
    }
}
