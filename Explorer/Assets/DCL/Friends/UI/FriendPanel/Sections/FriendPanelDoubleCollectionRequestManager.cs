using Cysharp.Threading.Tasks;
using DCL.WebRequests;
using SuperScrollView;
using System;
using System.Threading;
using Utility;

namespace DCL.Friends.UI.FriendPanel.Sections
{
    public abstract class FriendPanelDoubleCollectionRequestManager<T> : IDisposable where T : FriendPanelUserView
    {
        protected readonly IFriendsService friendsService;
        protected readonly IFriendsEventBus friendEventBus;
        private readonly int pageSize;
        private readonly int elementsMissingThreshold;
        private readonly bool disablePagination;

        private readonly IWebRequestController webRequestController;
        private readonly IProfileThumbnailCache profileThumbnailCache;
        private readonly FriendPanelStatus firstCollectionStatus;
        private readonly FriendPanelStatus secondCollectionStatus;
        private readonly int statusElementIndex;
        private readonly int emptyElementIndex;
        private readonly int userElementIndex;
        private readonly CancellationTokenSource fetchNewDataCts = new ();

        private int pageNumber;
        private int totalFetched;
        private int totalToFetch;
        private bool isFetching;

        private bool excludeFirstCollection;
        private bool excludeSecondCollection;

        public bool HasElements { get; private set; }
        public bool WasInitialised { get; private set; }

        public event Action<FriendProfile>? ElementClicked;
        public event Action? FirstFolderClicked;
        public event Action? SecondFolderClicked;

        protected FriendPanelDoubleCollectionRequestManager(IFriendsService friendsService,
            IFriendsEventBus friendEventBus,
            IWebRequestController webRequestController,
            IProfileThumbnailCache profileThumbnailCache,
            int pageSize,
            int elementsMissingThreshold,
            FriendPanelStatus firstCollectionStatus,
            FriendPanelStatus secondCollectionStatus,
            int statusElementIndex,
            int emptyElementIndex,
            int userElementIndex,
            bool disablePagination = false)
        {
            this.friendsService = friendsService;
            this.friendEventBus = friendEventBus;
            this.webRequestController = webRequestController;
            this.profileThumbnailCache = profileThumbnailCache;
            this.pageSize = pageSize;
            this.elementsMissingThreshold = elementsMissingThreshold;
            this.firstCollectionStatus = firstCollectionStatus;
            this.secondCollectionStatus = secondCollectionStatus;
            this.statusElementIndex = statusElementIndex;
            this.emptyElementIndex = emptyElementIndex;
            this.userElementIndex = userElementIndex;
            this.disablePagination = disablePagination;
        }

        public virtual void Dispose()
        {
            fetchNewDataCts.SafeCancelAndDispose();
        }

        protected abstract int GetFirstCollectionCount();
        protected abstract int GetSecondCollectionCount();

        protected abstract FriendProfile GetFirstCollectionElement(int index);
        protected abstract FriendProfile GetSecondCollectionElement(int index);

        protected virtual void CustomiseElement(T elementView, int index, FriendPanelStatus section) { }

        public LoopListViewItem2 GetLoopListItemByIndex(LoopListView2 loopListView, int index)
        {
            LoopListViewItem2 listItem = null;
            int onlineFriendMarker = excludeFirstCollection ? 0 : GetFirstCollectionCount();
            if (GetFirstCollectionCount() == 0) onlineFriendMarker++; //Count the empty element
            int offlineFriendMarker = excludeSecondCollection ? 0 : GetSecondCollectionCount();
            if (GetSecondCollectionCount() == 0) offlineFriendMarker++; //Count the empty element

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
                if (GetFirstCollectionCount() == 0)
                    listItem = loopListView.NewListViewItem(loopListView.ItemPrefabDataList[emptyElementIndex].mItemPrefab.name);
                else
                {
                    listItem = loopListView.NewListViewItem(loopListView.ItemPrefabDataList[userElementIndex].mItemPrefab.name);
                    T friendListUserView = listItem.GetComponent<T>();
                    int collectionIndex = index - 1;
                    friendListUserView.Configure(GetFirstCollectionElement(collectionIndex), webRequestController, profileThumbnailCache);
                    CustomiseElement(friendListUserView, collectionIndex, firstCollectionStatus);
                    friendListUserView.RemoveMainButtonClickListeners();
                    friendListUserView.MainButtonClicked += profile => ElementClicked?.Invoke(profile);
                    friendListUserView.RemoveSpriteLoadedListeners();
                    friendListUserView.SpriteLoaded += sprite => profileThumbnailCache.SetThumbnail(friendListUserView.UserProfile.Address.ToString(), sprite);
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
                if (GetSecondCollectionCount() == 0)
                    listItem = loopListView.NewListViewItem(loopListView.ItemPrefabDataList[emptyElementIndex].mItemPrefab.name);
                else
                {
                    listItem = loopListView.NewListViewItem(loopListView.ItemPrefabDataList[userElementIndex].mItemPrefab.name);
                    T friendListUserView = listItem.GetComponent<T>();
                    int collectionIndex = index - onlineFriendMarker - 2;
                    friendListUserView.Configure(GetSecondCollectionElement(collectionIndex), webRequestController, profileThumbnailCache);
                    CustomiseElement(friendListUserView, collectionIndex, secondCollectionStatus);
                    friendListUserView.RemoveMainButtonClickListeners();
                    friendListUserView.MainButtonClicked += profile => ElementClicked?.Invoke(profile);
                    friendListUserView.RemoveSpriteLoadedListeners();
                    friendListUserView.SpriteLoaded += sprite => profileThumbnailCache.SetThumbnail(friendListUserView.UserProfile.Address.ToString(), sprite);

                    if (!disablePagination && collectionIndex >= GetSecondCollectionCount() - elementsMissingThreshold && totalFetched < totalToFetch && !isFetching)
                        FetchNewDataAsync(loopListView, fetchNewDataCts.Token).Forget();
                }
            }

            return listItem;
        }

        private async UniTaskVoid FetchNewDataAsync(LoopListView2 loopListView, CancellationToken ct)
        {
            isFetching = true;

            pageNumber++;
            totalToFetch = await FetchDataAsync(pageNumber, pageSize, ct);
            totalFetched = GetFirstCollectionCount() + GetSecondCollectionCount();

            loopListView.SetListItemCount(GetElementsNumber(), false);
            loopListView.RefreshAllShownItem();

            isFetching = false;
        }

        public int GetElementsNumber()
        {
            int count = 2;

            if (!excludeFirstCollection)
                count += GetFirstCollectionCount();

            if (!excludeSecondCollection)
                count += GetSecondCollectionCount();

            if (GetFirstCollectionCount() == 0)
                count++;

            if (GetSecondCollectionCount() == 0)
                count++;

            return count;
        }

        public void Reset()
        {
            HasElements = false;
            WasInitialised = false;
            pageNumber = 0;
            totalFetched = 0;
            ResetCollections();
        }

        protected abstract void ResetCollections();

        protected abstract UniTask<int> FetchDataAsync(int pageNumber, int pageSize, CancellationToken ct);

        public async UniTask InitAsync(CancellationToken ct)
        {
            totalToFetch = await FetchDataAsync(pageNumber, pageSize, ct);
            totalFetched = GetFirstCollectionCount() + GetSecondCollectionCount();

            HasElements = totalFetched > 0;
            WasInitialised = true;
        }

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
