using Cysharp.Threading.Tasks;
using DCL.Chat.ControllerShowParams;
using DCL.Chat.EventBus;
using DCL.Friends.UI.FriendPanel.Sections.Blocked;
using DCL.Friends.UI.FriendPanel.Sections.Friends;
using DCL.Friends.UI.FriendPanel.Sections.Requests;
using DCL.Multiplayer.Connectivity;
using DCL.Profiles;
using DCL.UI.SharedSpaceManager;
using DCL.Web3;
using ECS.SceneLifeCycle.Realm;
using MVC;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.InputSystem;
using Utility;

namespace DCL.Friends.UI.FriendPanel
{
    public class FriendsPanelController : ControllerBase<FriendsPanelView, FriendsPanelParameter>, IControllerInSharedSpace<FriendsPanelView, FriendsPanelParameter>
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
        private readonly DCLInput dclInput;
        private readonly bool includeUserBlocking;
        private readonly IChatEventBus chatEventBus;
        private readonly ISharedSpaceManager sharedSpaceManager;

        private CancellationTokenSource friendsPanelCts = new ();
        private UniTaskCompletionSource closeTaskCompletionSource = new ();

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        public event Action? FriendsPanelOpened;
        public event Action<string>? OnlineFriendClicked;
        public event Action<string, Vector2Int>? JumpToFriendClicked;

        public event IPanelInSharedSpace.ViewShowingCompleteDelegate? ViewShowingComplete;

        public FriendsPanelController(ViewFactoryMethod viewFactory,
            FriendsPanelView instantiatedView,
            NotificationIndicatorView sidebarRequestNotificationIndicator,
            IFriendsService friendsService,
            IFriendsEventBus friendEventBus,
            IMVCManager mvcManager,
            IProfileRepository profileRepository,
            DCLInput dclInput,
            IPassportBridge passportBridge,
            IOnlineUsersProvider onlineUsersProvider,
            IRealmNavigator realmNavigator,
            IFriendsConnectivityStatusTracker friendsConnectivityStatusTracker,
            IChatEventBus chatEventBus,
            ViewDependencies viewDependencies,
            bool includeUserBlocking,
            bool isConnectivityStatusEnabled,
            ISharedSpaceManager sharedSpaceManager) : base(viewFactory)
        {
            this.sidebarRequestNotificationIndicator = sidebarRequestNotificationIndicator;
            this.dclInput = dclInput;
            this.chatEventBus = chatEventBus;
            this.includeUserBlocking = includeUserBlocking;
            this.sharedSpaceManager = sharedSpaceManager;

            if (isConnectivityStatusEnabled)
            {
                friendSectionControllerConnectivity = new FriendsSectionDoubleCollectionController(instantiatedView.FriendsSection,
                    friendsService,
                    friendEventBus,
                    mvcManager,
                    new FriendListPagedDoubleCollectionRequestManager(friendsService, friendEventBus, profileRepository, friendsConnectivityStatusTracker, instantiatedView.FriendsSection.LoopList, viewDependencies, FRIENDS_PAGE_SIZE, FRIENDS_FETCH_ELEMENTS_THRESHOLD),
                    passportBridge,
                    onlineUsersProvider,
                    realmNavigator,
                    friendsConnectivityStatusTracker,
                    chatEventBus,
                    sharedSpaceManager,
                    viewDependencies);

                friendSectionControllerConnectivity.OnlineFriendClicked += OnlineFriendClick;
                friendSectionControllerConnectivity.JumpInClicked += JumpToFriendClick;
                friendSectionControllerConnectivity.OpenConversationClicked += OnOpenConversationClicked;
            }
            else
                friendSectionController = new FriendSectionController(instantiatedView.FriendsSection,
                    mvcManager,
                    new FriendListRequestManager(friendsService, friendEventBus, profileRepository, instantiatedView.FriendsSection.LoopList, viewDependencies, FRIENDS_PAGE_SIZE, FRIENDS_FETCH_ELEMENTS_THRESHOLD),
                    passportBridge,
                    onlineUsersProvider,
                    realmNavigator,
                    viewDependencies,
                    chatEventBus,
                    sharedSpaceManager);

            requestsSectionController = new RequestsSectionController(instantiatedView.RequestsSection,
                friendsService,
                friendEventBus,
                mvcManager,
                new RequestsRequestManager(friendsService, friendEventBus, viewDependencies, FRIENDS_REQUEST_PAGE_SIZE, instantiatedView.RequestsSection.LoopList),
                passportBridge,
                includeUserBlocking);

            blockedSectionController = new BlockedSectionController(instantiatedView.BlockedSection,
                mvcManager,
                new BlockedPanelList(friendsService, friendEventBus, viewDependencies, instantiatedView.BlockedSection.LoopList, FRIENDS_PAGE_SIZE, FRIENDS_FETCH_ELEMENTS_THRESHOLD),
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
            UnregisterCloseHotkey();
        }

        public async UniTask OnHiddenInSharedSpaceAsync(CancellationToken ct)
        {
            closeTaskCompletionSource.TrySetResult();
            await UniTask.WaitUntil(() => State == ControllerState.ViewHidden, PlayerLoopTiming.Update, ct);
        }

        private void OnlineFriendClick(string targetAddress) =>
            OnlineFriendClicked?.Invoke(targetAddress);

        private void JumpToFriendClick(string targetAddress, Vector2Int parcel)
        {
            closeTaskCompletionSource.TrySetResult();
            JumpToFriendClicked?.Invoke(targetAddress, parcel);
        }

        public UniTask InitAsync(CancellationToken ct) =>
            requestsSectionController.InitAsync(ct);

        public void Reset()
        {
            requestsSectionController.Reset();
            friendSectionController?.Reset();
            friendSectionControllerConnectivity?.Reset();
            blockedSectionController.Reset();
        }

        private void RegisterCloseHotkey()
        {
            dclInput.UI.Close.performed += CloseFriendsPanel;
        }

        private void UnregisterCloseHotkey()
        {
            dclInput.UI.Close.performed -= CloseFriendsPanel;
        }

        private void OnOpenConversationClicked(Web3Address web3Address)
        {
            OpenChatConversationAsync(web3Address).Forget();
        }

        private async UniTaskVoid OpenChatConversationAsync(Web3Address web3Address)
        {
            await sharedSpaceManager.ShowAsync(PanelsSharingSpace.Chat, new ChatControllerShowParams(true, true));
            chatEventBus.OpenConversationUsingUserId(web3Address);
        }

        private void CloseFriendsPanel(InputAction.CallbackContext obj) =>
            closeTaskCompletionSource.TrySetResult();

        protected override void OnViewShow()
        {
            RegisterCloseHotkey();
            FriendsPanelOpened?.Invoke();
        }

        protected override void OnBeforeViewShow()
        {
            base.OnBeforeViewShow();
            friendsPanelCts = friendsPanelCts.SafeRestart();
            closeTaskCompletionSource = new UniTaskCompletionSource();

            ToggleTabs(inputData.TabToShow);
        }

        protected override void OnViewClose()
        {
            base.OnViewClose();

            UnregisterCloseHotkey();
        }

        protected override void OnViewInstantiated()
        {
            base.OnViewInstantiated();

            viewInstance!.FriendsTabButton.onClick.AddListener(() => ToggleTabs(FriendsPanelTab.FRIENDS));
            viewInstance.RequestsTabButton.onClick.AddListener(() => ToggleTabs(FriendsPanelTab.REQUESTS));
            viewInstance.BlockedTabButton.onClick.AddListener(() => ToggleTabs(FriendsPanelTab.BLOCKED));
            viewInstance.CloseButton.onClick.AddListener(() => CloseFriendsPanel(default(InputAction.CallbackContext)));
            viewInstance.BackgroundCloseButton.onClick.AddListener(() => CloseFriendsPanel(default(InputAction.CallbackContext)));

            viewInstance.BlockedTabButton.gameObject.SetActive(includeUserBlocking);

            ToggleTabs(FriendsPanelTab.FRIENDS);
        }

        private void FriendRequestCountChanged(int count)
        {
            sidebarRequestNotificationIndicator.SetNotificationCount(count);
        }

        internal void ToggleTabs(FriendsPanelTab tab)
        {
            viewInstance!.FriendsTabSelected.SetActive(tab == FriendsPanelTab.FRIENDS);
            viewInstance!.FriendsSection.SetActive(tab == FriendsPanelTab.FRIENDS);
            viewInstance.RequestsTabSelected.SetActive(tab == FriendsPanelTab.REQUESTS);
            viewInstance.RequestsSection.SetActive(tab == FriendsPanelTab.REQUESTS);
            viewInstance.BlockedTabSelected.SetActive(tab == FriendsPanelTab.BLOCKED);
            viewInstance.BlockedSection.SetActive(tab == FriendsPanelTab.BLOCKED);
        }

        protected override async UniTask WaitForCloseIntentAsync(CancellationToken ct)
        {
            ViewShowingComplete?.Invoke(this);
            await UniTask.WhenAny(viewInstance!.CloseButton.OnClickAsync(ct), viewInstance!.BackgroundCloseButton.OnClickAsync(ct), closeTaskCompletionSource.Task);
        }
    }
}
