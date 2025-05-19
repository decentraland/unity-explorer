using MVC;
using SuperScrollView;
using System;

namespace DCL.Friends.UI.FriendPanel.Sections
{
    public abstract class FriendPanelDoubleCollectionRequestManager<T> : FriendPanelRequestManagerBase where T: FriendPanelUserView
    {
        protected readonly IFriendsService friendsService;
        protected readonly IFriendsEventBus friendEventBus;
        private readonly ViewDependencies viewDependencies;
        private readonly int elementsMissingThreshold;
        private readonly bool disablePagination;

        private readonly FriendPanelStatus firstCollectionStatus;
        private readonly FriendPanelStatus secondCollectionStatus;
        private readonly int statusElementIndex;
        private readonly int emptyElementIndex;
        private readonly int userElementIndex;

        private bool isInitializing;

        private bool excludeFirstCollection;
        private bool excludeSecondCollection;

        public event Action<FriendProfile>? ElementClicked;
        public event Action? FirstFolderClicked;
        public event Action? SecondFolderClicked;

        protected FriendPanelDoubleCollectionRequestManager(IFriendsService friendsService,
            IFriendsEventBus friendEventBus,
            ViewDependencies viewDependencies,
            LoopListView2 loopListView,
            int pageSize,
            int elementsMissingThreshold,
            FriendPanelStatus firstCollectionStatus,
            FriendPanelStatus secondCollectionStatus,
            int statusElementIndex,
            int emptyElementIndex,
            int userElementIndex,
            bool disablePagination = false) : base(loopListView, pageSize)
        {
            this.friendsService = friendsService;
            this.friendEventBus = friendEventBus;
            this.viewDependencies = viewDependencies;
            this.elementsMissingThreshold = elementsMissingThreshold;
            this.firstCollectionStatus = firstCollectionStatus;
            this.secondCollectionStatus = secondCollectionStatus;
            this.statusElementIndex = statusElementIndex;
            this.emptyElementIndex = emptyElementIndex;
            this.userElementIndex = userElementIndex;
            this.disablePagination = disablePagination;
        }

        protected abstract int GetFirstCollectionCount();
        protected abstract int GetSecondCollectionCount();

        protected abstract FriendProfile GetFirstCollectionElement(int index);
        protected abstract FriendProfile GetSecondCollectionElement(int index);

        protected virtual void CustomiseElement(T elementView, int index, FriendPanelStatus section) { }

        public LoopListViewItem2 GetLoopListItemByIndex(LoopListView2 loopListView, int index)
        {
            // TODO: possible NRE
            LoopListViewItem2 listItem = null;
            int onlineFriendMarker = excludeFirstCollection ? 0 : GetFirstCollectionCount();
            if (GetFirstCollectionCount() == 0 && !excludeFirstCollection) onlineFriendMarker++; //Count the empty element
            int offlineFriendMarker = excludeSecondCollection ? 0 : GetSecondCollectionCount();
            if (GetSecondCollectionCount() == 0 && !excludeSecondCollection) offlineFriendMarker++; //Count the empty element

            if (index == 0)
            {
                listItem = loopListView.NewListViewItem(loopListView.ItemPrefabDataList[statusElementIndex].mItemPrefab.name);
                StatusWrapperView statusWrapperView = listItem.GetComponent<StatusWrapperView>();
                statusWrapperView.SetStatusText(firstCollectionStatus, GetFirstCollectionCount());
                statusWrapperView.ResetCallback();
                statusWrapperView.FolderButtonClicked += FolderClick;
            }
            else if (index > 0 && index <= onlineFriendMarker)
            {
                if (GetFirstCollectionCount() == 0 && !excludeFirstCollection)
                    listItem = loopListView.NewListViewItem(loopListView.ItemPrefabDataList[emptyElementIndex].mItemPrefab.name);
                else
                {
                    listItem = loopListView.NewListViewItem(loopListView.ItemPrefabDataList[userElementIndex].mItemPrefab.name);
                    T friendListUserView = listItem.GetComponent<T>();
                    friendListUserView.InjectDependencies(viewDependencies);
                    int collectionIndex = index - 1;
                    friendListUserView.Configure(GetFirstCollectionElement(collectionIndex));
                    CustomiseElement(friendListUserView, collectionIndex, firstCollectionStatus);
                    friendListUserView.RemoveMainButtonClickListeners();
                    friendListUserView.MainButtonClicked += profile => ElementClicked?.Invoke(profile);
                }
            }
            else if (index == onlineFriendMarker + 1)
            {
                listItem = loopListView.NewListViewItem(loopListView.ItemPrefabDataList[statusElementIndex].mItemPrefab.name);
                StatusWrapperView statusWrapperView = listItem.GetComponent<StatusWrapperView>();
                statusWrapperView.SetStatusText(secondCollectionStatus, GetSecondCollectionCount());
                statusWrapperView.ResetCallback();
                statusWrapperView.FolderButtonClicked += FolderClick;
            }
            else if (index > onlineFriendMarker + 1 && index <= onlineFriendMarker + 1 + offlineFriendMarker + 1)
            {
                if (GetSecondCollectionCount() == 0 && !excludeSecondCollection)
                    listItem = loopListView.NewListViewItem(loopListView.ItemPrefabDataList[emptyElementIndex].mItemPrefab.name);
                else
                {
                    listItem = loopListView.NewListViewItem(loopListView.ItemPrefabDataList[userElementIndex].mItemPrefab.name);
                    T friendListUserView = listItem.GetComponent<T>();
                    friendListUserView.InjectDependencies(viewDependencies);
                    int collectionIndex = index - onlineFriendMarker - 2;
                    friendListUserView.Configure(GetSecondCollectionElement(collectionIndex));
                    CustomiseElement(friendListUserView, collectionIndex, secondCollectionStatus);
                    friendListUserView.RemoveMainButtonClickListeners();
                    friendListUserView.MainButtonClicked += profile => ElementClicked?.Invoke(profile);

                    if (!disablePagination && collectionIndex >= GetSecondCollectionCount() - elementsMissingThreshold && totalFetched < totalToFetch && !isFetching)
                        FetchNewDataAsync(loopListView).Forget();
                }
            }

            return listItem;
        }

        public int GetElementsNumber()
        {
            int count = 2;

            if (!excludeFirstCollection)
                count += GetFirstCollectionCount();

            if (!excludeSecondCollection)
                count += GetSecondCollectionCount();

            if (GetFirstCollectionCount() == 0 && !excludeFirstCollection)
                count++;

            if (GetSecondCollectionCount() == 0 && !excludeSecondCollection)
                count++;

            return count;
        }

        public override int GetCollectionCount() =>
            GetFirstCollectionCount() + GetSecondCollectionCount();

        private void FolderClick(bool isFolded, FriendPanelStatus panelStatus)
        {
            if (panelStatus == firstCollectionStatus)
            {
                excludeFirstCollection = isFolded;
                FirstFolderClicked?.Invoke();
            }
            else if (panelStatus == secondCollectionStatus)
            {
                excludeSecondCollection = isFolded;
                SecondFolderClicked?.Invoke();
            }
        }
    }
}
