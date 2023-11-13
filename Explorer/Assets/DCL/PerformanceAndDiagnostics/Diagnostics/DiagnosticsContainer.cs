using System;
using UnityEngine;

namespace DCL.Diagnostics
{
    /// <summary>
    ///     Holds diagnostics dependencies that can be shared between different systems
    /// </summary>
    public class DiagnosticsContainer : IDisposable
    {
        private ILogHandler defaultLogHandler;
        public ReportHubLogger ReportHubLogger { get; private set; }

        public void Dispose()
        {
            // Restore Default Unity Logger
            Debug.unityLogger.logHandler = defaultLogHandler;
        }

        public static DiagnosticsContainer Create(IReportsHandlingSettings settings)
        {
            var logger = new ReportHubLogger(new (ReportHandler, IReportHandler)[]
            {
                (ReportHandler.DebugLog, new DebugLogReportHandler(Debug.unityLogger.logHandler, settings.GetMatrix(ReportHandler.DebugLog), settings.DebounceEnabled)),

                // Insert Sentry Logger when implemented
            });

            ILogHandler defaultLogHandler = Debug.unityLogger.logHandler;

            // Override Default Unity Logger
            Debug.unityLogger.logHandler = logger;

            // Enable Hub static accessors
            ReportHub.Instance = logger;

            return new DiagnosticsContainer { ReportHubLogger = logger, defaultLogHandler = defaultLogHandler };
        }
    }
}
