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
        where U: ICommunityFetchingView<T>
    {
        private readonly int pageSize;
        protected CancellationToken cancellationToken;
        protected bool isFetching => activeFetch != null;
        protected abstract SectionFetchData<T> currentSectionFetchData { get; }

        private readonly U view;
        private CancellationTokenSource? activeFetch;

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
            CancelActiveFetch();
        }

        private void OnNewDataRequested()
        {
            if (isFetching) return;

            FetchNewDataAsync(cancellationToken).Forget();
        }

        protected async UniTask FetchNewDataAsync(CancellationToken ct)
        {
            CancelActiveFetch();
            using var fetch = CancellationTokenSource.CreateLinkedTokenSource(ct);
            activeFetch = fetch;
            SectionFetchData<T> data = currentSectionFetchData;
            bool completed = false;

            try
            {
                data.PageNumber = (data.TotalFetched / pageSize) + 1;
                view.SetLoadingStateActive(data.PageNumber == 1);
                view.SetLoadingMoreBadgeActive(data.PageNumber > 1);
                view.SetEmptyStateActive(false);

                int? total = await FetchDataAsync(fetch.Token);
                if (fetch.IsCancellationRequested)
                    return;

                if (total.HasValue)
                {
                    data.TotalToFetch = total.Value;
                    data.TotalFetched = data.PageNumber * pageSize;
                }
                else
                    data.PageNumber--;

                completed = true;

                EndLoading(data.TotalToFetch == 0);
                view.RefreshGrid(data, data.PageNumber > 1);
            }
            finally
            {
                if (activeFetch == fetch)
                {
                    activeFetch = null;
                    if (!completed)
                        EndLoading(data.TotalFetched > 0 && data.TotalToFetch == 0);
                }
            }
        }

        private void CancelActiveFetch()
        {
            CancellationTokenSource? fetch = activeFetch;
            activeFetch = null;
            fetch?.Cancel();
        }

        private void EndLoading(bool showEmptyState)
        {
            view.SetLoadingStateActive(false);
            view.SetLoadingMoreBadgeActive(false);
            view.SetEmptyStateActive(showEmptyState);
        }

        /// <returns>The total available results, or null when the page request failed and can be retried.</returns>
        protected abstract UniTask<int?> FetchDataAsync(CancellationToken ct);

        protected void RefreshGrid(bool redraw) =>
            view.RefreshGrid(currentSectionFetchData, redraw);

        public virtual void Reset()
        {
            CancelActiveFetch();
            EndLoading(false);
            RefreshGrid(true);
        }
    }
}
