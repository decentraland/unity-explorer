using DCL.UI;
using DCL.UI.Profiles.Helpers;
using DCL.UI.Utilities;
using SuperScrollView;
using System;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using CommunityData = DCL.Communities.GetUserCommunitiesData.CommunityData;

namespace DCL.Communities.CommunitiesBrowser
{
    public class CommunitiesBrowserView : MonoBehaviour
    {
        private const float NORMALIZED_V_POSITION_OFFSET_FOR_LOADING_MORE = 0.01f;

        public event Action? ViewAllMyCommunitiesButtonClicked;
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


        public bool IsResultsScrollPositionAtBottom =>
            filteredCommunitiesView.IsResultsScrollPositionAtBottom(NORMALIZED_V_POSITION_OFFSET_FOR_LOADING_MORE);

        public int CurrentResultsCount => browserStateService.GetFilteredResultsCount();

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
        [SerializeField] private GameObject myCommunitiesSection = null!;
        [SerializeField] private GameObject myCommunitiesMainContainer = null!;
        [SerializeField] private GameObject myCommunitiesEmptyContainer = null!;
        [SerializeField] private SkeletonLoadingView myCommunitiesLoadingSpinner = null!;
        [SerializeField] private LoopListView2 myCommunitiesLoopList = null!;
        [SerializeField] private Button myCommunitiesViewAllButton = null!;
        [SerializeField] private Sprite defaultThumbnailSprite = null!;

        [Header("Filtered Results Section")]
        [SerializeField] private FilteredCommunitiesView filteredCommunitiesView = null!;

        [Header("Browse All Section")]
        [SerializeField] private BrowseAllCommunitiesView browseAllCommunitiesView = null!;

        [Header("Streaming Section")]
        [SerializeField] private StreamingCommunitiesView streamingCommunitiesView = null!;

        private readonly CommunitiesBrowserStateService browserStateService = new();

        private ThumbnailLoader? thumbnailLoader;
        private CommunitiesSections currentSection = CommunitiesSections.BROWSE_ALL_COMMUNITIES;

        private void Awake()
        {
            myCommunitiesViewAllButton.onClick.AddListener(() => ViewAllMyCommunitiesButtonClicked?.Invoke());
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
            myCommunitiesViewAllButton.onClick.RemoveAllListeners();
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

        public void SetMyCommunitiesAsLoading(bool isLoading)
        {
            if (isLoading)
                myCommunitiesLoadingSpinner.ShowLoading();
            else
                myCommunitiesLoadingSpinner.HideLoading();
        }

        public void SetResultsAsLoading(bool isLoading)
        {
            switch (currentSection)
            {
                case CommunitiesSections.BROWSE_ALL_COMMUNITIES:
                    browseAllCommunitiesView.SetBrowseAllAsLoading(isLoading);
                    break;
                case CommunitiesSections.FILTERED_COMMUNITIES:
                    filteredCommunitiesView.SetResultsAsLoading(isLoading);
                    break;
                default: throw new ArgumentOutOfRangeException();
            }
        }

        public void SetActiveSection(CommunitiesSections activeSection)
        {
            browseAllCommunitiesSection.SetActive(activeSection == CommunitiesSections.BROWSE_ALL_COMMUNITIES);
            filteredCommunitiesSection.SetActive(activeSection == CommunitiesSections.FILTERED_COMMUNITIES);
        }

        public void SetResultsBackButtonVisible(bool isVisible) =>
            filteredCommunitiesView.SetResultsBackButtonVisible(isVisible);

        public void SetResultsTitleText(string text) =>
            filteredCommunitiesView.SetResultsTitleText(text);

        public void SetResultsCountText(int count) =>
            filteredCommunitiesView.SetResultsCountText(count);

        public void SetResultsLoadingMoreActive(bool isActive) =>
            filteredCommunitiesView.SetResultsLoadingMoreActive(isActive);

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

        public void InitializeMyCommunitiesList(int itemTotalCount, ISpriteCache thumbnailCache)
        {
            myCommunitiesLoopList.InitListView(itemTotalCount, SetupMyCommunityCardByIndex);
            myCommunitiesLoopList.gameObject.GetComponent<ScrollRect>()?.SetScrollSensitivityBasedOnPlatform();
        }

        public void ClearMyCommunitiesItems()
        {
            browserStateService.ClearMyCommunities();
            myCommunitiesLoopList.SetListItemCount(0, false);
            SetMyCommunitiesAsEmpty(true);
        }

        public void AddMyCommunitiesItems(CommunityData[] communities, bool resetPos)
        {
            browserStateService.AddMyCommunities(communities);
            myCommunitiesLoopList.SetListItemCount(browserStateService.GetMyCommunitiesCount(), resetPos);
            SetMyCommunitiesAsEmpty(browserStateService.GetMyCommunitiesCount() == 0);
            myCommunitiesLoopList.ScrollRect.verticalNormalizedPosition = 1f;
        }

        public void InitializeResultsGrid(int itemTotalCount, ProfileRepositoryWrapper profileDataProvider, ISpriteCache thumbnailCache)
        {
            filteredCommunitiesView.InitializeResultsGrid(itemTotalCount);
            filteredCommunitiesView.SetProfileRepositoryWrapper(profileDataProvider);
            browseAllCommunitiesView.SetProfileRepositoryWrapper(profileDataProvider);
        }

        public void ClearResultsItems()
        {
            browserStateService.ClearFilteredResults();
            filteredCommunitiesView.ClearResultsItems();
        }

        public void AddResultsItems(CommunityData[] communities, bool resetPos)
        {
            browserStateService.AddFilteredResults(communities);
            filteredCommunitiesView.AddResultsItems(communities, resetPos);
        }

        public void UpdateJoinedCommunity(string communityId, bool isJoined, bool isSuccess)
        {
            browserStateService.UpdateJoinedCommunity(communityId, isJoined, isSuccess);

            if (isSuccess)
            {
                // Update UI for My Communities
                myCommunitiesLoopList.SetListItemCount(browserStateService.GetMyCommunitiesCount(), false);
                SetMyCommunitiesAsEmpty(browserStateService.GetMyCommunitiesCount() == 0);
            }

            filteredCommunitiesView.UpdateJoinedCommunity(communityId, isJoined, isSuccess);
            browseAllCommunitiesView.UpdateJoinedCommunity(communityId, isJoined, isSuccess);
            streamingCommunitiesView.UpdateJoinedCommunity(communityId, isJoined, isSuccess);
        }

        public void RemoveOneMemberFromCounter(string communityId)
        {
            browserStateService.RemoveOneMemberFromCounter(communityId);

            filteredCommunitiesView.RemoveOneMemberFromCounter(communityId);
            browseAllCommunitiesView.RemoveOneMemberFromCounter(communityId);
            streamingCommunitiesView.RemoveOneMemberFromCounter(communityId);
        }

        private void SetSearchBarClearButtonActive(bool isActive) =>
            searchBar.clearSearchButton.gameObject.SetActive(isActive);

        private CancellationTokenSource myCommunityThumbnailsLoadingCts = new();
        private bool streamingIsLoading;
        private bool browseAllIsLoading;

        private LoopListViewItem2 SetupMyCommunityCardByIndex(LoopListView2 loopListView, int index)
        {
            CommunityData communityData = browserStateService.MyCommunities[index];
            LoopListViewItem2 listItem = loopListView.NewListViewItem(loopListView.ItemPrefabDataList[0].mItemPrefab.name);
            MyCommunityCardView cardView = listItem.GetComponent<MyCommunityCardView>();

            // Setup card data
            cardView.SetCommunityId(communityData.id);
            cardView.SetTitle(communityData.name);
            cardView.SetUserRole(communityData.role);
            cardView.ConfigureListenersCount(communityData.voiceChatStatus.isActive, communityData.voiceChatStatus.participantCount);

            thumbnailLoader!.LoadCommunityThumbnailAsync(communityData.thumbnails?.raw, cardView.communityThumbnail, defaultThumbnailSprite, myCommunityThumbnailsLoadingCts.Token).Forget();

            // Setup card events
            cardView.MainButtonClicked -= CommunityProfileOpened;
            cardView.MainButtonClicked += CommunityProfileOpened;

            return listItem;
        }

        private void SetMyCommunitiesAsEmpty(bool isEmpty)
        {
            myCommunitiesEmptyContainer.SetActive(isEmpty);
            myCommunitiesMainContainer.SetActive(!isEmpty);
        }

        public void SetThumbnailLoader(ThumbnailLoader newThumbnailLoader)
        {
            this.thumbnailLoader = newThumbnailLoader;
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
            browserStateService.AddStreamingResults(dataResults);
            streamingCommunitiesView.AddStreamingResultsItems(dataResults);
        }

        public void ClearStreamingResultsItems()
        {
            browserStateService.ClearStreamingResults();
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

        public void ClearBrowseAllItems()
        {
            browserStateService.ClearBrowseAllResults();
            browseAllCommunitiesView.ClearBrowseAllItems();
        }

        public void AddBrowseAllItems(CommunityData[] communities, bool resetPos)
        {
            browserStateService.AddBrowseAllResults(communities);
            browseAllCommunitiesView.AddBrowseAllItems(communities, resetPos);
        }

        public void SetBrowseAllAsLoading(bool isLoading)
        {
            browseAllIsLoading = isLoading;

            if (isLoading)
                browseAllCommunitiesView.SetBrowseAllAsLoading(isLoading);
            else
                TryDisableLoading();
        }

        public void SetBrowseAllTitleText(string text)
        {
            browseAllCommunitiesView.SetBrowseAllTitleText(text);
        }

        public void SetBrowseAllCountText(int count)
        {
            browseAllCommunitiesView.SetBrowseAllCountText(count);
        }

        public void SetBrowseAllLoadingMoreActive(bool isActive)
        {
            browseAllCommunitiesView.SetBrowseAllLoadingMoreActive(isActive);
        }

        private void TryDisableLoading()
        {
            if (streamingIsLoading || browseAllIsLoading) return;

            streamingCommunitiesView.SetStreamingResultsAsLoading(false);
            browseAllCommunitiesView.SetBrowseAllAsLoading(false);
            filteredCommunitiesView.SetResultsAsLoading(false);
        }
    }
}
