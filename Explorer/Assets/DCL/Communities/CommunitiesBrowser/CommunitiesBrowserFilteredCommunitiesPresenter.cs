using Cysharp.Threading.Tasks;
using DCL.Communities.CommunitiesBrowser.Commands;
using DCL.Diagnostics;
using DCL.UI.Profiles.Helpers;
using DCL.Utilities.Extensions;
using System;
using System.Threading;
using Utility;
using DCL.Communities.CommunitiesDataProvider.DTOs;
using DCL.NotificationsBus;
using DCL.NotificationsBus.NotificationTypes;
using DCL.Utility.Types;
using DCL.VoiceChat;

namespace DCL.Communities.CommunitiesBrowser
{
    public class CommunitiesBrowserFilteredCommunitiesPresenter : IDisposable
    {
        private const int COMMUNITIES_PER_PAGE = 20;
        private const string MY_COMMUNITIES_RESULTS_TITLE = "My Communities";
        private const string STREAMING_COMMUNITIES_RESULTS_TITLE = "Voice Streaming Communities";
        private const string BROWSE_COMMUNITIES_TITLE = "Browse Communities";
        private const string SEARCH_RESULTS_TITLE_FORMAT = "Results for '{0}'";
        private const string ALL_COMMUNITIES_LOADING_ERROR_MESSAGE = "There was an error loading Communities. Please try again.";

        private readonly FilteredCommunitiesView view;
        private readonly CommunitiesDataProvider.CommunitiesDataProvider dataProvider;
        private readonly CommunitiesBrowserStateService browserStateService;
        private readonly EventSubscriptionScope scope = new ();
        private readonly CommunitiesBrowserEventBus browserEventBus;
        private readonly CommunitiesBrowserCommandsLibrary commandsLibrary;
        private readonly ICommunityCallOrchestrator orchestrator;

        private string currentNameFilter = string.Empty;
        private int currentPageNumberFilter = 1;
        private bool currentOnlyMemberOf;
        private bool isGridResultsLoadingItems;
        private int currentResultsTotalAmount;

        private CancellationTokenSource? loadResultsCts;

        public event Action? ResultsBackButtonClicked;

        public CommunitiesBrowserFilteredCommunitiesPresenter(
            FilteredCommunitiesView view,
            CommunitiesDataProvider.CommunitiesDataProvider dataProvider,
            ProfileRepositoryWrapper profileRepositoryWrapper,
            CommunitiesBrowserStateService browserStateService,
            CommunitiesBrowserEventBus browserEventBus,
            CommunitiesBrowserCommandsLibrary commandsLibrary,
            ICommunityCallOrchestrator orchestrator)
        {
            this.view = view;
            this.dataProvider = dataProvider;
            this.browserStateService = browserStateService;
            this.browserEventBus = browserEventBus;
            this.commandsLibrary = commandsLibrary;
            this.orchestrator = orchestrator;

            view.BackButtonClicked += OnBackButtonClicked;
            view.CommunityJoined += OnCommunityJoined;
            view.CommunityProfileOpened += OnCommunityProfileOpened;
            view.RequestedToJoinCommunity += OnRequestedToJoinCommunity;
            view.RequestToJoinCommunityCanceled += OnRequestToJoinCommunityCanceled;
            view.JoinStreamClicked += OnJoinStream;
            view.GoToStreamClicked += OnGoToStream;

            view.InitializeResultsGrid();
            view.SetProfileRepositoryWrapper(profileRepositoryWrapper);

            scope.Add(browserEventBus.Subscribe<CommunitiesBrowserEvents.UpdateJoinedCommunityEvent>(UpdateJoinedCommunity));
            scope.Add(browserEventBus.Subscribe<CommunitiesBrowserEvents.UserRemovedFromCommunityEvent>(RemoveOneMemberFromCounter));
        }

        private void OnGoToStream(string communityId)
        {
            commandsLibrary.GoToStreamCommand.Execute(communityId);
        }

        public void Dispose()
        {
            view.CommunityProfileOpened -= OnCommunityProfileOpened;
            view.CommunityJoined -= OnCommunityJoined;
            view.BackButtonClicked -= OnBackButtonClicked;
            scope.Dispose();
        }

        private void OnJoinStream(string communityId)
        {
            commandsLibrary.JoinStreamCommand.Execute(communityId);
        }

        private void OnRequestToJoinCommunityCanceled(string communityId, string requestId)
        {
            browserEventBus.RaiseRequestToJoinCommunityCancelledEvent(communityId, requestId);
        }

        private void OnRequestedToJoinCommunity(string communityId)
        {
            browserEventBus.RaiseRequestToJoinCommunityEvent(communityId);
        }

        private void OnCommunityProfileOpened(string communityId)
        {
            browserEventBus.RaiseCommunityProfileOpened(communityId);
        }

        private void OnCommunityJoined(string communityId)
        {
            browserEventBus.RaiseCommunityJoinedClickedEvent(communityId);
        }

        private void RemoveOneMemberFromCounter(CommunitiesBrowserEvents.UserRemovedFromCommunityEvent evt)
        {
            view.RemoveOneMemberFromCounter(evt.CommunityId);
        }

        private void OnBackButtonClicked()
        {
            ResultsBackButtonClicked?.Invoke();
        }

        public void LoadAllMyCommunities()
        {
            view.SetResultsTitleText(MY_COMMUNITIES_RESULTS_TITLE);
            view.SetActiveViewSection(FilteredCommunitiesView.ActiveViewSection.MY_COMMUNITIES);

            loadResultsCts = loadResultsCts.SafeRestart();

            LoadResultsAsync(
                    name: string.Empty,
                    onlyMemberOf: true,
                    pageNumber: 1,
                    elementsPerPage: COMMUNITIES_PER_PAGE,
                    ct: loadResultsCts.Token)
               .Forget();
        }

        public void LoadAllStreamingCommunities()
        {
            view.SetResultsTitleText(STREAMING_COMMUNITIES_RESULTS_TITLE);
            view.SetActiveViewSection(FilteredCommunitiesView.ActiveViewSection.STREAMING);

            loadResultsCts = loadResultsCts.SafeRestart();

            LoadResultsAsync(
                    name: string.Empty,
                    onlyMemberOf: false,
                    pageNumber: 1,
                    elementsPerPage: COMMUNITIES_PER_PAGE,
                    ct: loadResultsCts.Token,
                    true)
               .Forget();
        }

        public async UniTask LoadAllCommunitiesAsync(CancellationToken ct)
        {
            view.SetResultsTitleText(BROWSE_COMMUNITIES_TITLE);
            view.SetActiveViewSection(FilteredCommunitiesView.ActiveViewSection.ALL_COMMUNITIES);
            loadResultsCts = loadResultsCts.SafeRestartLinked(ct);

            await LoadResultsAsync(
                name: string.Empty,
                onlyMemberOf: false,
                pageNumber: 1,
                elementsPerPage: COMMUNITIES_PER_PAGE,
                ct: loadResultsCts.Token,
                isStreaming: false);
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
                    ct: loadResultsCts.Token)
               .Forget();
        }

        private async UniTask LoadResultsAsync(
            string name,
            bool onlyMemberOf,
            int pageNumber,
            int elementsPerPage,
            CancellationToken ct,
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
                                                                               isStreaming: isStreaming)
                                                                          .SuppressToResultAsync(ReportCategory.COMMUNITIES);

            if (ct.IsCancellationRequested)
                return;

            if (!result.Success)
            {
                NotificationsBusController.Instance.AddNotification(new ServerErrorNotification(ALL_COMMUNITIES_LOADING_ERROR_MESSAGE));
                return;
            }

            if (result.Value.data.results.Length > 0)
            {
                // Match the current join requests with the results to know what communities have been requested to join
                foreach (GetUserCommunitiesData.CommunityData communityData in result.Value.data.results)
                {
                    communityData.pendingActionType = InviteRequestAction.none;

                    foreach (GetUserInviteRequestData.UserInviteRequestData joinRequest in browserStateService.CurrentJoinRequests)
                    {
                        if (communityData.id == joinRequest.communityId)
                        {
                            communityData.pendingActionType = InviteRequestAction.request_to_join;
                            communityData.inviteOrRequestId = joinRequest.id;
                            break;
                        }
                    }
                }

                currentPageNumberFilter = pageNumber;
                browserStateService.AddCommunities(result.Value.data.results);
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
            view.SetActiveViewSection(FilteredCommunitiesView.ActiveViewSection.SEARCH_COMMUNITIES);
            view.SetResultsBackButtonVisible(true);
            view.SetResultsTitleText(string.Format(SEARCH_RESULTS_TITLE_FORMAT, searchText));

            loadResultsCts = loadResultsCts.SafeRestart();

            LoadResultsAsync(
                    name: searchText,
                    onlyMemberOf: false,
                    pageNumber: 1,
                    elementsPerPage: COMMUNITIES_PER_PAGE,
                    ct: loadResultsCts.Token)
               .Forget();
        }

        private void UpdateJoinedCommunity(CommunitiesBrowserEvents.UpdateJoinedCommunityEvent evt)
        {
            view.UpdateJoinedCommunity(evt.CommunityId, evt.Success);
        }

        public void SetAsLoading(bool isLoading)
        {
            view.SetAsLoading(isLoading);
        }

        public void Deactivate()
        {
            loadResultsCts.SafeCancelAndDispose();
        }
    }
}
