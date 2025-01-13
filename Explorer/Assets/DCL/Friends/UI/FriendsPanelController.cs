using Cysharp.Threading.Tasks;
using DCL.Chat;
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

        private BlockedUsersController blockedUsersController;
        private CancellationTokenSource friendsPanelCts = new ();
        private bool chatWasVisible;

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        public FriendsPanelController(ViewFactoryMethod viewFactory,
            ChatView chatView,
            IFriendsService friendsService,
            IFriendsEventBus friendEventBus) : base(viewFactory)
        {
            this.chatView = chatView;
            this.friendsService = friendsService;
            this.friendEventBus = friendEventBus;
        }

        public override void Dispose()
        {
            base.Dispose();

            viewInstance!.FriendsTabButton.onClick.RemoveAllListeners();
            viewInstance.RequestsTabButton.onClick.RemoveAllListeners();
            viewInstance.BlockedTabButton.onClick.RemoveAllListeners();
            friendsPanelCts.SafeCancelAndDispose();

            blockedUsersController.Dispose();
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

            blockedUsersController = new BlockedUsersController(viewInstance!.BlockedPanel);

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
            viewInstance!.FriendsPanel.SetActive(tab == FriendsPanelTab.FRIENDS);
            viewInstance.RequestsTabSelected.SetActive(tab == FriendsPanelTab.REQUESTS);
            viewInstance.RequestsPanel.SetActive(tab == FriendsPanelTab.REQUESTS);
            viewInstance.BlockedTabSelected.SetActive(tab == FriendsPanelTab.BLOCKED);
            viewInstance.BlockedPanel.SetActive(tab == FriendsPanelTab.BLOCKED);
        }

        private void Close() =>
            HideViewAsync(friendsPanelCts.Token).Forget();

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            UniTask.Never(ct);
    }
}
