using Cysharp.Threading.Tasks;
using DCL.Input;
using DCL.Input.Component;
using DCL.Profiles.Self;
using DCL.UI;
using DCL.UI.Utilities;
using DCL.WebRequests;
using SuperScrollView;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using Utility;
using CommunityData = DCL.Communities.GetUserCommunitiesResponse.CommunityData;

namespace DCL.Communities.CommunitiesBrowser
{
    public class CommunitiesBrowserController : ISection, IDisposable
    {
        private const int COMMUNITIES_PER_PAGE = 20;
        private const string MY_COMMUNITIES_RESULTS_TITLE = "My Communities";
        private const string MY_GENERAL_RESULTS_TITLE = "Decentraland Communities";
        private const int SEARCH_AWAIT_TIME = 1000;

        private readonly CommunitiesBrowserView view;
        private readonly RectTransform rectTransform;
        private readonly ICursor cursor;
        private readonly ICommunitiesDataProvider dataProvider;
        private readonly ISelfProfile selfProfile;
        private readonly IWebRequestController webRequestController;
        private readonly IInputBlock inputBlock;
        private readonly List<CommunityData> currentMyCommunities = new ();
        private readonly List<CommunityData> currentResults = new ();

        private CancellationTokenSource loadMyCommunitiesCts;
        private CancellationTokenSource loadResultsCts;
        private CancellationTokenSource searchCancellationToken;

        private string currentNameFilter;
        private bool currentIsOwnerFilter;
        private bool currentIsMemberFilter;
        private int currentPageNumberFilter = 1;
        private int currentResultsTotalAmount;
        private bool isGridResultsLoadingItems;

        public CommunitiesBrowserController(
            CommunitiesBrowserView view,
            ICursor cursor,
            ICommunitiesDataProvider dataProvider,
            ISelfProfile selfProfile,
            IWebRequestController webRequestController,
            IInputBlock inputBlock)
        {
            this.view = view;
            rectTransform = view.transform.parent.GetComponent<RectTransform>();
            this.cursor = cursor;
            this.dataProvider = dataProvider;
            this.selfProfile = selfProfile;
            this.webRequestController = webRequestController;
            this.inputBlock = inputBlock;

            ConfigureMyCommunitiesList();
            ConfigureResultsGrid();

            view.myCommunitiesViewAllButton.onClick.AddListener(LoadAllMyCommunitiesResults);
            view.resultsBackButton.onClick.AddListener(LoadAllCommunitiesResults);
            view.searchBar.inputField.onSelect.AddListener(DisableShortcutsInput);
            view.searchBar.inputField.onDeselect.AddListener(RestoreInput);
            view.searchBar.inputField.onValueChanged.AddListener(OnSearchBarValueChanged);
            view.searchBar.clearSearchButton.onClick.AddListener(OnSearchBarCleared);
            view.SetSearchBarClearButtonActive(false);
        }

        public void Activate()
        {
            view.gameObject.SetActive(true);
            cursor.Unlock();

            loadMyCommunitiesCts = loadMyCommunitiesCts.SafeRestart();
            LoadMyCommunitiesAsync(loadMyCommunitiesCts.Token).Forget();

            LoadAllCommunitiesResults();
        }

        public void Deactivate()
        {
            view.gameObject.SetActive(false);
            loadMyCommunitiesCts?.SafeCancelAndDispose();
            loadResultsCts?.SafeCancelAndDispose();
            searchCancellationToken?.SafeCancelAndDispose();
        }

        public void Animate(int triggerId)
        {
            view.panelAnimator.SetTrigger(triggerId);
            view.headerAnimator.SetTrigger(triggerId);
        }

        public void ResetAnimator()
        {
            view.panelAnimator.Rebind();
            view.headerAnimator.Rebind();
            view.panelAnimator.Update(0);
            view.headerAnimator.Update(0);
        }

        public RectTransform GetRectTransform() =>
            rectTransform;

        public void Dispose()
        {
            view.myCommunitiesViewAllButton.onClick.RemoveListener(LoadAllMyCommunitiesResults);
            view.resultsBackButton.onClick.RemoveListener(LoadAllCommunitiesResults);
            view.searchBar.inputField.onSelect.RemoveListener(DisableShortcutsInput);
            view.searchBar.inputField.onDeselect.RemoveListener(RestoreInput);
            view.searchBar.inputField.onValueChanged.RemoveListener(OnSearchBarValueChanged);
            view.searchBar.clearSearchButton.onClick.RemoveListener(OnSearchBarCleared);
            loadMyCommunitiesCts?.SafeCancelAndDispose();
            loadResultsCts?.SafeCancelAndDispose();
            searchCancellationToken?.SafeCancelAndDispose();
        }

        private void ConfigureMyCommunitiesList()
        {
            view.myCommunitiesLoopList.InitListView(0, SetupMyCommunityCardByIndex);
            view.myCommunitiesLoopList.gameObject.GetComponent<ScrollRect>()?.SetScrollSensitivityBasedOnPlatform();
        }

        private void ConfigureResultsGrid()
        {
            view.resultLoopGrid.InitGridView(0, SetupCommunityResultCardByIndex);
            view.resultLoopGrid.gameObject.GetComponent<ScrollRect>()?.SetScrollSensitivityBasedOnPlatform();
            view.resultLoopGrid.ScrollRect.onValueChanged.AddListener(_ =>
            {
                if (isGridResultsLoadingItems || currentResults.Count >= currentResultsTotalAmount || view.resultLoopGrid.ScrollRect.verticalNormalizedPosition > 0.01f)
                    return;

                loadResultsCts = loadResultsCts.SafeRestart();
                LoadResultsAsync(currentNameFilter, currentIsOwnerFilter, currentIsMemberFilter, currentPageNumberFilter + 1, COMMUNITIES_PER_PAGE, loadResultsCts.Token).Forget();
            });
        }

        private LoopListViewItem2 SetupMyCommunityCardByIndex(LoopListView2 loopListView, int index)
        {
            LoopListViewItem2 listItem = loopListView.NewListViewItem(loopListView.ItemPrefabDataList[0].mItemPrefab.name);

            MyCommunityCardView cardView = listItem.GetComponent<MyCommunityCardView>();
            cardView.SetTitle(currentMyCommunities[index].name);
            cardView.SetUserRole(currentMyCommunities[index].role);
            cardView.ConfigureImageController(webRequestController);
            cardView.SetCommunityThumbnail(currentMyCommunities[index].thumbnails[0]);

            return listItem;
        }

        private LoopGridViewItem SetupCommunityResultCardByIndex(LoopGridView loopGridView, int index, int row, int column)
        {
            LoopGridViewItem gridItem = loopGridView.NewListViewItem(loopGridView.ItemPrefabDataList[0].mItemPrefab.name);
            CommunityResultCardView cardView = gridItem.GetComponent<CommunityResultCardView>();
            cardView.SetTitle(currentResults[index].name);
            cardView.ConfigureImageController(webRequestController);
            cardView.SetCommunityThumbnail(currentResults[index].thumbnails[0]);

            return gridItem;
        }

        private async UniTask LoadMyCommunitiesAsync(CancellationToken ct)
        {
            currentMyCommunities.Clear();
            view.myCommunitiesLoopList.SetListItemCount(0, false);
            view.SetMyCommunitiesAsLoading(true);

            var ownProfile = await selfProfile.ProfileAsync(ct);
            if (ownProfile == null)
                return;

            var userCommunitiesResponse = await dataProvider.GetUserCommunitiesAsync(ownProfile.UserId, name: string.Empty, isOwner: true, isMember: true, pageNumber: 1, elementsPerPage: 1000, ct);

            foreach (CommunityData community in userCommunitiesResponse.communities)
                currentMyCommunities.Add(community);

            view.SetMyCommunitiesAsLoading(false);
            view.myCommunitiesLoopList.SetListItemCount(currentMyCommunities.Count, true);
            view.SetMyCommunitiesAsEmpty(currentMyCommunities.Count == 0);
        }

        private void LoadAllMyCommunitiesResults()
        {
            view.SetResultsBackButtonVisible(true);
            view.SetResultsTitleText(MY_COMMUNITIES_RESULTS_TITLE);

            loadResultsCts = loadResultsCts.SafeRestart();
            LoadResultsAsync(string.Empty, true, true, 1, COMMUNITIES_PER_PAGE, loadResultsCts.Token).Forget();

        }

        private void LoadAllCommunitiesResults()
        {
            view.CleanSearchBar(raiseOnChangeEvent: false);
            loadResultsCts = loadResultsCts.SafeRestart();
            LoadResultsAsync(string.Empty, false, false, 1, COMMUNITIES_PER_PAGE, loadResultsCts.Token).Forget();

            view.SetResultsBackButtonVisible(false);
            view.SetResultsTitleText(MY_GENERAL_RESULTS_TITLE);
        }

        private async UniTask LoadResultsAsync(string name, bool isOwner, bool isMember, int pageNumber, int elementsPerPage, CancellationToken ct)
        {
            isGridResultsLoadingItems = true;

            if (pageNumber == 1)
            {
                currentResults.Clear();
                view.resultLoopGrid.SetListItemCount(0, false);
                view.SetResultsAsLoading(true);
            }
            else
                view.SetResultsLoadingMoreActive(true);

            var ownProfile = await selfProfile.ProfileAsync(ct);
            if (ownProfile == null)
                return;

            var userCommunitiesResponse = await dataProvider.GetUserCommunitiesAsync(ownProfile.UserId, name, isOwner, isMember, pageNumber, elementsPerPage, ct);

            if (userCommunitiesResponse.communities.Length > 0)
            {
                currentPageNumberFilter = pageNumber;

                foreach (CommunityData community in userCommunitiesResponse.communities)
                    currentResults.Add(community);
            }

            currentResultsTotalAmount = userCommunitiesResponse.totalAmount;

            if (pageNumber == 1)
            {
                view.SetResultsAsLoading(false);
                view.SetResultsAsEmpty(currentResults.Count == 0);
            }

            view.SetResultsLoadingMoreActive(false);
            view.SetResultsCountText(currentResultsTotalAmount);
            view.resultLoopGrid.SetListItemCount(currentResults.Count, resetPos: pageNumber == 1);

            currentNameFilter = name;
            currentIsOwnerFilter = isOwner;
            currentIsMemberFilter = isMember;
            isGridResultsLoadingItems = false;
        }

        private void DisableShortcutsInput(string text) =>
            inputBlock.Disable(InputMapComponent.Kind.SHORTCUTS, InputMapComponent.Kind.IN_WORLD_CAMERA);

        private void RestoreInput(string text) =>
            inputBlock.Enable(InputMapComponent.Kind.SHORTCUTS, InputMapComponent.Kind.IN_WORLD_CAMERA);

        private void OnSearchBarValueChanged(string searchText)
        {
            if (string.IsNullOrEmpty(searchText))
                LoadAllCommunitiesResults();
            else
            {
                searchCancellationToken = searchCancellationToken.SafeRestart();
                AwaitAndSendSearchAsync(searchText, searchCancellationToken.Token).Forget();
            }

            view.SetSearchBarClearButtonActive(!string.IsNullOrEmpty(searchText));
        }

        private async UniTaskVoid AwaitAndSendSearchAsync(string searchText, CancellationToken ct)
        {
            await UniTask.Delay(SEARCH_AWAIT_TIME, cancellationToken: ct);

            view.SetResultsBackButtonVisible(true);
            view.SetResultsTitleText($"Results for '{searchText}'");

            loadResultsCts = loadResultsCts.SafeRestart();
            LoadResultsAsync(searchText, false, false, 1, COMMUNITIES_PER_PAGE, loadResultsCts.Token).Forget();
        }

        private void OnSearchBarCleared() =>
            view.CleanSearchBar();
    }
}
