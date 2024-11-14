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
using DCL.Chat;
using DCL.Chat.Commands;
using DCL.Chat.History;
using DCL.Chat.MessageBus;
using DCL.DebugUtilities;
using DCL.EventsApi;
using DCL.Input;
using DCL.Landscape;
using DCL.LOD.Systems;
using DCL.MapRenderer;
using DCL.Multiplayer.Connections.Archipelago.AdapterAddress.Current;
using DCL.Multiplayer.Connections.Archipelago.Rooms;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Multiplayer.Connections.GateKeeper.Meta;
using DCL.Multiplayer.Connections.GateKeeper.Rooms;
using DCL.Multiplayer.Connections.Messaging.Hubs;
using DCL.Multiplayer.Connections.Pools;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Multiplayer.Connections.Rooms.Status;
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
using DCL.NftInfoAPIService;
using DCL.Notifications;
using DCL.NotificationsBusController.NotificationsBus;
using DCL.Optimization.Pools;
using DCL.ParcelsService;
using DCL.PerformanceAndDiagnostics.Analytics;
using DCL.PlacesAPIService;
using DCL.PluginSystem;
using DCL.PluginSystem.Global;
using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.SceneLoadingScreens.LoadingScreen;
using DCL.SceneRestrictionBusController.SceneRestrictionBus;
using DCL.Settings;
using DCL.SidebarBus;
using DCL.StylizedSkybox.Scripts.Plugin;
using DCL.UI.MainUI;
using DCL.UserInAppInitializationFlow;
using DCL.Utilities.Extensions;
using DCL.Web3.Identities;
using DCL.WebRequests.Analytics;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.CurrentScene;
using ECS.SceneLifeCycle.LocalSceneDevelopment;
using ECS.SceneLifeCycle.Realm;
using Global.AppArgs;
using Global.Dynamic.ChatCommands;
using LiveKit.Internal.FFIClients.Pools;
using LiveKit.Internal.FFIClients.Pools.Memory;
using LiveKit.Proto;
using MVC;
using MVC.PopupsController.PopupCloser;
using PortableExperiences.Controller;
using SceneRunner.Debugging.Hub;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.EventSystems;
using UnityEngine.Pool;
using Utility.Ownership;
using Utility.PriorityQueue;
using Object = UnityEngine.Object;

namespace Global.Dynamic
{
    public class DynamicWorldContainer : DCLWorldContainer<DynamicWorldSettings>
    {
        private const string PARAMS_FORCED_EMOTES_FLAG = "self-force-emotes";

        private ECSReloadScene? reloadSceneController;
        private LocalSceneDevelopmentController? localSceneDevelopmentController;
        private IWearablesProvider? wearablesProvider;

        public IMVCManager MvcManager { get; private set; } = null!;

        public DefaultTexturesContainer DefaultTexturesContainer { get; private set; } = null!;

        public LODContainer LODContainer { get; private set; } = null!;

        public MapRendererContainer MapRendererContainer { get; private set; } = null!;

        public IGlobalRealmController RealmController { get; private set; } = null!;

        public GlobalWorldFactory GlobalWorldFactory { get; private set; } = null!;

        public IReadOnlyList<IDCLGlobalPlugin> GlobalPlugins { get; private set; } = null!;

        public IProfileRepository ProfileRepository { get; private set; } = null!;

        public ParcelServiceContainer ParcelServiceContainer { get; private set; } = null!;

        public RealUserInAppInitializationFlow UserInAppInAppInitializationFlow { get; private set; } = null!;

        // TODO move multiplayer related dependencies to a separate container
        public ICharacterDataPropagationUtility CharacterDataPropagationUtility { get; private set; } = null!;

        public IChatMessagesBus ChatMessagesBus { get; private set; } = null!;

        public IMessagePipesHub MessagePipesHub { get; private set; } = null!;

        public IRemoteMetadata RemoteMetadata { get; private set; } = null!;

        public ISceneRoomMetaDataSource SceneRoomMetaDataSource { get; private set; } = null!;

        public IProfileBroadcast ProfileBroadcast { get; private set; } = null!;

        public IRoomHub RoomHub { get; private set; } = null!;

        private MultiplayerMovementMessageBus? multiplayerMovementMessageBus;

        public override void Dispose()
        {
            ChatMessagesBus.Dispose();
            ProfileBroadcast.Dispose();
            MessagePipesHub.Dispose();
            localSceneDevelopmentController?.Dispose();
        }

        public static async UniTask<(DynamicWorldContainer? container, bool success)> CreateAsync(
            BootstrapContainer bootstrapContainer,
            DynamicWorldDependencies dynamicWorldDependencies,
            DynamicWorldParams dynamicWorldParams,
            AudioClipConfig backgroundMusic,
            IPortableExperiencesController portableExperiencesController,
            World globalWorld,
            Entity playerEntity,
            IAppArgs appArgs,
            ISceneRestrictionBusController sceneRestrictionBusController,
            CancellationToken ct)
        {
            var container = new DynamicWorldContainer();
            DynamicSettings dynamicSettings = dynamicWorldDependencies.DynamicSettings;
            StaticContainer staticContainer = dynamicWorldDependencies.StaticContainer;
            IWeb3IdentityCache identityCache = dynamicWorldDependencies.Web3IdentityCache;
            IAssetsProvisioner assetsProvisioner = dynamicWorldDependencies.AssetsProvisioner;
            IDebugContainerBuilder debugBuilder = dynamicWorldDependencies.DebugContainerBuilder;

            // If we have many undesired delays when using the third-party providers, it might be useful to cache it at app's bootstrap
            // So far, the chance of using it is quite low, so it's preferable to do it lazy avoiding extra requests & memory allocations
            IThirdPartyNftProviderSource thirdPartyNftProviderSource = new RealmThirdPartyNftProviderSource(staticContainer.WebRequestsContainer.WebRequestController,
                staticContainer.RealmData);

            IEventsApiService eventsApiService = new HttpEventsApiService(staticContainer.WebRequestsContainer.WebRequestController,
                URLDomain.FromString(bootstrapContainer.DecentralandUrlsSource.Url(DecentralandUrl.ApiEvents)));
            var placesAPIService = new PlacesAPIService(new PlacesAPIClient(staticContainer.WebRequestsContainer.WebRequestController, bootstrapContainer.DecentralandUrlsSource));
            var mapPathEventBus = new MapPathEventBus();
            INotificationsBusController notificationsBusController = new NotificationsBusController();

            async UniTask InitializeContainersAsync(IPluginSettingsContainer settingsContainer, CancellationToken ct)
            {
                // Init itself
                await settingsContainer.InitializePluginAsync(container, ct)!.ThrowOnFail();

                // Init other containers
                container.DefaultTexturesContainer = await DefaultTexturesContainer.CreateAsync(settingsContainer, assetsProvisioner, ct).ThrowOnFail();
                container.LODContainer = await LODContainer.CreateAsync(assetsProvisioner, bootstrapContainer.DecentralandUrlsSource, staticContainer, settingsContainer, staticContainer.RealmData, container.DefaultTexturesContainer.TextureArrayContainerFactory, debugBuilder, dynamicWorldParams.EnableLOD, ct).ThrowOnFail();
                container.MapRendererContainer = await MapRendererContainer.CreateAsync(settingsContainer, staticContainer, bootstrapContainer.DecentralandUrlsSource, assetsProvisioner, placesAPIService, mapPathEventBus, notificationsBusController, ct);
            }

            try { await InitializeContainersAsync(dynamicWorldDependencies.SettingsContainer, ct); }
            catch (Exception) { return (null, false); }

            CursorSettings cursorSettings = (await assetsProvisioner.ProvideMainAssetAsync(dynamicSettings.CursorSettings, ct)).Value;
            ProvidedAsset<Texture2D> normalCursorAsset = await assetsProvisioner.ProvideMainAssetAsync(cursorSettings.NormalCursor, ct);
            ProvidedAsset<Texture2D> interactionCursorAsset = await assetsProvisioner.ProvideMainAssetAsync(cursorSettings.InteractionCursor, ct);
            ProvidedAsset<MultiplayerDebugSettings> multiplayerDebugSettings = await assetsProvisioner.ProvideMainAssetAsync(dynamicSettings.MultiplayerDebugSettings, ct);

            var unityEventSystem = new UnityEventSystem(EventSystem.current.EnsureNotNull());
            var dclCursor = new DCLCursor(normalCursorAsset.Value, interactionCursorAsset.Value, cursorSettings.NormalCursorHotspot, cursorSettings.InteractionCursorHotspot);

            staticContainer.QualityContainer.AddDebugViews(debugBuilder);

            var realmSamplingData = new RealmSamplingData();
            var dclInput = new DCLInput();
            staticContainer.InputProxy.SetObject(dclInput);

            ExposedGlobalDataContainer exposedGlobalDataContainer = staticContainer.ExposedGlobalDataContainer;

            PopupCloserView popupCloserView = Object.Instantiate((await assetsProvisioner.ProvideMainAssetAsync(dynamicSettings.PopupCloserView, ct: CancellationToken.None)).Value.GetComponent<PopupCloserView>()).EnsureNotNull();
            MainUIView mainUIView = Object.Instantiate((await assetsProvisioner.ProvideMainAssetAsync(dynamicSettings.MainUIView, ct: CancellationToken.None)).Value.GetComponent<MainUIView>()).EnsureNotNull();

            var coreMvcManager = new MVCManager(new WindowStackManager(), new CancellationTokenSource(), popupCloserView);

            container.MvcManager = dynamicWorldParams.EnableAnalytics
                ? new MVCManagerAnalyticsDecorator(coreMvcManager, bootstrapContainer.Analytics!)
                : coreMvcManager;

            var parcelServiceContainer = ParcelServiceContainer.Create(staticContainer.RealmData, staticContainer.SceneReadinessReportQueue, debugBuilder, container.MvcManager, staticContainer.SingletonSharedDependencies.SceneAssetLock);
            container.ParcelServiceContainer = parcelServiceContainer;

            var nftInfoAPIClient = new OpenSeaAPIClient(staticContainer.WebRequestsContainer.WebRequestController, bootstrapContainer.DecentralandUrlsSource);
            var wearableCatalog = new WearableStorage();
            var characterPreviewFactory = new CharacterPreviewFactory(staticContainer.ComponentsContainer.ComponentPoolsRegistry);
            IWebBrowser webBrowser = bootstrapContainer.WebBrowser;
            ChatEntryConfigurationSO chatEntryConfiguration = (await assetsProvisioner.ProvideMainAssetAsync(dynamicSettings.ChatEntryConfiguration, ct)).Value;
            NametagsData nametagsData = (await assetsProvisioner.ProvideMainAssetAsync(dynamicSettings.NametagsData, ct)).Value;

            IProfileCache profileCache = new DefaultProfileCache();

            container.ProfileRepository = new LogProfileRepository(
                new RealmProfileRepository(staticContainer.WebRequestsContainer.WebRequestController, staticContainer.RealmData, profileCache)
            );

            var genesisTerrain = new TerrainGenerator(staticContainer.Profiler);
            var worldsTerrain = new WorldTerrainGenerator();
            var satelliteView = new SatelliteFloor();
            var landscapePlugin = new LandscapePlugin(satelliteView, genesisTerrain, worldsTerrain, assetsProvisioner,
                debugBuilder, container.MapRendererContainer.TextureContainer,
                staticContainer.WebRequestsContainer.WebRequestController, dynamicWorldParams.EnableLandscape,
                bootstrapContainer.Environment.Equals(DecentralandEnvironment.Zone));

            IMultiPool MultiPoolFactory() =>
                new DCLMultiPool();

            var memoryPool = new ArrayMemoryPool(ArrayPool<byte>.Shared!);

            var assetBundlesURL = URLDomain.FromString(bootstrapContainer.DecentralandUrlsSource.Url(DecentralandUrl.AssetBundlesCDN));

            var emotesCache = new MemoryEmotesStorage();
            staticContainer.CacheCleaner.Register(emotesCache);
            var equippedWearables = new EquippedWearables();
            var equippedEmotes = new EquippedEmotes();
            var forceRender = new List<string>();

            List<URN> selfEmotes = new List<URN>();
            ParseParamsForcedEmotes(bootstrapContainer.ApplicationParametersParser, ref selfEmotes);
            ParseDebugForcedEmotes(bootstrapContainer.DebugSettings.EmotesToAddToUserProfile, ref selfEmotes);

            var selfProfile = new SelfProfile(container.ProfileRepository, identityCache, equippedWearables, wearableCatalog,
                emotesCache, equippedEmotes, forceRender, selfEmotes);

            IEmoteProvider emoteProvider = new ApplicationParamsEmoteProvider(appArgs,
                new EcsEmoteProvider(globalWorld, staticContainer.RealmData));

            container.wearablesProvider = new ApplicationParametersWearablesProvider(appArgs,
                new ECSWearablesProvider(identityCache, globalWorld),
                globalWorld);

            container.SceneRoomMetaDataSource = new SceneRoomMetaDataSource(staticContainer.RealmData, staticContainer.CharacterContainer.Transform, placesAPIService, dynamicWorldParams.IsolateScenesCommunication);

            var metaDataSource = new SceneRoomLogMetaDataSource(container.SceneRoomMetaDataSource);

            IGateKeeperSceneRoom gateKeeperSceneRoom = new GateKeeperSceneRoom(staticContainer.WebRequestsContainer.WebRequestController, metaDataSource, bootstrapContainer.DecentralandUrlsSource, staticContainer.ScenesCache)
               .AsActivatable();

            var currentAdapterAddress = ICurrentAdapterAddress.NewDefault(staticContainer.RealmData);

            var archipelagoIslandRoom = IArchipelagoIslandRoom.NewDefault(
                identityCache,
                MultiPoolFactory(),
                staticContainer.CharacterContainer.CharacterObject,
                currentAdapterAddress,
                staticContainer.WebRequestsContainer.WebRequestController
            );

            container.RealmController = new RealmController(
                identityCache,
                staticContainer.WebRequestsContainer.WebRequestController,
                parcelServiceContainer.TeleportController,
                parcelServiceContainer.RetrieveSceneFromFixedRealm,
                parcelServiceContainer.RetrieveSceneFromVolatileWorld,
                dynamicWorldParams.StaticLoadPositions,
                staticContainer.RealmData,
                staticContainer.ScenesCache,
                staticContainer.PartitionDataContainer,
                staticContainer.SingletonSharedDependencies.SceneAssetLock,
                debugBuilder);

            bool localSceneDevelopment = !string.IsNullOrEmpty(dynamicWorldParams.LocalSceneDevelopmentRealm);
            container.reloadSceneController = new ECSReloadScene(staticContainer.ScenesCache, globalWorld, playerEntity, localSceneDevelopment);

            if (localSceneDevelopment)
                container.localSceneDevelopmentController = new LocalSceneDevelopmentController(container.reloadSceneController, dynamicWorldParams.LocalSceneDevelopmentRealm);

            container.RoomHub = localSceneDevelopment ? NullRoomHub.INSTANCE : new RoomHub(archipelagoIslandRoom, gateKeeperSceneRoom);
            container.MessagePipesHub = new MessagePipesHub(container.RoomHub, MultiPoolFactory(), MultiPoolFactory(), memoryPool);

            RoomsStatus roomsStatus = new RoomsStatus(
                container.RoomHub,

                //override allowed only in Editor
                Application.isEditor
                    ? new LinkedBox<(bool use, ConnectionQuality quality)>(
                        () => (bootstrapContainer.DebugSettings.OverrideConnectionQuality, bootstrapContainer.DebugSettings.ConnectionQuality)
                    )
                    : new Box<(bool use, ConnectionQuality quality)>((false, ConnectionQuality.QualityExcellent))
            );

            var entityParticipantTable = new EntityParticipantTable();

            var queuePoolFullMovementMessage = new ObjectPool<SimplePriorityQueue<NetworkMovementMessage>>(
                () => new SimplePriorityQueue<NetworkMovementMessage>(),
                actionOnRelease: queue => queue.Clear()
            );

            var remoteEntities = new RemoteEntities(
                container.RoomHub,
                entityParticipantTable,
                staticContainer.ComponentsContainer.ComponentPoolsRegistry,
                queuePoolFullMovementMessage,
                staticContainer.EntityCollidersGlobalCache
            );

            ILoadingScreen loadingScreen = new LoadingScreen(container.MvcManager);

            IRealmNavigator realmNavigator = new RealmNavigator(
                loadingScreen,
                container.MapRendererContainer.MapRenderer,
                container.RealmController,
                parcelServiceContainer.TeleportController,
                container.RoomHub,
                remoteEntities,
                bootstrapContainer.DecentralandUrlsSource,
                globalWorld,
                container.LODContainer.RoadPlugin,
                genesisTerrain,
                worldsTerrain,
                satelliteView,
                dynamicWorldParams.EnableLandscape,
                staticContainer.ExposedGlobalDataContainer.ExposedCameraData.CameraEntityProxy,
                exposedGlobalDataContainer.CameraSamplingData,
                localSceneDevelopment,
                staticContainer.LoadingStatus,
                staticContainer.CacheCleaner,
                staticContainer.SingletonSharedDependencies.MemoryBudget,
                staticContainer.FeatureFlagsCache);

            IHealthCheck livekitHealthCheck = bootstrapContainer.DebugSettings.EnableEmulateNoLivekitConnection
                ? new IHealthCheck.AlwaysFails("Livekit connection is in debug, always fail mode")
                : new SequentialHealthCheck(
                    new MultipleURLHealthCheck(staticContainer.WebRequestsContainer.WebRequestController, bootstrapContainer.DecentralandUrlsSource,
                        DecentralandUrl.ArchipelagoStatus,
                        DecentralandUrl.GatekeeperStatus
                    ),
                    new StartLiveKitRooms(container.RoomHub)
                );

            livekitHealthCheck = dynamicWorldParams.EnableAnalytics
                ? livekitHealthCheck.WithFailAnalytics(bootstrapContainer.Analytics!)
                : livekitHealthCheck;

            livekitHealthCheck.WithRetries();

            container.UserInAppInAppInitializationFlow = new RealUserInAppInitializationFlow(
                staticContainer.LoadingStatus,
                livekitHealthCheck,
                bootstrapContainer.DecentralandUrlsSource,
                container.MvcManager,
                selfProfile,
                dynamicWorldParams.StartParcel,
                staticContainer.MainPlayerAvatarBaseProxy,
                backgroundMusic,
                realmNavigator,
                loadingScreen,
                staticContainer.FeatureFlagsProvider,
                staticContainer.FeatureFlagsCache,
                identityCache,
                container.RealmController,
                dynamicWorldParams.AppParameters,
                bootstrapContainer.DebugSettings,
                staticContainer.PortableExperiencesController,
                container.RoomHub
            );

            var worldInfoHub = new LocationBasedWorldInfoHub(
                new WorldInfoHub(staticContainer.SingletonSharedDependencies.SceneMapping),
                staticContainer.CharacterContainer.CharacterObject
            );

            dynamicWorldDependencies.WorldInfoTool.Initialize(worldInfoHub);

            container.CharacterDataPropagationUtility = new CharacterDataPropagationUtility(staticContainer.ComponentsContainer.ComponentPoolsRegistry.AddComponentPool<SDKProfile>());

            var chatHistory = new ChatHistory();

            var currentSceneInfo = new CurrentSceneInfo();
            ConnectionStatusPanelPlugin connectionStatusPanelPlugin = new ConnectionStatusPanelPlugin(container.UserInAppInAppInitializationFlow, container.MvcManager, mainUIView, roomsStatus, currentSceneInfo, container.reloadSceneController, globalWorld, playerEntity, debugBuilder);

            var chatCommandsFactory = new Dictionary<Regex, Func<IChatCommand>>
            {
                { GoToChatCommand.REGEX, () => new GoToChatCommand(realmNavigator) },
                {
                    ChangeRealmChatCommand.REGEX,
                    () => new ChangeRealmChatCommand(realmNavigator, bootstrapContainer.DecentralandUrlsSource,
                        new EnvironmentValidator(bootstrapContainer.Environment))
                },
                { DebugPanelChatCommand.REGEX, () => new DebugPanelChatCommand(debugBuilder, connectionStatusPanelPlugin) },
                { ShowEntityInfoChatCommand.REGEX, () => new ShowEntityInfoChatCommand(worldInfoHub) },
                { ClearChatCommand.REGEX, () => new ClearChatCommand(chatHistory) },
                { ReloadSceneChatCommand.REGEX, () => new ReloadSceneChatCommand(container.reloadSceneController) },
                { LoadPortableExperienceChatCommand.REGEX, () => new LoadPortableExperienceChatCommand(portableExperiencesController, staticContainer.FeatureFlagsCache)},
                { KillPortableExperienceChatCommand.REGEX, () => new KillPortableExperienceChatCommand(portableExperiencesController, staticContainer.FeatureFlagsCache)},
            };

            IChatMessagesBus coreChatMessageBus = new MultiplayerChatMessagesBus(container.MessagePipesHub, container.ProfileRepository, new MessageDeduplication<double>())
                                                 .WithSelfResend(identityCache, container.ProfileRepository)
                                                 .WithIgnoreSymbols()
                                                 .WithCommands(chatCommandsFactory)
                                                 .WithDebugPanel(debugBuilder);

            container.ChatMessagesBus = dynamicWorldParams.EnableAnalytics
                ? new ChatMessagesBusAnalyticsDecorator(coreChatMessageBus, bootstrapContainer.Analytics!)
                : coreChatMessageBus;

            var coreBackpackEventBus = new BackpackEventBus();

            IBackpackEventBus backpackEventBus = dynamicWorldParams.EnableAnalytics
                ? new BackpackEventBusAnalyticsDecorator(coreBackpackEventBus, bootstrapContainer.Analytics!)
                : coreBackpackEventBus;

            container.ProfileBroadcast = new DebounceProfileBroadcast(
                new EnsureSelfPublishedProfileBroadcast(
                    new ProfileBroadcast(container.MessagePipesHub, selfProfile),
                    selfProfile,
                    staticContainer.RealmData
                )
            );

            var notificationsRequestController = new NotificationsRequestController(staticContainer.WebRequestsContainer.WebRequestController, notificationsBusController, bootstrapContainer.DecentralandUrlsSource, identityCache);
            notificationsRequestController.StartGettingNewNotificationsOverTimeAsync(ct).SuppressCancellationThrow().Forget();

            var multiplayerEmotesMessageBus = new MultiplayerEmotesMessageBus(container.MessagePipesHub, multiplayerDebugSettings);

            container.RemoteMetadata = new DebounceRemoteMetadata(new RemoteMetadata(container.RoomHub, staticContainer.RealmData));

            var characterPreviewEventBus = new CharacterPreviewEventBus();
            var sidebarBus = new SidebarBus();
            AudioMixer generalAudioMixer = (await assetsProvisioner.ProvideMainAssetAsync(dynamicSettings.GeneralAudioMixer, ct)).Value;
            var audioMixerVolumesController = new AudioMixerVolumesController(generalAudioMixer);

            container.multiplayerMovementMessageBus = new MultiplayerMovementMessageBus(container.MessagePipesHub, entityParticipantTable, globalWorld);

            var badgesAPIClient = new BadgesAPIClient(staticContainer.WebRequestsContainer.WebRequestController, bootstrapContainer.DecentralandUrlsSource);

            IUserCalendar userCalendar = new GoogleUserCalendar(webBrowser);

            var globalPlugins = new List<IDCLGlobalPlugin>
            {
                new MultiplayerPlugin(
                    assetsProvisioner,
                    archipelagoIslandRoom,
                    gateKeeperSceneRoom,
                    container.RoomHub,
                    roomsStatus,
                    container.ProfileRepository,
                    container.ProfileBroadcast,
                    debugBuilder,
                    staticContainer.LoadingStatus,
                    entityParticipantTable,
                    container.MessagePipesHub,
                    container.RemoteMetadata,
                    staticContainer.CharacterContainer.CharacterObject,
                    staticContainer.RealmData,
                    remoteEntities,
                    staticContainer.ScenesCache,
                    emotesCache,
                    container.CharacterDataPropagationUtility,
                    staticContainer.ComponentsContainer.ComponentPoolsRegistry
                ),
                new WorldInfoPlugin(worldInfoHub, debugBuilder, chatHistory),
                new CharacterMotionPlugin(assetsProvisioner, staticContainer.CharacterContainer.CharacterObject, debugBuilder, staticContainer.ComponentsContainer.ComponentPoolsRegistry),
                new InputPlugin(dclInput, dclCursor, unityEventSystem, assetsProvisioner, dynamicWorldDependencies.CursorUIDocument, multiplayerEmotesMessageBus, container.MvcManager, debugBuilder, dynamicWorldDependencies.RootUIDocument, dynamicWorldDependencies.CursorUIDocument, exposedGlobalDataContainer.ExposedCameraData),
                new GlobalInteractionPlugin(dclInput, dynamicWorldDependencies.RootUIDocument, assetsProvisioner, staticContainer.EntityCollidersGlobalCache, exposedGlobalDataContainer.GlobalInputEvents, dclCursor, unityEventSystem, container.MvcManager),
                new CharacterCameraPlugin(assetsProvisioner, realmSamplingData, exposedGlobalDataContainer.ExposedCameraData, debugBuilder, dynamicWorldDependencies.CommandLineArgs, dclInput),
                new WearablePlugin(assetsProvisioner, staticContainer.WebRequestsContainer.WebRequestController, staticContainer.RealmData, assetBundlesURL, staticContainer.CacheCleaner, wearableCatalog),
                new EmotePlugin(staticContainer.WebRequestsContainer.WebRequestController, emotesCache, staticContainer.RealmData, multiplayerEmotesMessageBus, debugBuilder,
                    assetsProvisioner, selfProfile, container.MvcManager, dclInput, staticContainer.CacheCleaner, identityCache, entityParticipantTable, assetBundlesURL, mainUIView, dclCursor, staticContainer.InputBlock, globalWorld, playerEntity),
                new ProfilingPlugin(staticContainer.Profiler, staticContainer.RealmData, staticContainer.SingletonSharedDependencies.MemoryBudget, debugBuilder, staticContainer.ScenesCache),
                new AvatarPlugin(
                    staticContainer.ComponentsContainer.ComponentPoolsRegistry,
                    assetsProvisioner,
                    staticContainer.SingletonSharedDependencies.FrameTimeBudget,
                    staticContainer.SingletonSharedDependencies.MemoryBudget,
                    staticContainer.RealmData,
                    staticContainer.MainPlayerAvatarBaseProxy,
                    debugBuilder,
                    staticContainer.CacheCleaner,
                    chatEntryConfiguration,
                    new DefaultFaceFeaturesHandler(wearableCatalog),
                    nametagsData,
                    container.DefaultTexturesContainer.TextureArrayContainerFactory,
                    wearableCatalog,
                    remoteEntities,
                    staticContainer.CharacterContainer.Transform),
                new MainUIPlugin(container.MvcManager, sidebarBus, mainUIView),
                new ProfilePlugin(container.ProfileRepository, profileCache, staticContainer.CacheCleaner, new ProfileIntentionCache()),
                new MapRendererPlugin(container.MapRendererContainer.MapRenderer),
                new SidebarPlugin(
                    assetsProvisioner,
                    container.MvcManager,
                    mainUIView,
                    notificationsBusController,
                    notificationsRequestController,
                    identityCache,
                    container.ProfileRepository,
                    staticContainer.WebRequestsContainer.WebRequestController,
                    webBrowser,
                    dynamicWorldDependencies.Web3Authenticator,
                    container.UserInAppInAppInitializationFlow,
                    profileCache, sidebarBus, chatEntryConfiguration,
                    globalWorld, playerEntity),
                new ErrorPopupPlugin(container.MvcManager, assetsProvisioner),
                connectionStatusPanelPlugin,
                new MinimapPlugin(container.MvcManager, container.MapRendererContainer, placesAPIService, staticContainer.RealmData, container.ChatMessagesBus, realmNavigator, staticContainer.ScenesCache, mainUIView, mapPathEventBus, sceneRestrictionBusController),
                new ChatPlugin(assetsProvisioner, container.MvcManager, container.ChatMessagesBus, chatHistory, entityParticipantTable, nametagsData, dclInput, unityEventSystem, mainUIView, staticContainer.InputBlock, globalWorld, playerEntity),
                new ExplorePanelPlugin(
                    assetsProvisioner,
                    container.MvcManager,
                    container.MapRendererContainer,
                    placesAPIService,
                    staticContainer.WebRequestsContainer.WebRequestController,
                    identityCache,
                    wearableCatalog,
                    characterPreviewFactory,
                    container.ProfileRepository,
                    dynamicWorldDependencies.Web3Authenticator,
                    container.UserInAppInAppInitializationFlow,
                    selfProfile,
                    equippedWearables,
                    equippedEmotes,
                    webBrowser,
                    emotesCache,
                    forceRender,
                    dclInput,
                    staticContainer.RealmData,
                    profileCache,
                    assetBundlesURL,
                    notificationsBusController,
                    characterPreviewEventBus,
                    mapPathEventBus,
                    chatEntryConfiguration,
                    backpackEventBus,
                    thirdPartyNftProviderSource,
                    container.wearablesProvider,
                    dclCursor,
                    staticContainer.InputBlock,
                    emoteProvider,
                    globalWorld,
                    playerEntity,
                    container.ChatMessagesBus,
                    staticContainer.MemoryCap,
                    bootstrapContainer.WorldVolumeMacBus,
                    eventsApiService,
                    userCalendar
                ),
                new CharacterPreviewPlugin(staticContainer.ComponentsContainer.ComponentPoolsRegistry, assetsProvisioner, staticContainer.CacheCleaner),
                new WebRequestsPlugin(staticContainer.WebRequestsContainer.AnalyticsContainer, debugBuilder),
                new Web3AuthenticationPlugin(assetsProvisioner, dynamicWorldDependencies.Web3Authenticator, debugBuilder, container.MvcManager, selfProfile, webBrowser, staticContainer.RealmData, identityCache, characterPreviewFactory, dynamicWorldDependencies.SplashScreen, audioMixerVolumesController, staticContainer.FeatureFlagsCache, characterPreviewEventBus, globalWorld),
                new StylizedSkyboxPlugin(assetsProvisioner, dynamicSettings.DirectionalLight, debugBuilder, staticContainer.FeatureFlagsCache),
                new LoadingScreenPlugin(assetsProvisioner, container.MvcManager, audioMixerVolumesController,
                    staticContainer.InputBlock, debugBuilder, staticContainer.LoadingStatus),
                new ExternalUrlPromptPlugin(assetsProvisioner, webBrowser, container.MvcManager, dclCursor),
                new TeleportPromptPlugin(assetsProvisioner, container.MvcManager,
                    staticContainer.WebRequestsContainer.WebRequestController, placesAPIService, dclCursor,
                    container.ChatMessagesBus),
                new ChangeRealmPromptPlugin(
                    assetsProvisioner,
                    container.MvcManager,
                    dclCursor,
                    realmUrl => container.RealmController.SetRealmAsync(URLDomain.FromString(realmUrl), CancellationToken.None).Forget()),
                new NftPromptPlugin(assetsProvisioner, webBrowser, container.MvcManager, nftInfoAPIClient, staticContainer.WebRequestsContainer.WebRequestController, dclCursor),
                staticContainer.CharacterContainer.CreateGlobalPlugin(),
                staticContainer.QualityContainer.CreatePlugin(),
                landscapePlugin,
                new MultiplayerMovementPlugin(
                    assetsProvisioner,
                    container.multiplayerMovementMessageBus,
                    debugBuilder,
                    remoteEntities,
                    staticContainer.CharacterContainer.Transform,
                    multiplayerDebugSettings,
                    appArgs,
                    entityParticipantTable,
                    staticContainer.RealmData,
                    container.RemoteMetadata,
                    staticContainer.FeatureFlagsCache),
                container.LODContainer.LODPlugin,
                container.LODContainer.RoadPlugin,
                new AudioPlaybackPlugin(genesisTerrain, assetsProvisioner, dynamicWorldParams.EnableLandscape),
                new RealmDataDirtyFlagPlugin(staticContainer.RealmData),
                new NotificationPlugin(
                    assetsProvisioner,
                    container.MvcManager,
                    staticContainer.WebRequestsContainer.WebRequestController,
                    notificationsBusController),
                new RewardPanelPlugin(container.MvcManager, assetsProvisioner, notificationsBusController, staticContainer.WebRequestsContainer.WebRequestController),
                new PassportPlugin(
                    assetsProvisioner,
                    container.MvcManager,
                    dclCursor,
                    container.ProfileRepository,
                    characterPreviewFactory,
                    chatEntryConfiguration,
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
                    container.RemoteMetadata,
                    globalWorld,
                    playerEntity
                ),
            };

            globalPlugins.AddRange(staticContainer.SharedPlugins);

            if (dynamicWorldParams.EnableAnalytics)
                globalPlugins.Add(new AnalyticsPlugin(
                        bootstrapContainer.Analytics!,
                        staticContainer.Profiler,
                        realmNavigator,
                        staticContainer.RealmData,
                        staticContainer.ScenesCache,
                        staticContainer.MainPlayerAvatarBaseProxy,
                        identityCache,
                        debugBuilder
                    )
                );

            container.GlobalWorldFactory = new GlobalWorldFactory(
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
                container.LODContainer.LodCache,
                multiplayerEmotesMessageBus,
                globalWorld,
                localSceneDevelopment
            );

            container.GlobalPlugins = globalPlugins;

            staticContainer.RoomHubProxy.SetObject(container.RoomHub);
            return (container, true);
        }

        private static void ParseDebugForcedEmotes(IReadOnlyList<string>? debugEmotes, ref List<URN> parsedEmotes)
        {
            if (debugEmotes?.Count > 0)
                parsedEmotes.AddRange(debugEmotes.Select(emote => new URN(emote)));
        }

        private static void ParseParamsForcedEmotes(IAppArgs appParams, ref List<URN> parsedEmotes)
        {
            if (appParams.TryGetValue(PARAMS_FORCED_EMOTES_FLAG, out string? csv) && !string.IsNullOrEmpty(csv))
                parsedEmotes.AddRange(csv.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(emote => new URN(emote)));
        }
    }
}
