using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AsyncLoadReporting;
using DCL.AvatarRendering.Wearables;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Backpack.BackpackBus;
using DCL.Browser;
using DCL.DebugUtilities;
using DCL.ParcelsService;
using DCL.PlacesAPIService;
using DCL.PluginSystem;
using DCL.PluginSystem.Global;
using DCL.SkyBox;
using DCL.Profiles;
using DCL.SceneLoadingScreens;
using DCL.Web3.Authenticators;
using DCL.Web3.Identities;
using DCL.WebRequests.Analytics;
using ECS;
using ECS.Prioritization.Components;
using MVC;
using MVC.PopupsController.PopupCloser;
using SceneRunner.EmptyScene;
using System;
using System.Collections.Generic;
using System.Threading;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Global.Dynamic
{
    public class DynamicWorldContainer : IDCLPlugin<DynamicWorldSettings>
    {
        private static readonly URLDomain ASSET_BUNDLES_URL = URLDomain.FromString("https://ab-cdn.decentraland.org/");

        public MVCManager MvcManager { get; private set; } = null!;

        public DebugUtilitiesContainer DebugContainer { get; private set; } = null!;

        public IRealmController RealmController { get; private set; } = null!;

        public GlobalWorldFactory GlobalWorldFactory { get; private set; } = null!;

        public EmptyScenesWorldFactory EmptyScenesWorldFactory { get; private set; } = null!;

        public IReadOnlyList<IDCLGlobalPlugin> GlobalPlugins { get; private set; } = null!;

        public IProfileRepository ProfileRepository { get; private set; } = null!;

        public ParcelServiceContainer ParcelServiceContainer { get; private set; }

        public void Dispose()
        {
            MvcManager.Dispose();
        }

        public static async UniTask<(DynamicWorldContainer? container, bool success)> CreateAsync(
            StaticContainer staticContainer,
            IPluginSettingsContainer settingsContainer,
            CancellationToken ct,
            UIDocument rootUIDocument,
            SkyBoxSceneData skyBoxSceneData,
            IReadOnlyList<int2> staticLoadPositions, int sceneLoadRadius,
            DynamicSettings dynamicSettings,
            IWeb3VerifiedAuthenticator web3Authenticator,
            IWeb3IdentityCache storedIdentityProvider)
        {
            var container = new DynamicWorldContainer();
            (_, bool result) = await settingsContainer.InitializePluginAsync(container, ct);

            if (!result)
                return (null, false);

            DebugContainerBuilder debugBuilder = container.DebugContainer.Builder;

            var realmSamplingData = new RealmSamplingData();
            var dclInput = new DCLInput();
            ExposedGlobalDataContainer exposedGlobalDataContainer = staticContainer.ExposedGlobalDataContainer;

            var realmData = new RealmData();

            PopupCloserView popupCloserView = Object.Instantiate((await staticContainer.AssetsProvisioner.ProvideMainAssetAsync(dynamicSettings.PopupCloserView, ct: CancellationToken.None)).Value.GetComponent<PopupCloserView>());
            container.MvcManager = new MVCManager(new WindowStackManager(), new CancellationTokenSource(), popupCloserView);

            var parcelServiceContainer = ParcelServiceContainer.Create(realmData, staticContainer.SceneReadinessReportQueue, debugBuilder, container.MvcManager);
            container.ParcelServiceContainer = parcelServiceContainer;

            MapRendererContainer mapRendererContainer = await MapRendererContainer.CreateAsync(staticContainer, dynamicSettings.MapRendererSettings, ct);
            var placesAPIService = new PlacesAPIService(new PlacesAPIClient(staticContainer.WebRequestsContainer.WebRequestController));
            var wearableCatalog = new WearableCatalog();
            var backpackCommandBus = new BackpackCommandBus();
            var backpackEventBus = new BackpackEventBus();

            IProfileCache profileCache = new DefaultProfileCache();

            container.ProfileRepository = new RealmProfileRepository(staticContainer.WebRequestsContainer.WebRequestController, realmData,
                profileCache);

            var globalPlugins = new List<IDCLGlobalPlugin>
            {
                new CharacterMotionPlugin(staticContainer.AssetsProvisioner, staticContainer.CharacterContainer.CharacterObject, debugBuilder),
                new InputPlugin(dclInput),
                new GlobalInteractionPlugin(dclInput, rootUIDocument, staticContainer.AssetsProvisioner, staticContainer.EntityCollidersGlobalCache, exposedGlobalDataContainer.GlobalInputEvents),
                new CharacterCameraPlugin(staticContainer.AssetsProvisioner, realmSamplingData, exposedGlobalDataContainer.CameraSamplingData, exposedGlobalDataContainer.ExposedCameraData),
                new WearablePlugin(staticContainer.AssetsProvisioner, staticContainer.WebRequestsContainer.WebRequestController, realmData, ASSET_BUNDLES_URL, staticContainer.CacheCleaner, wearableCatalog),
                new ProfilingPlugin(staticContainer.ProfilingProvider, staticContainer.SingletonSharedDependencies.FrameTimeBudget, staticContainer.SingletonSharedDependencies.MemoryBudget, debugBuilder),
                new AvatarPlugin(staticContainer.ComponentsContainer.ComponentPoolsRegistry, staticContainer.AssetsProvisioner,
                    staticContainer.SingletonSharedDependencies.FrameTimeBudget, staticContainer.SingletonSharedDependencies.MemoryBudget, realmData, debugBuilder, staticContainer.CacheCleaner),
                new ProfilePlugin(container.ProfileRepository, profileCache, staticContainer.CacheCleaner, new ProfileIntentionCache()),
                new MapRendererPlugin(mapRendererContainer.MapRenderer),
                new MinimapPlugin(staticContainer.AssetsProvisioner, container.MvcManager, mapRendererContainer, placesAPIService),
                new ExplorePanelPlugin(
                    staticContainer.AssetsProvisioner,
                    container.MvcManager,
                    mapRendererContainer,
                    placesAPIService,
                    parcelServiceContainer.TeleportController,
                    dynamicSettings.BackpackSettings,
                    backpackCommandBus,
                    backpackEventBus,
                    staticContainer.WebRequestsContainer.WebRequestController,
                    storedIdentityProvider,
                    wearableCatalog),

                new WebRequestsPlugin(staticContainer.WebRequestsContainer.AnalyticsContainer, debugBuilder),
                new Web3AuthenticationPlugin(staticContainer.AssetsProvisioner, web3Authenticator, debugBuilder, container.MvcManager, container.ProfileRepository, new UnityAppWebBrowser(), realmData, storedIdentityProvider),
                new SkyBoxPlugin(debugBuilder, skyBoxSceneData),
                new LoadingScreenPlugin(staticContainer.AssetsProvisioner, container.MvcManager),
            };

            globalPlugins.AddRange(staticContainer.SharedPlugins);

            container.RealmController = new RealmController(
                staticContainer.WebRequestsContainer.WebRequestController,
                parcelServiceContainer.TeleportController,
                parcelServiceContainer.RetrieveSceneFromFixedRealm,
                parcelServiceContainer.RetrieveSceneFromVolatileWorld,
                sceneLoadRadius, staticLoadPositions, realmData, staticContainer.ScenesCache);

            container.GlobalWorldFactory = new GlobalWorldFactory(
                in staticContainer,
                exposedGlobalDataContainer.CameraSamplingData,
                realmSamplingData,
                ASSET_BUNDLES_URL,
                realmData,
                globalPlugins,
                debugBuilder,
                staticContainer.ScenesCache);

            container.GlobalPlugins = globalPlugins;
            container.EmptyScenesWorldFactory = new EmptyScenesWorldFactory(staticContainer.SingletonSharedDependencies, staticContainer.ECSWorldPlugins);

            BuildTeleportWidget(container.RealmController, container.MvcManager, debugBuilder);

            return (container, true);
        }

        public UniTask InitializeAsync(DynamicWorldSettings settings, CancellationToken ct)
        {
            DebugContainer = DebugUtilitiesContainer.Create(settings.DebugViewsCatalog);
            return UniTask.CompletedTask;
        }

        private static void BuildTeleportWidget(IRealmController realmController, MVCManager mvcManager,
            IDebugContainerBuilder debugContainerBuilder)
        {
            async UniTask ChangeRealmAsync(string realm, CancellationToken ct)
            {
                var loadReport = new AsyncLoadProcessReport(new UniTaskCompletionSource(), new AsyncReactiveProperty<float>(0));

                await UniTask.WhenAll(mvcManager.ShowAsync(
                        SceneLoadingScreenController.IssueCommand(new SceneLoadingScreenController.Params(loadReport, TimeSpan.FromSeconds(30)))),
                    realmController.SetRealmAsync(URLDomain.FromString(realm), Vector2Int.zero, loadReport, ct));
            }

            debugContainerBuilder.AddWidget("Realm")
                                 .AddStringFieldWithConfirmation("https://peer.decentraland.org", "Change", realm => { ChangeRealmAsync(realm, CancellationToken.None).Forget(); });
        }
    }
}
