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
using Utility;
using DCL.UI.SharedSpaceManager;
using DCL.VoiceChat;
using DCL.Web3;
using DCL.WebRequests;
using MVC;
using System;
using System.Threading;
using UnityEngine;
using Utility.Types;
using Notifications = DCL.NotificationsBusController.NotificationsBus;

namespace DCL.Communities.CommunitiesBrowser
{
    public class CommunitiesBrowserController : ISection, IDisposable
    {
        private const int SEARCH_AWAIT_TIME = 1000;
        private const string JOIN_COMMUNITY_ERROR_MESSAGE = "There was an error joining community. Please try again.";

        private const string INVITATIONS_COMMUNITIES_LOADING_ERROR_MESSAGE = "There was an error loading invites. Please try again.";
        private const string REQUESTS_COMMUNITIES_LOADING_ERROR_MESSAGE = "There was an error loading requests. Please try again.";
        private const string ACCEPT_COMMUNITY_INVITATION_ERROR_MESSAGE = "There was an error accepting community invitation. Please try again.";
        private const string REJECT_COMMUNITY_INVITATION_ERROR_MESSAGE = "There was an error rejecting community invitation. Please try again.";
        private const string REQUEST_TO_JOIN_COMMUNITY_ERROR_MESSAGE = "There was an error requesting to join community. Please try again.";
        private const string CANCEL_REQUEST_TO_JOIN_COMMUNITY_ERROR_MESSAGE = "There was an error cancelling join request. Please try again.";

        private const string INVITES_RESULTS_TITLE = "Invites";
        private const string REQUESTS_RESULTS_TITLE = "Requests";

        private readonly CommunitiesBrowserView view;
        private readonly RectTransform rectTransform;
        private readonly ICursor cursor;
        private readonly CommunitiesDataProvider.CommunitiesDataProvider dataProvider;
        private readonly IInputBlock inputBlock;
        private readonly IMVCManager mvcManager;
        private readonly ISelfProfile selfProfile;
        private readonly INftNamesProvider nftNamesProvider;
        private readonly ISpriteCache spriteCache;
        private readonly CommunitiesBrowserEventBus browserEventBus;
        private readonly EventSubscriptionScope scope = new();

        private readonly CommunitiesBrowserMyCommunitiesPresenter myCommunitiesPresenter;
        private readonly CommunitiesBrowserStateService browserStateService;
        private readonly CommunitiesBrowserMainRightSectionPresenter mainRightSectionPresenter;

        private CancellationTokenSource? searchCancellationCts;
        private CancellationTokenSource? openCommunityCreationCts;
        private CancellationTokenSource? updateInvitesCounterCts;
        private CancellationTokenSource? joinCommunityCts;
        private CancellationTokenSource? requestToJoinCommunityCts;
        private CancellationTokenSource? cancelRequestToJoinCommunityCts;
        private CancellationTokenSource? acceptCommunityInvitationCts;
        private CancellationTokenSource? rejectCommunityInvitationCts;
        private CancellationTokenSource? loadResultsCts;

        private bool isSectionActivated;
        private string currentSearchText = string.Empty;
        private bool isInvitesAndRequestsSectionActive => currentSection == CommunitiesRightSideSections.INVITES_AND_REQUESTS_SECTION;
        private CommunitiesRightSideSections currentSection = CommunitiesRightSideSections.MAIN_SECTION;

        public CommunitiesBrowserController(
            CommunitiesBrowserView view,
            ICursor cursor,
            CommunitiesDataProvider.CommunitiesDataProvider dataProvider,
            IWebRequestController webRequestController,
            IInputBlock inputBlock,
            IMVCManager mvcManager,
            ProfileRepositoryWrapper profileRepositoryWrapper,
            ISelfProfile selfProfile,
            INftNamesProvider nftNamesProvider,
            ICommunityCallOrchestrator orchestrator,
            ISharedSpaceManager sharedSpaceManager)
        {
            this.view = view;
            rectTransform = view.transform.parent.GetComponent<RectTransform>();
            this.cursor = cursor;
            this.dataProvider = dataProvider;
            this.inputBlock = inputBlock;
            this.mvcManager = mvcManager;
            this.selfProfile = selfProfile;
            this.nftNamesProvider = nftNamesProvider;

            spriteCache = new SpriteCache(webRequestController);
            browserEventBus = new CommunitiesBrowserEventBus();
            browserStateService = new CommunitiesBrowserStateService(browserEventBus);
            var thumbnailLoader = new ThumbnailLoader(spriteCache);

            myCommunitiesPresenter = new CommunitiesBrowserMyCommunitiesPresenter(view.MyCommunitiesView, dataProvider, browserStateService, thumbnailLoader, browserEventBus);
            myCommunitiesPresenter.ViewAllMyCommunitiesButtonClicked += ViewAllMyCommunitiesResults;

            mainRightSectionPresenter = new CommunitiesBrowserMainRightSectionPresenter(view.RightSectionView, dataProvider, sharedSpaceManager, browserStateService, thumbnailLoader, profileRepositoryWrapper, orchestrator, browserEventBus);

            view.SetThumbnailLoader(thumbnailLoader);
            view.InvitesAndRequestsView.Initialize(profileRepositoryWrapper);
            view.InvitesAndRequestsView.InvitesAndRequestsButtonClicked += LoadInvitesAndRequestsResults;
            view.InvitesAndRequestsView.BackButtonClicked += OnBackButtonClicked;

            scope.Add(browserEventBus.Subscribe<CommunitiesBrowserEvents.ClearSearchBarEvent>(OnClearSearchBar));
            scope.Add(browserEventBus.Subscribe<CommunitiesBrowserEvents.CommunityProfileOpenedEvent>(OpenCommunityProfile));
            scope.Add(browserEventBus.Subscribe<CommunitiesBrowserEvents.CommunityJoinedClickedEvent>(JoinCommunity));
            scope.Add(browserEventBus.Subscribe<CommunitiesBrowserEvents.RequestedToJoinCommunityEvent>(RequestToJoinCommunity));
            scope.Add(browserEventBus.Subscribe<CommunitiesBrowserEvents.RequestToJoinCommunityCancelledEvent>(CancelRequestToJoinCommunity));

            view.SearchBarSelected += DisableShortcutsInput;
            view.SearchBarDeselected += RestoreInput;
            view.SearchBarValueChanged += SearchBarValueChanged;
            view.SearchBarSubmit += SearchBarSubmit;
            view.SearchBarClearButtonClicked += SearchBarCleared;
            view.CommunityProfileOpened += OnOpenCommunityProfile;
            view.CommunityRequestToJoinCanceled += OnCancelRequestToJoinCommunity;
            view.CommunityInvitationAccepted += AcceptCommunityInvitation;
            view.CommunityInvitationRejected += RejectCommunityInvitation;
            view.CreateCommunityButtonClicked += CreateCommunity;

            Notifications.NotificationsBusController.Instance.SubscribeToNotificationTypeReceived(NotificationType.COMMUNITY_REQUEST_TO_JOIN_RECEIVED, OnJoinRequestReceived);
            Notifications.NotificationsBusController.Instance.SubscribeToNotificationTypeReceived(NotificationType.COMMUNITY_INVITE_RECEIVED, OnInvitationReceived);
            Notifications.NotificationsBusController.Instance.SubscribeToNotificationTypeReceived(NotificationType.COMMUNITY_REQUEST_TO_JOIN_ACCEPTED, OnJoinRequestAccepted);
            Notifications.NotificationsBusController.Instance.SubscribeToNotificationTypeReceived(NotificationType.COMMUNITY_DELETED_CONTENT_VIOLATION, OnCommunityDeleted);
            Notifications.NotificationsBusController.Instance.SubscribeToNotificationTypeReceived(NotificationType.COMMUNITY_DELETED, OnCommunityDeleted);
        }

        private void OnBackButtonClicked()
        {
            LoadJoinRequestsAndAllCommunities();
        }

        public void Dispose()
        {
            view.SearchBarSelected -= DisableShortcutsInput;
            view.SearchBarDeselected -= RestoreInput;
            view.SearchBarValueChanged -= SearchBarValueChanged;
            view.SearchBarSubmit -= SearchBarSubmit;
            view.SearchBarClearButtonClicked -= SearchBarCleared;
            view.CommunityProfileOpened -= OnOpenCommunityProfile;
            view.CreateCommunityButtonClicked -= CreateCommunity;

            view.CommunityInvitationAccepted -= AcceptCommunityInvitation;
            view.CommunityInvitationRejected -= RejectCommunityInvitation;

            myCommunitiesPresenter.ViewAllMyCommunitiesButtonClicked -= ViewAllMyCommunitiesResults;

            UnsubscribeDataProviderEvents();

            browserStateService.Dispose();
            scope.Dispose();

            myCommunitiesPresenter.Dispose();
            searchCancellationCts?.SafeCancelAndDispose();
            openCommunityCreationCts?.SafeCancelAndDispose();
            spriteCache.Clear();

            updateInvitesCounterCts?.SafeCancelAndDispose();
            joinCommunityCts?.SafeCancelAndDispose();
            requestToJoinCommunityCts?.SafeCancelAndDispose();
            cancelRequestToJoinCommunityCts?.SafeCancelAndDispose();
            acceptCommunityInvitationCts?.SafeCancelAndDispose();
            rejectCommunityInvitationCts?.SafeCancelAndDispose();

            view.InvitesAndRequestsView.InvitesAndRequestsButtonClicked -= LoadInvitesAndRequestsResults;
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
            searchCancellationCts?.SafeCancelAndDispose();
            openCommunityCreationCts?.SafeCancelAndDispose();
            updateInvitesCounterCts?.SafeCancelAndDispose();
            joinCommunityCts?.SafeCancelAndDispose();
            requestToJoinCommunityCts?.SafeCancelAndDispose();
            cancelRequestToJoinCommunityCts?.SafeCancelAndDispose();
            acceptCommunityInvitationCts?.SafeCancelAndDispose();
            rejectCommunityInvitationCts?.SafeCancelAndDispose();
            spriteCache.Clear();
            myCommunitiesPresenter.Deactivate();
            mainRightSectionPresenter.Deactivate();

            UnsubscribeDataProviderEvents();
        }

        public void Animate(int triggerId) =>
            view.PlayAnimator(triggerId);

        public void ResetAnimator() =>
            view.ResetAnimator();

        public RectTransform GetRectTransform() =>
            rectTransform;

        private void ViewAllMyCommunitiesResults()
        {
            ClearSearchBar();
            SetActiveSection(CommunitiesRightSideSections.MAIN_SECTION);
            mainRightSectionPresenter.ViewAllMyCommunitiesResults();
        }

        private void ReloadBrowser()
        {
            LoadMyCommunities();
            LoadJoinRequestsAndAllCommunities();
            RefreshInvitesCounter();
        }


        private void LoadMyCommunities()
        {
            myCommunitiesPresenter.LoadMyCommunities();
        }

        private void LoadJoinRequestsAndAllCommunities()
        {
            SetActiveSection(CommunitiesRightSideSections.MAIN_SECTION);
            loadResultsCts = loadResultsCts.SafeRestart();

            mainRightSectionPresenter.SetAsLoading();
            LoadJoinRequestsAndAllCommunitiesAsync(loadResultsCts.Token).Forget();
            return;

            async UniTaskVoid LoadJoinRequestsAndAllCommunitiesAsync(CancellationToken ct)
            {
                await LoadJoinRequestsAsync(ct);
                mainRightSectionPresenter.LoadAllCommunitiesAsync().Forget();
            }
        }

        private void LoadInvitesAndRequestsResults()
        {
            ClearSearchBar();
            SetActiveSection(CommunitiesRightSideSections.INVITES_AND_REQUESTS_SECTION);
            loadResultsCts = loadResultsCts.SafeRestart();
            LoadInvitesAndRequestsAsync(loadResultsCts.Token).Forget();
            return;

            async UniTaskVoid LoadInvitesAndRequestsAsync(CancellationToken ct)
            {
                view.InvitesAndRequestsView.SetAsLoading(true);
                view.InvitesAndRequestsView.SetInvitesAndRequestsAsEmpty(false);
                int invitesCount = await LoadInvitesAsync(updateInvitesGrid: true, ct);
                view.InvitesAndRequestsView.SetInvitesTitle($"{INVITES_RESULTS_TITLE} ({invitesCount})");
                if (ct.IsCancellationRequested) return;
                int requestsCount = await LoadJoinRequestsAsync(ct);
                view.InvitesAndRequestsView.SetRequestsTitle($"{REQUESTS_RESULTS_TITLE} ({requestsCount})");
                view.InvitesAndRequestsView.SetAsLoading(false);
                view.InvitesAndRequestsView.SetInvitesAndRequestsAsEmpty(invitesCount == 0 && requestsCount == 0);
            }
        }


        private async UniTask<int> LoadInvitesAsync(bool updateInvitesGrid, CancellationToken ct)
        {
            if (updateInvitesGrid)
                view.InvitesAndRequestsView.ClearInvitesItems();

            browserStateService.ClearInvitationsRequests();

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
                browserStateService.AddInvitationsRequests(invitesResult.Value.data.results);
            }

            view.InvitesAndRequestsView.SetInvitesCounter(invitesCount);

            return invitesCount;
        }

        private async UniTask<int> LoadJoinRequestsAsync(CancellationToken ct)
        {
            view.InvitesAndRequestsView.ClearRequestsItems();
            browserStateService.ClearJoinRequests();

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
                browserStateService.AddJoinRequests(requestsResult.Value.data.results);
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

            SetActiveSection(CommunitiesRightSideSections.MAIN_SECTION);

            if (string.IsNullOrEmpty(searchText))
                mainRightSectionPresenter.LoadAllCommunities();
            else
                mainRightSectionPresenter.LoadSearchResults(searchText);

            currentSearchText = searchText;
        }

        private void SetActiveSection(CommunitiesRightSideSections activeSection)
        {
            currentSection = activeSection;
            switch (activeSection)
            {
                case CommunitiesRightSideSections.MAIN_SECTION:
                    view.SetResultsSectionActive(true);
                    view.InvitesAndRequestsView.SetSectionActive(false);
                    break;
                case CommunitiesRightSideSections.INVITES_AND_REQUESTS_SECTION:
                    view.SetResultsSectionActive(false);
                    view.InvitesAndRequestsView.SetSectionActive(true);
                    break;
            }
        }

        private void SearchBarCleared()
        {
            ClearSearchBar();
            mainRightSectionPresenter.LoadAllCommunities();
        }

        private void OnClearSearchBar(CommunitiesBrowserEvents.ClearSearchBarEvent evt)
        {
            ClearSearchBar();
        }

        private void ClearSearchBar()
        {
            currentSearchText = string.Empty;
            view.CleanSearchBar(raiseOnChangeEvent: false);
        }

        private void JoinCommunity(CommunitiesBrowserEvents.CommunityJoinedClickedEvent evt)
        {
            joinCommunityCts = joinCommunityCts.SafeRestart();
            JoinCommunityAsync(evt.CommunityId, joinCommunityCts.Token).Forget();
            return;

            async UniTaskVoid JoinCommunityAsync(string communityId, CancellationToken ct)
            {
                Result<bool> result = await dataProvider.JoinCommunityAsync(communityId, ct).SuppressToResultAsync(ReportCategory.COMMUNITIES);

                if (ct.IsCancellationRequested)
                    return;

                if (!result.Success || !result.Value)
                    Notifications.NotificationsBusController.Instance.AddNotification(new ServerErrorNotification(JOIN_COMMUNITY_ERROR_MESSAGE));
            }
        }

        private void RequestToJoinCommunity(CommunitiesBrowserEvents.RequestedToJoinCommunityEvent evt)
        {
            requestToJoinCommunityCts = requestToJoinCommunityCts.SafeRestart();
            RequestToJoinCommunityAsync(requestToJoinCommunityCts.Token).Forget();
            return;

            async UniTaskVoid RequestToJoinCommunityAsync(CancellationToken ct)
            {
                var ownProfile = await selfProfile.ProfileAsync(ct);
                if (ownProfile == null)
                    return;

                var result = await dataProvider.SendInviteOrRequestToJoinAsync(evt.CommunityId, ownProfile.UserId, InviteRequestAction.request_to_join, ct)
                                               .SuppressToResultAsync(ReportCategory.COMMUNITIES);

                if (ct.IsCancellationRequested)
                    return;

                if (!result.Success)
                    Notifications.NotificationsBusController.Instance.AddNotification(new ServerErrorNotification(REQUEST_TO_JOIN_COMMUNITY_ERROR_MESSAGE));
            }
        }

        private void OnCancelRequestToJoinCommunity(string communityId, string requestId)
        {
            cancelRequestToJoinCommunityCts = requestToJoinCommunityCts.SafeRestart();
            CancelRequestToJoinCommunityAsync(communityId, requestId, cancelRequestToJoinCommunityCts.Token).Forget();
        }


        private void CancelRequestToJoinCommunity(CommunitiesBrowserEvents.RequestToJoinCommunityCancelledEvent evt)
        {
            cancelRequestToJoinCommunityCts = requestToJoinCommunityCts.SafeRestart();
            CancelRequestToJoinCommunityAsync(evt.CommunityId, evt.RequestId, cancelRequestToJoinCommunityCts.Token).Forget();
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
            for ( var i = 0; i < browserStateService.CurrentJoinRequests.Count; i++)
            {
                GetUserInviteRequestData.UserInviteRequestData joinRequest = browserStateService.CurrentJoinRequests[i];
                if (joinRequest.communityId == communityId && joinRequest.id == requestId)
                {
                    indexToRemove = i;
                    break;
                }
            }

            if (indexToRemove != null)
                browserStateService.RemoveJoinRequestAt(indexToRemove.Value);

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

        private void OnOpenCommunityProfile(string communityId) =>
            mvcManager.ShowAsync(CommunityCardController.IssueCommand(new CommunityCardParameter(communityId, spriteCache))).Forget();


        private void OpenCommunityProfile(CommunitiesBrowserEvents.CommunityProfileOpenedEvent evt) =>
            OnOpenCommunityProfile(evt.CommunityId);

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
                               spriteCache)), ct)
                      .Forget();
        }

        private void OnCommunityUpdated(string _) =>
            ReloadBrowser();

        private void OnCommunityJoined(string communityId, bool success)
        {
            foreach (GetUserInviteRequestData.UserInviteRequestData invitation in browserStateService.CurrentInvitationRequests)
            {
                if (communityId == invitation.communityId)
                {
                    RefreshInvitesCounter(setCounterToZeroAtTheBeginning: false);
                    break;
                }
            }

            browserEventBus.RaiseUpdateJoinedCommunityEvent(communityId, true, success);
        }

        private void OnCommunityRequestedToJoin(string communityId, string requestId, bool success)
        {
            var alreadyExistsInvitation = false;
            foreach (GetUserInviteRequestData.UserInviteRequestData invitation in browserStateService.CurrentInvitationRequests)
            {
                if (communityId == invitation.communityId)
                {
                    alreadyExistsInvitation = true;
                    RefreshInvitesCounter(setCounterToZeroAtTheBeginning: false);
                    break;
                }
            }

            if (!isInvitesAndRequestsSectionActive)
            {
                browserStateService.UpdateRequestToJoinCommunity(communityId, requestId, true, success, alreadyExistsInvitation);

                if (success)
                    browserEventBus.RaiseUpdateJoinedCommunityEvent(communityId, true, false);
            }
            else
                LoadInvitesAndRequestsResults();

            if (success)
                browserStateService.AddJoinRequest(new GetUserInviteRequestData.UserInviteRequestData { communityId = communityId, id = requestId });
        }

        private void OnCommunityInviteRequestCancelled(string communityId, bool success)
        {
            if (!isInvitesAndRequestsSectionActive)
            {
                browserStateService.UpdateRequestToJoinCommunity(communityId, null, false, success, false);
            }
            else
                view.InvitesAndRequestsView.UpdateJoinRequestCancelled(communityId, success);

            browserEventBus.RaiseUpdateJoinedCommunityEvent(communityId, success, false);

            if (success && !RemoveCurrentCommunityInviteRequest(communityId))
                LoadMyCommunities();
        }

        private void OnCommunityInviteRequestAccepted(string communityId, bool success)
        {
            if (isInvitesAndRequestsSectionActive)
                view.InvitesAndRequestsView.UpdateCommunityInvitation(communityId, success);

            if (success)
            {
                browserEventBus.RaiseUpdateJoinedCommunityEvent(communityId, true, success);

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
                LoadMyCommunities();
            }
        }

        private bool RemoveCurrentCommunityInviteRequest(string communityId)
        {
            var foundInvitation = false;
            foreach (var invitation in browserStateService.CurrentInvitationRequests)
            {
                if (invitation.communityId == communityId)
                {
                    browserStateService.RemoveInvitation(invitation);
                    foundInvitation = true;
                    break;
                }
            }

            var foundJoinRequest = false;
            foreach (var joinRequest in browserStateService.CurrentJoinRequests)
            {
                if (joinRequest.communityId == communityId)
                {
                    browserStateService.RemoveJoinRequest(joinRequest);

                    foundJoinRequest = true;
                    break;
                }
            }

            return foundInvitation || foundJoinRequest;
        }

        private void OnCommunityLeft(string communityId, bool success)
        {
            browserEventBus.RaiseUpdateJoinedCommunityEvent(communityId, false, success);
        }

        private void OnCommunityCreated(CreateOrUpdateCommunityResponse.CommunityData newCommunity) =>
            ReloadBrowser();

        private void OnCommunityDeleted(string communityId) =>
            RefreshAfterDeletion();

        private void OnUserRemovedFromCommunity(string communityId)
        {
            browserEventBus.RaiseUserRemovedFromCommunity(communityId);
        }

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
                LoadJoinRequestsAndAllCommunities();
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
                LoadJoinRequestsAndAllCommunities();
        }
    }

    public enum CommunitiesRightSideSections
    {
        MAIN_SECTION,
        INVITES_AND_REQUESTS_SECTION,
    }

    public enum CommunitiesViews
    {
        BROWSE_ALL_COMMUNITIES,
        FILTERED_COMMUNITIES,
    }
}
