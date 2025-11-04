using DCL.Communities.CommunitiesDataProvider.DTOs;
using DCL.UI;
using DCL.UI.Utilities;
using SuperScrollView;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using CommunityData = DCL.Communities.CommunitiesDataProvider.DTOs.GetCommunityResponse.CommunityData;

namespace DCL.Communities.CommunitiesCard.Announcements
{
    public class AnnouncementsSectionView : MonoBehaviour, ICommunityFetchingView<CommunityPost>
    {
        [field: SerializeField] private LoopListView2 loopList { get; set; } = null!;
        [field: SerializeField] private ScrollRect loopListScrollRect { get; set; } = null!;
        [field: SerializeField] private GameObject emptyState { get; set; } = null!;
        [field: SerializeField] private SkeletonLoadingView loadingObject { get; set; } = null!;

        public event Action? NewDataRequested;

        private SectionFetchData<CommunityPost> announcementsInfo = null!;
        private bool canModify;
        private CommunityData communityData;
        private CancellationToken cancellationToken;

        private void Awake() =>
            loopListScrollRect.SetScrollSensitivityBasedOnPlatform();

        public void SetActive(bool active) =>
            gameObject.SetActive(active);

        public void SetEmptyStateActive(bool active) =>
            emptyState.SetActive(active && !canModify);

        public void SetLoadingStateActive(bool active)
        {
            if (active)
                loadingObject.ShowLoading();
            else
                loadingObject.HideLoading();
        }

        public void SetCanModify(bool canModify) =>
            this.canModify = canModify;

        public void SetCommunityData(CommunityData community) =>
            communityData = community;

        public void InitList(CancellationToken panelCancellationToken)
        {
            loopList.InitListView(0, GetLoopListItemByIndex);
            cancellationToken = panelCancellationToken;
        }

        private LoopListViewItem2 GetLoopListItemByIndex(LoopListView2 loopListView, int index)
        {
            LoopListViewItem2 listItem = loopList.NewListViewItem(loopListView.ItemPrefabDataList[0].mItemPrefab.name);
            AnnouncementCardView elementView = listItem.GetComponent<AnnouncementCardView>();

            SectionFetchData<CommunityPost> announcementsData = announcementsInfo;

            CommunityPost announcementInfo = announcementsData.Items[index];
            elementView.Configure(announcementInfo);

            if (index >= announcementsData.TotalFetched - 1 && announcementsData.TotalFetched < announcementsData.TotalToFetch)
                NewDataRequested?.Invoke();

            return listItem;
        }

        public void RefreshGrid(SectionFetchData<CommunityPost> announcementsInfo, bool redraw)
        {
            this.announcementsInfo = announcementsInfo;
            int count = announcementsInfo.Items.Count;

            loopList.SetListItemCount(count, false);

            if (redraw)
                loopList.RefreshAllShownItem();
        }
    }
}
