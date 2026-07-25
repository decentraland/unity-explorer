using DCL.Audio;
using DCL.Chat;
using DCL.Clipboard;
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
        [SerializeField] private EmojiButtonView emojiButton = null!;
        [SerializeField] private EmojiPanelView emojiPanel = null!;
        [SerializeField] private EmojiPanelConfigurationSO emojiPanelConfiguration = null!;
        [SerializeField] private AudioClipConfig addEmojiAudio = null!;
        [SerializeField] private AudioClipConfig openEmojiPanelAudio = null!;
        [SerializeField] private InputSuggestionPanelView suggestionPanel = null!;
        [SerializeField] private Transform suggestionPanelParent = null!;
        [SerializeField] private ViewEventBus inputEventBus = null!;

        public event Action<string>? CreateAnnouncementButtonClicked;
        public event Action<bool>? InputFocusChanged;

        private string currentProfileThumbnailUrl = null!;
        private AnnouncementEmojiController? announcementEmojiController;
        private ClipboardManager? subscribedClipboardManager;

        private void Awake()
        {
            characterCounter.SetMaximumLength(announcementInput.characterLimit);

            announcementInput.onSelect.AddListener(OnAnnouncementInputSelected);
            announcementInput.onDeselect.AddListener(OnAnnouncementInputDeselected);
            announcementInput.onValueChanged.AddListener(OnAnnouncementInputValueChanged);
            announcementInput.PasteShortcutPerformed += OnAnnouncementInputPasteShortcut;
            createAnnouncementButton.onClick.AddListener(OnCreateAnnouncementButton);
            subscribedClipboardManager = ViewDependencies.ClipboardManager;
            subscribedClipboardManager.OnPaste += OnPasteClipboardText;
        }

        private void OnDestroy()
        {
            if (announcementInput != null)
            {
                announcementInput.onSelect.RemoveListener(OnAnnouncementInputSelected);
                announcementInput.onDeselect.RemoveListener(OnAnnouncementInputDeselected);
                announcementInput.onValueChanged.RemoveListener(OnAnnouncementInputValueChanged);
                announcementInput.PasteShortcutPerformed -= OnAnnouncementInputPasteShortcut;
            }
            if (createAnnouncementButton != null)
                createAnnouncementButton.onClick.RemoveListener(OnCreateAnnouncementButton);
            if (subscribedClipboardManager != null)
                subscribedClipboardManager.OnPaste -= OnPasteClipboardText;

            announcementEmojiController?.Dispose();
        }

        public void Configure(Profile? profile, ProfileRepositoryWrapper profileDataProvider)
        {
            UpdateCreateButtonState();
            UpdateCharacterCounter();

            if (profile != null && currentProfileThumbnailUrl != profile.Compact.FaceSnapshotUrl)
            {
                profilePicture.Setup(profileDataProvider, profile.UserNameColor, profile.Compact.FaceSnapshotUrl);
                currentProfileThumbnailUrl = profile.Compact.FaceSnapshotUrl;
            }

            announcementEmojiController ??= new AnnouncementEmojiController(
                announcementInput,
                emojiButton,
                emojiPanel,
                emojiPanelConfiguration,
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

        private void OnAnnouncementInputSelected(string _)
        {
            createAnnouncementInputOutline.SetActive(true);
            InputFocusChanged?.Invoke(true);
        }

        private void OnAnnouncementInputDeselected(string _)
        {
            createAnnouncementInputOutline.SetActive(false);
            InputFocusChanged?.Invoke(false);
        }

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
