using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.FeatureFlags;
using DCL.Friends;
using DCL.Chat.EventBus;
using DCL.Friends.UI;
using DCL.Friends.UI.BlockUserPrompt;
using DCL.Friends.UI.FriendPanel;
using DCL.Friends.UI.PushNotifications;
using DCL.Friends.UI.Requests;
using DCL.Friends.UserBlocking;
using DCL.Input;
using DCL.Multiplayer.Connectivity;
using DCL.NotificationsBusController.NotificationsBus;
using DCL.PerformanceAndDiagnostics.Analytics;
using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.RealmNavigation;
using DCL.SocialService;
using DCL.UI.MainUI;
using DCL.UI.SharedSpaceManager;
using DCL.Utilities;
using DCL.Web3.Identities;
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
        private readonly IMVCManager mvcManager;
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly IWeb3IdentityCache web3IdentityCache;
        private readonly IProfileRepository profileRepository;
        private readonly ILoadingStatus loadingStatus;
        private readonly IInputBlock inputBlock;
        private readonly DCLInput dclInput;
        private readonly ISelfProfile selfProfile;
        private readonly IPassportBridge passportBridge;
        private readonly ObjectProxy<IFriendsService> friendServiceProxy;
        private readonly ObjectProxy<IFriendsConnectivityStatusTracker> friendOnlineStatusCacheProxy;
        private readonly ObjectProxy<IUserBlockingCache> userBlockingCacheProxy;
        private readonly ObjectProxy<FriendsCache> friendCacheProxy;
        private readonly IOnlineUsersProvider onlineUsersProvider;
        private readonly IRealmNavigator realmNavigator;
        private readonly INotificationsBusController notificationsBusController;
        private readonly bool includeUserBlocking;
        private readonly IAppArgs appArgs;
        private readonly FeatureFlagsCache featureFlagsCache;
        private readonly IAnalyticsController? analyticsController;
        private readonly ViewDependencies viewDependencies;
        private readonly bool useAnalytics;
        private readonly IChatEventBus chatEventBus;
        private readonly ISharedSpaceManager sharedSpaceManager;
        private readonly ObjectProxy<IRPCSocialServices> socialServicesRPCProxy;
        private readonly ISocialServiceEventBus socialServiceEventBus;
        private readonly IFriendsEventBus friendsEventBus;

        private CancellationTokenSource friendServiceSubscriptionCancellationToken = new ();
        private RPCFriendsService? friendsService;
        private FriendsPanelController? friendsPanelController;
        private UnfriendConfirmationPopupController? unfriendConfirmationPopupController;
        private CancellationTokenSource? prewarmFriendsCancellationToken;
        private CancellationTokenSource? syncBlockingStatusOnRpcConnectionCts;
        private UserBlockingCache? userBlockingCache;

        public FriendsPlugin(
            MainUIView mainUIView,
            IMVCManager mvcManager,
            IAssetsProvisioner assetsProvisioner,
            IWeb3IdentityCache web3IdentityCache,
            IProfileRepository profileRepository,
            ILoadingStatus loadingStatus,
            IInputBlock inputBlock,
            DCLInput dclInput,
            ISelfProfile selfProfile,
            IPassportBridge passportBridge,
            ObjectProxy<IFriendsService> friendServiceProxy,
            ObjectProxy<IFriendsConnectivityStatusTracker> friendOnlineStatusCacheProxy,
            ObjectProxy<IUserBlockingCache> userBlockingCacheProxy,
            INotificationsBusController notificationsBusController,
            IOnlineUsersProvider onlineUsersProvider,
            IRealmNavigator realmNavigator,
            bool includeUserBlocking,
            IAppArgs appArgs,
            FeatureFlagsCache featureFlagsCache,
            bool useAnalytics,
            IAnalyticsController? analyticsController,
            IChatEventBus chatEventBus,
            ViewDependencies viewDependencies,
            ISharedSpaceManager sharedSpaceManager,
            ISocialServiceEventBus socialServiceEventBus,
            ObjectProxy<IRPCSocialServices> socialServicesRPCProxy,
            ObjectProxy<FriendsCache> friendCacheProxy, IFriendsEventBus friendsEventBus)
        {
            this.mainUIView = mainUIView;
            this.mvcManager = mvcManager;
            this.assetsProvisioner = assetsProvisioner;
            this.web3IdentityCache = web3IdentityCache;
            this.profileRepository = profileRepository;
            this.loadingStatus = loadingStatus;
            this.inputBlock = inputBlock;
            this.dclInput = dclInput;
            this.selfProfile = selfProfile;
            this.passportBridge = passportBridge;
            this.friendServiceProxy = friendServiceProxy;
            this.friendOnlineStatusCacheProxy = friendOnlineStatusCacheProxy;
            this.userBlockingCacheProxy = userBlockingCacheProxy;
            this.onlineUsersProvider = onlineUsersProvider;
            this.realmNavigator = realmNavigator;
            this.notificationsBusController = notificationsBusController;
            this.includeUserBlocking = includeUserBlocking;
            this.appArgs = appArgs;
            this.featureFlagsCache = featureFlagsCache;
            this.useAnalytics = useAnalytics;
            this.analyticsController = analyticsController;
            this.viewDependencies = viewDependencies;
            this.chatEventBus = chatEventBus;
            this.sharedSpaceManager = sharedSpaceManager;
            this.socialServiceEventBus = socialServiceEventBus;
            this.socialServicesRPCProxy = socialServicesRPCProxy;
            this.friendCacheProxy = friendCacheProxy;
            this.friendsEventBus = friendsEventBus;
        }

        public void Dispose()
        {
            friendsPanelController?.Dispose();
            friendServiceSubscriptionCancellationToken.SafeCancelAndDispose();
            prewarmFriendsCancellationToken.SafeCancelAndDispose();
            socialServiceEventBus.RPCClientReconnected -= OnRPCClientReconnected;
            syncBlockingStatusOnRpcConnectionCts.SafeCancelAndDispose();
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments) { }

        public async UniTask InitializeAsync(FriendsPluginSettings settings, CancellationToken ct)
        {
            var friendsCache = new FriendsCache();
            friendCacheProxy.SetObject(friendsCache);

            friendsService = new RPCFriendsService(friendsEventBus, friendsCache, selfProfile, socialServicesRPCProxy, socialServiceEventBus);

            IFriendsService injectableFriendService = useAnalytics ? new FriendServiceAnalyticsDecorator(friendsService, analyticsController!) : friendsService;

            friendServiceProxy.SetObject(injectableFriendService);

            bool isConnectivityStatusEnabled = IsConnectivityStatusEnabled();

            IFriendsConnectivityStatusTracker friendsConnectivityStatusTracker = new FriendsConnectivityStatusTracker(friendsEventBus, isConnectivityStatusEnabled);
            friendOnlineStatusCacheProxy.SetObject(friendsConnectivityStatusTracker);

            if (includeUserBlocking)
            {
                userBlockingCache = new UserBlockingCache(friendsEventBus);
                userBlockingCacheProxy.SetObject(userBlockingCache);

                friendsService.WebSocketConnectionEstablished += SyncBlockingStatus;
            }

            friendsPanelController = new FriendsPanelController(() =>
                {
                    var panelView = mainUIView.FriendsPanelViewView;
                    panelView.gameObject.SetActive(false);
                    return panelView;
                },
                mainUIView.FriendsPanelViewView,
                mainUIView.SidebarView.FriendRequestNotificationIndicator,
                injectableFriendService,
                friendsEventBus,
                mvcManager,
                profileRepository,
                dclInput,
                passportBridge,
                onlineUsersProvider,
                realmNavigator,
                friendsConnectivityStatusTracker,
                chatEventBus,
                viewDependencies,
                includeUserBlocking,
                isConnectivityStatusEnabled,
                sharedSpaceManager
            );

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
                inputBlock, viewDependencies);

            mvcManager.RegisterController(friendRequestController);

            var friendPushNotificationController = new FriendPushNotificationController(() => mainUIView.FriendPushNotificationView,
                friendsConnectivityStatusTracker, viewDependencies, loadingStatus);

            mvcManager.RegisterController(friendPushNotificationController);

            UnfriendConfirmationPopupView unfriendConfirmationPopupPrefab = (await assetsProvisioner.ProvideMainAssetAsync(settings.UnfriendConfirmationPrefab, ct)).Value;

            unfriendConfirmationPopupController = new UnfriendConfirmationPopupController(
                UnfriendConfirmationPopupController.CreateLazily(unfriendConfirmationPopupPrefab, null),
                injectableFriendService, profileRepository, viewDependencies);

            mvcManager.RegisterController(unfriendConfirmationPopupController);

            socialServiceEventBus.RPCClientReconnected += OnRPCClientReconnected;

            loadingStatus.CurrentStage.Subscribe(PreWarmFriends);

            if (includeUserBlocking)
            {
                BlockUserPromptView blockUserPromptPrefab = (await assetsProvisioner.ProvideMainAssetAsync(settings.BlockUserPromptPrefab, ct)).Value;

                var blockUserPromptController = new BlockUserPromptController(
                    BlockUserPromptController.CreateLazily(blockUserPromptPrefab, null),
                    injectableFriendService,
                    dclInput);

                mvcManager.RegisterController(blockUserPromptController);
            }
        }

        private void PreWarmFriends(LoadingStatus.LoadingStage stage)
        {
            if (stage != LoadingStatus.LoadingStage.Completed) return;

            friendServiceSubscriptionCancellationToken = friendServiceSubscriptionCancellationToken.SafeRestart();

            // Fire and forget as this task will never finish
            friendsService!.SubscribeToIncomingFriendshipEventsAsync(friendServiceSubscriptionCancellationToken.Token).Forget();

            if (IsConnectivityStatusEnabled())
                friendsService.SubscribeToConnectivityStatusAsync(friendServiceSubscriptionCancellationToken.Token).Forget();

            if (includeUserBlocking)
                friendsService.SubscribeToUserBlockUpdatersAsync(friendServiceSubscriptionCancellationToken.Token).Forget();

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

        private void SyncBlockingStatus()
        {
            syncBlockingStatusOnRpcConnectionCts = syncBlockingStatusOnRpcConnectionCts.SafeRestart();
            SyncBlockingStatusAsync(syncBlockingStatusOnRpcConnectionCts.Token).Forget();
            return;

            async UniTask SyncBlockingStatusAsync(CancellationToken ct)
            {
                UserBlockingStatus blockingStatus = await friendsService!.GetUserBlockingStatusAsync(ct);
                userBlockingCache!.Reset(blockingStatus);
            }
        }

        private bool IsConnectivityStatusEnabled() =>
            appArgs.HasFlag(AppArgsFlags.FRIENDS_ONLINE_STATUS)
            || featureFlagsCache.Configuration.IsEnabled(FeatureFlagsStrings.FRIENDS_ONLINE_STATUS);

        private void OnRPCClientReconnected()
        {
            friendServiceSubscriptionCancellationToken = friendServiceSubscriptionCancellationToken.SafeRestart();
            ReconnectFriendServiceAsync(friendServiceSubscriptionCancellationToken.Token);
            return;

            void ReconnectFriendServiceAsync(CancellationToken ct)
            {
                if (!socialServicesRPCProxy.Configured || friendsService == null) return;

                friendsService.SubscribeToIncomingFriendshipEventsAsync(ct).Forget();

                if (IsConnectivityStatusEnabled())
                    friendsService.SubscribeToConnectivityStatusAsync(ct).Forget();

                if (includeUserBlocking && userBlockingCache != null)
                    friendsService.SubscribeToUserBlockUpdatersAsync(ct).Forget();

                friendsPanelController?.Reset();
            }
        }
    }

    public class FriendsPluginSettings : IDCLPluginSettings
    {
        [field: SerializeField]
        public FriendRequestAssetReference FriendRequestPrefab { get; set; }

        [field: SerializeField]
        public UnfriendConfirmationPopupAssetReference UnfriendConfirmationPrefab { get; set; }

        [field: SerializeField]
        public BlockUserPromptPopupAssetReference BlockUserPromptPrefab { get; set; }

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

        [Serializable]
        public class BlockUserPromptPopupAssetReference : ComponentReference<BlockUserPromptView>
        {
            public BlockUserPromptPopupAssetReference(string guid) : base(guid) { }
        }
    }
}
