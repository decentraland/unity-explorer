using Cysharp.Threading.Tasks;
using DCL.PluginSystem;
using Global.Static;
using System.Threading;
using UnityEngine.AddressableAssets;

namespace Global.Tests
{
    public static class IntegrationTestsSuite
    {
        private const string GLOBAL_CONTAINER_ADDRESS = "Integration Tests Global Container";
        private const string WORLD_CONTAINER_ADDRESS = "Integration Tests World Container";

        public static async UniTask<(StaticContainer staticContainer, SceneSharedContainer sceneSharedContainer)> CreateStaticContainer()
        {
            PluginSettingsContainer globalSettingsContainer = await Addressables.LoadAssetAsync<PluginSettingsContainer>(GLOBAL_CONTAINER_ADDRESS);
            PluginSettingsContainer sceneSettingsContainer = await Addressables.LoadAssetAsync<PluginSettingsContainer>(WORLD_CONTAINER_ADDRESS);
            return await StaticSceneLauncher.Install(globalSettingsContainer, sceneSettingsContainer, CancellationToken.None);
        }
    }
}
