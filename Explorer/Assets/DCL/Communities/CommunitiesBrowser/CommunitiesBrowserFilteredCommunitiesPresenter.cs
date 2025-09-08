using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.UI.Profiles.Helpers;
using DCL.Utilities.Extensions;
using System;
using System.Threading;
using Utility;
using Utility.Types;
using DCL.Communities.CommunitiesDataProvider;
using DCL.Communities.CommunitiesDataProvider.DTOs;
using DCL.NotificationsBusController.NotificationTypes;
using Notifications = DCL.NotificationsBusController.NotificationsBus;

namespace DCL.Communities.CommunitiesBrowser
{
    public class CommunitiesBrowserFilteredCommunitiesPresenter : IDisposable
    {
        private const int COMMUNITIES_PER_PAGE = 20;
        private const string MY_COMMUNITIES_RESULTS_TITLE = "My Communities";
        private const string STREAMING_COMMUNITIES_RESULTS_TITLE = "All Streaming Communities";
        private const string BROWSE_COMMUNITIES_TITLE = "Browse Communities";
        private const string SEARCH_RESULTS_TITLE_FORMAT = "Results for '{0}'";
        private const string ALL_COMMUNITIES_LOADING_ERROR_MESSAGE = "There was an error loading Communities. Please try again.";

        public event Action? ResultsBackButtonClicked;
        public event Action<string>? CommunityProfileOpened;
        public event Action<string>? CommunityJoined;


        private readonly FilteredCommunitiesView view;
        private readonly CommunitiesDataProvider.CommunitiesDataProvider dataProvider;

        private string currentNameFilter = string.Empty;
        private bool currentIsOwnerFilter;
        private bool currentIsMemberFilter;
        private int currentPageNumberFilter = 1;
        private bool currentOnlyMemberOf;
        private bool isGridResultsLoadingItems;
        private int currentResultsTotalAmount;

        private CancellationTokenSource? loadResultsCts;

        public CommunitiesBrowserFilteredCommunitiesPresenter(
            FilteredCommunitiesView view, CommunitiesDataProvider.CommunitiesDataProvider dataProvider, ProfileRepositoryWrapper profileRepositoryWrapper)
        {
            this.view = view;
            this.dataProvider = dataProvider;

            view.BackButtonClicked += OnBackButtonClicked;
            view.CommunityJoined += OnCommunityJoined;
            view.CommunityProfileOpened += OnCommunityProfileOpened;

            view.InitializeResultsGrid(0);
            view.SetProfileRepositoryWrapper(profileRepositoryWrapper);
        }

        private void OnCommunityProfileOpened(string communityId)
        {
            CommunityProfileOpened?.Invoke(communityId);
        }

        private void OnCommunityJoined(string communityId)
        {
            CommunityJoined?.Invoke(communityId);
        }

        public void Dispose()
        {
            view.BackButtonClicked -= OnBackButtonClicked;
        }

        private void OnBackButtonClicked()
        {
            ResultsBackButtonClicked?.Invoke();
        }

        public void ViewAllMyCommunitiesResults()
        {
            view.SetResultsTitleText(MY_COMMUNITIES_RESULTS_TITLE);

            loadResultsCts = loadResultsCts.SafeRestart();

            LoadResultsAsync(
                    name: string.Empty,
                    onlyMemberOf: true,
                    pageNumber: 1,
                    elementsPerPage: COMMUNITIES_PER_PAGE,
                    false,
                    ct: loadResultsCts.Token)
               .Forget();
        }

        public void ViewAllStreamingCommunities()
        {
            view.SetResultsTitleText(STREAMING_COMMUNITIES_RESULTS_TITLE);

            loadResultsCts = loadResultsCts.SafeRestart();

            LoadResultsAsync(
                    name: string.Empty,
                    onlyMemberOf: false,
                    pageNumber: 1,
                    elementsPerPage: COMMUNITIES_PER_PAGE,
                    false,
                    ct: loadResultsCts.Token,
                    true)
               .Forget();
        }

        public async UniTask LoadAllCommunitiesResultsAsync(bool updateInvitations, CancellationToken ct)
        {
            view.SetResultsTitleText(BROWSE_COMMUNITIES_TITLE);
            loadResultsCts = loadResultsCts.SafeRestartLinked(ct);

            await LoadResultsAsync(
                name: string.Empty,
                onlyMemberOf: false,
                pageNumber: 1,
                elementsPerPage: COMMUNITIES_PER_PAGE,
                updateInvitations,
                ct: loadResultsCts.Token);
        }

        public void TryLoadMoreResults(bool isResultsScrollPositionAtBottom)
        {
            if (isGridResultsLoadingItems ||
                view.CurrentResultsCount >= currentResultsTotalAmount ||
                !isResultsScrollPositionAtBottom)
                return;

            loadResultsCts = loadResultsCts.SafeRestart();

            LoadResultsAsync(
                    name: currentNameFilter,
                    currentOnlyMemberOf,
                    pageNumber: currentPageNumberFilter + 1,
                    elementsPerPage: COMMUNITIES_PER_PAGE,
                    updateJoinRequests: false,
                    ct: loadResultsCts.Token)
               .Forget();
        }

        private async UniTask LoadResultsAsync(string name, bool onlyMemberOf, int pageNumber, int elementsPerPage, bool updateJoinRequests, CancellationToken ct,
            bool isStreaming = false)
        {
            isGridResultsLoadingItems = true;

            if (pageNumber == 1)
            {
                view.ClearResultsItems();
                view.SetAsLoading(true);
            }
            else
                view.SetResultsLoadingMoreActive(true);

            Result<GetUserCommunitiesResponse> result = await dataProvider.GetUserCommunitiesAsync(
                                                                               name,
                                                                               onlyMemberOf,
                                                                               pageNumber,
                                                                               elementsPerPage,
                                                                               ct,
                                                                               isStreaming)
                                                                          .SuppressToResultAsync(ReportCategory.COMMUNITIES);

            if (ct.IsCancellationRequested)
                return;

            if (!result.Success)
            {
                Notifications.NotificationsBusController.Instance.AddNotification(new ServerErrorNotification(ALL_COMMUNITIES_LOADING_ERROR_MESSAGE));
                return;
            }

            if (result.Value.data.results.Length > 0)
            {
                currentPageNumberFilter = pageNumber;
                view.AddResultsItems(result.Value.data.results, pageNumber == 1);
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

        public void LoadSearchResults(string searchText)
        {
            view.SetResultsBackButtonVisible(true);
            view.SetResultsTitleText(string.Format(SEARCH_RESULTS_TITLE_FORMAT, searchText));

            loadResultsCts = loadResultsCts.SafeRestart();

            LoadResultsAsync(
                    name: searchText,
                    onlyMemberOf: false,
                    pageNumber: 1,
                    elementsPerPage: COMMUNITIES_PER_PAGE,
                    updateJoinRequests: false,
                    ct: loadResultsCts.Token)
               .Forget();
        }

        public void UpdateJoinedCommunity(string communityId, bool isSuccess)
        {
            view.UpdateJoinedCommunity(communityId, isSuccess);
        }

        public void SetAsLoading(bool isLoading)
        {
            view.SetAsLoading(isLoading);
        }

        public void RemoveOneMemberFromCounter(string communityId)
        {
            view.RemoveOneMemberFromCounter(communityId);
        }
    }
}
