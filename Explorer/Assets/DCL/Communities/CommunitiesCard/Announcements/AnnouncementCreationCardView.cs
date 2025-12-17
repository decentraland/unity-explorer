using DCL.Audio;
using DCL.Chat;
using DCL.Emoji;
using DCL.Profiles;
using DCL.UI.CustomInputField;
using DCL.UI.ProfileElements;
using DCL.UI.Profiles.Helpers;
using DCL.UI.SuggestionPanel;
using MVC;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Communities.CommunitiesCard.Announcements
{
    public class AnnouncementCreationCardView : MonoBehaviour
    {
        [SerializeField] private ProfilePictureView profilePicture = null!;
        [SerializeField] private CustomInputField announcementInput = null!;
        [SerializeField] private Button createAnnouncementButton = null!;
        [SerializeField] private TMP_Text createAnnouncementButtonText = null!;
        [SerializeField] private GameObject createAnnouncementButtonLoadingSpinner = null!;
        [SerializeField] private GameObject createAnnouncementInputOutline = null!;
        [SerializeField] private CharacterCounterView characterCounter = null!;
        [SerializeField] private TMP_Text characterCounterText = null!;

        [Header("Emoji Panel Configuration")]
        [SerializeField] internal EmojiButtonView emojiButton = null!;
        [SerializeField] internal EmojiPanelView emojiPanel = null!;
        [SerializeField] internal EmojiPanelConfigurationSO emojiPanelConfiguration = null!;
        [SerializeField] internal EmojiSectionView emojiSectionViewPrefab = null!;
        [SerializeField] internal EmojiButton emojiButtonPrefab = null!;
        [SerializeField] internal AudioClipConfig addEmojiAudio = null!;
        [SerializeField] internal AudioClipConfig openEmojiPanelAudio = null!;
        [SerializeField] internal InputSuggestionPanelView suggestionPanel = null!;
        [SerializeField] internal Transform suggestionPanelParent = null!;
        [SerializeField] internal ViewEventBus inputEventBus = null!;

        public event Action<string>? CreateAnnouncementButtonClicked;

        private string currentProfileThumbnailUrl = null!;
        private AnnouncementEmojiController? announcementEmojiController;

        private void Awake()
        {
            characterCounter.SetMaximumLength(announcementInput.characterLimit);

            announcementInput.onSelect.AddListener(OnAnnouncementInputSelected);
            announcementInput.onDeselect.AddListener(OnAnnouncementInputDeselected);
            announcementInput.onValueChanged.AddListener(OnAnnouncementInputValueChanged);
            announcementInput.PasteShortcutPerformed += OnAnnouncementInputPasteShortcut;
            createAnnouncementButton.onClick.AddListener(OnCreateAnnouncementButton);
            ViewDependencies.ClipboardManager.OnPaste += OnPasteClipboardText;
        }

        private void OnDestroy()
        {
            announcementInput.onSelect.RemoveListener(OnAnnouncementInputSelected);
            announcementInput.onDeselect.RemoveListener(OnAnnouncementInputDeselected);
            announcementInput.onValueChanged.RemoveListener(OnAnnouncementInputValueChanged);
            announcementInput.PasteShortcutPerformed -= OnAnnouncementInputPasteShortcut;
            createAnnouncementButton.onClick.RemoveListener(OnCreateAnnouncementButton);
            ViewDependencies.ClipboardManager.OnPaste -= OnPasteClipboardText;

            announcementEmojiController?.Dispose();
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

            announcementEmojiController ??= new AnnouncementEmojiController(
                announcementInput,
                emojiButton,
                emojiPanel,
                emojiPanelConfiguration,
                emojiSectionViewPrefab,
                emojiButtonPrefab,
                addEmojiAudio,
                openEmojiPanelAudio,
                suggestionPanel,
                suggestionPanelParent,
                inputEventBus);
        }

        public void CleanInput()
        {
            announcementInput.text = string.Empty;
            UpdateCharacterCounter();
        }

        public void SetAsLoading(bool isLoading)
        {
            if (isLoading)
                createAnnouncementButton.interactable = false;
            else
                UpdateCreateButtonState();

            createAnnouncementButtonText.gameObject.SetActive(!isLoading);
            createAnnouncementButtonLoadingSpinner.SetActive(isLoading);
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

        private void OnCreateAnnouncementButton() =>
            CreateAnnouncementButtonClicked?.Invoke(announcementInput.text);

        private void UpdateCharacterCounter()
        {
            characterCounter.SetCharacterCount(announcementInput.text.Length);
            characterCounterText.text = $"{announcementInput.text.Length}/{announcementInput.characterLimit}";
            characterCounterText.gameObject.SetActive(announcementInput.text.Length > 0);
        }

        private void UpdateCreateButtonState() =>
            createAnnouncementButton.interactable = !string.IsNullOrEmpty(announcementInput.text);

        private void OnPasteClipboardText(object sender, string pastedText) =>
            announcementInput.InsertTextAtCaretPosition(pastedText);
    }
}
