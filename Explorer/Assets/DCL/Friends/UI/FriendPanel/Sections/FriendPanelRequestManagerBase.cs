using Cysharp.Threading.Tasks;
using SuperScrollView;
using System;
using System.Threading;
using Utility;

namespace DCL.Friends.UI.FriendPanel.Sections
{
    public abstract class FriendPanelRequestManagerBase : IDisposable
    {
        private readonly CancellationTokenSource fetchNewDataCts = new ();

        private readonly LoopListView2 loopListView;
        private readonly int pageSize;

        private int pageNumber;
        private bool isInitializing;

        public bool HasElements { get; private set; }
        public bool WasInitialised { get; private set; }

        protected int totalToFetch { get; private set; }
        protected int totalFetched { get; private set; }
        protected bool isFetching { get; private set; }

        protected FriendPanelRequestManagerBase(LoopListView2 loopListView, int pageSize)
        {
            this.loopListView = loopListView;
            this.pageSize = pageSize;
        }

        public virtual void Dispose()
        {
            fetchNewDataCts.SafeCancelAndDispose();
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

        protected abstract void ResetCollection();

        protected abstract UniTask<int> FetchDataAsync(int pageNumber, int pageSize, CancellationToken ct);

        public async UniTask InitAsync(CancellationToken ct)
        {
            // This could happen when there's a prewarm and the user navigates to this section before the prewarm finishes
            if (isInitializing) return;

            isInitializing = true;

            try
            {
                await FetchDataInternalAsync(ct);

                HasElements = GetCollectionsDataCount() > 0;
                WasInitialised = true;
            }
            finally { isInitializing = false; }
        }

        /// <summary>
        ///     Data count of the collection(-s)
        /// </summary>
        protected abstract int GetCollectionsDataCount();

        /// <summary>
        ///     List View elements include foldable sections
        /// </summary>
        protected abstract int GetListViewElementsCount();

        internal void RefreshLoopList()
        {
            loopListView.SetListItemCount(GetListViewElementsCount(), false);
            loopListView.RefreshAllShownItem();
        }

        protected async UniTaskVoid FetchNewDataAsync(LoopListView2 loopListView)
        {
            isFetching = true;

            try
            {
                pageNumber++;
                await FetchDataInternalAsync(fetchNewDataCts.Token);

                loopListView.SetListItemCount(GetListViewElementsCount(), false);
                loopListView.RefreshAllShownItem();
            }
            finally { isFetching = false; }
        }

        private async UniTask FetchDataInternalAsync(CancellationToken ct)
        {
            totalToFetch = await FetchDataAsync(pageNumber, pageSize, ct);
            totalFetched = (pageNumber + 1) * pageSize;
        }
    }
}
