using MVC;
using SuperScrollView;
using System;

namespace DCL.Friends.UI.FriendPanel.Sections
{
    public abstract class FriendPanelRequestManager<T> : FriendPanelRequestManagerBase where T: FriendPanelUserView
    {
        private readonly ViewDependencies viewDependencies;
        private readonly int elementsMissingThreshold;

        public event Action<FriendProfile>? ElementClicked;

        protected FriendPanelRequestManager(ViewDependencies viewDependencies,
            LoopListView2 loopListView,
            int pageSize, int elementsMissingThreshold) : base(loopListView, pageSize)
        {
            this.viewDependencies = viewDependencies;
            this.elementsMissingThreshold = elementsMissingThreshold;
        }

        protected abstract FriendProfile GetCollectionElement(int index);

        protected override int GetListViewElementsCount() =>
            GetCollectionsDataCount();

        protected virtual void CustomiseElement(T elementView, int index) { }

        public LoopListViewItem2 GetLoopListItemByIndex(LoopListView2 loopListView, int index)
        {
            LoopListViewItem2 listItem = loopListView.NewListViewItem(loopListView.ItemPrefabDataList[0].mItemPrefab.name);
            T view = listItem.GetComponent<T>();
            view.InjectDependencies(viewDependencies);
            view.Configure(GetCollectionElement(index));

            view.RemoveMainButtonClickListeners();
            view.MainButtonClicked += profile => ElementClicked?.Invoke(profile);

            CustomiseElement(view, index);

            if (index >= totalFetched - elementsMissingThreshold && totalFetched < totalToFetch && !isFetching)
                FetchNewDataAsync(loopListView).Forget();

            return listItem;
        }
    }
}
