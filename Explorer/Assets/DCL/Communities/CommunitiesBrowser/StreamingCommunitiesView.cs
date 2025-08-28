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

        private readonly List<CommunityData> currentStreamingResults = new();
        private ThumbnailLoader? thumbnailLoader;
        private Sprite defaultThumbnailSprite = null!;

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

        public void AddStreamingResultsItems(CommunityData[] dataResults)
        {
            streamingSection.SetActive(true);
            currentStreamingResults.AddRange(dataResults);
            streamingLoopGrid.SetListItemCount(currentStreamingResults.Count, true);
            SetStreamingResultsAsEmpty(currentStreamingResults.Count == 0);
            streamingLoopGrid.ScrollRect.verticalNormalizedPosition = 1f;
        }

        public void ClearStreamingResultsItems()
        {
            currentStreamingResults.Clear();
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
            CommunityData communityData = currentStreamingResults[index];
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
    }
}
