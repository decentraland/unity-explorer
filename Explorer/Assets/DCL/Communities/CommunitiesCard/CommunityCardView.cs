using DCL.Communities.CommunitiesCard.Members;
using DCL.Communities.CommunitiesCard.Places;
using DCL.Friends.UI.FriendPanel.Sections;
using DCL.InWorldCamera.CameraReelGallery;
using DCL.UI;
using DG.Tweening;
using MVC;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Communities.CommunitiesCard
{
    public class CommunityCardView : ViewBase, IView
    {
        private const string COMMUNITY_MEMBERS_NUMBER_FORMAT = "<b>{0}</b> members";

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

        public event Action<Sections>? SectionChanged;
        public event Action? OpenWizard;
        public event Action? JoinCommunity;
        public event Action? LeaveCommunityRequested;

        [field: Header("References")]
        [field: SerializeField] public Button CloseButton { get; private set; }
        [field: SerializeField] public Button BackgroundCloseButton { get; private set; }
        [field: SerializeField] public SectionLoadingView LoadingObject { get; private set; }
        [field: SerializeField] public Image BackgroundImage { get; private set; }
        [field: SerializeField] public Color BackgroundColor { get; private set; }
        [field: SerializeField] public ConfirmationDialogView ConfirmationDialogView { get; private set; }

        [field: Header("Community interactions")]
        [field: SerializeField] public Button OpenWizardButton { get; private set; }
        [field: SerializeField] public Button JoinedButton { get; private set; }
        [field: SerializeField] public Button JoinButton { get; private set; }

        [field: Header("Community data references")]
        [field: SerializeField] public TMP_Text CommunityName { get; private set; }
        [field: SerializeField] public TMP_Text CommunityMembersNumber { get; private set; }
        [field: SerializeField] public TMP_Text CommunityDescription { get; private set; }
        [field: SerializeField] public ImageView CommunityThumbnail { get; private set; }

        [field: Header("-- Sections")]
        [field: Header("Buttons")]
        [field: SerializeField] public Button PhotosButton { get; private set; }
        [field: SerializeField] public Button MembersButton { get; private set; }
        [field: SerializeField] public Button PlacesButton { get; private set; }
        [field: SerializeField] public Button PlacesWithSignButton { get; private set; }
        [field: SerializeField] public Button MembersTextButton { get; private set; }

        [field: Header("Selections")]
        [field: SerializeField] public GameObject PhotosSectionSelection { get; private set; }
        [field: SerializeField] public GameObject MembersSectionSelection { get; private set; }
        [field: SerializeField] public GameObject PlacesSectionSelection { get; private set; }
        [field: SerializeField] public GameObject PlacesWithSignSectionSelection { get; private set; }

        [field: Header("Sections views")]
        [field: SerializeField] public CameraReelGalleryConfig CameraReelGalleryConfigs { get; private set; }
        [field: SerializeField] public MembersListView MembersListView { get; private set; }
        [field: SerializeField] public PlacesSectionView PlacesSectionView { get; private set; }

        private static string NumberToCompactString(long number)
        {
            return number switch
                   {
                       >= 1_000_000_000 => (number / 1_000_000_000D).ToString("0.#") + "B",
                       >= 1_000_000 => (number / 1_000_000D).ToString("0.#") + "M",
                       >= 1_000 => (number / 1_000D).ToString("0.#") + "k",
                       _ => number.ToString()
                   };
        }

        private void Awake()
        {
            OpenWizardButton.onClick.AddListener(() => OpenWizard?.Invoke());
            JoinButton.onClick.AddListener(() => JoinCommunity?.Invoke());
            JoinedButton.onClick.AddListener(() => LeaveCommunityRequested?.Invoke());
        }

        public void ToggleUIListeners(bool active)
        {
            if (active)
            {
                PhotosButton.onClick.AddListener(() => ToggleSection(Sections.PHOTOS));
                MembersButton.onClick.AddListener(() => ToggleSection(Sections.MEMBERS));
                MembersTextButton.onClick.AddListener(() => ToggleSection(Sections.MEMBERS));
                PlacesButton.onClick.AddListener(() => ToggleSection(Sections.PLACES));
                PlacesWithSignButton.onClick.AddListener(() => ToggleSection(Sections.PLACES));

                ToggleSection(Sections.PHOTOS);
            }
            else
            {
                PhotosButton.onClick.RemoveAllListeners();
                MembersButton.onClick.RemoveAllListeners();
                MembersTextButton.onClick.RemoveAllListeners();
                PlacesButton.onClick.RemoveAllListeners();
                PlacesWithSignButton.onClick.RemoveAllListeners();
            }
        }

        public void SetLoadingState(bool isLoading)
        {
            if (isLoading)
                LoadingObject?.Show();
            else
                LoadingObject?.Hide();
        }

        private void ToggleSection(Sections section)
        {
            PhotosSectionSelection.SetActive(section == Sections.PHOTOS);
            MembersSectionSelection.SetActive(section == Sections.MEMBERS);
            PlacesSectionSelection.SetActive(section == Sections.PLACES);
            PlacesWithSignSectionSelection.SetActive(section == Sections.PLACES);

            CameraReelGalleryConfigs.CameraReelGalleryView.transform.parent.gameObject.SetActive(section == Sections.PHOTOS);
            MembersListView.gameObject.SetActive(section == Sections.MEMBERS);
            PlacesSectionView.gameObject.SetActive(section == Sections.PLACES);

            SectionChanged?.Invoke(section);
        }

        public void ConfigureInteractionButtons(CommunityMemberRole role)
        {
            JoinedButton.gameObject.SetActive(role is CommunityMemberRole.member or CommunityMemberRole.moderator);
            OpenWizardButton.gameObject.SetActive(role is CommunityMemberRole.owner);
            JoinButton.gameObject.SetActive(role == CommunityMemberRole.none);
        }

        public void ConfigureCommunity(GetCommunityResponse.CommunityData communityData, ImageController imageController)
        {
            CommunityName.text = communityData.name;
            CommunityMembersNumber.text = string.Format(COMMUNITY_MEMBERS_NUMBER_FORMAT, NumberToCompactString(communityData.membersCount));
            CommunityDescription.text = communityData.description;
            imageController.RequestImage(communityData.thumbnails[0], true, true);

            ConfigureInteractionButtons(communityData.role);

            PlacesWithSignButton.gameObject.SetActive(communityData.role is CommunityMemberRole.owner or CommunityMemberRole.moderator);
            PlacesButton.gameObject.SetActive(!PlacesWithSignButton.gameObject.activeSelf);
        }
    }
}
