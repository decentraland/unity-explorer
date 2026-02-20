using Cysharp.Threading.Tasks;
using DCL.DebugUtilities;
using DCL.Diagnostics;
using DCL.PerformanceAndDiagnostics.Analytics.Services;
using DCL.PluginSystem;
using DCL.Utility;
using DCL.Web3.Identities;
using Global.AppArgs;
using Global.Dynamic;
using Global.Versioning;
using Plugins.RustSegment.SegmentServerWrap;
using System;
using System.Threading;
using UnityEngine;

namespace DCL.PerformanceAndDiagnostics.Analytics
{
    /// <summary>
    ///     Contains shared analytics-related dependencies
    /// </summary>
    public class AnalyticsContainer : DCLGlobalContainer<AnalyticsContainer.Settings>
    {
        public bool Enabled => settings.AnalyticsConfig.Mode != AnalyticsMode.DISABLED;

        public IAnalyticsController Controller { get; private set; } = null!;

        public EntitiesAnalytics EntitiesAnalytics { get; private set; } = null!;

        public static async UniTask<AnalyticsContainer> CreateAsync(
            IAppArgs appArgs,
            IWeb3IdentityCache identityCache,
            ILaunchMode realmLaunchSettings,
            IDebugContainerBuilder debugBuilder,
            BuildData buildData,
            IPluginSettingsContainer settingsContainer,
            DCLVersion dclVersion,
            CancellationToken ct)
        {
            var container = new AnalyticsContainer();

            await container.InitializeContainerAsync<AnalyticsContainer, Settings>(settingsContainer, ct, container =>
            {
                if (container.Enabled)
                {
                    var launcherTraits = LauncherTraits.FromAppArgs(appArgs);

                    IAnalyticsService service = CreateAnalyticsService(
                        container.settings.AnalyticsConfig,
                        launcherTraits,
                        appArgs,
                        realmLaunchSettings.CurrentMode is LaunchMode.LocalSceneDevelopment,
                        ct);

                    var analyticsController = new AnalyticsController(service, appArgs, container.settings.AnalyticsConfig, launcherTraits, buildData, dclVersion, identityCache?.Identity);
                    CrashDetector.Initialize(analyticsController);

                    container.Controller = analyticsController;

                    container.EntitiesAnalytics = new EntitiesAnalytics(analyticsController, new EntitiesAnalyticsDebug(debugBuilder.TryAddWidget(IDebugContainerBuilder.Categories.ENTITY_REQUESTS)));
                }
                else
                    container.Controller = IAnalyticsController.Null;

                return UniTask.CompletedTask;
            });

            return container;
        }

        private static IAnalyticsService CreateAnalyticsService(AnalyticsConfiguration analyticsConfig, LauncherTraits launcherTraits, IAppArgs args, bool isLocalSceneDevelopment, CancellationToken token)
        {
            // Avoid Segment analytics for: Unity Editor or Debug Mode (except when in Local Scene Development mode)

            if (!Application.isEditor && (!args.HasDebugFlag() || isLocalSceneDevelopment))
                return CreateSegmentAnalyticsOrFallbackToDebug(analyticsConfig, launcherTraits, token);

            return analyticsConfig.Mode switch
                   {
                       AnalyticsMode.SEGMENT => CreateSegmentAnalyticsOrFallbackToDebug(analyticsConfig, launcherTraits, token),
                       AnalyticsMode.DEBUG_LOG => new DebugAnalyticsService(),
                       AnalyticsMode.DISABLED => throw new InvalidOperationException("Trying to create analytics when it is disabled"),
                       _ => throw new ArgumentOutOfRangeException(),
                   };
        }

        private static IAnalyticsService CreateSegmentAnalyticsOrFallbackToDebug(AnalyticsConfiguration analyticsConfig, LauncherTraits launcherTraits, CancellationToken token)
        {
            if (analyticsConfig.TryGetSegmentConfiguration(out Configuration segmentConfiguration))
                return new RustSegmentAnalyticsService(segmentConfiguration.WriteKey!, launcherTraits.LauncherAnonymousId)
                   .WithTimeFlush(TimeSpan.FromSeconds(analyticsConfig.FlushInterval), token);

            // Fall back to debug if segment is not configured
            ReportHub.LogWarning(ReportCategory.ANALYTICS, $"Segment configuration not found. Falling back to {nameof(DebugAnalyticsService)}.");
            return new DebugAnalyticsService();
        }

        [Serializable]
        public class Settings : IDCLPluginSettings
        {
            [field: SerializeField]
            public AnalyticsConfiguration AnalyticsConfig { get; private set; } = null!;
        }
    }
}
