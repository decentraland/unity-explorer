using Cysharp.Threading.Tasks;
using DCL.UI.Profiles.Helpers;
using DCL.UI.SharedSpaceManager;
using DCL.VoiceChat;
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
            ISharedSpaceManager sharedSpaceManager,
            CommunitiesBrowserStateService browserStateService,
            ThumbnailLoader thumbnailLoader,
            ProfileRepositoryWrapper profileRepositoryWrapper,
            ICommunityCallOrchestrator orchestrator,
            CommunitiesBrowserEventBus browserEventBus)
        {
            this.view = view;
            this.browserEventBus = browserEventBus;

            streamingCommunitiesPresenter = new CommunitiesBrowserStreamingCommunitiesPresenter(view.StreamingCommunitiesView, dataProvider,
                browserStateService, orchestrator, sharedSpaceManager);
            streamingCommunitiesPresenter.ViewAllClicked += OnViewAllStreamingCommunities;


            filteredCommunitiesPresenter = new CommunitiesBrowserFilteredCommunitiesPresenter(view.FilteredCommunitiesView, dataProvider, profileRepositoryWrapper, browserStateService, browserEventBus);
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

        private void LoadAllCommunities()
        {
            LoadAllCommunities(null);
        }

        public void LoadAllCommunities(Func<CancellationToken, UniTask<int>>? loadJoinRequests)
        {
            browserEventBus.RaiseClearSearchBarEvent();

            view.SetActiveView(CommunitiesViews.BROWSE_ALL_COMMUNITIES);
            loadCts = loadCts.SafeRestart();

            LoadAllCommunitiesResultsAsync(loadCts.Token).Forget();
            return;

            async UniTaskVoid LoadAllCommunitiesResultsAsync(CancellationToken ct)
            {
                await UniTask.WhenAll(
                                  streamingCommunitiesPresenter.LoadStreamingCommunitiesAsync(ct),
                                  filteredCommunitiesPresenter.LoadAllCommunitiesAsync(loadJoinRequests, ct)
                              )
                             .AttachExternalCancellation(ct);

                streamingCommunitiesPresenter.SetAsLoading(false);
                filteredCommunitiesPresenter.SetAsLoading(false);
            }
        }

        public void ViewAllMyCommunitiesResults()
        {
            view.SetActiveView(CommunitiesViews.FILTERED_COMMUNITIES);
            filteredCommunitiesPresenter.LoadAllMyCommunities();
        }
    }
}
