using Cysharp.Threading.Tasks;
using DCL.Audio;
using DCL.Chat;
using DCL.Emoji;
using DCL.UI.CustomInputField;
using DCL.UI.SuggestionPanel;
using MVC;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using UnityEngine;
using UnityEngine.InputSystem;
using Utility;

namespace DCL.Communities.CommunitiesCard.Announcements
{
    public class AnnouncementEmojiController
    {
        private static readonly Regex EMOJI_PATTERN_REGEX = new (@"(?<!https?:)(:\w{2,10})", RegexOptions.Compiled);
        private static readonly Regex PRE_MATCH_PATTERN_REGEX = new (@"(?<=^|\s)([@:]\S+)$", RegexOptions.Compiled);

        private readonly CustomInputField announcementInput;
        private readonly EmojiButtonView emojiButton;
        private readonly EmojiPanelView emojiPanel;
        private readonly AudioClipConfig addEmojiAudio;
        private readonly AudioClipConfig openEmojiPanelAudio;
        private readonly InputSuggestionPanelView suggestionPanel;
        private readonly EmojiPanelPresenter emojiPanelPresenter;
        private readonly InputSuggestionPanelController suggestionPanelController;
        private readonly Dictionary<string, EmojiInputSuggestionData> emojiSuggestionsDictionary;

        private int wordMatchIndex;
        private Match lastMatch = Match.Empty;
        private readonly EventSubscriptionScope eventsScope = new ();

        public AnnouncementEmojiController(
            CustomInputField announcementInput,
            EmojiButtonView emojiButton,
            EmojiPanelView emojiPanel,
            EmojiPanelConfigurationSO emojiPanelConfiguration,
            EmojiSectionView emojiSectionViewPrefab,
            EmojiButton emojiButtonPrefab,
            AudioClipConfig addEmojiAudio,
            AudioClipConfig openEmojiPanelAudio,
            InputSuggestionPanelView suggestionPanel,
            Transform suggestionPanelParent,
            ViewEventBus inputEventBus)
        {
            this.announcementInput = announcementInput;
            this.emojiButton = emojiButton;
            this.emojiPanel = emojiPanel;
            this.addEmojiAudio = addEmojiAudio;
            this.openEmojiPanelAudio = openEmojiPanelAudio;
            this.suggestionPanel = suggestionPanel;

            if (suggestionPanelParent != null)
                this.suggestionPanel.transform.SetParent(suggestionPanelParent);

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

            eventsScope.Add(inputEventBus.Subscribe<InputSuggestionsEvents.SuggestionSelectedEvent>(ReplaceSuggestionInText));

            announcementInput.onValueChanged.AddListener(OnAnnouncementInputValueChanged);
            announcementInput.onValidateInput += OnAnnouncementInputValidateInput;
            emojiButton.Button.onClick.AddListener(OnOpenEmojisPanel);
            emojiPanelPresenter.EmojiSelected += OnEmojiSelected;
            DCLInput.Instance.UI.Click.performed += OnUIClicked;
        }

        public void Dispose()
        {
            announcementInput.onValueChanged.RemoveListener(OnAnnouncementInputValueChanged);
            announcementInput.onValidateInput -= OnAnnouncementInputValidateInput;
            emojiButton.Button.onClick.RemoveListener(OnOpenEmojisPanel);
            emojiPanelPresenter.EmojiSelected -= OnEmojiSelected;
            DCLInput.Instance.UI.Click.performed -= OnUIClicked;

            emojiPanelPresenter.Dispose();
            suggestionPanelController.Dispose();
            eventsScope.Dispose();
        }

        private void OnAnnouncementInputValueChanged(string text)
        {
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

        private void OnOpenEmojisPanel()
        {
            if (emojiPanel.gameObject.activeSelf)
                return;

            SetEmojiPanelVisibility(true);
        }

        private void OnEmojiSelected(string emoji)
        {
            UIAudioEventsBus.Instance.SendPlayAudioEvent(addEmojiAudio);

            if (!announcementInput.IsWithinCharacterLimit(emoji.Length))
                return;

            announcementInput.InsertTextAtCaretPosition(emoji);
        }

        private void OnUIClicked(InputAction.CallbackContext context)
        {
            if (!context.control.IsPressed())
                return;

            var clickPosition = GetPointerPosition(context);
            bool isClickedInsideEmojiPanel = RectTransformUtility.RectangleContainsScreenPoint((RectTransform)emojiPanel.transform, clickPosition, null);
            if (!isClickedInsideEmojiPanel)
                SetEmojiPanelVisibility(false);
        }

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

            if (isVisible)
                UIAudioEventsBus.Instance.SendPlayAudioEvent(openEmojiPanelAudio);
        }
    }
}
