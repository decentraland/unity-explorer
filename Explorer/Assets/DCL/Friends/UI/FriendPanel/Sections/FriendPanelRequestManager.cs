using Cysharp.Threading.Tasks;
using DCL.Profiles;
using DCL.UI.Profiles.Helpers;
using SuperScrollView;
using System;

namespace DCL.Friends.UI.FriendPanel.Sections
{
    public abstract class FriendPanelRequestManager<T> : FriendPanelRequestManagerBase where T: FriendPanelUserView
    {
        private readonly ProfileRepositoryWrapper profileRepositoryWrapper;
        private readonly int elementsMissingThreshold;

        public event Action<Profile.CompactInfo>? ElementClicked;

        protected FriendPanelRequestManager(ProfileRepositoryWrapper profileDataProvider,
            LoopListView2 loopListView,
            int pageSize, int elementsMissingThreshold) : base(loopListView, pageSize)
        {
            this.profileRepositoryWrapper = profileDataProvider;
            this.elementsMissingThreshold = elementsMissingThreshold;
        }

        protected abstract Profile.CompactInfo GetCollectionElement(int index);

        protected override int GetListViewElementsCount() =>
            GetCollectionsDataCount();

        protected virtual void CustomiseElement(T elementView, int index) { }

        public LoopListViewItem2 GetLoopListItemByIndex(LoopListView2 loopListView, int index)
        {
            LoopListViewItem2 listItem = loopListView.NewListViewItem(loopListView.ItemPrefabDataList[0].mItemPrefab.name);
            T view = listItem.GetComponent<T>();
            Profile.CompactInfo friendProfile = GetCollectionElement(index);
            view.Configure(friendProfile, profileRepositoryWrapper);

            view.RemoveMainButtonClickListeners();
            view.MainButtonClicked += profile => ElementClicked?.Invoke(profile);

            CustomiseElement(view, index);
            view.ConfigureThumbnailClickData(thumbnailContextMenuActions[friendProfile.UserId]);

            if (index >= totalFetched - elementsMissingThreshold && totalFetched < totalToFetch && !isFetching)
                FetchNewDataAsync(loopListView).Forget();

            return listItem;
        }
    }
}
