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
using DCL.Chat.MessageBus;
using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using DCL.Input;
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
using DCL.ParcelsService;
using DCL.PlacesAPIService;
using DCL.PluginSystem;
using DCL.PluginSystem.Global;
using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.SceneLoadingScreens.LoadingScreen;
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
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Pool;
using Utility.PriorityQueue;
using Object = UnityEngine.Object;

namespace Global.Dynamic
{
    public class DynamicWorldContainer : IDCLPlugin<DynamicWorldSettings>
    {
        private static readonly URLDomain ASSET_BUNDLES_URL = URLDomain.FromString("https://ab-cdn.decentraland.org/");

        public MVCManager MvcManager { get; private set; } = null!;

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

        public void Dispose()
        {
            MvcManager.Dispose();
            ChatMessagesBus.Dispose();
            ProfileBroadcast.Dispose();
            MessagePipesHub.Dispose();
        }

        public UniTask InitializeAsync(DynamicWorldSettings settings, CancellationToken ct) =>
            UniTask.CompletedTask;

        private static void BuildTeleportWidget(IRealmNavigator realmNavigator, IDebugContainerBuilder debugContainerBuilder, List<string> realms)
        {
            debugContainerBuilder.AddWidget("Realm")
                                 .AddControl(new DebugDropdownDef(realms, new ElementBinding<string>(string.Empty,
                                      evt => { realmNavigator.TryChangeRealmAsync(URLDomain.FromString(evt.newValue), CancellationToken.None).Forget(); }), string.Empty), null)
                                 .AddStringFieldWithConfirmation("https://peer.decentraland.org", "Change", realm => { realmNavigator.TryChangeRealmAsync(URLDomain.FromString(realm), CancellationToken.None).Forget(); });
        }

        public static async UniTask<(DynamicWorldContainer? container, bool success)> CreateAsync(
            DynamicWorldDependencies dynamicWorldDependencies,
            DynamicWorldParams dynamicWorldParams,
            AudioClipConfig backgroundMusic,
            CancellationToken ct)
        {
            var container = new DynamicWorldContainer();
            DynamicSettings dynamicSettings = dynamicWorldDependencies.DynamicSettings;
            StaticContainer staticContainer = dynamicWorldDependencies.StaticContainer;
            IWeb3IdentityCache identityCache = dynamicWorldDependencies.Web3IdentityCache;
            var debugBuilder = dynamicWorldDependencies.DebugContainerBuilder;

            async UniTask InitializeContainersAsync(IPluginSettingsContainer settingsContainer, CancellationToken ct)
            {
                // Init itself
                await settingsContainer.InitializePluginAsync(container, ct)!.ThrowOnFail();

                // Init other containers
                container.DefaultTexturesContainer = await DefaultTexturesContainer.CreateAsync(settingsContainer, staticContainer.AssetsProvisioner, ct).ThrowOnFail();
                container.LODContainer = await LODContainer.CreateAsync(staticContainer, settingsContainer, staticContainer.RealmData, container.DefaultTexturesContainer.TextureArrayContainerFactory, debugBuilder, dynamicWorldParams.EnableLOD, ct).ThrowOnFail();
            }

            try { await InitializeContainersAsync(dynamicWorldDependencies.SettingsContainer, ct); }
            catch (Exception) { return (null, false); }

            CursorSettings cursorSettings = (await staticContainer.AssetsProvisioner.ProvideMainAssetAsync(dynamicSettings.CursorSettings, ct)).Value;
            ProvidedAsset<Texture2D> normalCursorAsset = await staticContainer.AssetsProvisioner.ProvideMainAssetAsync(cursorSettings.NormalCursor, ct);
            ProvidedAsset<Texture2D> interactionCursorAsset = await staticContainer.AssetsProvisioner.ProvideMainAssetAsync(cursorSettings.InteractionCursor, ct);

            var unityEventSystem = new UnityEventSystem(EventSystem.current.EnsureNotNull());
            var dclCursor = new DCLCursor(normalCursorAsset.Value, interactionCursorAsset.Value);

            staticContainer.QualityContainer.AddDebugViews(debugBuilder);

            var realmSamplingData = new RealmSamplingData();
            var dclInput = new DCLInput();
            ExposedGlobalDataContainer exposedGlobalDataContainer = staticContainer.ExposedGlobalDataContainer;

            PopupCloserView popupCloserView = Object.Instantiate((await staticContainer.AssetsProvisioner.ProvideMainAssetAsync(dynamicSettings.PopupCloserView, ct: CancellationToken.None)).Value.GetComponent<PopupCloserView>());
            container.MvcManager = new MVCManager(new WindowStackManager(), new CancellationTokenSource(), popupCloserView);

            var parcelServiceContainer = ParcelServiceContainer.Create(staticContainer.RealmData, staticContainer.SceneReadinessReportQueue, debugBuilder, container.MvcManager);
            container.ParcelServiceContainer = parcelServiceContainer;

            var placesAPIService = new PlacesAPIService(new PlacesAPIClient(staticContainer.WebRequestsContainer.WebRequestController));
            MapRendererContainer mapRendererContainer = await MapRendererContainer.CreateAsync(staticContainer, dynamicSettings.MapRendererSettings, placesAPIService, ct);
            var nftInfoAPIClient = new OpenSeaAPIClient(staticContainer.WebRequestsContainer.WebRequestController);
            var wearableCatalog = new WearableCatalog();
            var characterPreviewFactory = new CharacterPreviewFactory(staticContainer.ComponentsContainer.ComponentPoolsRegistry);
            var webBrowser = new UnityAppWebBrowser();
            ChatEntryConfigurationSO chatEntryConfiguration = (await staticContainer.AssetsProvisioner.ProvideMainAssetAsync(dynamicSettings.ChatEntryConfiguration, ct)).Value;
            NametagsData nametagsData = (await staticContainer.AssetsProvisioner.ProvideMainAssetAsync(dynamicSettings.NametagsData, ct)).Value;

            IProfileCache profileCache = new DefaultProfileCache();

            container.ProfileRepository = new LogProfileRepository(
                new RealmProfileRepository(staticContainer.WebRequestsContainer.WebRequestController, staticContainer.RealmData, profileCache)
            );

            var genesisTerrain = new TerrainGenerator();
            var worldsTerrain = new WorldTerrainGenerator();
            var satelliteView = new SatelliteFloor();
            var landscapePlugin = new LandscapePlugin(satelliteView, genesisTerrain, worldsTerrain, staticContainer.AssetsProvisioner, debugBuilder, mapRendererContainer.TextureContainer, dynamicWorldParams.EnableLandscape);

            var multiPool = new ThreadSafeMultiPool();
            var memoryPool = new ArrayMemoryPool(ArrayPool<byte>.Shared!);
            var realFlowLoadingStatus = new RealFlowLoadingStatus();

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
                staticContainer.PartitionDataContainer);

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
                queuePoolFullMovementMessage
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
                realFlowLoadingStatus,
                container.MvcManager,
                selfProfile,
                dynamicWorldParams.StartParcel,
                staticContainer.MainPlayerAvatarBaseProxy,
                backgroundMusic,
                realmNavigator,
                loadingScreen
            );

            var chatCommandsFactory = new Dictionary<Regex, Func<IChatCommand>>
            {
                { TeleportToChatCommand.REGEX, () => new TeleportToChatCommand(realmNavigator) },
                { ChangeRealmChatCommand.REGEX, () => new ChangeRealmChatCommand(realmNavigator) },
                { DebugPanelChatCommand.REGEX, () => new DebugPanelChatCommand(debugBuilder) },
            };

            container.ChatMessagesBus = new MultiplayerChatMessagesBus(container.MessagePipesHub, container.ProfileRepository, new MessageDeduplication<double>())
                                       .WithSelfResend(identityCache, container.ProfileRepository)
                                       .WithIgnoreSymbols()
                                       .WithCommands(chatCommandsFactory)
                                       .WithDebugPanel(debugBuilder);

            container.ProfileBroadcast = new DebounceProfileBroadcast(
                new EnsureSelfPublishedProfileBroadcast(
                    new ProfileBroadcast(container.MessagePipesHub, selfProfile),
                    selfProfile,
                    staticContainer.RealmData
                )
            );

            var multiplayerEmotesMessageBus = new MultiplayerEmotesMessageBus(container.MessagePipesHub);

            var remotePoses = new DebounceRemotePoses(
                new RemotePoses(container.RoomHub)
            );

            var globalPlugins = new List<IDCLGlobalPlugin>
            {
                new MultiplayerPlugin(
                    archipelagoIslandRoom,
                    gateKeeperSceneRoom,
                    container.RoomHub,
                    container.ProfileRepository,
                    container.ProfileBroadcast,
                    debugBuilder,
                    realFlowLoadingStatus,
                    entityParticipantTable,
                    container.MessagePipesHub,
                    remotePoses,
                    staticContainer.CharacterContainer.CharacterObject,
                    staticContainer.RealmData,
                    remoteEntities,
                    staticContainer.ScenesCache,
                    emotesCache
                ),
                new CharacterMotionPlugin(staticContainer.AssetsProvisioner, staticContainer.CharacterContainer.CharacterObject, debugBuilder, staticContainer.ComponentsContainer.ComponentPoolsRegistry),
                new InputPlugin(dclInput, dclCursor, unityEventSystem, staticContainer.AssetsProvisioner, dynamicWorldDependencies.CursorUIDocument, multiplayerEmotesMessageBus, container.MvcManager, debugBuilder, dynamicWorldDependencies.RootUIDocument, dynamicWorldDependencies.CursorUIDocument),
                new GlobalInteractionPlugin(dclInput, dynamicWorldDependencies.RootUIDocument, staticContainer.AssetsProvisioner, staticContainer.EntityCollidersGlobalCache, exposedGlobalDataContainer.GlobalInputEvents, dclCursor, unityEventSystem),
                new CharacterCameraPlugin(staticContainer.AssetsProvisioner, realmSamplingData, exposedGlobalDataContainer.ExposedCameraData, debugBuilder, dclInput),
                new WearablePlugin(staticContainer.AssetsProvisioner, staticContainer.WebRequestsContainer.WebRequestController, staticContainer.RealmData, ASSET_BUNDLES_URL, staticContainer.CacheCleaner, wearableCatalog), new EmotePlugin(staticContainer.WebRequestsContainer.WebRequestController, emotesCache, staticContainer.RealmData, multiplayerEmotesMessageBus, debugBuilder, staticContainer.AssetsProvisioner, selfProfile, container.MvcManager, dclInput, staticContainer.CacheCleaner, identityCache, entityParticipantTable, ASSET_BUNDLES_URL),
                new ProfilingPlugin(staticContainer.ProfilingProvider, staticContainer.SingletonSharedDependencies.FrameTimeBudget, staticContainer.SingletonSharedDependencies.MemoryBudget, debugBuilder),
                new AvatarPlugin(
                    staticContainer.ComponentsContainer.ComponentPoolsRegistry,
                    staticContainer.AssetsProvisioner,
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
                new ProfilePlugin(container.ProfileRepository, profileCache, staticContainer.CacheCleaner, new ProfileIntentionCache()), new MapRendererPlugin(mapRendererContainer.MapRenderer), new MinimapPlugin(staticContainer.AssetsProvisioner, container.MvcManager, mapRendererContainer, placesAPIService, staticContainer.RealmData, container.ChatMessagesBus, realmNavigator, staticContainer.ScenesCache),
                new ChatPlugin(staticContainer.AssetsProvisioner, container.MvcManager, container.ChatMessagesBus, entityParticipantTable, nametagsData, dclInput, unityEventSystem),
                new ExplorePanelPlugin(
                    staticContainer.AssetsProvisioner,
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
                    ASSET_BUNDLES_URL
                ),
                new CharacterPreviewPlugin(staticContainer.ComponentsContainer.ComponentPoolsRegistry, staticContainer.AssetsProvisioner, staticContainer.CacheCleaner),
                new WebRequestsPlugin(staticContainer.WebRequestsContainer.AnalyticsContainer, debugBuilder),
                new Web3AuthenticationPlugin(staticContainer.AssetsProvisioner, dynamicWorldDependencies.Web3Authenticator, debugBuilder, container.MvcManager, selfProfile, webBrowser, staticContainer.RealmData, identityCache, characterPreviewFactory, dynamicWorldDependencies.SplashAnimator),
                new StylizedSkyboxPlugin(staticContainer.AssetsProvisioner, dynamicSettings.DirectionalLight, debugBuilder),
                new LoadingScreenPlugin(staticContainer.AssetsProvisioner, container.MvcManager),
                new ExternalUrlPromptPlugin(staticContainer.AssetsProvisioner, webBrowser, container.MvcManager, dclCursor), new TeleportPromptPlugin(staticContainer.AssetsProvisioner, realmNavigator, container.MvcManager, staticContainer.WebRequestsContainer.WebRequestController, placesAPIService, dclCursor),
                new ChangeRealmPromptPlugin(
                    staticContainer.AssetsProvisioner,
                    container.MvcManager,
                    dclCursor,
                    realmUrl => container.RealmController.SetRealmAsync(URLDomain.FromString(realmUrl), CancellationToken.None).Forget()),
                new NftPromptPlugin(staticContainer.AssetsProvisioner, webBrowser, container.MvcManager, nftInfoAPIClient, staticContainer.WebRequestsContainer.WebRequestController, dclCursor),
                staticContainer.CharacterContainer.CreateGlobalPlugin(),
                staticContainer.QualityContainer.CreatePlugin(),
                landscapePlugin,
                new MultiplayerMovementPlugin(staticContainer.AssetsProvisioner, new MultiplayerMovementMessageBus(container.MessagePipesHub, entityParticipantTable)),
                container.LODContainer.LODPlugin,
                container.LODContainer.RoadPlugin,
                new AudioPlaybackPlugin(genesisTerrain, staticContainer.AssetsProvisioner, dynamicWorldParams.EnableLandscape),
                new RealmDataDirtyFlagPlugin(staticContainer.RealmData)
            };

            globalPlugins.AddRange(staticContainer.SharedPlugins);

            container.GlobalWorldFactory = new GlobalWorldFactory(
                in staticContainer,
                exposedGlobalDataContainer.CameraSamplingData,
                realmSamplingData,
                ASSET_BUNDLES_URL,
                staticContainer.RealmData,
                globalPlugins,
                debugBuilder,
                staticContainer.ScenesCache,
                dynamicWorldParams.HybridSceneParams);

            container.GlobalPlugins = globalPlugins;

            staticContainer.RoomHubProxy.SetObject(container.RoomHub);

            BuildTeleportWidget(realmNavigator, debugBuilder, dynamicWorldParams.Realms);

            return (container, true);
        }
    }
}
