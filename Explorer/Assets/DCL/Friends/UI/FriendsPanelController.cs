using Cysharp.Threading.Tasks;
using MVC;
using System.Threading;

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

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Overlay;

        public FriendsPanelController(ViewFactoryMethod viewFactory,
            IFriendsService friendsService,
            IFriendsEventBus friendEventBus) : base(viewFactory)
        {
            this.friendsService = friendsService;
            this.friendEventBus = friendEventBus;
        }

        public override void Dispose()
        {
            base.Dispose();

            viewInstance!.FriendsTabButton.onClick.RemoveAllListeners();
            viewInstance.RequestsTabButton.onClick.RemoveAllListeners();
            viewInstance.BlockedTabButton.onClick.RemoveAllListeners();
        }

        protected override void OnViewInstantiated()
        {
            base.OnViewInstantiated();

            viewInstance!.FriendsTabButton.onClick.AddListener(() => ToggleTabs(FriendsPanelTab.FRIENDS));
            viewInstance.RequestsTabButton.onClick.AddListener(() => ToggleTabs(FriendsPanelTab.REQUESTS));
            viewInstance.BlockedTabButton.onClick.AddListener(() => ToggleTabs(FriendsPanelTab.BLOCKED));

            ToggleTabs(FriendsPanelTab.FRIENDS);
        }

        private void ToggleTabs(FriendsPanelTab tab)
        {
            viewInstance!.FriendsTabSelected.SetActive(tab == FriendsPanelTab.FRIENDS);
            viewInstance.RequestsTabSelected.SetActive(tab == FriendsPanelTab.REQUESTS);
            viewInstance.BlockedTabSelected.SetActive(tab == FriendsPanelTab.BLOCKED);
        }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            viewInstance!.CloseButton.OnClickAsync(ct);
    }
}
