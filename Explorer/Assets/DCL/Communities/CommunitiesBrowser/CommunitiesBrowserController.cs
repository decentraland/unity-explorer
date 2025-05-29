using Cysharp.Threading.Tasks;
using DCL.Input;
using DCL.Input.Component;
using DCL.Profiles.Self;
using DCL.UI;
using DCL.WebRequests;
using MVC;
using SuperScrollView;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
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
        private const string SEARCH_RESULTS_TITLE_FORMAT = "Results for '{0}'";

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
        private readonly CommunityMemberRole[] rolesIncludedForMyCommunities = { CommunityMemberRole.owner, CommunityMemberRole.moderator, CommunityMemberRole.member };
        private readonly CommunityMemberRole[] rolesIncludedForGenericSearch = { CommunityMemberRole.owner, CommunityMemberRole.moderator, CommunityMemberRole.member, CommunityMemberRole.none };

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

            view.ViewAllMyCommunitiesButtonClicked += ViewAllMyCommunitiesResults;
            view.ResultsBackButtonClicked += LoadAllCommunitiesResults;
            view.SearchBarSelected += DisableShortcutsInput;
            view.SearchBarDeselected += RestoreInput;
            view.SearchBarValueChanged += SearchBarValueChanged;
            view.SearchBarSubmit += SearchBarSubmit;
            view.SearchBarClearButtonClicked += SearchBarCleared;
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
            loadMyCommunitiesCts?.SafeCancelAndDispose();
            loadResultsCts?.SafeCancelAndDispose();
            searchCancellationCts?.SafeCancelAndDispose();
        }

        private void ConfigureMyCommunitiesList() =>
            view.InitializeMyCommunitiesList(0, SetupMyCommunityCardByIndex);

        private void ConfigureResultsGrid()
        {
            view.InitializeResultsGrid(0, SetupCommunityResultCardByIndex);
            view.ResultsLoopGridScrollChanged += LoadMoreResults;
        }

        private LoopListViewItem2 SetupMyCommunityCardByIndex(LoopListView2 loopListView, int index)
        {
            CommunityData communityData = currentMyCommunities[index];
            LoopListViewItem2 listItem = loopListView.NewListViewItem(loopListView.ItemPrefabDataList[0].mItemPrefab.name);
            MyCommunityCardView cardView = listItem.GetComponent<MyCommunityCardView>();

            // Setup card data
            cardView.SetCommunityId(communityData.id);
            cardView.SetTitle(communityData.name);
            cardView.SetUserRole(communityData.role);
            cardView.SetLiveMarkAsActive(communityData.isLive);
            cardView.ConfigureImageController(webRequestController);
            cardView.SetCommunityThumbnail(communityData.thumbnails[0]);

            // Setup card events
            cardView.MainButtonClicked -= OpenCommunityProfile;
            cardView.MainButtonClicked += OpenCommunityProfile;

            return listItem;
        }

        private LoopGridViewItem SetupCommunityResultCardByIndex(LoopGridView loopGridView, int index, int row, int column)
        {
            CommunityData communityData = currentResults[index];
            LoopGridViewItem gridItem = loopGridView.NewListViewItem(loopGridView.ItemPrefabDataList[0].mItemPrefab.name);
            CommunityResultCardView cardView = gridItem.GetComponent<CommunityResultCardView>();

            // Setup card data
            cardView.SetCommunityId(communityData.id);
            cardView.SetIndex(index);
            cardView.SetTitle(communityData.name);
            cardView.SetPrivacy(communityData.privacy);
            cardView.SetMembersCount(communityData.memberCount);
            cardView.SetOwnership(communityData.role != CommunityMemberRole.none);
            cardView.SetLiveMarkAsActive(communityData.isLive);
            cardView.ConfigureImageController(webRequestController);
            cardView.SetCommunityThumbnail(communityData.thumbnails[0]);
            cardView.SetJoiningLoadingActive(false);

            // Setup card events
            cardView.MainButtonClicked -= OpenCommunityProfile;
            cardView.MainButtonClicked += OpenCommunityProfile;
            cardView.ViewCommunityButtonClicked -= OpenCommunityProfile;
            cardView.ViewCommunityButtonClicked += OpenCommunityProfile;
            cardView.JoinCommunityButtonClicked -= JoinCommunity;
            cardView.JoinCommunityButtonClicked += JoinCommunity;

            // Setup mutual friends
            cardView.SetupMutualFriends(viewDependencies, communityData);

            return gridItem;
        }

        private async UniTaskVoid LoadMyCommunitiesAsync(CancellationToken ct)
        {
            currentMyCommunities.Clear();
            view.SetMyCommunitiesLoopListItemCount(0, false);
            view.SetMyCommunitiesAsLoading(true);

            var ownProfile = await selfProfile.ProfileAsync(ct);
            if (ownProfile == null)
                return;

            var userCommunitiesResponse = await dataProvider.GetUserCommunitiesAsync(
                userId: ownProfile.UserId,
                name: string.Empty,
                memberRolesIncluded: rolesIncludedForMyCommunities,
                pageNumber: 1,
                elementsPerPage: 1000,
                ct: ct);

            currentMyCommunities.AddRange(userCommunitiesResponse.communities);

            view.SetMyCommunitiesAsLoading(false);
            view.SetMyCommunitiesLoopListItemCount(currentMyCommunities.Count);
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
                memberRolesIncluded: rolesIncludedForMyCommunities,
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
                memberRolesIncluded: rolesIncludedForGenericSearch,
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
                view.GetResultsLoopGridVerticalNormalizedPosition() > NORMALIZED_V_POSITION_OFFSET_FOR_LOADING_MORE)
                return;

            loadResultsCts = loadResultsCts.SafeRestart();
            LoadResultsAsync(
                name: currentNameFilter,
                memberRolesIncluded: currentMemberRolesIncluded.ToArray(),
                pageNumber: currentPageNumberFilter + 1,
                elementsPerPage: COMMUNITIES_PER_PAGE,
                ct: loadResultsCts.Token).Forget();
        }

        private async UniTaskVoid LoadResultsAsync(string name, CommunityMemberRole[] memberRolesIncluded, int pageNumber, int elementsPerPage, CancellationToken ct)
        {
            isGridResultsLoadingItems = true;

            if (pageNumber == 1)
            {
                currentResults.Clear();
                view.SetResultsLoopGridItemCount(0, false);
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
                currentResults.AddRange(userCommunitiesResponse.communities);
            }

            currentResultsTotalAmount = userCommunitiesResponse.totalAmount;

            if (pageNumber == 1)
            {
                view.SetResultsAsLoading(false);
                view.SetResultsAsEmpty(currentResults.Count == 0);
            }

            view.SetResultsLoadingMoreActive(false);
            view.SetResultsCountText(currentResultsTotalAmount);
            view.SetResultsLoopGridItemCount(currentResults.Count, resetPos: pageNumber == 1);

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
                    memberRolesIncluded: rolesIncludedForGenericSearch,
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

        private void JoinCommunity(int index, CommunityResultCardView cardView) =>
            JoinCommunityAsync(index, cardView, CancellationToken.None).Forget();

        private async UniTaskVoid JoinCommunityAsync(int index, CommunityResultCardView cardView, CancellationToken ct)
        {
            cardView.SetJoiningLoadingActive(true);
            bool joinedSuccess = await dataProvider.JoinCommunityAsync(currentResults[index].id, ct);
            if (joinedSuccess)
            {
                currentResults[index].role = CommunityMemberRole.member;
                currentResults[index].memberCount++;
                currentMyCommunities.Add(currentResults[index]);

                view.RefreshResultsLoopGridItemByItemIndex(index);
                view.SetMyCommunitiesLoopListItemCount(currentMyCommunities.Count, false);
                view.SetMyCommunitiesAsEmpty(currentMyCommunities.Count == 0);
            }
        }

        private void OpenCommunityProfile(string communityId)
        {
            // TODO: Open community profile (currently implemented by Lorenzo)
        }
    }
}
