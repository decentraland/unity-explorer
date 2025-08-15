using DCL.UI;
using DCL.UI.Profiles.Helpers;
using DCL.UI.Utilities;
using SuperScrollView;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Utility;
using CommunityData = DCL.Communities.GetUserCommunitiesData.CommunityData;

namespace DCL.Communities.CommunitiesBrowser
{
    public class CommunitiesGridView : MonoBehaviour
    {
        public event Action<string>? CommunityJoined;
        public event Action<string>? CommunityProfileOpened;

        [SerializeField] private TMP_Text titleText = null!;
        [SerializeField] private TMP_Text countText = null!;
        [SerializeField] private GameObject contentSection = null!;
        [SerializeField] private LoopGridView loopGrid = null!;
        [SerializeField] private GameObject emptyContainer = null!;
        [SerializeField] private SkeletonLoadingView loadingSpinner = null!;
        [SerializeField] private GameObject loadingMoreSpinner = null!;
        [SerializeField] private Sprite defaultThumbnailSprite = null!;

        private ProfileRepositoryWrapper? profileRepositoryWrapper;
        private ThumbnailLoader? thumbnailLoader;
        private CommunitiesBrowserOrchestrator? orchestrator;
        private readonly Dictionary<string, int> communityIdToGridIndexMap = new();
        private CancellationTokenSource myCommunityThumbnailsLoadingCts = new();


        public void InitializeResultsGrid(int itemTotalCount, ProfileRepositoryWrapper profileDataProvider)
        {
            myCommunityThumbnailsLoadingCts = myCommunityThumbnailsLoadingCts.SafeRestart();
            loopGrid.InitGridView(itemTotalCount, SetupCommunityResultCardByIndex);
            loopGrid.gameObject.GetComponent<ScrollRect>()?.SetScrollSensitivityBasedOnPlatform();
            profileRepositoryWrapper = profileDataProvider;
        }


        public void SetResultsCountText(int count) =>
            countText.text = $"({count})";

        public void SetResultsTitleText(string text) =>
            titleText.text = text;

        public void SetResultsLoadingMoreActive(bool isActive) =>
            loadingMoreSpinner.SetActive(isActive);

        public void SetResultsAsLoading(bool isLoading)
        {
            if (isLoading)
            {
                countText.text = string.Empty;
                loadingSpinner.ShowLoading();
            }
            else
                loadingSpinner.HideLoading();
        }

        public void SetThumbnailLoader(ThumbnailLoader newThumbnailLoader)
        {
            this.thumbnailLoader = newThumbnailLoader;
        }

        private void SetResultsAsEmpty(bool isEmpty)
        {
            emptyContainer.SetActive(isEmpty);
            loopGrid.gameObject.SetActive(!isEmpty);
        }

        public void ClearResultsItems()
        {
            communityIdToGridIndexMap.Clear();
            loopGrid.SetListItemCount(0, false);
            SetResultsAsEmpty(true);
        }

        public void AddResultsItems(bool resetPos)
        {
            if (orchestrator == null) return;

            if (resetPos)
                communityIdToGridIndexMap.Clear();

            var index = 0;
            foreach (var community in orchestrator.GetFilteredCommunities())
            {
                communityIdToGridIndexMap[community.id] = index++;
            }

            loopGrid.SetListItemCount(index, resetPos);
            SetResultsAsEmpty(index == 0);

            if (resetPos)
                loopGrid.ScrollRect.verticalNormalizedPosition = 1f;
        }

        public void RefreshCommunityCardInGrid(string communityId)
        {
            if (communityIdToGridIndexMap.TryGetValue(communityId, out int gridIndex))
            {
                loopGrid.RefreshItemByItemIndex(gridIndex);
            }
        }

        private void OnCommunityJoined(string communityId, CommunityResultCardView cardView)
        {
            cardView.SetJoiningLoadingActive(true);

            //Send event to controller, then get order FROM controller to update view - is what makes more sense tbh

            if (orchestrator?.GetFilteredCommunity(communityId) is CommunityData communityData)
            {
                CommunityJoined?.Invoke(communityData.id);
            }
        }

        private LoopGridViewItem SetupCommunityResultCardByIndex(LoopGridView loopGridView, int index, int row, int column)
        {
            if (orchestrator == null) return null!;

            var communitiesArray = orchestrator.GetFilteredCommunities();
            CommunityData communityData = communitiesArray[index];
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
            thumbnailLoader!.LoadCommunityThumbnailAsync(communityData.thumbnails?.raw, cardView.communityThumbnail, defaultThumbnailSprite, myCommunityThumbnailsLoadingCts.Token).Forget();
            cardView.SetJoiningLoadingActive(false);

            // Setup card events
            cardView.MainButtonClicked -= CommunityProfileOpened;
            cardView.MainButtonClicked += CommunityProfileOpened;
            cardView.ViewCommunityButtonClicked -= CommunityProfileOpened;
            cardView.ViewCommunityButtonClicked += CommunityProfileOpened;
            cardView.JoinCommunityButtonClicked -= OnCommunityJoined;
            cardView.JoinCommunityButtonClicked += OnCommunityJoined;

            // Setup mutual friends
            if (profileRepositoryWrapper != null)
                cardView.SetupMutualFriends(profileRepositoryWrapper, communityData);

            return gridItem;
        }

    }
}
