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
        private const int DEFAULT_REPORT_HANDLERS_COUNT = 2; // DebugLog + Sentry

        private ILogHandler defaultLogHandler;
        public ReportHubLogger ReportHubLogger { get; private set; }

        public SentryReportHandler? Sentry { get; private set; }

        public void Dispose()
        {
            // Restore Default Unity Logger
            Debug.unityLogger.logHandler = defaultLogHandler;
        }

        public void AddSentryScopeConfigurator(SentryReportHandler.ConfigureScope configureScope)
        {
            Sentry?.AddScopeConfigurator(configureScope);
        }

        public static DiagnosticsContainer Create(IReportsHandlingSettings settings, bool enableSceneDebugConsole = false, params (ReportHandler, IReportHandler)[] additionalHandlers)
        {
            settings.NotifyErrorDebugLogDisabled();

            int handlersCount = DEFAULT_REPORT_HANDLERS_COUNT + additionalHandlers.Length + (enableSceneDebugConsole ? 1 : 0);
            List<(ReportHandler, IReportHandler)> handlers = new List<(ReportHandler, IReportHandler)>(handlersCount);
            handlers.AddRange(additionalHandlers);

            if (settings.IsEnabled(ReportHandler.DebugLog))
                handlers.Add((ReportHandler.DebugLog, new DebugLogReportHandler(Debug.unityLogger.logHandler, settings.GetMatrix(ReportHandler.DebugLog), settings.DebounceEnabled)));

            SentryReportHandler? sentryReportHandler = null;

            if (settings.IsEnabled(ReportHandler.Sentry))
                handlers.Add((ReportHandler.Sentry, sentryReportHandler = new SentryReportHandler(settings.GetMatrix(ReportHandler.Sentry), settings.DebounceEnabled)));

            if (enableSceneDebugConsole)
                AddLocalSceneReportingHandler(handlers);

            var logger = new ReportHubLogger(handlers);

            ILogHandler defaultLogHandler = Debug.unityLogger.logHandler;

            // Override Default Unity Logger
            Debug.unityLogger.logHandler = logger;

            // Enable Hub static accessors
            ReportHub.Initialize(logger, enableSceneDebugConsole);

            return new DiagnosticsContainer { ReportHubLogger = logger, defaultLogHandler = defaultLogHandler, Sentry = sentryReportHandler };
        }

        private static void AddLocalSceneReportingHandler(List<(ReportHandler, IReportHandler)> handlers)
        {
            var jsOnlyMatrix = new CategorySeverityMatrix();
            var entries = new List<CategorySeverityMatrix.Entry>();
            entries.Add(new CategorySeverityMatrix.Entry() { Category = ReportCategory.JAVASCRIPT, Severity = LogType.Error });
            entries.Add(new CategorySeverityMatrix.Entry() { Category = ReportCategory.JAVASCRIPT, Severity = LogType.Exception });
            entries.Add(new CategorySeverityMatrix.Entry() { Category = ReportCategory.JAVASCRIPT, Severity = LogType.Log });
            jsOnlyMatrix.entries = entries;
            handlers.Add((ReportHandler.DebugLog, new SceneDedicatedReportHandler(jsOnlyMatrix, false)));
        }
    }
}
