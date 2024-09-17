using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Browser.DecentralandUrls;
using DCL.DebugUtilities;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Multiplayer.Connections.Messaging.Hubs;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.PluginSystem;
using DCL.Profiles;
using DCL.Web3;
using DCL.Web3.Identities;
using DCL.WebRequests;
using ECS;
using Global.AppArgs;
using MVC;
using MVC.PopupsController.PopupCloser;
using NSubstitute;
using System;
using System.Threading;
using UnityEngine.AddressableAssets;

namespace Global.Tests.PlayMode
{
    public static class IntegrationTestsSuite
    {
        private const string GLOBAL_CONTAINER_ADDRESS = "Integration Tests Global Container";
        private const string WORLD_CONTAINER_ADDRESS = "Integration Tests World Container";

        public static async UniTask<(StaticContainer staticContainer, SceneSharedContainer sceneSharedContainer)> CreateStaticContainer(CancellationToken ct)
        {
            PluginSettingsContainer globalSettingsContainer = await Addressables.LoadAssetAsync<PluginSettingsContainer>(GLOBAL_CONTAINER_ADDRESS);
            PluginSettingsContainer sceneSettingsContainer = await Addressables.LoadAssetAsync<PluginSettingsContainer>(WORLD_CONTAINER_ADDRESS);
            IAssetsProvisioner assetProvisioner = new AddressablesProvisioner().WithErrorTrace();
            IDecentralandUrlsSource dclUrls = new DecentralandUrlsSource(DecentralandEnvironment.Org);

            IWeb3IdentityCache identityCache = new MemoryWeb3IdentityCache();

            IReportsHandlingSettings? reportSettings = Substitute.For<IReportsHandlingSettings>();
            reportSettings.IsEnabled(ReportHandler.DebugLog).Returns(true);

            var diagnosticsContainer = DiagnosticsContainer.Create(reportSettings);

            (StaticContainer? staticContainer, bool success) = await StaticContainer.CreateAsync(dclUrls,
                assetProvisioner,
                Substitute.For<IReportsHandlingSettings>(),
                Substitute.For<IAppArgs>(),
                new DebugViewsCatalog(),
                globalSettingsContainer,
                diagnosticsContainer,
                identityCache,
                Substitute.For<IEthereumApi>(),
                false,
                false,
                World.Create(),
                new Entity(),
                ct);

            if (!success)
                throw new Exception("Cannot create the static container");

            await UniTask.WhenAll(staticContainer!.ECSWorldPlugins.Select(gp => sceneSettingsContainer.InitializePluginAsync(gp, ct)));

            var sceneSharedContainer = SceneSharedContainer.Create(
                in staticContainer,
                dclUrls,
                new MVCManager(
                    new WindowStackManager(),
                    new CancellationTokenSource(),
                    Substitute.For<IPopupCloserView>()
                ),
                identityCache,
                new MemoryProfileRepository(new DefaultProfileCache()),
                Substitute.For<IWebRequestController>(),
                NullRoomHub.INSTANCE,
                new IRealmData.Fake(),
                new IMessagePipesHub.Fake()
            );

            return (staticContainer, sceneSharedContainer);
        }
    }
}
