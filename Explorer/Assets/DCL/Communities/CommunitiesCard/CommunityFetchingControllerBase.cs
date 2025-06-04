using Cysharp.Threading.Tasks;
using System;
using System.Threading;

namespace DCL.Communities.CommunitiesCard
{
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

        protected virtual async UniTask FetchNewDataAsync(CancellationToken ct)
        {
            isFetching = true;

            SectionFetchData<T> membersData = currentSectionFetchData;

            int membersCount = membersData.members.Count;

            membersData.pageNumber++;
            membersData.totalToFetch = await FetchDataAsync(ct);
            membersData.totalFetched = membersData.pageNumber * pageSize;

            if (membersCount != membersData.members.Count)
                view.RefreshGrid();

            isFetching = false;
        }

        protected abstract UniTask<int> FetchDataAsync(CancellationToken ct);
    }
}
