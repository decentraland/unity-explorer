using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Audio;
using DCL.DebugUtilities;
using DCL.PerformanceAndDiagnostics.Analytics;
using DCL.PluginSystem;
using DCL.PluginSystem.Global;
using DCL.UserInAppInitializationFlow;
using Segment.Serialization;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.UIElements;

namespace Global.Dynamic
{
    public class BootstrapAnalyticsDecorator : IBootstrap
    {
        private const string TRACK_NAME = "initial_loading";

        private readonly Bootstrap core;
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly AnalyticsSettings analyticsSettings;

        private AnalyticsController analytics;
        private AnalyticsConfiguration analyticsConfig;
        private string STAGE_KEY = "state";
        private string RESULT_KEY = "result";

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

        public async UniTask PreInitializeSetup(RealmLaunchSettings launchSettings, UIDocument cursorRoot, UIDocument debugUiRoot, GameObject splashRoot, DebugViewsCatalog debugViewsCatalog,
            CancellationToken ct)
        {
            analyticsConfig = (await assetsProvisioner.ProvideMainAssetAsync(analyticsSettings.AnalyticsConfigRef, ct)).Value;

            analytics = new AnalyticsController(
                // new DebugAnalyticsService()
                new SegmentAnalyticsService(analyticsConfig)
            );

            analytics.Track(TRACK_NAME, new Dictionary<string, JsonElement>
            {
                { STAGE_KEY, "initialization started" },
            });

            core.PreInitializeSetup(launchSettings, cursorRoot, debugUiRoot, splashRoot, debugViewsCatalog, ct);
        }

        public async UniTask<(StaticContainer?, bool)> LoadStaticContainerAsync(PluginSettingsContainer globalPluginSettingsContainer, DynamicSceneLoaderSettings settings, IAssetsProvisioner assetsProvisioner, CancellationToken ct)
        {
            (StaticContainer? container, bool isSuccess) result = await core.LoadStaticContainerAsync(globalPluginSettingsContainer, settings, assetsProvisioner, ct);

            analytics.SetCommonParam(result.container.RealmData, core.IdentityCache, result.container.CharacterContainer.CharacterObject.Transform);

            analytics.Track(TRACK_NAME, new Dictionary<string, JsonElement>
            {
                { STAGE_KEY, "static container loaded" },
                { RESULT_KEY, result.isSuccess ? "success" : "failure" },
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

            analytics.Track(TRACK_NAME, new Dictionary<string, JsonElement>
            {
                { STAGE_KEY, "dynamic container loaded" },
                { RESULT_KEY, result.Item2 ? "success" : "failure" },
            });

            return result;
        }

        public async UniTask<bool> InitializePluginsAsync(StaticContainer staticContainer, DynamicWorldContainer dynamicWorldContainer, PluginSettingsContainer scenePluginSettingsContainer, PluginSettingsContainer globalPluginSettingsContainer, CancellationToken ct)
        {
            bool result = await core.InitializePluginsAsync(staticContainer, dynamicWorldContainer, scenePluginSettingsContainer, globalPluginSettingsContainer, ct);

            analytics.Track(TRACK_NAME, new Dictionary<string, JsonElement>
            {
                { STAGE_KEY, "plugins initialized" },
                { RESULT_KEY, result ? "success" : "failure" },
            });

            return result;
        }

        public UniTask InitializeFeatureFlagsAsync(StaticContainer staticContainer, CancellationToken ct)
        {
            UniTask result = core.InitializeFeatureFlagsAsync(staticContainer, ct);

            analytics.Track(TRACK_NAME, new Dictionary<string, JsonElement>
            {
                { STAGE_KEY, "feature flag initialized" },
            });

            return result;
        }

        public (GlobalWorld, Entity) CreateGlobalWorldAndPlayer(StaticContainer staticContainer, DynamicWorldContainer dynamicWorldContainer, UIDocument debugUiRoot)
        {
            (GlobalWorld, Entity) result = core.CreateGlobalWorldAndPlayer(staticContainer, dynamicWorldContainer, debugUiRoot);

            analytics.Track(TRACK_NAME, new Dictionary<string, JsonElement>
            {
                { STAGE_KEY, "global world and player created" },
            });

            return result;
        }

        public async UniTask LoadStartingRealmAsync(DynamicWorldContainer dynamicWorldContainer, CancellationToken ct)
        {
            await core.LoadStartingRealmAsync(dynamicWorldContainer, ct);

            analytics.Track(TRACK_NAME, new Dictionary<string, JsonElement>
            {
                { STAGE_KEY, "realm loaded" },
            });
        }

        public async UniTask UserInitializationAsync(DynamicWorldContainer dynamicWorldContainer, GlobalWorld? globalWorld, Entity playerEntity, Animator splashScreenAnimation, GameObject splashRoot,
            CancellationToken ct)
        {
            dynamicWorldContainer.RealFlowLoadingStatus.StageChanged += OnLoadingStageChanged;
            await core.UserInitializationAsync(dynamicWorldContainer, globalWorld, playerEntity, splashScreenAnimation, splashRoot, ct);
            dynamicWorldContainer.RealFlowLoadingStatus.StageChanged -= OnLoadingStageChanged;

            analytics.Track(TRACK_NAME, new Dictionary<string, JsonElement>
            {
                { STAGE_KEY, "completed" },
            });
        }

        private void OnLoadingStageChanged(RealFlowLoadingStatus.Stage stage)
        {
            analytics.Track(TRACK_NAME, new Dictionary<string, JsonElement>
            {
                { STAGE_KEY, "loading screen: " + stage },
            });
        }
    }
}
