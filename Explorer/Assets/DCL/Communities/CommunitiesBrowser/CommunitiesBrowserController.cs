using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Input;
using DCL.Input.Component;
using DCL.UI;
using DCL.Utilities.Extensions;
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
        private const string MY_GENERAL_RESULTS_TITLE = "Decentraland Communities";
        private const int SEARCH_AWAIT_TIME = 1000;
        private const string SEARCH_RESULTS_TITLE_FORMAT = "Results for '{0}'";
        private const string MY_COMMUNITIES_LOADING_ERROR_MESSAGE = "There was an error loading My Communities. Please try again.";
        private const string ALL_COMMUNITIES_LOADING_ERROR_MESSAGE = "There was an error loading Communities. Please try again.";
        private const string JOIN_COMMUNITY_ERROR_MESSAGE = "There was an error joining community. Please try again.";
        private const int WARNING_MESSAGE_DELAY_MS = 3000;

        private readonly CommunitiesBrowserView view;
        private readonly RectTransform rectTransform;
        private readonly ICursor cursor;
        private readonly ICommunitiesDataProvider dataProvider;
        private readonly IWebRequestController webRequestController;
        private readonly IInputBlock inputBlock;
        private readonly ViewDependencies viewDependencies;
        private readonly WarningNotificationView warningNotificationView;

        private CancellationTokenSource loadMyCommunitiesCts;
        private CancellationTokenSource loadResultsCts;
        private CancellationTokenSource searchCancellationCts;
        private CancellationTokenSource showErrorCts;

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
            ICommunitiesDataProvider dataProvider,
            IWebRequestController webRequestController,
            IInputBlock inputBlock,
            ViewDependencies viewDependencies,
            WarningNotificationView warningNotificationView)
        {
            this.view = view;
            rectTransform = view.transform.parent.GetComponent<RectTransform>();
            this.cursor = cursor;
            this.dataProvider = dataProvider;
            this.webRequestController = webRequestController;
            this.inputBlock = inputBlock;
            this.viewDependencies = viewDependencies;
            this.warningNotificationView = warningNotificationView;

            ConfigureMyCommunitiesList();
            ConfigureResultsGrid();

            view.ViewAllMyCommunitiesButtonClicked += ViewAllMyCommunitiesResults;
            view.ResultsBackButtonClicked += LoadAllCommunitiesResults;
            view.SearchBarSelected += DisableShortcutsInput;
            view.SearchBarDeselected += RestoreInput;
            view.SearchBarValueChanged += SearchBarValueChanged;
            view.SearchBarSubmit += SearchBarSubmit;
            view.SearchBarClearButtonClicked += SearchBarCleared;
            view.CommunityProfileOpened += OpenCommunityProfile;
            view.CommunityJoined += JoinCommunity;
        }

        public void Activate()
        {
            view.SetViewActive(true);
            cursor.Unlock();

            // Each time we open the Communities section, we load both my communities and Decentraland communities
            loadMyCommunitiesCts = loadMyCommunitiesCts.SafeRestart();
            LoadMyCommunitiesAsync(loadMyCommunitiesCts.Token).Forget();
            LoadAllCommunitiesResults();
        }

        public void Deactivate()
        {
            view.SetViewActive(false);
            loadMyCommunitiesCts?.SafeCancelAndDispose();
            loadResultsCts?.SafeCancelAndDispose();
            searchCancellationCts?.SafeCancelAndDispose();
            showErrorCts?.SafeCancelAndDispose();
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
            loadMyCommunitiesCts?.SafeCancelAndDispose();
            loadResultsCts?.SafeCancelAndDispose();
            searchCancellationCts?.SafeCancelAndDispose();
            showErrorCts?.SafeCancelAndDispose();
        }

        private void ConfigureMyCommunitiesList() =>
            view.InitializeMyCommunitiesList(0, webRequestController);

        private void ConfigureResultsGrid()
        {
            view.InitializeResultsGrid(0, webRequestController, viewDependencies);
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

            if (!result.Success)
            {
                showErrorCts = showErrorCts.SafeRestart();
                ShowErrorNotificationAsync(MY_COMMUNITIES_LOADING_ERROR_MESSAGE, showErrorCts.Token).Forget();
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

            if (!result.Success)
            {
                showErrorCts = showErrorCts.SafeRestart();
                ShowErrorNotificationAsync(ALL_COMMUNITIES_LOADING_ERROR_MESSAGE, showErrorCts.Token).Forget();
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

        private void JoinCommunity(int index, string communityId) =>
            JoinCommunityAsync(index, communityId, CancellationToken.None).Forget();

        private async UniTaskVoid JoinCommunityAsync(int index, string communityId, CancellationToken ct)
        {
            var result = await dataProvider.JoinCommunityAsync(communityId, ct).SuppressToResultAsync(ReportCategory.COMMUNITIES);

            if (!result.Success || !result.Value)
            {
                showErrorCts = showErrorCts.SafeRestart();
                ShowErrorNotificationAsync(JOIN_COMMUNITY_ERROR_MESSAGE, showErrorCts.Token).Forget();
            }

            view.UpdateJoinedCommunity(index, result.Value);
        }

        private void OpenCommunityProfile(string communityId)
        {
            // TODO: Open community profile (currently implemented by Lorenzo)
        }

        private async UniTask ShowErrorNotificationAsync(string errorMessage, CancellationToken ct)
        {
            warningNotificationView.SetText(errorMessage);
            warningNotificationView.Show(ct);

            await UniTask.Delay(WARNING_MESSAGE_DELAY_MS, cancellationToken: ct);

            warningNotificationView.Hide(ct: ct);
        }
    }
}
