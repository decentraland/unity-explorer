using Cysharp.Threading.Tasks;
using DCL.Communities.CommunityCreation;
using DCL.Communities.CommunitiesCard;
using DCL.Diagnostics;
using DCL.Input;
using DCL.Input.Component;
using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.UI;
using DCL.UI.Profiles.Helpers;
using DCL.Utilities;
using DCL.Utilities.Extensions;
using DCL.Web3;
using DCL.WebRequests;
using MVC;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Utility;
using CommunityData = DCL.Communities.GetUserCommunitiesData.CommunityData;

namespace DCL.Communities.CommunitiesBrowser
{
    public class CommunitiesBrowserController : ISection, IDisposable
    {
        private const string MY_COMMUNITIES_RESULTS_TITLE = "My Communities";
        private const string MY_GENERAL_RESULTS_TITLE = "Browse Communities";
        private const int SEARCH_AWAIT_TIME = 1000;
        private const string SEARCH_RESULTS_TITLE_FORMAT = "Results for '{0}'";
        private const string MY_COMMUNITIES_LOADING_ERROR_MESSAGE = "There was an error loading My Communities. Please try again.";
        private const string ALL_COMMUNITIES_LOADING_ERROR_MESSAGE = "There was an error loading Communities. Please try again.";
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
        private readonly ISpriteCache spriteCache;
        private readonly CommunitiesBrowserOrchestrator browserOrchestrator;
        private readonly ThumbnailLoader thumbnailLoader;
        private readonly IDisposable sectionTypeChangedSubscription;

        private readonly CommunitiesBrowserRightSectionController rightSectionController;

        private CancellationTokenSource? loadMyCommunitiesCts;
        private CancellationTokenSource? loadResultsCts;
        private CancellationTokenSource? searchCancellationCts;
        private CancellationTokenSource? showErrorCts;
        private CancellationTokenSource? openCommunityCreationCts;

        private bool isSectionActivated;
        private string currentSearchText = string.Empty;

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

            this.rightSectionController = new CommunitiesBrowserRightSectionController(view.RightSectionView);

            spriteCache = new SpriteCache(webRequestController);
            browserOrchestrator = new CommunitiesBrowserOrchestrator();
            thumbnailLoader = new ThumbnailLoader(spriteCache);

            ConfigureMyCommunitiesList();
            view.Initialize(browserOrchestrator, thumbnailLoader);

            view.ViewAllMyCommunitiesButtonClicked += ViewAllMyCommunitiesResults;
            view.SearchBarSelected += DisableShortcutsInput;
            view.SearchBarDeselected += RestoreInput;
            view.SearchBarValueChanged += SearchBarValueChanged;
            view.SearchBarSubmit += SearchBarSubmit;
            view.SearchBarClearButtonClicked += SearchBarCleared;
            view.CommunityProfileOpened += OpenCommunityProfile;
            view.CreateCommunityButtonClicked += CreateCommunity;

            //view.CommunityJoined += JoinCommunity;

            sectionTypeChangedSubscription = browserOrchestrator.CurrentBrowserSectionType.Subscribe(OnBrowserSectionTypeChanged);
            browserOrchestrator.CommunityRefreshRequested += RefreshCommunityCardInGrid;
        }

        private void OnBrowserSectionTypeChanged(BrowserSectionType type)
        {
            //EQUIVALENT TO -> view.ResultsBackButtonClicked += LoadAllCommunitiesResults;
            if (type == BrowserSectionType.BROWSE_ALL_SECTION)
                LoadAllCommunitiesResults();
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
            view.ViewAllMyCommunitiesButtonClicked -= ViewAllMyCommunitiesResults;
            view.SearchBarSelected -= DisableShortcutsInput;
            view.SearchBarDeselected -= RestoreInput;
            view.SearchBarValueChanged -= SearchBarValueChanged;
            view.SearchBarSubmit -= SearchBarSubmit;
            view.SearchBarClearButtonClicked -= SearchBarCleared;
            view.CommunityProfileOpened -= OpenCommunityProfile;
            view.CreateCommunityButtonClicked -= CreateCommunity;

            UnsubscribeDataProviderEvents();

            rightSectionController.Dispose();
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


        private async UniTaskVoid LoadMyCommunitiesAsync(CancellationToken ct)
        {
            browserOrchestrator.ClearMyCommunities();
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

            foreach (CommunityData communityData in result.Value.data.results)
            {
                browserOrchestrator.AddMyCommunity(communityData);
            }

            view.AddMyCommunitiesItems(true);
            view.SetMyCommunitiesAsLoading(false);
        }

        private void ViewAllMyCommunitiesResults()
        {
            ClearSearchBar();
            //Setup results view with my communities data!
            // we need to update the orchestrator onlyMemberOf value, make the name empty, and page number = 1.
            // then call to re-setup the
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
            // SWITCH ORCHESTRATOR VIEW TYPE SO IT TRIGGERS CHANGE OF VIEWS?
            loadResultsCts = loadResultsCts.SafeRestart();
            LoadResultsAsync(
                name: string.Empty,
                onlyMemberOf: false,
                pageNumber: 1,
                elementsPerPage: COMMUNITIES_PER_PAGE,
                ct: loadResultsCts.Token).Forget();

            view.SetResultsBackButtonVisible(false);
            view.SetResultsTitleText(MY_GENERAL_RESULTS_TITLE);
        }

        private void LoadMoreResults(Vector2 _)
        {
            if (isGridResultsLoadingItems ||
                browserOrchestrator.GetCommunitiesCount() >= currentResultsTotalAmount ||
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
                browserOrchestrator.ClearResults();
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
                foreach (CommunityData communityData in result.Value.data.results)
                {
                    browserOrchestrator.AddResultCommunity(communityData);
                }

                view.AddResultsItems(pageNumber == 1);
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
                //SETUP FILTERED RESULTS VIEW USING THE SEARCH TEXT AS NAME
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

        private void OnCommunityJoined(string communityId, bool success)
        {
            UpdateJoinedCommunity(communityId, true, success);
            // Refresh the community card (if exists) in the results' grid
            RefreshCommunityCardInGrid(communityId);

            view.UpdateJoinedCommunity(success);
        }

        private void OnCommunityLeft(string communityId, bool success)
        {
            UpdateJoinedCommunity(communityId, false, success);
            view.UpdateJoinedCommunity(success);
        }

        private void UpdateJoinedCommunity(string communityId, bool isJoined, bool isSuccess)
        {
            if (!isSuccess) return;

            browserOrchestrator.UpdateJoinedCommunity(communityId, isJoined);

        }

        private void OnCommunityCreated(CreateOrUpdateCommunityResponse.CommunityData newCommunity) =>
            ReloadBrowser();

        private void OnCommunityDeleted(string communityId) =>
            ReloadBrowser();

        private void OnUserRemovedFromCommunity(string communityId)
        {
            browserOrchestrator.DecreaseMembersCount(communityId);

            // Refresh the community card (if exists) in the results' grid
            RefreshCommunityCardInGrid(communityId);
        }

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
