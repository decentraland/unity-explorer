using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Communities;
using DCL.Communities.CommunitiesCard;
using DCL.Friends;
using DCL.InWorldCamera.CameraReelStorageService;
using DCL.UI;
using DCL.UI.Profiles.Helpers;
using DCL.Utilities;
using DCL.WebRequests;
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
        private readonly ProfileRepositoryWrapper profileRepositoryWrapper;
        private readonly ObjectProxy<IFriendsService> friendServiceProxy;
        private readonly ICommunitiesDataProvider communitiesDataProvider;
        private readonly IWebRequestController webRequestController;
        private readonly WarningNotificationView inWorldWarningNotificationView;

        public CommunitiesPlugin(IMVCManager mvcManager,
            IAssetsProvisioner assetsProvisioner,
            ICameraReelStorageService cameraReelStorageService,
            ICameraReelScreenshotsStorage cameraReelScreenshotsStorage,
            ProfileRepositoryWrapper profileDataProvider,
            ObjectProxy<IFriendsService> friendServiceProxy,
            ICommunitiesDataProvider communitiesDataProvider,
            IWebRequestController webRequestController,
            WarningNotificationView inWorldWarningNotificationView)
        {
            this.mvcManager = mvcManager;
            this.assetsProvisioner = assetsProvisioner;
            this.cameraReelStorageService = cameraReelStorageService;
            this.cameraReelScreenshotsStorage = cameraReelScreenshotsStorage;
            this.profileRepositoryWrapper = profileDataProvider;
            this.friendServiceProxy = friendServiceProxy;
            this.communitiesDataProvider = communitiesDataProvider;
            this.webRequestController = webRequestController;
            this.inWorldWarningNotificationView = inWorldWarningNotificationView;
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
                friendServiceProxy,
                communitiesDataProvider,
                webRequestController,
                inWorldWarningNotificationView,
                profileRepositoryWrapper));
        }
    }

    [Serializable]
    public class CommunitiesPluginSettings : IDCLPluginSettings
    {
        [field: Header("Community Card")]
        [field: SerializeField] internal AssetReferenceGameObject CommunityCardPrefab { get; private set; }
    }
}
