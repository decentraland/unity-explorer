using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Browser;
using DCL.Clipboard;
using DCL.Communities;
using DCL.Communities.CommunitiesCard;
using DCL.Friends;
using DCL.InWorldCamera.CameraReelStorageService;
using DCL.PlacesAPIService;
using DCL.UI;
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
        private readonly ICameraReelStorageService cameraReelStorageService;
        private readonly ICameraReelScreenshotsStorage cameraReelScreenshotsStorage;
        private readonly ViewDependencies viewDependencies;
        private readonly ObjectProxy<IFriendsService> friendServiceProxy;
        private readonly ICommunitiesDataProvider communitiesDataProvider;
        private readonly IWebRequestController webRequestController;
        private readonly WarningNotificationView inWorldWarningNotificationView;
        private readonly IPlacesAPIService placesAPIService;
        private readonly IRealmNavigator realmNavigator;
        private readonly ISystemClipboard clipboard;
        private readonly IWebBrowser webBrowser;

        public CommunitiesPlugin(IMVCManager mvcManager,
            IAssetsProvisioner assetsProvisioner,
            ICameraReelStorageService cameraReelStorageService,
            ICameraReelScreenshotsStorage cameraReelScreenshotsStorage,
            ViewDependencies viewDependencies,
            ObjectProxy<IFriendsService> friendServiceProxy,
            ICommunitiesDataProvider communitiesDataProvider,
            IWebRequestController webRequestController,
            WarningNotificationView inWorldWarningNotificationView,
            IPlacesAPIService placesAPIService,
            IRealmNavigator realmNavigator,
            ISystemClipboard clipboard,
            IWebBrowser webBrowser)
        {
            this.mvcManager = mvcManager;
            this.assetsProvisioner = assetsProvisioner;
            this.cameraReelStorageService = cameraReelStorageService;
            this.cameraReelScreenshotsStorage = cameraReelScreenshotsStorage;
            this.viewDependencies = viewDependencies;
            this.friendServiceProxy = friendServiceProxy;
            this.communitiesDataProvider = communitiesDataProvider;
            this.webRequestController = webRequestController;
            this.inWorldWarningNotificationView = inWorldWarningNotificationView;
            this.placesAPIService = placesAPIService;
            this.realmNavigator = realmNavigator;
            this.clipboard = clipboard;
            this.webBrowser = webBrowser;
        }

        public void Dispose()
        {
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
        }

        public async UniTask InitializeAsync(CommunitiesPluginSettings settings, CancellationToken ct)
        {
            CommunityCardView communityCardViewAsset = (await assetsProvisioner.ProvideMainAssetValueAsync(settings.CommunityCardPrefab, ct: ct)).GetComponent<CommunityCardView>();
            ControllerBase<CommunityCardView, CommunityCardParameter>.ViewFactoryMethod viewFactoryMethod = CommunityCardController.Preallocate(communityCardViewAsset, null, out CommunityCardView communityCardView);

            mvcManager.RegisterController(new CommunityCardController(viewFactoryMethod,
                mvcManager,
                cameraReelStorageService,
                cameraReelScreenshotsStorage,
                viewDependencies,
                friendServiceProxy,
                communitiesDataProvider,
                webRequestController,
                inWorldWarningNotificationView,
                placesAPIService,
                realmNavigator,
                clipboard,
                webBrowser));
        }
    }

    [Serializable]
    public class CommunitiesPluginSettings : IDCLPluginSettings
    {
        [field: Header("Community Card")]
        [field: SerializeField] internal AssetReferenceGameObject CommunityCardPrefab { get; private set; }
    }
}
