using DCL.UI;
using DCL.UI.Profiles.Helpers;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using CommunityData = DCL.Communities.GetUserCommunitiesData.CommunityData;

namespace DCL.Communities.CommunitiesBrowser
{
    public class CommunitiesBrowserView : MonoBehaviour
    {
        private const float NORMALIZED_V_POSITION_OFFSET_FOR_LOADING_MORE = 0.01f;

        public event Action? ResultsBackButtonClicked;
        public event Action<string>? SearchBarSelected;
        public event Action<string>? SearchBarDeselected;
        public event Action<string>? SearchBarValueChanged;
        public event Action<string>? SearchBarSubmit;
        public event Action? SearchBarClearButtonClicked;
        public event Action<Vector2>? ResultsLoopGridScrollChanged;
        public event Action<string>? CommunityProfileOpened;
        public event Action<string>? CommunityJoined;
        public event Action? CreateCommunityButtonClicked;

        private CommunitiesBrowserStateService browserStateService;

        public bool IsResultsScrollPositionAtBottom =>
            filteredCommunitiesView.IsResultsScrollPositionAtBottom(NORMALIZED_V_POSITION_OFFSET_FOR_LOADING_MORE);


        //This value will depend on which view is active? is it worth to have the logic centralized??
        public int CurrentResultsCount => 1;//browserStateService.GetFilteredResultsCount();

        public MyCommunitiesView MyCommunitiesView => myCommunitiesView;
        public StreamingCommunitiesView StreamingCommunitiesView => streamingCommunitiesView;

        [Header("Animators")]
        [SerializeField] private Animator panelAnimator = null!;
        [SerializeField] private Animator headerAnimator = null!;

        [Header("Search")]
        [SerializeField] private SearchBarView searchBar = null!;

        [Header("Creation Section")]
        [SerializeField] private Button createCommunityButton = null!;

        [Header("My Communities Section")]
        [SerializeField] private MyCommunitiesView myCommunitiesView = null!;
        [SerializeField] private Sprite defaultThumbnailSprite = null!;

        [Header("Right Side Section")]
        [SerializeField] private FilteredCommunitiesView filteredCommunitiesView = null!;
        [SerializeField] private StreamingCommunitiesView streamingCommunitiesView = null!;
        [SerializeField] private ScrollRect scrollRect;

        private CommunitiesSections currentSection = CommunitiesSections.BROWSE_ALL_COMMUNITIES;

        private void Awake()
        {
            MyCommunitiesView.CommunityProfileOpened += communityId => CommunityProfileOpened?.Invoke(communityId);

            searchBar.inputField.onSelect.AddListener(text => SearchBarSelected?.Invoke(text));
            searchBar.inputField.onDeselect.AddListener(text => SearchBarDeselected?.Invoke(text));
            searchBar.inputField.onValueChanged.AddListener(text =>
            {
                SearchBarValueChanged?.Invoke(text);
                SetSearchBarClearButtonActive(!string.IsNullOrEmpty(text));
            });
            searchBar.inputField.onSubmit.AddListener(text => SearchBarSubmit?.Invoke(text));
            searchBar.clearSearchButton.onClick.AddListener(() => SearchBarClearButtonClicked?.Invoke());
            createCommunityButton.onClick.AddListener(() => CreateCommunityButtonClicked?.Invoke());

            filteredCommunitiesView.BackButtonClicked += () => ResultsBackButtonClicked?.Invoke();
            filteredCommunitiesView.CommunityProfileOpened += communityId => CommunityProfileOpened?.Invoke(communityId);
            filteredCommunitiesView.CommunityJoined += communityId => CommunityJoined?.Invoke(communityId);

            scrollRect.onValueChanged.AddListener(pos => ResultsLoopGridScrollChanged?.Invoke(pos));
        }

        private void OnDestroy()
        {
            searchBar.inputField.onSelect.RemoveAllListeners();
            searchBar.inputField.onDeselect.RemoveAllListeners();
            searchBar.inputField.onValueChanged.RemoveAllListeners();
            searchBar.inputField.onSubmit.RemoveAllListeners();
            searchBar.clearSearchButton.onClick.RemoveAllListeners();
            createCommunityButton.onClick.RemoveAllListeners();

            filteredCommunitiesView.CommunityProfileOpened -= communityId => CommunityProfileOpened?.Invoke(communityId);
            filteredCommunitiesView.CommunityJoined -= communityId => CommunityJoined?.Invoke(communityId);
        }

        public void SetViewActive(bool isActive) =>
            gameObject.SetActive(isActive);

        public void PlayAnimator(int triggerId)
        {
            panelAnimator.SetTrigger(triggerId);
            headerAnimator.SetTrigger(triggerId);
        }

        public void ResetAnimator()
        {
            panelAnimator.Rebind();
            headerAnimator.Rebind();
            panelAnimator.Update(0);
            headerAnimator.Update(0);
        }

        public void SetAsLoading(bool isLoading)
        {
            filteredCommunitiesView.SetAsLoading(isLoading);
        }

        public void SetActiveSection(CommunitiesSections activeSection)
        {
            currentSection = activeSection;
            if (activeSection == CommunitiesSections.FILTERED_COMMUNITIES)
                streamingCommunitiesView.ClearStreamingResultsItems();
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

        public void CleanSearchBar(bool raiseOnChangeEvent = true)
        {
            TMP_InputField.OnChangeEvent originalEvent = searchBar.inputField.onValueChanged;

            if (!raiseOnChangeEvent)
                searchBar.inputField.onValueChanged = new TMP_InputField.OnChangeEvent();

            searchBar.inputField.text = string.Empty;
            SetSearchBarClearButtonActive(false);

            if (!raiseOnChangeEvent)
                searchBar.inputField.onValueChanged = originalEvent;
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

        public void AddItems(CommunityData[] communities, bool resetPos)
        {
            filteredCommunitiesView.AddResultsItems(communities, resetPos);
        }

        public void UpdateJoinedCommunity(string communityId, bool isJoined, bool isSuccess)
        {
            browserStateService.UpdateJoinedCommunity(communityId, isJoined, isSuccess);
            filteredCommunitiesView.UpdateJoinedCommunity(communityId, isSuccess);
        }

        public void RemoveOneMemberFromCounter(string communityId)
        {
            browserStateService.RemoveOneMemberFromCounter(communityId);
            filteredCommunitiesView.RemoveOneMemberFromCounter(communityId);
        }

        private void SetSearchBarClearButtonActive(bool isActive) =>
            searchBar.clearSearchButton.gameObject.SetActive(isActive);


        public void SetThumbnailLoader(ThumbnailLoader newThumbnailLoader)
        {
            streamingCommunitiesView.SetThumbnailLoader(newThumbnailLoader, defaultThumbnailSprite);
            filteredCommunitiesView.SetThumbnailLoader(newThumbnailLoader, defaultThumbnailSprite);
        }

        public void InitializeStreamingResultsGrid(int itemTotalCount)
        {
            streamingCommunitiesView.InitializeStreamingResultsGrid(itemTotalCount);
        }

        public void AddStreamingResultsItems(CommunityData[] dataResults)
        {
            streamingCommunitiesView.AddStreamingResultsItems(dataResults);
        }

        public void ClearStreamingResultsItems()
        {
            streamingCommunitiesView.ClearStreamingResultsItems();
        }

        public void SetCommunitiesBrowserState(CommunitiesBrowserStateService communitiesBrowserStateService)
        {
            browserStateService = communitiesBrowserStateService;
            streamingCommunitiesView.SetCommunitiesBrowserState(browserStateService);
            filteredCommunitiesView.SetCommunitiesBrowserState(browserStateService);
        }
    }
}
