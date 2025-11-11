using DCL.Chat;
using DCL.Emoji;
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
        [SerializeField] private CharacterCounterView characterCounter = null!;
        [SerializeField] internal EmojiButtonView emojiButton = null!;
        [SerializeField] internal EmojiPanelView emojiPanel = null!;

        public event Action<string>? CreateAnnouncementButtonClicked;

        private string currentProfileThumbnailUrl = null!;

        private void Awake()
        {
            announcementInput.onSelect.AddListener(OnAnnouncementInputSelected);
            announcementInput.onDeselect.AddListener(OnAnnouncementInputDeselected);
            announcementInput.onValueChanged.AddListener(OnAnnouncementInputValueChanged);
            createAnnouncementButton.onClick.AddListener(OnCreateAnnouncementButton);
            emojiButton.Button.onClick.AddListener(OnToggleEmojisPanel);

            characterCounter.SetMaximumLength(announcementInput.characterLimit);
        }

        private void OnDestroy()
        {
            announcementInput.onSelect.RemoveListener(OnAnnouncementInputSelected);
            announcementInput.onDeselect.RemoveListener(OnAnnouncementInputDeselected);
            announcementInput.onValueChanged.RemoveListener(OnAnnouncementInputValueChanged);
            createAnnouncementButton.onClick.RemoveListener(OnCreateAnnouncementButton);
            emojiButton.Button.onClick.RemoveListener(OnToggleEmojisPanel);
        }

        public void Configure(Profile? profile, ProfileRepositoryWrapper profileDataProvider)
        {
            UpdateCreateButtonState();
            UpdateCharacterCounter();

            if (profile != null && currentProfileThumbnailUrl != profile.Avatar.FaceSnapshotUrl)
            {
                profilePicture.Setup(profileDataProvider, profile.UserNameColor, profile.Avatar.FaceSnapshotUrl);
                currentProfileThumbnailUrl = profile.Avatar.FaceSnapshotUrl;
            }
        }

        public void CleanInput()
        {
            announcementInput.text = string.Empty;
            UpdateCharacterCounter();
        }

        private void OnAnnouncementInputSelected(string _) =>
            createAnnouncementInputOutline.SetActive(true);

        private void OnAnnouncementInputDeselected(string _) =>
            createAnnouncementInputOutline.SetActive(false);

        private void OnAnnouncementInputValueChanged(string text)
        {
            UpdateCreateButtonState();
            UpdateCharacterCounter();
        }

        private void OnCreateAnnouncementButton() =>
            CreateAnnouncementButtonClicked?.Invoke(announcementInput.text);

        private void OnToggleEmojisPanel()
        {
            emojiPanel.gameObject.SetActive(!emojiPanel.gameObject.activeSelf);
        }

        private void UpdateCreateButtonState() =>
            createAnnouncementButton.interactable = !string.IsNullOrEmpty(announcementInput.text);

        private void UpdateCharacterCounter() =>
            characterCounter.SetCharacterCount(announcementInput.text.Length);
    }
}
