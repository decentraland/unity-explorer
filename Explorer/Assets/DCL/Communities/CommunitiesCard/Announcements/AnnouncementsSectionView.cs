using DCL.Communities.CommunitiesDataProvider.DTOs;
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
        [SerializeField] private LoopListView2 loopList = null!;
        [SerializeField] private ScrollRect loopListScrollRect = null!;
        [SerializeField] private GameObject emptyState = null!;
        [SerializeField] private SkeletonLoadingView loadingObject = null!;
        [SerializeField] private Button createAnnouncementButton = null!;

        public event Action? NewDataRequested;
        public event Action? CreateAnnouncementButtonClicked;
        public event Action<string, string>? DeleteAnnouncementButtonClicked;

        private ProfileRepositoryWrapper profileRepositoryWrapper = null!;
        private SectionFetchData<CommunityPost> currentAnnouncementsFetchData = null!;

        private void Awake()
        {
            loopListScrollRect.SetScrollSensitivityBasedOnPlatform();
            createAnnouncementButton.onClick.AddListener(OnCreateAnnouncementButtonClicked);
        }

        private void OnDestroy() =>
            createAnnouncementButton.onClick.RemoveListener(OnCreateAnnouncementButtonClicked);

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

        public void InitList(ProfileRepositoryWrapper profileRepoWrapper)
        {
            loopList.InitListView(0, GetLoopListItemByIndex);
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
            LoopListViewItem2 listItem = loopList.NewListViewItem(loopListView.ItemPrefabDataList[0].mItemPrefab.name);
            AnnouncementCardView elementView = listItem.GetComponent<AnnouncementCardView>();

            SectionFetchData<CommunityPost> announcementsData = currentAnnouncementsFetchData;

            CommunityPost announcementInfo = announcementsData.Items[index];
            elementView.Configure(announcementInfo, profileRepositoryWrapper);
            elementView.DeleteAnnouncementButtonClicked -= OnDeleteAnnouncementButtonClicked;
            elementView.DeleteAnnouncementButtonClicked += OnDeleteAnnouncementButtonClicked;

            if (index >= announcementsData.TotalFetched - 1 && announcementsData.TotalFetched < announcementsData.TotalToFetch)
                NewDataRequested?.Invoke();

            return listItem;
        }

        private void OnCreateAnnouncementButtonClicked() =>
            CreateAnnouncementButtonClicked?.Invoke();

        private void OnDeleteAnnouncementButtonClicked(string communityId, string announcementId) =>
            DeleteAnnouncementButtonClicked?.Invoke(communityId, announcementId);
    }
}
