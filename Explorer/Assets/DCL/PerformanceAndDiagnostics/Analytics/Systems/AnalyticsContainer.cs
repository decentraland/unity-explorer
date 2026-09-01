using Cysharp.Threading.Tasks;
using DCL.DebugUtilities;
using DCL.Diagnostics;
using DCL.PerformanceAndDiagnostics.Analytics.Services;
using DCL.PluginSystem;
using DCL.Utility;
using DCL.Web3.Identities;
using Global.AppArgs;
using Global.Versioning;
using Plugins.RustSegment.SegmentServerWrap;
using System;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.PerformanceAndDiagnostics.Analytics
{
    /// <summary>
    ///     Contains shared analytics-related dependencies
    /// </summary>
    public class AnalyticsContainer : DCLGlobalContainer<AnalyticsContainer.Settings>
    {
        public bool Enabled => settings.AnalyticsConfig.Mode != AnalyticsMode.Disabled;

        public IAnalyticsController Controller { get; private set; } = null!;

        public EntitiesAnalytics EntitiesAnalytics { get; private set; } = null!;

        /// <summary>
        ///     Carries analytics-originated events (e.g. <see cref="AnalyticsDiskFullDetected" />) to
        ///     UI-layer subscribers that are created much later than this container.
        /// </summary>
        public IEventBus EventBus { get; } = new EventBus(invokeSubscribersOnMainThread: true);

        public static async UniTask<AnalyticsContainer> CreateAsync(
            IAppArgs appArgs,
            IWeb3IdentityCache identityCache,
            ILaunchMode realmLaunchSettings,
            IDebugContainerBuilder debugBuilder,
            string installSource,
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
                        container.EventBus,
                        ct);

                    var analyticsController = new AnalyticsController(service, appArgs, container.settings.AnalyticsConfig, launcherTraits, installSource, dclVersion, identityCache?.Identity);
                    LaunchCounter.Increment();

                    container.Controller = analyticsController;

                    container.EntitiesAnalytics = new EntitiesAnalytics(analyticsController, new EntitiesAnalyticsDebug(debugBuilder.TryAddWidget(IDebugContainerBuilder.Categories.ENTITY_REQUESTS)));
                }
                else
                    container.Controller = IAnalyticsController.Null;

                return UniTask.CompletedTask;
            });

            return container;
        }

        private static IAnalyticsService CreateAnalyticsService(AnalyticsConfiguration analyticsConfig, LauncherTraits launcherTraits, IAppArgs args, bool isLocalSceneDevelopment, IEventBus eventBus, CancellationToken token)
        {
            // Avoid Segment analytics for: Unity Editor or Debug Mode (except when in Local Scene Development mode)

            if (!Application.isEditor && (!args.HasDebugFlag() || isLocalSceneDevelopment))
                return CreateSegmentAnalyticsOrFallbackToDebug(analyticsConfig, launcherTraits, eventBus, token);

            return analyticsConfig.Mode switch
                   {
                       AnalyticsMode.Segment => CreateSegmentAnalyticsOrFallbackToDebug(analyticsConfig, launcherTraits, eventBus, token),
                       AnalyticsMode.DebugLog => new DebugAnalyticsService(),
                       AnalyticsMode.Disabled => throw new InvalidOperationException("Trying to create analytics when it is disabled"),
                       _ => throw new ArgumentOutOfRangeException(),
                   };
        }

        private static IAnalyticsService CreateSegmentAnalyticsOrFallbackToDebug(AnalyticsConfiguration analyticsConfig, LauncherTraits launcherTraits, IEventBus eventBus, CancellationToken token)
        {
            if (analyticsConfig.TryGetSegmentConfiguration(out Configuration segmentConfiguration))
                return new RustSegmentAnalyticsService(segmentConfiguration.WriteKey!, launcherTraits.LauncherAnonymousId, eventBus)
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
