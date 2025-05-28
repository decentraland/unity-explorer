using Cysharp.Threading.Tasks;
using DCL.UI.Profiles.Helpers;
using SuperScrollView;
using System;
using System.Threading;
using Utility;

namespace DCL.Friends.UI.FriendPanel.Sections
{
    public abstract class FriendPanelRequestManager<T> : IDisposable where T : FriendPanelUserView
    {
        private readonly ProfileRepositoryWrapper profileRepositoryWrapper;
        private readonly LoopListView2 loopListView;
        private readonly int pageSize;
        private readonly int elementsMissingThreshold;
        private readonly CancellationTokenSource fetchNewDataCts = new ();

        private int pageNumber;
        private int totalFetched;
        private int totalToFetch;
        private bool isFetching;
        private bool isInitializing;

        public bool HasElements { get; private set; }
        public bool WasInitialised { get; private set; }

        public event Action<FriendProfile>? ElementClicked;

        protected FriendPanelRequestManager(ProfileRepositoryWrapper profileDataProvider,
            LoopListView2 loopListView,
            int pageSize, int elementsMissingThreshold)
        {
            this.profileRepositoryWrapper = profileDataProvider;
            this.loopListView = loopListView;
            this.pageSize = pageSize;
            this.elementsMissingThreshold = elementsMissingThreshold;
        }

        public virtual void Dispose()
        {
            fetchNewDataCts.SafeCancelAndDispose();
        }

        public abstract int GetCollectionCount();
        protected abstract FriendProfile GetCollectionElement(int index);

        protected virtual void CustomiseElement(T elementView, int index) { }

        public LoopListViewItem2 GetLoopListItemByIndex(LoopListView2 loopListView, int index)
        {
            LoopListViewItem2 listItem = loopListView.NewListViewItem(loopListView.ItemPrefabDataList[0].mItemPrefab.name);
            T view = listItem.GetComponent<T>();
            view.Configure(GetCollectionElement(index), profileRepositoryWrapper);

            view.RemoveMainButtonClickListeners();
            view.MainButtonClicked += profile => ElementClicked?.Invoke(profile);

            CustomiseElement(view, index);

            if (index >= totalFetched - elementsMissingThreshold && totalFetched < totalToFetch && !isFetching)
                FetchNewDataAsync(loopListView, fetchNewDataCts.Token).Forget();

            return listItem;
        }

        private async UniTaskVoid FetchNewDataAsync(LoopListView2 loopListView, CancellationToken ct)
        {
            isFetching = true;

            pageNumber++;
            await FetchDataInternalAsync(ct);

            loopListView.SetListItemCount(GetCollectionCount(), false);
            loopListView.RefreshAllShownItem();

            isFetching = false;
        }

        private async UniTask FetchDataInternalAsync(CancellationToken ct)
        {
            totalToFetch = await FetchDataAsync(pageNumber, pageSize, ct);
            totalFetched = (pageNumber + 1) * pageSize;
        }

        protected abstract UniTask<int> FetchDataAsync(int pageNumber, int pageSize, CancellationToken ct);

        public async UniTask InitAsync(CancellationToken ct)
        {
            //This could happen when there's a prewarm and the user navigates to this section before the prewarm finishes
            if (isInitializing) return;

            isInitializing = true;

            await FetchDataInternalAsync(ct);

            HasElements = GetCollectionCount() > 0;
            WasInitialised = true;
            isInitializing = false;
        }

        public void Reset()
        {
            HasElements = false;
            WasInitialised = false;
            isInitializing = false;
            pageNumber = 0;
            totalFetched = 0;
            ResetCollection();
            RefreshLoopList();
        }

        protected void RefreshLoopList()
        {
            loopListView.SetListItemCount(GetCollectionCount(), false);
            loopListView.RefreshAllShownItem();
        }

        protected abstract void ResetCollection();
    }
}
