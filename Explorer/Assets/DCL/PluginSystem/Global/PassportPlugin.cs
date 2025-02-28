using Arch.Core;
using Arch.SystemGroups;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.AvatarRendering.Wearables;
using DCL.Backpack;
using DCL.BadgesAPIService;
using DCL.Browser;
using DCL.CharacterPreview;
using DCL.Chat;
using DCL.Clipboard;
using DCL.Friends;
using DCL.Input;
using DCL.InWorldCamera.CameraReelStorageService;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Multiplayer.Connectivity;
using DCL.Multiplayer.Profiles.Poses;
using DCL.NotificationsBusController.NotificationsBus;
using DCL.Passport;
using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.Utilities;
using DCL.Web3.Identities;
using DCL.WebRequests;
using ECS;
using ECS.SceneLifeCycle.Realm;
using MVC;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DCL.PluginSystem.Global
{
    public class PassportPlugin : IDCLGlobalPlugin<PassportPlugin.PassportSettings>
    {
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly IMVCManager mvcManager;
        private readonly ICursor cursor;
        private readonly IProfileRepository profileRepository;
        private readonly ICharacterPreviewFactory characterPreviewFactory;
        private readonly IRealmData realmData;
        private readonly URLDomain assetBundleURL;
        private readonly IWebRequestController webRequestController;
        private readonly CharacterPreviewEventBus characterPreviewEventBus;
        private readonly ISelfProfile selfProfile;
        private readonly IWebBrowser webBrowser;
        private readonly IDecentralandUrlsSource decentralandUrlsSource;
        private readonly BadgesAPIClient badgesAPIClient;
        private readonly IInputBlock inputBlock;
        private readonly IRemoteMetadata remoteMetadata;
        private readonly INotificationsBusController notificationsBusController;
        private readonly ICameraReelStorageService cameraReelStorageService;
        private readonly ICameraReelScreenshotsStorage cameraReelScreenshotsStorage;
        private readonly Arch.Core.World world;
        private readonly Entity playerEntity;
        private readonly bool enableCameraReel;
        private readonly ObjectProxy<IFriendsService> friendsService;
        private readonly ObjectProxy<IFriendsConnectivityStatusTracker> friendOnlineStatusCache;
        private readonly ISystemClipboard systemClipboard;
        private readonly IProfileThumbnailCache profileThumbnailCache;
        private readonly IOnlineUsersProvider onlineUsersProvider;
        private readonly IRealmNavigator realmNavigator;
        private readonly IWeb3IdentityCache web3IdentityCache;
        private readonly bool enableFriends;
        private readonly bool includeUserBlocking;

        private PassportController? passportController;

        public PassportPlugin(
            IAssetsProvisioner assetsProvisioner,
            IMVCManager mvcManager,
            ICursor cursor,
            IProfileRepository profileRepository,
            ICharacterPreviewFactory characterPreviewFactory,
            IRealmData realmData,
            URLDomain assetBundleURL,
            IWebRequestController webRequestController,
            CharacterPreviewEventBus characterPreviewEventBus,
            ISelfProfile selfProfile,
            IWebBrowser webBrowser,
            IDecentralandUrlsSource decentralandUrlsSource,
            BadgesAPIClient badgesAPIClient,
            INotificationsBusController notificationsBusController,
            IInputBlock inputBlock,
            IRemoteMetadata remoteMetadata,
            ICameraReelStorageService cameraReelStorageService,
            ICameraReelScreenshotsStorage cameraReelScreenshotsStorage,
            Arch.Core.World world,
            Entity playerEntity,
            bool enableCameraReel,
            ObjectProxy<IFriendsService> friendsService,
            ObjectProxy<IFriendsConnectivityStatusTracker> friendOnlineStatusCacheProxy,
            ISystemClipboard systemClipboard,
            IProfileThumbnailCache profileThumbnailCache,
            IOnlineUsersProvider onlineUsersProvider,
            IRealmNavigator realmNavigator,
            IWeb3IdentityCache web3IdentityCache,
            bool enableFriends,
            bool includeUserBlocking
        )
        {
            this.assetsProvisioner = assetsProvisioner;
            this.mvcManager = mvcManager;
            this.cursor = cursor;
            this.profileRepository = profileRepository;
            this.characterPreviewFactory = characterPreviewFactory;
            this.realmData = realmData;
            this.assetBundleURL = assetBundleURL;
            this.webRequestController = webRequestController;
            this.characterPreviewEventBus = characterPreviewEventBus;
            this.selfProfile = selfProfile;
            this.webBrowser = webBrowser;
            this.decentralandUrlsSource = decentralandUrlsSource;
            this.badgesAPIClient = badgesAPIClient;
            this.inputBlock = inputBlock;
            this.remoteMetadata = remoteMetadata;
            this.notificationsBusController = notificationsBusController;
            this.world = world;
            this.playerEntity = playerEntity;
            this.cameraReelStorageService = cameraReelStorageService;
            this.cameraReelScreenshotsStorage = cameraReelScreenshotsStorage;
            this.enableCameraReel = enableCameraReel;
            this.friendsService = friendsService;
            this.friendOnlineStatusCache = friendOnlineStatusCacheProxy;
            this.systemClipboard = systemClipboard;
            this.profileThumbnailCache = profileThumbnailCache;
            this.onlineUsersProvider = onlineUsersProvider;
            this.realmNavigator = realmNavigator;
            this.web3IdentityCache = web3IdentityCache;
            this.enableFriends = enableFriends;
            this.includeUserBlocking = includeUserBlocking;
        }

        public void Dispose()
        {
            passportController?.Dispose();
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments) { }

        public async UniTask InitializeAsync(PassportSettings passportSettings, CancellationToken ct)
        {
            (NFTColorsSO rarityColorMappings, NftTypeIconSO categoryIconsMapping, NftTypeIconSO rarityBackgroundsMapping, NftTypeIconSO rarityInfoPanelBackgroundsMapping) = await UniTask.WhenAll(
                assetsProvisioner.ProvideMainAssetValueAsync(passportSettings.RarityColorMappings, ct),
                assetsProvisioner.ProvideMainAssetValueAsync(passportSettings.CategoryIconsMapping, ct),
                assetsProvisioner.ProvideMainAssetValueAsync(passportSettings.RarityBackgroundsMapping, ct),
                assetsProvisioner.ProvideMainAssetValueAsync(passportSettings.RarityInfoPanelBackgroundsMapping, ct));

            PassportView chatView = (await assetsProvisioner.ProvideMainAssetAsync(passportSettings.PassportPrefab, ct)).Value.GetComponent<PassportView>();

            var thumbnailProvider = new ECSThumbnailProvider(realmData, world, assetBundleURL, webRequestController);

            passportController = new PassportController(
                PassportController.CreateLazily(chatView, null),
                cursor,
                profileRepository,
                characterPreviewFactory,
                rarityBackgroundsMapping,
                rarityColorMappings,
                categoryIconsMapping,
                characterPreviewEventBus,
                mvcManager,
                selfProfile,
                world,
                playerEntity,
                thumbnailProvider,
                webBrowser,
                decentralandUrlsSource,
                badgesAPIClient,
                webRequestController,
                inputBlock,
                notificationsBusController,
                remoteMetadata,
                cameraReelStorageService,
                cameraReelScreenshotsStorage,
                friendsService,
                friendOnlineStatusCache,
                systemClipboard,
                profileThumbnailCache,
                onlineUsersProvider,
                realmNavigator,
                web3IdentityCache,
                passportSettings.GridLayoutFixedColumnCount,
                passportSettings.ThumbnailHeight,
                passportSettings.ThumbnailWidth,
                enableCameraReel,
                enableFriends,
                includeUserBlocking
            );

            mvcManager.RegisterController(passportController);
        }

        public class PassportSettings : IDCLPluginSettings
        {
            [field: Header(nameof(PassportPlugin) + "." + nameof(PassportSettings))]
            [field: Space]
            [field: SerializeField]
            public AssetReferenceGameObject PassportPrefab;

            [field: SerializeField]
            public AssetReferenceT<NFTColorsSO> RarityColorMappings { get; set; }

            [field: SerializeField]
            public AssetReferenceT<NftTypeIconSO> CategoryIconsMapping { get; set; }

            [field: SerializeField]
            public AssetReferenceT<NftTypeIconSO> RarityBackgroundsMapping { get; set; }

            [field: SerializeField]
            public AssetReferenceT<NftTypeIconSO> RarityInfoPanelBackgroundsMapping { get; set; }

            [field: SerializeField]
            public int GridLayoutFixedColumnCount { get; private set; }

            [field: SerializeField]
            public int ThumbnailHeight { get; private set; }

            [field: SerializeField]
            public int ThumbnailWidth { get; private set; }
        }
    }
}
