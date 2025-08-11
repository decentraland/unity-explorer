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
using DCL.Browser.DecentralandUrls;
using DCL.CharacterPreview;
using DCL.Chat.Commands;
using DCL.Chat.EventBus;
using DCL.Chat.History;
using DCL.Chat.MessageBus;
using DCL.Clipboard;
using DCL.Communities;
using DCL.DebugUtilities;
using DCL.Diagnostics;
using DCL.EventsApi;
using DCL.FeatureFlags;
using DCL.Friends;
using DCL.Friends.Passport;
using DCL.Friends.UserBlocking;
using DCL.Input;
using DCL.InWorldCamera.CameraReelStorageService;
using DCL.LOD.Systems;
using DCL.MapRenderer;
using DCL.Minimap;
using DCL.Multiplayer.Connections.Archipelago.AdapterAddress.Current;
using DCL.Multiplayer.Connections.Archipelago.Rooms;
using DCL.Multiplayer.Connections.Archipelago.Rooms.Chat;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Multiplayer.Connections.GateKeeper.Meta;
using DCL.Multiplayer.Connections.GateKeeper.Rooms;
using DCL.Multiplayer.Connections.GateKeeper.Rooms.Options;
using DCL.Multiplayer.Connections.Messaging.Hubs;
using DCL.Multiplayer.Connections.Pools;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Multiplayer.Connections.Rooms.Connective;
using DCL.Multiplayer.Connections.Rooms.Status;
using DCL.Multiplayer.Connections.Systems.Throughput;
using DCL.Multiplayer.Connectivity;
using DCL.Multiplayer.Deduplication;
using DCL.Multiplayer.Emotes;
using DCL.Multiplayer.HealthChecks;
using DCL.Multiplayer.HealthChecks.Struct;
using DCL.Multiplayer.Movement;
using DCL.Multiplayer.Movement.Settings;
using DCL.Multiplayer.Movement.Systems;
using DCL.Multiplayer.Profiles.BroadcastProfiles;
using DCL.Multiplayer.Profiles.Entities;
using DCL.Multiplayer.Profiles.Poses;
using DCL.Multiplayer.Profiles.Tables;
using DCL.Multiplayer.SDK.Systems.GlobalWorld;
using DCL.Nametags;
using DCL.Navmap;
using DCL.NftInfoAPIService;
using DCL.Notifications;
using DCL.NotificationsBusController.NotificationsBus;
using DCL.Optimization.AdaptivePerformance.Systems;
using DCL.Optimization.Pools;
using DCL.PerformanceAndDiagnostics.Analytics;
using DCL.PlacesAPIService;
using DCL.PluginSystem;
using DCL.PluginSystem.Global;
using DCL.Profiles;
using DCL.Profiles.Helpers;
using DCL.Profiles.Self;
using DCL.RealmNavigation;
using DCL.Rendering.GPUInstancing.Systems;
using DCL.RuntimeDeepLink;
using DCL.SceneLoadingScreens.LoadingScreen;
using DCL.SkyBox;
using DCL.SocialService;
using DCL.UI;
using DCL.UI.ConfirmationDialog;
using DCL.UI.GenericContextMenu;
using DCL.UI.GenericContextMenu.Controllers;
using DCL.UI.InputFieldFormatting;
using DCL.UI.MainUI;
using DCL.UI.Profiles.Helpers;
using DCL.UI.SharedSpaceManager;
using DCL.UserInAppInitializationFlow;
using DCL.Utilities;
using DCL.Utilities.Extensions;
using DCL.VoiceChat;
using DCL.VoiceChat.Services;
using DCL.Web3.Identities;
using DCL.WebRequests.Analytics;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.CurrentScene;
using ECS.SceneLifeCycle.Realm;
using Global.AppArgs;
using Global.Dynamic.ChatCommands;
using Global.Dynamic.RealmUrl;
using Global.Dynamic.LaunchModes;
using Global.Versioning;
using LiveKit.Internal.FFIClients.Pools;
using LiveKit.Internal.FFIClients.Pools.Memory;
using LiveKit.Proto;
using MVC;
using MVC.PopupsController.PopupCloser;
using SceneRunner.Debugging.Hub;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using DCL.InWorldCamera;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.EventSystems;
using UnityEngine.Pool;
using Utility;
using Utility.Ownership;
using Utility.PriorityQueue;
using Object = UnityEngine.Object;

namespace Global.Dynamic
{
    public class DynamicWorldContainer : DCLWorldContainer<DynamicWorldSettings>
    {
        private readonly IChatMessagesBus chatMessagesBus;
        private readonly IProfileBroadcast profileBroadcast;
        private readonly SocialServicesContainer socialServicesContainer;
        private readonly ISelfProfile selfProfile;

        public IMVCManager MvcManager { get; }

        public IGlobalRealmController RealmController { get; }

        public GlobalWorldFactory GlobalWorldFactory { get; }

        public IReadOnlyList<IDCLGlobalPlugin> GlobalPlugins { get; }

        public IProfileRepository ProfileRepository { get; }

        public IUserInAppInitializationFlow UserInAppInAppInitializationFlow { get; }

        public IMessagePipesHub MessagePipesHub { get; }

        public IRemoteMetadata RemoteMetadata { get; }

        public IRoomHub RoomHub { get; }

        private DynamicWorldContainer(
            IMVCManager mvcManager,
            IGlobalRealmController realmController,
            GlobalWorldFactory globalWorldFactory,
            IReadOnlyList<IDCLGlobalPlugin> globalPlugins,
            IProfileRepository profileRepository,
            IUserInAppInitializationFlow userInAppInAppInitializationFlow,
            IChatMessagesBus chatMessagesBus,
            IMessagePipesHub messagePipesHub,
            IRemoteMetadata remoteMetadata,
            IProfileBroadcast profileBroadcast,
            IRoomHub roomHub,
            SocialServicesContainer socialServicesContainer,
            ISelfProfile selfProfile)
        {
            MvcManager = mvcManager;
            RealmController = realmController;
            GlobalWorldFactory = globalWorldFactory;
            GlobalPlugins = globalPlugins;
            ProfileRepository = profileRepository;
            UserInAppInAppInitializationFlow = userInAppInAppInitializationFlow;
            MessagePipesHub = messagePipesHub;
            RemoteMetadata = remoteMetadata;
            RoomHub = roomHub;
            this.chatMessagesBus = chatMessagesBus;
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
                staticContainer.RealmData);

            var placesAPIService = new PlacesAPIService(new PlacesAPIClient(staticContainer.WebRequestsContainer.WebRequestController, bootstrapContainer.DecentralandUrlsSource));

            IEventsApiService eventsApiService = new HttpEventsApiService(staticContainer.WebRequestsContainer.WebRequestController,
                URLDomain.FromString(bootstrapContainer.DecentralandUrlsSource.Url(DecentralandUrl.ApiEvents)));

            var mapPathEventBus = new MapPathEventBus();
            INotificationsBusController notificationsBusController = new NotificationsBusController();

            DefaultTexturesContainer defaultTexturesContainer = null!;
            LODContainer lodContainer = null!;

            IOnlineUsersProvider baseUserProvider = new ArchipelagoHttpOnlineUsersProvider(staticContainer.WebRequestsContainer.WebRequestController,
                URLAddress.FromString(bootstrapContainer.DecentralandUrlsSource.Url(DecentralandUrl.RemotePeers)));

            var onlineUsersProvider = new WorldInfoOnlineUsersProviderDecorator(
                baseUserProvider,
                staticContainer.WebRequestsContainer.WebRequestController,
                URLAddress.FromString(bootstrapContainer.DecentralandUrlsSource.Url(DecentralandUrl.RemotePeersWorld)));

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

                lodContainer =
                    await LODContainer
                         .CreateAsync(
                              assetsProvisioner,
                              bootstrapContainer.DecentralandUrlsSource,
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
            }

            try { await InitializeContainersAsync(dynamicWorldDependencies.SettingsContainer, ct); }
            catch (Exception) { return (null, false); }

            CursorSettings cursorSettings = (await assetsProvisioner.ProvideMainAssetAsync(dynamicSettings.CursorSettings, ct)).Value;
            ProvidedAsset<Texture2D> normalCursorAsset = await assetsProvisioner.ProvideMainAssetAsync(cursorSettings.NormalCursor, ct);
            ProvidedAsset<Texture2D> interactionCursorAsset = await assetsProvisioner.ProvideMainAssetAsync(cursorSettings.InteractionCursor, ct);
            ProvidedAsset<MultiplayerDebugSettings> multiplayerDebugSettings = await assetsProvisioner.ProvideMainAssetAsync(dynamicSettings.MultiplayerDebugSettings, ct);
            ProvidedAsset<AdaptivePhysicsSettings> physicsSettings = await assetsProvisioner.ProvideMainAssetAsync(dynamicSettings.AdaptivePhysicsSettings, ct);

            var unityEventSystem = new UnityEventSystem(EventSystem.current.EnsureNotNull());
            var dclCursor = new DCLCursor(normalCursorAsset.Value, interactionCursorAsset.Value, cursorSettings.NormalCursorHotspot, cursorSettings.InteractionCursorHotspot);

            staticContainer.QualityContainer.AddDebugViews(debugBuilder);

            var realmSamplingData = new RealmSamplingData();

            ExposedGlobalDataContainer exposedGlobalDataContainer = staticContainer.ExposedGlobalDataContainer;

            PopupCloserView popupCloserView = Object.Instantiate((await assetsProvisioner.ProvideMainAssetAsync(dynamicSettings.PopupCloserView, CancellationToken.None)).Value.GetComponent<PopupCloserView>()).EnsureNotNull();
            MainUIView mainUIView = Object.Instantiate((await assetsProvisioner.ProvideMainAssetAsync(dynamicSettings.MainUIView, CancellationToken.None)).Value.GetComponent<MainUIView>()).EnsureNotNull();

            var coreMvcManager = new MVCManager(new WindowStackManager(), new CancellationTokenSource(), popupCloserView);

            IMVCManager mvcManager = dynamicWorldParams.EnableAnalytics
                ? new MVCManagerAnalyticsDecorator(coreMvcManager, bootstrapContainer.Analytics!)
                : coreMvcManager;

            var loadingScreenTimeout = new LoadingScreenTimeout();
            ILoadingScreen loadingScreen = new LoadingScreen(mvcManager, loadingScreenTimeout);

            var nftInfoAPIClient = new OpenSeaAPIClient(staticContainer.WebRequestsContainer.WebRequestController, bootstrapContainer.DecentralandUrlsSource);
            var wearableCatalog = new WearableStorage();
            var characterPreviewFactory = new CharacterPreviewFactory(staticContainer.ComponentsContainer.ComponentPoolsRegistry, appArgs);
            IWebBrowser webBrowser = bootstrapContainer.WebBrowser;
            ISystemClipboard clipboard = new UnityClipboard();
            ProfileNameColorHelper.SetNameColors(dynamicSettings.UserNameColors);
            NametagsData nametagsData = (await assetsProvisioner.ProvideMainAssetAsync(dynamicSettings.NametagsData, ct)).Value;

            IProfileCache profileCache = new DefaultProfileCache();

            var profileRepository = new LogProfileRepository(
                new RealmProfileRepository(staticContainer.WebRequestsContainer.WebRequestController, staticContainer.RealmData, profileCache)
            );

            GalleryEventBus galleryEventBus = new GalleryEventBus();

            static IMultiPool MultiPoolFactory() =>
                new DCLMultiPool();

            var memoryPool = new ArrayMemoryPool(ArrayPool<byte>.Shared!);

            var assetBundlesURL = URLDomain.FromString(bootstrapContainer.DecentralandUrlsSource.Url(DecentralandUrl.AssetBundlesCDN));
            var builderDTOsURL = URLDomain.FromString(bootstrapContainer.DecentralandUrlsSource.Url(DecentralandUrl.BuilderApiDtos));
            var builderContentURL = URLDomain.FromString(bootstrapContainer.DecentralandUrlsSource.Url(DecentralandUrl.BuilderApiContent));

            var emotesCache = new MemoryEmotesStorage();
            staticContainer.CacheCleaner.Register(emotesCache);
            var equippedWearables = new EquippedWearables();
            var equippedEmotes = new EquippedEmotes();
            var forceRender = new List<string>();

            var selfEmotes = new List<URN>();
            ParseParamsForcedEmotes(bootstrapContainer.ApplicationParametersParser, ref selfEmotes);
            ParseDebugForcedEmotes(bootstrapContainer.DebugSettings.EmotesToAddToUserProfile, ref selfEmotes);

            var selfProfile = new SelfProfile(profileRepository, identityCache, equippedWearables, wearableCatalog,
                emotesCache, equippedEmotes, forceRender, selfEmotes, profileCache, globalWorld, playerEntity);

            IEmoteProvider emoteProvider = new ApplicationParamsEmoteProvider(appArgs,
                new EcsEmoteProvider(globalWorld, staticContainer.RealmData), builderDTOsURL.Value);

            var wearablesProvider = new ApplicationParametersWearablesProvider(appArgs,
                new ECSWearablesProvider(identityCache, globalWorld), builderDTOsURL.Value);

            //TODO should be unified with LaunchMode
            bool localSceneDevelopment = !string.IsNullOrEmpty(dynamicWorldParams.LocalSceneDevelopmentRealm);
            bool builderCollectionsPreview = appArgs.HasFlag(AppArgsFlags.SELF_PREVIEW_BUILDER_COLLECTIONS);

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
                new TeleportController(staticContainer.SceneReadinessReportQueue));

            var terrainContainer = TerrainContainer.Create(staticContainer, realmContainer, dynamicWorldParams.EnableLandscape, localSceneDevelopment);

            SceneRoomLogMetaDataSource playSceneMetaDataSource = new SceneRoomMetaDataSource(staticContainer.RealmData, staticContainer.CharacterContainer.Transform, globalWorld, dynamicWorldParams.IsolateScenesCommunication).WithLog();
            SceneRoomLogMetaDataSource localDevelopmentMetaDataSource = ConstSceneRoomMetaDataSource.FromMachineUUID().WithLog();

            var gateKeeperSceneRoomOptions = new GateKeeperSceneRoomOptions(staticContainer.LaunchMode, bootstrapContainer.DecentralandUrlsSource, playSceneMetaDataSource, localDevelopmentMetaDataSource);

            IGateKeeperSceneRoom gateKeeperSceneRoom = new GateKeeperSceneRoom(staticContainer.WebRequestsContainer.WebRequestController, staticContainer.ScenesCache, gateKeeperSceneRoomOptions)
               .AsActivatable();

            var currentAdapterAddress = ICurrentAdapterAddress.NewDefault(staticContainer.RealmData);

            var archipelagoIslandRoom = IArchipelagoIslandRoom.NewDefault(
                identityCache,
                MultiPoolFactory(),
                new ArrayMemoryPool(),
                staticContainer.CharacterContainer.CharacterObject,
                currentAdapterAddress,
                staticContainer.WebRequestsContainer.WebRequestController
            );

            var reloadSceneController = new ECSReloadScene(staticContainer.ScenesCache, globalWorld, playerEntity, localSceneDevelopment);

            var chatRoom = new ChatConnectiveRoom(staticContainer.WebRequestsContainer.WebRequestController, URLAddress.FromString(bootstrapContainer.DecentralandUrlsSource.Url(DecentralandUrl.ChatAdapter)));

            var voiceChatRoom = new VoiceChatActivatableConnectiveRoom();

            IRoomHub roomHub = new RoomHub(
                localSceneDevelopment ? IConnectiveRoom.Null.INSTANCE : archipelagoIslandRoom,
                gateKeeperSceneRoom,
                chatRoom,
                voiceChatRoom
            );

            var islandThroughputBunch = new ThroughputBufferBunch(new ThroughputBuffer(), new ThroughputBuffer());
            var sceneThroughputBunch = new ThroughputBufferBunch(new ThroughputBuffer(), new ThroughputBuffer());
            var chatThroughputBunch = new ThroughputBufferBunch(new ThroughputBuffer(), new ThroughputBuffer());

            var messagePipesHub = new MessagePipesHub(roomHub, MultiPoolFactory(), MultiPoolFactory(), memoryPool, islandThroughputBunch, sceneThroughputBunch, chatThroughputBunch);

            var roomsStatus = new RoomsStatus(
                roomHub,

                //override allowed only in Editor
                Application.isEditor
                    ? new LinkedBox<(bool use, ConnectionQuality quality)>(
                        () => (bootstrapContainer.DebugSettings.OverrideConnectionQuality, bootstrapContainer.DebugSettings.ConnectionQuality)
                    )
                    : new Box<(bool use, ConnectionQuality quality)>((false, ConnectionQuality.QualityExcellent))
            );

            var entityParticipantTable = new EntityParticipantTable();
            staticContainer.EntityParticipantTableProxy.SetObject(entityParticipantTable);

            var queuePoolFullMovementMessage = new ObjectPool<SimplePriorityQueue<NetworkMovementMessage>>(
                () => new SimplePriorityQueue<NetworkMovementMessage>(),
                actionOnRelease: queue => queue.Clear()
            );

            var remoteEntities = new RemoteEntities(
                entityParticipantTable,
                staticContainer.ComponentsContainer.ComponentPoolsRegistry,
                queuePoolFullMovementMessage,
                staticContainer.EntityCollidersGlobalCache
            );

            var realmNavigatorContainer = RealmNavigationContainer.Create
                (staticContainer, bootstrapContainer, lodContainer, realmContainer, remoteEntities, globalWorld, roomHub, terrainContainer.Landscape, exposedGlobalDataContainer, loadingScreen);

            IHealthCheck livekitHealthCheck = bootstrapContainer.DebugSettings.EnableEmulateNoLivekitConnection
                ? new IHealthCheck.AlwaysFails() :
                  new StartLiveKitRooms(roomHub);

            livekitHealthCheck = dynamicWorldParams.EnableAnalytics
                ? livekitHealthCheck.WithFailAnalytics(bootstrapContainer.Analytics!)
                : livekitHealthCheck;

            FeatureFlagsConfiguration featureFlags = FeatureFlagsConfiguration.Instance;

            bool includeCameraReel = featureFlags.IsEnabled(FeatureFlagsStrings.CAMERA_REEL) || (appArgs.HasDebugFlag() && appArgs.HasFlag(AppArgsFlags.CAMERA_REEL)) || Application.isEditor;
            bool includeFriends = (featureFlags.IsEnabled(FeatureFlagsStrings.FRIENDS) || (appArgs.HasDebugFlag() && appArgs.HasFlag(AppArgsFlags.FRIENDS)) || Application.isEditor) && !localSceneDevelopment;
            bool includeUserBlocking = featureFlags.IsEnabled(FeatureFlagsStrings.FRIENDS_USER_BLOCKING) || (appArgs.HasDebugFlag() && appArgs.HasFlag(AppArgsFlags.FRIENDS_USER_BLOCKING));
            bool includeVoiceChat = includeFriends && includeUserBlocking && (Application.isEditor || featureFlags.IsEnabled(FeatureFlagsStrings.VOICE_CHAT) || (appArgs.HasDebugFlag() && appArgs.HasFlag(AppArgsFlags.VOICE_CHAT)));
            bool isNameEditorEnabled = featureFlags.IsEnabled(FeatureFlagsStrings.PROFILE_NAME_EDITOR) || (appArgs.HasDebugFlag() && appArgs.HasFlag(AppArgsFlags.PROFILE_NAME_EDITOR)) || Application.isEditor;
            bool includeMarketplaceCredits = featureFlags.IsEnabled(FeatureFlagsStrings.MARKETPLACE_CREDITS);

            CommunitiesFeatureAccess.Initialize(new CommunitiesFeatureAccess(identityCache));
            bool includeCommunities = await CommunitiesFeatureAccess.Instance.IsUserAllowedToUseTheFeatureAsync(ct, ignoreAllowedList: true, cacheResult: false);

            var chatHistory = new ChatHistory();
            ISharedSpaceManager sharedSpaceManager = new SharedSpaceManager(mvcManager, globalWorld, includeFriends, includeCameraReel);

            var initializationFlowContainer = InitializationFlowContainer.Create(staticContainer,
                bootstrapContainer,
                realmContainer,
                realmNavigatorContainer,
                terrainContainer,
                loadingScreen,
                livekitHealthCheck,
                bootstrapContainer.DecentralandUrlsSource,
                mvcManager,
                selfProfile,
                dynamicWorldParams,
                appArgs,
                backgroundMusic,
                roomHub,
                localSceneDevelopment,
                staticContainer.CharacterContainer);

            IRealmNavigator realmNavigator = realmNavigatorContainer.WithMainScreenFallback(initializationFlowContainer.InitializationFlow, playerEntity, globalWorld);

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
                        notificationsBusController,
                        realmNavigator,
                        staticContainer.RealmData,
                        sharedNavmapCommandBus,
                        onlineUsersProvider,
                        ct
                    );

            var minimap = new MinimapController(
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
                bootstrapContainer.DecentralandUrlsSource
            );

            var worldInfoHub = new LocationBasedWorldInfoHub(
                new WorldInfoHub(staticContainer.SingletonSharedDependencies.SceneMapping),
                staticContainer.CharacterContainer.CharacterObject
            );

            dynamicWorldDependencies.WorldInfoTool.Initialize(worldInfoHub);

            var characterDataPropagationUtility = new CharacterDataPropagationUtility(staticContainer.ComponentsContainer.ComponentPoolsRegistry.AddComponentPool<SDKProfile>());

            var currentSceneInfo = new CurrentSceneInfo();

            var chatTeleporter = new ChatTeleporter(realmNavigator, new ChatEnvironmentValidator(bootstrapContainer.Environment), bootstrapContainer.DecentralandUrlsSource);

            var chatCommands = new List<IChatCommand>
            {
                new GoToChatCommand(chatTeleporter, staticContainer.WebRequestsContainer.WebRequestController, bootstrapContainer.DecentralandUrlsSource),
                new GoToLocalChatCommand(chatTeleporter),
                new WorldChatCommand(chatTeleporter),
                new DebugPanelChatCommand(debugBuilder),
                new ShowEntityChatCommand(worldInfoHub),
                new ReloadSceneChatCommand(reloadSceneController, globalWorld, playerEntity, staticContainer.ScenesCache),
                new LoadPortableExperienceChatCommand(staticContainer.PortableExperiencesController),
                new KillPortableExperienceChatCommand(staticContainer.PortableExperiencesController),
                new VersionChatCommand(dclVersion),
                new RoomsChatCommand(roomHub),
                new LogsChatCommand(),
                new AppArgsCommand(appArgs)
            };

            chatCommands.Add(new HelpChatCommand(chatCommands, appArgs));

            var chatMessageFactory = new ChatMessageFactory(profileCache, identityCache);
            var userBlockingCacheProxy = new ObjectProxy<IUserBlockingCache>();

            IChatMessagesBus coreChatMessageBus = new MultiplayerChatMessagesBus(messagePipesHub, chatMessageFactory, new MessageDeduplication<double>(), userBlockingCacheProxy, new DecentralandUrlsSource(bootstrapContainer.Environment, ILaunchMode.PLAY))
                                                 .WithSelfResend(identityCache, chatMessageFactory)
                                                 .WithIgnoreSymbols()
                                                 .WithCommands(chatCommands, staticContainer.LoadingStatus)
                                                 .WithDebugPanel(debugBuilder);

            IChatMessagesBus chatMessagesBus = dynamicWorldParams.EnableAnalytics
                ? new ChatMessagesBusAnalyticsDecorator(coreChatMessageBus, bootstrapContainer.Analytics!, profileCache, selfProfile)
                : coreChatMessageBus;

            var coreBackpackEventBus = new BackpackEventBus();

            ISocialServiceEventBus socialServiceEventBus = new SocialServiceEventBus();
            var socialServiceContainer = new SocialServicesContainer(bootstrapContainer.DecentralandUrlsSource, identityCache, socialServiceEventBus, appArgs);

            IVoiceService voiceService = new RPCVoiceChatService(socialServiceContainer.socialServicesRPC, socialServiceEventBus);
            IVoiceChatCallStatusService voiceChatCallStatusService = new VoiceChatCallStatusService(voiceService);

            IBackpackEventBus backpackEventBus = dynamicWorldParams.EnableAnalytics
                ? new BackpackEventBusAnalyticsDecorator(coreBackpackEventBus, bootstrapContainer.Analytics!)
                : coreBackpackEventBus;

            var profileBroadcast = new DebounceProfileBroadcast(
                new ProfileBroadcast(messagePipesHub, selfProfile)
            );

            var multiplayerEmotesMessageBus = new MultiplayerEmotesMessageBus(messagePipesHub, multiplayerDebugSettings, userBlockingCacheProxy);

            var remoteMetadata = new DebounceRemoteMetadata(new RemoteMetadata(roomHub, staticContainer.RealmData));

            var characterPreviewEventBus = new CharacterPreviewEventBus();
            var upscaleController = new UpscalingController(mvcManager);

            AudioMixer generalAudioMixer = (await assetsProvisioner.ProvideMainAssetAsync(dynamicSettings.GeneralAudioMixer, ct)).Value;
            var audioMixerVolumesController = new AudioMixerVolumesController(generalAudioMixer);

            var multiplayerMovementMessageBus = new MultiplayerMovementMessageBus(messagePipesHub, entityParticipantTable, globalWorld);

            var badgesAPIClient = new BadgesAPIClient(staticContainer.WebRequestsContainer.WebRequestController, bootstrapContainer.DecentralandUrlsSource);

            ICameraReelImagesMetadataDatabase cameraReelImagesMetadataDatabase = new CameraReelImagesMetadataRemoteDatabase(staticContainer.WebRequestsContainer.WebRequestController, bootstrapContainer.DecentralandUrlsSource);
            ICameraReelScreenshotsStorage cameraReelScreenshotsStorage = new CameraReelS3BucketScreenshotsStorage(staticContainer.WebRequestsContainer.WebRequestController);

            var cameraReelStorageService = new CameraReelRemoteStorageService(cameraReelImagesMetadataDatabase, cameraReelScreenshotsStorage, identityCache.Identity?.Address);

            IUserCalendar userCalendar = new GoogleUserCalendar(webBrowser);
            var clipboardManager = new ClipboardManager(clipboard);
            ITextFormatter hyperlinkTextFormatter = new HyperlinkTextFormatter(profileCache, selfProfile);

            var notificationsRequestController = new NotificationsRequestController(staticContainer.WebRequestsContainer.WebRequestController, notificationsBusController, bootstrapContainer.DecentralandUrlsSource, identityCache, includeFriends);
            notificationsRequestController.StartGettingNewNotificationsOverTimeAsync(ct).SuppressCancellationThrow().Forget();

            // Local scene development scenes are excluded from deeplink runtime handling logic
            if (appArgs.HasFlag(AppArgsFlags.LOCAL_SCENE) == false)
            {
                DeepLinkHandle deepLinkHandleImplementation = new DeepLinkHandle(dynamicWorldParams.StartParcel, chatTeleporter, ct);
                deepLinkHandleImplementation.StartListenForDeepLinksAsync(ct).Forget();
            }

            var friendServiceProxy = new ObjectProxy<IFriendsService>();
            var friendOnlineStatusCacheProxy = new ObjectProxy<FriendsConnectivityStatusTracker>();
            var friendsCacheProxy = new ObjectProxy<FriendsCache>();

            ISpriteCache thumbnailCache = new SpriteCache(staticContainer.WebRequestsContainer.WebRequestController);
            ProfileRepositoryWrapper profileRepositoryWrapper = new ProfileRepositoryWrapper(profileRepository, thumbnailCache, remoteMetadata);

            IChatEventBus chatEventBus = new ChatEventBus();
            IFriendsEventBus friendsEventBus = new DefaultFriendsEventBus();
            CommunitiesEventBus communitiesEventBus = new CommunitiesEventBus();

            var profileChangesBus = new ProfileChangesBus();

            GenericUserProfileContextMenuSettings genericUserProfileContextMenuSettingsSo = (await assetsProvisioner.ProvideMainAssetAsync(dynamicSettings.GenericUserProfileContextMenuSettings, ct)).Value;

            var communitiesDataProvider = new CommunitiesDataProvider(staticContainer.WebRequestsContainer.WebRequestController, bootstrapContainer.DecentralandUrlsSource, identityCache);

            IMVCManagerMenusAccessFacade menusAccessFacade = new MVCManagerMenusAccessFacade(
                mvcManager,
                profileCache,
                friendServiceProxy,
                chatEventBus,
                genericUserProfileContextMenuSettingsSo,
                includeUserBlocking,
                bootstrapContainer.Analytics,
                onlineUsersProvider,
                realmNavigator,
                friendOnlineStatusCacheProxy,
                profileRepository,
                sharedSpaceManager,
                includeVoiceChat);

            ViewDependencies.Initialize(new ViewDependencies(
                unityEventSystem,
                menusAccessFacade,
                clipboardManager,
                dclCursor,
                new ContextMenuOpener(mvcManager),
                identityCache,
                new ConfirmationDialogOpener(mvcManager)));

            var realmNftNamesProvider = new RealmNftNamesProvider(staticContainer.WebRequestsContainer.WebRequestController,
                staticContainer.RealmData);

            var lambdasProfilesProvider = new LambdasProfilesProvider(staticContainer.WebRequestsContainer.WebRequestController, bootstrapContainer.DecentralandUrlsSource);

            var globalPlugins = new List<IDCLGlobalPlugin>
            {
                new MultiplayerPlugin(
                    assetsProvisioner,
                    archipelagoIslandRoom,
                    gateKeeperSceneRoom,
                    chatRoom,
                    roomHub,
                    roomsStatus,
                    profileRepository,
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
                new WorldInfoPlugin(worldInfoHub, debugBuilder, chatHistory),
                new CharacterMotionPlugin(assetsProvisioner, staticContainer.CharacterContainer.CharacterObject, debugBuilder, staticContainer.ComponentsContainer.ComponentPoolsRegistry, staticContainer.SceneReadinessReportQueue),
                new InputPlugin(dclCursor, unityEventSystem, assetsProvisioner, dynamicWorldDependencies.CursorUIDocument, multiplayerEmotesMessageBus, mvcManager, debugBuilder, dynamicWorldDependencies.RootUIDocument, dynamicWorldDependencies.ScenesUIDocument, dynamicWorldDependencies.CursorUIDocument),
                new GlobalInteractionPlugin(dynamicWorldDependencies.RootUIDocument, assetsProvisioner, staticContainer.EntityCollidersGlobalCache, exposedGlobalDataContainer.GlobalInputEvents, unityEventSystem, mvcManager, menusAccessFacade),
                new CharacterCameraPlugin(assetsProvisioner, realmSamplingData, exposedGlobalDataContainer.ExposedCameraData, debugBuilder, dynamicWorldDependencies.CommandLineArgs),
                new WearablePlugin(assetsProvisioner, staticContainer.WebRequestsContainer.WebRequestController, staticContainer.RealmData, assetBundlesURL, staticContainer.CacheCleaner, wearableCatalog, builderContentURL.Value, builderCollectionsPreview),
                new EmotePlugin(staticContainer.WebRequestsContainer.WebRequestController, emotesCache, staticContainer.RealmData, multiplayerEmotesMessageBus, debugBuilder,
                    assetsProvisioner, selfProfile, mvcManager, staticContainer.CacheCleaner, identityCache, entityParticipantTable, assetBundlesURL, dclCursor, staticContainer.InputBlock, globalWorld, playerEntity, builderContentURL.Value, localSceneDevelopment, sharedSpaceManager, builderCollectionsPreview, appArgs),
                new ProfilingPlugin(staticContainer.Profiler, staticContainer.RealmData, staticContainer.SingletonSharedDependencies.MemoryBudget, debugBuilder, staticContainer.ScenesCache, dclVersion, physicsSettings.Value, staticContainer.SceneLoadingLimit),
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
                    nametagsData,
                    defaultTexturesContainer.TextureArrayContainerFactory,
                    wearableCatalog,
                    userBlockingCacheProxy),
                new MainUIPlugin(mvcManager, mainUIView, includeFriends, sharedSpaceManager),
                new ProfilePlugin(profileRepository, profileCache, staticContainer.CacheCleaner),
                new MapRendererPlugin(mapRendererContainer.MapRenderer),
                new SidebarPlugin(
                    assetsProvisioner, mvcManager, mainUIView, notificationsBusController,
                    notificationsRequestController, identityCache, profileRepository,
                    staticContainer.WebRequestsContainer.WebRequestController,
                    webBrowser, dynamicWorldDependencies.Web3Authenticator,
                    initializationFlowContainer.InitializationFlow,
                    profileCache,
                    globalWorld, playerEntity, includeCameraReel, includeFriends, includeMarketplaceCredits,
                    chatHistory, profileRepositoryWrapper, sharedSpaceManager, profileChangesBus,
                    selfProfile, staticContainer.RealmData, staticContainer.SceneRestrictionBusController,
                    bootstrapContainer.DecentralandUrlsSource),
                new ErrorPopupPlugin(mvcManager, assetsProvisioner),
                new MinimapPlugin(mvcManager, minimap),
                new ChatPlugin(
                    mvcManager,
                    chatMessagesBus,
                    chatHistory,
                    entityParticipantTable,
                    nametagsData,
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
                    thumbnailCache,
                    mainUIView.WarningNotification,
                    communitiesEventBus,
                    voiceChatCallStatusService,
                    includeVoiceChat,
                    realmNavigator),
                new ExplorePanelPlugin(
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
                    profileRepository,
                    dynamicWorldDependencies.Web3Authenticator,
                    initializationFlowContainer.InitializationFlow,
                    selfProfile,
                    equippedWearables,
                    equippedEmotes,
                    webBrowser,
                    emotesCache,
                    forceRender,
                    staticContainer.RealmData,
                    profileCache,
                    assetBundlesURL,
                    notificationsBusController,
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
                    bootstrapContainer.WorldVolumeMacBus,
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
                    includeVoiceChat,
                    galleryEventBus
                ),
                new CharacterPreviewPlugin(staticContainer.ComponentsContainer.ComponentPoolsRegistry, assetsProvisioner, staticContainer.CacheCleaner),
                new WebRequestsPlugin(staticContainer.WebRequestsContainer.AnalyticsContainer, debugBuilder),
                new Web3AuthenticationPlugin(assetsProvisioner, dynamicWorldDependencies.Web3Authenticator, debugBuilder, mvcManager, selfProfile, webBrowser, staticContainer.RealmData, identityCache, characterPreviewFactory, dynamicWorldDependencies.SplashScreen, audioMixerVolumesController, characterPreviewEventBus, globalWorld),
                new SkyboxPlugin(assetsProvisioner, dynamicSettings.DirectionalLight, staticContainer.ScenesCache, staticContainer.SceneRestrictionBusController),
                new LoadingScreenPlugin(assetsProvisioner, mvcManager, audioMixerVolumesController,
                    staticContainer.InputBlock, debugBuilder, staticContainer.LoadingStatus),
                new ExternalUrlPromptPlugin(assetsProvisioner, webBrowser, mvcManager, dclCursor),
                new TeleportPromptPlugin(
                    assetsProvisioner,
                    mvcManager,
                    staticContainer.WebRequestsContainer.WebRequestController,
                    placesAPIService,
                    dclCursor,
                    chatMessagesBus
                ),
                new ChangeRealmPromptPlugin(
                    assetsProvisioner,
                    mvcManager,
                    dclCursor,
                    realmUrl => chatMessagesBus.Send(ChatChannel.NEARBY_CHANNEL, $"/{ChatCommandsUtils.COMMAND_GOTO} {realmUrl}", "RestrictedActionAPI")),
                new NftPromptPlugin(assetsProvisioner, webBrowser, mvcManager, nftInfoAPIClient, staticContainer.WebRequestsContainer.WebRequestController, dclCursor),
                staticContainer.CharacterContainer.CreateGlobalPlugin(),
                staticContainer.QualityContainer.CreatePlugin(),
                new MultiplayerMovementPlugin(
                    assetsProvisioner,
                    multiplayerMovementMessageBus,
                    debugBuilder,
                    remoteEntities,
                    staticContainer.CharacterContainer.Transform,
                    multiplayerDebugSettings,
                    appArgs,
                    entityParticipantTable,
                    staticContainer.RealmData,
                    remoteMetadata),
                new AudioPlaybackPlugin(terrainContainer.GenesisTerrain, assetsProvisioner, dynamicWorldParams.EnableLandscape),
                new RealmDataDirtyFlagPlugin(staticContainer.RealmData),
                new NotificationPlugin(
                    assetsProvisioner,
                    mvcManager,
                    staticContainer.WebRequestsContainer.WebRequestController,
                    notificationsBusController,
                    sharedSpaceManager),
                new RewardPanelPlugin(mvcManager, assetsProvisioner, notificationsBusController, staticContainer.WebRequestsContainer.WebRequestController),
                new PassportPlugin(
                    assetsProvisioner,
                    mvcManager,
                    dclCursor,
                    profileRepository,
                    characterPreviewFactory,
                    staticContainer.RealmData,
                    assetBundlesURL,
                    staticContainer.WebRequestsContainer.WebRequestController,
                    characterPreviewEventBus,
                    selfProfile,
                    webBrowser,
                    bootstrapContainer.DecentralandUrlsSource,
                    badgesAPIClient,
                    notificationsBusController,
                    staticContainer.InputBlock,
                    remoteMetadata,
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
                    isNameEditorEnabled,
                    includeVoiceChat,
                    chatEventBus,
                    sharedSpaceManager,
                    profileRepositoryWrapper,
                    voiceChatCallStatusService,
                    galleryEventBus
                ),
                new GenericPopupsPlugin(assetsProvisioner, mvcManager, clipboardManager),
                new GenericContextMenuPlugin(assetsProvisioner, mvcManager, profileRepositoryWrapper),
                realmNavigatorContainer.CreatePlugin(),
                new GPUInstancingPlugin(staticContainer.GPUInstancingService, assetsProvisioner, staticContainer.RealmData, staticContainer.LoadingStatus, exposedGlobalDataContainer.ExposedCameraData),
                new ConfirmationDialogPlugin(assetsProvisioner, mvcManager, profileRepositoryWrapper),
            };

            if (includeVoiceChat)
                globalPlugins.Add(
                    new VoiceChatPlugin(
                        assetsProvisioner,
                        roomHub,
                        mainUIView,
                        voiceChatCallStatusService,
                        profileRepositoryWrapper,
                        entityParticipantTable,
                        globalWorld,
                        playerEntity));


            if (!appArgs.HasDebugFlag() || !appArgs.HasFlagWithValueFalse(AppArgsFlags.LANDSCAPE_TERRAIN_ENABLED))
                globalPlugins.Add(terrainContainer.CreatePlugin(staticContainer, bootstrapContainer, mapRendererContainer, debugBuilder, FeatureFlagsConfiguration.Instance.IsEnabled(FeatureFlagsStrings.GPUI_ENABLED)));

            if (localSceneDevelopment)
                globalPlugins.Add(new LocalSceneDevelopmentPlugin(reloadSceneController, realmUrls));
            else
            {
                globalPlugins.Add(lodContainer.LODPlugin);
                globalPlugins.Add(lodContainer.RoadPlugin);
            }

            if (localSceneDevelopment || builderCollectionsPreview)
                globalPlugins.Add(new GlobalGLTFLoadingPlugin(staticContainer.WebRequestsContainer.WebRequestController, staticContainer.RealmData, builderContentURL.Value, localSceneDevelopment));

            globalPlugins.AddRange(staticContainer.SharedPlugins);

            if (includeFriends)
            {
                // TODO many circular dependencies - adjust the flow and get rid of ObjectProxy
                var friendsContainer = new FriendsContainer(
                    mainUIView,
                    mvcManager,
                    assetsProvisioner,
                    identityCache,
                    profileRepository,
                    staticContainer.LoadingStatus,
                    staticContainer.InputBlock,
                    selfProfile,
                    new MVCPassportBridge(mvcManager),
                    notificationsBusController,
                    onlineUsersProvider,
                    realmNavigator,
                    includeUserBlocking,
                    includeVoiceChat,
                    appArgs,
                    dynamicWorldParams.EnableAnalytics,
                    bootstrapContainer.Analytics,
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
                    voiceChatCallStatusService
                );

                globalPlugins.Add(friendsContainer);
            }

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
                    staticContainer.WebRequestsContainer.WebRequestController,
                    profileRepository,
                    realmNavigator,
                    assetsProvisioner,
                    wearableCatalog,
                    wearablesProvider,
                    assetBundlesURL,
                    dclCursor,
                    mainUIView.SidebarView.EnsureNotNull().InWorldCameraButton,
                    globalWorld,
                    debugBuilder,
                    nametagsData,
                    profileRepositoryWrapper,
                    sharedSpaceManager,
                    identityCache,
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
                    notificationsBusController,
                    staticContainer.RealmData,
                    sharedSpaceManager,
                    identityCache,
                    staticContainer.LoadingStatus));
            }

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
                    notificationsBusController,
                    lambdasProfilesProvider,
                    bootstrapContainer.DecentralandUrlsSource,
                    identityCache));

            if (dynamicWorldParams.EnableAnalytics)
                globalPlugins.Add(new AnalyticsPlugin(
                        bootstrapContainer.Analytics!,
                        staticContainer.Profiler,
                        staticContainer.LoadingStatus,
                        staticContainer.RealmData,
                        staticContainer.ScenesCache,
                        staticContainer.MainPlayerAvatarBaseProxy,
                        identityCache,
                        debugBuilder,
                        cameraReelStorageService,
                        entityParticipantTable
                    )
                );

            if (localSceneDevelopment || appArgs.HasFlag(AppArgsFlags.SCENE_CONSOLE))
                globalPlugins.Add(new DebugMenuPlugin(DiagnosticsContainer.SceneConsoleLogEntryBus!, staticContainer.InputBlock, assetsProvisioner, currentSceneInfo, roomsStatus));

            var globalWorldFactory = new GlobalWorldFactory(
                in staticContainer,
                exposedGlobalDataContainer.CameraSamplingData,
                realmSamplingData,
                assetBundlesURL,
                staticContainer.RealmData,
                globalPlugins,
                debugBuilder,
                staticContainer.ScenesCache,
                dynamicWorldParams.HybridSceneParams,
                currentSceneInfo,
                lodContainer.LodCache,
                lodContainer.RoadCoordinates,
                lodContainer.LODSettings,
                multiplayerEmotesMessageBus,
                globalWorld,
                staticContainer.SceneReadinessReportQueue,
                localSceneDevelopment,
                profileRepository,
                bootstrapContainer.UseRemoteAssetBundles,
                lodContainer.RoadAssetsPool,
                staticContainer.SceneLoadingLimit
            );

            staticContainer.RoomHubProxy.SetObject(roomHub);

            var container = new DynamicWorldContainer(
                mvcManager,
                realmContainer.RealmController,
                globalWorldFactory,
                globalPlugins,
                profileRepository,
                initializationFlowContainer.InitializationFlow,
                chatMessagesBus,
                messagePipesHub,
                remoteMetadata,
                profileBroadcast,
                roomHub,
                socialServiceContainer,
                selfProfile
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
