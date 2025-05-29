using MVC;
using SuperScrollView;

namespace DCL.Friends.UI.FriendPanel.Sections
{
    public abstract class FriendPanelSectionDoubleCollectionController<T, U, K> : FriendPanelSectionControllerBase<T, U>
        where T: FriendPanelSectionView
        where K: FriendPanelUserView
        where U: FriendPanelDoubleCollectionRequestManager<K>
    {
        protected readonly IFriendsService friendsService;
        protected readonly IFriendsEventBus friendEventBus;
        protected readonly IMVCManager mvcManager;

        protected FriendPanelSectionDoubleCollectionController(T view,
            IFriendsService friendsService,
            IFriendsEventBus friendEventBus,
            IMVCManager mvcManager,
            U requestManager) : base(view, requestManager)
        {
            this.friendsService = friendsService;
            this.friendEventBus = friendEventBus;
            this.mvcManager = mvcManager;

            requestManager.ElementClicked += ElementClicked;
        }

        public override void Dispose()
        {
            requestManager.ElementClicked -= ElementClicked;
            base.Dispose();
        }

        protected override LoopListViewItem2 OnGetItemByIndex(LoopListView2 loopListView, int index) =>
            requestManager.GetLoopListItemByIndex(loopListView, index);

        protected abstract void ElementClicked(FriendProfile profile);
    }
}
