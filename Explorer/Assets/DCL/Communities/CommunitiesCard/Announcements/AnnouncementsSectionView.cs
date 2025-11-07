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
        private bool isCreationAllowed;

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

        public void SetAllowCreation(bool isAllowed) =>
            this.isCreationAllowed = isAllowed;

        public void RefreshGrid(SectionFetchData<CommunityPost> announcementsFetchData, bool redraw)
        {
            this.currentAnnouncementsFetchData = announcementsFetchData;
            loopList.SetListItemCount(currentAnnouncementsFetchData.Items.Count, false);

            if (redraw)
                loopList.RefreshAllShownItem();
        }

        private LoopListViewItem2 GetLoopListItemByIndex(LoopListView2 loopListView, int index)
        {
            if (isCreationAllowed)
            {
                if (currentAnnouncementsFetchData.Items[index].type == CommunityPostType.CREATION_INPUT)
                {
                    LoopListViewItem2 creationInputItem = loopList.NewListViewItem(loopList.ItemPrefabDataList[0].mItemPrefab.name);
                    AnnouncementCreationCardView announcementCreationCardItem = creationInputItem.GetComponent<AnnouncementCreationCardView>();
                    announcementCreationCardItem.Configure(currentProfile, profileRepositoryWrapper);
                    announcementCreationCardItem.CreateAnnouncementButtonClicked -= OnCreateAnnouncementButtonClicked;
                    announcementCreationCardItem.CreateAnnouncementButtonClicked += OnCreateAnnouncementButtonClicked;
                    return creationInputItem;
                }

                if (currentAnnouncementsFetchData.Items[index].type == CommunityPostType.SEPARATOR)
                {
                    LoopListViewItem2 separatorItem = loopList.NewListViewItem(loopList.ItemPrefabDataList[1].mItemPrefab.name);
                    return separatorItem;
                }
            }

            LoopListViewItem2 listItem = loopList.NewListViewItem(loopListView.ItemPrefabDataList[2].mItemPrefab.name);
            AnnouncementCardView announcementCardItem = listItem.GetComponent<AnnouncementCardView>();

            CommunityPost announcementInfo = currentAnnouncementsFetchData.Items[index];
            announcementCardItem.Configure(announcementInfo, profileRepositoryWrapper, isCreationAllowed);
            announcementCardItem.DeleteAnnouncementButtonClicked -= OnDeleteAnnouncementButtonClicked;
            announcementCardItem.DeleteAnnouncementButtonClicked += OnDeleteAnnouncementButtonClicked;

            if (index >= currentAnnouncementsFetchData.TotalFetched - 1 && currentAnnouncementsFetchData.TotalFetched < currentAnnouncementsFetchData.TotalToFetch)
                NewDataRequested?.Invoke();

            return listItem;
        }

        private void OnCreateAnnouncementButtonClicked(string announcementContent) =>
            CreateAnnouncementButtonClicked?.Invoke(announcementContent);

        private void OnDeleteAnnouncementButtonClicked(string announcementId) =>
            DeleteAnnouncementButtonClicked?.Invoke(announcementId);
    }
}
