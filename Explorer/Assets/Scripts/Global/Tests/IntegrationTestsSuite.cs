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

        public static async UniTask<(StaticContainer staticContainer, SceneSharedContainer sceneSharedContainer)> CreateStaticContainer()
        {
            PluginSettingsContainer settingsContainer = await Addressables.LoadAssetAsync<PluginSettingsContainer>(GLOBAL_CONTAINER_ADDRESS);
            return await StaticSceneLauncher.Install(settingsContainer, CancellationToken.None);
        }
    }
}
