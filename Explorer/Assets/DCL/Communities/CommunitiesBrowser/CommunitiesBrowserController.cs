using Cysharp.Threading.Tasks;
using DCL.Input;
using DCL.Input.Component;
using DCL.Profiles.Self;
using DCL.UI;
using DCL.UI.Profiles.Helpers;
using DCL.UI.Utilities;
using DCL.WebRequests;
using MVC;
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
        private const float NORMALIZED_V_POSITION_OFFSET_FOR_LOADING_MORE = 0.01f;

        private readonly CommunitiesBrowserView view;
        private readonly RectTransform rectTransform;
        private readonly ICursor cursor;
        private readonly ICommunitiesDataProvider dataProvider;
        private readonly ISelfProfile selfProfile;
        private readonly IWebRequestController webRequestController;
        private readonly IInputBlock inputBlock;
        private readonly ViewDependencies viewDependencies;
        private readonly List<CommunityData> currentMyCommunities = new ();
        private readonly List<CommunityData> currentResults = new ();
        private readonly List<CommunityMemberRole> currentMemberRolesIncluded = new ();

        private CancellationTokenSource loadMyCommunitiesCts;
        private CancellationTokenSource loadResultsCts;
        private CancellationTokenSource searchCancellationCts;

        private string currentNameFilter;
        private bool currentIsOwnerFilter;
        private bool currentIsMemberFilter;
        private int currentPageNumberFilter = 1;
        private int currentResultsTotalAmount;
        private string currentSearchText = string.Empty;
        private bool isGridResultsLoadingItems;

        public CommunitiesBrowserController(
            CommunitiesBrowserView view,
            ICursor cursor,
            ICommunitiesDataProvider dataProvider,
            ISelfProfile selfProfile,
            IWebRequestController webRequestController,
            IInputBlock inputBlock,
            ViewDependencies viewDependencies)
        {
            this.view = view;
            rectTransform = view.transform.parent.GetComponent<RectTransform>();
            this.cursor = cursor;
            this.dataProvider = dataProvider;
            this.selfProfile = selfProfile;
            this.webRequestController = webRequestController;
            this.inputBlock = inputBlock;
            this.viewDependencies = viewDependencies;

            ConfigureMyCommunitiesList();
            ConfigureResultsGrid();

            view.myCommunitiesViewAllButton.onClick.AddListener(ViewAllMyCommunitiesResults);
            view.resultsBackButton.onClick.AddListener(LoadAllCommunitiesResults);
            view.searchBar.inputField.onSelect.AddListener(DisableShortcutsInput);
            view.searchBar.inputField.onDeselect.AddListener(RestoreInput);
            view.searchBar.inputField.onValueChanged.AddListener(OnSearchBarValueChanged);
            view.searchBar.clearSearchButton.onClick.AddListener(OnSearchBarCleared);
        }

        public void Activate()
        {
            view.gameObject.SetActive(true);
            cursor.Unlock();

            // Each time we open the Communities section, we load both my communities and Decentraland communities
            loadMyCommunitiesCts = loadMyCommunitiesCts.SafeRestart();
            LoadMyCommunitiesAsync(loadMyCommunitiesCts.Token).Forget();
            LoadAllCommunitiesResults();
        }

        public void Deactivate()
        {
            view.gameObject.SetActive(false);
            loadMyCommunitiesCts?.SafeCancelAndDispose();
            loadResultsCts?.SafeCancelAndDispose();
            searchCancellationCts?.SafeCancelAndDispose();
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
            view.resultLoopGrid.ScrollRect.onValueChanged.RemoveListener(LoadMoreResults);
            view.myCommunitiesViewAllButton.onClick.RemoveListener(ViewAllMyCommunitiesResults);
            view.resultsBackButton.onClick.RemoveListener(LoadAllCommunitiesResults);
            view.searchBar.inputField.onSelect.RemoveListener(DisableShortcutsInput);
            view.searchBar.inputField.onDeselect.RemoveListener(RestoreInput);
            view.searchBar.inputField.onValueChanged.RemoveListener(OnSearchBarValueChanged);
            view.searchBar.clearSearchButton.onClick.RemoveListener(OnSearchBarCleared);
            loadMyCommunitiesCts?.SafeCancelAndDispose();
            loadResultsCts?.SafeCancelAndDispose();
            searchCancellationCts?.SafeCancelAndDispose();
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
            view.resultLoopGrid.ScrollRect.onValueChanged.AddListener(LoadMoreResults);
        }

        private LoopListViewItem2 SetupMyCommunityCardByIndex(LoopListView2 loopListView, int index)
        {
            CommunityData communityData = currentMyCommunities[index];
            LoopListViewItem2 listItem = loopListView.NewListViewItem(loopListView.ItemPrefabDataList[0].mItemPrefab.name);
            MyCommunityCardView cardView = listItem.GetComponent<MyCommunityCardView>();

            // Setup card data
            cardView.SetTitle(communityData.name);
            cardView.SetUserRole(communityData.role);
            cardView.SetLiveMarkAsActive(communityData.isLive);
            cardView.ConfigureImageController(webRequestController);
            cardView.SetCommunityThumbnail(communityData.thumbnails[0]);

            // Setup card events
            cardView.mainButton.onClick.RemoveAllListeners();
            cardView.mainButton.onClick.AddListener(() => { OpenCommunityProfile(communityData.id); });

            return listItem;
        }

        private LoopGridViewItem SetupCommunityResultCardByIndex(LoopGridView loopGridView, int index, int row, int column)
        {
            CommunityData communityData = currentResults[index];
            LoopGridViewItem gridItem = loopGridView.NewListViewItem(loopGridView.ItemPrefabDataList[0].mItemPrefab.name);
            CommunityResultCardView cardView = gridItem.GetComponent<CommunityResultCardView>();

            // Setup card data
            cardView.SetTitle(communityData.name);
            cardView.SetPrivacy(communityData.privacy);
            cardView.SetMembersCount(communityData.memberCount);
            cardView.SetOwnership(communityData.role != CommunityMemberRole.none);
            cardView.SetLiveMarkAsActive(communityData.isLive);
            cardView.ConfigureImageController(webRequestController);
            cardView.SetCommunityThumbnail(communityData.thumbnails[0]);
            cardView.SetJoiningLoadingActive(false);

            // Setup card events
            cardView.mainButton.onClick.RemoveAllListeners();
            cardView.mainButton.onClick.AddListener(() => OpenCommunityProfile(communityData.id));
            cardView.viewCommunityButton.onClick.RemoveAllListeners();
            cardView.viewCommunityButton.onClick.AddListener(() => OpenCommunityProfile(communityData.id));
            cardView.joinCommunityButton.onClick.RemoveAllListeners();
            cardView.joinCommunityButton.onClick.AddListener(() => JoinCommunityAsync(index, cardView, CancellationToken.None).Forget());

            // Setup mutual friends
            cardView.InjectDependencies(viewDependencies);
            for (var i = 0; i < cardView.mutualFriends.thumbnails.Length; i++)
            {
                bool friendExists = i < communityData.friends.Length;
                cardView.mutualFriends.thumbnails[i].root.SetActive(friendExists);
                if (!friendExists) continue;
                GetUserCommunitiesResponse.FriendInCommunity mutualFriend = communityData.friends[i];
                cardView.mutualFriends.thumbnails[i].picture.Setup(ProfileNameColorHelper.GetNameColor(mutualFriend.name), mutualFriend.profilePictureUrl, mutualFriend.id);
            }

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

            var userCommunitiesResponse = await dataProvider.GetUserCommunitiesAsync(
                userId: ownProfile.UserId,
                name: string.Empty,
                memberRolesIncluded: new [] { CommunityMemberRole.owner, CommunityMemberRole.moderator, CommunityMemberRole.member },
                pageNumber: 1,
                elementsPerPage: 1000,
                ct: ct);

            foreach (CommunityData community in userCommunitiesResponse.communities)
                currentMyCommunities.Add(community);

            view.SetMyCommunitiesAsLoading(false);
            view.myCommunitiesLoopList.SetListItemCount(currentMyCommunities.Count);
            view.SetMyCommunitiesAsEmpty(currentMyCommunities.Count == 0);
        }

        private void ViewAllMyCommunitiesResults()
        {
            ClearSearchBar();
            view.SetResultsBackButtonVisible(true);
            view.SetResultsTitleText(MY_COMMUNITIES_RESULTS_TITLE);

            loadResultsCts = loadResultsCts.SafeRestart();
            LoadResultsAsync(
                name: string.Empty,
                memberRolesIncluded: new [] { CommunityMemberRole.owner, CommunityMemberRole.moderator, CommunityMemberRole.member },
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
                memberRolesIncluded: new [] { CommunityMemberRole.owner, CommunityMemberRole.moderator, CommunityMemberRole.member, CommunityMemberRole.none },
                pageNumber: 1,
                elementsPerPage: COMMUNITIES_PER_PAGE,
                ct: loadResultsCts.Token).Forget();

            view.SetResultsBackButtonVisible(false);
            view.SetResultsTitleText(MY_GENERAL_RESULTS_TITLE);
        }

        private void LoadMoreResults(Vector2 _)
        {
            if (isGridResultsLoadingItems ||
                currentResults.Count >= currentResultsTotalAmount ||
                view.resultLoopGrid.ScrollRect.verticalNormalizedPosition > NORMALIZED_V_POSITION_OFFSET_FOR_LOADING_MORE)
                return;

            loadResultsCts = loadResultsCts.SafeRestart();
            LoadResultsAsync(
                name: currentNameFilter,
                memberRolesIncluded: currentMemberRolesIncluded.ToArray(),
                pageNumber: currentPageNumberFilter + 1,
                elementsPerPage: COMMUNITIES_PER_PAGE,
                ct: loadResultsCts.Token).Forget();
        }

        private async UniTask LoadResultsAsync(string name, CommunityMemberRole[] memberRolesIncluded, int pageNumber, int elementsPerPage, CancellationToken ct)
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

            var userCommunitiesResponse = await dataProvider.GetUserCommunitiesAsync(
                ownProfile.UserId,
                name,
                memberRolesIncluded,
                pageNumber,
                elementsPerPage,
                ct);

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
            currentMemberRolesIncluded.Clear();
            foreach (CommunityMemberRole memberRole in memberRolesIncluded)
                currentMemberRolesIncluded.Add(memberRole);
            isGridResultsLoadingItems = false;
        }

        private void DisableShortcutsInput(string text) =>
            inputBlock.Disable(InputMapComponent.Kind.SHORTCUTS, InputMapComponent.Kind.IN_WORLD_CAMERA);

        private void RestoreInput(string text) =>
            inputBlock.Enable(InputMapComponent.Kind.SHORTCUTS, InputMapComponent.Kind.IN_WORLD_CAMERA);

        private void OnSearchBarValueChanged(string searchText)
        {
            searchCancellationCts = searchCancellationCts.SafeRestart();
            AwaitAndSendSearchAsync(searchText, searchCancellationCts.Token).Forget();

            view.SetSearchBarClearButtonActive(!string.IsNullOrEmpty(searchText));
        }

        private async UniTaskVoid AwaitAndSendSearchAsync(string searchText, CancellationToken ct)
        {
            await UniTask.Delay(SEARCH_AWAIT_TIME, cancellationToken: ct);

            if (currentSearchText == searchText)
                return;

            if (string.IsNullOrEmpty(searchText))
                LoadAllCommunitiesResults();
            else
            {
                view.SetResultsBackButtonVisible(true);
                view.SetResultsTitleText($"Results for '{searchText}'");

                loadResultsCts = loadResultsCts.SafeRestart();
                LoadResultsAsync(
                    name: searchText,
                    memberRolesIncluded: new [] { CommunityMemberRole.owner, CommunityMemberRole.moderator, CommunityMemberRole.member, CommunityMemberRole.none },
                    pageNumber: 1,
                    elementsPerPage: COMMUNITIES_PER_PAGE,
                    ct: loadResultsCts.Token).Forget();
            }

            currentSearchText = searchText;
        }

        private void OnSearchBarCleared()
        {
            ClearSearchBar();
            LoadAllCommunitiesResults();
        }

        private void ClearSearchBar()
        {
            currentSearchText = string.Empty;
            view.CleanSearchBar(raiseOnChangeEvent: false);
        }

        private async UniTask JoinCommunityAsync(int index, CommunityResultCardView cardView, CancellationToken ct)
        {
            cardView.SetJoiningLoadingActive(true);
            bool joinedSuccess = await dataProvider.JoinCommunityAsync(currentResults[index].id, ct);
            if (joinedSuccess)
            {
                currentResults[index].role = CommunityMemberRole.member;
                currentResults[index].memberCount++;
                currentMyCommunities.Add(currentResults[index]);

                view.resultLoopGrid.RefreshItemByItemIndex(index);
                view.myCommunitiesLoopList.SetListItemCount(currentMyCommunities.Count, false);
                view.SetMyCommunitiesAsEmpty(currentMyCommunities.Count == 0);
            }
        }

        private void OpenCommunityProfile(string communityId)
        {
            // TODO: Open community profile (currently implemented by Lorenzo)
        }
    }
}
