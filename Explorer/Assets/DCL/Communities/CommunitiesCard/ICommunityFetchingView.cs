using System;

namespace DCL.Communities.CommunitiesCard
{
    public interface ICommunityFetchingView
    {
        public event Action? NewDataRequested;
        void RefreshGrid();

        void SetEmptyStateActive(bool active);
        void SetLoadingStateActive(bool active);
    }
}
