using DCL.Communities.CommunitiesCard.Members;
using DCL.Friends.UI.FriendPanel.Sections;
using DCL.InWorldCamera.CameraReelGallery;
using MVC;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Communities.CommunitiesCard
{
    public class CommunityCardView : ViewBase, IView
    {
        public enum Sections
        {
            PHOTOS,
            MEMBERS,
            PLACES,
        }

        [Serializable]
        public struct CameraReelGalleryConfig
        {
            public CameraReelGalleryView CameraReelGalleryView;
            public int GridLayoutFixedColumnCount;
            public int ThumbnailHeight;
            public int ThumbnailWidth;
        }

        public event Action<Sections, bool>? SectionChanged;

        [field: Header("References")]
        [field: SerializeField] public Button CloseButton { get; private set; }
        [field: SerializeField] public Button BackgroundCloseButton { get; private set; }
        [field: SerializeField] public SectionLoadingView LoadingObject { get; private set; }

        [field: Header("-- Sections")]
        [field: Header("Buttons")]
        [field: SerializeField] public Button PhotosButton { get; private set; }
        [field: SerializeField] public Button MembersButton { get; private set; }
        [field: SerializeField] public Button PlacesButton { get; private set; }

        [field: Header("Selections")]
        [field: SerializeField] public GameObject PhotosSectionSelection { get; private set; }
        [field: SerializeField] public GameObject MembersSectionSelection { get; private set; }
        [field: SerializeField] public GameObject PlacesSectionSelection { get; private set; }

        [field: Header("Sections views")]
        [field: SerializeField] public CameraReelGalleryConfig CameraReelGalleryConfigs { get; private set; }
        [field: SerializeField] public MembersListView MembersListView { get; private set; }

        private void Awake()
        {
            PhotosButton.onClick.AddListener(() => ToggleSection(Sections.PHOTOS));
            MembersButton.onClick.AddListener(() => ToggleSection(Sections.MEMBERS));
            PlacesButton.onClick.AddListener(() => ToggleSection(Sections.PLACES));
        }

        private void OnEnable()
        {
            ToggleSection(Sections.PHOTOS, false);
        }

        public void SetLoadingState(bool isLoading)
        {
            if (isLoading)
                LoadingObject?.Show();
            else
                LoadingObject?.Hide();
        }

        private void ToggleSection(Sections section, bool wasManual = true)
        {
            PhotosSectionSelection.SetActive(section == Sections.PHOTOS);
            MembersSectionSelection.SetActive(section == Sections.MEMBERS);
            PlacesSectionSelection.SetActive(section == Sections.PLACES);

            CameraReelGalleryConfigs.CameraReelGalleryView.transform.parent.gameObject.SetActive(section == Sections.PHOTOS);
            MembersListView.gameObject.SetActive(section == Sections.MEMBERS);

            SectionChanged?.Invoke(section, wasManual);
        }
    }
}
