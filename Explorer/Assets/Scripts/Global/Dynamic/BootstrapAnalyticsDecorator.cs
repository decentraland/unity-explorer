﻿using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Audio;
using DCL.DebugUtilities;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Optimization.PerformanceBudgeting;
using DCL.PerformanceAndDiagnostics.Analytics;
using DCL.PluginSystem;
using DCL.PluginSystem.Global;
using DCL.SceneLoadingScreens.SplashScreen;
using DCL.Web3.Identities;
using Global.AppArgs;
using Plugins.TexturesFuse.TexturesServerWrap.Unzips;
using SceneRunner.Debugging;
using Segment.Serialization;
using System.Threading;
using DCL.FeatureFlags;
using UnityEngine.UIElements;
using Utility;
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

        public UniTask PreInitializeSetupAsync(UIDocument cursorRoot, UIDocument debugUiRoot, CancellationToken ct)
        {
            analytics.Track(General.INITIAL_LOADING, new JsonObject
            {
                { STAGE_KEY, "0 - started" },
            });

            return core.PreInitializeSetupAsync(cursorRoot, debugUiRoot, ct);
        }

        public async UniTask<(StaticContainer?, bool)> LoadStaticContainerAsync(
            BootstrapContainer bootstrapContainer,
            PluginSettingsContainer globalPluginSettingsContainer,
            IDebugContainerBuilder debugContainerBuilder,
            Entity playerEntity,
            ITexturesFuse texturesFuse,
            ISystemMemoryCap memoryCap,
            CancellationToken ct,
            bool compressionEnabled = false
        )
        {
            (StaticContainer? container, bool isSuccess) result = await core.LoadStaticContainerAsync(bootstrapContainer, globalPluginSettingsContainer, debugContainerBuilder, playerEntity, texturesFuse, memoryCap, ct);

            analytics.SetCommonParam(result.container!.RealmData, bootstrapContainer.IdentityCache, result.container.CharacterContainer.Transform);

            analytics.Track(General.INITIAL_LOADING, new JsonObject
            {
                { STAGE_KEY, "1 - static container loaded" },
                { RESULT_KEY, result.isSuccess ? "success" : "failure" },
            });

            return result;
        }

        public async UniTask<(DynamicWorldContainer?, bool)> LoadDynamicWorldContainerAsync(BootstrapContainer bootstrapContainer, StaticContainer staticContainer, PluginSettingsContainer scenePluginSettingsContainer, DynamicSceneLoaderSettings settings, DynamicSettings dynamicSettings,
            UIDocument uiToolkitRoot, UIDocument cursorRoot, AudioClipConfig backgroundMusic,
            WorldInfoTool worldInfoTool,
            Entity playerEntity,
            IAppArgs appArgs,
            ICoroutineRunner coroutineRunner,
            CancellationToken ct)
        {
            (DynamicWorldContainer? container, bool) result =
                await core.LoadDynamicWorldContainerAsync(bootstrapContainer, staticContainer, scenePluginSettingsContainer,
                    settings, dynamicSettings, uiToolkitRoot, cursorRoot, backgroundMusic, worldInfoTool, playerEntity, appArgs, coroutineRunner, ct);

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

        public void InitializePlayerEntity(StaticContainer staticContainer, Entity playerEntity) =>
            core.InitializePlayerEntity(staticContainer, playerEntity);

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

        public GlobalWorld CreateGlobalWorld(BootstrapContainer bootstrapContainer,
            StaticContainer staticContainer,
            DynamicWorldContainer dynamicWorldContainer,
            UIDocument debugUiRoot,
            Entity playerEntity)
        {
            GlobalWorld result = core.CreateGlobalWorld(bootstrapContainer, staticContainer, dynamicWorldContainer, debugUiRoot, playerEntity);

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

        public void ApplyFeatureFlagConfigs(FeatureFlagsCache featureFlagsCache)
        {
            core.ApplyFeatureFlagConfigs(featureFlagsCache);
            //No analytics to track on this step
        }

        public async UniTask UserInitializationAsync(DynamicWorldContainer dynamicWorldContainer, GlobalWorld globalWorld, Entity playerEntity, CancellationToken ct)
        {
            await core.UserInitializationAsync(dynamicWorldContainer, globalWorld, playerEntity, ct);

            analytics.Track(General.INITIAL_LOADING, new JsonObject
            {
                { STAGE_KEY, "8 - end" },
            });
        }
    }
}
