using Cysharp.Threading.Tasks;
using DCL.Chat.ControllerShowParams;
using DCL.Chat.EventBus;
using DCL.Communities.CommunitiesCard;
using DCL.Communities.CommunitiesDataProvider.DTOs;
using DCL.Communities.CommunitiesBrowser.Commands;
using DCL.Diagnostics;
using DCL.Friends.UI.BlockUserPrompt;
using DCL.Input;
using DCL.Input.Component;
using DCL.NotificationsBus;
using DCL.NotificationsBus.NotificationTypes;
using DCL.Passport;
using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.UI;
using DCL.UI.Profiles.Helpers;
using DCL.Utilities.Extensions;
using Utility;
using DCL.UI.SharedSpaceManager;
using DCL.Utility.Types;
using DCL.VoiceChat;
using DCL.Web3;
using DCL.WebRequests;
using MVC;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace DCL.Communities.CommunitiesBrowser
{
    public class CommunitiesBrowserController : ISection, IDisposable
    {
        private const int SEARCH_AWAIT_TIME = 1000;

        private const string INVITATIONS_COMMUNITIES_LOADING_ERROR_MESSAGE = "There was an error loading invites. Please try again.";
        private const string REQUESTS_RECEIVED_COMMUNITIES_LOADING_ERROR_MESSAGE = "There was an error loading requests received. Please try again.";
        private const string MANAGE_REQUEST_RECEIVED_ERROR_TEXT = "There was an error managing the user request. Please try again.";
        private const string REQUESTS_COMMUNITIES_LOADING_ERROR_MESSAGE = "There was an error loading requests. Please try again.";
        private const string ACCEPT_COMMUNITY_INVITATION_ERROR_MESSAGE = "There was an error accepting community invitation. Please try again.";
        private const string REJECT_COMMUNITY_INVITATION_ERROR_MESSAGE = "There was an error rejecting community invitation. Please try again.";
        private const string REQUEST_TO_JOIN_COMMUNITY_ERROR_MESSAGE = "There was an error requesting to join community. Please try again.";
        private const string CANCEL_REQUEST_TO_JOIN_COMMUNITY_ERROR_MESSAGE = "There was an error cancelling join request. Please try again.";

        private readonly CommunitiesBrowserView view;
        private readonly RectTransform rectTransform;
        private readonly ICursor cursor;
        private readonly CommunitiesDataProvider.CommunitiesDataProvider dataProvider;
        private readonly IInputBlock inputBlock;
        private readonly IMVCManager mvcManager;
        private readonly ISelfProfile selfProfile;
        private readonly ISpriteCache spriteCache;
        private readonly CommunitiesBrowserEventBus browserEventBus;
        private readonly EventSubscriptionScope scope = new ();
        private readonly CommunitiesBrowserCommandsLibrary commandsLibrary;
        private readonly ISharedSpaceManager sharedSpaceManager;
        private readonly IChatEventBus chatEventBus;
        private readonly ICommunityCallOrchestrator orchestrator;

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
        private CancellationTokenSource? manageRequestReceivedCts;

        private bool isSectionActivated;
        private string currentSearchText = string.Empty;
        private CommunitiesRightSideSections currentSection = CommunitiesRightSideSections.MAIN_SECTION;
        private bool isInvitesAndRequestsSectionActive => currentSection == CommunitiesRightSideSections.INVITES_AND_REQUESTS_SECTION;

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
            ISharedSpaceManager sharedSpaceManager,
            IChatEventBus chatEventBus)
        {
            this.view = view;
            rectTransform = view.transform.parent.GetComponent<RectTransform>();
            this.cursor = cursor;
            this.dataProvider = dataProvider;
            this.inputBlock = inputBlock;
            this.mvcManager = mvcManager;
            this.selfProfile = selfProfile;
            this.sharedSpaceManager = sharedSpaceManager;
            this.chatEventBus = chatEventBus;
            this.orchestrator = orchestrator;

            spriteCache = new SpriteCache(webRequestController);
            browserEventBus = new CommunitiesBrowserEventBus();
            browserStateService = new CommunitiesBrowserStateService(browserEventBus, orchestrator);
            var thumbnailLoader = new ThumbnailLoader(spriteCache);
            commandsLibrary = new CommunitiesBrowserCommandsLibrary(orchestrator, sharedSpaceManager, chatEventBus, selfProfile, nftNamesProvider, mvcManager, spriteCache, dataProvider);

            myCommunitiesPresenter = new CommunitiesBrowserMyCommunitiesPresenter(view.MyCommunitiesView, dataProvider, browserStateService, thumbnailLoader, browserEventBus, orchestrator);
            myCommunitiesPresenter.ViewAllMyCommunitiesButtonClicked += ViewAllMyCommunitiesResults;

            mainRightSectionPresenter = new CommunitiesBrowserMainRightSectionPresenter(view.RightSectionView, dataProvider, browserStateService, thumbnailLoader, profileRepositoryWrapper, browserEventBus, commandsLibrary, orchestrator);

            view.SetThumbnailLoader(thumbnailLoader);
            view.InvitesAndRequestsView.Initialize(profileRepositoryWrapper, dataProvider, browserStateService);
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
            view.OpenProfilePassportRequested += OpenProfilePassport;
            view.OpenUserChatRequested += OpenUserChatAsync;
            view.CallUserRequested += CallUserAsync;
            view.BlockUserRequested += BlockUserAsync;
            view.ManageRequestReceivedRequested += ManageRequestReceived;

            NotificationsBusController.Instance.SubscribeToNotificationTypeReceived(NotificationType.COMMUNITY_REQUEST_TO_JOIN_RECEIVED, OnJoinRequestReceived);
            NotificationsBusController.Instance.SubscribeToNotificationTypeReceived(NotificationType.COMMUNITY_INVITE_RECEIVED, OnInvitationReceived);
            NotificationsBusController.Instance.SubscribeToNotificationTypeReceived(NotificationType.COMMUNITY_REQUEST_TO_JOIN_ACCEPTED, OnJoinRequestAccepted);
            NotificationsBusController.Instance.SubscribeToNotificationTypeReceived(NotificationType.COMMUNITY_DELETED_CONTENT_VIOLATION, OnCommunityDeleted);
            NotificationsBusController.Instance.SubscribeToNotificationTypeReceived(NotificationType.COMMUNITY_DELETED, OnCommunityDeleted);
            NotificationsBusController.Instance.SubscribeToNotificationTypeReceived(NotificationType.COMMUNITY_OWNERSHIP_TRANSFERRED, OnCommunityTransferredToMe);
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
            view.OpenProfilePassportRequested -= OpenProfilePassport;
            view.OpenUserChatRequested -= OpenUserChatAsync;
            view.CallUserRequested -= CallUserAsync;
            view.BlockUserRequested -= BlockUserAsync;
            view.ManageRequestReceivedRequested -= ManageRequestReceived;

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
            manageRequestReceivedCts?.SafeCancelAndDispose();

            view.InvitesAndRequestsView.InvitesAndRequestsButtonClicked -= LoadInvitesAndRequestsResults;
        }

        private void OnBackButtonClicked()
        {
            LoadJoinRequestsAndAllCommunities();
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
            RefreshInvitesAndRequestsCounter();
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
                view.InvitesAndRequestsView.SetInvitesGridCounter(invitesCount);
                if (ct.IsCancellationRequested) return;
                int receivedRequestsCount = await LoadRequestsReceivedAsync(updateRequestsReceivedGrid: true, ct);
                view.InvitesAndRequestsView.SetRequestsReceivedGridCounter(receivedRequestsCount);
                view.InvitesAndRequestsView.SetInvitesAndRequestsCounter(invitesCount + receivedRequestsCount);
                if (ct.IsCancellationRequested) return;
                int requestsCount = await LoadJoinRequestsAsync(ct);
                view.InvitesAndRequestsView.SetRequestsGridCounter(requestsCount);
                view.InvitesAndRequestsView.SetAsLoading(false);
                view.InvitesAndRequestsView.SetInvitesAndRequestsAsEmpty(invitesCount == 0 && receivedRequestsCount == 0 && requestsCount == 0);
            }
        }

        private async UniTask<int> LoadInvitesAsync(bool updateInvitesGrid, CancellationToken ct)
        {
            if (updateInvitesGrid)
                view.InvitesAndRequestsView.ClearInvitesItems();

            browserStateService.ClearInvitationsRequests();

            Result<GetUserInviteRequestResponse> invitesResult = await dataProvider.GetUserInviteRequestAsync(
                                                                                        InviteRequestAction.invite,
                                                                                        ct)
                                                                                   .SuppressToResultAsync(ReportCategory.COMMUNITIES);

            if (ct.IsCancellationRequested)
                return 0;

            if (!invitesResult.Success)
            {
                NotificationsBusController.Instance.AddNotification(new ServerErrorNotification(INVITATIONS_COMMUNITIES_LOADING_ERROR_MESSAGE));
                return 0;
            }

            int invitesCount = invitesResult.Value.data.results.Length;

            if (updateInvitesGrid && invitesCount > 0)
            {
                view.InvitesAndRequestsView.SetInvitesItems(invitesResult.Value.data.results);
                browserStateService.AddInvitationsRequests(invitesResult.Value.data.results);
            }

            return invitesCount;
        }

        private async UniTask<int> LoadRequestsReceivedAsync(bool updateRequestsReceivedGrid, CancellationToken ct)
        {
            Result<GetUserCommunitiesResponse> myCommunitiesResult = await dataProvider.GetUserCommunitiesAsync(
                                                                                            name: string.Empty,
                                                                                            onlyMemberOf: true,
                                                                                            pageNumber: 1,
                                                                                            elementsPerPage: 1000,
                                                                                            ct: ct,
                                                                                            includeRequestsReceivedPerCommunity: true)
                                                                                       .SuppressToResultAsync(ReportCategory.COMMUNITIES);

            if (ct.IsCancellationRequested)
                return 0;

            if (updateRequestsReceivedGrid)
                view.InvitesAndRequestsView.ClearRequestsReceivedItems();

            if (!myCommunitiesResult.Success)
            {
                NotificationsBusController.Instance.AddNotification(new ServerErrorNotification(REQUESTS_RECEIVED_COMMUNITIES_LOADING_ERROR_MESSAGE));
                return 0;
            }

            List<KeyValuePair<GetUserCommunitiesData.CommunityData, ICommunityMemberData[]>> requestsReceivedCommunities = new ();
            var totalAmountOfRequests = 0;
            if (myCommunitiesResult.Value.data.results.Length > 0)
            {
                foreach (GetUserCommunitiesData.CommunityData myCommunityData in myCommunitiesResult.Value.data.results)
                {
                    if (myCommunityData.requestsReceived > 0)
                    {
                        Result<ICommunityMemberPagedResponse> communityRequestsReceived = await dataProvider.GetCommunityInviteRequestAsync(
                                                                                                                 communityId: myCommunityData.id,
                                                                                                                 action: InviteRequestAction.request_to_join,
                                                                                                                 pageNumber: 1,
                                                                                                                 elementsPerPage: 1000,
                                                                                                                 ct: ct)
                                                                                                            .SuppressToResultAsync(ReportCategory.COMMUNITIES);

                        if (ct.IsCancellationRequested)
                            break;

                        requestsReceivedCommunities.Add(
                            new KeyValuePair<GetUserCommunitiesData.CommunityData, ICommunityMemberData[]>(
                                myCommunityData,
                                communityRequestsReceived.Success ? communityRequestsReceived.Value.members : Array.Empty<ICommunityMemberData>()));

                        totalAmountOfRequests += communityRequestsReceived.Value.total;
                    }
                }

                if (updateRequestsReceivedGrid)
                    view.InvitesAndRequestsView.SetRequestsReceivedItems(requestsReceivedCommunities);
            }

            return totalAmountOfRequests;
        }

        private async UniTask<int> LoadJoinRequestsAsync(CancellationToken ct)
        {
            view.InvitesAndRequestsView.ClearRequestsItems();
            browserStateService.ClearJoinRequests();

            Result<GetUserInviteRequestResponse> requestsResult = await dataProvider.GetUserInviteRequestAsync(
                                                                                         InviteRequestAction.request_to_join,
                                                                                         ct)
                                                                                    .SuppressToResultAsync(ReportCategory.COMMUNITIES);

            if (ct.IsCancellationRequested)
                return 0;

            if (!requestsResult.Success)
            {
                NotificationsBusController.Instance.AddNotification(new ServerErrorNotification(REQUESTS_COMMUNITIES_LOADING_ERROR_MESSAGE));
                return 0;
            }

            if (requestsResult.Value.data.results.Length > 0)
            {
                view.InvitesAndRequestsView.SetRequestsItems(requestsResult.Value.data.results);
                browserStateService.AddJoinRequests(requestsResult.Value.data.results);
            }

            return requestsResult.Value.data.results.Length;
        }

        private void RefreshInvitesAndRequestsCounter(bool setCounterToZeroAtTheBeginning = true)
        {
            if (setCounterToZeroAtTheBeginning)
                view.InvitesAndRequestsView.SetInvitesAndRequestsCounter(0);

            // Get the current invites & received requests to update the invite counter on the main page
            updateInvitesCounterCts = updateInvitesCounterCts.SafeRestart();
            RefreshInvitesCounterAsync(updateInvitesCounterCts.Token).Forget();
            return;

            async UniTaskVoid RefreshInvitesCounterAsync(CancellationToken ct)
            {
                int invitesCount = await LoadInvitesAsync(updateInvitesGrid: false, updateInvitesCounterCts.Token);
                int receivedRequestsCount = await LoadRequestsReceivedAsync(updateRequestsReceivedGrid: false, updateInvitesCounterCts.Token);
                view.InvitesAndRequestsView.SetInvitesAndRequestsCounter(invitesCount + receivedRequestsCount);
            }
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
                    manageRequestReceivedCts?.SafeCancelAndDispose();
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
            commandsLibrary.JoinCommunityCommand.Execute(evt.CommunityId, joinCommunityCts.Token);
        }

        private void RequestToJoinCommunity(CommunitiesBrowserEvents.RequestedToJoinCommunityEvent evt)
        {
            requestToJoinCommunityCts = requestToJoinCommunityCts.SafeRestart();
            RequestToJoinCommunityAsync(requestToJoinCommunityCts.Token).Forget();
            return;

            async UniTaskVoid RequestToJoinCommunityAsync(CancellationToken ct)
            {
                Profile? ownProfile = await selfProfile.ProfileAsync(ct);

                if (ownProfile == null)
                    return;

                Result<string> result = await dataProvider.SendInviteOrRequestToJoinAsync(evt.CommunityId, ownProfile.UserId, InviteRequestAction.request_to_join, ct)
                                                          .SuppressToResultAsync(ReportCategory.COMMUNITIES);

                if (ct.IsCancellationRequested)
                    return;

                if (!result.Success)
                    NotificationsBusController.Instance.AddNotification(new ServerErrorNotification(REQUEST_TO_JOIN_COMMUNITY_ERROR_MESSAGE));
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
            Result<bool> result = await dataProvider.ManageInviteRequestToJoinAsync(communityId, requestId, InviteRequestIntention.cancelled, ct)
                                                    .SuppressToResultAsync(ReportCategory.COMMUNITIES);

            if (ct.IsCancellationRequested)
                return;

            if (!result.Success || !result.Value) { NotificationsBusController.Instance.AddNotification(new ServerErrorNotification(CANCEL_REQUEST_TO_JOIN_COMMUNITY_ERROR_MESSAGE)); }

            int? indexToRemove = null;

            for (var i = 0; i < browserStateService.CurrentJoinRequests.Count; i++)
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
            Result<bool> result = await dataProvider.ManageInviteRequestToJoinAsync(communityId, invitationId, InviteRequestIntention.accepted, ct)
                                                    .SuppressToResultAsync(ReportCategory.COMMUNITIES);

            if (ct.IsCancellationRequested)
                return;

            if (!result.Success || !result.Value) { NotificationsBusController.Instance.AddNotification(new ServerErrorNotification(ACCEPT_COMMUNITY_INVITATION_ERROR_MESSAGE)); }
        }

        private void RejectCommunityInvitation(string communityId, string invitationId)
        {
            rejectCommunityInvitationCts = rejectCommunityInvitationCts.SafeRestart();
            RejectCommunityInvitationAsync(communityId, invitationId, rejectCommunityInvitationCts.Token).Forget();
        }

        private async UniTaskVoid RejectCommunityInvitationAsync(string communityId, string invitationId, CancellationToken ct)
        {
            Result<bool> result = await dataProvider.ManageInviteRequestToJoinAsync(communityId, invitationId, InviteRequestIntention.rejected, ct)
                                                    .SuppressToResultAsync(ReportCategory.COMMUNITIES);

            if (ct.IsCancellationRequested)
                return;

            if (!result.Success || !result.Value) { NotificationsBusController.Instance.AddNotification(new ServerErrorNotification(REJECT_COMMUNITY_INVITATION_ERROR_MESSAGE)); }
        }

        private void OnOpenCommunityProfile(string communityId) =>
            mvcManager.ShowAsync(CommunityCardController.IssueCommand(new CommunityCardParameter(communityId, spriteCache))).Forget();

        private void OpenCommunityProfile(CommunitiesBrowserEvents.CommunityProfileOpenedEvent evt) =>
            OnOpenCommunityProfile(evt.CommunityId);

        private void CreateCommunity()
        {
            openCommunityCreationCts = openCommunityCreationCts.SafeRestart();
            commandsLibrary.CreateCommunityCommand.Execute(openCommunityCreationCts.Token);
        }

        private void OnCommunityUpdated(string _) =>
            ReloadBrowser();

        private void OnCommunityJoined(string communityId, bool success)
        {
            foreach (GetUserInviteRequestData.UserInviteRequestData invitation in browserStateService.CurrentInvitationRequests)
            {
                if (communityId == invitation.communityId)
                {
                    RefreshInvitesAndRequestsCounter(setCounterToZeroAtTheBeginning: false);
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
                    RefreshInvitesAndRequestsCounter(setCounterToZeroAtTheBeginning: false);
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
                browserStateService.UpdateRequestToJoinCommunity(communityId, null, false, success, false);
            else
                view.InvitesAndRequestsView.UpdateJoinRequestCancelled(communityId, success);

            browserEventBus.RaiseUpdateJoinedCommunityEvent(communityId, success, false);

            if (success && !RemoveCurrentCommunityInviteRequest(communityId))
                LoadMyCommunities();
        }

        private void OnCommunityInviteRequestAccepted(string communityId, string requestId, bool success)
        {
            if (isInvitesAndRequestsSectionActive)
            {
                view.InvitesAndRequestsView.UpdateCommunityInvitation(communityId, success);
                view.InvitesAndRequestsView.UpdateRequestsReceived(communityId, requestId, success);
            }

            if (success)
            {
                browserEventBus.RaiseUpdateJoinedCommunityEvent(communityId, true, success);

                RefreshInvitesAndRequestsCounter(setCounterToZeroAtTheBeginning: false);

                if (!RemoveCurrentCommunityInviteRequest(communityId))
                    LoadMyCommunities();
            }
        }

        private void OnCommunityInviteRequestRejected(string communityId, string requestId, bool success)
        {
            if (isInvitesAndRequestsSectionActive)
            {
                view.InvitesAndRequestsView.UpdateCommunityInvitation(communityId, success);
                view.InvitesAndRequestsView.UpdateRequestsReceived(communityId, requestId, success);
            }

            if (success)
            {
                RefreshInvitesAndRequestsCounter(setCounterToZeroAtTheBeginning: false);
                RemoveCurrentCommunityInviteRequest(communityId);
                LoadMyCommunities();
            }
        }

        private bool RemoveCurrentCommunityInviteRequest(string communityId)
        {
            var foundInvitation = false;

            foreach (GetUserInviteRequestData.UserInviteRequestData? invitation in browserStateService.CurrentInvitationRequests)
            {
                if (invitation.communityId == communityId)
                {
                    browserStateService.RemoveInvitation(invitation);
                    foundInvitation = true;
                    break;
                }
            }

            var foundJoinRequest = false;

            foreach (GetUserInviteRequestData.UserInviteRequestData? joinRequest in browserStateService.CurrentJoinRequests)
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
            dataProvider.CommunityOwnershipTransferred += OnCommunityUpdated;
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
            dataProvider.CommunityOwnershipTransferred -= OnCommunityUpdated;
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

            RefreshInvitesAndRequestsCounter();
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

        private void OnCommunityTransferredToMe(INotification notification)
        {
            if (!isSectionActivated)
                return;

            ReloadBrowser();
        }

        private void RefreshAfterDeletion()
        {
            if (!isSectionActivated)
                return;

            LoadMyCommunities();

            if (!isInvitesAndRequestsSectionActive)
                LoadJoinRequestsAndAllCommunities();
        }

        private void OpenProfilePassport(ICommunityMemberData profile) =>
            mvcManager.ShowAsync(PassportController.IssueCommand(new PassportParams(profile.Address)), CancellationToken.None).Forget();

        private async void OpenUserChatAsync(ICommunityMemberData profile)
        {
            try
            {
                await sharedSpaceManager.ShowAsync(PanelsSharingSpace.Chat, new ChatMainSharedAreaControllerShowParams(true, true));
                chatEventBus.OpenPrivateConversationUsingUserId(profile.Address);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                ReportHub.LogException(ex, ReportCategory.COMMUNITIES);
            }
        }

        private async void CallUserAsync(ICommunityMemberData profile)
        {
            try
            {
                await sharedSpaceManager.ShowAsync(PanelsSharingSpace.Chat, new ChatMainSharedAreaControllerShowParams(true, true));
                orchestrator.StartPrivateCallWithUserId(profile.Address);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { ReportHub.LogError(new ReportData(ReportCategory.VOICE_CHAT), $"Error starting call from passport {ex.Message}"); }
        }

        private async void BlockUserAsync(ICommunityMemberData profile)
        {
            try
            {
                await mvcManager.ShowAsync(BlockUserPromptController.IssueCommand(
                    new BlockUserPromptParams(new Web3Address(profile.Address), profile.Name, BlockUserPromptParams.UserBlockAction.BLOCK)), CancellationToken.None);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                ReportHub.LogException(ex, ReportCategory.COMMUNITIES);
            }
        }

        private void ManageRequestReceived(string communityId, ICommunityMemberData profile, InviteRequestIntention intention)
        {
            manageRequestReceivedCts = manageRequestReceivedCts.SafeRestart();
            ManageRequestReceivedAsync(communityId, profile.Id, intention, manageRequestReceivedCts.Token).Forget();
            return;

            async UniTaskVoid ManageRequestReceivedAsync(string commId, string profileId, InviteRequestIntention reqIntention, CancellationToken ct)
            {
                Result<bool> result = await dataProvider.ManageInviteRequestToJoinAsync(commId, profileId, reqIntention, ct)
                                                        .SuppressToResultAsync(ReportCategory.COMMUNITIES);

                if (ct.IsCancellationRequested)
                    return;

                if (!result.Success || !result.Value)
                    NotificationsBusController.Instance.AddNotification(new ServerErrorNotification(MANAGE_REQUEST_RECEIVED_ERROR_TEXT));
            }
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
