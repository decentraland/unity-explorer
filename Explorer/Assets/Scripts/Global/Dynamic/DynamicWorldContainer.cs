using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Wearables;
using DCL.Browser;
using DCL.DebugUtilities;
using DCL.ParcelsService;
using DCL.PlacesAPIService;
using DCL.PluginSystem;
using DCL.PluginSystem.Global;
using DCL.Web3Authentication;
using DCL.Profiles;
using DCL.WebRequests.Analytics;
using ECS;
using ECS.Prioritization.Components;
using MVC;
using MVC.PopupsController.PopupCloser;
using SceneRunner.EmptyScene;
using System.Collections.Generic;
using System.Threading;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UIElements;

namespace Global.Dynamic
{
    public class DynamicWorldContainer : IDCLPlugin<DynamicWorldSettings>
    {
        private static readonly URLDomain ASSET_BUNDLES_URL = URLDomain.FromString("https://ab-cdn.decentraland.org/");

        private static MVCManager mvcManager;

        public DebugUtilitiesContainer DebugContainer { get; private set; }

        public IRealmController RealmController { get; private set; }

        public GlobalWorldFactory GlobalWorldFactory { get; private set; }

        public EmptyScenesWorldFactory EmptyScenesWorldFactory { get; private set; }

        public IReadOnlyList<IDCLGlobalPlugin> GlobalPlugins { get; private set; }

        public IWeb3Authenticator Web3Authenticator { get; private set; }

        public IProfileRepository ProfileRepository { get; private set; }

        public void Dispose()
        {
            mvcManager.Dispose();
        }

        public static async UniTask<(DynamicWorldContainer container, bool success)> CreateAsync(
            StaticContainer staticContainer,
            IPluginSettingsContainer settingsContainer,
            CancellationToken ct,
            UIDocument rootUIDocument,
            IReadOnlyList<int2> staticLoadPositions, int sceneLoadRadius,
            DynamicSettings dynamicSettings,
            IWeb3VerifiedAuthenticator web3Authenticator)
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

            var parcelServiceContainer = ParcelServiceContainer.Create(realmData, staticContainer.CharacterObject, debugBuilder);

            PopupCloserView popupCloserView = Object.Instantiate((await staticContainer.AssetsProvisioner.ProvideMainAssetAsync(dynamicSettings.PopupCloserView, ct: CancellationToken.None)).Value.GetComponent<PopupCloserView>());
            mvcManager = new MVCManager(new WindowStackManager(), new CancellationTokenSource(), popupCloserView);
            MapRendererContainer mapRendererContainer = await MapRendererContainer.CreateAsync(staticContainer, dynamicSettings.MapRendererSettings, ct);
            var placesAPIService = new PlacesAPIService(new PlacesAPIClient(staticContainer.WebRequestsContainer.WebRequestController));

            IProfileCache profileCache = new DefaultProfileCache();
            container.ProfileRepository = new RealmProfileRepository(staticContainer.WebRequestsContainer.WebRequestController, realmData,
                profileCache);

            var globalPlugins = new List<IDCLGlobalPlugin>
            {
                new CharacterMotionPlugin(staticContainer.AssetsProvisioner, staticContainer.CharacterObject, debugBuilder),
                new InputPlugin(dclInput),
                new GlobalInteractionPlugin(dclInput, rootUIDocument, staticContainer.AssetsProvisioner, staticContainer.EntityCollidersGlobalCache, exposedGlobalDataContainer.GlobalInputEvents),
                new CharacterCameraPlugin(staticContainer.AssetsProvisioner, realmSamplingData, exposedGlobalDataContainer.CameraSamplingData, exposedGlobalDataContainer.ExposedCameraData),
                new ProfilingPlugin(staticContainer.ProfilingProvider, staticContainer.SingletonSharedDependencies.FrameTimeBudgetProvider, staticContainer.SingletonSharedDependencies.MemoryBudgetProvider, debugBuilder),
                new WearablePlugin(staticContainer.AssetsProvisioner, staticContainer.WebRequestsContainer.WebRequestController, realmData, ASSET_BUNDLES_URL, staticContainer.CacheCleaner),
                new AvatarPlugin(staticContainer.ComponentsContainer.ComponentPoolsRegistry, staticContainer.AssetsProvisioner,
                    staticContainer.SingletonSharedDependencies.FrameTimeBudgetProvider, staticContainer.SingletonSharedDependencies.MemoryBudgetProvider, realmData, debugBuilder, staticContainer.CacheCleaner),
                new ProfilePlugin(container.ProfileRepository, profileCache, staticContainer.CacheCleaner, new ProfileIntentionCache()),
                new MapRendererPlugin(mapRendererContainer.MapRenderer),
                new MinimapPlugin(staticContainer.AssetsProvisioner, mvcManager, mapRendererContainer, placesAPIService),
                new ExplorePanelPlugin(staticContainer.AssetsProvisioner, mvcManager, mapRendererContainer, placesAPIService, parcelServiceContainer.TeleportController, dynamicSettings.BackpackSettings, staticContainer.WebRequestsContainer.WebRequestController),
                new WebRequestsPlugin(staticContainer.WebRequestsContainer.AnalyticsContainer, debugBuilder),
                new Web3AuthenticationPlugin(staticContainer.AssetsProvisioner, web3Authenticator, debugBuilder, mvcManager, container.ProfileRepository, new UnityAppWebBrowser()),
            };

            globalPlugins.AddRange(staticContainer.SharedPlugins);

            container.RealmController = new RealmController(
                staticContainer.WebRequestsContainer.WebRequestController,
                parcelServiceContainer.TeleportController,
                parcelServiceContainer.RetrieveSceneFromFixedRealm,
                parcelServiceContainer.RetrieveSceneFromVolatileWorld,
                sceneLoadRadius, staticLoadPositions, realmData);

            container.GlobalWorldFactory = new GlobalWorldFactory(in staticContainer, staticContainer.RealmPartitionSettings,
                exposedGlobalDataContainer.CameraSamplingData, realmSamplingData, ASSET_BUNDLES_URL, realmData, globalPlugins,
                debugBuilder);

            container.GlobalPlugins = globalPlugins;
            container.EmptyScenesWorldFactory = new EmptyScenesWorldFactory(staticContainer.SingletonSharedDependencies, staticContainer.ECSWorldPlugins);

            container.Web3Authenticator = web3Authenticator;

            return (container, true);
        }

        public UniTask InitializeAsync(DynamicWorldSettings settings, CancellationToken ct)
        {
            DebugContainer = DebugUtilitiesContainer.Create(settings.DebugViewsCatalog);
            return UniTask.CompletedTask;
        }
    }
}
