using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Audio;
using DCL.AvatarRendering.Emotes;
using DCL.AvatarRendering.Emotes.Equipped;
using DCL.AvatarRendering.Wearables;
using DCL.AvatarRendering.Wearables.Equipped;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Browser;
using DCL.CharacterPreview;
using DCL.Chat;
using DCL.Chat.Commands;
using DCL.Chat.History;
using DCL.Chat.MessageBus;
using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using DCL.ExplorePanel;
using DCL.Input;
using DCL.Input.UnityInputSystem.Blocks;
using DCL.Landscape;
using DCL.LOD.Systems;
using DCL.Multiplayer.Connections.Archipelago.AdapterAddress.Current;
using DCL.Multiplayer.Connections.Archipelago.Rooms;
using DCL.Multiplayer.Connections.GateKeeper.Meta;
using DCL.Multiplayer.Connections.GateKeeper.Rooms;
using DCL.Multiplayer.Connections.Messaging.Hubs;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Multiplayer.Deduplication;
using DCL.Multiplayer.Emotes;
using DCL.Multiplayer.Movement;
using DCL.Multiplayer.Movement.Systems;
using DCL.Multiplayer.Profiles.BroadcastProfiles;
using DCL.Multiplayer.Profiles.Entities;
using DCL.Multiplayer.Profiles.Poses;
using DCL.Multiplayer.Profiles.Tables;
using DCL.Nametags;
using DCL.NftInfoAPIService;
using DCL.Notification.NotificationsBus;
using DCL.ParcelsService;
using DCL.PerformanceAndDiagnostics.Analytics;
using DCL.PlacesAPIService;
using DCL.PluginSystem;
using DCL.PluginSystem.Global;
using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.SceneLoadingScreens.LoadingScreen;
using DCL.UI.MainUI;
using DCL.UserInAppInitializationFlow;
using DCL.Utilities.Extensions;
using DCL.Web3.Identities;
using DCL.WebRequests.Analytics;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.Realm;
using Global.Dynamic.ChatCommands;
using LiveKit.Internal.FFIClients.Pools;
using LiveKit.Internal.FFIClients.Pools.Memory;
using MVC;
using MVC.PopupsController.PopupCloser;
using SceneRunner.Debugging.Hub;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using ECS.SceneLifeCycle.Systems;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Pool;
using Utility.PriorityQueue;
using Object = UnityEngine.Object;

namespace Global.Dynamic
{
    public class DynamicWorldContainer : DCLWorldContainer<DynamicWorldSettings>
    {
        private static readonly URLDomain ASSET_BUNDLES_URL = URLDomain.FromString("https://ab-cdn.decentraland.org/");

        public IMVCManager MvcManager { get; private set; } = null!;

        public DefaultTexturesContainer DefaultTexturesContainer { get; private set; } = null!;

        public LODContainer LODContainer { get; private set; } = null!;

        public IRealmController RealmController { get; private set; } = null!;

        public GlobalWorldFactory GlobalWorldFactory { get; private set; } = null!;

        public IReadOnlyList<IDCLGlobalPlugin> GlobalPlugins { get; private set; } = null!;

        public IProfileRepository ProfileRepository { get; private set; } = null!;

        public ParcelServiceContainer ParcelServiceContainer { get; private set; } = null!;

        public RealUserInitializationFlowController UserInAppInitializationFlow { get; private set; } = null!;

        // TODO move multiplayer related dependencies to a separate container
        public IChatMessagesBus ChatMessagesBus { get; private set; } = null!;

        public IMessagePipesHub MessagePipesHub { get; private set; } = null!;

        public IProfileBroadcast ProfileBroadcast { get; private set; } = null!;

        public IRoomHub RoomHub { get; private set; } = null!;

        public RealFlowLoadingStatus RealFlowLoadingStatus { get; private set; } = null!;

        public override void Dispose()
        {
            MvcManager.Dispose();
            ChatMessagesBus.Dispose();
            ProfileBroadcast.Dispose();
            MessagePipesHub.Dispose();
        }

        private static void BuildTeleportWidget(IRealmNavigator realmNavigator, IDebugContainerBuilder debugContainerBuilder, List<string> realms)
        {
            debugContainerBuilder.AddWidget("Realm")
                                 .AddControl(new DebugDropdownDef(realms, new ElementBinding<string>(string.Empty,
                                      evt => { realmNavigator.TryChangeRealmAsync(URLDomain.FromString(evt.newValue), CancellationToken.None).Forget(); }), string.Empty), null)
                                 .AddStringFieldWithConfirmation("https://peer.decentraland.org", "Change", realm => { realmNavigator.TryChangeRealmAsync(URLDomain.FromString(realm), CancellationToken.None).Forget(); });
        }

        public static async UniTask<(DynamicWorldContainer? container, bool success)> CreateAsync(BootstrapContainer bootstrapContainer,
            DynamicWorldDependencies dynamicWorldDependencies,
            DynamicWorldParams dynamicWorldParams,
            AudioClipConfig backgroundMusic, CancellationToken ct)
        {
            var container = new DynamicWorldContainer();
            DynamicSettings dynamicSettings = dynamicWorldDependencies.DynamicSettings;
            StaticContainer staticContainer = dynamicWorldDependencies.StaticContainer;
            IWeb3IdentityCache identityCache = dynamicWorldDependencies.Web3IdentityCache;
            IAssetsProvisioner assetsProvisioner = dynamicWorldDependencies.AssetsProvisioner;
            IDebugContainerBuilder debugBuilder = dynamicWorldDependencies.DebugContainerBuilder;

            async UniTask InitializeContainersAsync(IPluginSettingsContainer settingsContainer, CancellationToken ct)
            {
                // Init itself
                await settingsContainer.InitializePluginAsync(container, ct)!.ThrowOnFail();

                // Init other containers
                container.DefaultTexturesContainer = await DefaultTexturesContainer.CreateAsync(settingsContainer, assetsProvisioner, ct).ThrowOnFail();
                container.LODContainer = await LODContainer.CreateAsync(assetsProvisioner, staticContainer, settingsContainer, staticContainer.RealmData, container.DefaultTexturesContainer.TextureArrayContainerFactory, debugBuilder, dynamicWorldParams.EnableLOD, ct).ThrowOnFail();
            }

            try { await InitializeContainersAsync(dynamicWorldDependencies.SettingsContainer, ct); }
            catch (Exception) { return (null, false); }

            CursorSettings cursorSettings = (await assetsProvisioner.ProvideMainAssetAsync(dynamicSettings.CursorSettings, ct)).Value;
            ProvidedAsset<Texture2D> normalCursorAsset = await assetsProvisioner.ProvideMainAssetAsync(cursorSettings.NormalCursor, ct);
            ProvidedAsset<Texture2D> interactionCursorAsset = await assetsProvisioner.ProvideMainAssetAsync(cursorSettings.InteractionCursor, ct);

            var unityEventSystem = new UnityEventSystem(EventSystem.current.EnsureNotNull());
            var dclCursor = new DCLCursor(normalCursorAsset.Value, interactionCursorAsset.Value);

            staticContainer.QualityContainer.AddDebugViews(debugBuilder);

            var realmSamplingData = new RealmSamplingData();
            var dclInput = new DCLInput();
            staticContainer.InputProxy.SetObject(dclInput);

            ExposedGlobalDataContainer exposedGlobalDataContainer = staticContainer.ExposedGlobalDataContainer;

            PopupCloserView popupCloserView = Object.Instantiate((await assetsProvisioner.ProvideMainAssetAsync(dynamicSettings.PopupCloserView, ct: CancellationToken.None)).Value.GetComponent<PopupCloserView>());

            MainUIContainer mainUIContainer = Object.Instantiate((await assetsProvisioner.ProvideMainAssetAsync(dynamicSettings.MainUIView, ct: CancellationToken.None)).Value.GetComponent<MainUIContainer>());

            var coreMvcManager = new MVCManager(new WindowStackManager(), new CancellationTokenSource(), popupCloserView);

            container.MvcManager = dynamicWorldParams.EnableAnalytics
                ? new MVCManagerAnalyticsDecorator(coreMvcManager, bootstrapContainer.Analytics!)
                : coreMvcManager;

            var parcelServiceContainer = ParcelServiceContainer.Create(staticContainer.RealmData, staticContainer.SceneReadinessReportQueue, debugBuilder, container.MvcManager, staticContainer.SingletonSharedDependencies.SceneAssetLock);
            container.ParcelServiceContainer = parcelServiceContainer;

            var placesAPIService = new PlacesAPIService(new PlacesAPIClient(staticContainer.WebRequestsContainer.WebRequestController));
            MapRendererContainer mapRendererContainer = await MapRendererContainer.CreateAsync(staticContainer, assetsProvisioner, dynamicSettings.MapRendererSettings, placesAPIService, ct);
            var nftInfoAPIClient = new OpenSeaAPIClient(staticContainer.WebRequestsContainer.WebRequestController);
            var wearableCatalog = new WearableCatalog();
            var characterPreviewFactory = new CharacterPreviewFactory(staticContainer.ComponentsContainer.ComponentPoolsRegistry);
            var webBrowser = new UnityAppWebBrowser();
            ChatEntryConfigurationSO chatEntryConfiguration = (await assetsProvisioner.ProvideMainAssetAsync(dynamicSettings.ChatEntryConfiguration, ct)).Value;
            NametagsData nametagsData = (await assetsProvisioner.ProvideMainAssetAsync(dynamicSettings.NametagsData, ct)).Value;

            IProfileCache profileCache = new DefaultProfileCache();

            container.ProfileRepository = new LogProfileRepository(
                new RealmProfileRepository(staticContainer.WebRequestsContainer.WebRequestController, staticContainer.RealmData, profileCache)
            );

            var genesisTerrain = new TerrainGenerator();
            var worldsTerrain = new WorldTerrainGenerator();
            var satelliteView = new SatelliteFloor();
            var landscapePlugin = new LandscapePlugin(satelliteView, genesisTerrain, worldsTerrain, assetsProvisioner, debugBuilder, mapRendererContainer.TextureContainer, staticContainer.WebRequestsContainer.WebRequestController, dynamicWorldParams.EnableLandscape);

            var multiPool = new ThreadSafeMultiPool();
            var memoryPool = new ArrayMemoryPool(ArrayPool<byte>.Shared!);
            container.RealFlowLoadingStatus = new RealFlowLoadingStatus();

            var emotesCache = new MemoryEmotesCache();
            staticContainer.CacheCleaner.Register(emotesCache);
            var equippedWearables = new EquippedWearables();
            var equippedEmotes = new EquippedEmotes();
            var forceRender = new List<string>();
            var selfProfile = new SelfProfile(container.ProfileRepository, identityCache, equippedWearables, wearableCatalog, emotesCache, equippedEmotes, forceRender);

            var metaDataSource = new LogMetaDataSource(new MetaDataSource(staticContainer.RealmData, staticContainer.CharacterContainer.CharacterObject, placesAPIService));
            var gateKeeperSceneRoom = new GateKeeperSceneRoom(staticContainer.WebRequestsContainer.WebRequestController, metaDataSource);

            var currentAdapterAddress = ICurrentAdapterAddress.NewDefault(staticContainer.WebRequestsContainer.WebRequestController, staticContainer.RealmData);

            var archipelagoIslandRoom = IArchipelagoIslandRoom.NewDefault(
                identityCache,
                multiPool,
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
                staticContainer.SingletonSharedDependencies.SceneAssetLock);

            container.RoomHub = new RoomHub(archipelagoIslandRoom, gateKeeperSceneRoom);
            container.MessagePipesHub = new MessagePipesHub(container.RoomHub, multiPool, memoryPool);

            var entityParticipantTable = new EntityParticipantTable();

            var queuePoolFullMovementMessage = new ObjectPool<SimplePriorityQueue<NetworkMovementMessage>>(
                () => new SimplePriorityQueue<NetworkMovementMessage>(),
                actionOnRelease: x => x.Clear()
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
                mapRendererContainer.MapRenderer,
                container.RealmController,
                parcelServiceContainer.TeleportController,
                container.RoomHub,
                remoteEntities,
                staticContainer.GlobalWorldProxy,
                container.LODContainer.RoadPlugin,
                genesisTerrain,
                worldsTerrain,
                satelliteView,
                dynamicWorldParams.EnableLandscape,
                staticContainer.ExposedGlobalDataContainer.ExposedCameraData.CameraEntityProxy,
                exposedGlobalDataContainer.CameraSamplingData
            );

            container.UserInAppInitializationFlow = new RealUserInitializationFlowController(
                container.RealFlowLoadingStatus,
                container.MvcManager,
                selfProfile,
                dynamicWorldParams.StartParcel,
                staticContainer.MainPlayerAvatarBaseProxy,
                backgroundMusic,
                realmNavigator,
                loadingScreen,
                staticContainer.FeatureFlagsProvider,
                identityCache
            );

            var worldInfoHub = new LocationBasedWorldInfoHub(
                new WorldInfoHub(staticContainer.SingletonSharedDependencies.SceneMapping),
                staticContainer.CharacterContainer.CharacterObject
            );

            dynamicWorldDependencies.WorldInfoTool.Initialize(worldInfoHub);

            var chatHistory = new ChatHistory();
            var reloadSceneController = new ReloadSceneController();

            var chatCommandsFactory = new Dictionary<Regex, Func<IChatCommand>>
            {
                {
                    GoToChatCommand.REGEX, () => new GoToChatCommand(realmNavigator)
                },
                { ChangeRealmChatCommand.REGEX, () => new ChangeRealmChatCommand(realmNavigator) },
                { DebugPanelChatCommand.REGEX, () => new DebugPanelChatCommand(debugBuilder) },
                { ShowEntityInfoChatCommand.REGEX, () => new ShowEntityInfoChatCommand(worldInfoHub) },
                { ClearChatCommand.REGEX, () => new ClearChatCommand(chatHistory) },
                { ReloadSceneChatCommand.REGEX, () => new ReloadSceneChatCommand(reloadSceneController) },
            };

            IChatMessagesBus chatMessageBus = new MultiplayerChatMessagesBus(container.MessagePipesHub, container.ProfileRepository, new MessageDeduplication<double>())
                                             .WithSelfResend(identityCache, container.ProfileRepository)
                                             .WithIgnoreSymbols()
                                             .WithCommands(chatCommandsFactory)
                                             .WithDebugPanel(debugBuilder);

            container.ChatMessagesBus = dynamicWorldParams.EnableAnalytics ? new ChatMessagesBusAnalyticsDecorator(chatMessageBus, bootstrapContainer.Analytics!) : chatMessageBus;

            reloadSceneController.InitializeChatMessageBus(container.ChatMessagesBus);

            container.ProfileBroadcast = new DebounceProfileBroadcast(
                new EnsureSelfPublishedProfileBroadcast(
                    new ProfileBroadcast(container.MessagePipesHub, selfProfile),
                    selfProfile,
                    staticContainer.RealmData
                )
            );

            INotificationsBusController notificationsBusController = new NotificationsBusController();

            var multiplayerEmotesMessageBus = new MultiplayerEmotesMessageBus(container.MessagePipesHub);

            var remotePoses = new DebounceRemotePoses(new RemotePoses(container.RoomHub));

            var characterPreviewEventBus = new CharacterPreviewEventBus();

            var globalPlugins = new List<IDCLGlobalPlugin>
            {
                new MultiplayerPlugin(
                    assetsProvisioner,
                    archipelagoIslandRoom,
                    gateKeeperSceneRoom,
                    container.RoomHub,
                    container.ProfileRepository,
                    container.ProfileBroadcast,
                    debugBuilder,
                    container.RealFlowLoadingStatus,
                    entityParticipantTable,
                    container.MessagePipesHub,
                    remotePoses,
                    staticContainer.CharacterContainer.CharacterObject,
                    staticContainer.RealmData,
                    remoteEntities,
                    staticContainer.ScenesCache,
                    emotesCache
                ),
                new WorldInfoPlugin(worldInfoHub, debugBuilder, chatHistory),
                new CharacterMotionPlugin(assetsProvisioner, staticContainer.CharacterContainer.CharacterObject, debugBuilder, staticContainer.ComponentsContainer.ComponentPoolsRegistry),
                new InputPlugin(dclInput, dclCursor, unityEventSystem, assetsProvisioner, dynamicWorldDependencies.CursorUIDocument, multiplayerEmotesMessageBus, container.MvcManager, debugBuilder, dynamicWorldDependencies.RootUIDocument, dynamicWorldDependencies.CursorUIDocument),
                new GlobalInteractionPlugin(dclInput, dynamicWorldDependencies.RootUIDocument, assetsProvisioner, staticContainer.EntityCollidersGlobalCache, exposedGlobalDataContainer.GlobalInputEvents, dclCursor, unityEventSystem, container.MvcManager),
                new CharacterCameraPlugin(assetsProvisioner, realmSamplingData, exposedGlobalDataContainer.ExposedCameraData, debugBuilder, dclInput),
                new WearablePlugin(assetsProvisioner, staticContainer.WebRequestsContainer.WebRequestController, staticContainer.RealmData, ASSET_BUNDLES_URL, staticContainer.CacheCleaner, wearableCatalog),
                new EmotePlugin(staticContainer.WebRequestsContainer.WebRequestController, emotesCache, staticContainer.RealmData, multiplayerEmotesMessageBus, debugBuilder, assetsProvisioner, selfProfile, container.MvcManager, dclInput, staticContainer.CacheCleaner, identityCache, entityParticipantTable, ASSET_BUNDLES_URL, mainUIContainer),
                new ProfilingPlugin(staticContainer.ProfilingProvider, staticContainer.SingletonSharedDependencies.FrameTimeBudget, staticContainer.SingletonSharedDependencies.MemoryBudget, debugBuilder),
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
                    entityParticipantTable,
                    nametagsData,
                    container.DefaultTexturesContainer.TextureArrayContainerFactory,
                    wearableCatalog
                ),
                new ProfilePlugin(container.ProfileRepository, profileCache, staticContainer.CacheCleaner, new ProfileIntentionCache()),
                new MapRendererPlugin(mapRendererContainer.MapRenderer),
                new SidebarPlugin(assetsProvisioner, container.MvcManager, mainUIContainer, notificationsBusController, identityCache, container.ProfileRepository, staticContainer.WebRequestsContainer.WebRequestController,
                    webBrowser, dynamicWorldDependencies.Web3Authenticator, container.UserInAppInitializationFlow,profileCache),
                new MinimapPlugin(container.MvcManager, mapRendererContainer, placesAPIService, staticContainer.RealmData, container.ChatMessagesBus, realmNavigator, staticContainer.ScenesCache, mainUIContainer),
                new ChatPlugin(assetsProvisioner, container.MvcManager, container.ChatMessagesBus, chatHistory, entityParticipantTable, nametagsData, dclInput, unityEventSystem, mainUIContainer, staticContainer.InputBlock),
                new ExplorePanelPlugin(
                    assetsProvisioner,
                    container.MvcManager,
                    mapRendererContainer,
                    placesAPIService,
                    staticContainer.WebRequestsContainer.WebRequestController,
                    identityCache,
                    wearableCatalog,
                    characterPreviewFactory,
                    container.ProfileRepository,
                    dynamicWorldDependencies.Web3Authenticator,
                    container.UserInAppInitializationFlow,
                    selfProfile,
                    equippedWearables,
                    equippedEmotes,
                    webBrowser,
                    emotesCache,
                    realmNavigator,
                    forceRender,
                    dclInput,
                    staticContainer.RealmData,
                    profileCache,
                    ASSET_BUNDLES_URL,
                    notificationsBusController,
                    characterPreviewEventBus
                ),
                new CharacterPreviewPlugin(staticContainer.ComponentsContainer.ComponentPoolsRegistry, assetsProvisioner, staticContainer.CacheCleaner),
                new WebRequestsPlugin(staticContainer.WebRequestsContainer.AnalyticsContainer, debugBuilder),
                new Web3AuthenticationPlugin(assetsProvisioner, dynamicWorldDependencies.Web3Authenticator, debugBuilder, container.MvcManager, selfProfile, webBrowser, staticContainer.RealmData, identityCache, characterPreviewFactory, dynamicWorldDependencies.SplashAnimator, characterPreviewEventBus, staticContainer.FeatureFlagsCache),
                new StylizedSkyboxPlugin(assetsProvisioner, dynamicSettings.DirectionalLight, debugBuilder),
                new LoadingScreenPlugin(assetsProvisioner, container.MvcManager),
                new ExternalUrlPromptPlugin(assetsProvisioner, webBrowser, container.MvcManager, dclCursor), new TeleportPromptPlugin(assetsProvisioner, realmNavigator, container.MvcManager, staticContainer.WebRequestsContainer.WebRequestController, placesAPIService, dclCursor),
                new ChangeRealmPromptPlugin(
                    assetsProvisioner,
                    container.MvcManager,
                    dclCursor,
                    realmUrl => container.RealmController.SetRealmAsync(URLDomain.FromString(realmUrl), CancellationToken.None).Forget()),
                new NftPromptPlugin(assetsProvisioner, webBrowser, container.MvcManager, nftInfoAPIClient, staticContainer.WebRequestsContainer.WebRequestController, dclCursor),
                staticContainer.CharacterContainer.CreateGlobalPlugin(),
                staticContainer.QualityContainer.CreatePlugin(),
                landscapePlugin,
                new MultiplayerMovementPlugin(assetsProvisioner, new MultiplayerMovementMessageBus(container.MessagePipesHub, entityParticipantTable)),
                container.LODContainer.LODPlugin,
                container.LODContainer.RoadPlugin,
                new AudioPlaybackPlugin(genesisTerrain, assetsProvisioner, dynamicWorldParams.EnableLandscape),
                new RealmDataDirtyFlagPlugin(staticContainer.RealmData),
                new NotificationPlugin(assetsProvisioner, container.MvcManager, staticContainer.WebRequestsContainer.WebRequestController, identityCache, notificationsBusController),
                new RewardPanelPlugin(container.MvcManager, assetsProvisioner, notificationsBusController, staticContainer.WebRequestsContainer.WebRequestController),
                new PassportPlugin(
                    assetsProvisioner,
                    container.MvcManager,
                    dclCursor,
                    container.ProfileRepository,
                    characterPreviewFactory,
                    chatEntryConfiguration,
                    staticContainer.RealmData,
                    ASSET_BUNDLES_URL,
                    staticContainer.WebRequestsContainer.WebRequestController,
                    characterPreviewEventBus,
                    selfProfile,
                    dclInput,
                    webBrowser),
            };

            globalPlugins.AddRange(staticContainer.SharedPlugins);

            if (dynamicWorldParams.EnableAnalytics)
                globalPlugins.Add(new AnalyticsPlugin(
                        bootstrapContainer.Analytics!,
                        staticContainer.ProfilingProvider,
                        staticContainer.RealmData,
                        staticContainer.ScenesCache
                    )
                );

            container.GlobalWorldFactory = new GlobalWorldFactory(
                in staticContainer,
                exposedGlobalDataContainer.CameraSamplingData,
                realmSamplingData,
                ASSET_BUNDLES_URL,
                staticContainer.RealmData,
                globalPlugins,
                debugBuilder,
                staticContainer.ScenesCache,
                dynamicWorldParams.HybridSceneParams,
                reloadSceneController);

            container.GlobalPlugins = globalPlugins;

            staticContainer.RoomHubProxy.SetObject(container.RoomHub);

            BuildTeleportWidget(realmNavigator, debugBuilder, dynamicWorldParams.Realms);

            return (container, true);
        }
    }
}
