using DCL.Communities.CommunitiesCard.Members;
using DCL.Friends.UI.FriendPanel.Sections;
using DCL.InWorldCamera.CameraReelGallery;
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

        public event Action<Sections, bool>? SectionChanged;
        public event Action? OpenWizard;

        [field: Header("References")]
        [field: SerializeField] public Button CloseButton { get; private set; }
        [field: SerializeField] public Button BackgroundCloseButton { get; private set; }
        [field: SerializeField] public SectionLoadingView LoadingObject { get; private set; }

        [field: Header("Community interactions")]
        [field: SerializeField] public Button OpenWizardButton { get; private set; }
        [field: SerializeField] public Button JoinedButton { get; private set; }
        [field: SerializeField] public Button JoinButton { get; private set; }

        [field: Header("Community data references")]
        [field: SerializeField] public TMP_Text CommunityName { get; private set; }
        [field: SerializeField] public TMP_Text CommunityMembersNumber { get; private set; }
        [field: SerializeField] public TMP_Text CommunityDescription { get; private set; }

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
        }

        public void ToggleUIListeners(bool active)
        {
            if (active)
            {
                PhotosButton.onClick.AddListener(() => ToggleSection(Sections.PHOTOS));
                MembersButton.onClick.AddListener(() => ToggleSection(Sections.MEMBERS));
                PlacesButton.onClick.AddListener(() => ToggleSection(Sections.PLACES));

                ToggleSection(Sections.PHOTOS, false);
            }
            else
            {
                PhotosButton.onClick.RemoveAllListeners();
                MembersButton.onClick.RemoveAllListeners();
                PlacesButton.onClick.RemoveAllListeners();
            }
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

        public void ConfigureCommunity(GetCommunityResponse.CommunityData communityData)
        {
            CommunityName.text = communityData.name;
            CommunityMembersNumber.text = string.Format(COMMUNITY_MEMBERS_NUMBER_FORMAT, NumberToCompactString(communityData.membersCount));
            CommunityDescription.text = communityData.description;

            JoinedButton.gameObject.SetActive(communityData.role is CommunityMemberRole.Member or CommunityMemberRole.Moderator);
            OpenWizardButton.gameObject.SetActive(communityData.role is CommunityMemberRole.Owner);
            JoinButton.gameObject.SetActive(communityData.role == CommunityMemberRole.None);

        }
    }
}
