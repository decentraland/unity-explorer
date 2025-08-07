using DCL.Audio;
using DCL.Emoji;
using DCL.UI.CustomInputField;
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
        [Serializable]
        public class EmojiContainer
        {
            [field: SerializeField] internal EmojiPanelConfigurationSO emojiPanelConfiguration { get; private set; }
            [field: SerializeField] internal EmojiButton emojiButtonPrefab { get; private set; }
            [field: SerializeField] internal TextAsset emojiMappingJson { get; private set; }
            [field: SerializeField] internal EmojiSectionView emojiSectionViewPrefab { get; private set; }
            [field: SerializeField] internal EmojiButtonView emojiPanelButton { get; private set; }
            [field: SerializeField] internal EmojiPanelView emojiPanel { get; private set; }
            [field: SerializeField] internal AudioClipConfig addEmojiAudio { get; private set; }
            [field: SerializeField] internal AudioClipConfig openEmojiPanelAudio { get; private set; }
        }

        [field: SerializeField] public CustomInputField inputField { get; private set; }
        [field: SerializeField] internal RectTransform pastePopupPosition { get; private set; }

        [SerializeField] private GameObject inputFieldContainer;
        [SerializeField] private LayoutElement layoutElement;

        [field: Header("Blocked")]
        [field: SerializeField] internal Button maskButton { get; private set; }
        [SerializeField] private GameObject maskContainer;
        [SerializeField] private TMP_Text maskText;

        [Header("Suggestion Panel")]
        [field: SerializeField] internal InputSuggestionPanelView suggestionPanel { get; private set; }

        [Header("Focus Visuals")]
        [SerializeField] private GameObject outlineObject;
        [SerializeField] private GameObject characterCounterObject;
        [SerializeField] private CharacterCounterView characterCounter;
        [SerializeField] private GameObject emojiButtonObject;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Color focusedBackgroundColor;
        [SerializeField] private Color unfocusedBackgroundColor;

        [field: Header("Emojis")]
        [field: SerializeField] internal EmojiContainer emojiContainer { get; private set; }

        [field: Header("Audio")]
        [field: SerializeField] internal AudioClipConfig chatInputTextAudio { get; private set; }
        [field: SerializeField] internal AudioClipConfig enterInputAudio { get; private set; }

        [field: Header("Event Bus")]
        [field: SerializeField] internal ViewEventBus inputEventBus { get; private set; }

        public void ApplyFocusStyle()
        {
            outlineObject.SetActive(true);
            characterCounterObject.SetActive(true);
            emojiButtonObject.SetActive(true);
        }

        private void ApplyUnfocusStyle()
        {
            outlineObject.SetActive(false);
            characterCounterObject.SetActive(false);
            emojiButtonObject.SetActive(false);
            inputField.DeactivateInputField();
        }

        public void Initialize()
        {
            characterCounter.SetMaximumLength(inputField.characterLimit);
        }
        
        public void InsertTextAtCaretPosition(string text)
        {
            inputField.InsertTextAtCaretPosition(text);
            characterCounter.SetCharacterCount(inputField.text.Length);
        }

        public void UpdateCharacterCount()
        {
            characterCounter.SetCharacterCount(inputField.text.Length);
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
    }
}
