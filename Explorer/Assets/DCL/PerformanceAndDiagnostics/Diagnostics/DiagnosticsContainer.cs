using DCL.Diagnostics.Sentry;
using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using UnityEngine;
using ZLogger;
using ZLogger.Providers;
using ZLogger.Unity;

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
            // NOTE: cleanup
            var container = new DiagnosticsContainer();
            settings.NotifyErrorDebugLogDisabled();

            List<(ReportHandler, IReportHandler)> handlers = new List<(ReportHandler, IReportHandler)>();
            handlers.AddRange(additionalHandlers);

            if (settings.IsEnabled(ReportHandler.DebugLog))
            {
                if (!settings.UseOptimizedLogger)
                {
                    handlers.Add((ReportHandler.DebugLog, new DebugLogReportHandler(Debug.unityLogger.logHandler, settings.GetMatrix(ReportHandler.DebugLog), settings.DebounceEnabled)));
                }
                else
                {
                    handlers.Add((
                        ReportHandler.DebugLog,
                        new ZLoggerReportLogger(settings.GetMatrix(ReportHandler.DebugLog),
                            settings.DebounceEnabled)
                    ));
                }
            }
            
            SentryReportHandler? sentryReportHandler = null;

            if (settings.IsEnabled(ReportHandler.Sentry))
                handlers.Add((ReportHandler.Sentry, sentryReportHandler = new SentryReportHandler(settings.GetMatrix(ReportHandler.Sentry), settings.DebounceEnabled)));

            if (enableSceneDebugConsole)
                AddSceneDebugConsoleReportHandler(handlers);
            
            var reportHubInstance = new ReportHubLogger(handlers);
            container.defaultLogHandler = Debug.unityLogger.logHandler;
            //Debug.unityLogger.logHandler = reportHubInstance; // Override with our hub

            ReportHub.Initialize(reportHubInstance, enableSceneDebugConsole);

            container.ReportHubLogger = reportHubInstance;
            container.Sentry = sentryReportHandler;
            return container;
        }

        private static void AddSceneDebugConsoleReportHandler(List<(ReportHandler, IReportHandler)> handlers)
        {
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
            handlers.Add((ReportHandler.DebugLog, new SceneDebugConsoleReportHandler(jsOnlyMatrix, false)));
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
