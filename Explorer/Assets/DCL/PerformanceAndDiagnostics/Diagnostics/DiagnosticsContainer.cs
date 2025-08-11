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

        private ILogHandler defaultLogHandler;
        public ReportHubLogger ReportHubLogger { get; private set; }
        public SentryReportHandler? Sentry { get; private set; }
        public static DebugMenuConsoleLogEntryBus? SceneConsoleLogEntryBus { get; private set; }

        public void Dispose()
        {
            // Restore Default Unity Logger
            Debug.unityLogger.logHandler = defaultLogHandler;
        }

        public void AddSentryScopeConfigurator(SentryReportHandler.ConfigureScope configureScope)
        {
            Sentry?.AddScopeConfigurator(configureScope);
        }

        public static DiagnosticsContainer Create(IReportsHandlingSettings settings, bool enableSceneDebugConsole = false, params IReportHandler[] additionalHandlers)
        {
            settings.NotifyErrorDebugLogDisabled();

            int handlersCount = DEFAULT_REPORT_HANDLERS_COUNT + additionalHandlers.Length + (enableSceneDebugConsole ? 1 : 0);
            List<IReportHandler> handlers = new (handlersCount);
            handlers.AddRange(additionalHandlers);

            if (settings.IsEnabled(ReportHandler.DebugLog))
                handlers.Add(new DebugLogReportHandler(Debug.unityLogger.logHandler, settings.GetMatrix(ReportHandler.DebugLog), settings.DebounceEnabled));

            SentryReportHandler? sentryReportHandler = null;

            if (settings.IsEnabled(ReportHandler.Sentry))
                handlers.Add(sentryReportHandler = new SentryReportHandler(settings.GetMatrix(ReportHandler.Sentry), settings.DebounceEnabled));

            if (enableSceneDebugConsole)
                AddSceneDebugConsoleReportHandler(handlers);

            var logger = new ReportHubLogger(handlers);

            ILogHandler defaultLogHandler = Debug.unityLogger.logHandler;

            // Override Default Unity Logger
            Debug.unityLogger.logHandler = logger;

            // Enable Hub static accessors
            ReportHub.Initialize(logger, enableSceneDebugConsole);

            return new DiagnosticsContainer { ReportHubLogger = logger, defaultLogHandler = defaultLogHandler, Sentry = sentryReportHandler };
        }

        private static void AddSceneDebugConsoleReportHandler(List<IReportHandler> handlers)
        {
            SceneConsoleLogEntryBus = new DebugMenuConsoleLogEntryBus();
            var jsOnlyMatrix = new CategorySeverityMatrix();

            var entries = GetMatrixEntriesList(
                    new []
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
                    }, logType: false);
            entries.Add(new () { Category = ReportCategory.JAVASCRIPT, Severity = LogType.Log });

            jsOnlyMatrix.entries = entries;
            handlers.Add((new SceneDebugConsoleReportHandler(jsOnlyMatrix, SceneConsoleLogEntryBus, false)));
        }

        private static List<CategorySeverityMatrix.Entry> GetMatrixEntriesList(string[] reportCategories, bool errorType = true, bool exceptionType = true, bool logType = true)
        {
            var entries = new List<CategorySeverityMatrix.Entry>();

            for (var i = 0; i < reportCategories.Length; i++)
            {
                if (errorType)
                    entries.Add(new () { Category = reportCategories[i], Severity = LogType.Error });
                if(exceptionType)
                    entries.Add(new () { Category = reportCategories[i], Severity = LogType.Exception });
                if(logType)
                    entries.Add(new () { Category = reportCategories[i], Severity = LogType.Log });
            }

            return entries;
        }
    }
}
