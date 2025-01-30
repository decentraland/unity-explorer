using Arch.SystemGroups;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Browser;
using DCL.Clipboard;
using DCL.Friends;
using DCL.Friends.UI.FriendPanel;
using DCL.Friends.UI.Requests;
using DCL.Input;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Profiles;
using DCL.RealmNavigation;
using DCL.Profiles.Self;
using DCL.Web3.Identities;
using DCL.UI.MainUI;
using DCL.Utilities;
using DCL.WebRequests;
using MVC;
using System;
using System.Net.WebSockets;
using System.Threading;
using Utility;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DCL.PluginSystem.Global
{
    public class FriendsPlugin : IDCLGlobalPlugin<FriendsPluginSettings>
    {
        private readonly MainUIView mainUIView;
        private readonly IDecentralandUrlsSource dclUrlSource;
        private readonly IMVCManager mvcManager;
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly IWeb3IdentityCache web3IdentityCache;
        private readonly IProfileRepository profileRepository;
        private readonly ISystemClipboard systemClipboard;
        private readonly IWebRequestController webRequestController;
        private readonly ILoadingStatus loadingStatus;
        private readonly IInputBlock inputBlock;
        private readonly DCLInput dclInput;
        private readonly ISelfProfile selfProfile;
        private readonly IPassportBridge passportBridge;
        private readonly ObjectProxy<IFriendsService> friendServiceProxy;
        private readonly IProfileThumbnailCache profileThumbnailCache;
        private readonly CancellationTokenSource lifeCycleCancellationToken = new ();
        private readonly IWebBrowser webBrowser;

        private CancellationTokenSource friendServiceSubscriptionCancellationToken = new ();
        private RPCFriendsService? friendsService;
        private FriendsPanelController? friendsPanelController;

        public FriendsPlugin(
            MainUIView mainUIView,
            IDecentralandUrlsSource dclUrlSource,
            IMVCManager mvcManager,
            IAssetsProvisioner assetsProvisioner,
            IWeb3IdentityCache web3IdentityCache,
            IProfileRepository profileRepository,
            ISystemClipboard systemClipboard,
            IWebRequestController webRequestController,
            ILoadingStatus loadingStatus,
            IInputBlock inputBlock,
            DCLInput dclInput,
            ISelfProfile selfProfile,
            IPassportBridge passportBridge,
            ObjectProxy<IFriendsService> friendServiceProxy,
            IProfileThumbnailCache profileThumbnailCache)
        {
            this.mainUIView = mainUIView;
            this.dclUrlSource = dclUrlSource;
            this.mvcManager = mvcManager;
            this.assetsProvisioner = assetsProvisioner;
            this.web3IdentityCache = web3IdentityCache;
            this.profileRepository = profileRepository;
            this.systemClipboard = systemClipboard;
            this.webRequestController = webRequestController;
            this.loadingStatus = loadingStatus;
            this.inputBlock = inputBlock;
            this.dclInput = dclInput;
            this.selfProfile = selfProfile;
            this.passportBridge = passportBridge;
            this.friendServiceProxy = friendServiceProxy;
            this.profileThumbnailCache = profileThumbnailCache;
        }

        public void Dispose()
        {
            friendsPanelController?.Dispose();
            friendServiceSubscriptionCancellationToken.SafeCancelAndDispose();
            friendsService?.Dispose();
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments) { }

        public async UniTask InitializeAsync(FriendsPluginSettings settings, CancellationToken ct)
        {
            IFriendsEventBus friendEventBus = new DefaultFriendsEventBus();


            var friendsCache = new FriendsCache();

            friendsService = new RPCFriendsService(URLAddress.FromString(dclUrlSource.Url(DecentralandUrl.ApiFriends)),
                friendEventBus, web3IdentityCache, friendsCache, selfProfile);

            friendServiceProxy.SetObject(friendsService);

            // Fire and forget as this task will never finish
            var cts = CancellationTokenSource.CreateLinkedTokenSource(friendServiceSubscriptionCancellationToken.Token, ct);

            friendsService.SubscribeToIncomingFriendshipEventsAsync(cts.Token).Forget();
            friendsService.SubscribeToConnectivityStatusAsync(cts.Token).Forget();

            FriendsPanelView friendsPanelPrefab = (await assetsProvisioner.ProvideMainAssetValueAsync(settings.FriendsPanelPrefab, ct)).GetComponent<FriendsPanelView>();

            friendsPanelController = new FriendsPanelController(FriendsPanelController.Preallocate(friendsPanelPrefab, null, out FriendsPanelView panelView),
                panelView,
                mainUIView.ChatView,
                mainUIView.SidebarView.FriendRequestNotificationIndicator,
                friendsService,
                friendEventBus,
                mvcManager,
                web3IdentityCache,
                profileRepository,
                systemClipboard,
                webRequestController,
                profileThumbnailCache,
                loadingStatus,
                dclInput,
                passportBridge);

            mvcManager.RegisterController(friendsPanelController);

            var persistentFriendsOpenerController = new PersistentFriendPanelOpenerController(() => mainUIView.SidebarView.PersistentFriendsPanelOpener, mvcManager, dclInput);
            mvcManager.RegisterController(persistentFriendsOpenerController);

            FriendRequestView friendRequestPrefab = (await assetsProvisioner.ProvideMainAssetAsync(settings.FriendRequestPrefab, ct)).Value;

            var friendRequestController = new FriendRequestController(
                FriendRequestController.CreateLazily(friendRequestPrefab, null),
                web3IdentityCache, friendsService, profileRepository, webRequestController,
                inputBlock);

            mvcManager.RegisterController(friendRequestController);

            web3IdentityCache.OnIdentityCleared += DisconnectRpcClient;
            web3IdentityCache.OnIdentityChanged += ReconnectRpcClient;
        }

        private void ReconnectRpcClient()
        {
            friendServiceSubscriptionCancellationToken = friendServiceSubscriptionCancellationToken.SafeRestart();
            ReconnectRpcClientAsync(friendServiceSubscriptionCancellationToken.Token).Forget();
            return;

            async UniTaskVoid ReconnectRpcClientAsync(CancellationToken ct)
            {
                if (friendsService == null) return;

                await friendsService.DisconnectAsync(ct);

                friendsService.SubscribeToIncomingFriendshipEventsAsync(ct).Forget();
                friendsService.SubscribeToConnectivityStatusAsync(ct).Forget();
            }
        }

        private void DisconnectRpcClient()
        {
            friendServiceSubscriptionCancellationToken.SafeCancelAndDispose();
            // friendsService?.DisconnectAsync(friendServiceSubscriptionCancellationToken.Token).Forget();
        }
    }

    public class FriendsPluginSettings : IDCLPluginSettings
    {
        [field: SerializeField] public AssetReferenceGameObject FriendsPanelPrefab { get; set; } = null!;
        [field: SerializeField]
        public FriendRequestAssetReference FriendRequestPrefab { get; set; }

        [Serializable]
        public class FriendRequestAssetReference : ComponentReference<FriendRequestView>
        {
            public FriendRequestAssetReference(string guid) : base(guid) { }
        }
    }
}
