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

        private readonly List<CommunityData> currentBrowseAllResults = new();
        private ProfileRepositoryWrapper? profileRepositoryWrapper;
        private ThumbnailLoader? thumbnailLoader;
        private Sprite defaultThumbnailSprite = null!;

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
            currentBrowseAllResults.Clear();
            browseAllLoopGrid.SetListItemCount(0, false);
            SetBrowseAllAsEmpty(true);
        }

        public void AddBrowseAllItems(CommunityData[] communities, bool resetPos)
        {
            currentBrowseAllResults.AddRange(communities);
            browseAllLoopGrid.SetListItemCount(currentBrowseAllResults.Count, resetPos);
            SetBrowseAllAsEmpty(currentBrowseAllResults.Count == 0);

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

        public void UpdateJoinedCommunity(string communityId, bool isJoined, bool isSuccess)
        {
            if (isSuccess)
            {
                CommunityData? browseAllCommunityData = GetBrowseAllCommunityById(communityId);
                browseAllCommunityData?.SetAsJoined(isJoined);
                RefreshCommunityCardInGrid(communityId);
            }
        }

        public void RemoveOneMemberFromCounter(string communityId)
        {
            CommunityData? browseAllCommunityData = GetBrowseAllCommunityById(communityId);
            browseAllCommunityData?.DecreaseMembersCount();
            RefreshCommunityCardInGrid(communityId);
        }

        private void RefreshCommunityCardInGrid(string communityId)
        {
            for (var i = 0; i < currentBrowseAllResults.Count; i++)
            {
                CommunityData communityData = currentBrowseAllResults[i];
                if (communityData.id != communityId) continue;
                browseAllLoopGrid.RefreshItemByItemIndex(i);
                break;
            }
        }

        private CommunityData? GetBrowseAllCommunityById(string communityId)
        {
            foreach (CommunityData communityData in currentBrowseAllResults)
            {
                if (communityData.id == communityId)
                    return communityData;
            }

            return null;
        }

        private void SetBrowseAllAsEmpty(bool isEmpty)
        {
            browseAllEmptyContainer.SetActive(isEmpty);
            browseAllLoopGrid.gameObject.SetActive(!isEmpty);
        }

        private LoopGridViewItem SetupBrowseAllCommunityResultCardByIndex(LoopGridView loopGridView, int index, int row, int column)
        {
            CommunityData communityData = currentBrowseAllResults[index];
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
    }
}
