using Cysharp.Threading.Tasks;
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
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Utility;

namespace DCL.Communities.CommunitiesCard.Announcements
{
    public class AnnouncementCreationCardView : MonoBehaviour
    {
        private static readonly Regex EMOJI_PATTERN_REGEX = new (@"(?<!https?:)(:\w{2,10})", RegexOptions.Compiled);
        private static readonly Regex PRE_MATCH_PATTERN_REGEX = new (@"(?<=^|\s)([@:]\S+)$", RegexOptions.Compiled);

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
        [SerializeField] internal InputSuggestionPanelView suggestionPanel = null!;
        [SerializeField] internal ViewEventBus inputEventBus = null!;

        public event Action<string>? CreateAnnouncementButtonClicked;

        private string currentProfileThumbnailUrl = null!;
        private EmojiPanelPresenter emojiPanelPresenter = null!;
        private InputSuggestionPanelController suggestionPanelController = null!;
        private Dictionary<string, EmojiInputSuggestionData> emojiSuggestionsDictionary = null!;
        private int wordMatchIndex;
        private Match lastMatch = Match.Empty;
        private readonly EventSubscriptionScope eventsScope = new ();

        private void Awake()
        {
            characterCounter.SetMaximumLength(announcementInput.characterLimit);

            EmojiMapping emojiMapping = new EmojiMapping(emojiPanelConfiguration);

            emojiPanelPresenter = new EmojiPanelPresenter(
                emojiPanel,
                emojiPanelConfiguration,
                emojiMapping,
                emojiSectionViewPrefab,
                emojiButtonPrefab
            );

            suggestionPanelController = new InputSuggestionPanelController(suggestionPanel);

            emojiSuggestionsDictionary = new Dictionary<string, EmojiInputSuggestionData>(emojiMapping.NameMapping.Count);
            foreach (KeyValuePair<string, EmojiData> pair in emojiMapping.NameMapping)
                emojiSuggestionsDictionary.Add(pair.Key, new EmojiInputSuggestionData(pair.Value.EmojiCode, pair.Value.EmojiName));

            announcementInput.onSelect.AddListener(OnAnnouncementInputSelected);
            announcementInput.onDeselect.AddListener(OnAnnouncementInputDeselected);
            announcementInput.onValueChanged.AddListener(OnAnnouncementInputValueChanged);
            announcementInput.onValidateInput += OnAnnouncementInputValidateInput;
            announcementInput.PasteShortcutPerformed += OnAnnouncementInputPasteShortcut;
            createAnnouncementButton.onClick.AddListener(OnCreateAnnouncementButton);
            emojiButton.Button.onClick.AddListener(OnOpenEmojisPanel);
            emojiPanelPresenter.EmojiSelected += OnEmojiSelected;
            DCLInput.Instance.UI.Click.performed += OnUIClicked;
            ViewDependencies.ClipboardManager.OnPaste += OnPasteClipboardText;
        }

        private void OnEnable() =>
            eventsScope.Add(inputEventBus.Subscribe<InputSuggestionsEvents.SuggestionSelectedEvent>(ReplaceSuggestionInText));

        private void OnDestroy()
        {
            announcementInput.onSelect.RemoveListener(OnAnnouncementInputSelected);
            announcementInput.onDeselect.RemoveListener(OnAnnouncementInputDeselected);
            announcementInput.onValueChanged.RemoveListener(OnAnnouncementInputValueChanged);
            announcementInput.PasteShortcutPerformed -= OnAnnouncementInputPasteShortcut;
            createAnnouncementButton.onClick.RemoveListener(OnCreateAnnouncementButton);
            emojiButton.Button.onClick.RemoveListener(OnOpenEmojisPanel);
            emojiPanelPresenter.EmojiSelected -= OnEmojiSelected;
            DCLInput.Instance.UI.Click.performed -= OnUIClicked;
            ViewDependencies.ClipboardManager.OnPaste -= OnPasteClipboardText;

            emojiPanelPresenter.Dispose();
            suggestionPanelController.Dispose();
            eventsScope.Dispose();
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

            Match wordMatch = PRE_MATCH_PATTERN_REGEX.Match(text, 0, announcementInput.stringPosition);

            lastMatch = Match.Empty;
            if (wordMatch.Success)
            {
                wordMatchIndex = wordMatch.Index;
                lastMatch = suggestionPanelController.HandleSuggestionsSearch(wordMatch.Value, EMOJI_PATTERN_REGEX, InputSuggestionType.EMOJIS, emojiSuggestionsDictionary);
            }

            suggestionPanelController.SetPanelVisibility(lastMatch.Success);
        }

        private char OnAnnouncementInputValidateInput(string text, int charIndex, char addedChar)
        {
            if (addedChar is '\n' or '\r' && suggestionPanel.gameObject.activeSelf)
            {
                suggestionPanelController.SetPanelVisibility(false);
                return '\0';
            }

            return addedChar;
        }

        private void OnAnnouncementInputPasteShortcut() =>
            ViewDependencies.ClipboardManager.Paste(this);

        private void ReplaceSuggestionInText(InputSuggestionsEvents.SuggestionSelectedEvent suggestion)
        {
            if (!lastMatch.Success)
                return;

            if (!announcementInput.IsWithinCharacterLimit(suggestion.Id.Length - lastMatch.Groups[1].Length))
                return;

            UIAudioEventsBus.Instance.SendPlayAudioEvent(addEmojiAudio);
            int replaceAmount = lastMatch.Groups[1].Length;
            int replaceAt = wordMatchIndex + lastMatch.Groups[1].Index;

            announcementInput.ReplaceTextAtPosition(replaceAt, replaceAmount, suggestion.Id);

            DeactivateSuggestionsNextFrameAsync().Forget();
            return;

            async UniTaskVoid DeactivateSuggestionsNextFrameAsync(CancellationToken ct = default)
            {
                await UniTask.NextFrame(ct);
                suggestionPanelController.SetPanelVisibility(false);
            }
        }

        private void OnCreateAnnouncementButton() =>
            CreateAnnouncementButtonClicked?.Invoke(announcementInput.text);

        private void UpdateCharacterCounter() =>
            characterCounter.SetCharacterCount(announcementInput.text.Length);

        private void UpdateCreateButtonState() =>
            createAnnouncementButton.interactable = !string.IsNullOrEmpty(announcementInput.text);

        private void OnOpenEmojisPanel()
        {
            if (emojiPanel.gameObject.activeSelf) return;
            SetEmojiPanelVisibility(true);
        }

        private void OnEmojiSelected(string emoji)
        {
            UIAudioEventsBus.Instance.SendPlayAudioEvent(addEmojiAudio);
            if (!announcementInput.IsWithinCharacterLimit(emoji.Length)) return;
            announcementInput.InsertTextAtCaretPosition(emoji);
        }

        private void OnUIClicked(InputAction.CallbackContext context)
        {
            var clickPosition = GetPointerPosition(context);
            bool isClickedOutsideEmojiPanel = RectTransformUtility.RectangleContainsScreenPoint((RectTransform)emojiPanel.transform, clickPosition, null);
            if (!isClickedOutsideEmojiPanel)
                SetEmojiPanelVisibility(false);
        }

        private static Vector2 GetPointerPosition(InputAction.CallbackContext context)
        {
            if (context.control is Pointer pCtrl) return pCtrl.position.ReadValue();
            if (Pointer.current != null) return Pointer.current.position.ReadValue();
            if (Mouse.current != null) return Mouse.current.position.ReadValue();
            if (Touchscreen.current?.primaryTouch != null) return Touchscreen.current.primaryTouch.position.ReadValue();
            return Vector2.zero;
        }

        private void SetEmojiPanelVisibility(bool isVisible)
        {
            emojiPanelPresenter.SetPanelVisibility(isVisible);
            emojiPanel.EmojiContainer.gameObject.SetActive(isVisible);
            emojiButton.SetState(isVisible);
        }

        private void OnPasteClipboardText(object sender, string pastedText) =>
            announcementInput.InsertTextAtCaretPosition(pastedText);
    }
}
