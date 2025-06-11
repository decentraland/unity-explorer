using Cysharp.Threading.Tasks;
using System;
using System.Threading;

namespace DCL.Communities.CommunitiesCard
{
    /// <summary>
    ///     A base class for controllers that handle fetching data for community sections in order to wrap common behavior such as:
    ///         - Handling the fetching state
    ///         - Storing the cancellation token given by the main controller
    ///         - Handling the fetch logic + loading and empty states
    /// </summary>
    public abstract class CommunityFetchingControllerBase<T, U> : IDisposable
    where U : ICommunityFetchingView
    {
        private readonly int pageSize;
        protected CancellationToken cancellationToken;
        protected bool isFetching;
        protected abstract SectionFetchData<T> currentSectionFetchData { get; }

        private U view;

        protected CommunityFetchingControllerBase(U view,
            int pageSize)
        {
            this.view = view;
            this.pageSize = pageSize;

            this.view.NewDataRequested += OnNewDataRequested;
        }

        public virtual void Dispose()
        {
            view.NewDataRequested -= OnNewDataRequested;
        }

        private void OnNewDataRequested()
        {
            if (isFetching) return;

            FetchNewDataAsync(cancellationToken).Forget();
        }

        protected async UniTaskVoid FetchNewDataAsync(CancellationToken ct)
        {
            isFetching = true;

            SectionFetchData<T> membersData = currentSectionFetchData;

            view.SetEmptyStateActive(false);

            if (membersData.pageNumber == 0)
                view.SetLoadingStateActive(true);

            membersData.pageNumber++;
            membersData.totalToFetch = await FetchDataAsync(ct);
            membersData.totalFetched = membersData.pageNumber * pageSize;

            view.SetLoadingStateActive(false);

            view.SetEmptyStateActive(membersData.totalToFetch == 0);

            view.RefreshGrid();

            isFetching = false;
        }

        protected abstract UniTask<int> FetchDataAsync(CancellationToken ct);

        public virtual void Reset()
        {
            isFetching = false;
            view.RefreshGrid();
        }
    }
}
