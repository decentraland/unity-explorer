using Cysharp.Threading.Tasks;
using DCL.Profiles;
using DCL.WebRequests;
using SuperScrollView;
using System;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.Friends.UI.FriendPanel.Sections
{
    public abstract class FriendPanelRequestManager<T> : IDisposable where T : FriendPanelUserView
    {
        private readonly int pageSize;
        private readonly int elementsMissingThreshold;
        private readonly IWebRequestController webRequestController;
        private readonly IProfileThumbnailCache profileThumbnailCache;

        private CancellationTokenSource fetchNewDataCts = new ();
        private int pageNumber;
        private int totalFetched;
        private int totalToFetch;
        private bool isFetching;

        public bool HasElements { get; private set; }
        public bool WasInitialised { get; private set; }

        public event Action<Profile>? ElementClicked;

        protected FriendPanelRequestManager(int pageSize, int elementsMissingThreshold,
            IWebRequestController webRequestController,
            IProfileThumbnailCache profileThumbnailCache)
        {
            this.pageSize = pageSize;
            this.elementsMissingThreshold = elementsMissingThreshold;
            this.webRequestController = webRequestController;
            this.profileThumbnailCache = profileThumbnailCache;
        }

        public virtual void Dispose()
        {
            fetchNewDataCts.SafeCancelAndDispose();
        }

        public abstract int GetCollectionCount();
        protected abstract Profile GetCollectionElement(int index);

        protected virtual void CustomiseElement(T elementView, int index) { }

        public LoopListViewItem2 GetLoopListItemByIndex(LoopListView2 loopListView, int index)
        {
            LoopListViewItem2 listItem = loopListView.NewListViewItem(loopListView.ItemPrefabDataList[0].mItemPrefab.name);
            T view = listItem.GetComponent<T>();
            view.Configure(GetCollectionElement(index), webRequestController, profileThumbnailCache);

            view.RemoveMainButtonClickListeners();
            view.MainButtonClicked += profile => ElementClicked?.Invoke(profile);

            view.RemoveSpriteLoadedListeners();
            view.SpriteLoaded += sprite => profileThumbnailCache.SetThumbnail(view.UserProfile.UserId, sprite);

            CustomiseElement(view, index);

            if (index >= totalFetched - elementsMissingThreshold && totalFetched < totalToFetch && !isFetching)
                FetchNewDataAsync(loopListView, fetchNewDataCts.Token).Forget();

            return listItem;
        }

        private async UniTaskVoid FetchNewDataAsync(LoopListView2 loopListView, CancellationToken ct)
        {
            isFetching = true;

            pageNumber++;
            await FetchDataAsync(pageNumber, pageSize, ct);
            totalFetched = GetCollectionCount();

            loopListView.SetListItemCount(totalFetched);

            isFetching = false;
        }

        protected abstract UniTask<int> FetchDataAsync(int pageNumber, int pageSize, CancellationToken ct);

        public async UniTask InitAsync(CancellationToken ct)
        {
            totalToFetch = await FetchDataAsync(pageNumber, pageSize, ct);
            totalFetched = GetCollectionCount();

            HasElements = totalFetched > 0;
            WasInitialised = true;
        }

        public void Reset()
        {
            HasElements = false;
            WasInitialised = false;
            pageNumber = 0;
            totalFetched = 0;
            ResetCollection();
        }

        protected abstract void ResetCollection();
    }
}
