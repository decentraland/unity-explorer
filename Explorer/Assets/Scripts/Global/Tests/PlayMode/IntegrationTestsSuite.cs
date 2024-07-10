using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.DebugUtilities;
using DCL.PluginSystem;
using DCL.PluginSystem.Global;
using DCL.Profiles;
using DCL.Web3;
using DCL.Web3.Identities;
using DCL.WebRequests;
using Global.Dynamic;
using Global.Static;
using NSubstitute;
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

            var assetProvisioner = new AddressablesProvisioner().WithErrorTrace();
            var reportHandlingSettings = await  BootstrapContainer.ProvideReportHandlingSettings(assetProvisioner, new AnalyticsSettings(), default(CancellationToken));

            return await StaticSceneLauncher.InstallAsync(
                assetProvisioner, reportHandlingSettings.Value, new DebugViewsCatalog(),
                globalSettingsContainer, sceneSettingsContainer,
                Substitute.For<IWeb3IdentityCache>(), Substitute.For<IEthereumApi>(),
                Substitute.For<IWeb3IdentityCache>(), Substitute.For<IProfileRepository>(), IWebRequestController.DEFAULT,
                CancellationToken.None);
        }
    }
}
