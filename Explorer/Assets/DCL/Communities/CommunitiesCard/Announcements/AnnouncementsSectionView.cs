using DCL.Communities.CommunitiesDataProvider.DTOs;
using DCL.Profiles;
using DCL.UI;
using DCL.UI.Profiles.Helpers;
using DCL.UI.Utilities;
using SuperScrollView;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Communities.CommunitiesCard.Announcements
{
    public class AnnouncementsSectionView : MonoBehaviour, ICommunityFetchingView<CommunityPost>
    {
        private const int ELEMENT_MISSING_THRESHOLD = 1;

        [SerializeField] private LoopListView2 loopList = null!;
        [SerializeField] private ScrollRect loopListScrollRect = null!;
        [SerializeField] private GameObject emptyState = null!;
        [SerializeField] private SkeletonLoadingView loadingObject = null!;

        public event Action? NewDataRequested;
        public event Action<string>? CreateAnnouncementButtonClicked;
        public event Action<string>? DeleteAnnouncementButtonClicked;

        private SectionFetchData<CommunityPost> currentAnnouncementsFetchData = null!;
        private Profile? currentProfile;
        private ProfileRepositoryWrapper profileRepositoryWrapper = null!;

        private void Awake() =>
            loopListScrollRect.SetScrollSensitivityBasedOnPlatform();

        public void SetActive(bool active) =>
            gameObject.SetActive(active);

        public void SetEmptyStateActive(bool active) =>
            emptyState.SetActive(active);

        public void SetLoadingStateActive(bool active)
        {
            if (active)
                loadingObject.ShowLoading();
            else
                loadingObject.HideLoading();
        }

        public void InitList() =>
            loopList.InitListView(0, GetLoopListItemByIndex);

        public void SetProfile(Profile? userProfile, ProfileRepositoryWrapper profileRepoWrapper)
        {
            this.currentProfile = userProfile;
            this.profileRepositoryWrapper = profileRepoWrapper;
        }

        public void RefreshGrid(SectionFetchData<CommunityPost> announcementsFetchData, bool redraw)
        {
            this.currentAnnouncementsFetchData = announcementsFetchData;
            loopList.SetListItemCount(currentAnnouncementsFetchData.Items.Count, false);

            if (redraw)
                loopList.RefreshAllShownItem();
        }

        private LoopListViewItem2 GetLoopListItemByIndex(LoopListView2 loopListView, int index)
        {
            if (index == 0)
            {
                LoopListViewItem2 firstItem = loopList.NewListViewItem(loopList.ItemPrefabDataList[0].mItemPrefab.name);
                AnnouncementCreationCardView announcementCreationCardItem = firstItem.GetComponent<AnnouncementCreationCardView>();
                announcementCreationCardItem.Configure(currentProfile, profileRepositoryWrapper);
                announcementCreationCardItem.CreateAnnouncementButtonClicked -= OnCreateAnnouncementButtonClicked;
                announcementCreationCardItem.CreateAnnouncementButtonClicked += OnCreateAnnouncementButtonClicked;

                return firstItem;
            }

            LoopListViewItem2 listItem = loopList.NewListViewItem(loopListView.ItemPrefabDataList[1].mItemPrefab.name);
            AnnouncementCardView announcementCardItem = listItem.GetComponent<AnnouncementCardView>();

            SectionFetchData<CommunityPost> announcementsData = currentAnnouncementsFetchData;

            int realIndex = index - 1;
            CommunityPost announcementInfo = announcementsData.Items[realIndex];
            announcementCardItem.Configure(announcementInfo, profileRepositoryWrapper);
            announcementCardItem.DeleteAnnouncementButtonClicked -= OnDeleteAnnouncementButtonClicked;
            announcementCardItem.DeleteAnnouncementButtonClicked += OnDeleteAnnouncementButtonClicked;

            if (realIndex >= announcementsData.TotalFetched - ELEMENT_MISSING_THRESHOLD && announcementsData.TotalFetched < announcementsData.TotalToFetch)
                NewDataRequested?.Invoke();

            return listItem;
        }

        private void OnCreateAnnouncementButtonClicked(string announcementContent) =>
            CreateAnnouncementButtonClicked?.Invoke(announcementContent);

        private void OnDeleteAnnouncementButtonClicked(string announcementId) =>
            DeleteAnnouncementButtonClicked?.Invoke(announcementId);
    }
}
