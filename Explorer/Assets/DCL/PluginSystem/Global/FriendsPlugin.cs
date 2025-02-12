using Arch.SystemGroups;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Clipboard;
using DCL.FeatureFlags;
using DCL.Friends;
using DCL.Friends.Chat;
using DCL.Friends.UI;
using DCL.Friends.UI.FriendPanel;
using DCL.Friends.UI.PushNotifications;
using DCL.Friends.UI.Requests;
using DCL.Input;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Multiplayer.Connectivity;
using DCL.NotificationsBusController.NotificationsBus;
using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.RealmNavigation;
using DCL.UI.MainUI;
using DCL.UI.Sidebar.SidebarActionsBus;
using DCL.Utilities;
using DCL.Web3.Identities;
using DCL.WebRequests;
using ECS.SceneLifeCycle.Realm;
using Global.AppArgs;
using MVC;
using System;
using System.Threading;
using UnityEngine;
using Utility;

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
        private readonly ObjectProxy<IFriendOnlineStatusCache> friendOnlineStatusCacheProxy;
        private readonly IProfileThumbnailCache profileThumbnailCache;
        private readonly IChatLifecycleBusController chatLifecycleBusController;
        private readonly IOnlineUsersProvider onlineUsersProvider;
        private readonly IRealmNavigator realmNavigator;
        private readonly INotificationsBusController notificationsBusController;
        private readonly bool includeUserBlocking;
        private readonly IAppArgs appArgs;
        private readonly FeatureFlagsCache featureFlagsCache;
        private readonly ISidebarActionsBus sidebarActionsBus;

        private CancellationTokenSource friendServiceSubscriptionCancellationToken = new ();
        private RPCFriendsService? friendsService;
        private FriendsPanelController? friendsPanelController;
        private UnfriendConfirmationPopupController? unfriendConfirmationPopupController;

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
            ObjectProxy<IFriendOnlineStatusCache> friendOnlineStatusCacheProxy,
            IProfileThumbnailCache profileThumbnailCache,
            IChatLifecycleBusController chatLifecycleBusController,
            INotificationsBusController notificationsBusController,
            IOnlineUsersProvider onlineUsersProvider,
            IRealmNavigator realmNavigator,
            bool includeUserBlocking,
            IAppArgs appArgs,
            FeatureFlagsCache featureFlagsCache,
            ISidebarActionsBus sidebarActionsBus)
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
            this.friendOnlineStatusCacheProxy = friendOnlineStatusCacheProxy;
            this.profileThumbnailCache = profileThumbnailCache;
            this.chatLifecycleBusController = chatLifecycleBusController;
            this.onlineUsersProvider = onlineUsersProvider;
            this.realmNavigator = realmNavigator;
            this.notificationsBusController = notificationsBusController;
            this.includeUserBlocking = includeUserBlocking;
            this.appArgs = appArgs;
            this.featureFlagsCache = featureFlagsCache;
            this.sidebarActionsBus = sidebarActionsBus;
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

            friendsService = new RPCFriendsService(GetApiUrl(),
                friendEventBus, web3IdentityCache, friendsCache, selfProfile, onlineUsersProvider);

            friendServiceProxy.SetObject(friendsService);

            // We need to restart the connection to the service as credentials changes
            // since that affects which friends the user can access
            web3IdentityCache.OnIdentityCleared += DisconnectRpcClient;
            web3IdentityCache.OnIdentityChanged += ReconnectRpcClient;

            // Fire and forget as this task will never finish
            var cts = CancellationTokenSource.CreateLinkedTokenSource(friendServiceSubscriptionCancellationToken.Token, ct);
            friendsService.SubscribeToIncomingFriendshipEventsAsync(cts.Token).Forget();

            bool isConnectivityStatusEnabled = IsConnectivityStatusEnabled();

            IFriendOnlineStatusCache friendOnlineStatusCache = new FriendOnlineStatusCache(friendEventBus, isConnectivityStatusEnabled);
            friendOnlineStatusCacheProxy.SetObject(friendOnlineStatusCache);

            if (isConnectivityStatusEnabled)
                friendsService.SubscribeToConnectivityStatusAsync(cts.Token).Forget();

            friendsPanelController = new FriendsPanelController(() =>
                {
                    var panelView = mainUIView.FriendsPanelViewView;
                    panelView.gameObject.SetActive(false);
                    return panelView;
                },
                mainUIView.FriendsPanelViewView,
                chatLifecycleBusController,
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
                passportBridge,
                onlineUsersProvider,
                realmNavigator,
                friendOnlineStatusCache,
                sidebarActionsBus,
                includeUserBlocking,
                isConnectivityStatusEnabled);

            mvcManager.RegisterController(friendsPanelController);

            var persistentFriendsOpenerController = new PersistentFriendPanelOpenerController(() => mainUIView.SidebarView.PersistentFriendsPanelOpener,
                mvcManager,
                dclInput,
                notificationsBusController,
                passportBridge,
                friendsService);

            mvcManager.RegisterController(persistentFriendsOpenerController);

            FriendRequestView friendRequestPrefab = (await assetsProvisioner.ProvideMainAssetAsync(settings.FriendRequestPrefab, ct)).Value;

            var friendRequestController = new FriendRequestController(
                FriendRequestController.CreateLazily(friendRequestPrefab, null),
                web3IdentityCache, friendsService, profileRepository,
                inputBlock, profileThumbnailCache);

            mvcManager.RegisterController(friendRequestController);

            var friendPushNotificationController = new FriendPushNotificationController(() => mainUIView.FriendPushNotificationView,
                friendOnlineStatusCache,
                profileThumbnailCache);

            mvcManager.RegisterController(friendPushNotificationController);

            UnfriendConfirmationPopupView unfriendConfirmationPopupPrefab = (await assetsProvisioner.ProvideMainAssetAsync(settings.UnfriendConfirmationPrefab, ct)).Value;

            unfriendConfirmationPopupController = new UnfriendConfirmationPopupController(
                UnfriendConfirmationPopupController.CreateLazily(unfriendConfirmationPopupPrefab, null),
                friendsService, profileRepository, profileThumbnailCache);

            mvcManager.RegisterController(unfriendConfirmationPopupController);
        }

        private URLAddress GetApiUrl()
        {
            string url = dclUrlSource.Url(DecentralandUrl.ApiFriends);

            if (appArgs.TryGetValue(AppArgsFlags.FRIENDS_API_URL, out string? urlFromArgs))
                url = urlFromArgs!;

            return URLAddress.FromString(url);
        }

        private bool IsConnectivityStatusEnabled() =>
            appArgs.HasFlag(AppArgsFlags.FRIENDS_ONLINE_STATUS)
                || featureFlagsCache.Configuration.IsEnabled(FeatureFlagsStrings.FRIENDS_ONLINE_STATUS);

        private void ReconnectRpcClient()
        {
            friendServiceSubscriptionCancellationToken = friendServiceSubscriptionCancellationToken.SafeRestart();
            ReconnectRpcClientAsync(friendServiceSubscriptionCancellationToken.Token).Forget();
            return;

            async UniTaskVoid ReconnectRpcClientAsync(CancellationToken ct)
            {
                if (friendsService == null) return;

                try { await friendsService.DisconnectAsync(ct); }
                catch (Exception) { }

                friendsService.SubscribeToIncomingFriendshipEventsAsync(ct).Forget();

                if (IsConnectivityStatusEnabled())
                    friendsService.SubscribeToConnectivityStatusAsync(ct).Forget();
            }
        }

        private void DisconnectRpcClient()
        {
            friendServiceSubscriptionCancellationToken = friendServiceSubscriptionCancellationToken.SafeRestart();
            DisconnectRpcClientAsync(friendServiceSubscriptionCancellationToken.Token).Forget();
            return;

            async UniTaskVoid DisconnectRpcClientAsync(CancellationToken ct)
            {
                if (friendsService == null) return;

                try { await friendsService.DisconnectAsync(ct); }
                catch (Exception) { }
            }
        }
    }

    public class FriendsPluginSettings : IDCLPluginSettings
    {
        [field: SerializeField]
        public FriendRequestAssetReference FriendRequestPrefab { get; set; }

        [field: SerializeField]
        public UnfriendConfirmationPopupAssetReference UnfriendConfirmationPrefab { get; set; }

        [Serializable]
        public class FriendRequestAssetReference : ComponentReference<FriendRequestView>
        {
            public FriendRequestAssetReference(string guid) : base(guid) { }
        }

        [Serializable]
        public class UnfriendConfirmationPopupAssetReference : ComponentReference<UnfriendConfirmationPopupView>
        {
            public UnfriendConfirmationPopupAssetReference(string guid) : base(guid) { }
        }
    }
}
