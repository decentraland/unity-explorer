using DCL.Audio;
using DCL.Chat;
using DCL.Emoji;
using DCL.Profiles;
using DCL.UI.CustomInputField;
using DCL.UI.ProfileElements;
using DCL.UI.Profiles.Helpers;
using MVC;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Communities.CommunitiesCard.Announcements
{
    public class AnnouncementCreationCardView : MonoBehaviour
    {
        [SerializeField] private ProfilePictureView profilePicture = null!;
        [SerializeField] private CustomInputField announcementInput = null!;
        [SerializeField] private Button createAnnouncementButton = null!;
        [SerializeField] private GameObject createAnnouncementInputOutline = null!;
        [SerializeField] private CharacterCounterView characterCounter = null!;

        [Header("Emoji Panel Configuration")]
        [SerializeField] internal EmojiButtonView emojiButton = null!;
        [SerializeField] internal EmojiPanelView emojiPanel = null!;
        [SerializeField] internal EmojiPanelConfigurationSO emojiPanelConfiguration = null!;
        [SerializeField] internal EmojiSectionView emojiSectionViewPrefab = null!;
        [SerializeField] internal EmojiButton emojiButtonPrefab = null!;
        [SerializeField] internal AudioClipConfig addEmojiAudio = null!;
        [SerializeField] internal AudioClipConfig openEmojiPanelAudio = null!;

        public event Action<string>? CreateAnnouncementButtonClicked;

        private string currentProfileThumbnailUrl = null!;
        private EmojiPanelPresenter emojiPanelPresenter = null!;

        private void Awake()
        {
            characterCounter.SetMaximumLength(announcementInput.characterLimit);

            emojiPanelPresenter = new EmojiPanelPresenter(
                emojiPanel,
                emojiPanelConfiguration,
                new EmojiMapping(emojiPanelConfiguration),
                emojiSectionViewPrefab,
                emojiButtonPrefab
            );

            announcementInput.onSelect.AddListener(OnAnnouncementInputSelected);
            announcementInput.onDeselect.AddListener(OnAnnouncementInputDeselected);
            announcementInput.onValueChanged.AddListener(OnAnnouncementInputValueChanged);
            createAnnouncementButton.onClick.AddListener(OnCreateAnnouncementButton);
            emojiButton.Button.onClick.AddListener(OnToggleEmojisPanel);
            announcementInput.PasteShortcutPerformed += OnAnnouncementInputPasteShortcut;
            ViewDependencies.ClipboardManager.OnPaste += OnPasteClipboardText;
            emojiPanelPresenter.EmojiSelected += OnEmojiSelected;
        }

        private void OnDestroy()
        {
            announcementInput.onSelect.RemoveListener(OnAnnouncementInputSelected);
            announcementInput.onDeselect.RemoveListener(OnAnnouncementInputDeselected);
            announcementInput.onValueChanged.RemoveListener(OnAnnouncementInputValueChanged);
            createAnnouncementButton.onClick.RemoveListener(OnCreateAnnouncementButton);
            emojiButton.Button.onClick.RemoveListener(OnToggleEmojisPanel);
            announcementInput.PasteShortcutPerformed -= OnAnnouncementInputPasteShortcut;
            ViewDependencies.ClipboardManager.OnPaste -= OnPasteClipboardText;
            emojiPanelPresenter.EmojiSelected -= OnEmojiSelected;
            emojiPanelPresenter.Dispose();
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

        private void OnAnnouncementInputPasteShortcut() =>
            ViewDependencies.ClipboardManager.Paste(this);

        private void OnPasteClipboardText(object sender, string pastedText) =>
            announcementInput.InsertTextAtCaretPosition(pastedText);

        private void OnCreateAnnouncementButton() =>
            CreateAnnouncementButtonClicked?.Invoke(announcementInput.text);

        private void UpdateCharacterCounter() =>
            characterCounter.SetCharacterCount(announcementInput.text.Length);

        private void UpdateCreateButtonState() =>
            createAnnouncementButton.interactable = !string.IsNullOrEmpty(announcementInput.text);

        private void OnToggleEmojisPanel()
        {
            emojiPanelPresenter.SetPanelVisibility(!emojiPanel.gameObject.activeSelf);
            emojiButton.SetState(emojiPanel.gameObject.activeSelf);
            emojiPanel.EmojiContainer.gameObject.SetActive(emojiPanel.gameObject.activeSelf);
        }

        private void OnEmojiSelected(string emoji)
        {
            UIAudioEventsBus.Instance.SendPlayAudioEvent(addEmojiAudio);
            if (!announcementInput.IsWithinCharacterLimit(emoji.Length)) return;
            announcementInput.InsertTextAtCaretPosition(emoji);
        }
    }
}
