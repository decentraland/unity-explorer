using Cysharp.Threading.Tasks;
using DCL.Chat;
using DCL.Friends.UI.Sections.Blocked;
using DCL.Friends.UI.Sections.Friends;
using DCL.Friends.UI.Sections.Requests;
using DCL.Web3.Identities;
using MVC;
using System.Threading;
using Utility;

namespace DCL.Friends.UI
{
    public class FriendsPanelController : ControllerBase<FriendsPanelView, FriendsPanelParameter>
    {
        private enum FriendsPanelTab
        {
            FRIENDS,
            REQUESTS,
            BLOCKED
        }

        private readonly IFriendsService friendsService;
        private readonly IFriendsEventBus friendEventBus;
        private readonly ChatView chatView;
        private readonly IMVCManager mvcManager;
        private readonly IWeb3IdentityCache web3IdentityCache;

        private BlockedSectionController blockedSectionController;
        private FriendsSectionController friendsSectionController;
        private RequestsSectionController requestsSectionController;
        private CancellationTokenSource friendsPanelCts = new ();
        private bool chatWasVisible;

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        public FriendsPanelController(ViewFactoryMethod viewFactory,
            ChatView chatView,
            IFriendsService friendsService,
            IFriendsEventBus friendEventBus,
            IMVCManager mvcManager,
            IWeb3IdentityCache web3IdentityCache) : base(viewFactory)
        {
            this.chatView = chatView;
            this.friendsService = friendsService;
            this.friendEventBus = friendEventBus;
            this.mvcManager = mvcManager;
            this.web3IdentityCache = web3IdentityCache;
        }

        public override void Dispose()
        {
            base.Dispose();

            viewInstance!.FriendsTabButton.onClick.RemoveAllListeners();
            viewInstance.RequestsTabButton.onClick.RemoveAllListeners();
            viewInstance.BlockedTabButton.onClick.RemoveAllListeners();
            friendsPanelCts.SafeCancelAndDispose();

            blockedSectionController.Dispose();
            friendsSectionController.Dispose();
            requestsSectionController.Dispose();
        }

        protected override void OnBeforeViewShow()
        {
            base.OnBeforeViewShow();
            friendsPanelCts = friendsPanelCts.SafeRestart();

            chatWasVisible = chatView.IsChatVisible();
            if (chatWasVisible)
                chatView.ToggleChat(false);
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

            blockedSectionController = new BlockedSectionController(viewInstance!.BlockedSection, mvcManager);
            friendsSectionController = new FriendsSectionController(viewInstance!.FriendsSection, friendsService, friendEventBus, web3IdentityCache, mvcManager);
            requestsSectionController = new RequestsSectionController(viewInstance!.RequestsSection, friendsService, friendEventBus, web3IdentityCache, mvcManager);

            viewInstance!.FriendsTabButton.onClick.AddListener(() => ToggleTabs(FriendsPanelTab.FRIENDS));
            viewInstance.RequestsTabButton.onClick.AddListener(() => ToggleTabs(FriendsPanelTab.REQUESTS));
            viewInstance.BlockedTabButton.onClick.AddListener(() => ToggleTabs(FriendsPanelTab.BLOCKED));
            viewInstance.CloseButton.onClick.AddListener(Close);
            viewInstance.BackgroundCloseButton.onClick.AddListener(Close);

            ToggleTabs(FriendsPanelTab.FRIENDS);
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

        private void Close() =>
            HideViewAsync(friendsPanelCts.Token).Forget();

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            UniTask.Never(ct);
    }
}
