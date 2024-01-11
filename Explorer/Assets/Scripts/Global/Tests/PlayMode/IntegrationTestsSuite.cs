using Cysharp.Threading.Tasks;
using DCL.PluginSystem;
using DCL.Web3Authentication;
using DCL.Web3Authentication.Identities;
using Global.Static;
using NSubstitute;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UIElements;

namespace Global.Tests
{
    public static class IntegrationTestsSuite
    {
        private const string GLOBAL_CONTAINER_ADDRESS = "Integration Tests Global Container";
        private const string WORLD_CONTAINER_ADDRESS = "Integration Tests World Container";
        private const string SCENES_UI_ROOT_CANVAS = "ScenesUIRootCanvas";
        private const string SCENES_UI_STYLE_SHEET = "ScenesUIStyleSheet";

        public static async UniTask<(StaticContainer staticContainer, SceneSharedContainer sceneSharedContainer)> CreateStaticContainer()
        {
            PluginSettingsContainer globalSettingsContainer = await Addressables.LoadAssetAsync<PluginSettingsContainer>(GLOBAL_CONTAINER_ADDRESS);
            PluginSettingsContainer sceneSettingsContainer = await Addressables.LoadAssetAsync<PluginSettingsContainer>(WORLD_CONTAINER_ADDRESS);
            UIDocument scenesUIRootCanvas = Object.Instantiate(await Addressables.LoadAssetAsync<GameObject>(SCENES_UI_ROOT_CANVAS)).GetComponent<UIDocument>();
            StyleSheet scenesUIStyleSheet = await Addressables.LoadAssetAsync<StyleSheet>(SCENES_UI_STYLE_SHEET);

            return await StaticSceneLauncher.InstallAsync(globalSettingsContainer, sceneSettingsContainer,
                scenesUIRootCanvas, scenesUIStyleSheet, Substitute.For<IWeb3IdentityCache>(), CancellationToken.None);
        }
    }
}
