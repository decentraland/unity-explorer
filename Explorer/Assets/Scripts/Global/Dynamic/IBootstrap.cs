using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Audio;
using DCL.DebugUtilities;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.PluginSystem;
using DCL.PluginSystem.Global;
using DCL.SceneLoadingScreens.SplashScreen;
using DCL.Web3.Identities;
using SceneRunner.Debugging;
using System.Threading;
using UnityEngine.UIElements;

namespace Global.Dynamic
{
    public interface IBootstrap
    {
        void PreInitializeSetup(UIDocument cursorRoot, UIDocument debugUiRoot, ISplashScreen splashScreen, CancellationToken ct);

        UniTask<(StaticContainer?, bool)> LoadStaticContainerAsync(BootstrapContainer bootstrapContainer, PluginSettingsContainer globalPluginSettingsContainer, DebugViewsCatalog debugViewsCatalog, CancellationToken ct);

        UniTask<(DynamicWorldContainer?, bool)> LoadDynamicWorldContainerAsync(
            BootstrapContainer bootstrapContainer,
            StaticContainer staticContainer,
            PluginSettingsContainer scenePluginSettingsContainer,
            DynamicSceneLoaderSettings settings,
            DynamicSettings dynamicSettings,
            UIDocument uiToolkitRoot,
            UIDocument cursorRoot,
            ISplashScreen splashScreen,
            AudioClipConfig backgroundMusic,
            WorldInfoTool worldInfoTool,
            CancellationToken ct
        );

        UniTask<bool> InitializePluginsAsync(StaticContainer staticContainer, DynamicWorldContainer dynamicWorldContainer,
            PluginSettingsContainer scenePluginSettingsContainer, PluginSettingsContainer globalPluginSettingsContainer,
            CancellationToken ct);

        UniTask InitializeFeatureFlagsAsync(IWeb3Identity? identity, IDecentralandUrlsSource decentralandUrlsSource, StaticContainer staticContainer, CancellationToken ct);

        (GlobalWorld, Entity) CreateGlobalWorldAndPlayer(BootstrapContainer bootstrapContainer, StaticContainer staticContainer, DynamicWorldContainer dynamicWorldContainer,
            UIDocument debugUiRoot);

        UniTask UserInitializationAsync(DynamicWorldContainer dynamicWorldContainer, GlobalWorld globalWorld, Entity playerEntity, ISplashScreen splashScreen, CancellationToken ct);

        UniTask LoadStartingRealmAsync(DynamicWorldContainer dynamicWorldContainer, CancellationToken ct);
    }
}
