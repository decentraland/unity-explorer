using DCL.Profiles;
using DCL.UI.ProfileElements;
using DCL.UI.Profiles.Helpers;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Communities.CommunitiesCard.Announcements
{
    public class AnnouncementCreationCardView : MonoBehaviour
    {
        [SerializeField] private ProfilePictureView profilePicture = null!;
        [SerializeField] private TMP_InputField announcementInput = null!;
        [SerializeField] private Button createAnnouncementButton = null!;
        [SerializeField] private GameObject createAnnouncementInputOutline = null!;

        public event Action<string>? CreateAnnouncementButtonClicked;

        private void Awake()
        {
            announcementInput.onSelect.AddListener(AnnouncementInputSelected);
            announcementInput.onDeselect.AddListener(AnnouncementInputSelectedDeselected);
            createAnnouncementButton.onClick.AddListener(OnCreateAnnouncementButton);
        }

        private void OnDestroy() =>
            createAnnouncementButton.onClick.RemoveListener(OnCreateAnnouncementButton);

        public void Configure(Profile? profile, ProfileRepositoryWrapper profileDataProvider)
        {
            announcementInput.text = string.Empty;

            if (profile != null)
                profilePicture.Setup(profileDataProvider, profile.UserNameColor, profile.Avatar.FaceSnapshotUrl);
        }

        private void AnnouncementInputSelected(string arg0) =>
            createAnnouncementInputOutline.SetActive(true);

        private void AnnouncementInputSelectedDeselected(string arg0) =>
            createAnnouncementInputOutline.SetActive(false);

        private void OnCreateAnnouncementButton() =>
            CreateAnnouncementButtonClicked?.Invoke(announcementInput.text);
    }
}
