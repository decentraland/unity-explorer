using Cysharp.Threading.Tasks;
using DCL.Browser;
using DCL.Chat;
using DCL.Clipboard;
using DCL.Friends.UI.FriendPanel.Sections.Blocked;
using DCL.Friends.UI.FriendPanel.Sections.Friends;
using DCL.Friends.UI.FriendPanel.Sections.Requests;
using DCL.Profiles;
using DCL.RealmNavigation;
using DCL.Web3.Identities;
using DCL.WebRequests;
using MVC;
using System.Threading;
using UnityEngine.InputSystem;
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

        private const int FRIENDS_PAGE_SIZE = 100;
        private const int FRIENDS_REQUEST_PAGE_SIZE = 1000;
        private const int FRIENDS_FETCH_ELEMENTS_THRESHOLD = 5;

        private readonly ChatView chatView;
        private readonly NotificationIndicatorView sidebarRequestNotificationIndicator;
        private readonly BlockedSectionController blockedSectionController;
        private readonly FriendSectionController friendSectionController;
        private readonly RequestsSectionController requestsSectionController;
        private readonly DCLInput dclInput;

        private CancellationTokenSource friendsPanelCts = new ();
        private UniTaskCompletionSource closeTaskCompletionSource = new ();
        private bool chatWasVisible;

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Persistent;

        public FriendsPanelController(ViewFactoryMethod viewFactory,
            FriendsPanelView instantiatedView,
            ChatView chatView,
            NotificationIndicatorView sidebarRequestNotificationIndicator,
            IFriendsService friendsService,
            IFriendsEventBus friendEventBus,
            IMVCManager mvcManager,
            IWeb3IdentityCache web3IdentityCache,
            IProfileRepository profileRepository,
            ISystemClipboard systemClipboard,
            IWebRequestController webRequestController,
            IProfileThumbnailCache profileThumbnailCache,
            ILoadingStatus loadingStatus,
            DCLInput dclInput,
            IPassportBridge passportBridge,
            IWebBrowser webBrowser) : base(viewFactory)
        {
            this.chatView = chatView;
            this.sidebarRequestNotificationIndicator = sidebarRequestNotificationIndicator;
            this.dclInput = dclInput;

            friendSectionController = new FriendSectionController(instantiatedView.FriendsSection,
                web3IdentityCache,
                mvcManager,
                systemClipboard,
                new FriendListRequestManager(friendsService, friendEventBus, profileRepository, webRequestController, profileThumbnailCache, instantiatedView.FriendsSection.LoopList, FRIENDS_PAGE_SIZE, FRIENDS_FETCH_ELEMENTS_THRESHOLD),
                passportBridge,
                webBrowser,
                profileThumbnailCache);
            requestsSectionController = new RequestsSectionController(instantiatedView.RequestsSection,
                friendsService,
                friendEventBus,
                web3IdentityCache,
                mvcManager,
                systemClipboard,
                loadingStatus,
                new RequestsRequestManager(friendsService, friendEventBus, webRequestController, profileThumbnailCache, FRIENDS_REQUEST_PAGE_SIZE, instantiatedView.RequestsSection.LoopList),
                passportBridge,
                webBrowser,
                profileThumbnailCache);
            blockedSectionController = new BlockedSectionController(instantiatedView.BlockedSection,
                web3IdentityCache,
                new BlockedRequestManager(profileRepository, web3IdentityCache, webRequestController, profileThumbnailCache, FRIENDS_PAGE_SIZE, FRIENDS_FETCH_ELEMENTS_THRESHOLD),
                mvcManager,
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

            blockedSectionController.Dispose();
            friendSectionController.Dispose();
            requestsSectionController.Dispose();
            UnregisterCloseHotkey();
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

        private void CloseFriendsPanel(InputAction.CallbackContext obj) =>
            closeTaskCompletionSource.TrySetResult();

        protected override void OnViewShow() =>
            RegisterCloseHotkey();

        protected override void OnBeforeViewShow()
        {
            base.OnBeforeViewShow();
            friendsPanelCts = friendsPanelCts.SafeRestart();
            closeTaskCompletionSource = new UniTaskCompletionSource();

            chatWasVisible = chatView.IsChatVisible();
            if (chatWasVisible)
                chatView.ToggleChat(false);

            ToggleTabs(inputData.TabToShow);
        }

        protected override void OnViewClose()
        {
            base.OnViewClose();

            if (chatWasVisible)
                chatView.ToggleChat(true);

            UnregisterCloseHotkey();
        }

        protected override void OnViewInstantiated()
        {
            base.OnViewInstantiated();

            viewInstance!.FriendsTabButton.onClick.AddListener(() => ToggleTabs(FriendsPanelTab.FRIENDS));
            viewInstance.RequestsTabButton.onClick.AddListener(() => ToggleTabs(FriendsPanelTab.REQUESTS));
            viewInstance.BlockedTabButton.onClick.AddListener(() => ToggleTabs(FriendsPanelTab.BLOCKED));
            viewInstance.CloseButton.onClick.AddListener(() => CloseFriendsPanel(default(InputAction.CallbackContext)));

            ToggleTabs(FriendsPanelTab.FRIENDS);
        }

        private void FriendRequestCountChanged(int count)
        {
            sidebarRequestNotificationIndicator.SetNotificationCount(count);
        }

        private void ToggleTabs(FriendsPanelTab tab)
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
            await UniTask.WhenAny(viewInstance!.CloseButton.OnClickAsync(ct), closeTaskCompletionSource.Task);
            await HideViewAsync(ct);
        }
    }
}
