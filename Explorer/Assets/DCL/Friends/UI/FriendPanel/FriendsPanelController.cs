using Cysharp.Threading.Tasks;
using DCL.Chat.ChatLifecycleBus;
using DCL.Friends.UI.FriendPanel.Sections.Blocked;
using DCL.Friends.UI.FriendPanel.Sections.Friends;
using DCL.Friends.UI.FriendPanel.Sections.Requests;
using DCL.Multiplayer.Connectivity;
using DCL.Profiles;
using DCL.UI.Sidebar.SidebarActionsBus;
using ECS.SceneLifeCycle.Realm;
using MVC;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Pool;
using Utility;

namespace DCL.Friends.UI.FriendPanel
{
    public class FriendsPanelController : ControllerBase<FriendsPanelView, FriendsPanelParameter>
    {
        public enum FriendsPanelTab
        {
            FRIENDS,
            REQUESTS,
            BLOCKED
        }

        private const int FRIENDS_PAGE_SIZE = 50;
        private const int FRIENDS_REQUEST_PAGE_SIZE = 100;
        private const int FRIENDS_FETCH_ELEMENTS_THRESHOLD = 5;

        private readonly IChatLifecycleBusController chatLifecycleBusController;
        private readonly NotificationIndicatorView sidebarRequestNotificationIndicator;
        private readonly BlockedSectionController blockedSectionController;
        private readonly FriendSectionController? friendSectionController;
        private readonly FriendsSectionDoubleCollectionController? friendSectionControllerConnectivity;
        private readonly RequestsSectionController requestsSectionController;
        private readonly DCLInput dclInput;
        private readonly ISidebarActionsBus sidebarActionsBus;
        private readonly bool includeUserBlocking;

        private CancellationTokenSource friendsPanelCts = new ();
        private UniTaskCompletionSource closeTaskCompletionSource = new ();

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Persistent;

        public event Action? FriendsPanelOpened;
        public event Action<string>? OnlineFriendClicked;
        public event Action<string, Vector2Int>? JumpToFriendClicked;

        public FriendsPanelController(ViewFactoryMethod viewFactory,
            FriendsPanelView instantiatedView,
            IChatLifecycleBusController chatLifecycleBusController,
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
            ISidebarActionsBus sidebarActionsBus,
            ViewDependencies viewDependencies,
            bool includeUserBlocking,
            bool isConnectivityStatusEnabled) : base(viewFactory)
        {
            this.chatLifecycleBusController = chatLifecycleBusController;
            this.sidebarRequestNotificationIndicator = sidebarRequestNotificationIndicator;
            this.dclInput = dclInput;
            this.sidebarActionsBus = sidebarActionsBus;
            this.includeUserBlocking = includeUserBlocking;

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
                    includeUserBlocking);
                friendSectionControllerConnectivity.OnlineFriendClicked += OnlineFriendClick;
                friendSectionControllerConnectivity.JumpInClicked += JumpToFriendClick;
            }
            else
                friendSectionController = new FriendSectionController(instantiatedView.FriendsSection,
                    mvcManager,
                    new FriendListRequestManager(friendsService, friendEventBus, profileRepository, instantiatedView.FriendsSection.LoopList, viewDependencies, FRIENDS_PAGE_SIZE, FRIENDS_FETCH_ELEMENTS_THRESHOLD),
                    passportBridge,
                    onlineUsersProvider,
                    realmNavigator,
                    includeUserBlocking);
            requestsSectionController = new RequestsSectionController(instantiatedView.RequestsSection,
                friendsService,
                friendEventBus,
                mvcManager,
                new RequestsRequestManager(friendsService, friendEventBus, viewDependencies, FRIENDS_REQUEST_PAGE_SIZE, instantiatedView.RequestsSection.LoopList),
                passportBridge,
                includeUserBlocking);
            blockedSectionController = new BlockedSectionController(instantiatedView.BlockedSection,
                mvcManager,
                new BlockedRequestManager(friendsService, friendEventBus, viewDependencies, instantiatedView.BlockedSection.LoopList, FRIENDS_PAGE_SIZE, FRIENDS_FETCH_ELEMENTS_THRESHOLD),
                passportBridge);

            requestsSectionController.ReceivedRequestsCountChanged += FriendRequestCountChanged;
            sidebarActionsBus.SubscribeOnWidgetOpen(() => CloseFriendsPanel(default(InputAction.CallbackContext)));
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
            }

            blockedSectionController.Dispose();
            friendSectionController?.Dispose();
            friendSectionControllerConnectivity?.Dispose();
            requestsSectionController.Dispose();
            UnregisterCloseHotkey();
        }

        private void OnlineFriendClick(string targetAddress) =>
            OnlineFriendClicked?.Invoke(targetAddress);

        private void JumpToFriendClick(string targetAddress, Vector2Int parcel) =>
            JumpToFriendClicked?.Invoke(targetAddress, parcel);

        public UniTask InitAsync(CancellationToken ct)
        {
            var tasks = ListPool<UniTask>.Get();

            try
            {
                tasks.Add(requestsSectionController.InitAsync(ct));

                // TODO: Remove it after the server's connectivity stream works as expected
                // This call forces to load at least the first page of friends (around 50)
                // Currently, as a mid-term solution, we poll connectivity status for each of the friends we have asked to the server through the archipelago api
                // If we do not request any friends then we will get no connectivity updates
                // This approach will work for most of the users (except those who have more than 50 friends whose going to get partial updates)
                // When server's rpc stream works, it will automatically send connectivity updates for each friend no matter if we previously asked for it or not
                // if (friendSectionControllerConnectivity != null)
                //     tasks.Add(friendSectionControllerConnectivity.InitAsync(ct));

                return UniTask.WhenAll(tasks);
            }
            finally
            {
                ListPool<UniTask>.Release(tasks);
            }
        }

        public void Reset()
        {
            requestsSectionController.Reset();
            friendSectionController?.Reset();
            friendSectionControllerConnectivity?.Reset();
        }

        private void RegisterCloseHotkey()
        {
            dclInput.Shortcuts.FriendPanel.performed += CloseFriendsPanel;
            dclInput.UI.Close.performed += CloseFriendsPanel;
        }

        private void UnregisterCloseHotkey()
        {
            dclInput.Shortcuts.FriendPanel.performed -= CloseFriendsPanel;
            dclInput.UI.Close.performed -= CloseFriendsPanel;
        }

        internal void CloseFriendsPanel(InputAction.CallbackContext obj) =>
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

            chatLifecycleBusController.HideChat();

            ToggleTabs(inputData.TabToShow);

            sidebarActionsBus.CloseAllWidgets();
        }

        protected override void OnViewClose()
        {
            base.OnViewClose();

            chatLifecycleBusController.ShowChat();

            UnregisterCloseHotkey();
        }

        protected override void OnViewInstantiated()
        {
            base.OnViewInstantiated();

            viewInstance!.FriendsTabButton.onClick.AddListener(() => ToggleTabs(FriendsPanelTab.FRIENDS));
            viewInstance.RequestsTabButton.onClick.AddListener(() => ToggleTabs(FriendsPanelTab.REQUESTS));
            viewInstance.BlockedTabButton.onClick.AddListener(() => ToggleTabs(FriendsPanelTab.BLOCKED));
            viewInstance.CloseButton.onClick.AddListener(() => CloseFriendsPanel(default(InputAction.CallbackContext)));

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
            await UniTask.WhenAny(viewInstance!.CloseButton.OnClickAsync(ct), viewInstance!.BackgroundCloseButton.OnClickAsync(ct), closeTaskCompletionSource.Task);
            await HideViewAsync(ct);
        }
    }
}
