﻿using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.AssetsProvision.CodeResolver;
using DCL.Browser.DecentralandUrls;
using DCL.CharacterCamera;
using DCL.DebugUtilities;
using DCL.Diagnostics;
using DCL.FeatureFlags;
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
using MVC;
using MVC.PopupsController.PopupCloser;
using NSubstitute;
using System;
using System.Threading;
using DCL.Audio;
using DCL.PerformanceAndDiagnostics.Analytics;
using DCL.Utilities;
using DCL.WebRequests.Analytics;
using DCL.WebRequests.ChromeDevtool;
using ECS.StreamableLoading.Cache.Disk;
using ECS.StreamableLoading.Common.Components;
using Global.AppArgs;
using Global.Dynamic.LaunchModes;
using SceneRuntime.Factory.WebSceneSource;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UIElements;

namespace Global.Tests.PlayMode
{
    public static class IntegrationTestsSuite
    {
        private const string GLOBAL_CONTAINER_ADDRESS = "Integration Tests Global Container";
        private const string WORLD_CONTAINER_ADDRESS = "Integration Tests World Container";

        public static async UniTask<(StaticContainer staticContainer, SceneSharedContainer sceneSharedContainer)> CreateStaticContainer(CancellationToken ct)
        {
            var appArgs = new ApplicationParametersParser();

            FeatureFlagsConfiguration.Initialize(new FeatureFlagsConfiguration(FeatureFlagsResultDto.Empty));
            FeaturesRegistry.Initialize(new FeaturesRegistry(appArgs, false));
            PluginSettingsContainer globalSettingsContainer = await Addressables.LoadAssetAsync<PluginSettingsContainer>(GLOBAL_CONTAINER_ADDRESS);
            PluginSettingsContainer sceneSettingsContainer = await Addressables.LoadAssetAsync<PluginSettingsContainer>(WORLD_CONTAINER_ADDRESS);
            IAssetsProvisioner assetProvisioner = new AddressablesProvisioner().WithErrorTrace();
            IDecentralandUrlsSource dclUrls = new DecentralandUrlsSource(DecentralandEnvironment.Org, ILaunchMode.PLAY);

            IWeb3IdentityCache identityCache = new MemoryWeb3IdentityCache();

            IWebJsSources webJsSources = new WebJsSources(
                new JsCodeResolver(
                    IWebRequestController.DEFAULT
                )
            );

            IReportsHandlingSettings? reportSettings = Substitute.For<IReportsHandlingSettings>();
            reportSettings.IsEnabled(ReportHandler.DebugLog).Returns(true);

            var diagnosticsContainer = DiagnosticsContainer.Create(reportSettings);

            var world = World.Create();
            var cameraEntity = world.Create();

            var cameraGameObject = new GameObject("TestCamera");
            var camera = cameraGameObject.AddComponent<Camera>();

            var cameraComponent = new CameraComponent(camera);
            world.Add(cameraEntity, cameraComponent);

            (StaticContainer? staticContainer, bool success) = await StaticContainer.CreateAsync(
                dclUrls,
                assetProvisioner,
                Substitute.For<IReportsHandlingSettings>(),
                Substitute.For<IDebugContainerBuilder>(),
                WebRequestsContainer.Create(new IWeb3IdentityCache.Default(), Substitute.For<IDebugContainerBuilder>(), dclUrls, ChromeDevtoolProtocolClient.NewForTest(), 1000, 1000),
                globalSettingsContainer,
                diagnosticsContainer,
                identityCache,
                Substitute.For<IEthereumApi>(),
                ILaunchMode.PLAY,
                useRemoteAssetBundles: false,
                world,
                new Entity(),
                new SystemMemoryCap(),
                new VolumeBus(),
                enableAnalytics: false,
                Substitute.For<IAnalyticsController>(),
                new IDiskCache.Fake(),
                Substitute.For<IDiskCache<PartialLoadingState>>(),
                new ObjectProxy<IProfileRepository>(),
                DecentralandEnvironment.Org,
                ct,
                appArgs,
                enableGPUInstancing: false
            );

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
                Substitute.For<IRemoteMetadata>(),
                webJsSources,
                DecentralandEnvironment.Org
            );

            return (staticContainer, sceneSharedContainer);
        }
    }
}
