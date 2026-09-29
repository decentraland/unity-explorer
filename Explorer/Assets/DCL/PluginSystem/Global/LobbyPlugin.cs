using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Backpack;
using DCL.Browser;
using DCL.CharacterPreview;
using DCL.Clipboard;
using DCL.Communities;
using DCL.Credits;
using DCL.DebugUtilities;
using DCL.Diagnostics;
using DCL.Events;
using DCL.EventsApi;
using DCL.Friends;
using DCL.Input;
using DCL.Lobby;
using DCL.MapRenderer.MapLayers.HomeMarker;
using DCL.MarketplaceCredits;
using DCL.Multiplayer.Connectivity;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Notifications;
using DCL.Notifications.NotificationsMenu;
using DCL.Passport;
using DCL.PlacesAPIService;
using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.RealmNavigation;
using DCL.UI;
using DCL.UI.Credits;
using DCL.UI.ProfileElements;
using DCL.UI.Profiles;
using DCL.UI.Profiles.Helpers;
using DCL.UserInAppInitializationFlow;
using DCL.Utilities.Extensions;
using DCL.Web3.Authenticators;
using DCL.Web3.Identities;
using DCL.WebRequests;
using ECS;
using ECS.SceneLifeCycle.Realm;
using MVC;
using System.Threading;
using Object = UnityEngine.Object;

namespace DCL.PluginSystem.Global
{
    public partial class LobbyPlugin : IDCLGlobalPlugin<LobbyPlugin.LobbyPluginSettings>
    {
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly IMVCManager mvcManager;
        private readonly IInputBlock inputBlock;
        private readonly IReadOnlyLoadingStatus loadingStatus;
        private readonly IDebugContainerBuilder debugContainerBuilder;
        private readonly ISelfProfile selfProfile;
        private readonly ProfileChangesBus profileChangesBus;
        private readonly ICharacterPreviewFactory characterPreviewFactory;
        private readonly CharacterPreviewEventBus characterPreviewEventBus;
        private readonly Arch.Core.World world;
        private readonly IPlacesAPIService placesAPIService;
        private readonly IRealmData realmData;
        private readonly IHomePlaceSource homePlace;
        private readonly HttpEventsApiService eventsApiService;
        private readonly IRealmNavigator realmNavigator;
        private readonly IDecentralandUrlsSource decentralandUrlsSource;
        private readonly ISystemClipboard clipboard;
        private readonly StartParcel startParcel;
        private readonly IWebRequestController webRequestController;
        private readonly IWeb3IdentityCache identityCache;
        private readonly IProfileRepository profileRepository;
        private readonly IProfileCache profileCache;
        private readonly ProfileRepositoryWrapper profileRepositoryWrapper;
        private readonly IPassportBridge passportBridge;
        private readonly Entity playerEntity;
        private readonly UnityAppWebBrowser webBrowser;
        private readonly ICompositeWeb3Provider web3Authenticator;
        private readonly IUserInAppInitializationFlow userInAppInitializationFlow;
        private readonly MarketplaceCreditsAPIClient marketplaceCreditsAPIClient;
        private readonly NotificationsRequestController notificationsRequestController;
        private readonly FriendsConnectivityStatusTracker? friendsConnectivity;
        private readonly IOnlineUsersProvider onlineUsersProvider;

        private LobbyController? lobbyController;
        private LobbyStage? lobbyStage;
        private LobbyFriendsPresenter? friendsPresenter;
        private SidebarProfileButtonPresenter? profileButtonPresenter;
        private ProfileMenuController<LobbyPopupParameter>? profileMenuController;
        private NotificationsPanelController<LobbyPopupParameter>? notificationsPanelController;
        private ICreditsPanelController creditsPanelController = new NullCreditsPanelController();

        public LobbyPlugin(
            IAssetsProvisioner assetsProvisioner,
            IMVCManager mvcManager,
            IInputBlock inputBlock,
            IReadOnlyLoadingStatus loadingStatus,
            IDebugContainerBuilder debugContainerBuilder,
            ISelfProfile selfProfile,
            ProfileChangesBus profileChangesBus,
            ICharacterPreviewFactory characterPreviewFactory,
            CharacterPreviewEventBus characterPreviewEventBus,
            Arch.Core.World world,
            IPlacesAPIService placesAPIService,
            IRealmData realmData,
            IHomePlaceSource homePlace,
            HttpEventsApiService eventsApiService,
            IRealmNavigator realmNavigator,
            IDecentralandUrlsSource decentralandUrlsSource,
            ISystemClipboard clipboard,
            StartParcel startParcel,
            IWebRequestController webRequestController,
            IWeb3IdentityCache identityCache,
            IProfileRepository profileRepository,
            IProfileCache profileCache,
            ProfileRepositoryWrapper profileRepositoryWrapper,
            IPassportBridge passportBridge,
            Entity playerEntity,
            UnityAppWebBrowser webBrowser,
            ICompositeWeb3Provider web3Authenticator,
            IUserInAppInitializationFlow userInAppInitializationFlow,
            MarketplaceCreditsAPIClient marketplaceCreditsAPIClient,
            NotificationsRequestController notificationsRequestController,
            FriendsConnectivityStatusTracker? friendsConnectivity,
            IOnlineUsersProvider onlineUsersProvider)
        {
            this.assetsProvisioner = assetsProvisioner;
            this.mvcManager = mvcManager;
            this.inputBlock = inputBlock;
            this.loadingStatus = loadingStatus;
            this.debugContainerBuilder = debugContainerBuilder;
            this.selfProfile = selfProfile;
            this.profileChangesBus = profileChangesBus;
            this.characterPreviewFactory = characterPreviewFactory;
            this.characterPreviewEventBus = characterPreviewEventBus;
            this.world = world;
            this.placesAPIService = placesAPIService;
            this.realmData = realmData;
            this.homePlace = homePlace;
            this.eventsApiService = eventsApiService;
            this.realmNavigator = realmNavigator;
            this.decentralandUrlsSource = decentralandUrlsSource;
            this.clipboard = clipboard;
            this.startParcel = startParcel;
            this.webRequestController = webRequestController;
            this.identityCache = identityCache;
            this.profileRepository = profileRepository;
            this.profileCache = profileCache;
            this.profileRepositoryWrapper = profileRepositoryWrapper;
            this.passportBridge = passportBridge;
            this.playerEntity = playerEntity;
            this.webBrowser = webBrowser;
            this.web3Authenticator = web3Authenticator;
            this.userInAppInitializationFlow = userInAppInitializationFlow;
            this.marketplaceCreditsAPIClient = marketplaceCreditsAPIClient;
            this.notificationsRequestController = notificationsRequestController;
            this.friendsConnectivity = friendsConnectivity;
            this.onlineUsersProvider = onlineUsersProvider;
        }

        public void Dispose()
        {
            // The lobby, profile menu and notifications controllers are registered in the MVC manager, which disposes them itself
            if (lobbyStage != null)
                Object.Destroy(lobbyStage.gameObject);

            friendsPresenter?.Dispose();
            profileButtonPresenter?.Dispose();
            creditsPanelController.Dispose();
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments) { }

        public async UniTask InitializeAsync(LobbyPluginSettings settings, CancellationToken ct)
        {
            LobbyView prefab = (await assetsProvisioner.ProvideMainAssetAsync(settings.LobbyPrefab, ct: ct)).Value;
            NotificationIconTypes notificationIconTypes = (await assetsProvisioner.ProvideMainAssetAsync(settings.NotificationIconTypes, ct)).Value;
            NotificationDefaultThumbnails notificationDefaultThumbnails = (await assetsProvisioner.ProvideMainAssetAsync(settings.NotificationDefaultThumbnails, ct)).Value;
            NftTypeIconSO rarityBackgroundMapping = await assetsProvisioner.ProvideMainAssetValueAsync(settings.RarityColorMappings, ct);

            // One stage for the whole session: it is parked at the preview position and only active while the lobby is shown
            lobbyStage = Object.Instantiate((await assetsProvisioner.ProvideMainAssetAsync(settings.StagePrefab, ct: ct)).Value);
            lobbyStage.gameObject.SetActive(false);

            // The top-bar presenters bind to the live view, so it is instantiated up front instead of lazily on first show
            ControllerBase<LobbyView, LobbyParameter>.ViewFactoryMethod viewFactory = LobbyController.Preallocate(prefab, null, out LobbyView lobbyView);

            profileButtonPresenter = new SidebarProfileButtonPresenter(lobbyView.ProfileWidgetView, identityCache, profileRepository, profileChangesBus);

            profileMenuController = new ProfileMenuController<LobbyPopupParameter>(() => lobbyView.ProfileMenuView,
                identityCache,
                world,
                playerEntity,
                webBrowser,
                web3Authenticator,
                userInAppInitializationFlow,
                profileCache,
                passportBridge,
                profileRepositoryWrapper);

            // The lobby has its own panel instance: the sidebar's one lives inside the sidebar view, which is inactive until the world is loaded
            notificationsPanelController = new NotificationsPanelController<LobbyPopupParameter>(() => lobbyView.NotificationsMenuView,
                notificationsRequestController,
                notificationIconTypes,
                notificationDefaultThumbnails,
                webRequestController,
                rarityBackgroundMapping,
                identityCache,
                profileRepositoryWrapper,
                mvcManager);

            // Without connectivity statuses there is nothing to list: the section stays hidden
            friendsPresenter = friendsConnectivity != null
                ? new LobbyFriendsPresenter(lobbyView.FriendsSection, friendsConnectivity, onlineUsersProvider, placesAPIService, passportBridge)
                : null;

            // The upcoming event cards are the Explore panel's, so their Interested, calendar and share actions run through the same controller
            var eventCardActions = new EventCardActionsController(eventsApiService, webBrowser, realmNavigator, clipboard, decentralandUrlsSource);

            lobbyController = new LobbyController(viewFactory, inputBlock, loadingStatus, mvcManager,
                selfProfile, profileChangesBus, characterPreviewFactory, characterPreviewEventBus, settings.AvatarSettings, lobbyStage, world,
                placesAPIService, realmData, homePlace, eventsApiService, eventCardActions, realmNavigator, decentralandUrlsSource, startParcel, new ThumbnailLoader(new SpriteCache(webRequestController)),
                profileButtonPresenter, friendsPresenter);

            mvcManager.RegisterController(lobbyController);
            mvcManager.RegisterController(profileMenuController);
            mvcManager.RegisterController(notificationsPanelController);

            EnableCreditsPanelAsync(lobbyView.CreditsPanelView, ct)
               .SuppressToResultAsync(ReportCategory.CREDITS_PURCHASE)
               .Forget();

            debugContainerBuilder
               .TryAddWidget("Lobby")?
               .AddSingleButton("Open", () => mvcManager.ShowAndForget(LobbyController.IssueCommand(new LobbyParameter(isStartup: false))));
        }

        private async UniTask EnableCreditsPanelAsync(CreditsPanelView view, CancellationToken ct)
        {
            creditsPanelController = await CreditsPanelSetup.EnableIfUserAllowedAsync(view, marketplaceCreditsAPIClient, profileChangesBus, identityCache, mvcManager, ct);
        }
    }
}
