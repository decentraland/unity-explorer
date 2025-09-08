using Cysharp.Threading.Tasks;
using DCL.UI.Profiles.Helpers;
using DCL.UI.SharedSpaceManager;
using DCL.VoiceChat;
using System;
using System.Threading;
using Utility;

namespace DCL.Communities.CommunitiesBrowser
{
    public class CommunitiesBrowserRightSectionPresenter : IDisposable
    {
        private readonly CommunitiesBrowserRightSectionMainView view;

        private readonly CommunitiesBrowserStreamingCommunitiesPresenter streamingCommunitiesPresenter;
        private readonly CommunitiesBrowserFilteredCommunitiesPresenter filteredCommunitiesPresenter;

        private CancellationTokenSource? loadCts;
        public event Action? ClearSearchBar;
        public event Action<string>? CommunityProfileOpened;
        public event Action<string>? CommunityJoined;

        public CommunitiesBrowserRightSectionPresenter(
            CommunitiesBrowserRightSectionMainView view,
            CommunitiesDataProvider.CommunitiesDataProvider dataProvider,
            ISharedSpaceManager sharedSpaceManager,
            CommunitiesBrowserStateService browserStateService,
            ThumbnailLoader thumbnailLoader,
            ProfileRepositoryWrapper profileRepositoryWrapper,
            ICommunityCallOrchestrator orchestrator)
        {
            this.view = view;

            streamingCommunitiesPresenter = new CommunitiesBrowserStreamingCommunitiesPresenter(view.StreamingCommunitiesView, dataProvider,
                browserStateService, orchestrator, sharedSpaceManager);

            streamingCommunitiesPresenter.ViewAllClicked += OnViewAllStreamingCommunities;

            filteredCommunitiesPresenter = new CommunitiesBrowserFilteredCommunitiesPresenter(view.FilteredCommunitiesView, dataProvider, profileRepositoryWrapper);
            filteredCommunitiesPresenter.ResultsBackButtonClicked += LoadAllCommunities;
            filteredCommunitiesPresenter.CommunityJoined += OnCommunityJoined;
            filteredCommunitiesPresenter.CommunityProfileOpened += OnCommunityProfileOpened;

            view.SetDependencies(thumbnailLoader, browserStateService);
            view.LoopGridScrollChanged += TryLoadMoreResults;
        }

        public void Dispose()
        {
            view.LoopGridScrollChanged -= TryLoadMoreResults;
        }

        private void OnViewAllStreamingCommunities()
        {
            ClearSearchBar?.Invoke();
            view.SetActiveSection(CommunitiesSections.FILTERED_COMMUNITIES);
            filteredCommunitiesPresenter.ViewAllStreamingCommunities();
        }

        private void OnCommunityJoined(string communityId)
        {
            CommunityJoined?.Invoke(communityId);
        }

        private void OnCommunityProfileOpened(string communityId)
        {
            CommunityProfileOpened?.Invoke(communityId);
        }

        private void TryLoadMoreResults()
        {
            filteredCommunitiesPresenter.TryLoadMoreResults(view.IsResultsScrollPositionAtBottom);
        }

        public void LoadSearchResults(string searchText)
        {
            view.SetActiveSection(CommunitiesSections.FILTERED_COMMUNITIES);
            filteredCommunitiesPresenter.LoadSearchResults(searchText);
        }

        public void Deactivate()
        {
            loadCts?.SafeCancelAndDispose();
        }

        public void UpdateJoinedCommunity(string communityId, bool isSuccess)
        {
            filteredCommunitiesPresenter.UpdateJoinedCommunity(communityId, isSuccess);
        }

        public void OnUserRemovedFromCommunity(string communityId)
        {
            filteredCommunitiesPresenter.RemoveOneMemberFromCounter(communityId);
        }

        private void LoadAllCommunities()
        {
            LoadAllCommunities(false);
        }

        public void LoadAllCommunities(bool updateInvitations)
        {
            ClearSearchBar?.Invoke();
            view.SetActiveSection(CommunitiesSections.BROWSE_ALL_COMMUNITIES);
            loadCts = loadCts.SafeRestart();

            LoadAllCommunitiesResultsAsync(loadCts.Token).Forget();
            return;

            async UniTaskVoid LoadAllCommunitiesResultsAsync(CancellationToken ct)
            {
                await UniTask.WhenAll(
                                  streamingCommunitiesPresenter.LoadStreamingCommunitiesAsync(ct),
                                  filteredCommunitiesPresenter.LoadAllCommunitiesResultsAsync(updateInvitations, ct)
                              )
                             .AttachExternalCancellation(ct);

                streamingCommunitiesPresenter.SetAsLoading(false);
                filteredCommunitiesPresenter.SetAsLoading(false);
            }
        }

        public void ViewAllMyCommunitiesResults()
        {
            ClearSearchBar?.Invoke();
            view.SetActiveSection(CommunitiesSections.FILTERED_COMMUNITIES);
            filteredCommunitiesPresenter.ViewAllMyCommunitiesResults();
        }
    }
}
