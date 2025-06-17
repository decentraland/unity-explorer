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
using DCL.EventsApi;
using DCL.Friends;
using DCL.InWorldCamera.CameraReelStorageService;
using DCL.PlacesAPIService;
using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.UI;
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
        private readonly ICommunitiesDataProvider communitiesDataProvider;
        private readonly IWebRequestController webRequestController;
        private readonly WarningNotificationView inWorldWarningNotificationView;
        private readonly INftNamesProvider nftNamesProvider;
        private readonly IPlacesAPIService placesAPIService;
        private readonly ISelfProfile selfProfile;
        private readonly IRealmNavigator realmNavigator;
        private readonly ISystemClipboard clipboard;
        private readonly IWebBrowser webBrowser;
        private readonly IEventsApiService eventsApiService;
        private readonly IWeb3IdentityCache web3IdentityCache;
        private readonly CommunityCreationEditionEventBus communityCreationEditionEventBus;
        private readonly ISharedSpaceManager sharedSpaceManager;
        private readonly IChatEventBus chatEventBus;

        private CommunityCardController? communityCardController;

        private CommunityCreationEditionController? communityCreationEditionController;

        public CommunitiesPlugin(
            IMVCManager mvcManager,
            IAssetsProvisioner assetsProvisioner,
            IInputBlock inputBlock,
            ICameraReelStorageService cameraReelStorageService,
            ICameraReelScreenshotsStorage cameraReelScreenshotsStorage,
            ProfileRepositoryWrapper profileDataProvider,
            ObjectProxy<IFriendsService> friendServiceProxy,
            ICommunitiesDataProvider communitiesDataProvider,
            IWebRequestController webRequestController,
            WarningNotificationView inWorldWarningNotificationView,
            INftNamesProvider nftNamesProvider,
            IPlacesAPIService placesAPIService,
            ISelfProfile selfProfile,
            IRealmNavigator realmNavigator,
            ISystemClipboard clipboard,
            IWebBrowser webBrowser,
            IEventsApiService eventsApiService,
            IWeb3IdentityCache web3IdentityCache,
            CommunityCreationEditionEventBus communityCreationEditionEventBus,
            ISharedSpaceManager sharedSpaceManager,
            IChatEventBus chatEventBus)
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
            this.inWorldWarningNotificationView = inWorldWarningNotificationView;
            this.nftNamesProvider = nftNamesProvider;
            this.placesAPIService = placesAPIService;
            this.selfProfile = selfProfile;
            this.realmNavigator = realmNavigator;
            this.clipboard = clipboard;
            this.webBrowser = webBrowser;
            this.eventsApiService = eventsApiService;
            this.communityCreationEditionEventBus = communityCreationEditionEventBus;
            this.web3IdentityCache = web3IdentityCache;
            this.sharedSpaceManager = sharedSpaceManager;
            this.chatEventBus = chatEventBus;
        }

        public void Dispose()
        {
            communityCardController?.Dispose();
            communityCreationEditionController?.Dispose();
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
                web3IdentityCache,
                sharedSpaceManager,
                chatEventBus);

            mvcManager.RegisterController(communityCardController);

            CommunityCreationEditionView communityCreationEditionViewAsset = (await assetsProvisioner.ProvideMainAssetValueAsync(settings.CommunityCreationEditionPrefab, ct: ct)).GetComponent<CommunityCreationEditionView>();
            ControllerBase<CommunityCreationEditionView, CommunityCreationEditionParameter>.ViewFactoryMethod communityCreationEditionViewFactoryMethod = CommunityCreationEditionController.Preallocate(communityCreationEditionViewAsset, null, out CommunityCreationEditionView communityCreationEditionView);
            communityCreationEditionController = new CommunityCreationEditionController(
                communityCreationEditionViewFactoryMethod,
                webBrowser,
                inputBlock,
                communitiesDataProvider,
                nftNamesProvider,
                placesAPIService,
                selfProfile,
                webRequestController,
                communityCreationEditionEventBus);
            mvcManager.RegisterController(communityCreationEditionController);
        }
    }

    [Serializable]
    public class CommunitiesPluginSettings : IDCLPluginSettings
    {
        [field: Header("Community Card")]
        [field: SerializeField] internal AssetReferenceGameObject CommunityCardPrefab { get; private set; }

        [field: Header("Community Creation Edition Wizard")]
        [field: SerializeField] internal AssetReferenceGameObject CommunityCreationEditionPrefab { get; private set; }
    }
}
