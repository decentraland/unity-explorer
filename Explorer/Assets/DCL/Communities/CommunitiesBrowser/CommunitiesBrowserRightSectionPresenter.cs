using Cysharp.Threading.Tasks;
using DCL.Chat.ControllerShowParams;
using DCL.Diagnostics;
using DCL.UI.Profiles.Helpers;
using DCL.UI.SharedSpaceManager;
using DCL.Utilities.Extensions;
using DCL.VoiceChat;
using System;
using System.Threading;
using Utility;

namespace DCL.Communities.CommunitiesBrowser
{
    public class CommunitiesBrowserRightSectionPresenter : IDisposable
    {
        private const int COMMUNITIES_PER_PAGE = 20;
        private const string MY_COMMUNITIES_RESULTS_TITLE = "My Communities";
        private const string STREAMING_COMMUNITIES_RESULTS_TITLE = "All Streaming Communities";
        private const string BROWSE_COMMUNITIES_TITLE = "Browse Communities";
        private const string SEARCH_RESULTS_TITLE_FORMAT = "Results for '{0}'";
        private const string ALL_COMMUNITIES_LOADING_ERROR_MESSAGE = "There was an error loading Communities. Please try again.";

        public event Action? ClearSearchBar;
        public event Action<string>? CommunityProfileOpened;
        public event Action<string>? CommunityJoined;

        private readonly CommunitiesBrowserRightSectionView view;
        private readonly ISharedSpaceManager sharedSpaceManager;
        private readonly CommunitiesDataProvider dataProvider;
        private readonly ProfileRepositoryWrapper profileRepositoryWrapper;
        private readonly ICommunityCallOrchestrator orchestrator;
        private readonly CommunitiesBrowserErrorNotificationService errorNotificationService;

        private readonly CommunitiesBrowserStreamingCommunitiesPresenter streamingCommunitiesPresenter;

        private string currentNameFilter = string.Empty;
        private bool currentIsOwnerFilter;
        private bool currentIsMemberFilter;
        private int currentPageNumberFilter = 1;
        private bool currentOnlyMemberOf;
        private bool isGridResultsLoadingItems;
        private int currentResultsTotalAmount;

        private CancellationTokenSource? loadResultsCts;

        public CommunitiesBrowserRightSectionPresenter(
            CommunitiesBrowserRightSectionView view,
            CommunitiesDataProvider dataProvider,
            ISharedSpaceManager sharedSpaceManager,
            CommunitiesBrowserStateService browserStateService,
            ThumbnailLoader thumbnailLoader,
            ProfileRepositoryWrapper profileRepositoryWrapper,
            ICommunityCallOrchestrator orchestrator,
            CommunitiesBrowserErrorNotificationService errorNotificationService)
        {
            this.view = view;
            this.dataProvider = dataProvider;
            this.sharedSpaceManager = sharedSpaceManager;
            this.profileRepositoryWrapper = profileRepositoryWrapper;
            this.orchestrator = orchestrator;
            this.errorNotificationService = errorNotificationService;

            streamingCommunitiesPresenter = new CommunitiesBrowserStreamingCommunitiesPresenter(view.StreamingCommunitiesView, dataProvider, browserStateService, errorNotificationService);
            streamingCommunitiesPresenter.JoinStream += OnJoinStream;
            streamingCommunitiesPresenter.ViewAllClicked += OnViewAllStreamingCommunities;

            view.SetThumbnailLoader(thumbnailLoader);
            view.SetCommunitiesBrowserState(browserStateService);

            view.InitializeStreamingResultsGrid(0);

            view.ResultsBackButtonClicked += LoadAllCommunitiesResults;
            view.CommunityJoined += OnCommunityJoined;
            view.CommunityProfileOpened += OnCommunityProfileOpened;
            ConfigureResultsGrid();
        }

        private void OnCommunityJoined(string communityId)
        {
            CommunityJoined?.Invoke(communityId);
        }

        private void OnCommunityProfileOpened(string communityId)
        {
            CommunityProfileOpened?.Invoke(communityId);
        }

        private void ConfigureResultsGrid()
        {
            view.InitializeResultsGrid(0, profileRepositoryWrapper);
            view.LoopGridScrollChanged += TryLoadMoreResults;
        }


        private void OnJoinStream(string communityId)
        {
            //If we already joined, we cannot join again
            if (orchestrator.CurrentCommunityId.Value == communityId) return;

            JoinStreamAsync().Forget();
            return;

            async UniTaskVoid JoinStreamAsync()
            {
                await sharedSpaceManager.ShowAsync(PanelsSharingSpace.Chat, new ChatControllerShowParams(false));
                //We wait until the panel has disappeared before starting the call, so the UX is better.
                await UniTask.Delay(500);
                orchestrator.JoinCommunityVoiceChat(communityId, true);
            }
        }

        private void OnViewAllStreamingCommunities()
        {
            ClearSearchBar?.Invoke();

            view.SetActiveSection(CommunitiesSections.FILTERED_COMMUNITIES);
            view.SetResultsTitleText(STREAMING_COMMUNITIES_RESULTS_TITLE);

            loadResultsCts = loadResultsCts.SafeRestart();
            LoadResultsAsync(
                name: string.Empty,
                onlyMemberOf: false,
                pageNumber: 1,
                elementsPerPage: COMMUNITIES_PER_PAGE,
                ct: loadResultsCts.Token,
                true).Forget();
        }

        public void LoadAllCommunitiesResults()
        {
            ClearSearchBar?.Invoke();

            view.SetActiveSection(CommunitiesSections.BROWSE_ALL_COMMUNITIES);
            view.SetResultsTitleText(BROWSE_COMMUNITIES_TITLE);

            LoadAllCommunitiesResultsAsync().Forget();
            return;

            async UniTaskVoid LoadAllCommunitiesResultsAsync()
            {
                loadResultsCts = loadResultsCts.SafeRestart();

                await UniTask.WhenAll(
                    streamingCommunitiesPresenter.LoadStreamingCommunitiesAsync(loadResultsCts.Token),
                    LoadResultsAsync(
                        name: string.Empty,
                        onlyMemberOf: false,
                        pageNumber: 1,
                        elementsPerPage: COMMUNITIES_PER_PAGE,
                        ct: loadResultsCts.Token)
                );

                streamingCommunitiesPresenter.SetAsLoading(false);
                view.SetAsLoading(false);
            }

        }

        private void TryLoadMoreResults()
        {
            if (isGridResultsLoadingItems ||
                view.CurrentResultsCount >= currentResultsTotalAmount ||
                !view.IsResultsScrollPositionAtBottom)
                return;

            loadResultsCts = loadResultsCts.SafeRestart();
            LoadResultsAsync(
                name: currentNameFilter,
                currentOnlyMemberOf,
                pageNumber: currentPageNumberFilter + 1,
                elementsPerPage: COMMUNITIES_PER_PAGE,
                ct: loadResultsCts.Token).Forget();
        }

        //Shows all "My Communities" in the Filtered Communities View
        public void ViewAllMyCommunitiesResults()
        {
            ClearSearchBar?.Invoke();

            view.SetActiveSection(CommunitiesSections.FILTERED_COMMUNITIES);
            view.SetResultsTitleText(MY_COMMUNITIES_RESULTS_TITLE);

            loadResultsCts = loadResultsCts.SafeRestart();
            LoadResultsAsync(
                name: string.Empty,
                onlyMemberOf: true,
                pageNumber: 1,
                elementsPerPage: COMMUNITIES_PER_PAGE,
                ct: loadResultsCts.Token).Forget();
        }

        private async UniTask LoadResultsAsync(string name, bool onlyMemberOf, int pageNumber, int elementsPerPage, CancellationToken ct, bool isStreaming = false)
        {
            isGridResultsLoadingItems = true;

            if (pageNumber == 1)
            {
                view.ClearItems();
                view.SetAsLoading(true);
            }
            else
                view.SetResultsLoadingMoreActive(true);

            var result = await dataProvider.GetUserCommunitiesAsync(
                name,
                onlyMemberOf,
                pageNumber,
                elementsPerPage,
                ct,
                isStreaming).SuppressToResultAsync(ReportCategory.COMMUNITIES);

            if (ct.IsCancellationRequested)
                return;

            if (!result.Success)
            {
                errorNotificationService.ShowWarningNotification(ALL_COMMUNITIES_LOADING_ERROR_MESSAGE).Forget();
            }

            if (result.Value.data.results.Length > 0)
            {
                currentPageNumberFilter = pageNumber;
                view.AddItems(result.Value.data.results, pageNumber == 1);
            }

            currentResultsTotalAmount = result.Value.data.total;

            if (pageNumber == 1)
                view.SetAsLoading(false);

            view.SetResultsLoadingMoreActive(false);
            view.SetResultsCountText(currentResultsTotalAmount);

            currentNameFilter = name;
            currentOnlyMemberOf = onlyMemberOf;
            isGridResultsLoadingItems = false;
        }

        public void Dispose()
        {
            loadResultsCts?.SafeCancelAndDispose();

            view.LoopGridScrollChanged -= TryLoadMoreResults;
            view.ResultsBackButtonClicked -= LoadAllCommunitiesResults;
        }

        public void LoadSearchResults(string searchText)
        {
            view.SetActiveSection(CommunitiesSections.FILTERED_COMMUNITIES);
            view.SetResultsBackButtonVisible(true);
            view.SetResultsTitleText(string.Format(SEARCH_RESULTS_TITLE_FORMAT, searchText));

            loadResultsCts = loadResultsCts.SafeRestart();
            LoadResultsAsync(
                name: searchText,
                onlyMemberOf: false,
                pageNumber: 1,
                elementsPerPage: COMMUNITIES_PER_PAGE,
                ct: loadResultsCts.Token).Forget();
        }

        public void Deactivate()
        {
            loadResultsCts?.SafeCancelAndDispose();
        }

        public void UpdateJoinedCommunity(string communityId, bool isJoined, bool isSuccess)
        {
            view.UpdateJoinedCommunity(communityId, isJoined, isSuccess);
        }

        public void OnUserRemovedFromCommunity(string communityId)
        {
            view.RemoveOneMemberFromCounter(communityId);
        }

        public void LoadAllCommunities()
        {
            LoadAllCommunitiesResults();
        }
    }
}
