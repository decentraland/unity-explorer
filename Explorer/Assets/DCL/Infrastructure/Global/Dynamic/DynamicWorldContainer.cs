using Arch.Core;
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
using DCL.Backpack.BackpackBus;
using DCL.BadgesAPIService;
using DCL.Browser;
using DCL.CharacterPreview;
using DCL.Chat.ChatServices;
using DCL.Chat.Commands;
using DCL.Chat.EventBus;
using DCL.Chat.History;
using DCL.Chat.MessageBus;
using DCL.ChatArea;
using DCL.Clipboard;
using DCL.Communities;
using DCL.Communities.CommunitiesCard.Members;
using DCL.Communities.CommunitiesDataProvider;
using DCL.DebugUtilities;
using DCL.Diagnostics;
using DCL.Donations;
using DCL.EventsApi;
using DCL.FeatureFlags;
using DCL.Friends;
using DCL.Friends.Passport;
using DCL.Friends.UserBlocking;
using DCL.Input;
using DCL.InWorldCamera;
using DCL.InWorldCamera.CameraReelStorageService;
using DCL.LOD;
using DCL.LOD.Systems;
using DCL.MapRenderer;
using DCL.Multiplayer.Connections.Archipelago.AdapterAddress.Current;
using DCL.Multiplayer.Connections.Archipelago.Rooms;
using DCL.Multiplayer.Connections.Archipelago.Rooms.Chat;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Multiplayer.Connections.GateKeeper.Meta;
using DCL.Multiplayer.Connections.GateKeeper.Rooms;
using DCL.Multiplayer.Connections.GateKeeper.Rooms.Options;
using DCL.Multiplayer.Connections.Messaging.Hubs;
using DCL.Multiplayer.Connections.Pools;

using DCL.LiveKit.Public;

#if !NO_LIVEKIT_MODE
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Multiplayer.Connections.Rooms.Status;
using DCL.Multiplayer.Profiles.Poses;
#endif

using DCL.Multiplayer.Connections.Rooms.Connective;
using DCL.Multiplayer.Connections.Systems.Throughput;
using DCL.Multiplayer.Connectivity;
using DCL.Multiplayer.Emotes;
using DCL.Multiplayer.HealthChecks;
using DCL.Multiplayer.Movement;
using DCL.Multiplayer.Movement.Systems;
using DCL.Multiplayer.Profiles.BroadcastProfiles;
using DCL.Multiplayer.Profiles.Entities;
using DCL.Multiplayer.Profiles.Tables;
using DCL.Multiplayer.SDK.Systems.GlobalWorld;
using DCL.Navmap;
using DCL.NftInfoAPIService;
using DCL.Notifications;
using DCL.Optimization.Pools;
using DCL.PerformanceAndDiagnostics.Analytics;
using DCL.PlacesAPIService;
using DCL.PluginSystem;
using DCL.PluginSystem.Global;
using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.RealmNavigation;
using DCL.Rendering.GPUInstancing.Systems;
using DCL.RuntimeDeepLink;
using DCL.SceneLoadingScreens.LoadingScreen;
using DCL.SkyBox;
using DCL.SocialService;
using DCL.UI;
using DCL.UI.ConfirmationDialog;
using DCL.UI.InputFieldFormatting;
using DCL.UI.MainUI;
using DCL.UI.ProfileElements;
using DCL.UI.Profiles.Helpers;
using DCL.UI.SharedSpaceManager;
using DCL.UserInAppInitializationFlow;
using DCL.Utilities;
using DCL.Utilities.Extensions;
using DCL.VoiceChat;
using DCL.Web3.Identities;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.CurrentScene;
using ECS.SceneLifeCycle.Realm;
using Global.AppArgs;
using Global.Dynamic.ChatCommands;
using Global.Dynamic.RealmUrl;
using Global.Versioning;
using LiveKit.Internal.FFIClients.Pools;
using LiveKit.Internal.FFIClients.Pools.Memory;
using MVC;
using MVC.PopupsController.PopupCloser;
using SceneRunner.Debugging.Hub;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using DCL.MapRenderer.MapLayers.HomeMarker;
using DCL.Backpack.Gifting.Services;
using DCL.Backpack.Gifting.Services.PendingTransfers;
using DCL.Backpack.Gifting.Services.SnapshotEquipped;
using DCL.NotificationsBus;
using DCL.PluginSystem.SmartWearables;
using DCL.Optimization.AdaptivePerformance.Systems;
using DCL.PluginSystem.World;
using DCL.SDKComponents.AvatarLocomotion;
using DCL.Settings.ScreenMode;
using DCL.PerformanceAndDiagnostics.Analytics.DecoratorBased;
using DCL.PrivateWorlds;
using DCL.Translation;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.EventSystems;
using UnityEngine.Pool;
using Utility;
using Utility.Ownership;
using Utility.PriorityQueue;

#if !NO_LIVEKIT_MODE
using MultiplayerPlugin = DCL.PluginSystem.Global.MultiplayerPlugin;
#endif

using Object = UnityEngine.Object;

namespace Global.Dynamic
{
    public class DynamicWorldContainer : DCLWorldContainer<DynamicWorldSettings>
    {

#if !NO_LIVEKIT_MODE
        private readonly IChatMessagesBus chatMessagesBus;
#endif

        private readonly IChatHistory chatHistory;
        private readonly IProfileBroadcast profileBroadcast;
        private readonly SocialServicesContainer socialServicesContainer;
        private readonly ISelfProfile selfProfile;

        public IMVCManager MvcManager { get; }

        public IGlobalRealmController RealmController { get; }

        public IRealmNavigator RealmNavigator { get; }

        public GlobalWorldFactory GlobalWorldFactory { get; }

        public IReadOnlyList<IDCLGlobalPlugin> GlobalPlugins { get; }

        public IProfileRepository ProfileRepository { get; }

        public IUserInAppInitializationFlow UserInAppInAppInitializationFlow { get; }

        public IMessagePipesHub MessagePipesHub { get; }

#if !NO_LIVEKIT_MODE
        public IRemoteMetadata RemoteMetadata { get; }

        public IRoomHub RoomHub { get; }
#endif

        public ISystemClipboard SystemClipboard { get; }

        public IChatHistory ChatHistory => chatHistory;

        private DynamicWorldContainer(
            IMVCManager mvcManager,
            IGlobalRealmController realmController,
            IRealmNavigator realmNavigator,
            GlobalWorldFactory globalWorldFactory,
            IReadOnlyList<IDCLGlobalPlugin> globalPlugins,
            IProfileRepository profileRepository,
            IUserInAppInitializationFlow userInAppInAppInitializationFlow,

#if !NO_LIVEKIT_MODE
            IChatMessagesBus chatMessagesBus,
            IChatHistory chatHistory,
            IMessagePipesHub messagePipesHub,
            IRemoteMetadata remoteMetadata,
#endif

            IProfileBroadcast profileBroadcast,

#if !NO_LIVEKIT_MODE
            IRoomHub roomHub,
#endif

            SocialServicesContainer socialServicesContainer,
            ISelfProfile selfProfile,
            ISystemClipboard systemClipboard)
        {
            MvcManager = mvcManager;
            RealmController = realmController;
            RealmNavigator = realmNavigator;
            GlobalWorldFactory = globalWorldFactory;
            GlobalPlugins = globalPlugins;
            ProfileRepository = profileRepository;
            UserInAppInAppInitializationFlow = userInAppInAppInitializationFlow;
            MessagePipesHub = messagePipesHub;

#if !NO_LIVEKIT_MODE
            RemoteMetadata = remoteMetadata;
            RoomHub = roomHub;
#endif

            SystemClipboard = systemClipboard;
            this.chatMessagesBus = chatMessagesBus;
            this.chatHistory = chatHistory;
            this.profileBroadcast = profileBroadcast;
            this.socialServicesContainer = socialServicesContainer;
            this.selfProfile = selfProfile;
        }

        public override void Dispose()
        {
            chatMessagesBus.Dispose();
            profileBroadcast.Dispose();
            MessagePipesHub.Dispose();
            socialServicesContainer.Dispose();
            selfProfile.Dispose();
        }

        public static async UniTask<(DynamicWorldContainer? container, bool success)> CreateAsync(
            BootstrapContainer bootstrapContainer,
            DynamicWorldDependencies dynamicWorldDependencies,
            DynamicWorldParams dynamicWorldParams,
            AudioClipConfig backgroundMusic,
            World globalWorld,
            Entity playerEntity,
            IAppArgs appArgs,
            ICoroutineRunner coroutineRunner,
            DCLVersion dclVersion,
            RealmUrls realmUrls,
            CancellationToken ct)
        {
            DynamicSettings dynamicSettings = dynamicWorldDependencies.DynamicSettings;
            StaticContainer staticContainer = dynamicWorldDependencies.StaticContainer;
            IWeb3IdentityCache identityCache = dynamicWorldDependencies.Web3IdentityCache;
            IAssetsProvisioner assetsProvisioner = dynamicWorldDependencies.AssetsProvisioner;
            IDebugContainerBuilder debugBuilder = dynamicWorldDependencies.DebugContainerBuilder;
            var explorePanelNavmapBus = new ObjectProxy<INavmapBus>();
            INavmapBus sharedNavmapCommandBus = new SharedNavmapBus(explorePanelNavmapBus);

            // If we have many undesired delays when using the third-party providers, it might be useful to cache it at app's bootstrap
            // So far, the chance of using it is quite low, so it's preferable to do it lazy avoiding extra requests & memory allocations
            IThirdPartyNftProviderSource thirdPartyNftProviderSource = new RealmThirdPartyNftProviderSource(staticContainer.WebRequestsContainer.WebRequestController,
                bootstrapContainer.DecentralandUrlsSource);

            var placesAPIService = new PlacesAPIService(new PlacesAPIClient(staticContainer.WebRequestsContainer.WebRequestController, bootstrapContainer.DecentralandUrlsSource));

            var eventsApiService = new HttpEventsApiService(staticContainer.WebRequestsContainer.WebRequestController, bootstrapContainer.DecentralandUrlsSource);

            var mapPathEventBus = new MapPathEventBus();
            NotificationsBusController.Initialize(new NotificationsBusController());

            DefaultTexturesContainer defaultTexturesContainer = null!;
            LODContainer? lodContainer = null;
            ILODCache lodCache = null!;
            HashSet<Vector2Int> roadCoordinates = null!;
            ILODSettingsAsset lodSettings = null!;
            RoadAssetsPool roadAssetsPool = null!;

            IOnlineUsersProvider baseUserProvider = new ArchipelagoHttpOnlineUsersProvider(staticContainer.WebRequestsContainer.WebRequestController,
                URLAddress.FromString(bootstrapContainer.DecentralandUrlsSource.Url(DecentralandUrl.RemotePeers)));

            var onlineUsersProvider = new WorldInfoOnlineUsersProviderDecorator(
                baseUserProvider,
                staticContainer.WebRequestsContainer.WebRequestController,
                URLAddress.FromString(bootstrapContainer.DecentralandUrlsSource.Url(DecentralandUrl.RemotePeersWorld)));

            var screenModeController = new ScreenModeController(appArgs);

            async UniTask InitializeContainersAsync(IPluginSettingsContainer settingsContainer, CancellationToken ct)
            {
                // Init other containers
                defaultTexturesContainer =
                    await DefaultTexturesContainer
                         .CreateAsync(
                              settingsContainer,
                              assetsProvisioner,
                              appArgs,
                              ct
                          )
                         .ThrowOnFail();

#if UNITY_WEBGL
                lodCache = new NoOpLODCache();
                roadCoordinates = new HashSet<Vector2Int>();
                lodSettings = new WebGLLODSettingsStub();
                roadAssetsPool = new RoadAssetsPool(staticContainer.RealmData, new List<GameObject>(), null);
#else
                lodContainer = await LODContainer
                    .CreateAsync(
                        assetsProvisioner,
                        staticContainer,
                        settingsContainer,
                        staticContainer.RealmData,
                        defaultTexturesContainer.TextureArrayContainerFactory,
                        debugBuilder,
                        dynamicWorldParams.EnableLOD,
                        staticContainer.GPUInstancingService,
                        ct
                    )
                    .ThrowOnFail();
                lodCache = lodContainer.LodCache;
                roadCoordinates = lodContainer.RoadCoordinates;
                lodSettings = lodContainer.LODSettings;
                roadAssetsPool = lodContainer.RoadAssetsPool;
#endif
            }

            try { await InitializeContainersAsync(dynamicWorldDependencies.SettingsContainer, ct); }
            catch (Exception) { return (null, false); }

            CursorSettings cursorSettings = (await assetsProvisioner.ProvideMainAssetAsync(dynamicSettings.CursorSettings, ct)).Value;
            ProvidedAsset<Texture2D> normalCursorAsset = await assetsProvisioner.ProvideMainAssetAsync(cursorSettings.NormalCursor, ct);
            ProvidedAsset<Texture2D> interactionCursorAsset = await assetsProvisioner.ProvideMainAssetAsync(cursorSettings.InteractionCursor, ct);

            var unityEventSystem = new UnityEventSystem(EventSystem.current.EnsureNotNull());
            var dclCursor = new DCLCursor(normalCursorAsset.Value, interactionCursorAsset.Value, cursorSettings.NormalCursorHotspot, cursorSettings.InteractionCursorHotspot);

            staticContainer.QualityContainer.AddDebugViews(debugBuilder);
            var realmSamplingData = new RealmSamplingData();

            ExposedGlobalDataContainer exposedGlobalDataContainer = staticContainer.ExposedGlobalDataContainer;

            PopupCloserView popupCloserView = Object.Instantiate((await assetsProvisioner.ProvideMainAssetAsync(dynamicSettings.PopupCloserView, CancellationToken.None)).Value.GetComponent<PopupCloserView>()).EnsureNotNull();
            MainUIView mainUIView = Object.Instantiate((await assetsProvisioner.ProvideMainAssetAsync(dynamicSettings.MainUIView, CancellationToken.None)).Value.GetComponent<MainUIView>()).EnsureNotNull();

            var coreMvcManager = new MVCManager(new WindowStackManager(), new CancellationTokenSource(), popupCloserView);

#if UNITY_WEBGL
            IMVCManager mvcManager = coreMvcManager;
#else

            IMVCManager mvcManager = dynamicWorldParams.EnableAnalytics
                ? new MVCManagerAnalyticsDecorator(coreMvcManager, bootstrapContainer.Analytics.Controller)
                : coreMvcManager;
#endif
            var loadingScreenTimeout = new LoadingScreenTimeout();
            ILoadingScreen loadingScreen = new LoadingScreen(mvcManager, loadingScreenTimeout);

            var nftInfoAPIClient = new OpenSeaAPIClient(staticContainer.WebRequestsContainer.WebRequestController, bootstrapContainer.DecentralandUrlsSource);
            var wearableCatalog = new WearableStorage();
            var trimmedWearableCatalog = new TrimmedWearableStorage();
            var trimmedEmoteCatalog = new TrimmedEmoteStorage();
            var characterPreviewFactory = new CharacterPreviewFactory(staticContainer.ComponentsContainer.ComponentPoolsRegistry, appArgs);
            IWebBrowser webBrowser = bootstrapContainer.WebBrowser;
            ISystemClipboard clipboard = new UnityClipboard();

            ChatSharedAreaEventBus chatSharedAreaEventBus = new ChatSharedAreaEventBus();

            GalleryEventBus galleryEventBus = new GalleryEventBus();

            static IMultiPool MultiPoolFactory() =>
                new DCLMultiPool();

            var memoryPool = new ArrayMemoryPool(ArrayPool<byte>.Shared!);

            var builderDTOsURL = URLDomain.FromString(bootstrapContainer.DecentralandUrlsSource.Url(DecentralandUrl.BuilderApiDtos));
            var builderContentURL = URLDomain.FromString(bootstrapContainer.DecentralandUrlsSource.Url(DecentralandUrl.BuilderApiContent));

            var emotesCache = new MemoryEmotesStorage();
            staticContainer.CacheCleaner.Register(emotesCache);
            staticContainer.CacheCleaner.Register(trimmedEmoteCatalog);
            var equippedWearables = new EquippedWearables();
            var equippedEmotes = new EquippedEmotes();

            var selfEmotes = new List<URN>();
            ParseParamsForcedEmotes(bootstrapContainer.AppArgs, ref selfEmotes);
            ParseDebugForcedEmotes(bootstrapContainer.DebugSettings.EmotesToAddToUserProfile, ref selfEmotes);

            IProfileRepository profilesRepository = staticContainer.ProfilesContainer.Repository;
            IProfileCache profileCache = staticContainer.ProfilesContainer.Cache;

            var selfProfile = new SelfProfile(profilesRepository, identityCache, equippedWearables, wearableCatalog,
                emotesCache, equippedEmotes, selfEmotes, profileCache, globalWorld, playerEntity);

            IGiftingPersistence giftingPersistence = new PlayerPrefsGiftingPersistence();
            IPendingTransferService pendingTransferService = new PendingTransferService(giftingPersistence);
            IAvatarEquippedStatusProvider equippedStatusProvider = new AvatarEquippedStatusProvider(selfProfile);
            var communitiesDataProvider = new CommunitiesDataProvider(staticContainer.WebRequestsContainer.WebRequestController, bootstrapContainer.DecentralandUrlsSource, identityCache);
            var communityMembershipChecker = new CommunityMembershipCheckerAdapter(communitiesDataProvider);
            IWorldPermissionsService worldPermissionsService = new WorldPermissionsService(staticContainer.WebRequestsContainer.WebRequestController,
                bootstrapContainer.DecentralandUrlsSource,
                identityCache,
                communityMembershipChecker);
            IEmoteProvider emoteProvider = new ApplicationParamsEmoteProvider(appArgs,
                new EcsEmoteProvider(globalWorld, identityCache), builderDTOsURL.Value);

            var wearablesProvider = new ApplicationParametersWearablesProvider(appArgs,
                new ECSWearablesProvider(identityCache, globalWorld), builderDTOsURL.Value);

            //TODO should be unified with LaunchMode
            bool localSceneDevelopment = !string.IsNullOrEmpty(dynamicWorldParams.LocalSceneDevelopmentRealm);
            bool builderCollectionsPreview = appArgs.HasFlag(AppArgsFlags.SELF_PREVIEW_BUILDER_COLLECTIONS);

            var teleportController = new TeleportController(staticContainer.SceneReadinessReportQueue);

            var realmContainer = RealmContainer.Create(
                staticContainer,
                identityCache,
                dynamicWorldParams.StaticLoadPositions,
                debugBuilder,
                loadingScreenTimeout,
                loadingScreen,
                localSceneDevelopment,
                bootstrapContainer.DecentralandUrlsSource,
                appArgs,
                teleportController,
                bootstrapContainer.Environment,
                worldPermissionsService);

            TerrainContainer terrainContainer = TerrainContainer.Create(
                staticContainer,
                realmContainer,
                dynamicWorldParams.EnableLandscape,
                localSceneDevelopment
            );

            SceneRoomLogMetaDataSource playSceneMetaDataSource = new SceneRoomMetaDataSource(staticContainer.RealmData, staticContainer.CharacterContainer.Transform, globalWorld, dynamicWorldParams.IsolateScenesCommunication, bootstrapContainer.DecentralandUrlsSource).WithLog();
            SceneRoomLogMetaDataSource localDevelopmentMetaDataSource = new LocalSceneDevelopmentSceneRoomMetaDataSource(staticContainer.WebRequestsContainer.WebRequestController).WithLog();

            var gateKeeperSceneRoomOptions = new GateKeeperSceneRoomOptions(staticContainer.LaunchMode,
                bootstrapContainer.DecentralandUrlsSource,
                playSceneMetaDataSource,
                localDevelopmentMetaDataSource,
                appArgs,
                staticContainer.RealmData);

            IGateKeeperSceneRoom gateKeeperSceneRoom = new GateKeeperSceneRoom(staticContainer.WebRequestsContainer.WebRequestController,
                    gateKeeperSceneRoomOptions).AsActivatable();

            var currentAdapterAddress = ICurrentAdapterAddress.NewDefault(staticContainer.RealmData);

            var archipelagoIslandRoom = IArchipelagoIslandRoom.NewDefault(
                identityCache,
                MultiPoolFactory(),
                new ArrayMemoryPool(),
                staticContainer.CharacterContainer.CharacterObject,
                currentAdapterAddress,
                staticContainer.WebRequestsContainer.WebRequestController,
                staticContainer.RealmData
            );

            var reloadSceneController = new ECSReloadScene(staticContainer.ScenesCache, globalWorld, playerEntity, localSceneDevelopment);

#if UNITY_WEBGL && UNITY_EDITOR
            // LiveKit Room uses WebGL native bridge (JSRef) which doesn't exist in Editor; use null room for chat.
            IActivatableConnectiveRoom chatRoom = new ActivatableConnectiveRoom(IConnectiveRoom.Null.INSTANCE);
#else
            var chatRoom = new ChatConnectiveRoom(staticContainer.WebRequestsContainer.WebRequestController, URLAddress.FromString(bootstrapContainer.DecentralandUrlsSource.Url(DecentralandUrl.ChatAdapter)));
#endif

            var voiceChatRoom = new VoiceChatActivatableConnectiveRoom();

#if !NO_LIVEKIT_MODE
            IRoomHub roomHub = new RoomHub(
                localSceneDevelopment ? IConnectiveRoom.Null.INSTANCE : archipelagoIslandRoom,
                gateKeeperSceneRoom,
                chatRoom,
                voiceChatRoom
            );
#endif

            var islandThroughputBunch = new ThroughputBufferBunch(new ThroughputBuffer(), new ThroughputBuffer());
            var sceneThroughputBunch = new ThroughputBufferBunch(new ThroughputBuffer(), new ThroughputBuffer());
            var chatThroughputBunch = new ThroughputBufferBunch(new ThroughputBuffer(), new ThroughputBuffer());

#if !NO_LIVEKIT_MODE
            var messagePipesHub = new MessagePipesHub(roomHub, MultiPoolFactory(), memoryPool, islandThroughputBunch, sceneThroughputBunch, chatThroughputBunch);
#endif

#if !NO_LIVEKIT_MODE
            var roomsStatus = new RoomsStatus(
                roomHub,

                //override allowed only in Editor
                Application.isEditor
                    ? new LinkedBox<(bool use, LKConnectionQuality quality)>(
                        () => (bootstrapContainer.DebugSettings.OverrideConnectionQuality, bootstrapContainer.DebugSettings.ConnectionQuality)
                    )
                    : new Box<(bool use, LKConnectionQuality quality)>((false, LKConnectionQuality.QualityExcellent))
            );
#endif

            var entityParticipantTable = new EntityParticipantTable();
            staticContainer.EntityParticipantTableProxy.SetObject(entityParticipantTable);

            var queuePoolFullMovementMessage = new ObjectPool<SimplePriorityQueue<NetworkMovementMessage>>(
                () => new SimplePriorityQueue<NetworkMovementMessage>(),
                actionOnRelease: queue => queue.Clear()
            );

#if !NO_LIVEKIT_MODE
            var remoteEntities = new RemoteEntities(
                entityParticipantTable,
                staticContainer.ComponentsContainer.ComponentPoolsRegistry,
                queuePoolFullMovementMessage,
                staticContainer.EntityCollidersGlobalCache
            );
#endif

            var worldAccessGate = new PrivateWorldAccessHandler(worldPermissionsService, mvcManager, staticContainer.RealmData);
            var realmNavigatorContainer = RealmNavigationContainer.Create(
                    staticContainer,
                    bootstrapContainer,
                    roadAssetsPool,
                    realmContainer,
#if !NO_LIVEKIT_MODE
                    remoteEntities,
#endif
                    globalWorld,
                    roomHub,
                    terrainContainer.Landscape,
                    exposedGlobalDataContainer,
                    loadingScreen,
                    placesAPIService,
                    worldAccessGate);


#if !NO_LIVEKIT_MODE
            IHealthCheck livekitHealthCheck = bootstrapContainer.DebugSettings.EnableEmulateNoLivekitConnection
                ? new IHealthCheck.AlwaysFails()
                : new StartLiveKitRooms(roomHub);

            livekitHealthCheck = dynamicWorldParams.EnableAnalytics
                ? livekitHealthCheck.WithFailAnalytics(bootstrapContainer.Analytics.Controller)
                : livekitHealthCheck;
#endif

            FeatureFlagsConfiguration featureFlags = FeatureFlagsConfiguration.Instance;

            bool includeCameraReel = appArgs.ResolveFeatureFlagArg(AppArgsFlags.CAMERA_REEL, featureFlags.IsEnabled(FeatureFlagsStrings.CAMERA_REEL) || Application.isEditor);
            bool includeFriends = appArgs.ResolveFeatureFlagArg(AppArgsFlags.FRIENDS, featureFlags.IsEnabled(FeatureFlagsStrings.FRIENDS) || Application.isEditor) && !localSceneDevelopment;
            bool includeUserBlocking = appArgs.ResolveFeatureFlagArg(AppArgsFlags.FRIENDS_USER_BLOCKING, featureFlags.IsEnabled(FeatureFlagsStrings.FRIENDS_USER_BLOCKING));
            bool includeTranslationChat = featureFlags.IsEnabled(FeatureFlagsStrings.CHAT_TRANSLATION_ENABLED);
            bool isNameEditorEnabled = appArgs.ResolveFeatureFlagArg(AppArgsFlags.PROFILE_NAME_EDITOR, featureFlags.IsEnabled(FeatureFlagsStrings.PROFILE_NAME_EDITOR) || Application.isEditor);
            bool includeMarketplaceCredits = featureFlags.IsEnabled(FeatureFlagsStrings.MARKETPLACE_CREDITS);
            bool includeBannedUsersFromScene = appArgs.ResolveFeatureFlagArg(AppArgsFlags.BANNED_USERS_FROM_SCENE, featureFlags.IsEnabled(FeatureFlagsStrings.BANNED_USERS_FROM_SCENE) || Application.isEditor);

            CommunitiesFeatureAccess.Initialize(new CommunitiesFeatureAccess(identityCache, appArgs));
            bool includeCommunities = await CommunitiesFeatureAccess.Instance.IsUserAllowedToUseTheFeatureAsync(ct, ignoreAllowedList: true, cacheResult: false);
            IRealmNavigator realmNavigator = realmNavigatorContainer.RealmNavigator;
            var chatHistory = new ChatHistory();
            IEventBus emotesEventBus = new EventBus(true);
            ISharedSpaceManager sharedSpaceManager = new SharedSpaceManager(mvcManager, globalWorld, includeFriends, includeCameraReel, emotesEventBus);
            var emoteWheelShortcutHandler = new EmoteWheelShortcutHandler(emotesEventBus);
            var initializationFlowContainer = InitializationFlowContainer.Create(
                staticContainer,
                bootstrapContainer,
                realmContainer,
                realmNavigator,
                realmNavigatorContainer,
                terrainContainer,
                loadingScreen,
#if !NO_LIVEKIT_MODE
                livekitHealthCheck,
#endif
                mvcManager,
                selfProfile,
                dynamicWorldParams,
                appArgs,
                backgroundMusic,
                roomHub,

#if !UNITY_WEBGL
                localSceneDevelopment,
#endif

                staticContainer.CharacterContainer);

            HomePlaceEventBus homePlaceEventBus = new HomePlaceEventBus();
            IEventBus eventBus = new EventBus(true);

            MapRendererContainer? mapRendererContainer =
                await MapRendererContainer
                   .CreateAsync(
                        dynamicWorldDependencies.SettingsContainer,
                        staticContainer,
                        bootstrapContainer.DecentralandUrlsSource,
                        assetsProvisioner,
                        placesAPIService,
                        eventsApiService,
                        mapPathEventBus,
                        staticContainer.MapPinsEventBus,
                        realmNavigator,
                        staticContainer.RealmData,
                        sharedNavmapCommandBus,
                        onlineUsersProvider,
                        identityCache,
                        homePlaceEventBus,
                        eventBus,
                        ct
                    );

            var worldInfoHub = new LocationBasedWorldInfoHub(
                new WorldInfoHub(staticContainer.SingletonSharedDependencies.SceneMapping),
                staticContainer.CharacterContainer.CharacterObject
            );

            dynamicWorldDependencies.WorldInfoTool.Initialize(worldInfoHub);

            var characterDataPropagationUtility = new CharacterDataPropagationUtility(staticContainer.ComponentsContainer.ComponentPoolsRegistry.AddComponentPool<SDKProfile>());

            var currentSceneInfo = new CurrentSceneInfo();

            var chatTeleporter = new ChatTeleporter(realmNavigator, new ChatEnvironmentValidator(bootstrapContainer.Environment), bootstrapContainer.DecentralandUrlsSource);

            var reloadSceneChatCommand = new ReloadSceneChatCommand(reloadSceneController, globalWorld, playerEntity, staticContainer.ScenesCache, teleportController, localSceneDevelopment);

            var chatMessageFactory = new ChatMessageFactory(profileCache, identityCache);
            var userBlockingCacheProxy = new ObjectProxy<IUserBlockingCache>();
            var currentChannelService = new CurrentChannelService();

            var chatCommands = new List<IChatCommand>
            {
                new GoToChatCommand(chatTeleporter, staticContainer.WebRequestsContainer.WebRequestController, bootstrapContainer.DecentralandUrlsSource),
                new GoToLocalChatCommand(chatTeleporter),
                new DebugPanelChatCommand(debugBuilder),
                new ShowEntityChatCommand(worldInfoHub),
                reloadSceneChatCommand,
                new LoadPortableExperienceChatCommand(staticContainer.PortableExperiencesController),
                new KillPortableExperienceChatCommand(staticContainer.PortableExperiencesController, staticContainer.SmartWearableCache),
                new VersionChatCommand(dclVersion),
#if !NO_LIVEKIT_MODE
                new RoomsChatCommand(roomHub),
#endif
                new LogsChatCommand(),
                new AppArgsCommand(appArgs),
                new LogMatrixChatCommand((RuntimeReportsHandlingSettings)bootstrapContainer.DiagnosticsContainer.Settings),
            };

            chatCommands.Add(new HelpChatCommand(chatCommands, appArgs));

#if !NO_LIVEKIT_MODE
            IChatMessagesBus coreChatMessageBus = new MultiplayerChatMessagesBus(messagePipesHub, chatMessageFactory, userBlockingCacheProxy, bootstrapContainer.Environment, identityCache, roomHub)
                                                 .WithSelfResend(identityCache, chatMessageFactory)
                                                 .WithIgnoreSymbols()
                                                 .WithCommands(chatCommands, staticContainer.LoadingStatus)
                                                 .WithDebugPanel(debugBuilder);

            IChatMessagesBus chatMessagesBus = dynamicWorldParams.EnableAnalytics
                ? new ChatMessagesBusAnalyticsDecorator(coreChatMessageBus, bootstrapContainer.Analytics.Controller, profileCache, selfProfile)
                : coreChatMessageBus;
#endif


            IDonationsService donationsService;
            if (FeaturesRegistry.Instance.IsEnabled(FeatureId.DONATIONS))
            {
                IDonationsService coreDonationsService = new DonationsService(staticContainer.ScenesCache, staticContainer.EthereumApi,
                    staticContainer.WebRequestsContainer.WebRequestController, staticContainer.RealmData,
                    placesAPIService, bootstrapContainer.Environment,
                    bootstrapContainer.DecentralandUrlsSource);

                donationsService = dynamicWorldParams.EnableAnalytics ? new DonationsServiceAnalyticsDecorator(coreDonationsService, bootstrapContainer.Analytics.Controller) : coreDonationsService;
            }
            else
                donationsService = new DonationsServiceDisabled();

            var coreBackpackEventBus = new BackpackEventBus();

            IChatEventBus chatEventBus = new ChatEventBus();
            ISocialServiceEventBus socialServiceEventBus = new SocialServiceEventBus();
            var socialServiceContainer = new SocialServicesContainer(bootstrapContainer.DecentralandUrlsSource, identityCache, socialServiceEventBus, appArgs);

#if !NO_LIVEKIT_MODE
            var voiceChatContainer = new VoiceChatContainer(
                socialServiceContainer.socialServicesRPC,
                socialServiceEventBus,
                roomHub,
                identityCache,
                staticContainer.WebRequestsContainer.WebRequestController,
                staticContainer.ScenesCache,
                realmNavigator,
                staticContainer.RealmData,
                bootstrapContainer.DecentralandUrlsSource,
                sharedSpaceManager,
                chatEventBus,
                currentChannelService
            );
#endif

            IBackpackEventBus backpackEventBus = dynamicWorldParams.EnableAnalytics
                ? new BackpackEventBusAnalyticsDecorator(coreBackpackEventBus, bootstrapContainer.Analytics.Controller)
                : coreBackpackEventBus;

            var profileBroadcast = new DebounceProfileBroadcast(
                new ProfileBroadcast(messagePipesHub, selfProfile)
            );

            var multiplayerEmotesMessageBus = new MultiplayerEmotesMessageBus(messagePipesHub, dynamicSettings.MultiplayerDebugSettings, userBlockingCacheProxy);

#if !NO_LIVEKIT_MODE
            var remoteMetadata = new DebounceRemoteMetadata(new RemoteMetadata(roomHub, staticContainer.RealmData, bootstrapContainer.DecentralandUrlsSource));
#endif

            var characterPreviewEventBus = new CharacterPreviewEventBus();
            var upscaleController = new UpscalingController(mvcManager);

            AudioMixer generalAudioMixer = (await assetsProvisioner.ProvideMainAssetAsync(dynamicSettings.GeneralAudioMixer, ct)).Value;
            var audioMixerVolumesController = new AudioMixerVolumesController(generalAudioMixer);

            var multiplayerMovementMessageBus = new MultiplayerMovementMessageBus(messagePipesHub, entityParticipantTable, globalWorld);

            var badgesAPIClient = new BadgesAPIClient(staticContainer.WebRequestsContainer.WebRequestController, bootstrapContainer.DecentralandUrlsSource);

            ICameraReelImagesMetadataDatabase cameraReelImagesMetadataDatabase = new CameraReelImagesMetadataRemoteDatabase(staticContainer.WebRequestsContainer.WebRequestController, bootstrapContainer.DecentralandUrlsSource);
            ICameraReelScreenshotsStorage cameraReelScreenshotsStorage = new CameraReelS3BucketScreenshotsStorage(staticContainer.WebRequestsContainer.WebRequestController);

            var cameraReelStorageService = new CameraReelRemoteStorageService(cameraReelImagesMetadataDatabase, cameraReelScreenshotsStorage, identityCache.Identity?.Address);

            GoogleUserCalendar userCalendar = new GoogleUserCalendar(webBrowser);
            var clipboardManager = new ClipboardManager(clipboard);
            ITextFormatter hyperlinkTextFormatter = new HyperlinkTextFormatter(profileCache, selfProfile);

            NotificationsRequestController notificationsRequestController = new (staticContainer.WebRequestsContainer.WebRequestController, bootstrapContainer.DecentralandUrlsSource, identityCache, includeFriends);

            var friendServiceProxy = new ObjectProxy<IFriendsService>();
            var friendOnlineStatusCacheProxy = new ObjectProxy<FriendsConnectivityStatusTracker>();
            var friendsCacheProxy = new ObjectProxy<FriendsCache>();

            ISpriteCache thumbnailCache = new SpriteCache(staticContainer.WebRequestsContainer.WebRequestController);

            var profileRepositoryWrapper = new ProfileRepositoryWrapper(
                    profilesRepository,
                    thumbnailCache

#if !NO_LIVEKIT_MODE
                    ,
                    remoteMetadata
#endif

                    );


            GetProfileThumbnailCommand.Initialize(new GetProfileThumbnailCommand(profileRepositoryWrapper));

            IFriendsEventBus friendsEventBus = new DefaultFriendsEventBus();
            var communitiesEventBus = new CommunitiesEventBus();

            var profileChangesBus = new ProfileChangesBus();

            var translationSettings = new PlayerPrefsTranslationSettings();

            GenericUserProfileContextMenuSettings genericUserProfileContextMenuSettingsSo = (await assetsProvisioner.ProvideMainAssetAsync(dynamicSettings.GenericUserProfileContextMenuSettings, ct)).Value;
            CommunityVoiceChatContextMenuConfiguration communityVoiceChatContextMenuSettingsSo = (await assetsProvisioner.ProvideMainAssetAsync(dynamicSettings.CommunityVoiceChatContextMenuSettings, ct)).Value;

#if !NO_LIVEKIT_MODE
            var communitiesDataService = new CommunityDataService(chatHistory,
                mvcManager,
                communitiesEventBus,
                communitiesDataProvider,
                identityCache);
#endif

            // Local scene development scenes are excluded from deeplink runtime handling logic
            if (appArgs.HasFlag(AppArgsFlags.LOCAL_SCENE) == false)
            {
#if !UNITY_WEBGL
                DeepLinkHandle deepLinkHandleImplementation = new DeepLinkHandle(dynamicWorldParams.StartParcel, chatTeleporter, ct, communitiesDataService);
                deepLinkHandleImplementation.StartListenForDeepLinksAsync(ct).Forget();
#endif
            }

            var passportBridge = new MVCPassportBridge(mvcManager);

            IMVCManagerMenusAccessFacade menusAccessFacade = new MVCManagerMenusAccessFacade(
                mvcManager,
                profileCache,
                friendServiceProxy,
                chatEventBus,
                genericUserProfileContextMenuSettingsSo,
                includeUserBlocking,
                bootstrapContainer.Analytics.Controller,
                onlineUsersProvider,
                realmNavigator,
                friendOnlineStatusCacheProxy,
                profilesRepository,
                sharedSpaceManager,
                communityVoiceChatContextMenuSettingsSo,

#if !NO_LIVEKIT_MODE
                voiceChatContainer.VoiceChatOrchestrator,
#endif

                includeCommunities,
                communitiesDataProvider,
                bootstrapContainer.DecentralandUrlsSource);

            ViewDependencies.Initialize(new ViewDependencies(
                unityEventSystem,
                menusAccessFacade,
                clipboardManager,
                dclCursor,
                new ContextMenuOpener(mvcManager),
                identityCache,
                new ConfirmationDialogOpener(mvcManager)));

            var realmNftNamesProvider = new RealmNftNamesProvider(staticContainer.WebRequestsContainer.WebRequestController,
                bootstrapContainer.DecentralandUrlsSource);

            var thumbnailProvider = new ECSThumbnailProvider(bootstrapContainer.DecentralandUrlsSource, globalWorld);

            var bannedSceneController = new ECSBannedScene(staticContainer.ScenesCache, globalWorld, playerEntity);

            var globalPlugins = new List<IDCLGlobalPlugin>
            {
#if !UNITY_WEBGL
                new ResourceUnloadingPlugin(staticContainer.SingletonSharedDependencies.MemoryBudget, staticContainer.CacheCleaner, staticContainer.SceneLoadingLimit),
                new LightSourceDebugPlugin(staticContainer.DebugContainerBuilder, globalWorld),
#endif
                new AdaptivePerformancePlugin(staticContainer.Profiler, staticContainer.LoadingStatus),

#if !NO_LIVEKIT_MODE
                new MultiplayerPlugin(
                    assetsProvisioner,
                    archipelagoIslandRoom,
                    gateKeeperSceneRoom,
                    chatRoom,
                    roomHub,
                    roomsStatus,
                    profilesRepository,
                    profileBroadcast,
                    debugBuilder,
                    staticContainer.LoadingStatus,
                    entityParticipantTable,
                    messagePipesHub,
                    remoteMetadata,
                    staticContainer.CharacterContainer.CharacterObject,
                    staticContainer.RealmData,
                    remoteEntities,
                    staticContainer.ScenesCache,
                    emotesCache,
                    characterDataPropagationUtility,
                    staticContainer.ComponentsContainer.ComponentPoolsRegistry,
                    islandThroughputBunch,
                    sceneThroughputBunch,
                    voiceChatRoom
                ),
#endif

                staticContainer.ProfilesContainer.CreatePlugin(),
                new WorldInfoPlugin(worldInfoHub, debugBuilder, chatHistory),
                new CharacterMotionPlugin(staticContainer.CharacterContainer.CharacterObject, debugBuilder, staticContainer.ComponentsContainer.ComponentPoolsRegistry, staticContainer.SceneReadinessReportQueue, terrainContainer.Landscape, staticContainer.ScenesCache, assetsProvisioner),
                new InputPlugin(dclCursor, unityEventSystem, assetsProvisioner, multiplayerEmotesMessageBus, emoteWheelShortcutHandler, mvcManager),
                new GlobalInteractionPlugin(assetsProvisioner, staticContainer.EntityCollidersGlobalCache, exposedGlobalDataContainer.GlobalInputEvents, unityEventSystem, staticContainer.ScenesCache, mvcManager, menusAccessFacade),
                new CharacterCameraPlugin(assetsProvisioner, realmSamplingData, exposedGlobalDataContainer.ExposedCameraData, debugBuilder, dynamicWorldDependencies.CommandLineArgs),
                new WearablePlugin(staticContainer.WebRequestsContainer.WebRequestController, staticContainer.RealmData, bootstrapContainer.DecentralandUrlsSource, staticContainer.CacheCleaner, wearableCatalog, trimmedWearableCatalog, bootstrapContainer.Analytics.EntitiesAnalytics, builderContentURL.Value, builderCollectionsPreview),
                new EmotePlugin(staticContainer.WebRequestsContainer.WebRequestController, emotesCache, staticContainer.RealmData, multiplayerEmotesMessageBus, debugBuilder,
                    assetsProvisioner, selfProfile, mvcManager, staticContainer.CacheCleaner, entityParticipantTable, dclCursor, staticContainer.InputBlock, globalWorld, playerEntity,
                    builderContentURL.Value, localSceneDevelopment, sharedSpaceManager, builderCollectionsPreview, appArgs, thumbnailProvider, staticContainer.ScenesCache,
                    bootstrapContainer.DecentralandUrlsSource, bootstrapContainer.Analytics.EntitiesAnalytics, trimmedEmoteCatalog),
                new ProfilingPlugin(staticContainer.Profiler, staticContainer.RealmData,
                    staticContainer.SingletonSharedDependencies.MemoryBudget, debugBuilder,
                    staticContainer.ScenesCache, dclVersion, dynamicSettings.AdaptivePhysicsSettings,
                    staticContainer.SceneLoadingLimit, appArgs, staticContainer.LoadingStatus),
                #if UNITY_EDITOR
                    new RenderingSystemPlugin(debugBuilder),
                #endif
                new AvatarPlugin(
                    staticContainer.ComponentsContainer.ComponentPoolsRegistry,
                    assetsProvisioner,
                    staticContainer.SingletonSharedDependencies.FrameTimeBudget,
                    staticContainer.SingletonSharedDependencies.MemoryBudget,
                    staticContainer.QualityContainer.RendererFeaturesCache,
                    staticContainer.RealmData,
                    staticContainer.MainPlayerAvatarBaseProxy,
                    debugBuilder,
                    staticContainer.CacheCleaner,
                    dynamicSettings.NametagsData,
                    defaultTexturesContainer.TextureArrayContainerFactory,
                    wearableCatalog,
                    userBlockingCacheProxy,
                    includeBannedUsersFromScene),
                new MainUIPlugin(mvcManager, mainUIView, includeFriends),
                new ProfilePlugin(profilesRepository, profileCache, staticContainer.CacheCleaner),
                new SidebarPlugin(
                    assetsProvisioner, mvcManager, mainUIView,
                    notificationsRequestController, identityCache, profilesRepository,
                    staticContainer.WebRequestsContainer.WebRequestController,
                    webBrowser, dynamicWorldDependencies.CompositeWeb3Provider,
                    initializationFlowContainer.InitializationFlow,
                    profileCache,
                    globalWorld, playerEntity, includeCameraReel, includeFriends, includeMarketplaceCredits,
                    chatHistory, profileRepositoryWrapper, sharedSpaceManager, profileChangesBus,
                    selfProfile, staticContainer.RealmData, staticContainer.SceneRestrictionBusController,
                    bootstrapContainer.DecentralandUrlsSource, passportBridge, eventBus,
                    staticContainer.SmartWearableCache, eventsApiService),
                new ErrorPopupPlugin(mvcManager, assetsProvisioner),
                new PrivateWorldsPlugin(
                    mvcManager,
                    assetsProvisioner,
                    roomHub,
                    worldPermissionsService,
                    worldAccessGate,
                    staticContainer.InputBlock,
                    staticContainer.RealmData,
                    realmNavigator,
                    chatHistory),
                new MinimapPlugin(
                    mainUIView.MinimapView.EnsureNotNull(),
                    mapRendererContainer.MapRenderer,
                    mvcManager,
                    placesAPIService,
                    staticContainer.RealmData,
                    realmNavigator,
                    staticContainer.ScenesCache,
                    mapPathEventBus,
                    staticContainer.SceneRestrictionBusController,
                    dynamicWorldParams.StartParcel.Peek(),
                    sharedSpaceManager,
                    clipboard,
                    bootstrapContainer.DecentralandUrlsSource,
                    chatMessagesBus,
                    reloadSceneChatCommand,
                    roomHub,
                    staticContainer.LoadingStatus,
                    includeBannedUsersFromScene,
                    homePlaceEventBus,
                    donationsService),
#if !NO_LIVEKIT_MODE
                new ChatPlugin(
                    mvcManager,
                    menusAccessFacade,
                    chatMessagesBus,
                    eventBus,
                    chatHistory,
                    clipboardManager,
                    entityParticipantTable,
                    dynamicSettings.NametagsData,
                    mainUIView,
                    staticContainer.InputBlock,
                    globalWorld,
                    playerEntity,
                    roomHub,
                    assetsProvisioner,
                    hyperlinkTextFormatter,
                    profileCache,
                    chatEventBus,
                    identityCache,
                    staticContainer.LoadingStatus,
                    sharedSpaceManager,
                    userBlockingCacheProxy,
                    socialServiceContainer.socialServicesRPC,
                    friendsEventBus,
                    chatMessageFactory,
                    profileRepositoryWrapper,
                    friendServiceProxy,
                    communitiesDataProvider,
                    communitiesDataService,
                    thumbnailCache,
                    communitiesEventBus,
                    voiceChatContainer.VoiceChatOrchestrator,
                    mainUIView.SidebarView.unreadMessagesButton.transform,
                    translationSettings,
                    staticContainer.WebRequestsContainer.WebRequestController,
                    bootstrapContainer.DecentralandUrlsSource,
                    chatSharedAreaEventBus,
                    currentChannelService),
#endif

#if !NO_LIVEKIT_MODE
                new ExplorePanelPlugin(
                    eventBus,
                    featureFlags,
                    assetsProvisioner,
                    mvcManager,
                    mapRendererContainer,
                    placesAPIService,
                    staticContainer.WebRequestsContainer.WebRequestController,
                    identityCache,
                    cameraReelStorageService,
                    cameraReelStorageService,
                    clipboard,
                    bootstrapContainer.DecentralandUrlsSource,
                    wearableCatalog,
                    characterPreviewFactory,
                    profilesRepository,
                    dynamicWorldDependencies.CompositeWeb3Provider,
                    initializationFlowContainer.InitializationFlow,
                    selfProfile,
                    equippedWearables,
                    equippedEmotes,
                    webBrowser,
                    emotesCache,
                    staticContainer.RealmData,
                    profileCache,
                    characterPreviewEventBus,
                    mapPathEventBus,
                    backpackEventBus,
                    thirdPartyNftProviderSource,
                    wearablesProvider,
                    dclCursor,
                    staticContainer.InputBlock,
                    emoteProvider,
                    globalWorld,
                    playerEntity,
                    chatMessagesBus,
                    staticContainer.MemoryCap,
                    bootstrapContainer.VolumeBus,
                    eventsApiService,
                    userCalendar,
                    clipboard,
                    explorePanelNavmapBus,
                    includeCameraReel,
                    appArgs,
                    userBlockingCacheProxy,
                    sharedSpaceManager,
                    profileChangesBus,
                    staticContainer.SceneLoadingLimit,
                    mainUIView.WarningNotification,
                    profileRepositoryWrapper,
                    upscaleController,
                    communitiesDataProvider,
                    realmNftNamesProvider,
                    voiceChatContainer.VoiceChatOrchestrator,
                    includeTranslationChat,
                    galleryEventBus,
                    thumbnailProvider,
                    passportBridge,
                    chatEventBus,
                    homePlaceEventBus,
                    staticContainer.SmartWearableCache,
                    staticContainer.ImageControllerProvider,
                    bootstrapContainer.Analytics.Controller,
                    communitiesDataService,
                    staticContainer.LoadingStatus,
                    donationsService,
                    realmNavigator,
                    friendServiceProxy,
                    staticContainer.PublishIpfsEntityCommand,
                    worldPermissionsService,
                    staticContainer.QualityContainer.RendererFeaturesCache
                ),
#endif

                new GiftingPlugin(assetsProvisioner,
                    mvcManager,
                    pendingTransferService,
                    staticContainer.WebRequestsContainer.WebRequestController,
                    equippedStatusProvider,
                    profilesRepository,
                    staticContainer.InputBlock,
                    wearablesProvider,
                    wearableCatalog,
                    emotesCache,
                    emoteProvider,
                    identityCache,
                    thumbnailProvider,
                    eventBus,
                    webBrowser,
                    bootstrapContainer.CompositeWeb3Provider,
                    bootstrapContainer.DecentralandUrlsSource,
                    sharedSpaceManager,
                    screenModeController,
                    staticContainer.ImageControllerProvider),
                new CharacterPreviewPlugin(staticContainer.ComponentsContainer.ComponentPoolsRegistry, assetsProvisioner, staticContainer.CacheCleaner),
                staticContainer.WebRequestsContainer.CreatePlugin(localSceneDevelopment),
                new Web3AuthenticationPlugin(assetsProvisioner, dynamicWorldDependencies.CompositeWeb3Provider, debugBuilder, mvcManager, selfProfile, webBrowser, staticContainer.RealmData, identityCache, characterPreviewFactory, dynamicWorldDependencies.SplashScreen, audioMixerVolumesController, staticContainer.InputBlock, characterPreviewEventBus, backgroundMusic, globalWorld, bootstrapContainer.AppArgs, wearablesProvider, staticContainer.WebRequestsContainer.WebRequestController, bootstrapContainer.DecentralandUrlsSource),
                new SkyboxPlugin(assetsProvisioner, dynamicSettings.DirectionalLight, staticContainer.ScenesCache, staticContainer.SceneRestrictionBusController, staticContainer.RealmData),
                new LoadingScreenPlugin(assetsProvisioner, mvcManager, audioMixerVolumesController,
                    staticContainer.InputBlock, debugBuilder, staticContainer.LoadingStatus, featureFlags),
                new ExternalUrlPromptPlugin(assetsProvisioner, webBrowser, mvcManager, dclCursor),
                new TeleportPromptPlugin(
                    assetsProvisioner,
                    mvcManager,
                    staticContainer.ImageControllerProvider,
                    placesAPIService,
                    dclCursor,
                    chatMessagesBus
                ),
                new ChangeRealmPromptPlugin(
                    assetsProvisioner,
                    mvcManager,
                    dclCursor,
                    realmUrl => chatMessagesBus.SendWithUtcNowTimestamp(ChatChannel.NEARBY_CHANNEL, $"/{ChatCommandsUtils.COMMAND_GOTO} {realmUrl}", ChatMessageOrigin.RESTRICTED_ACTION_API)),
                new NftPromptPlugin(assetsProvisioner, webBrowser, mvcManager, nftInfoAPIClient, staticContainer.ImageControllerProvider, dclCursor),
                staticContainer.CharacterContainer.CreateGlobalPlugin(),
                staticContainer.QualityContainer.CreatePlugin(),

#if !NO_LIVEKIT_MODE
                new MultiplayerMovementPlugin(
                    assetsProvisioner,
                    multiplayerMovementMessageBus,
                    debugBuilder,
                    remoteEntities,
                    staticContainer.CharacterContainer.Transform,
                    dynamicSettings.MultiplayerDebugSettings,
                    appArgs,
                    entityParticipantTable,
                    staticContainer.RealmData,
                    remoteMetadata),
#endif

                new AudioPlaybackPlugin(

// TODO it's for now to be without audio for Terrain
#if !UNITY_WEBGL
                        terrainContainer.GenesisTerrain,
                        terrainContainer.WorldsTerrain,
#endif


                        assetsProvisioner,
                        dynamicWorldParams.EnableLandscape,
                        audioMixerVolumesController,
                        staticContainer.RealmData
                        ),
                new RealmDataDirtyFlagPlugin(staticContainer.RealmData),
                new NotificationPlugin(
                    assetsProvisioner,
                    mvcManager,
                    staticContainer.ImageControllerProvider,
                    notificationsRequestController,
                    identityCache,
                    profilesRepository),
                new RewardPanelPlugin(mvcManager, assetsProvisioner, staticContainer.ImageControllerProvider),
                new PassportPlugin(
                    assetsProvisioner,
                    mvcManager,
                    dclCursor,
                    profilesRepository,
                    characterPreviewFactory,
                    characterPreviewEventBus,
                    selfProfile,
                    webBrowser,
                    bootstrapContainer.DecentralandUrlsSource,
                    badgesAPIClient,
                    staticContainer.InputBlock,

#if !NO_LIVEKIT_MODE
                    remoteMetadata,
#endif

                    cameraReelStorageService,
                    cameraReelStorageService,
                    globalWorld,
                    playerEntity,
                    includeCameraReel,
                    friendServiceProxy,
                    friendOnlineStatusCacheProxy,
                    onlineUsersProvider,
                    realmNavigator,
                    identityCache,
                    realmNftNamesProvider,
                    profileChangesBus,
                    includeFriends,
                    includeUserBlocking,
                    includeCommunities,
                    isNameEditorEnabled,

#if !NO_LIVEKIT_MODE
                    chatEventBus,
#endif

                    sharedSpaceManager,
                    profileRepositoryWrapper,

#if !NO_LIVEKIT_MODE
                    voiceChatContainer.VoiceChatOrchestrator,
#endif

                    galleryEventBus,
                    clipboard,
                    communitiesDataProvider,
                    thumbnailProvider,
                    staticContainer.ImageControllerProvider
                ),
                new GenericPopupsPlugin(assetsProvisioner, mvcManager, clipboardManager),
                new ColorPickerPlugin(assetsProvisioner, mvcManager),
                new GenericContextMenuPlugin(assetsProvisioner, mvcManager, profileRepositoryWrapper),
#if !UNITY_WEBGL
                new GPUInstancingPlugin(staticContainer.GPUInstancingService, assetsProvisioner, staticContainer.RealmData, staticContainer.LoadingStatus, exposedGlobalDataContainer.ExposedCameraData),
#endif
                new ConfirmationDialogPlugin(assetsProvisioner, mvcManager, profileRepositoryWrapper),

#if !NO_LIVEKIT_MODE
                new BannedUsersPlugin(roomHub, bannedSceneController, staticContainer.LoadingStatus, includeBannedUsersFromScene),
#endif

                new SmartWearablesGlobalPlugin(wearableCatalog,
                    backpackEventBus,
                    staticContainer.PortableExperiencesController,
                    staticContainer.ScenesCache,
                    staticContainer.SmartWearableCache,
                    assetsProvisioner,
                    staticContainer.LoadingStatus,
                    mvcManager,
                    thumbnailProvider,
                    identityCache),
                new AvatarLocomotionOverridesGlobalPlugin(),
                new JumpIndicatorPlugin(assetsProvisioner)
            };

            if (donationsService.DonationFeatureEnabled)
                globalPlugins.Add(new DonationsPlugin(
                    mvcManager,
                    assetsProvisioner,
                    donationsService,
                    staticContainer.ProfilesContainer.Repository,
                    playerEntity,
                    globalWorld,
                    webBrowser,
                    bootstrapContainer.DecentralandUrlsSource,
                    staticContainer.InputBlock,
                    dynamicWorldDependencies.CompositeWeb3Provider));

            // ReSharper disable once MethodHasAsyncOverloadWithCancellation
            if (FeaturesRegistry.Instance.IsEnabled(FeatureId.STOP_ON_DUPLICATE_IDENTITY))
                globalPlugins.Add(new DuplicateIdentityPlugin(roomHub, mvcManager, assetsProvisioner));

            globalPlugins.Add(new MapRendererPlugin(mapRendererContainer.MapRenderer));
            globalPlugins.Add(realmNavigatorContainer.CreatePlugin());

            // ReSharper disable once MethodHasAsyncOverloadWithCancellation
#if !NO_LIVEKIT_MODE
            if (FeaturesRegistry.Instance.IsEnabled(FeatureId.VOICE_CHAT))
                globalPlugins.Add(
                    new VoiceChatPlugin(
                        roomHub,
                        mainUIView.ChatMainView.VoiceChatPanelView,
                        voiceChatContainer,
                        profileRepositoryWrapper,
                        entityParticipantTable,
                        globalWorld,
                        playerEntity,
                        communitiesDataProvider,
                        staticContainer.ImageControllerProvider,
                        assetsProvisioner,
                        chatSharedAreaEventBus,
                        debugBuilder)
                );
#endif

            if (!appArgs.HasDebugFlag() || !appArgs.HasFlagWithValueFalse(AppArgsFlags.LANDSCAPE_TERRAIN_ENABLED))
                globalPlugins.Add(terrainContainer.CreatePlugin(staticContainer, bootstrapContainer, mapRendererContainer, debugBuilder));

            if (localSceneDevelopment)
                globalPlugins.Add(new LocalSceneDevelopmentPlugin(reloadSceneController, realmUrls));
            else
            {
#if !UNITY_WEBGL
                globalPlugins.Add(lodContainer!.LODPlugin);
                globalPlugins.Add(lodContainer!.RoadPlugin);
#endif
            }

#if !UNITY_WEBGL
            if (localSceneDevelopment || builderCollectionsPreview)
                globalPlugins.Add(new GlobalGLTFLoadingPlugin(staticContainer.WebRequestsContainer.WebRequestController, staticContainer.RealmData, builderContentURL.Value, localSceneDevelopment));
#endif

            globalPlugins.AddRange(staticContainer.SharedPlugins);

#if !NO_LIVEKIT_MODE
            if (includeFriends)
            {
                // TODO many circular dependencies - adjust the flow and get rid of ObjectProxy
                var friendsContainer = new FriendsContainer(
                    mainUIView,
                    mvcManager,
                    assetsProvisioner,
                    identityCache,
                    profilesRepository,
                    staticContainer.LoadingStatus,
                    staticContainer.InputBlock,
                    selfProfile,
                    passportBridge,
                    onlineUsersProvider,
                    realmNavigator,
                    includeUserBlocking,
                    appArgs,
                    dynamicWorldParams.EnableAnalytics,
                    bootstrapContainer.Analytics.Controller,
                    chatEventBus,
                    sharedSpaceManager,
                    socialServiceEventBus,
                    socialServiceContainer.socialServicesRPC,
                    friendsEventBus,
                    friendServiceProxy,
                    friendOnlineStatusCacheProxy,
                    friendsCacheProxy,
                    userBlockingCacheProxy,
                    profileRepositoryWrapper,
#if !NO_LIVEKIT_MODE
                    voiceChatContainer.VoiceChatOrchestrator,
#endif
                    bootstrapContainer.DecentralandUrlsSource
                );

                globalPlugins.Add(friendsContainer);
            }
#endif

#if !UNITY_WEBGL
            if (includeCameraReel)
                globalPlugins.Add(new InWorldCameraPlugin(
                    selfProfile,
                    staticContainer.RealmData,
                    playerEntity,
                    placesAPIService,
                    staticContainer.CharacterContainer.CharacterObject,
                    coroutineRunner,
                    cameraReelStorageService,
                    cameraReelStorageService,
                    mvcManager,
                    clipboard,
                    bootstrapContainer.DecentralandUrlsSource,
                    webBrowser,
                    profilesRepository,
                    realmNavigator,
                    assetsProvisioner,
                    wearableCatalog,
                    wearablesProvider,
                    dclCursor,
                    mainUIView.SidebarView.EnsureNotNull().InWorldCameraButton,
                    globalWorld,
                    debugBuilder,
                    dynamicSettings.NametagsData,
                    profileRepositoryWrapper,
                    sharedSpaceManager,
                    identityCache,
                    thumbnailProvider,
                    galleryEventBus));

            if (includeMarketplaceCredits)
            {
                globalPlugins.Add(new MarketplaceCreditsPlugin(
                    mainUIView,
                    assetsProvisioner,
                    webBrowser,
                    staticContainer.InputBlock,
                    selfProfile,
                    staticContainer.WebRequestsContainer.WebRequestController,
                    bootstrapContainer.DecentralandUrlsSource,
                    mvcManager,
                    staticContainer.RealmData,
                    sharedSpaceManager,
                    identityCache,
                    staticContainer.LoadingStatus,
                    hyperlinkTextFormatter,
                    staticContainer.ImageControllerProvider));
            }
#endif

#if !NO_LIVEKIT_MODE
            if (includeCommunities)
                globalPlugins.Add(new CommunitiesPlugin(
                    mvcManager,
                    assetsProvisioner,
                    staticContainer.InputBlock,
                    cameraReelStorageService,
                    cameraReelScreenshotsStorage,
                    profileRepositoryWrapper,
                    friendServiceProxy,
                    communitiesDataProvider,
                    staticContainer.WebRequestsContainer.WebRequestController,
                    placesAPIService,
                    selfProfile,
                    realmNavigator,
                    clipboard,
                    webBrowser,
                    eventsApiService,
                    sharedSpaceManager,
                    chatEventBus,
                    galleryEventBus,
                    communitiesEventBus,
                    socialServiceContainer.socialServicesRPC,
                    profilesRepository,
                    bootstrapContainer.DecentralandUrlsSource,
                    identityCache,
                    voiceChatContainer.VoiceChatOrchestrator,
                    bootstrapContainer.Analytics.Controller,
                    homePlaceEventBus,
                    socialServiceEventBus,
                    worldPermissionsService));
#endif


#if !UNITY_WEBGL
            if (dynamicWorldParams.EnableAnalytics)
                globalPlugins.Add(new AnalyticsPlugin(
                        bootstrapContainer.Analytics.Controller,
                        staticContainer.Profiler,
                        staticContainer.LoadingStatus,
                        staticContainer.RealmData,
                        staticContainer.MainPlayerAvatarBaseProxy,
                        identityCache,
                        debugBuilder,
                        cameraReelStorageService,
                        entityParticipantTable,
                        staticContainer.ScenesCache,
                        eventBus, translationSettings
                    ));

            if (localSceneDevelopment || appArgs.HasFlag(AppArgsFlags.SCENE_CONSOLE))
                globalPlugins.Add(new DebugMenuPlugin(
                    bootstrapContainer.DiagnosticsContainer,
                    staticContainer.InputBlock,
                    assetsProvisioner,
                    debugBuilder
                    ));
#if !NO_LIVEKIT_MODE
            if (!localSceneDevelopment)
                globalPlugins.Add(new ConnectionStatusPanelPlugin(roomsStatus, currentSceneInfo, assetsProvisioner, appArgs));
#endif
#endif

            var globalWorldFactory = new GlobalWorldFactory(
                in staticContainer,
                exposedGlobalDataContainer.CameraSamplingData,
                realmSamplingData,
                bootstrapContainer.DecentralandUrlsSource,
                staticContainer.RealmData,
                globalPlugins,
                debugBuilder,
                staticContainer.ScenesCache,
                dynamicWorldParams.HybridSceneParams,
                currentSceneInfo,
                lodCache,
                roadCoordinates,
                lodSettings,
                multiplayerEmotesMessageBus,
                globalWorld,
                staticContainer.SceneReadinessReportQueue,
                localSceneDevelopment,
                profilesRepository,
                bootstrapContainer.UseRemoteAssetBundles,
                roadAssetsPool,
                staticContainer.SceneLoadingLimit,
                dynamicWorldParams.StartParcel,
                builderCollectionsPreview,
                bootstrapContainer.Analytics.EntitiesAnalytics
            );

#if !NO_LIVEKIT_MODE
            staticContainer.RoomHubProxy.SetObject(roomHub);
#endif

            var container = new DynamicWorldContainer(
                mvcManager,
                realmContainer.RealmController,
                realmNavigator,
                globalWorldFactory,
                globalPlugins,
                profilesRepository,
                initializationFlowContainer.InitializationFlow,

#if !NO_LIVEKIT_MODE
                chatMessagesBus,
                chatHistory,
                messagePipesHub,
                remoteMetadata,
#endif

                profileBroadcast,

#if !NO_LIVEKIT_MODE
                roomHub,
#endif

                socialServiceContainer,
                selfProfile,
                clipboard
            );

            // Init itself
            await dynamicWorldDependencies.SettingsContainer.InitializePluginAsync(container, ct)!.ThrowOnFail();

            return (container, true);
        }

        private static void ParseDebugForcedEmotes(IReadOnlyCollection<string>? debugEmotes, ref List<URN> parsedEmotes)
        {
            if (debugEmotes?.Count > 0)
                parsedEmotes.AddRange(debugEmotes.Select(emote => new URN(emote)));
        }

        private static void ParseParamsForcedEmotes(IAppArgs appParams, ref List<URN> parsedEmotes)
        {
            if (appParams.TryGetValue(AppArgsFlags.FORCED_EMOTES, out string? csv) && !string.IsNullOrEmpty(csv!))
                parsedEmotes.AddRange(csv.Split(',', StringSplitOptions.RemoveEmptyEntries)?.Select(emote => new URN(emote)) ?? ArraySegment<URN>.Empty);
        }
    }
}
