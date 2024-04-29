using DCL.Diagnostics.Sentry;
using System;
using System.Collections.Generic;
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
            Debug.Log($"DiagnosticsContainer.settings.NotifyErrorDebugLogDisabled");
            settings.NotifyErrorDebugLogDisabled();
            List<(ReportHandler, IReportHandler)> handlers = new (2);
            
            Debug.Log($"DiagnosticsContainer.settings.IsEnabled(ReportHandler.DebugLog): {settings.IsEnabled(ReportHandler.DebugLog)}");
            Debug.Log($"DiagnosticsContainer.settings.IsEnabled(ReportHandler.Sentry): {settings.IsEnabled(ReportHandler.Sentry)}");

            if (settings.IsEnabled(ReportHandler.DebugLog))
                handlers.Add((ReportHandler.DebugLog, new DebugLogReportHandler(Debug.unityLogger.logHandler, settings.GetMatrix(ReportHandler.DebugLog), settings.DebounceEnabled)));

            if (settings.IsEnabled(ReportHandler.Sentry))
                handlers.Add((ReportHandler.Sentry, new SentryReportHandler(settings.GetMatrix(ReportHandler.Sentry), settings.DebounceEnabled)));
            
            Debug.Log($"DiagnosticsContainer.ReportHubLogger.ctor");
            var logger = new ReportHubLogger(handlers);

            Debug.Log($"DiagnosticsContainer.Debug.unityLogger.logHandler");
            ILogHandler defaultLogHandler = Debug.unityLogger.logHandler;

            Debug.Log($"DiagnosticsContainer.Debug.unityLogger.logHandler.override");
            // Override Default Unity Logger
            Debug.unityLogger.logHandler = logger;

            Debug.Log($"DiagnosticsContainer.ReportHub.Instance");
            // Enable Hub static accessors
            ReportHub.Instance = logger;

            Debug.Log($"DiagnosticsContainer.return.DiagnosticsContainer.ctor");
            return new DiagnosticsContainer { ReportHubLogger = logger, defaultLogHandler = defaultLogHandler };
        }
    }
}
