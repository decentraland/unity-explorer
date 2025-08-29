using DCL.UI;
using DCL.UI.Profiles.Helpers;
using DCL.UI.Utilities;
using SuperScrollView;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using CommunityData = DCL.Communities.GetUserCommunitiesData.CommunityData;

namespace DCL.Communities.CommunitiesBrowser
{
    public class MyCommunitiesView : MonoBehaviour
    {
        public event Action? ViewAllMyCommunitiesButtonClicked;
        public event Action<string>? CommunityProfileOpened;

        [Header("My Communities Section")]
        [SerializeField] private GameObject myCommunitiesSection = null!;
        [SerializeField] private GameObject myCommunitiesMainContainer = null!;
        [SerializeField] private GameObject myCommunitiesEmptyContainer = null!;
        [SerializeField] private SkeletonLoadingView myCommunitiesLoadingSpinner = null!;
        [SerializeField] private LoopListView2 myCommunitiesLoopList = null!;
        [SerializeField] private Button myCommunitiesViewAllButton = null!;
        [SerializeField] private Sprite defaultThumbnailSprite = null!;

        private CommunitiesBrowserStateService browserStateService = null!;
        private ThumbnailLoader? thumbnailLoader;
        private CancellationTokenSource myCommunityThumbnailsLoadingCts = new();

        private void Awake()
        {
            myCommunitiesViewAllButton.onClick.AddListener(() => ViewAllMyCommunitiesButtonClicked?.Invoke());
        }

        private void OnDestroy()
        {
            myCommunitiesViewAllButton.onClick.RemoveAllListeners();
        }

        public void SetMyCommunitiesAsLoading(bool isLoading)
        {
            if (isLoading)
                myCommunitiesLoadingSpinner.ShowLoading();
            else
                myCommunitiesLoadingSpinner.HideLoading();
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

        public void UpdateJoinedCommunity(string communityId, bool isJoined, bool isSuccess)
        {
            browserStateService.UpdateJoinedCommunity(communityId, isJoined, isSuccess);

            if (isSuccess)
            {
                myCommunitiesLoopList.SetListItemCount(browserStateService.GetMyCommunitiesCount(), false);
                SetMyCommunitiesAsEmpty(browserStateService.GetMyCommunitiesCount() == 0);
            }
        }

        public void SetThumbnailLoader(ThumbnailLoader newThumbnailLoader)
        {
            this.thumbnailLoader = newThumbnailLoader;
        }

        public void SetStateService(CommunitiesBrowserStateService stateService)
        {
            this.browserStateService = stateService;
        }

        private LoopListViewItem2 SetupMyCommunityCardByIndex(LoopListView2 loopListView, int index)
        {
            CommunityData communityData = browserStateService.MyCommunities[index];
            LoopListViewItem2 listItem = loopListView.NewListViewItem(loopListView.ItemPrefabDataList[0].mItemPrefab.name);
            MyCommunityCardView cardView = listItem.GetComponent<MyCommunityCardView>();

            cardView.SetCommunityId(communityData.id);
            cardView.SetTitle(communityData.name);
            cardView.SetUserRole(communityData.role);
            cardView.ConfigureListenersCount(communityData.voiceChatStatus.isActive, communityData.voiceChatStatus.participantCount);

            thumbnailLoader!.LoadCommunityThumbnailAsync(communityData.thumbnails?.raw, cardView.communityThumbnail, defaultThumbnailSprite, myCommunityThumbnailsLoadingCts.Token).Forget();

            cardView.MainButtonClicked -= CommunityProfileOpened;
            cardView.MainButtonClicked += CommunityProfileOpened;

            return listItem;
        }

        private void SetMyCommunitiesAsEmpty(bool isEmpty)
        {
            myCommunitiesEmptyContainer.SetActive(isEmpty);
            myCommunitiesMainContainer.SetActive(!isEmpty);
        }
    }
}
