using System;

namespace DCL.Communities.CommunitiesCard
{
    /// <summary>
    ///     An interface for views tied to controllers that handle fetching data for community sections in order to wrap common behavior such as:
    ///         - Exposing the event to request new data
    ///         - Refreshing the grid when new data is fetched
    ///         - Manage loading and empty states
    /// </summary>
    public interface ICommunityFetchingView<T>
    {
        public event Action? NewDataRequested;

        void RefreshGrid(SectionFetchData<T> membersData, bool redraw);

        void SetEmptyStateActive(bool active);
        void SetLoadingStateActive(bool active);
    }
}
