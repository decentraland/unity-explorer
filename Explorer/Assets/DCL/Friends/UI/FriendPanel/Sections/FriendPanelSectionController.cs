using SuperScrollView;

namespace DCL.Friends.UI.FriendPanel.Sections
{
    public abstract class FriendPanelSectionController<T, U, K> : FriendPanelSectionControllerBase<T, U>
        where T : FriendPanelSectionView
        where K : FriendPanelUserView
        where U : FriendPanelRequestManager<K>
    {
        protected FriendPanelSectionController(T view, U requestManager) : base(view, requestManager)
        {
            requestManager.ElementClicked += ElementClicked;
        }

        public override void Dispose()
        {
            base.Dispose();
            requestManager.ElementClicked -= ElementClicked;
        }

        protected override LoopListViewItem2 OnGetItemByIndex(LoopListView2 loopListView, int index) =>
            requestManager.GetLoopListItemByIndex(loopListView, index);

        protected abstract void ElementClicked(FriendProfile profile);

        protected override void RefreshLoopList()
        {
            view.LoopList.SetListItemCount(requestManager.GetCollectionCount(), false);
            view.LoopList.RefreshAllShownItem();
        }
    }
}
