using DCL.UI;
using DCL.UI.Profiles.Helpers;
using DCL.UI.Utilities;
using SuperScrollView;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using CommunityData = DCL.Communities.GetUserCommunitiesData.CommunityData;

namespace DCL.Communities.CommunitiesBrowser
{
    public class BrowseAllCommunitiesView : MonoBehaviour
    {
        public event Action<string>? CommunityProfileOpened;
        public event Action<string, CommunityResultCardView>? CommunityJoined;
        [Header("Browse All Section")]
        [SerializeField] private TMP_Text browseAllTitleText = null!;
        [SerializeField] private TMP_Text browseAllCountText = null!;
        [SerializeField] private GameObject browseAllSection = null!;
        [SerializeField] private LoopGridView browseAllLoopGrid = null!;
        [SerializeField] private GameObject browseAllEmptyContainer = null!;
        [SerializeField] private SkeletonLoadingView browseAllLoadingSpinner = null!;
        [SerializeField] private GameObject browseAllLoadingMoreSpinner = null!;
        [SerializeField] private Sprite defaultThumbnailSprite = null!;

        private readonly List<string> browseAllResultsIds = new();

        private ProfileRepositoryWrapper? profileRepositoryWrapper;
        private ThumbnailLoader? thumbnailLoader;
        private CommunitiesBrowserStateService? browserStateService;

        public void SetThumbnailLoader(ThumbnailLoader newThumbnailLoader, Sprite defaultSprite)
        {
            thumbnailLoader = newThumbnailLoader;
            defaultThumbnailSprite = defaultSprite;
        }

        public void SetProfileRepositoryWrapper(ProfileRepositoryWrapper profileDataProvider)
        {
            profileRepositoryWrapper = profileDataProvider;
        }

        public void InitializeBrowseAllGrid(int itemTotalCount)
        {
            browseAllLoopGrid.InitGridView(itemTotalCount, SetupBrowseAllCommunityResultCardByIndex);
            browseAllLoopGrid.gameObject.GetComponent<ScrollRect>()?.SetScrollSensitivityBasedOnPlatform();
        }

        public void ClearBrowseAllItems()
        {
            browseAllResultsIds.Clear();
            browseAllLoopGrid.SetListItemCount(0, false);
            SetBrowseAllAsEmpty(true);
        }

        public void AddBrowseAllItems(CommunityData[] communities, bool resetPos)
        {
            browserStateService!.AddCommunities(communities);
            foreach (CommunityData communityData in communities)
            {
                browseAllResultsIds.Add(communityData.id);
            }

            browseAllLoopGrid.SetListItemCount(browseAllResultsIds.Count, resetPos);
            SetBrowseAllAsEmpty(browseAllResultsIds.Count == 0);

            if (resetPos)
                browseAllLoopGrid.ScrollRect.verticalNormalizedPosition = 1f;
        }

        public void SetBrowseAllAsLoading(bool isLoading)
        {
            if (isLoading)
            {
                browseAllCountText.text = string.Empty;
                browseAllLoadingSpinner.ShowLoading();
            }
            else
                browseAllLoadingSpinner.HideLoading();
        }

        public void SetBrowseAllTitleText(string text)
        {
            browseAllTitleText.text = text;
        }

        public void SetBrowseAllCountText(int count)
        {
            browseAllCountText.text = $"({count})";
        }

        public void SetBrowseAllLoadingMoreActive(bool isActive)
        {
            browseAllLoadingMoreSpinner.SetActive(isActive);
        }

        public void UpdateJoinedCommunity(string communityId, bool isSuccess)
        {
            if (isSuccess)
                RefreshCommunityCardInGrid(communityId);
        }

        public void RemoveOneMemberFromCounter(string communityId)
        {
            RefreshCommunityCardInGrid(communityId);
        }

        private void RefreshCommunityCardInGrid(string communityId)
        {
            for (var i = 0; i < browseAllResultsIds.Count; i++)
            {
                CommunityData communityData = browserStateService!.GetCommunityDataById(browseAllResultsIds[i]);
                if (communityData.id != communityId) continue;
                browseAllLoopGrid.RefreshItemByItemIndex(i);
                break;
            }
        }

        private void SetBrowseAllAsEmpty(bool isEmpty)
        {
            browseAllEmptyContainer.SetActive(isEmpty);
            browseAllLoopGrid.gameObject.SetActive(!isEmpty);
        }

        private LoopGridViewItem SetupBrowseAllCommunityResultCardByIndex(LoopGridView loopGridView, int index, int row, int column)
        {
            CommunityData communityData = browserStateService!.GetCommunityDataById(browseAllResultsIds[index]);
            LoopGridViewItem gridItem = loopGridView.NewListViewItem(loopGridView.ItemPrefabDataList[0].mItemPrefab.name);
            CommunityResultCardView cardView = gridItem.GetComponent<CommunityResultCardView>();

            // Setup card data
            cardView.SetCommunityId(communityData.id);
            cardView.SetTitle(communityData.name);
            cardView.SetOwner(communityData.ownerName);
            cardView.SetDescription(communityData.description);
            cardView.SetPrivacy(communityData.privacy);
            cardView.SetMembersCount(communityData.membersCount);
            cardView.SetOwnership(communityData.role != CommunityMemberRole.none);
            cardView.ConfigureListenersCount(communityData.voiceChatStatus.isActive, communityData.voiceChatStatus.participantCount);
            thumbnailLoader!.LoadCommunityThumbnailAsync(communityData.thumbnails?.raw, cardView.communityThumbnail, defaultThumbnailSprite, default).Forget();
            cardView.SetJoiningLoadingActive(false);

            // Setup card events
            cardView.MainButtonClicked -= OnCommunityProfileOpened;
            cardView.MainButtonClicked += OnCommunityProfileOpened;
            cardView.ViewCommunityButtonClicked -= OnCommunityProfileOpened;
            cardView.ViewCommunityButtonClicked += OnCommunityProfileOpened;
            cardView.JoinCommunityButtonClicked -= OnCommunityJoined;
            cardView.JoinCommunityButtonClicked += OnCommunityJoined;

            // Setup mutual friends
            if (profileRepositoryWrapper != null)
                cardView.SetupMutualFriends(profileRepositoryWrapper, communityData);

            return gridItem;
        }

        private void OnCommunityProfileOpened(string communityId)
        {
            CommunityProfileOpened?.Invoke(communityId);
        }

        private void OnCommunityJoined(string communityId, CommunityResultCardView cardView)
        {
            cardView.SetJoiningLoadingActive(true);
            CommunityJoined?.Invoke(communityId, cardView);
        }

        public void SetCommunitiesBrowserState(CommunitiesBrowserStateService browserStateService)
        {
            this.browserStateService = browserStateService;
        }
    }
}
