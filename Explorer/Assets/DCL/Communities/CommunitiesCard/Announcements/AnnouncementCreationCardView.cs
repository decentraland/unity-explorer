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

        private string currentProfileThumbnailUrl = null!;

        private void Awake()
        {
            announcementInput.onSelect.AddListener(OnAnnouncementInputSelected);
            announcementInput.onDeselect.AddListener(OnAnnouncementInputDeselected);
            announcementInput.onValueChanged.AddListener(OnAnnouncementInputValueChanged);
            createAnnouncementButton.onClick.AddListener(OnCreateAnnouncementButton);
        }

        private void OnDestroy()
        {
            announcementInput.onSelect.RemoveListener(OnAnnouncementInputSelected);
            announcementInput.onDeselect.RemoveListener(OnAnnouncementInputDeselected);
            announcementInput.onValueChanged.RemoveListener(OnAnnouncementInputValueChanged);
            createAnnouncementButton.onClick.RemoveListener(OnCreateAnnouncementButton);
        }

        public void Configure(Profile? profile, ProfileRepositoryWrapper profileDataProvider)
        {
            announcementInput.text = string.Empty;
            UpdateCreateButtonState();

            if (profile != null && currentProfileThumbnailUrl != profile.Avatar.FaceSnapshotUrl)
            {
                profilePicture.Setup(profileDataProvider, profile.UserNameColor, profile.Avatar.FaceSnapshotUrl);
                currentProfileThumbnailUrl = profile.Avatar.FaceSnapshotUrl;
            }
        }

        private void OnAnnouncementInputSelected(string _) =>
            createAnnouncementInputOutline.SetActive(true);

        private void OnAnnouncementInputDeselected(string _) =>
            createAnnouncementInputOutline.SetActive(false);

        private void OnAnnouncementInputValueChanged(string text) =>
            UpdateCreateButtonState();

        private void OnCreateAnnouncementButton() =>
            CreateAnnouncementButtonClicked?.Invoke(announcementInput.text);

        private void UpdateCreateButtonState() =>
            createAnnouncementButton.interactable = !string.IsNullOrEmpty(announcementInput.text);
    }
}
