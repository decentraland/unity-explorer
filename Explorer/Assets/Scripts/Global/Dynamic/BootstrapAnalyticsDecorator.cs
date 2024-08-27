using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Audio;
using DCL.DebugUtilities;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.PerformanceAndDiagnostics.Analytics;
using DCL.PluginSystem;
using DCL.PluginSystem.Global;
using DCL.SceneLoadingScreens.SplashScreen;
using DCL.UserInAppInitializationFlow;
using DCL.Utilities;
using DCL.Web3.Identities;
using SceneRunner.Debugging;
using Segment.Serialization;
using System.Threading;
using UnityEngine.UIElements;
using static DCL.PerformanceAndDiagnostics.Analytics.AnalyticsEvents;

namespace Global.Dynamic
{
    public class BootstrapAnalyticsDecorator : IBootstrap
    {
        private const string STAGE_KEY = "state";
        private const string RESULT_KEY = "result";

        private readonly Bootstrap core;
        private readonly IAnalyticsController analytics;

        private int loadingScreenStageId;

        public BootstrapAnalyticsDecorator(Bootstrap core, IAnalyticsController analytics)
        {
            this.core = core;
            this.analytics = analytics;
        }

        public void PreInitializeSetup(UIDocument cursorRoot, UIDocument debugUiRoot, ISplashScreen splashScreen, CancellationToken ct)
        {
            analytics.Track(General.INITIAL_LOADING, new JsonObject
            {
                { STAGE_KEY, "0 - started" },
            });

            core.PreInitializeSetup(cursorRoot, debugUiRoot, splashScreen, ct);
        }

        public async UniTask<(StaticContainer?, bool)> LoadStaticContainerAsync(BootstrapContainer bootstrapContainer, PluginSettingsContainer globalPluginSettingsContainer, DebugViewsCatalog debugViewsCatalog, CancellationToken ct)
        {
            (StaticContainer? container, bool isSuccess) result = await core.LoadStaticContainerAsync(bootstrapContainer, globalPluginSettingsContainer, debugViewsCatalog, ct);

            analytics.SetCommonParam(result.container!.RealmData, bootstrapContainer.IdentityCache, result.container.CharacterContainer.Transform);

            analytics.Track(General.INITIAL_LOADING, new JsonObject
            {
                { STAGE_KEY, "1 - static container loaded" },
                { RESULT_KEY, result.isSuccess ? "success" : "failure" },
            });

            return result;
        }

        public async UniTask<(DynamicWorldContainer?, bool)> LoadDynamicWorldContainerAsync(BootstrapContainer bootstrapContainer, StaticContainer staticContainer, PluginSettingsContainer scenePluginSettingsContainer, DynamicSceneLoaderSettings settings, DynamicSettings dynamicSettings,
            UIDocument uiToolkitRoot, UIDocument cursorRoot, ISplashScreen splashScreen, AudioClipConfig backgroundMusic,
            WorldInfoTool worldInfoTool,
            CancellationToken ct)
        {
            (DynamicWorldContainer? container, bool) result =
                await core.LoadDynamicWorldContainerAsync(bootstrapContainer, staticContainer, scenePluginSettingsContainer,
                    settings, dynamicSettings, uiToolkitRoot, cursorRoot, splashScreen, backgroundMusic, worldInfoTool, ct);

            analytics.Track(General.INITIAL_LOADING, new JsonObject
            {
                { STAGE_KEY, "2 - dynamic container loaded" },
                { RESULT_KEY, result.Item2 ? "success" : "failure" },
            });

            return result;
        }

        public UniTask InitializeFeatureFlagsAsync(IWeb3Identity? identity, IDecentralandUrlsSource decentralandUrlsSource, StaticContainer staticContainer, CancellationToken ct)
        {
            UniTask result = core.InitializeFeatureFlagsAsync(identity, decentralandUrlsSource, staticContainer, ct);

            analytics.Track(General.INITIAL_LOADING, new JsonObject
            {
                { STAGE_KEY, "3 - feature flag initialized" },
            });

            return result;
        }

        public async UniTask<bool> InitializePluginsAsync(StaticContainer staticContainer, DynamicWorldContainer dynamicWorldContainer, PluginSettingsContainer scenePluginSettingsContainer, PluginSettingsContainer globalPluginSettingsContainer, CancellationToken ct)
        {
            bool anyFailure = await core.InitializePluginsAsync(staticContainer, dynamicWorldContainer, scenePluginSettingsContainer, globalPluginSettingsContainer, ct);

            analytics.Track(General.INITIAL_LOADING, new JsonObject
            {
                { STAGE_KEY, "4 - plugins initialized" },
                { RESULT_KEY, !anyFailure ? "success" : "failure" },
            });

            return anyFailure;
        }

        public (GlobalWorld, Entity) CreateGlobalWorldAndPlayer(BootstrapContainer bootstrapContainer, StaticContainer staticContainer, DynamicWorldContainer dynamicWorldContainer, UIDocument debugUiRoot)
        {
            (GlobalWorld, Entity) result = core.CreateGlobalWorldAndPlayer(bootstrapContainer, staticContainer, dynamicWorldContainer, debugUiRoot);

            analytics.Track(General.INITIAL_LOADING, new JsonObject
            {
                { STAGE_KEY, "5 - global world and player created" },
            });

            return result;
        }

        public async UniTask LoadStartingRealmAsync(DynamicWorldContainer dynamicWorldContainer, CancellationToken ct)
        {
            await core.LoadStartingRealmAsync(dynamicWorldContainer, ct);

            analytics.Track(General.INITIAL_LOADING, new JsonObject
            {
                { STAGE_KEY, "6 - realm loaded" },
            });
        }

        public async UniTask UserInitializationAsync(DynamicWorldContainer dynamicWorldContainer, GlobalWorld globalWorld, Entity playerEntity, ISplashScreen splashScreen, CancellationToken ct)
        {
            using (dynamicWorldContainer.RealFlowLoadingStatus.CurrentStage.Subscribe(OnLoadingStageChanged))
                await core.UserInitializationAsync(dynamicWorldContainer, globalWorld, playerEntity, splashScreen, ct);

            analytics.Track(General.INITIAL_LOADING, new JsonObject
            {
                { STAGE_KEY, "9 - end" },
            });
        }

        private void OnLoadingStageChanged(RealFlowLoadingStatus.Stage stage)
        {
            analytics.Track(General.INITIAL_LOADING, new JsonObject
            {
                { STAGE_KEY, $"7.{loadingScreenStageId++} - loading screen: {stage}" },
            });
        }
    }
}
