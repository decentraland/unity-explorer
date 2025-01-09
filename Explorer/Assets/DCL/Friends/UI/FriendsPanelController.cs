using Cysharp.Threading.Tasks;
using MVC;
using System.Threading;

namespace DCL.Friends.UI
{
    public class FriendsPanelController : ControllerBase<FriendsPanelView, FriendsPanelParameter>
    {
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

        protected override void OnViewInstantiated()
        {
            base.OnViewInstantiated();
        }

        public override void Dispose()
        {
            base.Dispose();
        }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            viewInstance!.CloseButton.OnClickAsync(ct);
    }
}
