using DCL.Diagnostics.Sentry;
using DCL.UI.DebugMenu.MessageBus;
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

        private readonly ILogHandler defaultLogHandler;
        private readonly ReportHubLogger reportHubLogger;

        public SentryReportHandler? Sentry { get; }
        public IReportsHandlingSettings Settings { get; }
        public SentrySampler? SentrySampler { get; }

        private DiagnosticsContainer(ILogHandler defaultLogHandler, ReportHubLogger reportHubLogger, SentryReportHandler? sentry, IReportsHandlingSettings settings, SentrySampler? sentrySampler)
        {
            this.defaultLogHandler = defaultLogHandler;
            this.reportHubLogger = reportHubLogger;
            Sentry = sentry;
            Settings = settings;
            SentrySampler = sentrySampler;
        }

        public void Dispose()
        {
            // Restore Default Unity Logger
            Debug.unityLogger.logHandler = defaultLogHandler;
        }

        public void AddSentryScopeConfigurator(SentryReportHandler.ConfigureScope configureScope)
        {
            Sentry?.AddScopeConfigurator(configureScope);
        }

        public static DiagnosticsContainer Create(IReportsHandlingSettings settings, params IReportHandler[] additionalHandlers)
        {
            settings.NotifyErrorDebugLogDisabled();

            int handlersCount = DEFAULT_REPORT_HANDLERS_COUNT + additionalHandlers.Length;
            List<IReportHandler> handlers = new (handlersCount);
            handlers.AddRange(additionalHandlers);

            if (settings.IsEnabled(ReportHandler.DebugLog))
                handlers.Add(new DebugLogReportHandler(Debug.unityLogger.logHandler, settings.GetMatrix(ReportHandler.DebugLog), settings.DebounceEnabled));

            SentryReportHandler? sentryReportHandler = null;
            SentrySampler? sentrySampler = null;

            if (settings.IsEnabled(ReportHandler.Sentry))
                handlers.Add(sentryReportHandler = new SentryReportHandler(settings.GetMatrix(ReportHandler.Sentry), sentrySampler = new SentrySampler(), settings.DebounceEnabled));

            var logger = new ReportHubLogger(handlers);

            ILogHandler defaultLogHandler = Debug.unityLogger.logHandler;

            // Override Default Unity Logger
            Debug.unityLogger.logHandler = logger;

            // Enable Hub static accessors
            ReportHub.Initialize(logger);

            return new DiagnosticsContainer(defaultLogHandler, logger, sentryReportHandler, settings, sentrySampler);
        }

        public void AddDebugConsoleHandler(DebugMenuConsoleLogEntryBus sceneDebugConsoleMessageBus)
        {
            SceneDebugConsoleReportHandler reportHandler = AddDebugConsoleReportHandler(sceneDebugConsoleMessageBus);
            ReportHub.EnforceUnconditionalVerboseLogs = true;
            reportHubLogger.AddHandler(reportHandler);
        }

        private static SceneDebugConsoleReportHandler AddDebugConsoleReportHandler(DebugMenuConsoleLogEntryBus sceneDebugConsoleMessageBus)
        {
            var jsOnlyMatrix = new CategorySeverityMatrix();

            List<CategorySeverityMatrix.Entry> entries = GetMatrixEntriesList(
                new[]
                {
                    ReportCategory.JAVASCRIPT,
                    ReportCategory.UNSPECIFIED,
                    ReportCategory.PLAYER_SDK_DATA,
                    ReportCategory.AVATAR,
                    ReportCategory.GLTF_CONTAINER,
                    ReportCategory.PRIMITIVE_COLLIDERS,
                    ReportCategory.PRIMITIVE_MESHES,
                    ReportCategory.NFT_INFO_WEB_REQUEST,
                    ReportCategory.NFT_SHAPE_WEB_REQUEST,
                    ReportCategory.MATERIALS,
                    ReportCategory.ANIMATOR,
                    ReportCategory.SCENE_UI,
                    ReportCategory.INPUT,
                    ReportCategory.MEDIA_STREAM,
                    ReportCategory.CHARACTER_TRIGGER_AREA,
                    ReportCategory.SDK_AUDIO_SOURCES,
                    ReportCategory.TWEEN,
                    ReportCategory.AVATAR_ATTACH,
                    ReportCategory.SDK_CAMERA,
                    ReportCategory.LIGHT_SOURCE,
                    ReportCategory.REALM,
                    ReportCategory.HIGHLIGHTS,
                    ReportCategory.GENERIC_WEB_REQUEST,
                    ReportCategory.TEXTURE_WEB_REQUEST,
                    ReportCategory.AUDIO_CLIP_WEB_REQUEST,
                    ReportCategory.TEXTURES,
                    ReportCategory.RESTRICTED_ACTIONS,
                    ReportCategory.SDK_OBSERVABLES,
                    ReportCategory.LIVEKIT,
                    ReportCategory.SCENE_FETCH_REQUEST,
                    ReportCategory.PORTABLE_EXPERIENCE,
                    ReportCategory.EMOTE,
                    ReportCategory.MCP,
                }, logType: false);

            entries.Add(new CategorySeverityMatrix.Entry { Category = ReportCategory.JAVASCRIPT, Severity = LogType.Log });
            entries.Add(new CategorySeverityMatrix.Entry { Category = ReportCategory.MCP, Severity = LogType.Log });

            jsOnlyMatrix.entries = entries;
            return new SceneDebugConsoleReportHandler(jsOnlyMatrix, sceneDebugConsoleMessageBus, false);
        }

        private static List<CategorySeverityMatrix.Entry> GetMatrixEntriesList(string[] reportCategories, bool errorType = true, bool exceptionType = true, bool logType = true)
        {
            var entries = new List<CategorySeverityMatrix.Entry>();

            for (var i = 0; i < reportCategories.Length; i++)
            {
                if (errorType)
                    entries.Add(new CategorySeverityMatrix.Entry { Category = reportCategories[i], Severity = LogType.Error });

                if (exceptionType)
                    entries.Add(new CategorySeverityMatrix.Entry { Category = reportCategories[i], Severity = LogType.Exception });

                if (logType)
                    entries.Add(new CategorySeverityMatrix.Entry { Category = reportCategories[i], Severity = LogType.Log });
            }

            return entries;
        }
    }
}
