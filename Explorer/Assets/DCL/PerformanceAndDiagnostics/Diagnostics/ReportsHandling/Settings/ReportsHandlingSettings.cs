using System;
using UnityEngine;

namespace DCL.Diagnostics
{
    [CreateAssetMenu(menuName = "Create Reports Handling Settings", fileName = "ReportsHandlingSettings", order = 0)]
    public class ReportsHandlingSettings : ScriptableObject, IReportsHandlingSettings
    {
        [SerializeField] private CategorySeverityMatrix debugLogMatrix;
        [SerializeField] private CategorySeverityMatrix sentryMatrix;
        [SerializeField] private bool debounceEnabled;
        [SerializeField] private bool isSentryEnabled = true;
        [SerializeField] private bool isDebugLogEnabled = true;

        public bool DebounceEnabled => debounceEnabled;

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
