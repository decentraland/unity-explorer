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

        public static DiagnosticsContainer Create(IReportsHandlingSettings settings, params (ReportHandler, IReportHandler)[] additionalHandlers)
        {
            settings.NotifyErrorDebugLogDisabled();

            List<(ReportHandler, IReportHandler)> handlers = new List<(ReportHandler, IReportHandler)>(additionalHandlers.Length + 2);
            handlers.AddRange(additionalHandlers);

            if (settings.IsEnabled(ReportHandler.DebugLog))
                handlers.Add((ReportHandler.DebugLog, new DebugLogReportHandler(Debug.unityLogger.logHandler, settings.GetMatrix(ReportHandler.DebugLog), settings.DebounceEnabled)));

            if (settings.IsEnabled(ReportHandler.Sentry))
                handlers.Add((ReportHandler.Sentry, new SentryReportHandler(settings.GetMatrix(ReportHandler.Sentry), settings.DebounceEnabled)));

            var logger = new ReportHubLogger(handlers);

            ILogHandler defaultLogHandler1 = Debug.unityLogger.logHandler;

            // Override Default Unity Logger
            Debug.unityLogger.logHandler = logger;

            // Enable Hub static accessors
            ReportHub.Instance = logger;

            return new DiagnosticsContainer { ReportHubLogger = logger, defaultLogHandler = defaultLogHandler1 };
        }
    }
}
