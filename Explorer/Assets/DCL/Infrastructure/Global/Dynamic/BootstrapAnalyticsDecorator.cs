﻿using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Audio;
using DCL.DebugUtilities;
using DCL.FeatureFlags;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Optimization.PerformanceBudgeting;
using DCL.PerformanceAndDiagnostics.Analytics;
using DCL.PluginSystem;
using DCL.PluginSystem.Global;
using DCL.Web3.Identities;
using Global.AppArgs;
using Global.Versioning;
using SceneRunner.Debugging;
using Segment.Serialization;
using System;
using System.Collections.Generic;
using System.Threading;
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

        public UniTask PreInitializeSetupAsync(CancellationToken ct)
        {
            analytics.Track(General.INITIAL_LOADING, new JsonObject
            {
                { STAGE_KEY, "0 - started" },
            });

            return core.PreInitializeSetupAsync(ct);
        }

        public async UniTask<(StaticContainer?, bool)> LoadStaticContainerAsync(
            BootstrapContainer bootstrapContainer,
            PluginSettingsContainer globalPluginSettingsContainer,
            IDebugContainerBuilder debugContainerBuilder,
            Entity playerEntity,
            ISystemMemoryCap memoryCap,
            bool hasDebugFlag,
            CancellationToken ct
        )
        {
            (StaticContainer? container, bool isSuccess) result = await core.LoadStaticContainerAsync(
                bootstrapContainer, globalPluginSettingsContainer, debugContainerBuilder, playerEntity, memoryCap, hasDebugFlag, ct);

            analytics.SetCommonParam(result.container!.RealmData, bootstrapContainer.IdentityCache, result.container.CharacterContainer.Transform);

            analytics.Track(General.INITIAL_LOADING, new JsonObject
            {
                { STAGE_KEY, "1 - static container loaded" },
                { RESULT_KEY, result.isSuccess ? "success" : "failure" },
            });

            return result;
        }

        public async UniTask<(DynamicWorldContainer?, bool)> LoadDynamicWorldContainerAsync(BootstrapContainer bootstrapContainer, StaticContainer staticContainer, PluginSettingsContainer scenePluginSettingsContainer, DynamicSceneLoaderSettings settings, DynamicSettings dynamicSettings,
            AudioClipConfig backgroundMusic,
            WorldInfoTool worldInfoTool,
            Entity playerEntity,
            IAppArgs appArgs,
            ICoroutineRunner coroutineRunner,
            DCLVersion dclVersion,
            CancellationToken ct,
            bool forceOnboarding)
        {
            (DynamicWorldContainer? container, bool) result =
                await core.LoadDynamicWorldContainerAsync(bootstrapContainer, staticContainer, scenePluginSettingsContainer,
                    settings, dynamicSettings, backgroundMusic, worldInfoTool, playerEntity, appArgs, coroutineRunner, dclVersion, ct, forceOnboarding);

            analytics.Track(General.INITIAL_LOADING, new JsonObject
            {
                { STAGE_KEY, "3 - dynamic container loaded" },
                { RESULT_KEY, result.Item2 ? "success" : "failure" },
            });

            return result;
        }

        public async UniTask InitializeFeatureFlagsAsync(IWeb3Identity? identity, IDecentralandUrlsSource decentralandUrlsSource, StaticContainer staticContainer, CancellationToken ct)
        {
            await core.InitializeFeatureFlagsAsync(identity, decentralandUrlsSource, staticContainer, ct);

            FeatureFlagsConfiguration configuration = FeatureFlagsConfiguration.Instance;

            var enabledFeatureFlags = new JsonArray();

            foreach (string flag in configuration.AllEnabledFlags)
            {
                string name = flag;

                if (configuration.TryGetVariant(flag, out FeatureFlagVariantDto variant))
                    if (!string.Equals(variant.name, "disabled", StringComparison.OrdinalIgnoreCase))
                        name += $":{variant.name}";

                enabledFeatureFlags.Add(name);
            }

            analytics.Track(FeatureFlags.ENABLED_FEATURES, new JsonObject
            {
                { "featureFlags", enabledFeatureFlags },
            });

            analytics.Track(General.INITIAL_LOADING, new JsonObject
            {
                { STAGE_KEY, "2 - feature flag initialized" },
            });
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

        public void ApplyFeatureFlagConfigs(FeatureFlagsConfiguration featureFlagsConfigurationCache)
        {
            core.ApplyFeatureFlagConfigs(featureFlagsConfigurationCache);

            //No analytics to track on this step
        }

        public void InitializeFeaturesRegistry()
        {
            core.InitializeFeaturesRegistry();
        }

        public async UniTask UserInitializationAsync(DynamicWorldContainer dynamicWorldContainer,
            BootstrapContainer bootstrapContainer,
            GlobalWorld globalWorld, Entity playerEntity, CancellationToken ct)
        {
            await core.UserInitializationAsync(dynamicWorldContainer, bootstrapContainer, globalWorld, playerEntity, ct);

            analytics.Track(General.INITIAL_LOADING, new JsonObject
            {
                { STAGE_KEY, "8 - end" },
            });
        }
    }
}
