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
        private const int SEARCH_AWAIT_TIME = 1000;
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
        private readonly ISelfProfile selfProfile;
        private readonly INftNamesProvider nftNamesProvider;
        private readonly ISpriteCache spriteCache;

        private readonly MyCommunitiesPresenter myCommunitiesPresenter;

        private CancellationTokenSource? searchCancellationCts;
        private CancellationTokenSource? showErrorCts;
        private CancellationTokenSource? openCommunityCreationCts;

        private bool isSectionActivated;
        private string currentSearchText = string.Empty;
        private readonly CommunitiesBrowserStateService browserStateService;
        private readonly CommunitiesBrowserRightSectionPresenter rightSectionPresenter;

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
            this.warningNotificationView = warningNotificationView;
            this.mvcManager = mvcManager;
            this.selfProfile = selfProfile;
            this.nftNamesProvider = nftNamesProvider;

            spriteCache = new SpriteCache(webRequestController);
            browserStateService = new CommunitiesBrowserStateService();

            var thumbnailLoader = new ThumbnailLoader(spriteCache);

            myCommunitiesPresenter = new MyCommunitiesPresenter(view.MyCommunitiesView, dataProvider, browserStateService, thumbnailLoader);
            myCommunitiesPresenter.ErrorLoadingMyCommunities += OnErrorLoadingMyCommunities;
            myCommunitiesPresenter.ViewAllMyCommunitiesButtonClicked += ViewAllMyCommunitiesResults;

            rightSectionPresenter = new CommunitiesBrowserRightSectionPresenter(view.RightSectionView, dataProvider,
                sharedSpaceManager, browserStateService, thumbnailLoader, profileDataProvider, orchestrator);

            rightSectionPresenter.ErrorLoadingStreamingCommunities += OnErrorLoadingStreamingCommunities;
            rightSectionPresenter.ClearSearchBar += ClearSearchBar;
            rightSectionPresenter.ErrorLoadingAllCommunities += OnErrorLoadingAllCommunities;
            rightSectionPresenter.CommunityProfileOpened += OpenCommunityProfile;
            rightSectionPresenter.CommunityJoined += JoinCommunity;

            view.SearchBarSelected += DisableShortcutsInput;
            view.SearchBarDeselected += RestoreInput;
            view.SearchBarValueChanged += SearchBarValueChanged;
            view.SearchBarSubmit += SearchBarSubmit;
            view.SearchBarClearButtonClicked += SearchBarCleared;
            view.CommunityProfileOpened += OpenCommunityProfile;
            view.CreateCommunityButtonClicked += CreateCommunity;
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
            showErrorCts?.SafeCancelAndDispose();
            openCommunityCreationCts?.SafeCancelAndDispose();
            spriteCache.Clear();
            myCommunitiesPresenter.Deactivate();
            rightSectionPresenter.Deactivate();

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
            view.SearchBarSelected -= DisableShortcutsInput;
            view.SearchBarDeselected -= RestoreInput;
            view.SearchBarValueChanged -= SearchBarValueChanged;
            view.SearchBarSubmit -= SearchBarSubmit;
            view.SearchBarClearButtonClicked -= SearchBarCleared;
            view.CommunityProfileOpened -= OpenCommunityProfile;
            view.CreateCommunityButtonClicked -= CreateCommunity;

            myCommunitiesPresenter.ErrorLoadingMyCommunities -= OnErrorLoadingMyCommunities;
            myCommunitiesPresenter.ViewAllMyCommunitiesButtonClicked -= ViewAllMyCommunitiesResults;

            UnsubscribeDataProviderEvents();

            browserStateService.Dispose();

            myCommunitiesPresenter.Dispose();
            searchCancellationCts?.SafeCancelAndDispose();
            showErrorCts?.SafeCancelAndDispose();
            openCommunityCreationCts?.SafeCancelAndDispose();
            spriteCache.Clear();
        }

        private void ViewAllMyCommunitiesResults()
        {
            rightSectionPresenter.ViewAllMyCommunitiesResults();
        }

        private void ReloadBrowser()
        {
            // Each time we open the Communities section, we load both my communities and all communities
            myCommunitiesPresenter.LoadMyCommunities();
            rightSectionPresenter.LoadAllCommunities();
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
                rightSectionPresenter.LoadAllCommunitiesResults();
            else
            {
                rightSectionPresenter.LoadSearchResults(searchText);
            }

            currentSearchText = searchText;
        }

        private void SearchBarCleared()
        {
            rightSectionPresenter.LoadAllCommunitiesResults();
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
                ShowErrorMessageAsync(JOIN_COMMUNITY_ERROR_MESSAGE).Forget();
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

        private void OnErrorLoadingMyCommunities()
        {
            ShowErrorMessageAsync(MY_COMMUNITIES_LOADING_ERROR_MESSAGE).Forget();
        }

        private void OnErrorLoadingStreamingCommunities()
        {
            ShowErrorMessageAsync(STREAMING_COMMUNITIES_LOADING_ERROR_MESSAGE).Forget();
        }

        private void OnErrorLoadingAllCommunities()
        {
            ShowErrorMessageAsync(ALL_COMMUNITIES_LOADING_ERROR_MESSAGE).Forget();
        }

        private async UniTaskVoid ShowErrorMessageAsync(string message)
        {
            showErrorCts = showErrorCts.SafeRestart();

            await warningNotificationView.AnimatedShowAsync(message, WARNING_MESSAGE_DELAY_MS, showErrorCts.Token)
                                         .SuppressToResultAsync(ReportCategory.COMMUNITIES);
        }

        private void OnCommunityUpdated(string _) =>
            ReloadBrowser();

        private void OnCommunityJoined(string communityId, bool success)
        {
            browserStateService.UpdateJoinedCommunity(communityId, true, success);
            myCommunitiesPresenter.UpdateJoinedCommunity(communityId, true, success);
            rightSectionPresenter.UpdateJoinedCommunity(communityId, true, success);
        }
        private void OnCommunityLeft(string communityId, bool success)
        {
            myCommunitiesPresenter.UpdateJoinedCommunity(communityId, false, success);
            rightSectionPresenter.UpdateJoinedCommunity(communityId, false, success);
        }

        private void OnCommunityCreated(CreateOrUpdateCommunityResponse.CommunityData newCommunity) =>
            ReloadBrowser();

        private void OnCommunityDeleted(string communityId) =>
            ReloadBrowser();

        private void OnUserRemovedFromCommunity(string communityId) =>
            rightSectionPresenter.OnUserRemovedFromCommunity(communityId);


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

    public enum CommunitiesSections
    {
        BROWSE_ALL_COMMUNITIES,
        FILTERED_COMMUNITIES
    }
}
