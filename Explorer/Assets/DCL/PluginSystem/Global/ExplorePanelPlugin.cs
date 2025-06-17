using Arch.Core;
using Arch.SystemGroups;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Audio;
using DCL.AvatarRendering.Emotes;
using DCL.AvatarRendering.Emotes.Equipped;
using DCL.AvatarRendering.Wearables;
using DCL.AvatarRendering.Wearables.Equipped;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.AvatarRendering.Wearables.ThirdParty;
using DCL.Backpack;
using DCL.Backpack.BackpackBus;
using DCL.Browser;
using DCL.CharacterPreview;
using DCL.ExplorePanel;
using DCL.Input;
using DCL.Landscape.Settings;
using DCL.MapRenderer;
using DCL.Navmap;
using DCL.NotificationsBusController.NotificationsBus;
using DCL.PlacesAPIService;
using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.Quality;
using DCL.Settings;
using DCL.Settings.Configuration;
using DCL.UI.ProfileElements;
using DCL.UserInAppInitializationFlow;
using DCL.Web3.Authenticators;
using DCL.Web3.Identities;
using DCL.WebRequests;
using ECS;
using ECS.Prioritization;
using Global.Dynamic;
using MVC;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using DCL.Chat.MessageBus;
using DCL.Clipboard;
using DCL.EventsApi;
using DCL.Friends.UserBlocking;
using DCL.Navmap.ScriptableObjects;
using DCL.InWorldCamera.CameraReelGallery;
using DCL.InWorldCamera.CameraReelStorageService;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Optimization.PerformanceBudgeting;
using DCL.UI.Profiles.Helpers;
using DCL.SDKComponents.MediaStream.Settings;
using DCL.Settings.Settings;
using DCL.UI;
using DCL.UI.Profiles;
using DCL.UI.SharedSpaceManager;
using DCL.Utilities;
using ECS.SceneLifeCycle.IncreasingRadius;
using Global.AppArgs;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Audio;
using UnityEngine.Pool;
using Object = UnityEngine.Object;

// ReSharper disable UnusedAutoPropertyAccessor.Local
namespace DCL.PluginSystem.Global
{
    public class ExplorePanelPlugin : IDCLGlobalPlugin<ExplorePanelPlugin.ExplorePanelSettings>
    {
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly MapRendererContainer mapRendererContainer;
        private readonly IMVCManager mvcManager;
        private readonly IPlacesAPIService placesAPIService;
        private readonly IProfileRepository profileRepository;
        private readonly IUserInAppInitializationFlow userInAppInitializationFlow;
        private readonly ISelfProfile selfProfile;
        private readonly IEquippedWearables equippedWearables;
        private readonly IEquippedEmotes equippedEmotes;
        private readonly IWeb3Authenticator web3Authenticator;
        private readonly IWeb3IdentityCache web3IdentityCache;
        private readonly ICameraReelStorageService cameraReelStorageService;
        private readonly ICameraReelScreenshotsStorage cameraReelScreenshotsStorage;
        private readonly ISystemClipboard systemClipboard;
        private readonly IDecentralandUrlsSource decentralandUrlsSource;
        private readonly IWearableStorage wearableStorage;
        private readonly ICharacterPreviewFactory characterPreviewFactory;
        private readonly IWebBrowser webBrowser;
        private readonly IEmoteStorage emoteStorage;
        private readonly DCLInput dclInput;
        private readonly IWebRequestController webRequestController;
        private readonly CharacterPreviewEventBus characterPreviewEventBus;
        private readonly IBackpackEventBus backpackEventBus;
        private readonly IThirdPartyNftProviderSource thirdPartyNftProviderSource;
        private readonly IWearablesProvider wearablesProvider;
        private readonly ICursor cursor;
        private readonly IEmoteProvider emoteProvider;
        private readonly Arch.Core.World world;
        private readonly Entity playerEntity;
        private readonly IMapPathEventBus mapPathEventBus;
        private readonly List<string> forceRender;
        private readonly IRealmData realmData;
        private readonly IProfileCache profileCache;
        private readonly URLDomain assetBundleURL;
        private readonly INotificationsBusController notificationsBusController;
        private readonly IInputBlock inputBlock;
        private readonly IChatMessagesBus chatMessagesBus;
        private readonly ISystemMemoryCap systemMemoryCap;
        private readonly WorldVolumeMacBus worldVolumeMacBus;
        private readonly IEventsApiService eventsApiService;
        private readonly IUserCalendar userCalendar;
        private readonly ISystemClipboard clipboard;
        private readonly ObjectProxy<INavmapBus> explorePanelNavmapBus;
        private readonly IAppArgs appArgs;
        private readonly ObjectProxy<IUserBlockingCache> userBlockingCacheProxy;
        private readonly ISharedSpaceManager sharedSpaceManager;
        private readonly SceneLoadingLimit sceneLoadingLimit;
        private readonly WarningNotificationView inWorldWarningNotificationView;
        private readonly IProfileChangesBus profileChangesBus;
        private readonly bool includeCameraReel;

        private ExplorePanelInputHandler? inputHandler;
        private NavmapController? navmapController;
        private SettingsController? settingsController;
        private BackpackSubPlugin? backpackSubPlugin;
        private CategoryFilterController? categoryFilterController;
        private SearchResultPanelController? searchResultPanelController;
        private PlacesAndEventsPanelController? placesAndEventsPanelController;
        private NavmapView? navmapView;
        private PlaceInfoPanelController? placeInfoPanelController;
        private NavmapSearchBarController? searchBarController;
        private EventInfoPanelController? eventInfoPanelController;
        private ViewDependencies viewDependencies;
        private readonly ProfileRepositoryWrapper profileRepositoryWrapper;
        private readonly STPController stpController;

        public ExplorePanelPlugin(IAssetsProvisioner assetsProvisioner,
            IMVCManager mvcManager,
            MapRendererContainer mapRendererContainer,
            IPlacesAPIService placesAPIService,
            IWebRequestController webRequestController,
            IWeb3IdentityCache web3IdentityCache,
            ICameraReelStorageService cameraReelStorageService,
            ICameraReelScreenshotsStorage cameraReelScreenshotsStorage,
            ISystemClipboard systemClipboard,
            IDecentralandUrlsSource decentralandUrlsSource,
            IWearableStorage wearableStorage,
            ICharacterPreviewFactory characterPreviewFactory,
            IProfileRepository profileRepository,
            IWeb3Authenticator web3Authenticator,
            IUserInAppInitializationFlow userInAppInitializationFlow,
            ISelfProfile selfProfile,
            IEquippedWearables equippedWearables,
            IEquippedEmotes equippedEmotes,
            IWebBrowser webBrowser,
            IEmoteStorage emoteStorage,
            List<string> forceRender,
            DCLInput dclInput,
            IRealmData realmData,
            IProfileCache profileCache,
            URLDomain assetBundleURL,
            INotificationsBusController notificationsBusController,
            CharacterPreviewEventBus characterPreviewEventBus,
            IMapPathEventBus mapPathEventBus,
            IBackpackEventBus backpackEventBus,
            IThirdPartyNftProviderSource thirdPartyNftProviderSource,
            IWearablesProvider wearablesProvider,
            ICursor cursor,
            IInputBlock inputBlock,
            IEmoteProvider emoteProvider,
            Arch.Core.World world,
            Entity playerEntity,
            IChatMessagesBus chatMessagesBus,
            ISystemMemoryCap systemMemoryCap,
            WorldVolumeMacBus worldVolumeMacBus,
            IEventsApiService eventsApiService,
            IUserCalendar userCalendar,
            ISystemClipboard clipboard,
            ObjectProxy<INavmapBus> explorePanelNavmapBus,
            bool includeCameraReel,
            IAppArgs appArgs, ViewDependencies viewDependencies,
            ObjectProxy<IUserBlockingCache> userBlockingCacheProxy,
            ISharedSpaceManager sharedSpaceManager,
            IProfileChangesBus profileChangesBus,
            SceneLoadingLimit sceneLoadingLimit,
            WarningNotificationView inWorldWarningNotificationView,
            ProfileRepositoryWrapper profileDataProvider,
            STPController stpController)
        {
            this.assetsProvisioner = assetsProvisioner;
            this.mvcManager = mvcManager;
            this.mapRendererContainer = mapRendererContainer;
            this.placesAPIService = placesAPIService;
            this.webRequestController = webRequestController;
            this.web3IdentityCache = web3IdentityCache;
            this.cameraReelStorageService = cameraReelStorageService;
            this.cameraReelScreenshotsStorage = cameraReelScreenshotsStorage;
            this.systemClipboard = systemClipboard;
            this.decentralandUrlsSource = decentralandUrlsSource;
            this.wearableStorage = wearableStorage;
            this.characterPreviewFactory = characterPreviewFactory;
            this.profileRepository = profileRepository;
            this.web3Authenticator = web3Authenticator;
            this.userInAppInitializationFlow = userInAppInitializationFlow;
            this.selfProfile = selfProfile;
            this.equippedWearables = equippedWearables;
            this.equippedEmotes = equippedEmotes;
            this.webBrowser = webBrowser;
            this.forceRender = forceRender;
            this.realmData = realmData;
            this.profileCache = profileCache;
            this.assetBundleURL = assetBundleURL;
            this.notificationsBusController = notificationsBusController;
            this.emoteStorage = emoteStorage;
            this.dclInput = dclInput;
            this.characterPreviewEventBus = characterPreviewEventBus;
            this.mapPathEventBus = mapPathEventBus;
            this.backpackEventBus = backpackEventBus;
            this.thirdPartyNftProviderSource = thirdPartyNftProviderSource;
            this.wearablesProvider = wearablesProvider;
            this.inputBlock = inputBlock;
            this.cursor = cursor;
            this.emoteProvider = emoteProvider;
            this.world = world;
            this.playerEntity = playerEntity;
            this.chatMessagesBus = chatMessagesBus;
            this.systemMemoryCap = systemMemoryCap;
            this.worldVolumeMacBus = worldVolumeMacBus;
            this.eventsApiService = eventsApiService;
            this.userCalendar = userCalendar;
            this.clipboard = clipboard;
            this.explorePanelNavmapBus = explorePanelNavmapBus;
            this.includeCameraReel = includeCameraReel;
            this.appArgs = appArgs;
            this.viewDependencies = viewDependencies;
            this.userBlockingCacheProxy = userBlockingCacheProxy;
            this.sharedSpaceManager = sharedSpaceManager;
            this.profileChangesBus = profileChangesBus;
            this.sceneLoadingLimit = sceneLoadingLimit;
            this.inWorldWarningNotificationView = inWorldWarningNotificationView;
            this.profileRepositoryWrapper = profileDataProvider;
            this.stpController = stpController;
        }

        public void Dispose()
        {
            categoryFilterController?.Dispose();
            navmapController?.Dispose();
            settingsController?.Dispose();
            backpackSubPlugin?.Dispose();
            inputHandler?.Dispose();
            placeInfoPanelController?.Dispose();
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments) { }

        public async UniTask InitializeAsync(ExplorePanelSettings settings, CancellationToken ct)
        {
            INavmapBus navmapBus = new NavmapCommandBus(CreateSearchPlaceCommand,
                CreateShowPlaceCommand, CreateShowEventCommand, placesAPIService);
            explorePanelNavmapBus.SetObject(navmapBus);

            backpackSubPlugin = new BackpackSubPlugin(
                assetsProvisioner,
                web3IdentityCache,
                characterPreviewFactory,
                wearableStorage,
                selfProfile,
                profileCache,
                equippedWearables,
                equippedEmotes,
                emoteStorage,
                settings.EmbeddedEmotesAsURN(),
                forceRender,
                realmData,
                assetBundleURL,
                webRequestController,
                characterPreviewEventBus,
                backpackEventBus,
                thirdPartyNftProviderSource,
                wearablesProvider,
                inputBlock,
                cursor,
                emoteProvider,
                world,
                playerEntity,
                appArgs,
                webBrowser,
                inWorldWarningNotificationView
            );

            ExplorePanelView panelViewAsset = (await assetsProvisioner.ProvideMainAssetValueAsync(settings.ExplorePanelPrefab, ct: ct)).GetComponent<ExplorePanelView>();
            ControllerBase<ExplorePanelView, ExplorePanelParameter>.ViewFactoryMethod viewFactoryMethod = ExplorePanelController.Preallocate(panelViewAsset, null, out ExplorePanelView explorePanelView);

            ProvidedAsset<SettingsMenuConfiguration> settingsMenuConfiguration = await assetsProvisioner.ProvideMainAssetAsync(settings.SettingsMenuConfiguration, ct);
            ProvidedAsset<AudioMixer> generalAudioMixer = await assetsProvisioner.ProvideMainAssetAsync(settings.GeneralAudioMixer, ct);
            ProvidedAsset<RealmPartitionSettingsAsset> realmPartitionSettings = await assetsProvisioner.ProvideMainAssetAsync(settings.RealmPartitionSettings, ct);
            ProvidedAsset<VideoPrioritizationSettings> videoPrioritizationSettings = await assetsProvisioner.ProvideMainAssetAsync(settings.VideoPrioritizationSettings, ct);

            ProvidedAsset<LandscapeData> landscapeData = await assetsProvisioner.ProvideMainAssetAsync(settings.LandscapeData, ct);
            ProvidedAsset<QualitySettingsAsset> qualitySettingsAsset = await assetsProvisioner.ProvideMainAssetAsync(settings.QualitySettingsAsset, ct);
            ProvidedAsset<ControlsSettingsAsset> controlsSettingsAsset = await assetsProvisioner.ProvideMainAssetAsync(settings.ControlsSettingsAsset, ct);
            ProvidedAsset<ChatSettingsAsset> chatSettingsAsset = await assetsProvisioner.ProvideMainAssetAsync(settings.ChatSettingsAsset, ct);

            ProvidedAsset<CategoryMappingSO> categoryMappingSO = await assetsProvisioner.ProvideMainAssetAsync(settings.CategoryMappingSO, ct);

            navmapView = explorePanelView.GetComponentInChildren<NavmapView>();
            categoryFilterController = new CategoryFilterController(navmapView.categoryToggles, mapRendererContainer.MapRenderer, navmapBus);

            NavmapZoomController zoomController = new (navmapView.zoomView, dclInput, navmapBus);

            ObjectPool<PlaceElementView> placeElementsPool = await InitializePlaceElementsPoolAsync(navmapView.SearchBarResultPanel, ct);
            ObjectPool<EventElementView> eventElementsPool = await InitializeEventElementsForPlacePoolAsync(navmapView.PlacesAndEventsPanelView.PlaceInfoPanelView, ct);
            ObjectPool<EventScheduleElementView> eventScheduleElementsPool = await InitializeEventScheduleElementsPoolAsync(navmapView.PlacesAndEventsPanelView.EventInfoPanelView, ct);

            searchResultPanelController = new SearchResultPanelController(navmapView.SearchBarResultPanel,
                placeElementsPool, navmapBus);

            searchBarController = new NavmapSearchBarController(navmapView.SearchBarView,
                navmapView.HistoryRecordPanelView, navmapView.PlacesAndEventsPanelView.SearchFiltersView,
                inputBlock, navmapBus, categoryMappingSO.Value);

            SharePlacesAndEventsContextMenuController shareContextMenu = new (navmapView.ShareContextMenuView,
                navmapView.WorldsWarningNotificationView, clipboard, webBrowser);

            placeInfoPanelController = new PlaceInfoPanelController(navmapView.PlacesAndEventsPanelView.PlaceInfoPanelView,
                webRequestController, placesAPIService, mapPathEventBus, navmapBus, chatMessagesBus, eventsApiService,
                eventElementsPool, shareContextMenu, webBrowser, mvcManager, cameraReelStorageService, cameraReelScreenshotsStorage,
                new ReelGalleryConfigParams(settings.PlaceGridLayoutFixedColumnCount, settings.PlaceThumbnailHeight, settings.PlaceThumbnailWidth, false, false), false);

            eventInfoPanelController = new EventInfoPanelController(navmapView.PlacesAndEventsPanelView.EventInfoPanelView,
                webRequestController, navmapBus, chatMessagesBus, eventsApiService, eventScheduleElementsPool,
                userCalendar, shareContextMenu, webBrowser);

            placesAndEventsPanelController = new PlacesAndEventsPanelController(navmapView.PlacesAndEventsPanelView,
                searchBarController, searchResultPanelController, placeInfoPanelController, eventInfoPanelController,
                zoomController);

            IMapRenderer mapRenderer = mapRendererContainer.MapRenderer;

            SatelliteController satelliteController = new (navmapView.GetComponentInChildren<SatelliteView>(),
                navmapView.MapCameraDragBehaviorData, mapRenderer, webBrowser);

            PlaceInfoToastController placeToastController = new (navmapView.PlaceToastView,
                new PlaceInfoPanelController(navmapView.PlaceToastView.PlacePanelView,
                    webRequestController, placesAPIService, mapPathEventBus, navmapBus, chatMessagesBus, eventsApiService,
                    eventElementsPool, shareContextMenu, webBrowser, mvcManager),
                placesAPIService, eventsApiService, navmapBus);

            settingsController = new SettingsController(
                explorePanelView.GetComponentInChildren<SettingsView>(),
                settingsMenuConfiguration.Value,
                generalAudioMixer.Value,
                realmPartitionSettings.Value,
                videoPrioritizationSettings.Value,
                landscapeData.Value,
                qualitySettingsAsset.Value,
                controlsSettingsAsset.Value,
                systemMemoryCap,
                chatSettingsAsset.Value,
                userBlockingCacheProxy,
                sceneLoadingLimit,
                worldVolumeMacBus,
                stpController);
            navmapController = new NavmapController(
                navmapView: explorePanelView.GetComponentInChildren<NavmapView>(),
                mapRendererContainer.MapRenderer,
                realmData,
                mapPathEventBus,
                world,
                playerEntity,
                navmapBus,
                UIAudioEventsBus.Instance,
                placesAndEventsPanelController,
                searchBarController,
                zoomController,
                satelliteController,
                placeToastController,
                placesAPIService);

            await backpackSubPlugin.InitializeAsync(settings.BackpackSettings, explorePanelView.GetComponentInChildren<BackpackView>(), ct);

            inputHandler = new ExplorePanelInputHandler(dclInput);

            CameraReelView cameraReelView = explorePanelView.GetComponentInChildren<CameraReelView>();
            var cameraReelController = new CameraReelController(cameraReelView,
                new CameraReelGalleryController(cameraReelView.CameraReelGalleryView, this.cameraReelStorageService,
                    cameraReelScreenshotsStorage,
                    new ReelGalleryConfigParams(settings.GridLayoutFixedColumnCount, settings.ThumbnailHeight, settings.ThumbnailWidth, true, true), true,
                    cameraReelView.CameraReelOptionsButton,
                    webBrowser, decentralandUrlsSource, inputHandler, systemClipboard,
                    new ReelGalleryStringMessages(settings.CameraReelGalleryShareToXMessage, settings.PhotoSuccessfullyDeletedMessage, settings.PhotoSuccessfullyUpdatedMessage, settings.PhotoSuccessfullyDownloadedMessage, settings.LinkCopiedMessage),
                    mvcManager),
                cameraReelStorageService,
                web3IdentityCache,
                mvcManager,
                settings.StorageProgressBarText);

            ExplorePanelController explorePanelController = new
                ExplorePanelController(viewFactoryMethod, navmapController, settingsController, backpackSubPlugin.backpackController!, cameraReelController,
                    new ProfileWidgetController(() => explorePanelView.ProfileWidget, web3IdentityCache, profileRepository, profileChangesBus, profileRepositoryWrapper),
                    new ProfileMenuController(() => explorePanelView.ProfileMenuView, web3IdentityCache, profileRepository, world, playerEntity, webBrowser, web3Authenticator, userInAppInitializationFlow, profileCache, mvcManager, profileRepositoryWrapper),
                    dclInput, inputHandler, notificationsBusController, inputBlock, includeCameraReel, sharedSpaceManager);

            sharedSpaceManager.RegisterPanel(PanelsSharingSpace.Explore, explorePanelController);
            mvcManager.RegisterController(explorePanelController);
        }

        private async UniTask<ObjectPool<PlaceElementView>> InitializePlaceElementsPoolAsync(SearchResultPanelView view, CancellationToken ct)
        {
            PlaceElementView asset = (await assetsProvisioner.ProvideInstanceAsync(view.ResultRef, ct: ct)).Value;

            return new ObjectPool<PlaceElementView>(
                () => CreatePoolElements(asset),
                actionOnGet: result => result.gameObject.SetActive(true),
                actionOnRelease: result => result.gameObject.SetActive(false),
                defaultCapacity: 8
            );

            PlaceElementView CreatePoolElements(PlaceElementView asset)
            {
                PlaceElementView placeElementView = Object.Instantiate(asset, view.searchResultsContainer);
                placeElementView.ConfigurePlaceImageController(webRequestController);
                return placeElementView;
            }
        }

        private async UniTask<ObjectPool<EventElementView>> InitializeEventElementsForPlacePoolAsync(PlaceInfoPanelView view, CancellationToken ct)
        {
            EventElementView asset = (await assetsProvisioner.ProvideInstanceAsync(view.EventElementViewRef, ct: ct)).Value;

            return new ObjectPool<EventElementView>(
                () => CreatePoolElements(asset),
                actionOnGet: result => result.gameObject.SetActive(true),
                actionOnRelease: result => result.gameObject.SetActive(false),
                defaultCapacity: 8
            );

            EventElementView CreatePoolElements(EventElementView asset)
            {
                EventElementView placeElementView = Object.Instantiate(asset, view.EventsContentContainer.transform);
                return placeElementView;
            }
        }

        private async UniTask<ObjectPool<EventScheduleElementView>> InitializeEventScheduleElementsPoolAsync(EventInfoPanelView view, CancellationToken ct)
        {
            EventScheduleElementView asset = (await assetsProvisioner.ProvideInstanceAsync(view.ScheduleElementRef, ct: ct)).Value;

            return new ObjectPool<EventScheduleElementView>(
                () => CreatePoolElements(asset),
                actionOnGet: result => result.gameObject.SetActive(true),
                actionOnRelease: result => result.gameObject.SetActive(false),
                defaultCapacity: 8
            );

            EventScheduleElementView CreatePoolElements(EventScheduleElementView asset)
            {
                EventScheduleElementView placeElementView = Object.Instantiate(asset, view.ScheduleElementsContainer);
                return placeElementView;
            }
        }

        private INavmapCommand CreateSearchPlaceCommand(INavmapBus.SearchPlaceResultDelegate callback, INavmapBus.SearchPlaceParams @params) =>
            new SearchForPlaceAndShowResultsCommand(placesAPIService, eventsApiService, placesAndEventsPanelController!,
                searchResultPanelController!, searchBarController!, callback,
                @params);

        private INavmapCommand<AdditionalParams> CreateShowPlaceCommand(PlacesData.PlaceInfo placeInfo) =>
            new ShowPlaceInfoCommand(placeInfo, navmapView!, placeInfoPanelController!, placesAndEventsPanelController!, eventsApiService,
                searchBarController!);

        private INavmapCommand CreateShowEventCommand(EventDTO @event, PlacesData.PlaceInfo? place = null) =>
            new ShowEventInfoCommand(@event, eventInfoPanelController!, placesAndEventsPanelController!,
                searchBarController!, placesAPIService, place);

        public class ExplorePanelSettings : IDCLPluginSettings
        {
            [field: Header(nameof(ExplorePanelPlugin) + "." + nameof(ExplorePanelSettings))]
            [field: Space]
            [field: SerializeField]
            public AssetReferenceGameObject ExplorePanelPrefab;

            [field: SerializeField]
            public BackpackSettings BackpackSettings { get; private set; }

            [field: SerializeField]
            public string[] EmbeddedEmotes { get; private set; }

            [field: SerializeField]
            public AssetReferenceT<SettingsMenuConfiguration> SettingsMenuConfiguration { get; private set; }

            [field: SerializeField]
            public AssetReferenceT<AudioMixer> GeneralAudioMixer { get; private set; }

            [field: SerializeField]
            public StaticSettings.RealmPartitionSettingsRef RealmPartitionSettings { get; private set; }

            [field: SerializeField]
            public StaticSettings.VideoPrioritizationSettingsRef VideoPrioritizationSettings { get; private set; }

            [field: SerializeField]
            public LandscapeSettings.LandscapeDataRef LandscapeData { get; private set; }

            [field: SerializeField]
            public AssetReferenceT<QualitySettingsAsset> QualitySettingsAsset { get; private set; }

            [field: SerializeField]
            public AssetReferenceT<ControlsSettingsAsset> ControlsSettingsAsset { get; private set; }

            [field: SerializeField]
            public AssetReferenceT<ChatSettingsAsset> ChatSettingsAsset { get; private set; }

            [field: SerializeField]
            public AssetReferenceT<CategoryMappingSO> CategoryMappingSO { get; private set; }

            [field: Header("Camera Reel")]
            [field: SerializeField]
            [field: Tooltip("Spaces will be HTTP sanitized, care for special characters")]
            public string CameraReelGalleryShareToXMessage { get; private set; }
            [field: SerializeField]
            public string PhotoSuccessfullyUpdatedMessage { get; private set; }
            [field: SerializeField]
            public string PhotoSuccessfullyDeletedMessage { get; private set; }
            [field: SerializeField]
            public string PhotoSuccessfullyDownloadedMessage { get; private set; }
            [field: SerializeField]
            public string LinkCopiedMessage { get; private set; }
            [field: SerializeField]
            public string StorageProgressBarText { get; private set; }

            [field: SerializeField]
            public int GridLayoutFixedColumnCount { get; private set; }
            [field: SerializeField]
            public int ThumbnailHeight { get; private set; }
            [field: SerializeField]
            public int ThumbnailWidth { get; private set; }

            [field: Header("Place Reel")]

            [field: SerializeField]
            public int PlaceGridLayoutFixedColumnCount { get; private set; }

            [field: SerializeField]
            public int PlaceThumbnailHeight { get; private set; }

            [field: SerializeField]
            public int PlaceThumbnailWidth { get; private set; }

            public IReadOnlyCollection<URN> EmbeddedEmotesAsURN() =>
                EmbeddedEmotes.Select(s => new URN(s)).ToArray();
        }
    }
}
