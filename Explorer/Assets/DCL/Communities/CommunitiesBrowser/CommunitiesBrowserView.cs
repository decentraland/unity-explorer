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
        public event Action<string>? JoinStream;

        private CommunitiesBrowserStateService browserStateService;

        public bool IsResultsScrollPositionAtBottom =>
            filteredCommunitiesView.IsResultsScrollPositionAtBottom(NORMALIZED_V_POSITION_OFFSET_FOR_LOADING_MORE);


        //This value will depend on which view is active? is it worth to have the logic centralized??
        public int CurrentResultsCount => 1;//browserStateService.GetFilteredResultsCount();

        public MyCommunitiesView MyCommunitiesView => myCommunitiesView;

        [Header("Sections")]
        [SerializeField] private GameObject filteredCommunitiesSection = null!;
        [SerializeField] private GameObject browseAllCommunitiesSection = null!;

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

        [Header("Filtered Results Section")]
        [SerializeField] private FilteredCommunitiesView filteredCommunitiesView = null!;

        [Header("Browse All Section")]
        [SerializeField] private BrowseAllCommunitiesView browseAllCommunitiesView = null!;

        [Header("Streaming Section")]
        [SerializeField] private StreamingCommunitiesView streamingCommunitiesView = null!;

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
            streamingCommunitiesView.JoinStream += communityId => JoinStream?.Invoke(communityId);

            browseAllCommunitiesView.CommunityProfileOpened += communityId => CommunityProfileOpened?.Invoke(communityId);
            browseAllCommunitiesView.CommunityJoined += (communityId, cardView) => CommunityJoined?.Invoke(communityId);

            filteredCommunitiesView.ResultsBackButtonClicked += () => ResultsBackButtonClicked?.Invoke();
            filteredCommunitiesView.CommunityProfileOpened += communityId => CommunityProfileOpened?.Invoke(communityId);
            filteredCommunitiesView.CommunityJoined += communityId => CommunityJoined?.Invoke(communityId);
        }

        private void Start() =>
            filteredCommunitiesView.ResultsLoopGridScrollChanged += pos => ResultsLoopGridScrollChanged?.Invoke(pos);

        private void OnDestroy()
        {
            searchBar.inputField.onSelect.RemoveAllListeners();
            searchBar.inputField.onDeselect.RemoveAllListeners();
            searchBar.inputField.onValueChanged.RemoveAllListeners();
            searchBar.inputField.onSubmit.RemoveAllListeners();
            searchBar.clearSearchButton.onClick.RemoveAllListeners();
            createCommunityButton.onClick.RemoveAllListeners();
            streamingCommunitiesView.JoinStream -= communityId => JoinStream?.Invoke(communityId);
            browseAllCommunitiesView.CommunityProfileOpened -= communityId => CommunityProfileOpened?.Invoke(communityId);
            browseAllCommunitiesView.CommunityJoined -= (communityId, cardView) => CommunityJoined?.Invoke(communityId);
            filteredCommunitiesView.ResultsBackButtonClicked -= () => ResultsBackButtonClicked?.Invoke();
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
            switch (currentSection)
            {
                case CommunitiesSections.BROWSE_ALL_COMMUNITIES:
                    SetBrowseAllAsLoading(isLoading);
                    break;
                case CommunitiesSections.FILTERED_COMMUNITIES:
                    filteredCommunitiesView.SetResultsAsLoading(isLoading);
                    break;
                default: throw new ArgumentOutOfRangeException();
            }
        }

        public void SetActiveSection(CommunitiesSections activeSection)
        {
            currentSection = activeSection;
            browseAllCommunitiesSection.SetActive(activeSection == CommunitiesSections.BROWSE_ALL_COMMUNITIES);
            filteredCommunitiesSection.SetActive(activeSection == CommunitiesSections.FILTERED_COMMUNITIES);
        }

        public void SetResultsBackButtonVisible(bool isVisible) =>
            filteredCommunitiesView.SetResultsBackButtonVisible(isVisible);

        public void SetResultsTitleText(string text) =>
            filteredCommunitiesView.SetResultsTitleText(text);

        public void SetResultsCountText(int count)
        {
            switch (currentSection)
            {
                case CommunitiesSections.BROWSE_ALL_COMMUNITIES:
                    browseAllCommunitiesView.SetBrowseAllCountText(count);
                    break;
                case CommunitiesSections.FILTERED_COMMUNITIES:
                    filteredCommunitiesView.SetResultsCountText(count);
                    break;
                default: throw new ArgumentOutOfRangeException();
            }
        }

        public void SetResultsLoadingMoreActive(bool isActive)
        {
            switch (currentSection)
            {
                case CommunitiesSections.BROWSE_ALL_COMMUNITIES:
                    browseAllCommunitiesView.SetBrowseAllLoadingMoreActive(isActive);
                    break;
                case CommunitiesSections.FILTERED_COMMUNITIES:
                    filteredCommunitiesView.SetResultsLoadingMoreActive(isActive);
                    break;
                default: throw new ArgumentOutOfRangeException();
            }
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
            browseAllCommunitiesView.SetProfileRepositoryWrapper(profileDataProvider);
        }

        public void ClearItems()
        {
            switch (currentSection)
            {
                case CommunitiesSections.FILTERED_COMMUNITIES:
                    filteredCommunitiesView.ClearResultsItems();
                    break;
                case CommunitiesSections.BROWSE_ALL_COMMUNITIES:
                    browseAllCommunitiesView.ClearBrowseAllItems();
                    break;
            }
        }

        public void AddItems(CommunityData[] communities, bool resetPos)
        {
            switch (currentSection)
            {
                case CommunitiesSections.FILTERED_COMMUNITIES:
                    filteredCommunitiesView.AddResultsItems(communities, resetPos);
                    break;
                case CommunitiesSections.BROWSE_ALL_COMMUNITIES:
                    browseAllCommunitiesView.AddBrowseAllItems(communities, resetPos);
                    break;
            }
        }

        public void UpdateJoinedCommunity(string communityId, bool isJoined, bool isSuccess)
        {
            browserStateService.UpdateJoinedCommunity(communityId, isJoined, isSuccess);
            filteredCommunitiesView.UpdateJoinedCommunity(communityId, isSuccess);
            browseAllCommunitiesView.UpdateJoinedCommunity(communityId, isSuccess);
        }

        public void RemoveOneMemberFromCounter(string communityId)
        {
            browserStateService.RemoveOneMemberFromCounter(communityId);
            filteredCommunitiesView.RemoveOneMemberFromCounter(communityId);
            browseAllCommunitiesView.RemoveOneMemberFromCounter(communityId);
        }

        private void SetSearchBarClearButtonActive(bool isActive) =>
            searchBar.clearSearchButton.gameObject.SetActive(isActive);


        private bool streamingIsLoading;
        private bool browseAllIsLoading;

        public void SetThumbnailLoader(ThumbnailLoader newThumbnailLoader)
        {
            streamingCommunitiesView.SetThumbnailLoader(newThumbnailLoader, defaultThumbnailSprite);
            browseAllCommunitiesView.SetThumbnailLoader(newThumbnailLoader, defaultThumbnailSprite);
            filteredCommunitiesView.SetThumbnailLoader(newThumbnailLoader, defaultThumbnailSprite);
        }

        public void InitializeStreamingResultsGrid(int itemTotalCount)
        {
            streamingCommunitiesView.InitializeStreamingResultsGrid(itemTotalCount);
        }

        public void AddStreamingResultsItems(CommunityData[] dataResults)
        {
            browserStateService.AddCommunities(dataResults);
            streamingCommunitiesView.AddStreamingResultsItems(dataResults);
        }

        public void ClearStreamingResultsItems()
        {
            streamingCommunitiesView.ClearStreamingResultsItems();
        }

        public void SetStreamingResultsAsLoading(bool isLoading)
        {
            streamingIsLoading = isLoading;
            if (isLoading)
                streamingCommunitiesView.SetStreamingResultsAsLoading(isLoading);
            else
                TryDisableLoading();
        }

        public void InitializeBrowseAllGrid(int itemTotalCount)
        {
            browseAllCommunitiesView.InitializeBrowseAllGrid(itemTotalCount);
        }

        private void SetBrowseAllAsLoading(bool isLoading)
        {
            browseAllIsLoading = isLoading;

            if (isLoading)
                browseAllCommunitiesView.SetBrowseAllAsLoading(isLoading);
            else
                TryDisableLoading();
        }

        private void TryDisableLoading()
        {
            if (streamingIsLoading || browseAllIsLoading) return;

            streamingCommunitiesView.SetStreamingResultsAsLoading(false);
            browseAllCommunitiesView.SetBrowseAllAsLoading(false);
        }

        public void SetCommunitiesBrowserState(CommunitiesBrowserStateService communitiesBrowserStateService)
        {
            browserStateService = communitiesBrowserStateService;
            streamingCommunitiesView.SetCommunitiesBrowserState(browserStateService);
            browseAllCommunitiesView.SetCommunitiesBrowserState(browserStateService);
            filteredCommunitiesView.SetCommunitiesBrowserState(browserStateService);
        }
    }
}
