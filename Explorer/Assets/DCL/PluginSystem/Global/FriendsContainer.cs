using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.FeatureFlags;
using DCL.Friends;
using DCL.Chat.EventBus;
using DCL.Diagnostics;
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
using DCL.UI.Profiles.Helpers;
using DCL.Profiles.Self;
using DCL.RealmNavigation;
using DCL.SocialService;
using DCL.UI.MainUI;
using DCL.UI.SharedSpaceManager;
using DCL.Utilities;
using DCL.Utilities.Extensions;
using DCL.VoiceChat;
using DCL.Web3.Identities;
using ECS.SceneLifeCycle.Realm;
using Global.AppArgs;
using MVC;
using System;
using System.Threading;
using UnityEngine;
using Utility;
using Utility.Types;

namespace DCL.PluginSystem.Global
{
    // TODO it should not be a plugin as it should expose dependencies to other parts but we have a mess with responsibilities of Static and World containers
    public class FriendsContainer : IDCLGlobalPlugin<FriendsPluginSettings>
    {
        private readonly MainUIView mainUIView;
        private readonly IMVCManager mvcManager;
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly IWeb3IdentityCache web3IdentityCache;
        private readonly IProfileRepository profileRepository;
        private readonly ILoadingStatus loadingStatus;
        private readonly IInputBlock inputBlock;
        private readonly DCLInput dclInput;
        private readonly bool includeUserBlocking;
        private readonly bool includeCall;
        private readonly IAppArgs appArgs;
        private readonly FeatureFlagsCache featureFlagsCache;
        private readonly ViewDependencies viewDependencies;
        private readonly ISocialServiceEventBus socialServiceEventBus;
        private readonly IFriendsEventBus friendsEventBus;
        private readonly ObjectProxy<IUserBlockingCache> userBlockingCacheProxy;
        private readonly ProfileRepositoryWrapper profileRepositoryWrapper;
        private readonly IVoiceChatCallStatusService voiceChatCallStatusService;

        private CancellationTokenSource friendServiceSubscriptionCts = new ();
        private UnfriendConfirmationPopupController? unfriendConfirmationPopupController;
        private CancellationTokenSource? prewarmFriendsCancellationToken;
        private CancellationTokenSource? syncBlockingStatusOnRpcConnectionCts;

        private UserBlockingCache? userBlockingCache;
        private readonly RPCFriendsService rpcFriendsService;

        private readonly FriendsPanelController friendsPanelController;
        private readonly IFriendsService friendsService;
        private readonly FriendsCache friendsCache;
        private readonly IFriendsConnectivityStatusTracker friendsConnectivityStatusTracker;

        public FriendsContainer(
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
            INotificationsBusController notificationsBusController,
            IOnlineUsersProvider onlineUsersProvider,
            IRealmNavigator realmNavigator,
            bool includeUserBlocking,
            bool includeCall,
            IAppArgs appArgs,
            FeatureFlagsCache featureFlagsCache,
            bool useAnalytics,
            IAnalyticsController? analyticsController,
            IChatEventBus chatEventBus,
            ViewDependencies viewDependencies,
            ISharedSpaceManager sharedSpaceManager,
            ISocialServiceEventBus socialServiceEventBus,
            IRPCSocialServices socialServicesRPC,
            IFriendsEventBus friendsEventBus,
            ObjectProxy<IFriendsService> friendServiceProxy,
            ObjectProxy<IFriendsConnectivityStatusTracker> friendsConnectivityStatusTrackerProxy,
            ObjectProxy<FriendsCache> friendsCacheProxy,
            ObjectProxy<IUserBlockingCache> userBlockingCacheProxy,
            ProfileRepositoryWrapper profileDataProvider,
            IVoiceChatCallStatusService voiceChatCallStatusService)
        {
            this.mainUIView = mainUIView;
            this.mvcManager = mvcManager;
            this.assetsProvisioner = assetsProvisioner;
            this.web3IdentityCache = web3IdentityCache;
            this.profileRepository = profileRepository;
            this.loadingStatus = loadingStatus;
            this.inputBlock = inputBlock;
            this.dclInput = dclInput;
            this.includeUserBlocking = includeUserBlocking;
            this.includeCall = includeCall;
            this.appArgs = appArgs;
            this.featureFlagsCache = featureFlagsCache;
            this.viewDependencies = viewDependencies;
            this.socialServiceEventBus = socialServiceEventBus;
            this.friendsEventBus = friendsEventBus;
            this.voiceChatCallStatusService = voiceChatCallStatusService;
            this.userBlockingCacheProxy = userBlockingCacheProxy;
            this.profileRepositoryWrapper = profileDataProvider;

            friendsCache = new FriendsCache();

            rpcFriendsService = new RPCFriendsService(friendsEventBus, friendsCache, selfProfile, socialServicesRPC);
            friendsService = useAnalytics ? new FriendServiceAnalyticsDecorator(rpcFriendsService, analyticsController!) : rpcFriendsService;

            this.socialServiceEventBus.TransportClosed += OnTransportClosed;

            bool isConnectivityStatusEnabled = IsConnectivityStatusEnabled();

            friendsConnectivityStatusTracker = new FriendsConnectivityStatusTracker(friendsEventBus, isConnectivityStatusEnabled);

            friendsPanelController = new FriendsPanelController(() =>
                {
                    var panelView = mainUIView.FriendsPanelViewView;
                    panelView.gameObject.SetActive(false);
                    return panelView;
                },
                mainUIView.FriendsPanelViewView,
                mainUIView.SidebarView.FriendRequestNotificationIndicator,
                friendsService,
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
                includeCall,
                isConnectivityStatusEnabled,
                sharedSpaceManager,
                profileRepositoryWrapper,
                voiceChatCallStatusService
            );

            sharedSpaceManager.RegisterPanel(PanelsSharingSpace.Friends, friendsPanelController);

            mvcManager.RegisterController(friendsPanelController);

            var persistentFriendsOpenerController = new PersistentFriendPanelOpenerController(() => mainUIView.SidebarView.PersistentFriendsPanelOpener,
                mvcManager,
                notificationsBusController,
                passportBridge,
                friendsService,
                sharedSpaceManager,
                friendsPanelController);

            mvcManager.RegisterController(persistentFriendsOpenerController);

            friendServiceProxy.SetObject(friendsService);
            friendsConnectivityStatusTrackerProxy.SetObject(friendsConnectivityStatusTracker);
            friendsCacheProxy.SetObject(friendsCache);
        }

        public void Dispose()
        {
            friendsPanelController.Dispose();
            friendServiceSubscriptionCts.SafeCancelAndDispose();
            prewarmFriendsCancellationToken.SafeCancelAndDispose();
            socialServiceEventBus.RPCClientReconnected -= OnRPCClientReconnected;
            socialServiceEventBus.TransportClosed -= OnTransportClosed;
            socialServiceEventBus.WebSocketConnectionEstablished -= SyncBlockingStatus;
            syncBlockingStatusOnRpcConnectionCts.SafeCancelAndDispose();
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments) { }

        public async UniTask InitializeAsync(FriendsPluginSettings settings, CancellationToken ct)
        {
            FriendRequestView friendRequestPrefab = (await assetsProvisioner.ProvideMainAssetAsync(settings.FriendRequestPrefab, ct)).Value;

            var friendRequestController = new FriendRequestController(
                FriendRequestController.CreateLazily(friendRequestPrefab, null),
                web3IdentityCache, friendsService, profileRepository,
                inputBlock, profileRepositoryWrapper);

            mvcManager.RegisterController(friendRequestController);

            var friendPushNotificationController = new FriendPushNotificationController(() => mainUIView.FriendPushNotificationView,
                friendsConnectivityStatusTracker, profileRepositoryWrapper, loadingStatus);

            mvcManager.RegisterController(friendPushNotificationController);

            UnfriendConfirmationPopupView unfriendConfirmationPopupPrefab = (await assetsProvisioner.ProvideMainAssetAsync(settings.UnfriendConfirmationPrefab, ct)).Value;

            unfriendConfirmationPopupController = new UnfriendConfirmationPopupController(
                UnfriendConfirmationPopupController.CreateLazily(unfriendConfirmationPopupPrefab, null),
                friendsService, profileRepository, profileRepositoryWrapper);

            mvcManager.RegisterController(unfriendConfirmationPopupController);

            socialServiceEventBus.RPCClientReconnected += OnRPCClientReconnected;

            loadingStatus.CurrentStage.Subscribe(PreWarmFriends);

            if (includeUserBlocking)
                await InitUserBlockingAsync();

            return;

            async UniTask InitUserBlockingAsync()
            {
                userBlockingCache = new UserBlockingCache(friendsEventBus);
                userBlockingCacheProxy.SetObject(userBlockingCache);
                socialServiceEventBus.WebSocketConnectionEstablished += SyncBlockingStatus;

                BlockUserPromptView blockUserPromptPrefab = (await assetsProvisioner.ProvideMainAssetAsync(settings.BlockUserPromptPrefab, ct)).Value;

                var blockUserPromptController = new BlockUserPromptController(
                    BlockUserPromptController.CreateLazily(blockUserPromptPrefab, null),
                    friendsService,
                    dclInput);

                mvcManager.RegisterController(blockUserPromptController);
            }
        }

        private void PreWarmFriends(LoadingStatus.LoadingStage stage)
        {
            if (stage != LoadingStatus.LoadingStage.Completed) return;

            friendServiceSubscriptionCts = friendServiceSubscriptionCts.SafeRestart();

            LaunchSubscriptions(friendServiceSubscriptionCts.Token);

            prewarmFriendsCancellationToken = prewarmFriendsCancellationToken.SafeRestart();
            PrewarmAsync(prewarmFriendsCancellationToken.Token).Forget();
            return;

            async UniTaskVoid PrewarmAsync(CancellationToken ct)
            {
                await friendsPanelController.InitAsync(ct);

                // TODO should not unsubscribe as the user can re-login with another account, and thus, pre-warming will be skipped
                loadingStatus.CurrentStage.Unsubscribe(PreWarmFriends);
            }
        }

        private void OnTransportClosed() =>
            friendServiceSubscriptionCts = friendServiceSubscriptionCts.SafeRestart();

        private void SyncBlockingStatus()
        {
            syncBlockingStatusOnRpcConnectionCts = syncBlockingStatusOnRpcConnectionCts.SafeRestart();
            SyncBlockingStatusAsync(syncBlockingStatusOnRpcConnectionCts.Token).Forget();
            return;

            async UniTaskVoid SyncBlockingStatusAsync(CancellationToken ct)
            {
                Result<UserBlockingStatus> result = await friendsService.GetUserBlockingStatusAsync(ct).SuppressToResultAsync(ReportCategory.FRIENDS);

                // TODO What to do if the result is not successful?
                if (result.Success)
                    userBlockingCache!.Reset(result.Value);
            }
        }

        private bool IsConnectivityStatusEnabled() =>
            appArgs.HasFlag(AppArgsFlags.FRIENDS_ONLINE_STATUS)
            || featureFlagsCache.Configuration.IsEnabled(FeatureFlagsStrings.FRIENDS_ONLINE_STATUS);

        private void OnRPCClientReconnected()
        {
            friendServiceSubscriptionCts = friendServiceSubscriptionCts.SafeRestart();
            ReconnectFriendServiceAsync(friendServiceSubscriptionCts.Token);
            return;

            void ReconnectFriendServiceAsync(CancellationToken ct)
            {
                LaunchSubscriptions(ct);

                friendsPanelController?.Reset();
            }
        }

        private void LaunchSubscriptions(CancellationToken ct)
        {
            rpcFriendsService.SubscribeToIncomingFriendshipEventsAsync(ct).Forget();

            if (IsConnectivityStatusEnabled())
                rpcFriendsService.SubscribeToConnectivityStatusAsync(ct).Forget();

            if (includeUserBlocking)
                rpcFriendsService.SubscribeToUserBlockUpdatersAsync(ct).Forget();
        }
    }

    [Serializable]
    public class FriendsPluginSettings : IDCLPluginSettings
    {
        [field: SerializeField]
        public FriendRequestAssetReference FriendRequestPrefab { get; private set; }

        [field: SerializeField]
        public UnfriendConfirmationPopupAssetReference UnfriendConfirmationPrefab { get; private set; }

        [field: SerializeField]
        public BlockUserPromptPopupAssetReference BlockUserPromptPrefab { get; private set; }

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
