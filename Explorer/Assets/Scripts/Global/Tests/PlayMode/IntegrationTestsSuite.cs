using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Browser.DecentralandUrls;
using DCL.DebugUtilities;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Multiplayer.Connections.Messaging.Hubs;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Multiplayer.Profiles.Poses;
using DCL.Optimization.PerformanceBudgeting;
using DCL.PluginSystem;
using DCL.Profiles;
using DCL.Settings;
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
using DCL.PerformanceAndDiagnostics.Analytics;
using Plugins.TexturesFuse.TexturesServerWrap.Unzips;
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

            (StaticContainer? staticContainer, bool success) = await StaticContainer.CreateAsync(
                dclUrls,
                assetProvisioner,
                Substitute.For<IReportsHandlingSettings>(),
                Substitute.For<IAppArgs>(),
                ITexturesUnzip.NewTestInstance(),
                new DebugViewsCatalog(),
                globalSettingsContainer,
                diagnosticsContainer,
                identityCache,
                Substitute.For<IEthereumApi>(),
                false,
                false,
                World.Create(),
                new Entity(),
                new SystemMemoryCap(MemoryCapMode.MAX_SYSTEM_MEMORY),
                new WorldVolumeMacBus(),
                false,
                Substitute.For<IAnalyticsController>(),
                ct);

            if (!success)
                throw new Exception("Cannot create the static container");

            await UniTask.WhenAll(staticContainer!.ECSWorldPlugins.Select(gp => sceneSettingsContainer.InitializePluginAsync(gp, ct)));

            var sceneSharedContainer = SceneSharedContainer.Create(
                in staticContainer,
                dclUrls,
                identityCache,
                Substitute.For<IWebRequestController>(),
                new IRealmData.Fake(),
                new MemoryProfileRepository(new DefaultProfileCache()),
                NullRoomHub.INSTANCE,
                new MVCManager(
                    new WindowStackManager(),
                    new CancellationTokenSource(),
                    Substitute.For<IPopupCloserView>()
                ),
                new IMessagePipesHub.Fake(),
                Substitute.For<IRemoteMetadata>()
            );

            return (staticContainer, sceneSharedContainer);
        }
    }
}
