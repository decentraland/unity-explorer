using Cysharp.Threading.Tasks;
using DCL.Chat;
using DCL.FeatureFlags;
using DCL.Friends.UI.FriendPanel.Sections.Blocked;
using DCL.Friends.UI.FriendPanel.Sections.Friends;
using DCL.Friends.UI.FriendPanel.Sections.Requests;
using DCL.Multiplayer.Connectivity;
using DCL.Passport;
using DCL.Profiles;
using DCL.UI.Profiles.Helpers;
using DCL.Web3;
using ECS.SceneLifeCycle.Realm;
using MVC;
using System;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.Friends.UI.FriendPanel
{
    public class FriendsPanelController : ControllerBase<FriendsPanelView, FriendsPanelParameter>
    {
        public enum FriendsPanelTab
        {
            FRIENDS,
            REQUESTS,
            BLOCKED,
        }

        private const int FRIENDS_PAGE_SIZE = 50;
        private const int FRIENDS_REQUEST_PAGE_SIZE = 100;
        private const int FRIENDS_FETCH_ELEMENTS_THRESHOLD = 5;

        private readonly NotificationIndicatorView sidebarRequestNotificationIndicator;
        private readonly BlockedSectionController blockedSectionController;
        private readonly FriendSectionController? friendSectionController;
        private readonly FriendsSectionDoubleCollectionController? friendSectionControllerConnectivity;
        private readonly RequestsSectionController requestsSectionController;

        private CancellationTokenSource friendsPanelCts = new ();
        private UniTaskCompletionSource closeTaskCompletionSource = new ();

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.FULLSCREEN;

        public event Action? FriendsPanelOpened;
        public event Action<string>? OnlineFriendClicked;
        public event Action<string, Vector2Int>? JumpToFriendClicked;

        public FriendsPanelController(ViewFactoryMethod viewFactory,
            FriendsPanelView instantiatedView,
            NotificationIndicatorView sidebarRequestNotificationIndicator,
            IFriendsService friendsService,
            IFriendsEventBus friendEventBus,
            IMVCManager mvcManager,
            IProfileRepository profileRepository,
            IPassportBridge passportBridge,
            IOnlineUsersProvider onlineUsersProvider,
            IRealmNavigator realmNavigator,
            FriendsConnectivityStatusTracker friendsConnectivityStatusTracker,
            ProfileRepositoryWrapper profileDataProvider) : base(viewFactory)
        {
            this.sidebarRequestNotificationIndicator = sidebarRequestNotificationIndicator;

            bool isConnectivityStatusEnabled = FeaturesRegistry.Instance.IsEnabled(FeatureId.FRIENDS_CONNECTIVITY_STATUS);
            if (isConnectivityStatusEnabled)
            {
                friendSectionControllerConnectivity = new FriendsSectionDoubleCollectionController(instantiatedView.FriendsSection,
                    friendsService,
                    friendEventBus,
                    mvcManager,
                    new FriendListPagedDoubleCollectionRequestManager(friendsService, friendEventBus, profileRepository, friendsConnectivityStatusTracker, instantiatedView.FriendsSection.LoopList, profileDataProvider, FRIENDS_PAGE_SIZE, FRIENDS_FETCH_ELEMENTS_THRESHOLD),
                    passportBridge,
                    onlineUsersProvider,
                    realmNavigator,
                    friendsConnectivityStatusTracker);

                friendSectionControllerConnectivity.OnlineFriendClicked += OnlineFriendClick;
                friendSectionControllerConnectivity.JumpInClicked += JumpToFriendClick;
                friendSectionControllerConnectivity.OpenConversationClicked += OnOpenConversationClicked;
            }
            else
                friendSectionController = new FriendSectionController(instantiatedView.FriendsSection,
                    new FriendListRequestManager(friendsService,
                        friendEventBus,
                        profileRepository,
                        instantiatedView.FriendsSection.LoopList,
                        profileDataProvider,
                        FRIENDS_PAGE_SIZE,
                        FRIENDS_FETCH_ELEMENTS_THRESHOLD),
                    passportBridge,
                    onlineUsersProvider,
                    realmNavigator);

            requestsSectionController = new RequestsSectionController(instantiatedView.RequestsSection,
                friendsService,
                friendEventBus,
                mvcManager,
                new RequestsRequestManager(friendsService, friendEventBus, profileDataProvider, FRIENDS_REQUEST_PAGE_SIZE, instantiatedView.RequestsSection.LoopList),
                passportBridge);

            blockedSectionController = new BlockedSectionController(instantiatedView.BlockedSection,
                mvcManager,
                new BlockedPanelList(friendsService, friendEventBus, profileDataProvider, instantiatedView.BlockedSection.LoopList, FRIENDS_PAGE_SIZE, FRIENDS_FETCH_ELEMENTS_THRESHOLD),
                passportBridge);

            requestsSectionController.ReceivedRequestsCountChanged += FriendRequestCountChanged;
        }

        public override void Dispose()
        {
            base.Dispose();

            viewInstance?.FriendsTabButton.onClick.RemoveAllListeners();
            viewInstance?.RequestsTabButton.onClick.RemoveAllListeners();
            viewInstance?.BlockedTabButton.onClick.RemoveAllListeners();
            viewInstance?.CloseButton.onClick.RemoveAllListeners();
            requestsSectionController.ReceivedRequestsCountChanged -= FriendRequestCountChanged;
            friendsPanelCts.SafeCancelAndDispose();

            if (friendSectionControllerConnectivity != null)
            {
                friendSectionControllerConnectivity.OnlineFriendClicked -= OnlineFriendClick;
                friendSectionControllerConnectivity.JumpInClicked -= JumpToFriendClick;
                friendSectionControllerConnectivity.OpenConversationClicked -= OnOpenConversationClicked;
            }

            blockedSectionController.Dispose();
            friendSectionController?.Dispose();
            friendSectionControllerConnectivity?.Dispose();
            requestsSectionController.Dispose();
        }

        private void OnlineFriendClick(string targetAddress) => OnlineFriendClicked?.Invoke(targetAddress);

        private void JumpToFriendClick(string targetAddress, Vector2Int parcel)
        {
            closeTaskCompletionSource.TrySetResult();
            JumpToFriendClicked?.Invoke(targetAddress, parcel);
        }

        public UniTask InitAsync(CancellationToken ct) => requestsSectionController.InitAsync(ct);

        public void Reset()
        {
            requestsSectionController.Reset();
            friendSectionController?.Reset();
            friendSectionControllerConnectivity?.Reset();
            blockedSectionController.Reset();
        }

        private void OnOpenConversationClicked(Web3Address web3Address) =>
            ChatOpener.Instance.OpenPrivateConversationWithUserId(web3Address);

        protected override void OnViewShow() => FriendsPanelOpened?.Invoke();

        protected override void OnBeforeViewShow()
        {
            base.OnBeforeViewShow();
            friendsPanelCts = friendsPanelCts.SafeRestart();
            closeTaskCompletionSource = new UniTaskCompletionSource();

            ToggleTabs(inputData.TabToShow);
        }

        protected override void OnViewInstantiated()
        {
            base.OnViewInstantiated();

            viewInstance!.FriendsTabButton.onClick.AddListener(OnFriendsTabButtonClicked);
            viewInstance.RequestsTabButton.onClick.AddListener(OnRequestsTabButtonClicked);
            viewInstance.BlockedTabButton.onClick.AddListener(OnBlockedTabButtonClicked);
            viewInstance.CloseButton.onClick.AddListener(CloseFriendsPanel);
            viewInstance.BackgroundCloseButton.onClick.AddListener(CloseFriendsPanel);

            bool isUserBlockingFeatureEnabled = FeaturesRegistry.Instance.IsEnabled(FeatureId.FRIENDS_USER_BLOCKING);
            viewInstance.BlockedTabButton.gameObject.SetActive(isUserBlockingFeatureEnabled);
            ToggleTabs(FriendsPanelTab.FRIENDS);
        }

        private void OnFriendsTabButtonClicked() => ToggleTabs(FriendsPanelTab.FRIENDS);
        private void OnRequestsTabButtonClicked() => ToggleTabs(FriendsPanelTab.REQUESTS);
        private void OnBlockedTabButtonClicked() => ToggleTabs(FriendsPanelTab.BLOCKED);
        public void CloseFriendsPanel() => closeTaskCompletionSource.TrySetResult();
        private void FriendRequestCountChanged(int count) => sidebarRequestNotificationIndicator.SetNotificationCount(count);

        internal void ToggleTabs(FriendsPanelTab tab)
        {
            viewInstance!.FriendsTabSelected.SetActive(tab == FriendsPanelTab.FRIENDS);
            viewInstance.FriendsSection.SetActive(tab == FriendsPanelTab.FRIENDS);
            viewInstance.RequestsTabSelected.SetActive(tab == FriendsPanelTab.REQUESTS);
            viewInstance.RequestsSection.SetActive(tab == FriendsPanelTab.REQUESTS);
            viewInstance.BlockedTabSelected.SetActive(tab == FriendsPanelTab.BLOCKED);
            viewInstance.BlockedSection.SetActive(tab == FriendsPanelTab.BLOCKED);
        }

        protected override async UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            await UniTask.WhenAny(viewInstance!.CloseButton.OnClickAsync(ct), viewInstance!.BackgroundCloseButton.OnClickAsync(ct), closeTaskCompletionSource.Task);
    }
}
