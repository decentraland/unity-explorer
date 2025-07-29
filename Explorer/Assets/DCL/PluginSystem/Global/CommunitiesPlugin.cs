using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Browser;
using DCL.Chat.EventBus;
using DCL.Input;
using DCL.Clipboard;
using DCL.Communities;
using DCL.Communities.CommunitiesCard;
using DCL.Communities.CommunityCreation;
using DCL.Communities.EventInfo;
using DCL.EventsApi;
using DCL.Friends;
using DCL.InWorldCamera.CameraReelStorageService;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.NotificationsBusController.NotificationsBus;
using DCL.PlacesAPIService;
using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.SocialService;
using DCL.UI.Profiles.Helpers;
using DCL.UI.SharedSpaceManager;
using DCL.Utilities;
using DCL.Web3.Identities;
using DCL.WebRequests;
using ECS.SceneLifeCycle.Realm;
using MVC;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DCL.PluginSystem.Global
{
    public class CommunitiesPlugin : IDCLGlobalPlugin<CommunitiesPluginSettings>
    {
        private readonly IMVCManager mvcManager;
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly IInputBlock inputBlock;
        private readonly ICameraReelStorageService cameraReelStorageService;
        private readonly ICameraReelScreenshotsStorage cameraReelScreenshotsStorage;
        private readonly ProfileRepositoryWrapper profileRepositoryWrapper;
        private readonly ObjectProxy<IFriendsService> friendServiceProxy;
        private readonly CommunitiesDataProvider communitiesDataProvider;
        private readonly IWebRequestController webRequestController;
        private readonly IPlacesAPIService placesAPIService;
        private readonly ISelfProfile selfProfile;
        private readonly IRealmNavigator realmNavigator;
        private readonly ISystemClipboard clipboard;
        private readonly IWebBrowser webBrowser;
        private readonly IEventsApiService eventsApiService;
        private readonly ISharedSpaceManager sharedSpaceManager;
        private readonly IChatEventBus chatEventBus;
        private readonly IRPCCommunitiesService rpcCommunitiesService;
        private readonly NotificationHandler notificationHandler;
        private readonly LambdasProfilesProvider lambdasProfilesProvider;
        private readonly IDecentralandUrlsSource decentralandUrlsSource;
        private readonly IWeb3IdentityCache web3IdentityCache;

        private CommunityCardController? communityCardController;
        private CommunityCreationEditionController? communityCreationEditionController;
        private EventInfoController? eventInfoController;

        public CommunitiesPlugin(
            IMVCManager mvcManager,
            IAssetsProvisioner assetsProvisioner,
            IInputBlock inputBlock,
            ICameraReelStorageService cameraReelStorageService,
            ICameraReelScreenshotsStorage cameraReelScreenshotsStorage,
            ProfileRepositoryWrapper profileDataProvider,
            ObjectProxy<IFriendsService> friendServiceProxy,
            CommunitiesDataProvider communitiesDataProvider,
            IWebRequestController webRequestController,
            IPlacesAPIService placesAPIService,
            ISelfProfile selfProfile,
            IRealmNavigator realmNavigator,
            ISystemClipboard clipboard,
            IWebBrowser webBrowser,
            IEventsApiService eventsApiService,
            ISharedSpaceManager sharedSpaceManager,
            IChatEventBus chatEventBus,
            CommunitiesEventBus communitiesEventBus,
            IRPCSocialServices rpcSocialServices,
            INotificationsBusController notificationsBusController,
            LambdasProfilesProvider lambdasProfilesProvider,
            IDecentralandUrlsSource decentralandUrlsSource,
            IWeb3IdentityCache web3IdentityCache)
        {
            this.mvcManager = mvcManager;
            this.assetsProvisioner = assetsProvisioner;
            this.inputBlock = inputBlock;
            this.cameraReelStorageService = cameraReelStorageService;
            this.cameraReelScreenshotsStorage = cameraReelScreenshotsStorage;
            this.profileRepositoryWrapper = profileDataProvider;
            this.friendServiceProxy = friendServiceProxy;
            this.communitiesDataProvider = communitiesDataProvider;
            this.webRequestController = webRequestController;
            this.placesAPIService = placesAPIService;
            this.selfProfile = selfProfile;
            this.realmNavigator = realmNavigator;
            this.clipboard = clipboard;
            this.webBrowser = webBrowser;
            this.eventsApiService = eventsApiService;
            this.sharedSpaceManager = sharedSpaceManager;
            this.chatEventBus = chatEventBus;
            this.lambdasProfilesProvider = lambdasProfilesProvider;
            this.decentralandUrlsSource = decentralandUrlsSource;
            this.web3IdentityCache = web3IdentityCache;
            rpcCommunitiesService = new RPCCommunitiesService(rpcSocialServices, communitiesEventBus);
            notificationHandler = new NotificationHandler(notificationsBusController, mvcManager, realmNavigator);
        }

        public void Dispose()
        {
            communityCardController?.Dispose();
            communityCreationEditionController?.Dispose();
            eventInfoController?.Dispose();
            notificationHandler.Dispose();
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
        }

        public async UniTask InitializeAsync(CommunitiesPluginSettings settings, CancellationToken ct)
        {
            CommunityCardView communityCardViewAsset = (await assetsProvisioner.ProvideMainAssetValueAsync(settings.CommunityCardPrefab, ct: ct)).GetComponent<CommunityCardView>();
            ControllerBase<CommunityCardView, CommunityCardParameter>.ViewFactoryMethod viewFactoryMethod = CommunityCardController.Preallocate(communityCardViewAsset, null, out CommunityCardView communityCardView);

            communityCardController = new CommunityCardController(viewFactoryMethod,
                mvcManager,
                cameraReelStorageService,
                cameraReelScreenshotsStorage,
                friendServiceProxy,
                communitiesDataProvider,
                webRequestController,
                profileRepositoryWrapper,
                placesAPIService,
                realmNavigator,
                clipboard,
                webBrowser,
                eventsApiService,
                sharedSpaceManager,
                chatEventBus,
                decentralandUrlsSource,
                web3IdentityCache,
                lambdasProfilesProvider);

            mvcManager.RegisterController(communityCardController);

            CommunityCreationEditionView communityCreationEditionViewAsset = (await assetsProvisioner.ProvideMainAssetValueAsync(settings.CommunityCreationEditionPrefab, ct: ct)).GetComponent<CommunityCreationEditionView>();
            ControllerBase<CommunityCreationEditionView, CommunityCreationEditionParameter>.ViewFactoryMethod communityCreationEditionViewFactoryMethod = CommunityCreationEditionController.Preallocate(communityCreationEditionViewAsset, null, out CommunityCreationEditionView communityCreationEditionView);
            communityCreationEditionController = new CommunityCreationEditionController(
                communityCreationEditionViewFactoryMethod,
                webBrowser,
                inputBlock,
                communitiesDataProvider,
                placesAPIService,
                selfProfile,
                mvcManager,
                lambdasProfilesProvider);
            mvcManager.RegisterController(communityCreationEditionController);

            EventInfoView eventInfoViewAsset = (await assetsProvisioner.ProvideMainAssetValueAsync(settings.EventInfoPrefab, ct: ct)).GetComponent<EventInfoView>();
            var eventInfoViewFactory = EventInfoController.CreateLazily(eventInfoViewAsset, null);
            eventInfoController = new EventInfoController(eventInfoViewFactory,
                webRequestController,
                clipboard,
                webBrowser,
                eventsApiService,
                realmNavigator);
            mvcManager.RegisterController(eventInfoController);

            rpcCommunitiesService.SubscribeToConnectivityStatusAsync(ct).Forget();
        }
    }

    [Serializable]
    public class CommunitiesPluginSettings : IDCLPluginSettings
    {
        [field: Header("Community Card")]
        [field: SerializeField] internal AssetReferenceGameObject CommunityCardPrefab { get; private set; }

        [field: Header("Community Creation Edition Wizard")]
        [field: SerializeField] internal AssetReferenceGameObject CommunityCreationEditionPrefab { get; private set; }

        [field: Header("Event info panel")]
        [field: SerializeField] internal AssetReferenceGameObject EventInfoPrefab { get; private set; }
    }
}
