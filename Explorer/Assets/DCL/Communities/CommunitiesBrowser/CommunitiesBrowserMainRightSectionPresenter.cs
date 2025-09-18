using Cysharp.Threading.Tasks;
using DCL.Communities.CommunitiesBrowser.Commands;
using DCL.UI.Profiles.Helpers;
using System;
using System.Threading;
using Utility;

namespace DCL.Communities.CommunitiesBrowser
{
    public class CommunitiesBrowserMainRightSectionPresenter : IDisposable
    {
        private readonly CommunitiesBrowserRightSectionMainView view;

        private readonly CommunitiesBrowserStreamingCommunitiesPresenter streamingCommunitiesPresenter;
        private readonly CommunitiesBrowserFilteredCommunitiesPresenter filteredCommunitiesPresenter;
        private readonly CommunitiesBrowserEventBus browserEventBus;

        private CancellationTokenSource? loadCts;

        public CommunitiesBrowserMainRightSectionPresenter(
            CommunitiesBrowserRightSectionMainView view,
            CommunitiesDataProvider.CommunitiesDataProvider dataProvider,
            CommunitiesBrowserStateService browserStateService,
            ThumbnailLoader thumbnailLoader,
            ProfileRepositoryWrapper profileRepositoryWrapper,
            CommunitiesBrowserEventBus browserEventBus,
            CommunitiesBrowserCommandsLibrary commandsLibrary)
        {
            this.view = view;
            this.browserEventBus = browserEventBus;

            streamingCommunitiesPresenter = new CommunitiesBrowserStreamingCommunitiesPresenter(view.StreamingCommunitiesView, dataProvider,
                browserStateService, commandsLibrary);

            streamingCommunitiesPresenter.ViewAllClicked += OnViewAllStreamingCommunities;

            filteredCommunitiesPresenter = new CommunitiesBrowserFilteredCommunitiesPresenter(view.FilteredCommunitiesView, dataProvider, profileRepositoryWrapper, browserStateService, browserEventBus, commandsLibrary);
            filteredCommunitiesPresenter.ResultsBackButtonClicked += LoadAllCommunities;

            view.SetDependencies(thumbnailLoader, browserStateService);
            view.LoopGridScrollChanged += TryLoadMoreResults;
        }

        public void Dispose()
        {
            view.LoopGridScrollChanged -= TryLoadMoreResults;
            filteredCommunitiesPresenter.ResultsBackButtonClicked -= LoadAllCommunities;
            streamingCommunitiesPresenter.ViewAllClicked -= OnViewAllStreamingCommunities;
        }

        private void OnViewAllStreamingCommunities()
        {
            browserEventBus.RaiseClearSearchBarEvent();
            view.SetActiveView(CommunitiesViews.FILTERED_COMMUNITIES);
            filteredCommunitiesPresenter.LoadAllStreamingCommunities();
        }

        private void TryLoadMoreResults()
        {
            filteredCommunitiesPresenter.TryLoadMoreResults(view.IsResultsScrollPositionAtBottom);
        }

        public void LoadSearchResults(string searchText)
        {
            view.SetActiveView(CommunitiesViews.FILTERED_COMMUNITIES);
            filteredCommunitiesPresenter.LoadSearchResults(searchText);
        }

        public void Deactivate()
        {
            loadCts?.SafeCancelAndDispose();
            filteredCommunitiesPresenter.Deactivate();
            streamingCommunitiesPresenter.Deactivate();
        }

        public void LoadAllCommunities()
        {
            browserEventBus.RaiseClearSearchBarEvent();
            view.SetActiveView(CommunitiesViews.BROWSE_ALL_COMMUNITIES);

            LoadAllCommunitiesAsync().Forget();
        }

        public void SetAsLoading()
        {
            browserEventBus.RaiseClearSearchBarEvent();
            view.SetActiveView(CommunitiesViews.BROWSE_ALL_COMMUNITIES);
            loadCts = loadCts.SafeRestart();
            streamingCommunitiesPresenter.SetAsLoading(true);
            filteredCommunitiesPresenter.SetAsLoading(true);
        }

        public async UniTaskVoid LoadAllCommunitiesAsync()
        {
            loadCts = loadCts.SafeRestart();
            CancellationToken ct = loadCts.Token;

            await UniTask.WhenAll(
                              streamingCommunitiesPresenter.LoadStreamingCommunitiesAsync(ct),
                              filteredCommunitiesPresenter.LoadAllCommunitiesAsync(ct)
                          )
                         .AttachExternalCancellation(ct);

            streamingCommunitiesPresenter.SetAsLoading(false);
            filteredCommunitiesPresenter.SetAsLoading(false);
        }

        public void ViewAllMyCommunitiesResults()
        {
            view.SetActiveView(CommunitiesViews.FILTERED_COMMUNITIES);
            filteredCommunitiesPresenter.LoadAllMyCommunities();
        }
    }
}
