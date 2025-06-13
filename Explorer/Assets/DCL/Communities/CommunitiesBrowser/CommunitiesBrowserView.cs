using DCL.UI;
using DCL.UI.Profiles.Helpers;
using DCL.UI.Utilities;
using DCL.WebRequests;
using SuperScrollView;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using CommunityData = DCL.Communities.GetUserCommunitiesData.CommunityData;

namespace DCL.Communities.CommunitiesBrowser
{
    public class CommunitiesBrowserView : MonoBehaviour
    {
        private const float NORMALIZED_V_POSITION_OFFSET_FOR_LOADING_MORE = 0.01f;

        public event Action ViewAllMyCommunitiesButtonClicked;
        public event Action ResultsBackButtonClicked;
        public event Action<string> SearchBarSelected;
        public event Action<string> SearchBarDeselected;
        public event Action<string> SearchBarValueChanged;
        public event Action<string> SearchBarSubmit;
        public event Action SearchBarClearButtonClicked;
        public event Action<Vector2> ResultsLoopGridScrollChanged;
        public event Action<string> CommunityProfileOpened;
        public event Action<int, string> CommunityJoined;
        public event Action CreateCommunityButtonClicked;

        public bool IsResultsScrollPositionAtBottom =>
            resultLoopGrid.ScrollRect.verticalNormalizedPosition <= NORMALIZED_V_POSITION_OFFSET_FOR_LOADING_MORE;

        public int CurrentResultsCount => currentResults.Count;

        [Header("Animators")]
        [SerializeField] private Animator panelAnimator;
        [SerializeField] private Animator headerAnimator;

        [Header("Search")]
        [SerializeField] private SearchBarView searchBar;

        [Header("Creation Section")]
        [SerializeField] private Button createCommunityButton;

        [Header("My Communities Section")]
        [SerializeField] private GameObject myCommunitiesSection;
        [SerializeField] private GameObject myCommunitiesMainContainer;
        [SerializeField] private GameObject myCommunitiesEmptyContainer;
        [SerializeField] private GameObject myCommunitiesLoadingSpinner;
        [SerializeField] private LoopListView2 myCommunitiesLoopList;
        [SerializeField] private Button myCommunitiesViewAllButton;

        [Header("Results Section")]
        [SerializeField] private Button resultsBackButton;
        [SerializeField] private TMP_Text resultsTitleText;
        [SerializeField] private TMP_Text resultsCountText;
        [SerializeField] private GameObject resultsSection;
        [SerializeField] private LoopGridView resultLoopGrid;
        [SerializeField] private GameObject resultsEmptyContainer;
        [SerializeField] private GameObject resultsLoadingSpinner;
        [SerializeField] private GameObject resultsLoadingMoreSpinner;

        private readonly List<CommunityData> currentMyCommunities = new ();
        private readonly List<CommunityData> currentResults = new ();
        private IWebRequestController webRequestController;
        private ProfileRepositoryWrapper profileRepositoryWrapper;

        private void Awake()
        {
            myCommunitiesViewAllButton.onClick.AddListener(() => ViewAllMyCommunitiesButtonClicked?.Invoke());
            resultsBackButton.onClick.AddListener(() => ResultsBackButtonClicked?.Invoke());
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
        }

        private void Start() =>
            resultLoopGrid.ScrollRect.onValueChanged.AddListener(pos => ResultsLoopGridScrollChanged?.Invoke(pos));

        private void OnDestroy()
        {
            myCommunitiesViewAllButton.onClick.RemoveAllListeners();
            resultsBackButton.onClick.RemoveAllListeners();
            searchBar.inputField.onSelect.RemoveAllListeners();
            searchBar.inputField.onDeselect.RemoveAllListeners();
            searchBar.inputField.onValueChanged.RemoveAllListeners();
            searchBar.inputField.onSubmit.RemoveAllListeners();
            searchBar.clearSearchButton.onClick.RemoveAllListeners();
            resultLoopGrid.ScrollRect.onValueChanged.RemoveAllListeners();
            createCommunityButton.onClick.RemoveAllListeners();
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
            myCommunitiesLoadingSpinner.SetActive(isLoading);
            myCommunitiesSection.SetActive(!isLoading);
        }

        public void SetResultsAsLoading(bool isLoading)
        {
            resultsLoadingSpinner.SetActive(isLoading);
            resultsSection.SetActive(!isLoading);

            if (isLoading)
                resultsCountText.text = string.Empty;
        }

        public void SetResultsBackButtonVisible(bool isVisible) =>
            resultsBackButton.gameObject.SetActive(isVisible);

        public void SetResultsTitleText(string text) =>
            resultsTitleText.text = text;

        public void SetResultsCountText(int count) =>
            resultsCountText.text = $"({count})";

        public void SetResultsLoadingMoreActive(bool isActive) =>
            resultsLoadingMoreSpinner.SetActive(isActive);

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

        public void InitializeMyCommunitiesList(int itemTotalCount, IWebRequestController webRequestCtrl)
        {
            myCommunitiesLoopList.InitListView(itemTotalCount, SetupMyCommunityCardByIndex);
            myCommunitiesLoopList.gameObject.GetComponent<ScrollRect>()?.SetScrollSensitivityBasedOnPlatform();
            webRequestController ??= webRequestCtrl;
        }

        public void ClearMyCommunitiesItems()
        {
            currentMyCommunities.Clear();
            myCommunitiesLoopList.SetListItemCount(0, false);
            SetMyCommunitiesAsEmpty(true);
        }

        public void AddMyCommunitiesItems(CommunityData[] communities, bool resetPos)
        {
            currentMyCommunities.AddRange(communities);
            myCommunitiesLoopList.SetListItemCount(currentMyCommunities.Count, resetPos);
            SetMyCommunitiesAsEmpty(currentMyCommunities.Count == 0);
            myCommunitiesLoopList.ScrollRect.verticalNormalizedPosition = 1f;
        }

        public void InitializeResultsGrid(int itemTotalCount, IWebRequestController webRequestCtrl, ProfileRepositoryWrapper profileDataProvider)
        {
            resultLoopGrid.InitGridView(itemTotalCount, SetupCommunityResultCardByIndex);
            resultLoopGrid.gameObject.GetComponent<ScrollRect>()?.SetScrollSensitivityBasedOnPlatform();
            webRequestController ??= webRequestCtrl;
            profileRepositoryWrapper = profileDataProvider;
        }

        public void ClearResultsItems()
        {
            currentResults.Clear();
            resultLoopGrid.SetListItemCount(0, false);
            SetResultsAsEmpty(true);
        }

        public void AddResultsItems(CommunityData[] communities, bool resetPos)
        {
            currentResults.AddRange(communities);
            resultLoopGrid.SetListItemCount(currentResults.Count, resetPos);
            SetResultsAsEmpty(currentResults.Count == 0);

            if (resetPos)
                resultLoopGrid.ScrollRect.verticalNormalizedPosition = 1f;
        }

        public void UpdateJoinedCommunity(int index, bool isSuccess)
        {
            if (isSuccess)
            {
                // Change the role and increment the members amount
                currentResults[index].role = CommunityMemberRole.member;
                currentResults[index].membersCount++;

                // Add the joined community to My Communities
                currentMyCommunities.Add(currentResults[index]);
                myCommunitiesLoopList.SetListItemCount(currentMyCommunities.Count, false);
                SetMyCommunitiesAsEmpty(currentMyCommunities.Count == 0);
            }

            resultLoopGrid.RefreshItemByItemIndex(index);
        }

        private void SetSearchBarClearButtonActive(bool isActive) =>
            searchBar.clearSearchButton.gameObject.SetActive(isActive);

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
            cardView.SetCommunityThumbnail(communityData.thumbnails?.raw);

            // Setup card events
            cardView.MainButtonClicked -= CommunityProfileOpened;
            cardView.MainButtonClicked += CommunityProfileOpened;

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
            cardView.SetDescription(communityData.description);
            cardView.SetPrivacy(communityData.privacy);
            cardView.SetMembersCount(communityData.membersCount);
            cardView.SetOwnership(communityData.role != CommunityMemberRole.none);
            cardView.SetLiveMarkAsActive(communityData.isLive);
            cardView.ConfigureImageController(webRequestController);
            cardView.SetCommunityThumbnail(communityData.thumbnails?.raw);
            cardView.SetJoiningLoadingActive(false);

            // Setup card events
            cardView.MainButtonClicked -= CommunityProfileOpened;
            cardView.MainButtonClicked += CommunityProfileOpened;
            cardView.ViewCommunityButtonClicked -= CommunityProfileOpened;
            cardView.ViewCommunityButtonClicked += CommunityProfileOpened;
            cardView.JoinCommunityButtonClicked -= OnCommunityJoined;
            cardView.JoinCommunityButtonClicked += OnCommunityJoined;

            // Setup mutual friends
            cardView.SetupMutualFriends(profileRepositoryWrapper, communityData);

            return gridItem;
        }

        private void SetMyCommunitiesAsEmpty(bool isEmpty)
        {
            myCommunitiesEmptyContainer.SetActive(isEmpty);
            myCommunitiesMainContainer.SetActive(!isEmpty);
        }

        private void SetResultsAsEmpty(bool isEmpty)
        {
            resultsEmptyContainer.SetActive(isEmpty);
            resultLoopGrid.gameObject.SetActive(!isEmpty);
        }

        private void OnCommunityJoined(int index, CommunityResultCardView cardView)
        {
            cardView.SetJoiningLoadingActive(true);
            CommunityJoined?.Invoke(index, currentResults[index].id);
        }
    }
}
