using Cysharp.Threading.Tasks;
using DCL.Communities.CommunityCreation;
using DCL.Communities.CommunitiesCard;
using DCL.Communities.CommunitiesDataProvider.DTOs;
using DCL.Diagnostics;
using DCL.Input;
using DCL.Input.Component;
using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.UI;
using DCL.UI.Profiles.Helpers;
using DCL.Utilities.Extensions;
using DCL.Web3;
using DCL.WebRequests;
using MVC;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.Communities.CommunitiesBrowser
{
    public class CommunitiesBrowserController : ISection, IDisposable
    {
        private const int COMMUNITIES_PER_PAGE = 20;
        private const string MY_COMMUNITIES_RESULTS_TITLE = "My Communities";
        private const string MY_GENERAL_RESULTS_TITLE = "Browse Communities";
        private const string INVITES_RESULTS_TITLE = "Invites";
        private const string REQUESTS_RESULTS_TITLE = "Requests";
        private const string INVITES_AND_REQUESTS_RESULTS_TITLE = "Invites & Requests";
        private const int SEARCH_AWAIT_TIME = 1000;
        private const string SEARCH_RESULTS_TITLE_FORMAT = "Results for '{0}'";
        private const string MY_COMMUNITIES_LOADING_ERROR_MESSAGE = "There was an error loading My Communities. Please try again.";
        private const string ALL_COMMUNITIES_LOADING_ERROR_MESSAGE = "There was an error loading Communities. Please try again.";
        private const string INVITATIONS_COMMUNITIES_LOADING_ERROR_MESSAGE = "There was an error loading invites. Please try again.";
        private const string REQUESTS_COMMUNITIES_LOADING_ERROR_MESSAGE = "There was an error loading requests. Please try again.";
        private const string JOIN_COMMUNITY_ERROR_MESSAGE = "There was an error joining community. Please try again.";
        private const string REQUEST_TO_JOIN_COMMUNITY_ERROR_MESSAGE = "There was an error requesting to join community. Please try again.";
        private const string CANCEL_REQUEST_TO_JOIN_COMMUNITY_ERROR_MESSAGE = "There was an error cancelling join request. Please try again.";
        private const string ACCEPT_COMMUNITY_INVITATION_ERROR_MESSAGE = "There was an error accepting community invitation. Please try again.";
        private const string REJECT_COMMUNITY_INVITATION_ERROR_MESSAGE = "There was an error rejecting community invitation. Please try again.";
        private const int WARNING_MESSAGE_DELAY_MS = 3000;

        private readonly CommunitiesBrowserView view;
        private readonly RectTransform rectTransform;
        private readonly ICursor cursor;
        private readonly CommunitiesDataProvider.CommunitiesDataProvider dataProvider;
        private readonly IInputBlock inputBlock;
        private readonly WarningNotificationView warningNotificationView;
        private readonly IMVCManager mvcManager;
        private readonly ProfileRepositoryWrapper profileRepositoryWrapper;
        private readonly ISelfProfile selfProfile;
        private readonly INftNamesProvider nftNamesProvider;
        private readonly ISpriteCache spriteCache;

        private CancellationTokenSource? loadMyCommunitiesCts;
        private CancellationTokenSource? loadResultsCts;
        private CancellationTokenSource? searchCancellationCts;
        private CancellationTokenSource? showErrorCts;
        private CancellationTokenSource? openCommunityCreationCts;
        private CancellationTokenSource? updateInvitesCounterCts;
        private CancellationTokenSource? joinCommunityCts;
        private CancellationTokenSource? requestToJoinCommunityCts;
        private CancellationTokenSource? cancelRequestToJoinCommunityCts;
        private CancellationTokenSource? acceptCommunityInvitationCts;
        private CancellationTokenSource? rejectCommunityInvitationCts;

        private bool isSectionActivated;
        private bool isInvitesAndRequestsSectionActive;
        private string currentNameFilter;
        private bool currentIsOwnerFilter;
        private bool currentIsMemberFilter;
        private int currentPageNumberFilter = 1;
        private int currentResultsTotalAmount;
        private string currentSearchText = string.Empty;
        private bool currentOnlyMemberOf;
        private bool isGridResultsLoadingItems;
        private readonly List<GetUserInviteRequestData.UserInviteRequestData> currentInvitations = new ();
        private readonly List<GetUserInviteRequestData.UserInviteRequestData> currentJoinRequests = new ();

        public CommunitiesBrowserController(
            CommunitiesBrowserView view,
            ICursor cursor,
            CommunitiesDataProvider.CommunitiesDataProvider dataProvider,
            IWebRequestController webRequestController,
            IInputBlock inputBlock,
            WarningNotificationView warningNotificationView,
            IMVCManager mvcManager,
            ProfileRepositoryWrapper profileDataProvider,
            ISelfProfile selfProfile,
            INftNamesProvider nftNamesProvider)
        {
            this.view = view;
            rectTransform = view.transform.parent.GetComponent<RectTransform>();
            this.cursor = cursor;
            this.dataProvider = dataProvider;
            this.inputBlock = inputBlock;
            this.profileRepositoryWrapper = profileDataProvider;
            this.warningNotificationView = warningNotificationView;
            this.mvcManager = mvcManager;
            this.selfProfile = selfProfile;
            this.nftNamesProvider = nftNamesProvider;

            spriteCache = new SpriteCache(webRequestController);

            ConfigureMyCommunitiesList();
            ConfigureResultsGrid();
            view.InvitesAndRequestsView.Initialize(profileRepositoryWrapper);
            view.SetThumbnailLoader(new ThumbnailLoader(spriteCache));

            view.ViewAllMyCommunitiesButtonClicked += ViewAllMyCommunitiesResults;
            view.ResultsBackButtonClicked += OnResultsBackButtonClicked;
            view.InvitesAndRequestsView.InvitesAndRequestsButtonClicked += LoadInvitesAndRequestsResults;
            view.SearchBarSelected += DisableShortcutsInput;
            view.SearchBarDeselected += RestoreInput;
            view.SearchBarValueChanged += SearchBarValueChanged;
            view.SearchBarSubmit += SearchBarSubmit;
            view.SearchBarClearButtonClicked += SearchBarCleared;
            view.CommunityProfileOpened += OpenCommunityProfile;
            view.CommunityJoined += JoinCommunity;
            view.CommunityRequestedToJoin += RequestToJoinCommunity;
            view.CommunityRequestToJoinCanceled += CancelRequestToJoinCommunity;
            view.CommunityInvitationAccepted += AcceptCommunityInvitation;
            view.CommunityInvitationRejected += RejectCommunityInvitation;
            view.CreateCommunityButtonClicked += CreateCommunity;
        }

        public void Activate()
        {
            if (isSectionActivated)
                return;

            isSectionActivated = true;
            view.SetViewActive(true);
            cursor.Unlock();
            ReloadBrowser();

            SubscribeDataProviderEvents();
        }

        public void Deactivate()
        {
            isSectionActivated = false;
            view.SetViewActive(false);
            loadMyCommunitiesCts?.SafeCancelAndDispose();
            loadResultsCts?.SafeCancelAndDispose();
            searchCancellationCts?.SafeCancelAndDispose();
            showErrorCts?.SafeCancelAndDispose();
            openCommunityCreationCts?.SafeCancelAndDispose();
            updateInvitesCounterCts?.SafeCancelAndDispose();
            joinCommunityCts?.SafeCancelAndDispose();
            requestToJoinCommunityCts?.SafeCancelAndDispose();
            cancelRequestToJoinCommunityCts?.SafeCancelAndDispose();
            acceptCommunityInvitationCts?.SafeCancelAndDispose();
            rejectCommunityInvitationCts?.SafeCancelAndDispose();
            spriteCache.Clear();

            UnsubscribeDataProviderEvents();
        }

        public void Animate(int triggerId) =>
            view.PlayAnimator(triggerId);

        public void ResetAnimator() =>
            view.ResetAnimator();

        public RectTransform GetRectTransform() =>
            rectTransform;

        public void Dispose()
        {
            view.ResultsLoopGridScrollChanged -= LoadMoreResults;
            view.ViewAllMyCommunitiesButtonClicked -= ViewAllMyCommunitiesResults;
            view.InvitesAndRequestsView.InvitesAndRequestsButtonClicked -= LoadInvitesAndRequestsResults;
            view.ResultsBackButtonClicked -= OnResultsBackButtonClicked;
            view.SearchBarSelected -= DisableShortcutsInput;
            view.SearchBarDeselected -= RestoreInput;
            view.SearchBarValueChanged -= SearchBarValueChanged;
            view.SearchBarSubmit -= SearchBarSubmit;
            view.SearchBarClearButtonClicked -= SearchBarCleared;
            view.CommunityProfileOpened -= OpenCommunityProfile;
            view.CommunityJoined -= JoinCommunity;
            view.CommunityRequestedToJoin -= RequestToJoinCommunity;
            view.CommunityRequestToJoinCanceled -= CancelRequestToJoinCommunity;
            view.CommunityInvitationAccepted -= AcceptCommunityInvitation;
            view.CommunityInvitationRejected -= RejectCommunityInvitation;
            view.CreateCommunityButtonClicked -= CreateCommunity;

            UnsubscribeDataProviderEvents();

            loadMyCommunitiesCts?.SafeCancelAndDispose();
            loadResultsCts?.SafeCancelAndDispose();
            searchCancellationCts?.SafeCancelAndDispose();
            showErrorCts?.SafeCancelAndDispose();
            openCommunityCreationCts?.SafeCancelAndDispose();
            updateInvitesCounterCts?.SafeCancelAndDispose();
            joinCommunityCts?.SafeCancelAndDispose();
            requestToJoinCommunityCts?.SafeCancelAndDispose();
            cancelRequestToJoinCommunityCts?.SafeCancelAndDispose();
            acceptCommunityInvitationCts?.SafeCancelAndDispose();
            rejectCommunityInvitationCts?.SafeCancelAndDispose();
            spriteCache.Clear();
        }

        private void ReloadBrowser()
        {
            loadMyCommunitiesCts = loadMyCommunitiesCts.SafeRestart();
            LoadMyCommunitiesAsync(loadMyCommunitiesCts.Token).Forget();
            LoadAllCommunitiesResults(updateInvitations: true);
            RefreshInvitesCounter();
        }

        private void ConfigureMyCommunitiesList() =>
            view.InitializeMyCommunitiesList(0, spriteCache);

        private void ConfigureResultsGrid()
        {
            view.InitializeResultsGrid(0, profileRepositoryWrapper, spriteCache);
            view.ResultsLoopGridScrollChanged += LoadMoreResults;
        }

        private async UniTaskVoid LoadMyCommunitiesAsync(CancellationToken ct)
        {
            view.ClearMyCommunitiesItems();
            view.SetMyCommunitiesAsLoading(true);

            var result = await dataProvider.GetUserCommunitiesAsync(
                                                name: string.Empty,
                                                onlyMemberOf: true,
                                                pageNumber: 1,
                                                elementsPerPage: 1000,
                                                ct: ct).SuppressToResultAsync(ReportCategory.COMMUNITIES);

            if (ct.IsCancellationRequested)
                return;

            if (!result.Success)
            {
                showErrorCts = showErrorCts.SafeRestart();
                await warningNotificationView.AnimatedShowAsync(MY_COMMUNITIES_LOADING_ERROR_MESSAGE, WARNING_MESSAGE_DELAY_MS, showErrorCts.Token)
                                             .SuppressToResultAsync(ReportCategory.COMMUNITIES);
                return;
            }

            view.AddMyCommunitiesItems(result.Value.data.results, true);
            view.SetMyCommunitiesAsLoading(false);
        }

        private void ViewAllMyCommunitiesResults()
        {
            ClearSearchBar();
            view.SetResultsBackButtonVisible(true);
            view.SetResultsTitleText(MY_COMMUNITIES_RESULTS_TITLE);
            view.SetResultsCountTextActive(true);
            view.SetResultsSectionActive(true);
            view.InvitesAndRequestsView.SetSectionActive(false);
            isInvitesAndRequestsSectionActive = false;

            loadResultsCts = loadResultsCts.SafeRestart();
            LoadResultsAsync(
                name: string.Empty,
                onlyMemberOf: true,
                pageNumber: 1,
                elementsPerPage: COMMUNITIES_PER_PAGE,
                updateJoinRequests: false,
                ct: loadResultsCts.Token).Forget();

        }

        private void OnResultsBackButtonClicked() =>
            LoadAllCommunitiesResults();

        private void LoadAllCommunitiesResults(bool updateInvitations = false)
        {
            ClearSearchBar();
            loadResultsCts = loadResultsCts.SafeRestart();
            LoadResultsAsync(
                name: string.Empty,
                onlyMemberOf: false,
                pageNumber: 1,
                elementsPerPage: COMMUNITIES_PER_PAGE,
                updateInvitations,
                ct: loadResultsCts.Token).Forget();

            view.SetResultsBackButtonVisible(false);
            view.SetResultsTitleText(MY_GENERAL_RESULTS_TITLE);
            view.SetResultsCountTextActive(true);
            view.SetResultsSectionActive(true);
            view.InvitesAndRequestsView.SetSectionActive(false);
            isInvitesAndRequestsSectionActive = false;
        }

        private void LoadMoreResults(Vector2 _)
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
                updateJoinRequests: false,
                ct: loadResultsCts.Token).Forget();
        }

        private async UniTaskVoid LoadResultsAsync(string name, bool onlyMemberOf, int pageNumber, int elementsPerPage, bool updateJoinRequests, CancellationToken ct)
        {
            if (updateJoinRequests)
                await LoadRequestsAsync(ct);

            isGridResultsLoadingItems = true;

            if (pageNumber == 1)
            {
                view.ClearResultsItems();
                view.SetResultsAsLoading(true);
            }
            else
                view.SetResultsLoadingMoreActive(true);

            var result = await dataProvider.GetUserCommunitiesAsync(
                name,
                onlyMemberOf,
                pageNumber,
                elementsPerPage,
                ct).SuppressToResultAsync(ReportCategory.COMMUNITIES);

            if (ct.IsCancellationRequested)
                return;

            if (!result.Success)
            {
                showErrorCts = showErrorCts.SafeRestart();
                await warningNotificationView.AnimatedShowAsync(ALL_COMMUNITIES_LOADING_ERROR_MESSAGE, WARNING_MESSAGE_DELAY_MS, showErrorCts.Token)
                                             .SuppressToResultAsync(ReportCategory.COMMUNITIES);
                return;
            }

            if (result.Value.data.results.Length > 0)
            {
                // Match the current join requests with the results to know what communities have been requested to join
                foreach (GetUserCommunitiesData.CommunityData communityData in result.Value.data.results)
                {
                    communityData.pendingActionType = InviteRequestAction.none;
                    foreach (GetUserInviteRequestData.UserInviteRequestData joinRequest in currentJoinRequests)
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
                view.AddResultsItems(result.Value.data.results, pageNumber == 1);
            }

            currentResultsTotalAmount = result.Value.data.total;

            if (pageNumber == 1)
                view.SetResultsAsLoading(false);

            view.SetResultsLoadingMoreActive(false);
            view.SetResultsCountText(currentResultsTotalAmount);

            currentNameFilter = name;
            currentOnlyMemberOf = onlyMemberOf;
            isGridResultsLoadingItems = false;
        }

        private void LoadInvitesAndRequestsResults()
        {
            ClearSearchBar();
            view.SetResultsBackButtonVisible(true);
            view.SetResultsTitleText(INVITES_AND_REQUESTS_RESULTS_TITLE);
            view.SetResultsCountTextActive(false);
            view.SetResultsSectionActive(false);
            view.InvitesAndRequestsView.SetSectionActive(true);
            isInvitesAndRequestsSectionActive = true;

            loadResultsCts = loadResultsCts.SafeRestart();
            LoadInvitesAndRequestsAsync(loadResultsCts.Token).Forget();
        }

        private async UniTaskVoid LoadInvitesAndRequestsAsync(CancellationToken ct)
        {
            view.InvitesAndRequestsView.SetAsLoading(true);
            int invitesCount = await LoadInvitesAsync(updateInvitesGrid: true, ct);
            view.InvitesAndRequestsView.SetInvitesTitle($"{INVITES_RESULTS_TITLE} ({invitesCount})");
            if (ct.IsCancellationRequested) return;
            int requestsCount = await LoadRequestsAsync(ct);
            view.InvitesAndRequestsView.SetRequestsTitle($"{REQUESTS_RESULTS_TITLE} ({requestsCount})");
            view.InvitesAndRequestsView.SetAsLoading(false);
            view.InvitesAndRequestsView.SetInvitesAndRequestsAsEmpty(invitesCount == 0 && requestsCount == 0);
        }

        private async UniTask<int> LoadInvitesAsync(bool updateInvitesGrid, CancellationToken ct)
        {
            view.InvitesAndRequestsView.ClearInvitesItems();
            currentInvitations.Clear();

            var invitesResult = await dataProvider.GetUserInviteRequestAsync(
                InviteRequestAction.invite,
                ct).SuppressToResultAsync(ReportCategory.COMMUNITIES);

            if (ct.IsCancellationRequested)
                return 0;

            if (!invitesResult.Success)
            {
                showErrorCts = showErrorCts.SafeRestart();
                await warningNotificationView.AnimatedShowAsync(INVITATIONS_COMMUNITIES_LOADING_ERROR_MESSAGE, WARNING_MESSAGE_DELAY_MS, showErrorCts.Token)
                                             .SuppressToResultAsync(ReportCategory.COMMUNITIES);
                return 0;
            }

            int invitesCount = invitesResult.Value.data.results.Length;

            if (updateInvitesGrid && invitesCount > 0)
            {
                view.InvitesAndRequestsView.SetInvitesItems(invitesResult.Value.data.results);
                currentInvitations.AddRange(invitesResult.Value.data.results);
            }

            view.InvitesAndRequestsView.SetInvitesCounter(invitesCount);

            return invitesCount;
        }

        private async UniTask<int> LoadRequestsAsync(CancellationToken ct)
        {
            view.InvitesAndRequestsView.ClearRequestsItems();
            currentJoinRequests.Clear();

            var requestsResult = await dataProvider.GetUserInviteRequestAsync(
                InviteRequestAction.request_to_join,
                ct).SuppressToResultAsync(ReportCategory.COMMUNITIES);

            if (ct.IsCancellationRequested)
                return 0;

            if (!requestsResult.Success)
            {
                showErrorCts = showErrorCts.SafeRestart();
                await warningNotificationView.AnimatedShowAsync(REQUESTS_COMMUNITIES_LOADING_ERROR_MESSAGE, WARNING_MESSAGE_DELAY_MS, showErrorCts.Token)
                                             .SuppressToResultAsync(ReportCategory.COMMUNITIES);
                return 0;
            }

            if (requestsResult.Value.data.results.Length > 0)
            {
                view.InvitesAndRequestsView.SetRequestsItems(requestsResult.Value.data.results);
                currentJoinRequests.AddRange(requestsResult.Value.data.results);
            }

            return requestsResult.Value.data.results.Length;
        }

        private void RefreshInvitesCounter(bool setCounterToZeroAtTheBeginning = true)
        {
            if (setCounterToZeroAtTheBeginning)
                view.InvitesAndRequestsView.SetInvitesCounter(0);

            // Get the current invites to update the invite counter on the main page
            updateInvitesCounterCts = updateInvitesCounterCts.SafeRestart();
            LoadInvitesAsync(updateInvitesGrid: false, updateInvitesCounterCts.Token).Forget();
        }

        private void DisableShortcutsInput(string text) =>
            inputBlock.Disable(InputMapComponent.Kind.SHORTCUTS, InputMapComponent.Kind.IN_WORLD_CAMERA);

        private void RestoreInput(string text) =>
            inputBlock.Enable(InputMapComponent.Kind.SHORTCUTS, InputMapComponent.Kind.IN_WORLD_CAMERA);

        private void SearchBarValueChanged(string searchText)
        {
            searchCancellationCts = searchCancellationCts.SafeRestart();
            AwaitAndSendSearchAsync(searchText, searchCancellationCts.Token).Forget();
        }

        private void SearchBarSubmit(string searchText)
        {
            searchCancellationCts = searchCancellationCts.SafeRestart();
            AwaitAndSendSearchAsync(searchText, searchCancellationCts.Token, skipAwait: true).Forget();
        }

        private async UniTaskVoid AwaitAndSendSearchAsync(string searchText, CancellationToken ct, bool skipAwait = false)
        {
            if (!skipAwait)
                await UniTask.Delay(SEARCH_AWAIT_TIME, cancellationToken: ct);

            if (currentSearchText == searchText)
                return;

            if (string.IsNullOrEmpty(searchText))
                LoadAllCommunitiesResults();
            else
            {
                view.SetResultsBackButtonVisible(true);
                view.SetResultsTitleText(string.Format(SEARCH_RESULTS_TITLE_FORMAT, searchText));
                view.SetResultsCountTextActive(true);
                view.SetResultsSectionActive(true);
                view.InvitesAndRequestsView.SetSectionActive(false);
                isInvitesAndRequestsSectionActive = false;

                loadResultsCts = loadResultsCts.SafeRestart();
                LoadResultsAsync(
                    name: searchText,
                    onlyMemberOf: false,
                    pageNumber: 1,
                    elementsPerPage: COMMUNITIES_PER_PAGE,
                    updateJoinRequests: false,
                    ct: loadResultsCts.Token).Forget();
            }

            currentSearchText = searchText;
        }

        private void SearchBarCleared()
        {
            ClearSearchBar();
            LoadAllCommunitiesResults();
        }

        private void ClearSearchBar()
        {
            currentSearchText = string.Empty;
            view.CleanSearchBar(raiseOnChangeEvent: false);
        }

        private void JoinCommunity(string communityId)
        {
            joinCommunityCts = joinCommunityCts.SafeRestart();
            JoinCommunityAsync(communityId, joinCommunityCts.Token).Forget();
        }

        private async UniTaskVoid JoinCommunityAsync(string communityId, CancellationToken ct)
        {
            var result = await dataProvider.JoinCommunityAsync(communityId, ct).SuppressToResultAsync(ReportCategory.COMMUNITIES);

            if (ct.IsCancellationRequested)
                return;

            if (!result.Success || !result.Value)
            {
                showErrorCts = showErrorCts.SafeRestart();
                await warningNotificationView.AnimatedShowAsync(JOIN_COMMUNITY_ERROR_MESSAGE, WARNING_MESSAGE_DELAY_MS, showErrorCts.Token)
                                             .SuppressToResultAsync(ReportCategory.COMMUNITIES);
            }
        }

        private void RequestToJoinCommunity(string communityId)
        {
            requestToJoinCommunityCts = requestToJoinCommunityCts.SafeRestart();
            RequestToJoinCommunityAsync(communityId, requestToJoinCommunityCts.Token).Forget();
        }

        private async UniTaskVoid RequestToJoinCommunityAsync(string communityId, CancellationToken ct)
        {
            var ownProfile = await selfProfile.ProfileAsync(ct);
            if (ownProfile == null)
                return;

            var result = await dataProvider.SendInviteOrRequestToJoinAsync(communityId, ownProfile.UserId, InviteRequestAction.request_to_join, ct)
                                           .SuppressToResultAsync(ReportCategory.COMMUNITIES);

            if (ct.IsCancellationRequested)
                return;

            if (!result.Success || !result.Value)
            {
                showErrorCts = showErrorCts.SafeRestart();
                await warningNotificationView.AnimatedShowAsync(REQUEST_TO_JOIN_COMMUNITY_ERROR_MESSAGE, WARNING_MESSAGE_DELAY_MS, showErrorCts.Token)
                                             .SuppressToResultAsync(ReportCategory.COMMUNITIES);
            }
        }

        private void CancelRequestToJoinCommunity(string communityId, string requestId)
        {
            cancelRequestToJoinCommunityCts = requestToJoinCommunityCts.SafeRestart();
            CancelRequestToJoinCommunityAsync(communityId, requestId, cancelRequestToJoinCommunityCts.Token).Forget();
        }

        private async UniTaskVoid CancelRequestToJoinCommunityAsync(string communityId, string requestId, CancellationToken ct)
        {
            var result = await dataProvider.ManageInviteRequestToJoinAsync(communityId, requestId, InviteRequestIntention.cancel, ct)
                                           .SuppressToResultAsync(ReportCategory.COMMUNITIES);

            if (ct.IsCancellationRequested)
                return;

            if (!result.Success || !result.Value)
            {
                showErrorCts = showErrorCts.SafeRestart();
                await warningNotificationView.AnimatedShowAsync(CANCEL_REQUEST_TO_JOIN_COMMUNITY_ERROR_MESSAGE, WARNING_MESSAGE_DELAY_MS, showErrorCts.Token)
                                             .SuppressToResultAsync(ReportCategory.COMMUNITIES);
            }
        }

        private void AcceptCommunityInvitation(string communityId, string invitationId)
        {
            acceptCommunityInvitationCts = acceptCommunityInvitationCts.SafeRestart();
            AcceptCommunityInvitationAsync(communityId, invitationId, acceptCommunityInvitationCts.Token).Forget();
        }

        private async UniTaskVoid AcceptCommunityInvitationAsync(string communityId, string invitationId, CancellationToken ct)
        {
            var result = await dataProvider.ManageInviteRequestToJoinAsync(communityId, invitationId, InviteRequestIntention.accept, ct)
                                           .SuppressToResultAsync(ReportCategory.COMMUNITIES);

            if (ct.IsCancellationRequested)
                return;

            if (!result.Success || !result.Value)
            {
                showErrorCts = showErrorCts.SafeRestart();
                await warningNotificationView.AnimatedShowAsync(ACCEPT_COMMUNITY_INVITATION_ERROR_MESSAGE, WARNING_MESSAGE_DELAY_MS, showErrorCts.Token)
                                             .SuppressToResultAsync(ReportCategory.COMMUNITIES);
            }
        }

        private void RejectCommunityInvitation(string communityId, string invitationId)
        {
            rejectCommunityInvitationCts = rejectCommunityInvitationCts.SafeRestart();
            RejectCommunityInvitationAsync(communityId, invitationId, rejectCommunityInvitationCts.Token).Forget();
        }

        private async UniTaskVoid RejectCommunityInvitationAsync(string communityId, string invitationId, CancellationToken ct)
        {
            var result = await dataProvider.ManageInviteRequestToJoinAsync(communityId, invitationId, InviteRequestIntention.reject, ct)
                                           .SuppressToResultAsync(ReportCategory.COMMUNITIES);

            if (ct.IsCancellationRequested)
                return;

            if (!result.Success || !result.Value)
            {
                showErrorCts = showErrorCts.SafeRestart();
                await warningNotificationView.AnimatedShowAsync(REJECT_COMMUNITY_INVITATION_ERROR_MESSAGE, WARNING_MESSAGE_DELAY_MS, showErrorCts.Token)
                                             .SuppressToResultAsync(ReportCategory.COMMUNITIES);
            }
        }

        private void OpenCommunityProfile(string communityId) =>
            mvcManager.ShowAsync(CommunityCardController.IssueCommand(new CommunityCardParameter(communityId, spriteCache))).Forget();

        private void CreateCommunity()
        {
            openCommunityCreationCts = openCommunityCreationCts.SafeRestart();
            CreateCommunityAsync(openCommunityCreationCts.Token).Forget();
        }

        private async UniTaskVoid CreateCommunityAsync(CancellationToken ct)
        {
            var canCreate = false;
            var ownProfile = await selfProfile.ProfileAsync(ct);

            if (ownProfile != null)
            {
                INftNamesProvider.PaginatedNamesResponse names = await nftNamesProvider.GetAsync(new Web3Address(ownProfile.UserId), 1, 1, ct);
                canCreate = names.TotalAmount > 0;
            }

            mvcManager.ShowAsync(
                CommunityCreationEditionController.IssueCommand(new CommunityCreationEditionParameter(
                    canCreateCommunities: canCreate,
                    communityId: string.Empty,
                    spriteCache)), ct).Forget();
        }

        private void OnCommunityUpdated(string _) =>
            ReloadBrowser();

        private void OnCommunityJoined(string communityId, bool success) =>
            view.UpdateJoinedCommunity(communityId, true, success);

        private void OnCommunityRequestedToJoin(string communityId, bool success)
        {
            var alreadyExistsInvitation = false;
            foreach (GetUserInviteRequestData.UserInviteRequestData invitation in currentInvitations)
            {
                if (communityId == invitation.communityId)
                {
                    alreadyExistsInvitation = true;
                    RefreshInvitesCounter(setCounterToZeroAtTheBeginning: false);
                    break;
                }
            }

            view.UpdateRequestedToJoinCommunity(communityId, true, success, alreadyExistsInvitation);
        }

        private void OnCommunityRequestToJoinCancelled(string communityId, bool success)
        {
            if (!isInvitesAndRequestsSectionActive)
                view.UpdateRequestedToJoinCommunity(communityId, false, success, false);
            else
                LoadInvitesAndRequestsResults();
        }

        private void OnCommunityInvitationAccepted(string communityId)
        {
            if (!isInvitesAndRequestsSectionActive)
                return;

            LoadInvitesAndRequestsResults();
        }

        private void OnCommunityInvitationRejected(string communityId)
        {
            if (!isInvitesAndRequestsSectionActive)
                return;

            LoadInvitesAndRequestsResults();
        }

        private void OnCommunityLeft(string communityId, bool success) =>
            view.UpdateJoinedCommunity(communityId, false, success);

        private void OnCommunityCreated(CreateOrUpdateCommunityResponse.CommunityData newCommunity) =>
            ReloadBrowser();

        private void OnCommunityDeleted(string communityId) =>
            ReloadBrowser();

        private void OnUserRemovedFromCommunity(string communityId) =>
            view.RemoveOneMemberFromCounter(communityId);

        private void OnUserBannedFromCommunity(string communityId, string userAddress) =>
            OnUserRemovedFromCommunity(communityId);

        private void SubscribeDataProviderEvents()
        {
            dataProvider.CommunityCreated += OnCommunityCreated;
            dataProvider.CommunityDeleted += OnCommunityDeleted;
            dataProvider.CommunityUpdated += OnCommunityUpdated;
            dataProvider.CommunityJoined += OnCommunityJoined;
            dataProvider.CommunityRequestedToJoin += OnCommunityRequestedToJoin;
            dataProvider.CommunityRequestToJoinCancelled += OnCommunityRequestToJoinCancelled;
            dataProvider.CommunityInvitationAccepted += OnCommunityInvitationAccepted;
            dataProvider.CommunityInvitationRejected += OnCommunityInvitationRejected;
            dataProvider.CommunityLeft += OnCommunityLeft;
            dataProvider.CommunityUserRemoved += OnUserRemovedFromCommunity;
            dataProvider.CommunityUserBanned += OnUserBannedFromCommunity;
        }

        private void UnsubscribeDataProviderEvents()
        {
            dataProvider.CommunityCreated -= OnCommunityCreated;
            dataProvider.CommunityDeleted -= OnCommunityDeleted;
            dataProvider.CommunityUpdated -= OnCommunityUpdated;
            dataProvider.CommunityJoined -= OnCommunityJoined;
            dataProvider.CommunityRequestedToJoin -= OnCommunityRequestedToJoin;
            dataProvider.CommunityRequestToJoinCancelled -= OnCommunityRequestToJoinCancelled;
            dataProvider.CommunityInvitationAccepted -= OnCommunityInvitationAccepted;
            dataProvider.CommunityInvitationRejected -= OnCommunityInvitationRejected;
            dataProvider.CommunityLeft -= OnCommunityLeft;
            dataProvider.CommunityUserRemoved -= OnUserRemovedFromCommunity;
            dataProvider.CommunityUserBanned -= OnUserBannedFromCommunity;
        }
    }
}
