using Cysharp.Threading.Tasks;
using DCL.Chat;
using DCL.Clipboard;
using DCL.Friends.UI.FriendPanel.Sections.Blocked;
using DCL.Friends.UI.FriendPanel.Sections.Friends;
using DCL.Friends.UI.FriendPanel.Sections.Requests;
using DCL.Profiles;
using DCL.UserInAppInitializationFlow;
using DCL.Web3.Identities;
using DCL.WebRequests;
using MVC;
using System.Threading;
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

        private CancellationTokenSource friendsPanelCts = new ();
        private bool chatWasVisible;

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        public FriendsPanelController(ViewFactoryMethod viewFactory,
            FriendsPanelView instantiatedView,
            ChatView chatView,
            NotificationIndicatorView sidebarRequestNotificationIndicator,
            IFriendsService friendsService,
            IFriendsEventBus friendEventBus,
            IMVCManager mvcManager,
            IWeb3IdentityCache web3IdentityCache,
            IProfileCache profileCache,
            IProfileRepository profileRepository,
            ISystemClipboard systemClipboard,
            IWebRequestController webRequestController,
            IProfileThumbnailCache profileThumbnailCache,
            ILoadingStatus loadingStatus) : base(viewFactory)
        {
            this.chatView = chatView;
            this.sidebarRequestNotificationIndicator = sidebarRequestNotificationIndicator;

            friendSectionController = new FriendSectionController(instantiatedView.FriendsSection,
                web3IdentityCache,
                mvcManager,
                systemClipboard,
                new FriendListRequestManager(friendsService, friendEventBus, profileRepository, webRequestController, profileThumbnailCache, instantiatedView.FriendsSection.LoopList, FRIENDS_PAGE_SIZE, FRIENDS_FETCH_ELEMENTS_THRESHOLD));
            requestsSectionController = new RequestsSectionController(instantiatedView.RequestsSection,
                friendsService,
                friendEventBus,
                web3IdentityCache,
                mvcManager,
                systemClipboard,
                loadingStatus,
                new RequestsRequestManager(friendsService, friendEventBus, webRequestController, profileThumbnailCache, FRIENDS_REQUEST_PAGE_SIZE, profileCache, profileRepository, instantiatedView.RequestsSection.LoopList));
            blockedSectionController = new BlockedSectionController(instantiatedView.BlockedSection,
                web3IdentityCache,
                new BlockedRequestManager(profileRepository, web3IdentityCache, webRequestController, profileThumbnailCache, FRIENDS_PAGE_SIZE, FRIENDS_FETCH_ELEMENTS_THRESHOLD),
                mvcManager);

            requestsSectionController.ReceivedRequestsCountChanged += FriendRequestCountChanged;
        }

        public override void Dispose()
        {
            base.Dispose();

            viewInstance!.FriendsTabButton.onClick.RemoveAllListeners();
            viewInstance.RequestsTabButton.onClick.RemoveAllListeners();
            viewInstance.BlockedTabButton.onClick.RemoveAllListeners();
            requestsSectionController.ReceivedRequestsCountChanged -= FriendRequestCountChanged;
            friendsPanelCts.SafeCancelAndDispose();

            blockedSectionController.Dispose();
            friendSectionController.Dispose();
            requestsSectionController.Dispose();
        }

        protected override void OnBeforeViewShow()
        {
            base.OnBeforeViewShow();
            friendsPanelCts = friendsPanelCts.SafeRestart();

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
        }

        protected override void OnViewInstantiated()
        {
            base.OnViewInstantiated();

            viewInstance!.FriendsTabButton.onClick.AddListener(() => ToggleTabs(FriendsPanelTab.FRIENDS));
            viewInstance.RequestsTabButton.onClick.AddListener(() => ToggleTabs(FriendsPanelTab.REQUESTS));
            viewInstance.BlockedTabButton.onClick.AddListener(() => ToggleTabs(FriendsPanelTab.BLOCKED));

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

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            viewInstance!.CloseButton.OnClickAsync(ct);
    }
}
