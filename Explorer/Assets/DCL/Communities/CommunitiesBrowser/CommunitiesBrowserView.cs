using DCL.Communities.CommunitiesDataProvider.DTOs;
using DCL.UI;
using DCL.UI.Profiles.Helpers;
using DCL.UI.Utilities;
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
    public class CommunitiesBrowserView : MonoBehaviour
    {
        public event Action<string>? SearchBarSelected;
        public event Action<string>? SearchBarDeselected;
        public event Action<string>? SearchBarValueChanged;
        public event Action<string>? SearchBarSubmit;
        public event Action? SearchBarClearButtonClicked;
        public event Action<string>? CommunityProfileOpened;
        public event Action<string>? CommunityJoined;
        public event Action<string>? CommunityRequestedToJoin;
        public event Action<string, string>? CommunityRequestToJoinCanceled;
        public event Action<string, string>? CommunityInvitationAccepted;
        public event Action<string, string>? CommunityInvitationRejected;
        public event Action? CreateCommunityButtonClicked;

        public MyCommunitiesView MyCommunitiesView => myCommunitiesView;
        public CommunitiesBrowserRightSectionView RightSectionView => rightSectionView;

        [Header("Animators")]
        [SerializeField] private Animator panelAnimator = null!;
        [SerializeField] private Animator headerAnimator = null!;

        [Header("Search")]
        [SerializeField] private SearchBarView searchBar = null!;

        [Header("Creation Section")]
        [SerializeField] private Button createCommunityButton = null!;

        [Header("My Communities Section")]
        [SerializeField] private MyCommunitiesView myCommunitiesView = null!;

        [Header("Right Side Section")]
        [SerializeField] private CommunitiesBrowserRightSectionView rightSectionView = null!;

        [Header("Results Section")]
        [SerializeField] private Button resultsBackButton = null!;
        [SerializeField] private TMP_Text resultsTitleText = null!;
        [SerializeField] private TMP_Text resultsCountText = null!;
        [SerializeField] private GameObject resultsSection = null!;
        [SerializeField] private LoopGridView resultLoopGrid = null!;
        [SerializeField] private GameObject resultsEmptyContainer = null!;
        [SerializeField] private SkeletonLoadingView resultsLoadingSpinner = null!;
        [SerializeField] private GameObject resultsLoadingMoreSpinner = null!;

        [Header("Invites & Requests Section")]
        [SerializeField] private CommunitiesInvitesAndRequestsView invitesAndRequestsView = null!;

        public CommunitiesInvitesAndRequestsView InvitesAndRequestsView => invitesAndRequestsView;

        private readonly List<CommunityData> currentMyCommunities = new ();
        private readonly List<CommunityData> currentResults = new ();
        private ProfileRepositoryWrapper? profileRepositoryWrapper;
        private ThumbnailLoader? thumbnailLoader;

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

            invitesAndRequestsView.CommunityProfileOpened += OnInvitesAndRequestsCommunityProfileOpened;
            invitesAndRequestsView.RequestToJoinCommunityCanceled += OnCommunityRequestToJoinCanceled;
            invitesAndRequestsView.CommunityInvitationAccepted += OnCommunityInvitationAccepted;
            invitesAndRequestsView.CommunityInvitationRejected += OnCommunityInvitationRejected;
        }

        private void OnDestroy()
        {
            searchBar.inputField.onSelect.RemoveAllListeners();
            searchBar.inputField.onDeselect.RemoveAllListeners();
            searchBar.inputField.onValueChanged.RemoveAllListeners();
            searchBar.inputField.onSubmit.RemoveAllListeners();
            searchBar.clearSearchButton.onClick.RemoveAllListeners();
            createCommunityButton.onClick.RemoveAllListeners();

            invitesAndRequestsView.CommunityProfileOpened -= OnInvitesAndRequestsCommunityProfileOpened;
            invitesAndRequestsView.RequestToJoinCommunityCanceled -= OnCommunityRequestToJoinCanceled;
            invitesAndRequestsView.CommunityInvitationAccepted -= OnCommunityInvitationAccepted;
            invitesAndRequestsView.CommunityInvitationRejected -= OnCommunityInvitationRejected;
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
            //if (isLoading)
                //myCommunitiesLoadingSpinner.ShowLoading();
            //else
                //myCommunitiesLoadingSpinner.HideLoading();
        }

        public void SetResultsAsLoading(bool isLoading)
        {
            if (isLoading)
            {
                resultsCountText.text = string.Empty;
                resultsLoadingSpinner.ShowLoading();
            }
            else
                resultsLoadingSpinner.HideLoading();
        }

        public void SetResultsBackButtonVisible(bool isVisible) =>
            resultsBackButton.gameObject.SetActive(isVisible);

        public void SetResultsTitleText(string text) =>
            resultsTitleText.text = text;

        public void SetResultsCountTextActive(bool isActive) =>
            resultsCountText.gameObject.SetActive(isActive);

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

        private void SetSearchBarClearButtonActive(bool isActive) =>
            searchBar.clearSearchButton.gameObject.SetActive(isActive);

        public void InitializeMyCommunitiesList(int itemTotalCount, ISpriteCache thumbnailCache)
        {
            //myCommunitiesLoopList.InitListView(itemTotalCount, SetupMyCommunityCardByIndex);
            //myCommunitiesLoopList.gameObject.GetComponent<ScrollRect>()?.SetScrollSensitivityBasedOnPlatform();
        }

        public void ClearMyCommunitiesItems()
        {
            currentMyCommunities.Clear();
         //   myCommunitiesLoopList.SetListItemCount(0, false);
            SetMyCommunitiesAsEmpty(true);
        }

        public void AddMyCommunitiesItems(CommunityData[] communities, bool resetPos)
        {
            currentMyCommunities.AddRange(communities);
            //myCommunitiesLoopList.SetListItemCount(currentMyCommunities.Count, resetPos);
            SetMyCommunitiesAsEmpty(currentMyCommunities.Count == 0);
            //myCommunitiesLoopList.ScrollRect.verticalNormalizedPosition = 1f;
        }

        public void InitializeResultsGrid(int itemTotalCount, ProfileRepositoryWrapper profileDataProvider, ISpriteCache thumbnailCache)
        {
            resultLoopGrid.InitGridView(itemTotalCount, SetupCommunityResultCardByIndex);
            resultLoopGrid.gameObject.GetComponent<ScrollRect>()?.SetScrollSensitivityBasedOnPlatform();
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

        public void UpdateJoinedCommunity(string communityId, bool isJoined, bool isSuccess)
        {
            if (isSuccess)
            {
                CommunityData? resultCommunityData = GetResultCommunityById(communityId);
                resultCommunityData?.SetAsJoined(isJoined);

                CommunityData? myCommunityData = GetMyCommunityById(communityId);
                if (!ReferenceEquals(myCommunityData, resultCommunityData))
                    myCommunityData?.SetAsJoined(isJoined);

                if (resultCommunityData != null && isJoined)
                    currentMyCommunities.Add(resultCommunityData);
                else if (myCommunityData != null)
                    currentMyCommunities.RemoveAll(c => c.id == myCommunityData.id);

                //myCommunitiesLoopList.SetListItemCount(currentMyCommunities.Count, false);
                SetMyCommunitiesAsEmpty(currentMyCommunities.Count == 0);
            }

            RefreshCommunityCardInGrid(communityId);
        }

        public void UpdateRequestedToJoinCommunity(string communityId, string? requestId, bool isRequestedToJoin, bool isSuccess, bool alreadyExistsInvitation)
        {
            if (isSuccess)
            {
                CommunityData? resultCommunityData = GetResultCommunityById(communityId);
                if (resultCommunityData != null)
                    resultCommunityData.inviteOrRequestId = requestId;

                if (!alreadyExistsInvitation)
                {
                    if (resultCommunityData != null)
                    {
                        resultCommunityData.pendingActionType = isRequestedToJoin ? InviteRequestAction.request_to_join : InviteRequestAction.none;

                        if (resultCommunityData.pendingActionType == InviteRequestAction.none)
                            resultCommunityData.inviteOrRequestId = null;
                    }
                }
                else
                    UpdateJoinedCommunity(communityId, true, true);
            }

            RefreshCommunityCardInGrid(communityId);
        }

        public void RemoveOneMemberFromCounter(string communityId)
        {
            CommunityData? resultCommunityData = GetResultCommunityById(communityId);
            resultCommunityData?.DecreaseMembersCount();

            CommunityData? myCommunityData = GetMyCommunityById(communityId);
            if (!ReferenceEquals(myCommunityData, resultCommunityData))
                myCommunityData?.DecreaseMembersCount();

            RefreshCommunityCardInGrid(communityId);
        }

        public void SetResultsSectionActive(bool isActive) =>
            resultsSection.SetActive(isActive);

        private void RefreshCommunityCardInGrid(string communityId)
        {
            for (var i = 0; i < currentResults.Count; i++)
            {
                CommunityData communityData = currentResults[i];
                if (communityData.id != communityId) continue;
                resultLoopGrid.RefreshItemByItemIndex(i);
                break;
            }
        }

        private CommunityData? GetResultCommunityById(string communityId)
        {
            foreach (CommunityData communityData in currentResults)
            {
                if (communityData.id == communityId)
                    return communityData;
            }

            return null;
        }

        private CommunityData? GetMyCommunityById(string communityId)
        {
            foreach (CommunityData communityData in currentMyCommunities)
            {
                if (communityData.id == communityId)
                    return communityData;
            }

            return null;
        }

        private readonly CancellationTokenSource myCommunityThumbnailsLoadingCts = new();

        private LoopListViewItem2 SetupMyCommunityCardByIndex(LoopListView2 loopListView, int index)
        {
            CommunityData communityData = currentMyCommunities[index];
            LoopListViewItem2 listItem = loopListView.NewListViewItem(loopListView.ItemPrefabDataList[0].mItemPrefab.name);
            MyCommunityCardView cardView = listItem.GetComponent<MyCommunityCardView>();

            // Setup card data
            cardView.SetCommunityId(communityData.id);
            cardView.SetTitle(communityData.name);
            cardView.SetUserRole(communityData.role);
            cardView.SetRequestsReceived(communityData.requestsReceived);
            //thumbnailLoader!.LoadCommunityThumbnailAsync(communityData.thumbnails?.raw, cardView.communityThumbnail, defaultThumbnailSprite, myCommunityThumbnailsLoadingCts.Token).Forget();

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
            cardView.SetTitle(communityData.name);
            cardView.SetOwner(communityData.ownerName);
            cardView.SetDescription(communityData.description);
            cardView.SetPrivacy(communityData.privacy);
            cardView.SetMembersCount(communityData.membersCount);
            cardView.SetInviteOrRequestId(communityData.inviteOrRequestId);
            cardView.SetActionButtonsType(communityData.privacy, communityData.pendingActionType, communityData.role != CommunityMemberRole.none);
            //thumbnailLoader!.LoadCommunityThumbnailAsync(communityData.thumbnails?.raw, cardView.communityThumbnail, defaultThumbnailSprite, myCommunityThumbnailsLoadingCts.Token).Forget();
            cardView.SetActionLoadingActive(false);

            // Setup card events
            cardView.MainButtonClicked -= CommunityProfileOpened;
            cardView.MainButtonClicked += CommunityProfileOpened;
            cardView.ViewCommunityButtonClicked -= CommunityProfileOpened;
            cardView.ViewCommunityButtonClicked += CommunityProfileOpened;
            cardView.JoinCommunityButtonClicked -= OnCommunityJoined;
            cardView.JoinCommunityButtonClicked += OnCommunityJoined;
            cardView.RequestToJoinCommunityButtonClicked -= OnCommunityRequestedToJoin;
            cardView.RequestToJoinCommunityButtonClicked += OnCommunityRequestedToJoin;
            cardView.CancelRequestToJoinCommunityButtonClicked -= OnCommunityRequestToJoinCanceled;
            cardView.CancelRequestToJoinCommunityButtonClicked += OnCommunityRequestToJoinCanceled;

            // Setup mutual friends
            if (profileRepositoryWrapper != null)
                cardView.SetupMutualFriends(profileRepositoryWrapper, communityData);

            return gridItem;
        }

        private void SetMyCommunitiesAsEmpty(bool isEmpty)
        {
            //myCommunitiesEmptyContainer.SetActive(isEmpty);
            //myCommunitiesMainContainer.SetActive(!isEmpty);
        }

        private void SetResultsAsEmpty(bool isEmpty)
        {
            resultsEmptyContainer.SetActive(isEmpty);
            resultLoopGrid.gameObject.SetActive(!isEmpty);
        }

        private void OnCommunityJoined(string communityId, CommunityResultCardView cardView)
        {
            cardView.SetActionLoadingActive(true);
            CommunityJoined?.Invoke(communityId);
        }

        private void OnCommunityRequestedToJoin(string communityId, CommunityResultCardView cardView)
        {
            cardView.SetActionLoadingActive(true);
            CommunityRequestedToJoin?.Invoke(communityId);
        }

        private void OnInvitesAndRequestsCommunityProfileOpened(string communityId) =>
            CommunityProfileOpened?.Invoke(communityId);

        private void OnCommunityRequestToJoinCanceled(string communityId, string requestId, CommunityResultCardView cardView)
        {
            cardView.SetActionLoadingActive(true);
            CommunityRequestToJoinCanceled?.Invoke(communityId, requestId);
        }

        private void OnCommunityInvitationAccepted(string communityId, string invitationId, CommunityResultCardView cardView)
        {
            cardView.SetActionLoadingActive(true);
            CommunityInvitationAccepted?.Invoke(communityId, invitationId);
        }

        private void OnCommunityInvitationRejected(string communityId, string invitationId, CommunityResultCardView cardView)
        {
            cardView.SetActionLoadingActive(true);
            CommunityInvitationRejected?.Invoke(communityId, invitationId);
        }

        public void SetThumbnailLoader(ThumbnailLoader newThumbnailLoader)
        {
            this.thumbnailLoader = newThumbnailLoader;
            invitesAndRequestsView.SetThumbnailLoader(newThumbnailLoader);
        }
    }
}
