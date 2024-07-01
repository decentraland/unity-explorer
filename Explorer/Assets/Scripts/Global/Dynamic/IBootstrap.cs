using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Audio;
using DCL.DebugUtilities;
using DCL.PluginSystem;
using DCL.PluginSystem.Global;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.UIElements;

namespace Global.Dynamic
{
    public interface IBootstrap: IDisposable
    {
        UniTask PreInitializeSetup(RealmLaunchSettings launchSettings, UIDocument cursorRoot, UIDocument debugUiRoot,
            GameObject splashRoot, DebugViewsCatalog debugViewsCatalog, CancellationToken ct);

        UniTask<(StaticContainer?, bool)> LoadStaticContainerAsync(PluginSettingsContainer globalPluginSettingsContainer,  DynamicSceneLoaderSettings settings,
            IAssetsProvisioner assetsProvisioner, CancellationToken ct);

        UniTask<(DynamicWorldContainer?, bool)> LoadDynamicWorldContainerAsync(StaticContainer staticContainer,
            PluginSettingsContainer scenePluginSettingsContainer, DynamicSceneLoaderSettings settings, DynamicSettings dynamicSettings, RealmLaunchSettings launchSettings,
            UIDocument uiToolkitRoot, UIDocument cursorRoot, Animator splashScreenAnimation, AudioClipConfig backgroundMusic, CancellationToken ct);

        UniTask<bool> InitializePluginsAsync(StaticContainer staticContainer, DynamicWorldContainer dynamicWorldContainer,
            PluginSettingsContainer scenePluginSettingsContainer, PluginSettingsContainer globalPluginSettingsContainer,
            CancellationToken ct);

        UniTask InitializeFeatureFlagsAsync(StaticContainer staticContainer, CancellationToken ct);

        (GlobalWorld, Entity) CreateGlobalWorldAndPlayer(StaticContainer staticContainer, DynamicWorldContainer dynamicWorldContainer, UIDocument debugUiRoot);

        UniTask UserInitializationAsync(DynamicWorldContainer dynamicWorldContainer,
            GlobalWorld? globalWorld, Entity playerEntity, Animator splashScreenAnimation, GameObject splashRoot, CancellationToken ct);

        UniTask LoadStartingRealmAsync(DynamicWorldContainer dynamicWorldContainer, CancellationToken ct);
    }
}
