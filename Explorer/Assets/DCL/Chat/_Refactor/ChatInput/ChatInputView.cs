﻿using DCL.Audio;
using DCL.Emoji;
using DCL.UI.CustomInputField;
using DCL.UI.SuggestionPanel;
using MVC;
using System;
using System.Text;
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
            [field: SerializeField] internal EmojiSectionView emojiSectionViewPrefab { get; private set; }
            [field: SerializeField] internal EmojiButtonView emojiPanelButton { get; private set; }
            [field: SerializeField] internal EmojiPanelView emojiPanel { get; private set; }
            [field: SerializeField] internal AudioClipConfig addEmojiAudio { get; private set; }
            [field: SerializeField] internal AudioClipConfig openEmojiPanelAudio { get; private set; }
        }

        [field: SerializeField] public CustomInputField inputField { get; private set; }
        [SerializeField] private TMP_Text inputOverlayText;
        [field: SerializeField] internal RectTransform pastePopupPosition { get; private set; }

        [SerializeField] private GameObject inputFieldContainer;
        [SerializeField] private LayoutElement layoutElement;

        [field: Header("Blocked")]
        [field: SerializeField] internal Button maskButton { get; private set; }
        [SerializeField] private GameObject maskContainer;
        [SerializeField] private TMP_Text maskText;

        [Header("Suggestion Panel")]
        [field: SerializeField] internal InputSuggestionPanelView suggestionPanel { get; private set; }
        [field: SerializeField] internal Color32 mentionColor { get; private set; } = new (0, 179, 255, 255);

        [Header("Focus Visuals")]
        [SerializeField] private GameObject outlineObject;
        [SerializeField] private GameObject characterCounterObject;
        [SerializeField] private CharacterCounterView characterCounter;
        [SerializeField] private GameObject emojiButtonObject;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private TextMeshProUGUI inputPlaceholderObject;
        [SerializeField] private Color focusedBackgroundColor;
        [SerializeField] private Color unfocusedBackgroundColor;

        [field: Header("Emojis")]
        [field: SerializeField] internal EmojiContainer emojiContainer { get; private set; }

        [field: Header("Audio")]
        [field: SerializeField] internal AudioClipConfig chatInputTextAudio { get; private set; }
        [field: SerializeField] internal AudioClipConfig enterInputAudio { get; private set; }

        [field: Header("Event Bus")]
        [field: SerializeField] internal ViewEventBus inputEventBus { get; private set; }

        private string previousText = string.Empty;
        private ChatConfig.ChatConfig chatConfig;

        public void ApplyFocusStyle()
        {
            outlineObject.SetActive(true);
            characterCounterObject.SetActive(true);
            emojiButtonObject.SetActive(true);
            inputPlaceholderObject.text = chatConfig.InputFocusedMessages;
            InsertTextAtCaretPosition(previousText);
        }

        private void ApplyUnfocusStyle()
        {
            outlineObject.SetActive(false);
            characterCounterObject.SetActive(false);
            emojiButtonObject.SetActive(false);
            inputPlaceholderObject.text = chatConfig.InputUnfocusedMessages;

            // NOTE: Remember the last typed message when going to unfocused state,
            // NOTE: except when it's a single "/" which is used to trigger commands.
            // NOTE: This prevents storing incomplete command triggers as normal messages.
            if (inputField.text.Length > 1 ||
                (inputField.text.Length == 1 && inputField.text[0] != '/'))
            {
                previousText = inputField.text;
            }
            else
            {
                previousText = string.Empty;
            }

            inputField.text = string.Empty;
            inputField.DeactivateInputField();
        }

        public void Initialize(ChatConfig.ChatConfig chatConfig)
        {
            characterCounter.SetMaximumLength(inputField.characterLimit);
            this.chatConfig = chatConfig;
            inputField.onValueChanged.AddListener(ColorMentions);
            inputField.TextReplacedWithoutNotification += ColorMentions;
        }

        private void ColorMentions(string input)
        {
            inputOverlayText.text = input;

            inputOverlayText.ForceMeshUpdate();
            bool mentionEverFound = false;

            for (int j = 0; j < inputOverlayText.textInfo.wordCount; j++)
            {
                TMP_WordInfo info = inputOverlayText.textInfo.wordInfo[j];
                if (input[Math.Max(0, info.firstCharacterIndex - 1)] != '@') continue;
                mentionEverFound = true;

                int startingIndex = input[info.firstCharacterIndex] == '@' ? info.firstCharacterIndex : -1;
                for (int i = startingIndex; i < info.characterCount; i++)
                {
                    int charIndex = info.firstCharacterIndex + i;
                    int meshIndex = inputOverlayText.textInfo.characterInfo[charIndex].materialReferenceIndex;
                    int vertexIndex = inputOverlayText.textInfo.characterInfo[charIndex].vertexIndex;

                    Color32[] vertexColors = inputOverlayText.textInfo.meshInfo[meshIndex].colors32;
                    vertexColors[vertexIndex + 0] = mentionColor;
                    vertexColors[vertexIndex + 1] = mentionColor;
                    vertexColors[vertexIndex + 2] = mentionColor;
                    vertexColors[vertexIndex + 3] = mentionColor;
                }
            }

            if (mentionEverFound)
                inputOverlayText.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
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
            previousText = string.Empty;
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
