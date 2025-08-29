using DCL.UI;
using DCL.UI.Utilities;
using SuperScrollView;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using CommunityData = DCL.Communities.GetUserCommunitiesData.CommunityData;

namespace DCL.Communities.CommunitiesBrowser
{
    public class StreamingCommunitiesView : MonoBehaviour
    {
        public event Action<string>? JoinStream;

        [Header("Streaming Section")]
        [SerializeField] private GameObject streamingSection = null!;
        [SerializeField] private LoopGridView streamingLoopGrid = null!;
        [SerializeField] private SkeletonLoadingView streamingLoadingSpinner = null!;
        [SerializeField] private Sprite defaultThumbnailSprite = null!;

        private readonly List<string> streamingResultsIds = new();
        private ThumbnailLoader? thumbnailLoader;
        private CommunitiesBrowserStateService? browserStateService;

        public void SetThumbnailLoader(ThumbnailLoader newThumbnailLoader, Sprite defaultSprite)
        {
            thumbnailLoader = newThumbnailLoader;
            defaultThumbnailSprite = defaultSprite;
        }

        public void InitializeStreamingResultsGrid(int itemTotalCount)
        {
            streamingLoopGrid.InitGridView(itemTotalCount, SetupStreamingCommunityResultCardByIndex);
            streamingLoopGrid.gameObject.GetComponent<ScrollRect>()?.SetScrollSensitivityBasedOnPlatform();
        }

        public void AddStreamingResultsItems(CommunityData[] communities)
        {
            streamingSection.SetActive(true);
            foreach (CommunityData communityData in communities)
            {
                streamingResultsIds.Add(communityData.id);
            }

            streamingLoopGrid.SetListItemCount(streamingResultsIds.Count);
            SetStreamingResultsAsEmpty(streamingResultsIds.Count == 0);
            streamingLoopGrid.ScrollRect.verticalNormalizedPosition = 1f;
        }

        public void ClearStreamingResultsItems()
        {
            streamingResultsIds.Clear();
            streamingLoopGrid.SetListItemCount(0, false);
            SetStreamingResultsAsEmpty(true);
        }

        public void SetStreamingResultsAsLoading(bool isLoading)
        {
            if (isLoading)
                streamingLoadingSpinner.ShowLoading();
            else
                streamingLoadingSpinner.HideLoading();
        }

        private void SetStreamingResultsAsEmpty(bool isEmpty)
        {
            streamingSection.SetActive(!isEmpty);
        }

        private LoopGridViewItem SetupStreamingCommunityResultCardByIndex(LoopGridView loopGridView, int index, int row, int column)
        {
            CommunityData communityData = browserStateService!.GetCommunityDataById(streamingResultsIds[index]);
            LoopGridViewItem gridItem = loopGridView.NewListViewItem(loopGridView.ItemPrefabDataList[0].mItemPrefab.name);
            StreamingCommunityResultCardView cardView = gridItem.GetComponent<StreamingCommunityResultCardView>();

            cardView.SetCommunityId(communityData.id);
            cardView.SetTitle(communityData.name);
            cardView.ConfigureListenersCount(communityData.voiceChatStatus.isActive, communityData.voiceChatStatus.participantCount);
            thumbnailLoader!.LoadCommunityThumbnailAsync(communityData.thumbnails?.raw, cardView.communityThumbnail, defaultThumbnailSprite, default).Forget();

            cardView.MainButtonClicked -= JoinStreamClicked;
            cardView.MainButtonClicked += JoinStreamClicked;

            return gridItem;
        }

        private void JoinStreamClicked(string communityId)
        {
            JoinStream?.Invoke(communityId);
        }

        public void SetCommunitiesBrowserState(CommunitiesBrowserStateService browserStateService)
        {
            this.browserStateService = browserStateService;
        }
    }
}
