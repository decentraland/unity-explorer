using DCL.UI.Profiles.Helpers;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Communities.CommunitiesBrowser
{
    public class CommunitiesBrowserRightSectionView : MonoBehaviour
    {
        private const float NORMALIZED_V_POSITION_OFFSET_FOR_LOADING_MORE = 0.01f;
        public event Action? ResultsBackButtonClicked;
        public event Action? LoopGridScrollChanged;
        public event Action<string>? CommunityProfileOpened;
        public event Action<string>? CommunityJoined;

        [SerializeField] private FilteredCommunitiesView filteredCommunitiesView = null!;
        [SerializeField] private StreamingCommunitiesView streamingCommunitiesView = null!;
        [SerializeField] private ScrollRect scrollRect = null!;

        public bool IsResultsScrollPositionAtBottom =>
            scrollRect.verticalNormalizedPosition <= NORMALIZED_V_POSITION_OFFSET_FOR_LOADING_MORE;

        public int CurrentResultsCount => filteredCommunitiesView.CurrentResultsCount;
        public StreamingCommunitiesView StreamingCommunitiesView => streamingCommunitiesView;

        private void Awake()
        {
            scrollRect.onValueChanged.AddListener(OnScrollRectValueChanged);
            filteredCommunitiesView.BackButtonClicked += OnBackButtonClicked;
            filteredCommunitiesView.CommunityProfileOpened += OnCommunityJoined;
            filteredCommunitiesView.CommunityJoined += OnCommunityJoined;
        }

        private void OnScrollRectValueChanged(Vector2 _)
        {
            LoopGridScrollChanged?.Invoke();
        }

        private void OnBackButtonClicked()
        {
            ResultsBackButtonClicked?.Invoke();
        }

        public void SetThumbnailLoader(ThumbnailLoader newThumbnailLoader)
        {
            streamingCommunitiesView.SetThumbnailLoader(newThumbnailLoader);
            filteredCommunitiesView.SetThumbnailLoader(newThumbnailLoader);
        }

        public void InitializeStreamingResultsGrid(int itemTotalCount)
        {
            streamingCommunitiesView.InitializeStreamingResultsGrid(itemTotalCount);
        }

        public void SetCommunitiesBrowserState(CommunitiesBrowserStateService communitiesBrowserStateService)
        {
            streamingCommunitiesView.SetCommunitiesBrowserState(communitiesBrowserStateService);
            filteredCommunitiesView.SetCommunitiesBrowserState(communitiesBrowserStateService);
        }

        private void OnDestroy()
        {
            filteredCommunitiesView.CommunityProfileOpened -= OnCommunityProfileOpened;
            filteredCommunitiesView.CommunityJoined -= OnCommunityJoined;
        }

        private void OnCommunityJoined(string communityId)
        {
            CommunityJoined?.Invoke(communityId);
        }

        private void OnCommunityProfileOpened(string communityId)
        {
            CommunityProfileOpened?.Invoke(communityId);
        }

        public void SetAsLoading(bool isLoading)
        {
            filteredCommunitiesView.SetAsLoading(isLoading);
        }

        public void SetActiveSection(CommunitiesSections activeSection)
        {
            if (activeSection == CommunitiesSections.FILTERED_COMMUNITIES)
            {
                streamingCommunitiesView.ClearStreamingResultsItems();
                filteredCommunitiesView.SetResultsBackButtonVisible(true);
            }
            else
            {
                filteredCommunitiesView.SetResultsBackButtonVisible(false);
            }
        }

        public void SetResultsBackButtonVisible(bool isVisible) =>
            filteredCommunitiesView.SetResultsBackButtonVisible(isVisible);

        public void SetResultsTitleText(string text) =>
            filteredCommunitiesView.SetResultsTitleText(text);

        public void SetResultsCountText(int count)
        {
            filteredCommunitiesView.SetResultsCountText(count);
        }

        public void SetResultsLoadingMoreActive(bool isActive)
        {
            filteredCommunitiesView.SetResultsLoadingMoreActive(isActive);
        }

        public void InitializeResultsGrid(int itemTotalCount, ProfileRepositoryWrapper profileDataProvider)
        {
            filteredCommunitiesView.InitializeResultsGrid(itemTotalCount);
            filteredCommunitiesView.SetProfileRepositoryWrapper(profileDataProvider);
        }

        public void ClearItems()
        {
            filteredCommunitiesView.ClearResultsItems();
        }

        public void AddItems(GetUserCommunitiesData.CommunityData[] communities, bool resetPos)
        {
            filteredCommunitiesView.AddResultsItems(communities, resetPos);
        }

        public void UpdateJoinedCommunity(string communityId, bool isJoined, bool isSuccess)
        {
            filteredCommunitiesView.UpdateJoinedCommunity(communityId, isSuccess);
        }

        public void RemoveOneMemberFromCounter(string communityId)
        {
            filteredCommunitiesView.RemoveOneMemberFromCounter(communityId);
        }
    }
}
