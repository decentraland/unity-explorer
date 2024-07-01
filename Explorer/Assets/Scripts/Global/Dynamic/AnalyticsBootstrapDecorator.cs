using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Audio;
using DCL.DebugUtilities;
using DCL.PerformanceAndDiagnostics.Analytics;
using DCL.PluginSystem;
using DCL.PluginSystem.Global;
using Segment.Serialization;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.UIElements;

namespace Global.Dynamic
{
    public class BootstrapAnalyticsDecorator : IBootstrap
    {
        private readonly Bootstrap core;
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly AnalyticsSettings analyticsSettings;

        private  AnalyticsController analytics;
        private AnalyticsConfiguration analyticsConfig;

        public DynamicWorldDependencies DynamicWorldDependencies { get; }

        public BootstrapAnalyticsDecorator(Bootstrap core, IAssetsProvisioner assetsProvisioner, AnalyticsSettings analyticsSettings)
        {
            this.core = core;
            this.assetsProvisioner = assetsProvisioner;
            this.analyticsSettings = analyticsSettings;


            DynamicWorldDependencies = core.DynamicWorldDependencies;
        }

        public void Dispose()
        {
            core.Dispose();
        }

        public async UniTask PreInitializeSetup(RealmLaunchSettings launchSettings, UIDocument cursorRoot, UIDocument debugUiRoot, GameObject splashRoot, DebugViewsCatalog debugViewsCatalog, CancellationToken ct)
        {
            analyticsConfig = (await assetsProvisioner.ProvideMainAssetAsync(analyticsSettings.AnalyticsConfigRef, ct)).Value;
            analytics = new AnalyticsController(
                new DebugAnalyticsService()
                // new SegmentAnalyticsService(analyticsConfig)
            );

            analytics.Track("initial_loading", new Dictionary<string, JsonElement>
            {
                { "state", "initialization started" },
            });


            core.PreInitializeSetup(launchSettings, cursorRoot, debugUiRoot, splashRoot, debugViewsCatalog, ct);
        }

        public async UniTask<(StaticContainer?, bool)> LoadStaticContainerAsync(PluginSettingsContainer globalPluginSettingsContainer, DynamicSceneLoaderSettings settings, IAssetsProvisioner assetsProvisioner, CancellationToken ct)
        {
            (StaticContainer? container, bool isSuccess) result = await core.LoadStaticContainerAsync(globalPluginSettingsContainer, settings, assetsProvisioner, ct);

            analytics.SetCommonParam(result.container.RealmData, core.IdentityCache, result.container.CharacterContainer.CharacterObject.Transform);

            analytics.Track("initial_loading", new Dictionary<string, JsonElement>
            {
                { "state", "static container loaded" },
                { "result", result.isSuccess? "success" : "failure" },
            });

            return result;
        }

        public async UniTask<(DynamicWorldContainer?, bool)> LoadDynamicWorldContainerAsync(StaticContainer staticContainer, PluginSettingsContainer scenePluginSettingsContainer, DynamicSceneLoaderSettings settings, DynamicSettings dynamicSettings, RealmLaunchSettings launchSettings,
            UIDocument uiToolkitRoot, UIDocument cursorRoot, Animator splashScreenAnimation, AudioClipConfig backgroundMusic, CancellationToken ct)
        {
            (DynamicWorldContainer? container, bool) result = await core.LoadDynamicWorldContainerAsync(staticContainer, scenePluginSettingsContainer, settings, dynamicSettings, launchSettings, uiToolkitRoot, cursorRoot, splashScreenAnimation, backgroundMusic, ct);

            result.container.GlobalPlugins.Add
            (
                new AnalyticsPlugin(
                    analytics,
                    analyticsConfig,
                    staticContainer.ProfilingProvider,
                    staticContainer.RealmData,
                    staticContainer.ScenesCache,
                    result.container.MvcManager,
                    result.container.ChatMessagesBus,
                    result.container.GoToChatCommand)
                );

            analytics.Track("initial_loading", new Dictionary<string, JsonElement>
            {
                { "state", "dynamic container loaded" },
                { "result", result.Item2? "success" : "failure" },
            });

            return result;
        }

        public async UniTask<bool> InitializePluginsAsync(StaticContainer staticContainer, DynamicWorldContainer dynamicWorldContainer, PluginSettingsContainer scenePluginSettingsContainer, PluginSettingsContainer globalPluginSettingsContainer, CancellationToken ct)
        {
            bool result = await core.InitializePluginsAsync(staticContainer, dynamicWorldContainer, scenePluginSettingsContainer, globalPluginSettingsContainer, ct);

            analytics.Track("initial_loading", new Dictionary<string, JsonElement>
            {
                { "state", "plugins initialized" },
                { "result", result? "success" : "failure" },
            });

            return result;
        }

        public UniTask InitializeFeatureFlagsAsync(StaticContainer staticContainer, CancellationToken ct)
        {
            var result = core.InitializeFeatureFlagsAsync(staticContainer, ct);

            analytics.Track("initial_loading", new Dictionary<string, JsonElement>
            {
                { "state", "feature flag initialized" },
            });

            return result;
        }

        public (GlobalWorld, Entity) CreateGlobalWorldAndPlayer(StaticContainer staticContainer, DynamicWorldContainer dynamicWorldContainer, UIDocument debugUiRoot)
        {
            var result = core.CreateGlobalWorldAndPlayer(staticContainer, dynamicWorldContainer, debugUiRoot);

            analytics.Track("initial_loading", new Dictionary<string, JsonElement>
            {
                { "state", "global world and player created" },
            });

            return result;
        }

        public async UniTask LoadStartingRealmAndUserInitializationAsync(DynamicWorldContainer dynamicWorldContainer, GlobalWorld? globalWorld, Entity playerEntity, Animator splashScreenAnimation, GameObject splashRoot,
            CancellationToken ct)
        {
            await core.LoadStartingRealmAndUserInitializationAsync(dynamicWorldContainer, globalWorld, playerEntity, splashScreenAnimation, splashRoot, ct);

            analytics.Track("initial_loading", new Dictionary<string, JsonElement>
            {
                { "state", "completed" },
            });
        }
    }
}
