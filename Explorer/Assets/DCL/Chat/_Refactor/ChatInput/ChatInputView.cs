using DCL.Audio;
using DCL.Emoji;
using DCL.UI.CustomInputField;
using DCL.UI.InputFieldFormatting;
using DCL.UI.SuggestionPanel;
using MVC;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Chat.ChatInput
{
    public class ChatInputView : MonoBehaviour
    {
        public event Action? DebugOnSubmit;

        [Serializable]
        public class EmojiContainer
        {
            [field: SerializeField] internal EmojiPanelConfigurationSO emojiPanelConfiguration { get; private set; } = null!;
            [field: SerializeField] internal EmojiButtonView emojiPanelButton { get; private set; } = null!;
            [field: SerializeField] internal EmojiPanelView emojiPanel { get; private set; } = null!;
            [field: SerializeField] internal AudioClipConfig addEmojiAudio { get; private set; } = null!;
            [field: SerializeField] internal AudioClipConfig openEmojiPanelAudio { get; private set; } = null!;

            [field: SerializeField]
            [field: Tooltip("Space kept between the input field's top edge and the emoji panel's bottom edge.")]
            internal float emojiPanelGap { get; private set; } = 5f;
        }

        [field: SerializeField] public CustomInputField inputField { get; private set; } = null!;
        [field: SerializeField] internal RectTransform pastePopupPosition { get; private set; } = null!;

        [SerializeField] private GameObject inputFieldContainer = null!;
        [SerializeField] private LayoutElement layoutElement = null!;

        [field: Header("Blocked")]
        [field: SerializeField] internal Button maskButton { get; private set; } = null!;
        [SerializeField] private GameObject maskContainer = null!;
        [SerializeField] private TMP_Text maskText = null!;

        [Header("Suggestion Panel")]
        [field: SerializeField] internal InputSuggestionPanelView suggestionPanel { get; private set; } = null!;

        [Header("Focus Visuals")]
        [SerializeField] private GameObject outlineObject = null!;
        [SerializeField] private GameObject characterCounterObject = null!;
        [SerializeField] private CharacterCounterView characterCounter = null!;
        [SerializeField] private GameObject emojiButtonObject = null!;
        [SerializeField] private TextMeshProUGUI inputPlaceholderObject = null!;
        [SerializeField] private Color focusedBackgroundColor;
        [SerializeField] private Color unfocusedBackgroundColor;

        [field: Header("Emojis")]
        [field: SerializeField] internal EmojiContainer emojiContainer { get; private set; } = null!;

        [field: Header("Audio")]
        [field: SerializeField] internal AudioClipConfig chatInputTextAudio { get; private set; } = null!;
        [field: SerializeField] internal AudioClipConfig enterInputAudio { get; private set; } = null!;

        [field: Header("Event Bus")]
        [field: SerializeField] internal ViewEventBus inputEventBus { get; private set; } = null!;

        private ChatConfig.ChatConfig chatConfig = null!;

        public void ApplyFocusStyle()
        {
            outlineObject.SetActive(true);
            characterCounterObject.SetActive(true);
            emojiButtonObject.SetActive(true);
            inputPlaceholderObject.text = chatConfig.InputFocusedMessages;
        }

        private void ApplyUnfocusStyle()
        {
            outlineObject.SetActive(false);
            characterCounterObject.SetActive(false);
            emojiButtonObject.SetActive(false);
            inputPlaceholderObject.text = chatConfig.InputUnfocusedMessages;

            // NOTE: Clear text when it's a single "/" which is used to trigger commands.
            // NOTE: This prevents storing incomplete command triggers as normal messages.
            if (inputField.text.Length <= 1 &&
                (inputField.text.Length != 1 || inputField.text[0] == '/'))
            {
                inputField.text = string.Empty;
            }

            inputField.DeactivateInputField();
        }

        public void Initialize(ChatConfig.ChatConfig config, ITextFormatter textFormatter)
        {
            characterCounter.SetMaximumLength(inputField.characterLimit);
            chatConfig = config;
            inputField.SetTextFormatter(textFormatter);
        }

        public void InsertTextAtCaretPosition(string text)
        {
            inputField.InsertTextAtCaretPosition(text);
            characterCounter.SetCharacterCount(inputField.text.Length);
        }

        public void ClearAndInsertText(string text)
        {
            inputField.SetTextWithoutNotify("");
            inputField.InsertTextAtCaretPosition(text);
            characterCounter.SetCharacterCount(inputField.text.Length);
        }

        public void UpdateCharacterCount()
        {
            characterCounter.SetCharacterCount(inputField.text.Length);
            layoutElement.preferredHeight = inputField.preferredHeight;
        }

        public void RefreshCharacterCount()
        {
            characterCounter.SetCharacterCount(inputField.text.Length);
        }

        public void RefreshHeight()
        {
            layoutElement.preferredHeight = inputField.preferredHeight;
        }

        public void ClearInput()
        {
            inputField.text = string.Empty;
            UpdateCharacterCount();
        }

        public string GetText() =>
            inputField.text;

        public void Show() =>
            gameObject.SetActive(true);

        public void Hide() =>
            gameObject.SetActive(false);

        public void SetActiveTyping()
        {
            maskContainer.SetActive(false);
            inputFieldContainer.SetActive(true);
            SelectInputField();
        }

        public void SelectInputField()
        {
            inputField.Select();
            inputField.ActivateInputField();
        }

        public void SetDefault()
        {
            maskContainer.SetActive(false);
            inputFieldContainer.SetActive(true);
            ApplyUnfocusStyle();
        }

        public void SetBlocked(string reason)
        {
            inputFieldContainer.SetActive(false);
            maskContainer.SetActive(true);
            maskText.text = reason;
        }

        public void DebugInsertTextAndSubmit(string text)
        {
            inputField.SetTextWithoutNotify(text);
            inputField.OnSubmit(null);
            DebugOnSubmit?.Invoke();
        }

    }
}
