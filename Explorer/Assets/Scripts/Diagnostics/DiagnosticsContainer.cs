using Diagnostics.ReportsHandling;
using UnityEngine;

namespace Diagnostics
{
    /// <summary>
    ///     Holds diagnostics dependencies that can be shared between different systems
    /// </summary>
    public class DiagnosticsContainer
    {
        public ReportHubLogger ReportHubLogger { get; private set; }

        public static DiagnosticsContainer Create(IReportsHandlingSettings settings)
        {
            var logger = new ReportHubLogger(new (ReportHandler, IReportHandler)[]
            {
                (ReportHandler.DebugLog, new DebugLogReportHandler(Debug.unityLogger.logHandler, settings.GetMatrix(ReportHandler.DebugLog), settings.DebounceEnabled)),

                // Insert Sentry Logger when implemented
            });

            // Override Default Unity Logger
            Debug.unityLogger.logHandler = logger;

            // Enable Hub static accessors
            ReportHub.Instance = logger;

            return new DiagnosticsContainer { ReportHubLogger = logger };
        }
    }
}
