using DCL.Communities.CommunitiesDataProvider.DTOs;
using DCL.UI;
using DCL.UI.Profiles.Helpers;
using DCL.UI.Utilities;
using DCL.VoiceChat;
using SuperScrollView;
using System;
using System.Collections.Generic;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using CommunityData = DCL.Communities.CommunitiesDataProvider.DTOs.GetUserCommunitiesData.CommunityData;

namespace DCL.Communities.CommunitiesBrowser
{
    public class FilteredCommunitiesView : MonoBehaviour
    {
        public event Action? BackButtonClicked;
        public event Action<string>? CommunityProfileOpened;
        public event Action<string>? CommunityJoined;
        public event Action<string>? RequestedToJoinCommunity;
        public event Action<string>? JoinStreamClicked;
        public event Action<string, string>? RequestToJoinCommunityCanceled;
        public event Action<string>? GoToStreamClicked;

        [SerializeField] private Button resultsBackButton = null!;
        [SerializeField] private TMP_Text resultsTitleText = null!;
        [SerializeField] private TMP_Text resultsCountText = null!;
        [SerializeField] private GameObject resultsSection = null!;
        [SerializeField] private LoopGridView resultLoopGrid = null!;
        [SerializeField] private GameObject resultsEmptyContainer = null!;
        [SerializeField] private SkeletonLoadingView resultsLoadingSpinner = null!;
        [SerializeField] private GameObject resultsLoadingMoreSpinner = null!;
        [SerializeField] private Sprite defaultThumbnailSprite = null!;

        private readonly List<string> currentFilteredIds = new ();
        private CommunitiesBrowserStateService? browserStateService;
        private ICommunityCallOrchestrator? communityVoiceChatOrchestrator;

        private ProfileRepositoryWrapper? profileRepositoryWrapper;
        private ThumbnailLoader? thumbnailLoader;
        private ActiveViewSection activeViewSection;

        private readonly CancellationTokenSource thumbnailLoadingCts = new ();

        public int CurrentResultsCount => currentFilteredIds.Count;

        private void Awake()
        {
            resultsBackButton.onClick.AddListener(OnResultsBackButtonClicked);

            return;

            void OnResultsBackButtonClicked()
            {
                BackButtonClicked?.Invoke();
            }
        }

        public void SetDependencies(ThumbnailLoader newThumbnailLoader, CommunitiesBrowserStateService communitiesBrowserStateService, ICommunityCallOrchestrator orchestrator)
        {
            browserStateService = communitiesBrowserStateService;
            thumbnailLoader = newThumbnailLoader;
            communityVoiceChatOrchestrator = orchestrator;
        }

        public void SetActiveViewSection(ActiveViewSection newActiveSection)
        {
            this.activeViewSection = newActiveSection;
        }

        public void SetProfileRepositoryWrapper(ProfileRepositoryWrapper profileDataProvider)
        {
            profileRepositoryWrapper = profileDataProvider;
        }

        public void InitializeResultsGrid()
        {
            resultLoopGrid.InitGridView(0, SetupCommunityResultCardByIndex);
            resultLoopGrid.gameObject.GetComponent<ScrollRect>()?.SetScrollSensitivityBasedOnPlatform();
        }

        public void ClearResultsItems()
        {
            currentFilteredIds.Clear();
            resultLoopGrid.SetListItemCount(0, false);
            SetResultsAsEmpty(true);
        }

        public void AddResultsItems(CommunityData[] communities, bool resetPos)
        {
            foreach (CommunityData communityData in communities)
                currentFilteredIds.Add(communityData.id);

            resultLoopGrid.SetListItemCount(currentFilteredIds.Count, resetPos);

            SetResultsAsEmpty(currentFilteredIds.Count == 0);

            if (resetPos)
                resultLoopGrid.ScrollRect.verticalNormalizedPosition = 1f;
        }

        public void SetAsLoading(bool isLoading)
        {
            if (isLoading)
            {
                resultsCountText.text = string.Empty;
                resultsLoadingSpinner.ShowLoading();
            }
            else
                resultsLoadingSpinner.HideLoading();
        }

        public void SetResultsBackButtonVisible(bool isVisible)
        {
            resultsBackButton.gameObject.SetActive(isVisible);
        }

        public void SetResultsTitleText(string text)
        {
            resultsTitleText.text = text;
        }

        public void SetResultsCountText(int count)
        {
            resultsCountText.text = $"({count})";
        }

        public void SetResultsLoadingMoreActive(bool isActive)
        {
            resultsLoadingMoreSpinner.SetActive(isActive);
        }

        public void UpdateJoinedCommunity(string communityId, bool isSuccess)
        {
            RefreshCommunityCardInGrid(communityId);
        }

        public void RemoveOneMemberFromCounter(string communityId)
        {
            RefreshCommunityCardInGrid(communityId);
        }

        private void RefreshCommunityCardInGrid(string communityId)
        {
            for (var i = 0; i < currentFilteredIds.Count; i++)
            {
                string id = currentFilteredIds[i];
                if (id != communityId) continue;
                resultLoopGrid.RefreshItemByItemIndex(i);
                break;
            }
        }

        private void SetResultsAsEmpty(bool isEmpty)
        {
            resultsEmptyContainer.SetActive(isEmpty);
            resultLoopGrid.gameObject.SetActive(!isEmpty);
        }

        private LoopGridViewItem SetupCommunityResultCardByIndex(LoopGridView loopGridView, int index, int row, int column)
        {
            CommunityData communityData = browserStateService!.GetCommunityDataById(currentFilteredIds[index]);
            LoopGridViewItem gridItem = loopGridView.NewListViewItem(loopGridView.ItemPrefabDataList[0].mItemPrefab.name);
            CommunityResultCardView cardView = gridItem.GetComponent<CommunityResultCardView>();

            // Setup card data
            cardView.SetCommunityId(communityData.id);
            cardView.SetTitle(communityData.name);
            cardView.SetOwner(communityData.ownerName);
            cardView.SetDescription(communityData.description);
            cardView.SetPrivacy(communityData.privacy);
            cardView.SetMembersCount(communityData.membersCount);
            cardView.SetInviteOrRequestId(communityData.inviteOrRequestId);
            var isStreaming = false;
            var hasJoined = false;

            if (activeViewSection == ActiveViewSection.STREAMING)
            {
                isStreaming = true;
                hasJoined = browserStateService.CurrentCommunityId.Value == communityData.id;
            }

            cardView.SetActionButtonsState(communityData.privacy, communityData.pendingActionType, communityData.role != CommunityMemberRole.none, isStreaming, hasJoined);
            thumbnailLoader!.LoadCommunityThumbnailAsync(communityData.thumbnails?.raw, cardView.communityThumbnail, defaultThumbnailSprite, thumbnailLoadingCts.Token).Forget();
            cardView.SetActionLoadingActive(false);

            if (communityVoiceChatOrchestrator?.CurrentCommunityId.Value == communityData.id)
                cardView.ConfigureListeningTooltip();
            else
                cardView.ConfigureListenersCount(communityData.voiceChatStatus.isActive, communityData.voiceChatStatus.participantCount);

            // Setup card events
            cardView.MainButtonClicked -= OnCommunityProfileOpened;
            cardView.MainButtonClicked += OnCommunityProfileOpened;
            cardView.ViewCommunityButtonClicked -= OnCommunityProfileOpened;
            cardView.ViewCommunityButtonClicked += OnCommunityProfileOpened;
            cardView.JoinCommunityButtonClicked -= OnCommunityJoined;
            cardView.JoinCommunityButtonClicked += OnCommunityJoined;
            cardView.RequestToJoinCommunityButtonClicked -= OnCommunityRequestedToJoin;
            cardView.RequestToJoinCommunityButtonClicked += OnCommunityRequestedToJoin;
            cardView.CancelRequestToJoinCommunityButtonClicked -= OnCommunityRequestToJoinCanceled;
            cardView.CancelRequestToJoinCommunityButtonClicked += OnCommunityRequestToJoinCanceled;
            cardView.JoinStreamButtonClicked -= OnJoinStreamClicked;
            cardView.JoinStreamButtonClicked += OnJoinStreamClicked;
            cardView.GoToStreamButtonClicked -= OnGoToStreamClicked;
            cardView.GoToStreamButtonClicked += OnGoToStreamClicked;

            // Setup mutual friends
            if (profileRepositoryWrapper != null)
                cardView.SetupMutualFriends(profileRepositoryWrapper, communityData);

            return gridItem;
        }

        private void OnGoToStreamClicked(string communityId)
        {
            GoToStreamClicked?.Invoke(communityId);
        }

        private void OnJoinStreamClicked(string communityId)
        {
            JoinStreamClicked?.Invoke(communityId);
        }

        private void OnCommunityRequestedToJoin(string communityId, CommunityResultCardView cardView)
        {
            cardView.SetActionLoadingActive(true);
            RequestedToJoinCommunity?.Invoke(communityId);
        }

        private void OnCommunityRequestToJoinCanceled(string communityId, string requestId, CommunityResultCardView cardView)
        {
            cardView.SetActionLoadingActive(true);
            RequestToJoinCommunityCanceled?.Invoke(communityId, requestId);
        }

        private void OnCommunityProfileOpened(string communityId)
        {
            CommunityProfileOpened?.Invoke(communityId);
        }

        private void OnCommunityJoined(string communityId, CommunityResultCardView cardView)
        {
            cardView.SetActionLoadingActive(true);
            CommunityJoined?.Invoke(communityId);
        }


        public enum ActiveViewSection
        {
            STREAMING,
            ALL_COMMUNITIES,
            SEARCH_COMMUNITIES,
            MY_COMMUNITIES
        }
    }
}
