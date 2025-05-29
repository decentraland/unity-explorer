using Cysharp.Threading.Tasks;
using DCL.Input;
using DCL.Input.Component;
using DCL.Profiles.Self;
using DCL.UI;
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
        private const string MY_GENERAL_RESULTS_TITLE = "Decentraland Communities";
        private const int SEARCH_AWAIT_TIME = 1000;
        private const string SEARCH_RESULTS_TITLE_FORMAT = "Results for '{0}'";

        private readonly CommunitiesBrowserView view;
        private readonly RectTransform rectTransform;
        private readonly ICursor cursor;
        private readonly ICommunitiesDataProvider dataProvider;
        private readonly ISelfProfile selfProfile;
        private readonly IWebRequestController webRequestController;
        private readonly IInputBlock inputBlock;
        private readonly ViewDependencies viewDependencies;
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

            view.AddMyCommunitiesItems(userCommunitiesResponse.communities, true);
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
                view.CurrentResultsCount >= currentResultsTotalAmount ||
                !view.IsResultsScrollPositionAtBottom)
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
                view.ClearResultsItems();
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
                view.AddResultsItems(userCommunitiesResponse.communities, pageNumber == 1);
            }

            currentResultsTotalAmount = userCommunitiesResponse.totalAmount;

            if (pageNumber == 1)
                view.SetResultsAsLoading(false);

            view.SetResultsLoadingMoreActive(false);
            view.SetResultsCountText(currentResultsTotalAmount);

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

        private void JoinCommunity(int index, string communityId) =>
            JoinCommunityAsync(index, communityId, CancellationToken.None).Forget();

        private async UniTaskVoid JoinCommunityAsync(int index, string communityId, CancellationToken ct)
        {
            bool joinedSuccess = await dataProvider.JoinCommunityAsync(communityId, ct);
            if (joinedSuccess)
                view.SetResultCommunityAsJoined(index);
        }

        private void OpenCommunityProfile(string communityId)
        {
            // TODO: Open community profile (currently implemented by Lorenzo)
        }
    }
}
