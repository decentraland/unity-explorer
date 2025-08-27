using Cysharp.Threading.Tasks;
using DCL.Chat.ControllerShowParams;
using DCL.Communities.CommunityCreation;
using DCL.Communities.CommunitiesCard;
using DCL.Diagnostics;
using DCL.Input;
using DCL.Input.Component;
using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.UI;
using DCL.UI.Profiles.Helpers;
using DCL.UI.SharedSpaceManager;
using DCL.Utilities.Extensions;
using DCL.VoiceChat;
using DCL.Web3;
using DCL.WebRequests;
using MVC;
using System;
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
        private const int SEARCH_AWAIT_TIME = 1000;
        private const string SEARCH_RESULTS_TITLE_FORMAT = "Results for '{0}'";
        private const string MY_COMMUNITIES_LOADING_ERROR_MESSAGE = "There was an error loading My Communities. Please try again.";
        private const string ALL_COMMUNITIES_LOADING_ERROR_MESSAGE = "There was an error loading Communities. Please try again.";
        private const string STREAMING_COMMUNITIES_LOADING_ERROR_MESSAGE = "There was an error loading Streaming Communities. Please try again.";

        private const string JOIN_COMMUNITY_ERROR_MESSAGE = "There was an error joining community. Please try again.";
        private const int WARNING_MESSAGE_DELAY_MS = 3000;

        private readonly CommunitiesBrowserView view;
        private readonly RectTransform rectTransform;
        private readonly ICursor cursor;
        private readonly CommunitiesDataProvider dataProvider;
        private readonly IInputBlock inputBlock;
        private readonly WarningNotificationView warningNotificationView;
        private readonly IMVCManager mvcManager;
        private readonly ProfileRepositoryWrapper profileRepositoryWrapper;
        private readonly ISelfProfile selfProfile;
        private readonly INftNamesProvider nftNamesProvider;
        private readonly ICommunityCallOrchestrator orchestrator;
        private readonly ISpriteCache spriteCache;
        private readonly ISharedSpaceManager sharedSpaceManager;

        private CancellationTokenSource? loadMyCommunitiesCts;
        private CancellationTokenSource? loadResultsCts;
        private CancellationTokenSource? searchCancellationCts;
        private CancellationTokenSource? showErrorCts;
        private CancellationTokenSource? openCommunityCreationCts;

        private bool isSectionActivated;
        private string currentNameFilter;
        private bool currentIsOwnerFilter;
        private bool currentIsMemberFilter;
        private int currentPageNumberFilter = 1;
        private int currentResultsTotalAmount;
        private string currentSearchText = string.Empty;
        private bool currentOnlyMemberOf;
        private bool isGridResultsLoadingItems;

        public CommunitiesBrowserController(
            CommunitiesBrowserView view,
            ICursor cursor,
            CommunitiesDataProvider dataProvider,
            IWebRequestController webRequestController,
            IInputBlock inputBlock,
            WarningNotificationView warningNotificationView,
            IMVCManager mvcManager,
            ProfileRepositoryWrapper profileDataProvider,
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
            this.profileRepositoryWrapper = profileDataProvider;
            this.warningNotificationView = warningNotificationView;
            this.mvcManager = mvcManager;
            this.selfProfile = selfProfile;
            this.nftNamesProvider = nftNamesProvider;
            this.orchestrator = orchestrator;
            this.sharedSpaceManager = sharedSpaceManager;

            spriteCache = new SpriteCache(webRequestController);

            ConfigureMyCommunitiesList();
            ConfigureResultsGrid();

            view.InitializeStreamingResultsGrid(0);

            view.SetThumbnailLoader(new ThumbnailLoader(spriteCache));

            view.ViewAllMyCommunitiesButtonClicked += ViewAllMyCommunitiesResults;
            view.ResultsBackButtonClicked += LoadAllCommunitiesResults;
            view.SearchBarSelected += DisableShortcutsInput;
            view.SearchBarDeselected += RestoreInput;
            view.SearchBarValueChanged += SearchBarValueChanged;
            view.SearchBarSubmit += SearchBarSubmit;
            view.SearchBarClearButtonClicked += SearchBarCleared;
            view.CommunityProfileOpened += OpenCommunityProfile;
            view.CommunityJoined += JoinCommunity;
            view.CreateCommunityButtonClicked += CreateCommunity;
            view.JoinStream += JoinStream;
        }

        private void JoinStream(string communityId)
        {
            //If we already joined, we cannot join again
            if (orchestrator.CurrentCommunityId.Value == communityId) return;

            JoinStreamAsync().Forget();
            return;

            async UniTaskVoid JoinStreamAsync()
            {
                await sharedSpaceManager.ShowAsync(PanelsSharingSpace.Chat, new ChatControllerShowParams(false));
                await UniTask.Delay(500);
                orchestrator.JoinCommunityVoiceChat(communityId, CancellationToken.None, true);
            }
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
            view.ResultsBackButtonClicked -= LoadAllCommunitiesResults;
            view.SearchBarSelected -= DisableShortcutsInput;
            view.SearchBarDeselected -= RestoreInput;
            view.SearchBarValueChanged -= SearchBarValueChanged;
            view.SearchBarSubmit -= SearchBarSubmit;
            view.SearchBarClearButtonClicked -= SearchBarCleared;
            view.CommunityProfileOpened -= OpenCommunityProfile;
            view.CommunityJoined -= JoinCommunity;
            view.CreateCommunityButtonClicked -= CreateCommunity;

            UnsubscribeDataProviderEvents();

            loadMyCommunitiesCts?.SafeCancelAndDispose();
            loadResultsCts?.SafeCancelAndDispose();
            searchCancellationCts?.SafeCancelAndDispose();
            showErrorCts?.SafeCancelAndDispose();
            openCommunityCreationCts?.SafeCancelAndDispose();
            spriteCache.Clear();
        }

        private void ReloadBrowser()
        {
            // Each time we open the Communities section, we load both my communities and Decentraland communities
            loadMyCommunitiesCts = loadMyCommunitiesCts.SafeRestart();
            LoadMyCommunitiesAsync(loadMyCommunitiesCts.Token).Forget();
            LoadAllCommunitiesResults();
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

            loadResultsCts = loadResultsCts.SafeRestart();
            LoadResultsAsync(
                name: string.Empty,
                onlyMemberOf: true,
                pageNumber: 1,
                elementsPerPage: COMMUNITIES_PER_PAGE,
                ct: loadResultsCts.Token).Forget();

        }

        private void LoadAllCommunitiesResults()
        {
            ClearSearchBar();
            loadResultsCts = loadResultsCts.SafeRestart();
            LoadResultsAsync(
                name: string.Empty,
                onlyMemberOf: false,
                pageNumber: 1,
                elementsPerPage: COMMUNITIES_PER_PAGE,
                ct: loadResultsCts.Token).Forget();

            view.SetResultsBackButtonVisible(false);
            view.SetResultsTitleText(MY_GENERAL_RESULTS_TITLE);
            LoadStreamingCommunitiesAsync(loadResultsCts.Token).Forget();
        }

        private async UniTaskVoid LoadStreamingCommunitiesAsync(CancellationToken ct)
        {
            view.ClearStreamingResultsItems();

            view.SetStreamingResultsAsLoading(true);

            var result = await dataProvider.GetUserCommunitiesAsync(
                string.Empty,
                false,
                1,
                7,
                ct,
                true
                ).SuppressToResultAsync(ReportCategory.COMMUNITIES);

            if (ct.IsCancellationRequested)
                return;

            if (!result.Success)
            {
                showErrorCts = showErrorCts.SafeRestart();
                await warningNotificationView.AnimatedShowAsync(STREAMING_COMMUNITIES_LOADING_ERROR_MESSAGE, WARNING_MESSAGE_DELAY_MS, showErrorCts.Token)
                                             .SuppressToResultAsync(ReportCategory.COMMUNITIES);
                return;
            }

            if (result.Value.data.results.Length > 0)
            {
                view.AddStreamingResultsItems(result.Value.data.results);
            }

            view.SetStreamingResultsAsLoading(false);
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
                ct: loadResultsCts.Token).Forget();
        }

        private async UniTaskVoid LoadResultsAsync(string name, bool onlyMemberOf, int pageNumber, int elementsPerPage, CancellationToken ct)
        {
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

                loadResultsCts = loadResultsCts.SafeRestart();
                LoadResultsAsync(
                    name: searchText,
                    onlyMemberOf: false,
                    pageNumber: 1,
                    elementsPerPage: COMMUNITIES_PER_PAGE,
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

        private void JoinCommunity(string communityId) =>
            JoinCommunityAsync(communityId, CancellationToken.None).Forget();

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
            dataProvider.CommunityLeft -= OnCommunityLeft;
            dataProvider.CommunityUserRemoved -= OnUserRemovedFromCommunity;
            dataProvider.CommunityUserBanned -= OnUserBannedFromCommunity;
        }
    }
}
