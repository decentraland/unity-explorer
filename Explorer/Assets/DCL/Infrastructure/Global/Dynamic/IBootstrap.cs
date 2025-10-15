using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Audio;
using DCL.DebugUtilities;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Optimization.PerformanceBudgeting;
using DCL.PluginSystem;
using DCL.PluginSystem.Global;
using DCL.Web3.Identities;
using Global.AppArgs;
using SceneRunner.Debugging;
using System.Threading;
using DCL.FeatureFlags;
using Global.Versioning;
using System.Collections.Generic;
using UnityEngine.UIElements;
using Utility;

namespace Global.Dynamic
{
    public interface IBootstrap
    {
        UniTask PreInitializeSetupAsync(CancellationToken ct);

        UniTask<(StaticContainer?, bool)> LoadStaticContainerAsync(
            BootstrapContainer bootstrapContainer,
            PluginSettingsContainer globalPluginSettingsContainer,
            IDebugContainerBuilder debugContainerBuilder,
            Entity playerEntity,
            ISystemMemoryCap memoryCap,
            IAppArgs appArgs,
            CancellationToken ct
        );

        UniTask<(DynamicWorldContainer?, bool)> LoadDynamicWorldContainerAsync(
            BootstrapContainer bootstrapContainer,
            StaticContainer staticContainer,
            PluginSettingsContainer scenePluginSettingsContainer,
            DynamicSceneLoaderSettings settings,
            DynamicSettings dynamicSettings,
            AudioClipConfig backgroundMusic,
            WorldInfoTool worldInfoTool,
            Entity playerEntity,
            IAppArgs appArgs,
            ICoroutineRunner coroutineRunner,
            DCLVersion dclVersion,
            CancellationToken ct);

        UniTask<bool> InitializePluginsAsync(StaticContainer staticContainer, DynamicWorldContainer dynamicWorldContainer,
            PluginSettingsContainer scenePluginSettingsContainer, PluginSettingsContainer globalPluginSettingsContainer,
            CancellationToken ct);

        UniTask InitializeFeatureFlagsAsync(IWeb3Identity? identity, IDecentralandUrlsSource decentralandUrlsSource, StaticContainer staticContainer, CancellationToken ct);

        void InitializePlayerEntity(StaticContainer staticContainer, Entity playerEntity);

        GlobalWorld CreateGlobalWorld(BootstrapContainer bootstrapContainer, StaticContainer staticContainer, DynamicWorldContainer dynamicWorldContainer,
            UIDocument debugUiRoot, Entity playerEntity);

        UniTask UserInitializationAsync(DynamicWorldContainer dynamicWorldContainer, GlobalWorld globalWorld, Entity playerEntity, CancellationToken ct);

        UniTask LoadStartingRealmAsync(DynamicWorldContainer dynamicWorldContainer, CancellationToken ct);

        void ApplyFeatureFlagConfigs(FeatureFlagsConfiguration featureFlagsConfigurationCache);

        void InitializeFeaturesRegistry();
    }
}
