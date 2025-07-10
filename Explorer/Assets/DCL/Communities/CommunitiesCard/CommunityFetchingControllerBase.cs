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

            try
            {

                SectionFetchData<T> membersData = currentSectionFetchData;

                view.SetEmptyStateActive(false);

                if (membersData.PageNumber == 0)
                    view.SetLoadingStateActive(true);

                int count = membersData.Items.Count;

                membersData.PageNumber++;
                membersData.TotalToFetch = await FetchDataAsync(ct);
                membersData.TotalFetched = membersData.PageNumber * pageSize;

                view.SetLoadingStateActive(false);

                view.SetEmptyStateActive(membersData.TotalToFetch == 0);

                view.RefreshGrid(currentSectionFetchData, count == membersData.Items.Count);
            }
            finally { isFetching = false; }
        }

        protected abstract UniTask<int> FetchDataAsync(CancellationToken ct);

        protected void RefreshGrid(bool redraw) =>
            view.RefreshGrid(currentSectionFetchData, redraw);

        public virtual void Reset()
        {
            isFetching = false;
            RefreshGrid(true);
        }
    }
}
