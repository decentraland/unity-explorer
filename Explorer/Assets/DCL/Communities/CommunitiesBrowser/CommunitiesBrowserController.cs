using Cysharp.Threading.Tasks;
using DCL.Communities.CommunityCreation;
using DCL.Communities.CommunitiesCard;
using DCL.Communities.CommunitiesDataProvider.DTOs;
using DCL.Diagnostics;
using DCL.Input;
using DCL.Input.Component;
using DCL.NotificationsBusController.NotificationTypes;
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
using Notifications = DCL.NotificationsBusController.NotificationsBus;

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

        private readonly CommunitiesBrowserView view;
        private readonly RectTransform rectTransform;
        private readonly ICursor cursor;
        private readonly CommunitiesDataProvider.CommunitiesDataProvider dataProvider;
        private readonly IInputBlock inputBlock;
        private readonly IMVCManager mvcManager;
        private readonly ProfileRepositoryWrapper profileRepositoryWrapper;
        private readonly ISelfProfile selfProfile;
        private readonly INftNamesProvider nftNamesProvider;
        private readonly ISpriteCache spriteCache;

        private CancellationTokenSource? loadMyCommunitiesCts;
        private CancellationTokenSource? loadResultsCts;
        private CancellationTokenSource? searchCancellationCts;
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

            Notifications.NotificationsBusController.Instance.SubscribeToNotificationTypeReceived(NotificationType.COMMUNITY_REQUEST_TO_JOIN_RECEIVED, OnJoinRequestReceived);
            Notifications.NotificationsBusController.Instance.SubscribeToNotificationTypeReceived(NotificationType.COMMUNITY_INVITE_RECEIVED, OnInvitationReceived);
            Notifications.NotificationsBusController.Instance.SubscribeToNotificationTypeReceived(NotificationType.COMMUNITY_REQUEST_TO_JOIN_ACCEPTED, OnJoinRequestAccepted);
            Notifications.NotificationsBusController.Instance.SubscribeToNotificationTypeReceived(NotificationType.COMMUNITY_DELETED_CONTENT_VIOLATION, OnCommunityDeleted);
            Notifications.NotificationsBusController.Instance.SubscribeToNotificationTypeReceived(NotificationType.COMMUNITY_DELETED, OnCommunityDeleted);
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
            LoadMyCommunities();
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

        private void LoadMyCommunities()
        {
            loadMyCommunitiesCts = loadMyCommunitiesCts.SafeRestart();
            LoadMyCommunitiesAsync(loadMyCommunitiesCts.Token).Forget();
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
                                                ct: ct,
                                                includeRequestsReceivedPerCommunity: true).SuppressToResultAsync(ReportCategory.COMMUNITIES);

            if (ct.IsCancellationRequested)
                return;

            if (!result.Success)
            {
                Notifications.NotificationsBusController.Instance.AddNotification(new ServerErrorNotification(MY_COMMUNITIES_LOADING_ERROR_MESSAGE));
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
            isGridResultsLoadingItems = true;

            if (pageNumber == 1)
            {
                view.ClearResultsItems();
                view.SetResultsAsLoading(true);
            }
            else
                view.SetResultsLoadingMoreActive(true);

            if (updateJoinRequests)
                await LoadRequestsAsync(ct);

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
                Notifications.NotificationsBusController.Instance.AddNotification(new ServerErrorNotification(ALL_COMMUNITIES_LOADING_ERROR_MESSAGE));
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
            view.InvitesAndRequestsView.SetInvitesAndRequestsAsEmpty(false);
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
            if (updateInvitesGrid)
                view.InvitesAndRequestsView.ClearInvitesItems();

            currentInvitations.Clear();

            var invitesResult = await dataProvider.GetUserInviteRequestAsync(
                InviteRequestAction.invite,
                ct).SuppressToResultAsync(ReportCategory.COMMUNITIES);

            if (ct.IsCancellationRequested)
                return 0;

            if (!invitesResult.Success)
            {
                Notifications.NotificationsBusController.Instance.AddNotification(new ServerErrorNotification(INVITATIONS_COMMUNITIES_LOADING_ERROR_MESSAGE));
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
                Notifications.NotificationsBusController.Instance.AddNotification(new ServerErrorNotification(REQUESTS_COMMUNITIES_LOADING_ERROR_MESSAGE));
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
                Notifications.NotificationsBusController.Instance.AddNotification(new ServerErrorNotification(JOIN_COMMUNITY_ERROR_MESSAGE));
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

            if (!result.Success)
            {
                Notifications.NotificationsBusController.Instance.AddNotification(new ServerErrorNotification(REQUEST_TO_JOIN_COMMUNITY_ERROR_MESSAGE));
            }
        }

        private void CancelRequestToJoinCommunity(string communityId, string requestId)
        {
            cancelRequestToJoinCommunityCts = requestToJoinCommunityCts.SafeRestart();
            CancelRequestToJoinCommunityAsync(communityId, requestId, cancelRequestToJoinCommunityCts.Token).Forget();
        }

        private async UniTaskVoid CancelRequestToJoinCommunityAsync(string communityId, string requestId, CancellationToken ct)
        {
            var result = await dataProvider.ManageInviteRequestToJoinAsync(communityId, requestId, InviteRequestIntention.cancelled, ct)
                                           .SuppressToResultAsync(ReportCategory.COMMUNITIES);

            if (ct.IsCancellationRequested)
                return;

            if (!result.Success || !result.Value)
            {
                Notifications.NotificationsBusController.Instance.AddNotification(new ServerErrorNotification(CANCEL_REQUEST_TO_JOIN_COMMUNITY_ERROR_MESSAGE));
            }

            int? indexToRemove = null;
            for (int i = 0; i < currentJoinRequests.Count; i++)
            {
                GetUserInviteRequestData.UserInviteRequestData joinRequest = currentJoinRequests[i];
                if (joinRequest.communityId == communityId && joinRequest.id == requestId)
                {
                    indexToRemove = i;
                    break;
                }
            }

            if (indexToRemove != null)
                currentJoinRequests.RemoveAt(indexToRemove.Value);
        }

        private void AcceptCommunityInvitation(string communityId, string invitationId)
        {
            acceptCommunityInvitationCts = acceptCommunityInvitationCts.SafeRestart();
            AcceptCommunityInvitationAsync(communityId, invitationId, acceptCommunityInvitationCts.Token).Forget();
        }

        private async UniTaskVoid AcceptCommunityInvitationAsync(string communityId, string invitationId, CancellationToken ct)
        {
            var result = await dataProvider.ManageInviteRequestToJoinAsync(communityId, invitationId, InviteRequestIntention.accepted, ct)
                                           .SuppressToResultAsync(ReportCategory.COMMUNITIES);

            if (ct.IsCancellationRequested)
                return;

            if (!result.Success || !result.Value)
            {
                Notifications.NotificationsBusController.Instance.AddNotification(new ServerErrorNotification(ACCEPT_COMMUNITY_INVITATION_ERROR_MESSAGE));
            }
        }

        private void RejectCommunityInvitation(string communityId, string invitationId)
        {
            rejectCommunityInvitationCts = rejectCommunityInvitationCts.SafeRestart();
            RejectCommunityInvitationAsync(communityId, invitationId, rejectCommunityInvitationCts.Token).Forget();
        }

        private async UniTaskVoid RejectCommunityInvitationAsync(string communityId, string invitationId, CancellationToken ct)
        {
            var result = await dataProvider.ManageInviteRequestToJoinAsync(communityId, invitationId, InviteRequestIntention.rejected, ct)
                                           .SuppressToResultAsync(ReportCategory.COMMUNITIES);

            if (ct.IsCancellationRequested)
                return;

            if (!result.Success || !result.Value)
            {
                Notifications.NotificationsBusController.Instance.AddNotification(new ServerErrorNotification(REJECT_COMMUNITY_INVITATION_ERROR_MESSAGE));
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
            bool canCreate = false;
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

        private void OnCommunityJoined(string communityId, bool success)
        {
            foreach (GetUserInviteRequestData.UserInviteRequestData invitation in currentInvitations)
            {
                if (communityId == invitation.communityId)
                {
                    RefreshInvitesCounter(setCounterToZeroAtTheBeginning: false);
                    break;
                }
            }

            view.UpdateJoinedCommunity(communityId, true, success);
        }

        private void OnCommunityRequestedToJoin(string communityId, string requestId, bool success)
        {
            bool alreadyExistsInvitation = false;
            foreach (GetUserInviteRequestData.UserInviteRequestData invitation in currentInvitations)
            {
                if (communityId == invitation.communityId)
                {
                    alreadyExistsInvitation = true;
                    RefreshInvitesCounter(setCounterToZeroAtTheBeginning: false);
                    break;
                }
            }

            if (!isInvitesAndRequestsSectionActive)
                view.UpdateRequestedToJoinCommunity(communityId, requestId, true, success, alreadyExistsInvitation);
            else
                LoadInvitesAndRequestsResults();

            if (success)
                currentJoinRequests.Add(new GetUserInviteRequestData.UserInviteRequestData { communityId = communityId, id = requestId });
        }

        private void OnCommunityInviteRequestCancelled(string communityId, bool success)
        {
            if (!isInvitesAndRequestsSectionActive)
                view.UpdateRequestedToJoinCommunity(communityId, null, false, success, false);
            else
                view.InvitesAndRequestsView.UpdateJoinRequestCancelled(communityId, success);

            if (success && !RemoveCurrentCommunityInviteRequest(communityId))
                LoadMyCommunities();
        }

        private void OnCommunityInviteRequestAccepted(string communityId, bool success)
        {
            if (isInvitesAndRequestsSectionActive)
                view.InvitesAndRequestsView.UpdateCommunityInvitation(communityId, success);

            if (success)
            {
                view.UpdateJoinedCommunity(communityId, true, success);
                RefreshInvitesCounter(setCounterToZeroAtTheBeginning: false);

                if (!RemoveCurrentCommunityInviteRequest(communityId))
                    LoadMyCommunities();
            }
        }

        private void OnCommunityInviteRequestRejected(string communityId, bool success)
        {
            if (isInvitesAndRequestsSectionActive)
                view.InvitesAndRequestsView.UpdateCommunityInvitation(communityId, success);

            if (success)
            {
                RefreshInvitesCounter(setCounterToZeroAtTheBeginning: false);
                RemoveCurrentCommunityInviteRequest(communityId);
            }
        }

        private bool RemoveCurrentCommunityInviteRequest(string communityId)
        {
            bool foundInvitation = false;
            foreach (var invitation in currentInvitations)
            {
                if (invitation.communityId == communityId)
                {
                    currentInvitations.Remove(invitation);
                    foundInvitation = true;
                    break;
                }
            }

            bool foundJoinRequest = false;
            foreach (var joinRequest in currentJoinRequests)
            {
                if (joinRequest.communityId == communityId)
                {
                    currentJoinRequests.Remove(joinRequest);
                    foundJoinRequest = true;
                    break;
                }
            }

            return foundInvitation || foundJoinRequest;
        }

        private void OnCommunityLeft(string communityId, bool success) =>
            view.UpdateJoinedCommunity(communityId, false, success);

        private void OnCommunityCreated(CreateOrUpdateCommunityResponse.CommunityData newCommunity) =>
            ReloadBrowser();

        private void OnCommunityDeleted(string communityId) =>
            RefreshAfterDeletion();

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
            dataProvider.CommunityInviteRequestCancelled += OnCommunityInviteRequestCancelled;
            dataProvider.CommunityInviteRequestAccepted += OnCommunityInviteRequestAccepted;
            dataProvider.CommunityInviteRequestRejected += OnCommunityInviteRequestRejected;
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
            dataProvider.CommunityInviteRequestCancelled -= OnCommunityInviteRequestCancelled;
            dataProvider.CommunityInviteRequestAccepted -= OnCommunityInviteRequestAccepted;
            dataProvider.CommunityInviteRequestRejected -= OnCommunityInviteRequestRejected;
            dataProvider.CommunityLeft -= OnCommunityLeft;
            dataProvider.CommunityUserRemoved -= OnUserRemovedFromCommunity;
            dataProvider.CommunityUserBanned -= OnUserBannedFromCommunity;
        }

        private void OnJoinRequestReceived(INotification notification)
        {
            if (!isSectionActivated)
                return;

            LoadMyCommunities();
        }

        private void OnInvitationReceived(INotification notification)
        {
            if (!isSectionActivated)
                return;

            if (isInvitesAndRequestsSectionActive)
                LoadInvitesAndRequestsResults();

            RefreshInvitesCounter();
        }

        private void OnJoinRequestAccepted(INotification notification)
        {
            if (!isSectionActivated)
                return;

            LoadMyCommunities();

            if (!isInvitesAndRequestsSectionActive)
                LoadAllCommunitiesResults(updateInvitations: true);
            else
                LoadInvitesAndRequestsResults();
        }

        private void OnCommunityDeleted(INotification notification) =>
            RefreshAfterDeletion();

        private void RefreshAfterDeletion()
        {
            if (!isSectionActivated)
                return;

            LoadMyCommunities();

            if (!isInvitesAndRequestsSectionActive)
                LoadAllCommunitiesResults(updateInvitations: true);
        }
    }
}
