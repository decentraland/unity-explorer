using Arch.SystemGroups;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Clipboard;
using DCL.FeatureFlags;
using DCL.Friends;
using DCL.Friends.UI;
using DCL.Friends.UI.FriendPanel;
using DCL.Friends.UI.PushNotifications;
using DCL.Friends.UI.Requests;
using DCL.Input;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Multiplayer.Connectivity;
using DCL.NotificationsBusController.NotificationsBus;
using DCL.PerformanceAndDiagnostics.Analytics;
using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.RealmNavigation;
using DCL.UI.MainUI;
using DCL.UI.SharedSpaceManager;
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
        private readonly ObjectProxy<IFriendsConnectivityStatusTracker> friendOnlineStatusCacheProxy;
        private readonly IProfileThumbnailCache profileThumbnailCache;
        private readonly IOnlineUsersProvider onlineUsersProvider;
        private readonly IRealmNavigator realmNavigator;
        private readonly INotificationsBusController notificationsBusController;
        private readonly bool includeUserBlocking;
        private readonly IAppArgs appArgs;
        private readonly FeatureFlagsCache featureFlagsCache;
        private readonly IAnalyticsController? analyticsController;
        private readonly bool useAnalytics;
        private readonly ISharedSpaceManager sharedSpaceManager;

        private CancellationTokenSource friendServiceSubscriptionCancellationToken = new ();
        private RPCFriendsService? friendsService;
        private FriendsPanelController? friendsPanelController;
        private UnfriendConfirmationPopupController? unfriendConfirmationPopupController;
        private CancellationTokenSource? prewarmFriendsCancellationToken;

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
            ObjectProxy<IFriendsConnectivityStatusTracker> friendOnlineStatusCacheProxy,
            IProfileThumbnailCache profileThumbnailCache,
            INotificationsBusController notificationsBusController,
            IOnlineUsersProvider onlineUsersProvider,
            IRealmNavigator realmNavigator,
            bool includeUserBlocking,
            IAppArgs appArgs,
            FeatureFlagsCache featureFlagsCache,
            bool useAnalytics,
            IAnalyticsController? analyticsController,
            ISharedSpaceManager sharedSpaceManager)
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
            this.onlineUsersProvider = onlineUsersProvider;
            this.realmNavigator = realmNavigator;
            this.notificationsBusController = notificationsBusController;
            this.includeUserBlocking = includeUserBlocking;
            this.appArgs = appArgs;
            this.featureFlagsCache = featureFlagsCache;
            this.useAnalytics = useAnalytics;
            this.analyticsController = analyticsController;
            this.sharedSpaceManager = sharedSpaceManager;
        }

        public void Dispose()
        {
            friendsPanelController?.Dispose();
            friendServiceSubscriptionCancellationToken.SafeCancelAndDispose();
            friendsService?.Dispose();
            prewarmFriendsCancellationToken.SafeCancelAndDispose();
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments) { }

        public async UniTask InitializeAsync(FriendsPluginSettings settings, CancellationToken ct)
        {
            IFriendsEventBus friendEventBus = new DefaultFriendsEventBus();

            var friendsCache = new FriendsCache();

            friendsService = new RPCFriendsService(GetApiUrl(),
                friendEventBus, web3IdentityCache, friendsCache, selfProfile);

            IFriendsService injectableFriendService = useAnalytics ? new FriendServiceAnalyticsDecorator(friendsService, analyticsController!) : friendsService;

            friendServiceProxy.SetObject(injectableFriendService);

            // Fire and forget as this task will never finish
            var cts = CancellationTokenSource.CreateLinkedTokenSource(friendServiceSubscriptionCancellationToken.Token, ct);
            friendsService.SubscribeToIncomingFriendshipEventsAsync(cts.Token).Forget();

            bool isConnectivityStatusEnabled = IsConnectivityStatusEnabled();

            IFriendsConnectivityStatusTracker friendsConnectivityStatusTracker = new FriendsConnectivityStatusTracker(friendEventBus, isConnectivityStatusEnabled);
            friendOnlineStatusCacheProxy.SetObject(friendsConnectivityStatusTracker);

            if (isConnectivityStatusEnabled)
                friendsService.SubscribeToConnectivityStatusAsync(cts.Token).Forget();

            // We need to restart the connection to the service as identity changes
            // since that affects which friends the user can access
            web3IdentityCache.OnIdentityCleared += DisconnectRpcClient;
            web3IdentityCache.OnIdentityChanged += ReconnectRpcClient;

            friendsPanelController = new FriendsPanelController(() =>
                {
                    var panelView = mainUIView.FriendsPanelViewView;
                    panelView.gameObject.SetActive(false);
                    return panelView;
                },
                mainUIView.FriendsPanelViewView,
                mainUIView.SidebarView.FriendRequestNotificationIndicator,
                injectableFriendService,
                friendEventBus,
                mvcManager,
                web3IdentityCache,
                profileRepository,
                systemClipboard,
                webRequestController,
                profileThumbnailCache,
                dclInput,
                passportBridge,
                onlineUsersProvider,
                realmNavigator,
                friendsConnectivityStatusTracker,
                includeUserBlocking,
                isConnectivityStatusEnabled,
                sharedSpaceManager);

            sharedSpaceManager.RegisterPanel(PanelsSharingSpace.Friends, friendsPanelController);

            mvcManager.RegisterController(friendsPanelController);

            var persistentFriendsOpenerController = new PersistentFriendPanelOpenerController(() => mainUIView.SidebarView.PersistentFriendsPanelOpener,
                mvcManager,
                notificationsBusController,
                passportBridge,
                injectableFriendService,
                sharedSpaceManager,
                friendsPanelController);

            mvcManager.RegisterController(persistentFriendsOpenerController);

            FriendRequestView friendRequestPrefab = (await assetsProvisioner.ProvideMainAssetAsync(settings.FriendRequestPrefab, ct)).Value;

            var friendRequestController = new FriendRequestController(
                FriendRequestController.CreateLazily(friendRequestPrefab, null),
                web3IdentityCache, injectableFriendService, profileRepository,
                inputBlock, profileThumbnailCache);

            mvcManager.RegisterController(friendRequestController);

            var friendPushNotificationController = new FriendPushNotificationController(() => mainUIView.FriendPushNotificationView,
                friendsConnectivityStatusTracker,
                profileThumbnailCache);

            mvcManager.RegisterController(friendPushNotificationController);

            UnfriendConfirmationPopupView unfriendConfirmationPopupPrefab = (await assetsProvisioner.ProvideMainAssetAsync(settings.UnfriendConfirmationPrefab, ct)).Value;

            unfriendConfirmationPopupController = new UnfriendConfirmationPopupController(
                UnfriendConfirmationPopupController.CreateLazily(unfriendConfirmationPopupPrefab, null),
                injectableFriendService, profileRepository, profileThumbnailCache);

            mvcManager.RegisterController(unfriendConfirmationPopupController);

            loadingStatus.CurrentStage.Subscribe(PreWarmFriends);
        }

        private void PreWarmFriends(LoadingStatus.LoadingStage stage)
        {
            if (stage != LoadingStatus.LoadingStage.Completed) return;

            prewarmFriendsCancellationToken = prewarmFriendsCancellationToken.SafeRestart();
            PrewarmAsync(prewarmFriendsCancellationToken.Token).Forget();
            return;

            async UniTaskVoid PrewarmAsync(CancellationToken ct)
            {
                if (friendsPanelController != null)
                    await friendsPanelController.InitAsync(ct);

                loadingStatus.CurrentStage.Unsubscribe(PreWarmFriends);
            }
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

                friendsPanelController?.Reset();
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
