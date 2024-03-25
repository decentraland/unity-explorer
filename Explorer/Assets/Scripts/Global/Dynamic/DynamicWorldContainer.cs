using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AsyncLoadReporting;
using DCL.AvatarRendering.Wearables;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Browser;
using DCL.CharacterPreview;
using DCL.Chat;
using DCL.Chat.MessageBus;
using DCL.Chat.MessageBus.Deduplication;
using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using DCL.LOD;
using DCL.ParcelsService;
using DCL.PlacesAPIService;
using DCL.PluginSystem;
using DCL.PluginSystem.Global;
using DCL.Profiles;
using DCL.SceneLoadingScreens;
using DCL.UserInAppInitializationFlow;
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
using DCL.Multiplayer.Connections.Archipelago.Rooms;
using DCL.Multiplayer.Connections.GateKeeper.Meta;
using DCL.Multiplayer.Connections.GateKeeper.Rooms;
using DCL.Multiplayer.Connections.Messaging.Hubs;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Multiplayer.Movement;
using DCL.Multiplayer.Movement.Systems;
using DCL.Multiplayer.Profiles.BroadcastProfiles;
using DCL.Multiplayer.Profiles.Poses;
using DCL.Multiplayer.Profiles.Tables;
using DCL.Nametags;
using DCL.NftInfoAPIService;
using DCL.Utilities.Extensions;
using LiveKit.Internal.FFIClients.Pools;
using LiveKit.Internal.FFIClients.Pools.Memory;
using System.Buffers;
using UnityEngine;
using UnityEngine.Pool;
using Utility.PriorityQueue;
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

        public RealUserInitializationFlowController UserInAppInitializationFlow { get; private set; } = null!;

        public IChatMessagesBus MessagesBus { get; private set; } = null!;

        public IProfileBroadcast ProfileBroadcast { get; private set; } = null!;

        public MultiplayerMovementMessageBus MultiplayerMovementMessageBus { get; private set; } = null!;

        public void Dispose()
        {
            MvcManager.Dispose();
            MessagesBus.Dispose();
            ProfileBroadcast.Dispose();
            MultiplayerMovementMessageBus.Dispose();
        }

        public UniTask InitializeAsync(DynamicWorldSettings settings, CancellationToken ct)
        {
            DebugContainer = DebugUtilitiesContainer.Create(settings.DebugViewsCatalog);
            return UniTask.CompletedTask;
        }

        private static void BuildTeleportWidget(IRealmController realmController, MVCManager mvcManager,
            IDebugContainerBuilder debugContainerBuilder, List<string> realms)
        {
            async UniTask ChangeRealmAsync(string realm, CancellationToken ct)
            {
                var loadReport = new AsyncLoadProcessReport(new UniTaskCompletionSource(), new AsyncReactiveProperty<float>(0));

                await UniTask.WhenAll(mvcManager.ShowAsync(
                        SceneLoadingScreenController.IssueCommand(new SceneLoadingScreenController.Params(loadReport, TimeSpan.FromSeconds(30)))),
                    realmController.SetRealmAsync(URLDomain.FromString(realm), Vector2Int.zero, loadReport, ct));
            }

            debugContainerBuilder.AddWidget("Realm")
                                 .AddControl(new DebugDropdownDef(realms, new ElementBinding<string>(realms[0],
                                      evt => { ChangeRealmAsync(evt.newValue, CancellationToken.None).Forget(); }), string.Empty), null)
                                 .AddStringFieldWithConfirmation("https://peer.decentraland.org", "Change", realm => { ChangeRealmAsync(realm, CancellationToken.None).Forget(); });
        }

        public static async UniTask<(DynamicWorldContainer? container, bool success)> CreateAsync(
            DynamicWorldDependencies dynamicWorldDependencies,
            DynamicWorldParams dynamicWorldParams,
            CancellationToken ct)
        {
            var container = new DynamicWorldContainer();
            (_, bool result) = await dynamicWorldDependencies.SettingsContainer.InitializePluginAsync(container, ct);

            if (!result)
                return (null, false);

            DebugContainerBuilder debugBuilder = container.DebugContainer.Builder.EnsureNotNull();
            DynamicSettings dynamicSettings = dynamicWorldDependencies.DynamicSettings;
            StaticContainer staticContainer = dynamicWorldDependencies.StaticContainer;
            IWeb3IdentityCache identityCache = dynamicWorldDependencies.Web3IdentityCache;

            staticContainer.QualityContainer.AddDebugViews(debugBuilder);

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
            var nftInfoAPIClient = new OpenSeaAPIClient(staticContainer.WebRequestsContainer.WebRequestController);
            var wearableCatalog = new WearableCatalog();
            var characterPreviewFactory = new CharacterPreviewFactory(staticContainer.ComponentsContainer.ComponentPoolsRegistry);
            var webBrowser = new UnityAppWebBrowser();
            ChatEntryConfigurationSO? chatEntryConfiguration = (await staticContainer.AssetsProvisioner.ProvideMainAssetAsync(dynamicSettings.ChatEntryConfiguration, ct)).Value;
            NametagsData? nametagsData = (await staticContainer.AssetsProvisioner.ProvideMainAssetAsync(dynamicSettings.NametagsData, ct)).Value;

            IProfileCache profileCache = new DefaultProfileCache();

            container.ProfileRepository = new RealmProfileRepository(staticContainer.WebRequestsContainer.WebRequestController, realmData, profileCache);

            var landscapePlugin = new LandscapePlugin(staticContainer.AssetsProvisioner, debugBuilder, mapRendererContainer.TextureContainer, dynamicWorldParams.EnableLandscape);

            var multiPool = new ThreadSafeMultiPool();
            var memoryPool = new ArrayMemoryPool(ArrayPool<byte>.Shared!);
            var realFlowLoadingStatus = new RealFlowLoadingStatus();

            container.UserInAppInitializationFlow = new RealUserInitializationFlowController(
                realFlowLoadingStatus,
                parcelServiceContainer.TeleportController,
                container.MvcManager,
                identityCache,
                container.ProfileRepository, dynamicWorldParams.StartParcel,
                staticContainer.MainPlayerAvatarBaseProxy,
                staticContainer.ExposedGlobalDataContainer.ExposedCameraData.CameraEntityProxy,
                exposedGlobalDataContainer.CameraSamplingData,
                dynamicWorldParams.EnableLandscape, landscapePlugin);

            var archipelagoIslandRoom = new ArchipelagoIslandRoom(staticContainer.CharacterContainer.CharacterObject, staticContainer.WebRequestsContainer.WebRequestController, identityCache, multiPool);

            var metaDataSource = new LogMetaDataSource(new MetaDataSource(realmData, staticContainer.CharacterContainer.CharacterObject, placesAPIService));
            var gateKeeperSceneRoom = new GateKeeperSceneRoom(staticContainer.WebRequestsContainer.WebRequestController, metaDataSource);

            container.RealmController = new RealmController(
                identityCache,
                staticContainer.WebRequestsContainer.WebRequestController,
                parcelServiceContainer.TeleportController,
                parcelServiceContainer.RetrieveSceneFromFixedRealm,
                parcelServiceContainer.RetrieveSceneFromVolatileWorld,
                dynamicWorldParams.StaticLoadPositions,
                realmData,
                staticContainer.ScenesCache);

            var roomHub = new RoomHub(archipelagoIslandRoom, gateKeeperSceneRoom);
            var messagePipesHub = new MessagePipesHub(roomHub, multiPool, memoryPool);

            var entityParticipantTable = new EntityParticipantTable();

            container.MessagesBus = new DebugPanelChatMessageBus(
                new SelfResendChatMessageBus(
                    new MultiplayerChatMessagesBus(messagePipesHub, container.ProfileRepository, new MessageDeduplication()),
                    identityCache,
                    container.ProfileRepository
                ),
                debugBuilder
            );

            var queuePoolFullMovementMessage = new ObjectPool<SimplePriorityQueue<FullMovementMessage>>(
                () => new SimplePriorityQueue<FullMovementMessage>(),
                actionOnRelease: x => x.Clear()
            );

            container.ProfileBroadcast = new DebounceProfileBroadcast(
                new ProfileBroadcast(messagePipesHub)
            );

            container.MultiplayerMovementMessageBus = new MultiplayerMovementMessageBus(messagePipesHub, entityParticipantTable);

            var remotePoses = new DebounceRemotePoses(
                new RemotePoses(roomHub)
            );

            var visualSceneStateResolver = new VisualSceneStateResolver();
            
            var globalPlugins = new List<IDCLGlobalPlugin>
            {
                new MultiplayerPlugin(
                    archipelagoIslandRoom,
                    gateKeeperSceneRoom,
                    roomHub,
                    container.ProfileRepository,
                    container.ProfileBroadcast,
                    debugBuilder,
                    realFlowLoadingStatus,
                    entityParticipantTable,
                    staticContainer.ComponentsContainer.ComponentPoolsRegistry,
                    messagePipesHub,
                    remotePoses,
                    staticContainer.CharacterContainer.CharacterObject,
                    queuePoolFullMovementMessage
                ),
                new CharacterMotionPlugin(staticContainer.AssetsProvisioner, staticContainer.CharacterContainer.CharacterObject, debugBuilder),
                new InputPlugin(dclInput),
                new GlobalInteractionPlugin(dclInput, dynamicWorldDependencies.RootUIDocument, staticContainer.AssetsProvisioner, staticContainer.EntityCollidersGlobalCache, exposedGlobalDataContainer.GlobalInputEvents),
                new CharacterCameraPlugin(staticContainer.AssetsProvisioner, realmSamplingData, exposedGlobalDataContainer.ExposedCameraData),
                new WearablePlugin(staticContainer.AssetsProvisioner, staticContainer.WebRequestsContainer.WebRequestController, realmData, ASSET_BUNDLES_URL, staticContainer.CacheCleaner, wearableCatalog),
                new ProfilingPlugin(staticContainer.ProfilingProvider, staticContainer.SingletonSharedDependencies.FrameTimeBudget, staticContainer.SingletonSharedDependencies.MemoryBudget, debugBuilder),
                new AvatarPlugin(
                    staticContainer.ComponentsContainer.ComponentPoolsRegistry,
                    staticContainer.AssetsProvisioner,
                    staticContainer.SingletonSharedDependencies.FrameTimeBudget,
                    staticContainer.SingletonSharedDependencies.MemoryBudget,
                    realmData,
                    staticContainer.MainPlayerAvatarBaseProxy,
                    debugBuilder,
                    staticContainer.CacheCleaner,
                    chatEntryConfiguration,
                    new DefaultFaceFeaturesHandler(wearableCatalog),
                    entityParticipantTable,
                    nametagsData
                ),
                new ProfilePlugin(container.ProfileRepository, profileCache, staticContainer.CacheCleaner, new ProfileIntentionCache()),
                new MapRendererPlugin(mapRendererContainer.MapRenderer),
                new MinimapPlugin(staticContainer.AssetsProvisioner, container.MvcManager, mapRendererContainer, placesAPIService),
                new ChatPlugin(staticContainer.AssetsProvisioner, container.MvcManager, container.MessagesBus, entityParticipantTable, nametagsData),
                new ExplorePanelPlugin(
                    staticContainer.AssetsProvisioner,
                    container.MvcManager,
                    mapRendererContainer,
                    placesAPIService,
                    parcelServiceContainer.TeleportController,
                    staticContainer.WebRequestsContainer.WebRequestController,
                    identityCache,
                    wearableCatalog,
                    characterPreviewFactory,
                    container.ProfileRepository,
                    dynamicWorldDependencies.Web3Authenticator,
                    container.UserInAppInitializationFlow,
                    webBrowser),
                new CharacterPreviewPlugin(staticContainer.ComponentsContainer.ComponentPoolsRegistry, staticContainer.AssetsProvisioner, staticContainer.CacheCleaner),
                new WebRequestsPlugin(staticContainer.WebRequestsContainer.AnalyticsContainer, debugBuilder),
                new Web3AuthenticationPlugin(staticContainer.AssetsProvisioner, dynamicWorldDependencies.Web3Authenticator, debugBuilder, container.MvcManager, container.ProfileRepository, webBrowser, realmData, identityCache, characterPreviewFactory),
                new StylizedSkyboxPlugin(staticContainer.AssetsProvisioner, dynamicSettings.DirectionalLight, debugBuilder),
                new LoadingScreenPlugin(staticContainer.AssetsProvisioner, container.MvcManager),
                new LODPlugin(staticContainer.CacheCleaner, realmData,
                    staticContainer.SingletonSharedDependencies.MemoryBudget,
                    staticContainer.SingletonSharedDependencies.FrameTimeBudget,
                    staticContainer.ScenesCache, debugBuilder, staticContainer.AssetsProvisioner, staticContainer.SceneReadinessReportQueue, visualSceneStateResolver),
                new ExternalUrlPromptPlugin(staticContainer.AssetsProvisioner, webBrowser, container.MvcManager),
                new TeleportPromptPlugin(staticContainer.AssetsProvisioner, parcelServiceContainer.TeleportController, container.MvcManager, staticContainer.WebRequestsContainer.WebRequestController, placesAPIService),
                new ChangeRealmPromptPlugin(
                    staticContainer.AssetsProvisioner,
                    container.MvcManager,
                    realmUrl => container.RealmController.SetRealmAsync(URLDomain.FromString(realmUrl), CancellationToken.None).Forget()),
                new NftPromptPlugin(staticContainer.AssetsProvisioner, webBrowser, container.MvcManager, nftInfoAPIClient, staticContainer.WebRequestsContainer.WebRequestController),
                staticContainer.CharacterContainer.CreateGlobalPlugin(),
                staticContainer.QualityContainer.CreatePlugin(),
                landscapePlugin,
                new MultiplayerMovementPlugin(staticContainer.AssetsProvisioner, container.MultiplayerMovementMessageBus),
                new RoadPlugin(staticContainer.CacheCleaner,
                    staticContainer.AssetsProvisioner,
                    staticContainer.SingletonSharedDependencies.FrameTimeBudget,
                    staticContainer.SingletonSharedDependencies.MemoryBudget,
                    visualSceneStateResolver)
            };

            globalPlugins.AddRange(staticContainer.SharedPlugins);

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

            BuildTeleportWidget(container.RealmController, container.MvcManager, debugBuilder, dynamicWorldParams.Realms);

            return (container, true);
        }
    }
}
