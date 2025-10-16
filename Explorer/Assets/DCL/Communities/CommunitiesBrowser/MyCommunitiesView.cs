using DCL.UI;
using DCL.UI.Utilities;
using DCL.VoiceChat;
using SuperScrollView;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using CommunityData = DCL.Communities.CommunitiesDataProvider.DTOs.GetUserCommunitiesData.CommunityData;

namespace DCL.Communities.CommunitiesBrowser
{
    public class MyCommunitiesView : MonoBehaviour
    {
        public event Action? ViewAllMyCommunitiesButtonClicked;
        public event Action<string>? CommunityProfileOpened;

        [SerializeField] private GameObject myCommunitiesSection = null!;
        [SerializeField] private GameObject myCommunitiesMainContainer = null!;
        [SerializeField] private GameObject myCommunitiesEmptyContainer = null!;
        [SerializeField] private SkeletonLoadingView myCommunitiesLoadingSpinner = null!;
        [SerializeField] private LoopListView2 myCommunitiesLoopList = null!;
        [SerializeField] private Button myCommunitiesViewAllButton = null!;
        [SerializeField] private Sprite defaultThumbnailSprite = null!;

        private readonly List<string> communitiesIds = new();

        private CommunitiesBrowserStateService browserStateService = null!;
        private ThumbnailLoader? thumbnailLoader;
        private ICommunityCallOrchestrator? communityCallOrchestrator;

        //TODO FRAN: MAKE THIS HAVE ANY USE!
        private CancellationTokenSource myCommunityThumbnailsLoadingCts = new();

        private void Awake()
        {
            myCommunitiesViewAllButton.onClick.AddListener(() => ViewAllMyCommunitiesButtonClicked?.Invoke());
        }

        private void OnDestroy()
        {
            myCommunitiesViewAllButton.onClick.RemoveAllListeners();
        }

        public void SetAsLoading(bool isLoading)
        {
            if (isLoading)
                myCommunitiesLoadingSpinner.ShowLoading();
            else
                myCommunitiesLoadingSpinner.HideLoading();
        }

        public void InitializeCommunitiesList(int itemTotalCount)
        {
            myCommunitiesLoopList.InitListView(itemTotalCount, SetupCommunityCardByIndex);
            myCommunitiesLoopList.gameObject.GetComponent<ScrollRect>()?.SetScrollSensitivityBasedOnPlatform();
        }

        private LoopListViewItem2 SetupCommunityCardByIndex(LoopListView2 loopListView, int index)
        {
            CommunityData communityData = browserStateService.GetCommunityDataById(communitiesIds[index]);
            LoopListViewItem2 listItem = loopListView.NewListViewItem(loopListView.ItemPrefabDataList[0].mItemPrefab.name);
            MyCommunityCardView cardView = listItem.GetComponent<MyCommunityCardView>();

            cardView.SetCommunityId(communityData.id);
            cardView.SetTitle(communityData.name);
            cardView.SetUserRole(communityData.role);

            if (communityCallOrchestrator?.CurrentCommunityId.Value == communityData.id)
                cardView.ConfigureListeningTooltip();
            else
                cardView.ConfigureListenersCount(communityData.voiceChatStatus.isActive, communityData.voiceChatStatus.participantCount);

            cardView.SetRequestsReceived(communityData.requestsReceived);

            thumbnailLoader!.LoadCommunityThumbnailFromUrlAsync(communityData.thumbnailUrl, cardView.communityThumbnail, defaultThumbnailSprite, myCommunityThumbnailsLoadingCts.Token, true).Forget();

            cardView.MainButtonClicked -= CommunityProfileOpened;
            cardView.MainButtonClicked += CommunityProfileOpened;

            return listItem;
        }

        public void ClearCommunitiesItems()
        {
            communitiesIds.Clear();
            myCommunitiesLoopList.SetListItemCount(0, false);
            SetAsEmpty(true);
        }

        public void AddCommunitiesItems(CommunityData[] communities, bool resetPos)
        {
            foreach (var community in communities)
                communitiesIds.Add(community.id);

            myCommunitiesLoopList.SetListItemCount(communitiesIds.Count, resetPos);
            SetAsEmpty(communitiesIds.Count == 0);
            myCommunitiesLoopList.ScrollRect.verticalNormalizedPosition = 1f;
        }

        public void UpdateJoinedCommunity(string communityId, bool isJoined, bool isSuccess)
        {
            if (isJoined)
            {
                if (!communitiesIds.Contains(communityId))
                    communitiesIds.Add(communityId);
            }
            else
                communitiesIds.Remove(communityId);

            if (isSuccess)
            {
                myCommunitiesLoopList.SetListItemCount(communitiesIds.Count, false);
                SetAsEmpty(communitiesIds.Count == 0);
            }
        }

        private void SetAsEmpty(bool isEmpty)
        {
            myCommunitiesEmptyContainer.SetActive(isEmpty);
            myCommunitiesMainContainer.SetActive(!isEmpty);
        }

        public void SetDependencies(CommunitiesBrowserStateService communitiesBrowserStateService, ThumbnailLoader newThumbnailLoader, ICommunityCallOrchestrator orchestrator)
        {
            browserStateService = communitiesBrowserStateService;
            thumbnailLoader = newThumbnailLoader;
            communityCallOrchestrator = orchestrator;
        }
    }
}
