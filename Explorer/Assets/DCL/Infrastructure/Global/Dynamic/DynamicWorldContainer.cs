using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.ApplicationBlocklistGuard;
using DCL.AssetsProvision;
using DCL.Audio;
using DCL.AvatarRendering.Emotes;
using DCL.AvatarRendering.Emotes.Equipped;
using DCL.AvatarRendering.Loading;
using DCL.AvatarRendering.Wearables;
using DCL.AvatarRendering.Wearables.Equipped;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.AvatarRendering.Wearables.ThirdParty;
using DCL.Backpack.BackpackBus;
using DCL.Backpack.Gifting.Services;
using DCL.Backpack.Gifting.Services.PendingTransfers;
using DCL.Backpack.Gifting.Services.SnapshotEquipped;
using DCL.BadgesAPIService;
using DCL.Browser;
using DCL.CharacterPreview;
using DCL.Chat.ChatServices;
using DCL.Chat.Commands;
using DCL.Chat.History;
using DCL.Chat.MessageBus;
using DCL.ChatArea;
using DCL.Clipboard;
using DCL.Communities;
using DCL.SpringBones;
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
using DCL.InWorldCamera;
using DCL.InWorldCamera.CameraReelStorageService;
using DCL.LOD.Systems;
using DCL.Multiplayer.Connections.Messaging.Hubs;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Multiplayer.Emotes;
using DCL.Multiplayer.Movement;
using DCL.Multiplayer.Profiles.BroadcastProfiles;
using DCL.Multiplayer.Profiles.Poses;
using DCL.Multiplayer.SDK.Systems.GlobalWorld;
using DCL.NftInfoAPIService;
using DCL.Notifications;
using DCL.NotificationsBus;
using DCL.Optimization.AdaptivePerformance.Systems;
using DCL.PerformanceAndDiagnostics.Analytics;
using DCL.PerformanceAndDiagnostics.Analytics.DecoratorBased;
using DCL.PluginSystem;
using DCL.PluginSystem.Global;
using DCL.PluginSystem.SmartWearables;
using DCL.PluginSystem.World;
using DCL.PrivateWorlds;
using DCL.Profiles;
using DCL.RealmNavigation;
using DCL.Rendering.GPUInstancing.Systems;
using DCL.RuntimeDeepLink;
using DCL.SceneLoadingScreens.LoadingScreen;
using DCL.SDKComponents.AvatarLocomotion;
using DCL.SkyBox;
using DCL.SocialService;
using DCL.Translation;
using DCL.UI;
using DCL.UI.ConfirmationDialog;
using DCL.UI.InputFieldFormatting;
using DCL.Prefs;
using DCL.UserInAppInitializationFlow;
using DCL.Utilities;
using DCL.Utilities.Extensions;
using DCL.VoiceChat;
using DCL.VoiceChat.Nearby;
using DCL.VoiceChat.Nearby.MutePersistence;
using DCL.Web3.Identities;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.CurrentScene;
using ECS.SceneLifeCycle.Realm;
using Global.AppArgs;
using Global.Dynamic.RealmUrl;
using Global.Versioning;
using MVC;
using SceneRunner.Debugging.Hub;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Diagnostics.CodeAnalysis;
using UnityEngine.Audio;
using Utility;

namespace Global.Dynamic
{
    public class DynamicWorldContainer : DCLWorldContainer<DynamicWorldSettings>
    {
        private readonly IProfileBroadcast profileBroadcast;
        private readonly SocialServicesContainer socialServicesContainer;
        private readonly MultiplayerContainer multiplayerContainer;
        private readonly BannedNotificationHandler bannedNotificationHandler;
        private readonly CommunitiesContainer communitiesContainer;
        private readonly VoiceChatContainer voiceChatContainer;
        private readonly CommsContainer commsContainer;
        private readonly ProfileContainer profileContainer;
        private readonly UIShellContainer uiShellContainer;
        private readonly ChatContainer chatContainer;

        public IMVCManager MvcManager => uiShellContainer.MvcManager;

        public IGlobalRealmController RealmController { get; }

        public IRealmNavigator RealmNavigator { get; }

        public GlobalWorldFactory GlobalWorldFactory { get; }

        public IReadOnlyList<IDCLGlobalPlugin> GlobalPlugins { get; }

        /// <summary>
        ///     Scene-world plugins owned by this container because they depend on comms/multiplayer services.
        ///     Combined with <see cref="StaticContainer.ECSWorldPlugins" /> for initialization and scene-world creation.
        /// </summary>
        public IReadOnlyList<IDCLWorldPlugin> WorldPlugins { get; }

        public IProfileRepository ProfileRepository { get; }

        public IUserInAppInitializationFlow UserInAppInAppInitializationFlow { get; }

        public IMessagePipesHub MessagePipesHub => commsContainer.MessagePipesHub;

        public IRemoteMetadata RemoteMetadata => commsContainer.RemoteMetadata;

        public IRoomHub RoomHub => commsContainer.RoomHub;

        public ISystemClipboard SystemClipboard => uiShellContainer.Clipboard;

        private DynamicWorldContainer(
            UIShellContainer uiShellContainer,
            IGlobalRealmController realmController,
            IRealmNavigator realmNavigator,
            GlobalWorldFactory globalWorldFactory,
            IReadOnlyList<IDCLGlobalPlugin> globalPlugins,
            IReadOnlyList<IDCLWorldPlugin> worldPlugins,
            IProfileRepository profileRepository,
            IUserInAppInitializationFlow userInAppInAppInitializationFlow,
            ChatContainer chatContainer,
            CommsContainer commsContainer,
            IProfileBroadcast profileBroadcast,
            SocialServicesContainer socialServicesContainer,
            ProfileContainer profileContainer,
            BannedNotificationHandler bannedNotificationHandler,
            MultiplayerContainer multiplayerContainer,
            CommunitiesContainer communitiesContainer,
            VoiceChatContainer voiceChatContainer)
        {
            this.uiShellContainer = uiShellContainer;
            RealmController = realmController;
            RealmNavigator = realmNavigator;
            GlobalWorldFactory = globalWorldFactory;
            GlobalPlugins = globalPlugins;
            WorldPlugins = worldPlugins;
            ProfileRepository = profileRepository;
            UserInAppInAppInitializationFlow = userInAppInAppInitializationFlow;
            this.commsContainer = commsContainer;
            this.chatContainer = chatContainer;
            this.profileBroadcast = profileBroadcast;
            this.socialServicesContainer = socialServicesContainer;
            this.profileContainer = profileContainer;
            this.bannedNotificationHandler = bannedNotificationHandler;
            this.multiplayerContainer = multiplayerContainer;
            this.communitiesContainer = communitiesContainer;
            this.voiceChatContainer = voiceChatContainer;
        }

        public override void Dispose()
        {
            // Reverse creation order
            voiceChatContainer.Dispose(); // disposes JoinedCommunitiesVoiceLiveTracker, which unsubscribes from CommunityDataService
            socialServicesContainer.Dispose();
            bannedNotificationHandler.Dispose();
            chatContainer.Dispose();
            communitiesContainer.Dispose(); // disposes CommunityDataService
            profileBroadcast.Dispose();
            multiplayerContainer.Dispose();
            commsContainer.Dispose();
            profileContainer.Dispose();
        }

        [SuppressMessage("ReSharper", "MethodHasAsyncOverloadWithCancellation")]
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
            var placesAndEventsContainer = PlacesAndEventsContainer.Create(staticContainer.WebRequestsContainer.WebRequestController, bootstrapContainer.DecentralandUrlsSource);

            var wearableContainer = WearableContainer.Create(staticContainer, bootstrapContainer, identityCache, globalWorld, appArgs, dynamicWorldParams.EnableAnalytics);

            NotificationsBusController.Initialize(new NotificationsBusController());

            DefaultTexturesContainer defaultTexturesContainer = null!;
            LODContainer lodContainer = null!;
            MultiplayerContainer multiplayerContainer = null!;

            UIShellContainer uiShellContainer = await UIShellContainer
                                                     .CreateAsync(dynamicWorldDependencies.SettingsContainer, assetsProvisioner, bootstrapContainer, dynamicWorldParams.EnableAnalytics, ct)
                                                     .ThrowOnFail();

            staticContainer.QualityContainer.AddDebugViews(debugBuilder);

            var realmSamplingData = new RealmSamplingData();

            ExposedGlobalDataContainer exposedGlobalDataContainer = staticContainer.ExposedGlobalDataContainer;

            var nftInfoAPIClient = new OpenSeaAPIClient(staticContainer.WebRequestsContainer.WebRequestController, bootstrapContainer.DecentralandUrlsSource);
            var characterPreviewFactory = new CharacterPreviewFactory(staticContainer.ComponentsContainer.ComponentPoolsRegistry, appArgs);
            IWebBrowser webBrowser = bootstrapContainer.WebBrowser;

            IEmoteStorage emotesCache = staticContainer.EmoteStorage;

            IProfileRepository profilesRepository = staticContainer.ProfilesContainer.Repository;
            IProfileCache profileCache = staticContainer.ProfilesContainer.Cache;

            var profileContainer = ProfileContainer.Create(staticContainer, bootstrapContainer, identityCache, globalWorld, playerEntity, wearableContainer);

            //TODO should be unified with LaunchMode
            bool localSceneDevelopment = !string.IsNullOrEmpty(dynamicWorldParams.LocalSceneDevelopmentRealm);

            var realmContainer = RealmContainer.Create(
                staticContainer,
                dynamicWorldParams.StaticLoadPositions,
                debugBuilder,
                uiShellContainer.MvcManager,
                localSceneDevelopment,
                bootstrapContainer.DecentralandUrlsSource,
                appArgs,
                bootstrapContainer.Environment,
                globalWorld,
                playerEntity);

            var terrainContainer = TerrainContainer.Create(staticContainer, realmContainer, dynamicWorldParams.EnableLandscape, localSceneDevelopment);

            var commsContainer = CommsContainer.Create(
                staticContainer,
                bootstrapContainer,
                identityCache,
                globalWorld,
                appArgs,
                dynamicWorldParams.IsolateScenesCommunication,
                dynamicWorldParams.EnableAnalytics,
                localSceneDevelopment);

            IFriendsEventBus friendsEventBus = new DefaultFriendsEventBus();

            IUserBlockingCache userBlockingCache = FeaturesRegistry.Instance.IsEnabled(FeatureId.FRIENDS_USER_BLOCKING)
                ? new UserBlockingCache(friendsEventBus)
                : new NullUserBlockingCache();

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

                multiplayerContainer = await MultiplayerContainer.CreateAsync(
                    settingsContainer,
                    staticContainer.RealmData,
                    identityCache,
                    commsContainer.MovementInbox,
                    staticContainer.QualityContainer.LandscapeData,
                    bootstrapContainer.DecentralandUrlsSource,
                    commsContainer.RoomHub,
                    commsContainer.MessagePipesHub,
                    dynamicSettings.MultiplayerDebugSettings,
                    userBlockingCache,
                    profileContainer.SelfProfile,
                    ct);
            }

            try { await InitializeContainersAsync(dynamicWorldDependencies.SettingsContainer, ct); }
            catch (Exception) { return (null, false); }

            CommunitiesContainer communitiesContainer = await CommunitiesContainer.CreateAsync(staticContainer.WebRequestsContainer.WebRequestController, bootstrapContainer.DecentralandUrlsSource, identityCache, appArgs, ct);

            var realmNavigatorContainer = RealmNavigationContainer.Create
                (staticContainer, bootstrapContainer, lodContainer, realmContainer, commsContainer.RemoteEntities, globalWorld, commsContainer.RoomHub, terrainContainer.Landscape, exposedGlobalDataContainer, realmContainer.LoadingScreen, placesAndEventsContainer.PlacesAPIService, identityCache, communitiesContainer.DataProvider, uiShellContainer.MvcManager);

            IRealmNavigator realmNavigator = realmNavigatorContainer.RealmNavigator;

            dynamicWorldDependencies.WorldInfoTool.Initialize(realmNavigatorContainer.WorldInfoHub);

            var chatContainer = ChatContainer.Create(
                staticContainer,
                bootstrapContainer,
                uiShellContainer,
                commsContainer,
                profileContainer,
                identityCache,
                userBlockingCache,
                realmNavigatorContainer.WorldInfoHub,
                realmContainer.ReloadSceneController,
                realmContainer.TeleportController,
                realmNavigator,
                debugBuilder,
                dclVersion,
                appArgs,
                globalWorld,
                playerEntity,
                localSceneDevelopment,
                dynamicWorldParams.EnableAnalytics);

            // Deferred: CommunityDataService needs chat history (only available now) but is owned by CommunitiesContainer.
            CommunityDataService communitiesDataService = communitiesContainer.CreateDataService(chatContainer.ChatHistory, uiShellContainer.MvcManager, identityCache);

            bool includeCameraReel = FeaturesRegistry.Instance.IsEnabled(FeatureId.CAMERA_REEL);
            bool includeFriends = FeaturesRegistry.Instance.IsEnabled(FeatureId.FRIENDS);
            bool includeMarketplaceCredits = FeaturesRegistry.Instance.IsEnabled(FeatureId.MARKETPLACE_CREDITS);
            bool includeBannedUsersFromScene = FeaturesRegistry.Instance.IsEnabled(FeatureId.BANNED_USERS_FROM_SCENE);

            var moderationDataProvider = new ModerationDataProvider(staticContainer.WebRequestsContainer.WebRequestController, bootstrapContainer.DecentralandUrlsSource);

            var bannedNotificationHandler = new BannedNotificationHandler(
                staticContainer.WebRequestsContainer.WebRequestController,
                bootstrapContainer.DecentralandUrlsSource,
                bootstrapContainer.IdentityCache!,
                moderationDataProvider,
                uiShellContainer.MvcManager,
                bootstrapContainer.WebBrowser);

            var initializationFlowContainer = InitializationFlowContainer.Create(staticContainer,
                bootstrapContainer,
                realmContainer,
                realmNavigatorContainer,
                terrainContainer,
                realmContainer.LoadingScreen,
                commsContainer.LivekitHealthCheck,
                uiShellContainer.MvcManager,
                profileContainer.SelfProfile,
                dynamicWorldParams,
                appArgs,
                backgroundMusic,
                commsContainer.RoomHub,
                localSceneDevelopment,
                staticContainer.CharacterContainer,
                moderationDataProvider,
                multiplayerContainer.PulseMultiplayerService,
                multiplayerContainer.ProfilePropagation,
                realmNavigatorContainer.WorldPermissionsService,
                chatContainer.ChatHistory);

            MapRendererContainer mapRendererContainer =
                await MapRendererContainer
                   .CreateAsync(
                        dynamicWorldDependencies.SettingsContainer,
                        staticContainer,
                        bootstrapContainer.DecentralandUrlsSource,
                        assetsProvisioner,
                        placesAndEventsContainer.PlacesAPIService,
                        placesAndEventsContainer.EventsApiService,
                        placesAndEventsContainer.MapPathEventBus,
                        staticContainer.MapPinsEventBus,
                        realmNavigator,
                        staticContainer.RealmData,
                        placesAndEventsContainer.NavmapBus,
                        placesAndEventsContainer.OnlineUsersProvider,
                        identityCache,
                        placesAndEventsContainer.HomePlaceEventBus,
                        chatContainer.ChatEventBus,
                        ct
                    );

            var socialServiceContainer = new SocialServicesContainer(
                bootstrapContainer.DecentralandUrlsSource,
                identityCache,
                appArgs,
                staticContainer.ScenesCache,
                staticContainer.EthereumApi,
                staticContainer.WebRequestsContainer.WebRequestController,
                staticContainer.RealmData,
                placesAndEventsContainer.PlacesAPIService,
                bootstrapContainer.Environment,
                bootstrapContainer.Analytics.Controller,
                localSceneDevelopment,
                dynamicWorldParams.EnableAnalytics);

            IDonationsService donationsService = socialServiceContainer.DonationsService;

            var voiceChatContainer = new VoiceChatContainer(
                socialServiceContainer.socialServicesRPC,
                socialServiceContainer.EventBus,
                commsContainer.RoomHub,
                identityCache,
                staticContainer.WebRequestsContainer.WebRequestController,
                staticContainer.ScenesCache,
                realmNavigator,
                staticContainer.RealmData,
                bootstrapContainer.DecentralandUrlsSource,
                chatContainer.ChatEventBus,
                chatContainer.CurrentChannelService,
                communitiesDataService
            );

            IEmotesMessageBus multiplayerEmotesMessageBus = multiplayerContainer.EmotesMessageBus;

            // Scene-world plugins that depend on comms/multiplayer services, which exist only in this container.
            // They join StaticContainer.ECSWorldPlugins for initialization and scene-world injection.
            var worldPlugins = new List<IDCLWorldPlugin>
            {
                new AvatarAttachPlugin(globalWorld, staticContainer.MainPlayerAvatarBaseProxy, staticContainer.ComponentsContainer.ComponentPoolsRegistry, commsContainer.EntityParticipantTable, staticContainer.CharacterContainer.Transform),
                new SceneMaskedEmotePlugin(globalWorld, playerEntity, staticContainer.MainPlayerAvatarBaseProxy, staticContainer.EmotesContainer.EmotePlayer, staticContainer.EmoteStorage, multiplayerEmotesMessageBus),
                new RealmInfoPlugin(staticContainer.RealmData, commsContainer.RoomHub),
            };

            var characterPreviewEventBus = new CharacterPreviewEventBus();
            var upscaleController = new UpscalingController(uiShellContainer.MvcManager);

            AudioMixer generalAudioMixer = (await assetsProvisioner.ProvideMainAssetAsync(dynamicSettings.GeneralAudioMixer, ct)).Value;
            var audioMixerVolumesController = new AudioMixerVolumesController(generalAudioMixer);

            var badgesAPIClient = new BadgesAPIClient(staticContainer.WebRequestsContainer.WebRequestController, bootstrapContainer.DecentralandUrlsSource);

            var cameraReelContainer = CameraReelContainer.Create(staticContainer.WebRequestsContainer.WebRequestController, bootstrapContainer.DecentralandUrlsSource, identityCache.Identity?.Address);

            var userCalendar = new GoogleUserCalendar(webBrowser);
            ITextFormatter hyperlinkTextFormatter = new HyperlinkTextFormatter(profileCache, profileContainer.SelfProfile);

            NotificationsRequestController notificationsRequestController = new (staticContainer.WebRequestsContainer.WebRequestController, bootstrapContainer.DecentralandUrlsSource, identityCache);

            FriendsServicesContainer? friendsServices = includeFriends
                ? new FriendsServicesContainer(
                    profileContainer.SelfProfile,
                    socialServiceContainer.socialServicesRPC,
                    friendsEventBus,
                    dynamicWorldParams.EnableAnalytics,
                    bootstrapContainer.Analytics.Controller)
                : null;

            GenericUserProfileContextMenuSettings genericUserProfileContextMenuSettingsSo = (await assetsProvisioner.ProvideMainAssetAsync(dynamicSettings.GenericUserProfileContextMenuSettings, ct)).Value;
            CommunityVoiceChatContextMenuConfiguration communityVoiceChatContextMenuSettingsSo = (await assetsProvisioner.ProvideMainAssetAsync(dynamicSettings.CommunityVoiceChatContextMenuSettings, ct)).Value;

            // Local scene development scenes are excluded from deeplink runtime handling logic
            if (!appArgs.HasFlag(AppArgsFlags.LOCAL_SCENE))
            {
                var deepLinkHandleImplementation = new DeepLinkHandle(dynamicWorldParams.StartParcel, chatContainer.ChatTeleporter, ct, communitiesDataService);
                deepLinkHandleImplementation.StartListenForDeepLinksAsync(ct).Forget();
            }

            IMVCManagerMenusAccessFacade menusAccessFacade = new MVCManagerMenusAccessFacade(
                uiShellContainer.MvcManager,
                profileCache,
                friendsServices?.FriendsService,
                chatContainer.ChatEventBus,
                genericUserProfileContextMenuSettingsSo,
                bootstrapContainer.Analytics.Controller,
                placesAndEventsContainer.OnlineUsersProvider,
                realmNavigator,
                friendsServices?.ConnectivityStatusTracker,
                profilesRepository,
                communityVoiceChatContextMenuSettingsSo,
                voiceChatContainer.VoiceChatOrchestrator,
                communitiesContainer.IncludeCommunities,
                communitiesContainer.DataProvider,
                bootstrapContainer.WebBrowser,
                bootstrapContainer.DecentralandUrlsSource,
                profileContainer.SelfProfile,
                voiceChatContainer.NearbyMuteService);

            ViewDependencies.Initialize(new ViewDependencies(
                uiShellContainer.EventSystem,
                menusAccessFacade,
                uiShellContainer.ClipboardManager,
                uiShellContainer.Cursor,
                new ContextMenuOpener(uiShellContainer.MvcManager),
                identityCache,
                new ConfirmationDialogOpener(uiShellContainer.MvcManager)));

            var realmNftNamesProvider = new RealmNftNamesProvider(staticContainer.WebRequestsContainer.WebRequestController,
                bootstrapContainer.DecentralandUrlsSource);

            var bannedSceneController = new ECSBannedScene(staticContainer.ScenesCache, globalWorld, playerEntity);

            var springBoneSimulationSettings = new SpringBoneSimulationSettings();

            var globalPlugins = new List<IDCLGlobalPlugin>
            {
                new ResourceUnloadingPlugin(staticContainer.SingletonSharedDependencies.MemoryBudget, staticContainer.CacheCleaner, staticContainer.SceneLoadingLimit),
                new AdaptivePerformancePlugin(staticContainer.Profiler, staticContainer.LoadingStatus),
                new LightSourceDebugPlugin(staticContainer.DebugContainerBuilder, globalWorld),
                commsContainer.CreateMultiplayerPlugin(staticContainer, assetsProvisioner, debugBuilder, multiplayerContainer),
                staticContainer.ProfilesContainer.CreatePlugin(),
                new WorldInfoPlugin(realmNavigatorContainer.WorldInfoHub, debugBuilder, chatContainer.ChatHistory),
                new CharacterMotionPlugin(staticContainer.RealmData, staticContainer.CharacterContainer.CharacterObject, debugBuilder, staticContainer.ComponentsContainer.ComponentPoolsRegistry,
                    staticContainer.SceneReadinessReportQueue, terrainContainer.Landscape, staticContainer.ScenesCache, assetsProvisioner, identityCache, friendsServices?.FriendsCache, multiplayerContainer.MovementMessageBus),
                uiShellContainer.CreateInputPlugin(assetsProvisioner, wearableContainer.EmoteWheelShortcutHandler),
                new GlobalInteractionPlugin(assetsProvisioner, staticContainer.EntityCollidersGlobalCache, exposedGlobalDataContainer.GlobalInputEvents, uiShellContainer.EventSystem, staticContainer.ScenesCache, uiShellContainer.MvcManager, menusAccessFacade, exposedGlobalDataContainer.ExposedCameraData.CameraEntityProxy),
                new CharacterCameraPlugin(assetsProvisioner, realmSamplingData, exposedGlobalDataContainer.ExposedCameraData, debugBuilder, dynamicWorldDependencies.CommandLineArgs),
                wearableContainer.CreateWearablePlugin(staticContainer, bootstrapContainer),
                wearableContainer.CreateEmotePlugin(staticContainer, bootstrapContainer, assetsProvisioner, debugBuilder, uiShellContainer, profileContainer, commsContainer,
                    multiplayerEmotesMessageBus, globalWorld, playerEntity),
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
                    wearableContainer.WearableCatalog,
                    userBlockingCache,
                    includeBannedUsersFromScene),
                uiShellContainer.CreateMainUIPlugin(includeFriends),
                profileContainer.CreateProfilePlugin(staticContainer),
                mapRendererContainer.CreatePlugin(),
                new SidebarPlugin(
                    assetsProvisioner,
                    uiShellContainer.MvcManager,
                    uiShellContainer.MainUIView,
                    notificationsRequestController,
                    identityCache,
                    profilesRepository,
                    staticContainer.WebRequestsContainer.WebRequestController,
                    webBrowser,
                    dynamicWorldDependencies.CompositeWeb3Provider,
                    initializationFlowContainer.InitializationFlow,
                    profileCache,
                    globalWorld,
                    playerEntity,
                    chatContainer.ChatHistory,
                    profileContainer.ProfileRepositoryWrapper,
                    profileContainer.ProfileChangesBus,
                    profileContainer.SelfProfile,
                    staticContainer.RealmData,
                    staticContainer.SceneRestrictionBusController,
                    bootstrapContainer.DecentralandUrlsSource,
                    uiShellContainer.PassportBridge,
                    chatContainer.ChatEventBus,
                    placesAndEventsContainer.EventsApiService,
                    staticContainer.SmartWearableCache,
                    uiShellContainer.SupportRequestService,
                    voiceChatContainer.JoinedCommunitiesVoiceLiveTracker),
                uiShellContainer.CreateErrorPopupPlugin(assetsProvisioner),
                new PrivateWorldsPlugin(
                    uiShellContainer.MvcManager,
                    assetsProvisioner,
                    commsContainer.RoomHub,
                    realmNavigatorContainer.WorldPermissionsService,
                    realmNavigatorContainer.WorldAccessGate,
                    staticContainer.InputBlock,
                    staticContainer.RealmData,
                    realmNavigator,
                    chatContainer.ChatHistory),
                new MinimapPlugin(
                    uiShellContainer.MainUIView.MinimapView.EnsureNotNull(),
                    mapRendererContainer.MapRenderer,
                    uiShellContainer.MvcManager,
                    placesAndEventsContainer.PlacesAPIService,
                    staticContainer.RealmData,
                    realmNavigator,
                    staticContainer.ScenesCache,
                    placesAndEventsContainer.MapPathEventBus,
                    staticContainer.SceneRestrictionBusController,
                    dynamicWorldParams.StartParcel.Peek(),
                    uiShellContainer.Clipboard,
                    bootstrapContainer.DecentralandUrlsSource,
                    chatContainer.ChatMessagesBus,
                    chatContainer.ReloadSceneChatCommand,
                    commsContainer.RoomHub,
                    staticContainer.LoadingStatus,
                    includeBannedUsersFromScene,
                    placesAndEventsContainer.HomePlaceEventBus,
                    donationsService),
                chatContainer.CreatePlugin(staticContainer, bootstrapContainer, assetsProvisioner, uiShellContainer, commsContainer, profileContainer, communitiesContainer,
                    voiceChatContainer, socialServiceContainer, menusAccessFacade, dynamicSettings.NametagsData, hyperlinkTextFormatter, identityCache, userBlockingCache,
                    friendsEventBus, friendsServices?.FriendsService, communitiesDataService, globalWorld, playerEntity),
                new ExplorePanelPlugin(
                    chatContainer.ChatEventBus,
                    assetsProvisioner,
                    uiShellContainer.MvcManager,
                    mapRendererContainer,
                    placesAndEventsContainer.PlacesAPIService,
                    staticContainer.WebRequestsContainer.WebRequestController,
                    identityCache,
                    cameraReelContainer.StorageService,
                    cameraReelContainer.StorageService,
                    uiShellContainer.Clipboard,
                    bootstrapContainer.DecentralandUrlsSource,
                    wearableContainer.WearableCatalog,
                    characterPreviewFactory,
                    profilesRepository,
                    dynamicWorldDependencies.CompositeWeb3Provider,
                    initializationFlowContainer.InitializationFlow,
                    profileContainer.SelfProfile,
                    profileContainer.EquippedWearables,
                    profileContainer.EquippedEmotes,
                    webBrowser,
                    emotesCache,
                    staticContainer.RealmData,
                    profileCache,
                    characterPreviewEventBus,
                    placesAndEventsContainer.MapPathEventBus,
                    wearableContainer.BackpackEventBus,
                    wearableContainer.ThirdPartyNftProviderSource,
                    wearableContainer.WearablesProvider,
                    uiShellContainer.Cursor,
                    staticContainer.InputBlock,
                    wearableContainer.EmoteProvider,
                    globalWorld,
                    playerEntity,
                    chatContainer.ChatMessagesBus,
                    staticContainer.MemoryCap,
                    bootstrapContainer.VolumeBus,
                    placesAndEventsContainer.EventsApiService,
                    userCalendar,
                    uiShellContainer.Clipboard,
                    placesAndEventsContainer.NavmapBus,
                    placesAndEventsContainer.NavmapCommandFactory,
                    appArgs,
                    userBlockingCache,
                    profileContainer.ProfileChangesBus,
                    staticContainer.SceneLoadingLimit,
                    uiShellContainer.MainUIView.WarningNotification,
                    profileContainer.ProfileRepositoryWrapper,
                    upscaleController,
                    communitiesContainer.DataProvider,
                    realmNftNamesProvider,
                    voiceChatContainer.VoiceChatOrchestrator,
                    cameraReelContainer.GalleryEventBus,
                    wearableContainer.ThumbnailProvider,
                    uiShellContainer.PassportBridge,
                    placesAndEventsContainer.HomePlaceEventBus,
                    staticContainer.SmartWearableCache,
                    staticContainer.ImageControllerProvider,
                    bootstrapContainer.Analytics.Controller,
                    communitiesDataService,
                    staticContainer.LoadingStatus,
                    donationsService,
                    realmNavigator,
                    friendsServices?.FriendsService,
                    staticContainer.PublishIpfsEntityCommand,
                    realmNavigatorContainer.WorldPermissionsService,
                    staticContainer.QualityContainer.RendererFeaturesCache,
                    springBoneSimulationSettings,
                    voiceChatContainer.JoinedCommunitiesVoiceLiveTracker,
                    profileContainer.PendingTransferService
                ),
                profileContainer.CreateGiftingPlugin(staticContainer, bootstrapContainer, assetsProvisioner, uiShellContainer, wearableContainer, chatContainer.ChatEventBus, identityCache),
                new CharacterPreviewPlugin(staticContainer.ComponentsContainer.ComponentPoolsRegistry, assetsProvisioner, staticContainer.CacheCleaner),
                staticContainer.WebRequestsContainer.CreatePlugin(localSceneDevelopment),
                new Web3AuthenticationPlugin(assetsProvisioner, dynamicWorldDependencies.CompositeWeb3Provider, debugBuilder, uiShellContainer.MvcManager, profileContainer.SelfProfile, webBrowser, staticContainer.RealmData, identityCache, characterPreviewFactory, dynamicWorldDependencies.SplashScreen, audioMixerVolumesController, staticContainer.InputBlock, characterPreviewEventBus, backgroundMusic, globalWorld, bootstrapContainer.AppArgs, wearableContainer.WearablesProvider, staticContainer.WebRequestsContainer.WebRequestController, bootstrapContainer.DecentralandUrlsSource, profileContainer.ProfileChangesBus),
                new SkyboxPlugin(assetsProvisioner, dynamicSettings.DirectionalLight, staticContainer.ScenesCache, staticContainer.SceneRestrictionBusController, staticContainer.RealmData, !appArgs.HasFlagWithValueFalse(AppArgsFlags.SKYBOX_TIME_ENABLED)),
                new LoadingScreenPlugin(assetsProvisioner, uiShellContainer.MvcManager, audioMixerVolumesController,
                    staticContainer.InputBlock, debugBuilder, staticContainer.LoadingStatus),
                new ExternalUrlPromptPlugin(assetsProvisioner, webBrowser, uiShellContainer.MvcManager, uiShellContainer.Cursor),
                new TeleportPromptPlugin(
                    assetsProvisioner,
                    uiShellContainer.MvcManager,
                    staticContainer.ImageControllerProvider,
                    placesAndEventsContainer.PlacesAPIService,
                    uiShellContainer.Cursor,
                    chatContainer.ChatMessagesBus
                ),
                new ChangeRealmPromptPlugin(
                    assetsProvisioner,
                    uiShellContainer.MvcManager,
                    uiShellContainer.Cursor,
                    realmUrl => chatContainer.ChatMessagesBus.SendWithUtcNowTimestamp(ChatChannel.NEARBY_CHANNEL, $"/{ChatCommandsUtils.COMMAND_GOTO} {realmUrl}", ChatMessageOrigin.RESTRICTED_ACTION_API)),
                new NftPromptPlugin(assetsProvisioner, webBrowser, uiShellContainer.MvcManager, nftInfoAPIClient, staticContainer.ImageControllerProvider, uiShellContainer.Cursor),
                staticContainer.CharacterContainer.CreateGlobalPlugin(),
                staticContainer.QualityContainer.CreatePlugin(),
                multiplayerContainer.CreatePlugin(staticContainer, assetsProvisioner, debugBuilder, commsContainer, dynamicSettings.MultiplayerDebugSettings, appArgs),
                new AudioPlaybackPlugin(terrainContainer.GenesisTerrain, terrainContainer.WorldsTerrain, assetsProvisioner, dynamicWorldParams.EnableLandscape, audioMixerVolumesController, staticContainer.RealmData),
                new RealmDataDirtyFlagPlugin(staticContainer.RealmData),
                new NotificationPlugin(
                    assetsProvisioner,
                    uiShellContainer.MvcManager,
                    staticContainer.ImageControllerProvider,
                    notificationsRequestController,
                    identityCache,
                    profilesRepository),
                new RewardPanelPlugin(uiShellContainer.MvcManager, assetsProvisioner, staticContainer.ImageControllerProvider),
                new PassportPlugin(
                    assetsProvisioner,
                    uiShellContainer.MvcManager,
                    uiShellContainer.Cursor,
                    profilesRepository,
                    characterPreviewFactory,
                    characterPreviewEventBus,
                    profileContainer.SelfProfile,
                    webBrowser,
                    bootstrapContainer.DecentralandUrlsSource,
                    badgesAPIClient,
                    staticContainer.InputBlock,
                    commsContainer.RemoteMetadata,
                    cameraReelContainer.StorageService,
                    cameraReelContainer.StorageService,
                    globalWorld,
                    playerEntity,
                    friendsServices?.FriendsService,
                    friendsServices?.ConnectivityStatusTracker,
                    placesAndEventsContainer.OnlineUsersProvider,
                    realmNavigator,
                    identityCache,
                    realmNftNamesProvider,
                    profileContainer.ProfileChangesBus,
                    communitiesContainer.IncludeCommunities,
                    profileContainer.ProfileRepositoryWrapper,
                    voiceChatContainer.VoiceChatOrchestrator,
                    cameraReelContainer.GalleryEventBus,
                    uiShellContainer.Clipboard,
                    communitiesContainer.DataProvider,
                    wearableContainer.ThumbnailProvider,
                    staticContainer.ImageControllerProvider
                ),
                uiShellContainer.CreateGenericPopupsPlugin(assetsProvisioner),
                uiShellContainer.CreateColorPickerPlugin(assetsProvisioner),
                uiShellContainer.CreateGenericContextMenuPlugin(assetsProvisioner, profileContainer.ProfileRepositoryWrapper),
                realmNavigatorContainer.CreatePlugin(),
                new GPUInstancingPlugin(staticContainer.GPUInstancingService, assetsProvisioner, staticContainer.RealmData, staticContainer.LoadingStatus, exposedGlobalDataContainer.ExposedCameraData),
                uiShellContainer.CreateConfirmationDialogPlugin(assetsProvisioner, profileContainer.ProfileRepositoryWrapper),
                new BannedUsersPlugin(commsContainer.RoomHub, staticContainer.RealmData, bannedSceneController, staticContainer.LoadingStatus, includeBannedUsersFromScene),
                new SmartWearablesGlobalPlugin(wearableContainer.WearableCatalog,
                    wearableContainer.BackpackEventBus,
                    staticContainer.PortableExperiencesController,
                    staticContainer.ScenesCache,
                    staticContainer.SmartWearableCache,
                    assetsProvisioner,
                    staticContainer.LoadingStatus,
                    uiShellContainer.MvcManager,
                    wearableContainer.ThumbnailProvider,
                    identityCache),
                new AvatarLocomotionOverridesGlobalPlugin(),
                new JumpIndicatorPlugin(assetsProvisioner),
                new SpringBonesPlugin(springBoneSimulationSettings),
                new EnsureClockSyncPlugin(realmNavigator, uiShellContainer.MvcManager, bootstrapContainer.RealmClock, staticContainer.WebRequestsContainer.WebRequestController, bootstrapContainer.DecentralandUrlsSource),
            };

            if (donationsService.DonationFeatureEnabled)
                globalPlugins.Add(new DonationsPlugin(
                    uiShellContainer.MvcManager,
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
                globalPlugins.Add(new DuplicateIdentityPlugin(commsContainer.RoomHub, uiShellContainer.MvcManager, assetsProvisioner));

            // No comms/internet popup while developing against a local scene.
            if (!localSceneDevelopment)
                globalPlugins.Add(new MultiplayerConnectionWatchdogPlugin(
                    commsContainer.RoomHub,
                    multiplayerContainer.PulseTransport,
                    staticContainer.WebRequestsContainer.WebRequestController,
                    uiShellContainer.MvcManager,
                    bootstrapContainer.DecentralandUrlsSource));

            // ReSharper disable once MethodHasAsyncOverloadWithCancellation
            if (FeaturesRegistry.Instance.IsEnabled(FeatureId.VOICE_CHAT))
                globalPlugins.Add(
                    new VoiceChatPlugin(
                        commsContainer.RoomHub,
                        uiShellContainer.MainUIView.ChatMainView.VoiceChatPanelView,
                        voiceChatContainer,
                        profileContainer.ProfileRepositoryWrapper,
                        commsContainer.EntityParticipantTable,
                        globalWorld,
                        playerEntity,
                        communitiesContainer.DataProvider,
                        staticContainer.ImageControllerProvider,
                        assetsProvisioner,
                        chatContainer.ChatSharedAreaEventBus,
                        debugBuilder,
                        staticContainer.LoadingStatus,
                        staticContainer.ScenesCache,
                        staticContainer.SceneRestrictionBusController,
                        uiShellContainer.MainUIView.SidebarView.NearbyVoiceChatButton,
                        uiShellContainer.MainUIView.SidebarView.NearbyVoiceWidget,
                        uiShellContainer.MainUIView.SidebarView.NearbyVoiceTip,
                        bootstrapContainer.VolumeBus,
                        userBlockingCache,
                        voiceChatContainer.NearbyMuteService,
                        voiceChatContainer.NearbyStateModel)
                );

            if (!appArgs.HasFlagWithValueFalse(AppArgsFlags.LANDSCAPE_TERRAIN_ENABLED))
                globalPlugins.Add(terrainContainer.CreatePlugin(staticContainer, bootstrapContainer, mapRendererContainer, debugBuilder));

            if (localSceneDevelopment)
                globalPlugins.Add(new LocalSceneDevelopmentPlugin(realmContainer.ReloadSceneController, realmUrls));
            else
            {
                globalPlugins.Add(lodContainer.LODPlugin);
                globalPlugins.Add(lodContainer.RoadPlugin);
            }

            if (FeaturesRegistry.Instance.IsEnabled(FeatureId.LOCAL_SCENE_DEVELOPMENT) || FeaturesRegistry.Instance.IsEnabled(FeatureId.SELF_PREVIEW_BUILDER_COLLECTIONS))
                globalPlugins.Add(new GlobalGLTFLoadingPlugin(staticContainer.WebRequestsContainer.WebRequestController, staticContainer.RealmData, wearableContainer.BuilderContentURL.Value, localSceneDevelopment, staticContainer.ComponentsContainer.ComponentPoolsRegistry.RootContainerTransform()));

            globalPlugins.AddRange(staticContainer.SharedPlugins);

            if (includeFriends)
            {
                var friendsContainer = new FriendsContainer(
                    uiShellContainer.MainUIView,
                    uiShellContainer.MvcManager,
                    assetsProvisioner,
                    identityCache,
                    profilesRepository,
                    staticContainer.LoadingStatus,
                    staticContainer.InputBlock,
                    profileContainer.SelfProfile,
                    uiShellContainer.PassportBridge,
                    placesAndEventsContainer.OnlineUsersProvider,
                    realmNavigator,
                    socialServiceContainer.EventBus,
                    friendsEventBus,
                    friendsServices!,
                    userBlockingCache,
                    profileContainer.ProfileRepositoryWrapper,
                    voiceChatContainer.VoiceChatOrchestrator,
                    bootstrapContainer.WebBrowser,
                    bootstrapContainer.DecentralandUrlsSource
                );

                globalPlugins.Add(friendsContainer);
            }

            if (includeCameraReel)
                globalPlugins.Add(new InWorldCameraPlugin(
                    profileContainer.SelfProfile,
                    staticContainer.RealmData,
                    playerEntity,
                    placesAndEventsContainer.PlacesAPIService,
                    staticContainer.CharacterContainer.CharacterObject,
                    coroutineRunner,
                    cameraReelContainer.StorageService,
                    cameraReelContainer.StorageService,
                    uiShellContainer.MvcManager,
                    uiShellContainer.Clipboard,
                    bootstrapContainer.DecentralandUrlsSource,
                    webBrowser,
                    profilesRepository,
                    realmNavigator,
                    assetsProvisioner,
                    wearableContainer.WearableCatalog,
                    wearableContainer.WearablesProvider,
                    uiShellContainer.Cursor,
                    globalWorld,
                    debugBuilder,
                    dynamicSettings.NametagsData,
                    profileContainer.ProfileRepositoryWrapper,
                    identityCache,
                    wearableContainer.ThumbnailProvider,
                    cameraReelContainer.GalleryEventBus
                ));

            if (includeMarketplaceCredits)
            {
                globalPlugins.Add(new MarketplaceCreditsPlugin(
                    uiShellContainer.MainUIView,
                    assetsProvisioner,
                    webBrowser,
                    staticContainer.InputBlock,
                    profileContainer.SelfProfile,
                    staticContainer.WebRequestsContainer.WebRequestController,
                    bootstrapContainer.DecentralandUrlsSource,
                    uiShellContainer.MvcManager,
                    staticContainer.RealmData,
                    identityCache,
                    staticContainer.LoadingStatus,
                    hyperlinkTextFormatter,
                    staticContainer.ImageControllerProvider));
            }

            if (communitiesContainer.IncludeCommunities)
                globalPlugins.Add(new CommunitiesPlugin(
                    uiShellContainer.MvcManager,
                    assetsProvisioner,
                    staticContainer.InputBlock,
                    cameraReelContainer.StorageService,
                    cameraReelContainer.ScreenshotsStorage,
                    profileContainer.ProfileRepositoryWrapper,
                    friendsServices?.FriendsService,
                    communitiesContainer.DataProvider,
                    staticContainer.WebRequestsContainer.WebRequestController,
                    placesAndEventsContainer.PlacesAPIService,
                    profileContainer.SelfProfile,
                    realmNavigator,
                    uiShellContainer.Clipboard,
                    webBrowser,
                    placesAndEventsContainer.EventsApiService,
                    chatContainer.ChatEventBus,
                    cameraReelContainer.GalleryEventBus,
                    communitiesContainer.EventBus,
                    socialServiceContainer.socialServicesRPC,
                    profilesRepository,
                    bootstrapContainer.DecentralandUrlsSource,
                    identityCache,
                    voiceChatContainer.VoiceChatOrchestrator,
                    bootstrapContainer.Analytics.Controller,
                    placesAndEventsContainer.HomePlaceEventBus,
                    socialServiceContainer.EventBus,
                    realmNavigatorContainer.WorldPermissionsService));

            if (dynamicWorldParams.EnableAnalytics)
                globalPlugins.Add(new AnalyticsPlugin(
                        bootstrapContainer.Analytics.Controller,
                        staticContainer.Profiler,
                        staticContainer.LoadingStatus,
                        staticContainer.RealmData,
                        staticContainer.MainPlayerAvatarBaseProxy,
                        identityCache,
                        debugBuilder,
                        cameraReelContainer.StorageService,
                        commsContainer.EntityParticipantTable,
                        staticContainer.ScenesCache,
                        chatContainer.ChatEventBus,
                        chatContainer.TranslationSettings,
                        voiceChatContainer.NearbyStateModel,
                        voiceChatContainer.NearbyMuteService
                    )
                );

            if (localSceneDevelopment || appArgs.HasFlag(AppArgsFlags.SCENE_CONSOLE))
                globalPlugins.Add(new DebugMenuPlugin(
                    bootstrapContainer.DiagnosticsContainer,
                    staticContainer.InputBlock,
                    assetsProvisioner,
                    debugBuilder
                ));

            if (!localSceneDevelopment)
                globalPlugins.Add(commsContainer.CreateConnectionStatusPanelPlugin(assetsProvisioner, appArgs));

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
                commsContainer.CurrentSceneInfo,
                lodContainer.LodCache,
                lodContainer.RoadCoordinates,
                lodContainer.LODSettings,
                globalWorld,
                staticContainer.SceneReadinessReportQueue,
                profilesRepository,
                bootstrapContainer.UseRemoteAssetBundles,
                lodContainer.RoadAssetsPool,
                staticContainer.SceneLoadingLimit,
                dynamicWorldParams.StartParcel,
                bootstrapContainer.Analytics.EntitiesAnalytics
            );

            var container = new DynamicWorldContainer(
                uiShellContainer,
                realmContainer.RealmController,
                realmNavigator,
                globalWorldFactory,
                globalPlugins,
                worldPlugins,
                profilesRepository,
                initializationFlowContainer.InitializationFlow,
                chatContainer,
                commsContainer,
                multiplayerContainer.ProfileBroadcast,
                socialServiceContainer,
                profileContainer,
                bannedNotificationHandler,
                multiplayerContainer,
                communitiesContainer,
                voiceChatContainer
            );

            // Init itself
            await dynamicWorldDependencies.SettingsContainer.InitializePluginAsync(container, ct)!.ThrowOnFail();

            return (container, true);
        }
    }
}
