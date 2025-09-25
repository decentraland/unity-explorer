using DCL.Chat.History;
using DCL.Translation;
using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DCL.Chat
{
    /// <summary>
    ///     This class represents the part of the chat entry that contains the chat bubble, so its where we display the text of the message
    ///     and also now we display a button that when clicked opens an option panel
    /// </summary>
    public class ChatEntryMessageBubbleElement : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [field: SerializeField] internal Color backgroundDefaultColor { get; private set; }
        [field: SerializeField] internal Color backgroundMentionedColor { get; private set; }
        [field: SerializeField] internal ChatEntryUsernameElement usernameElement { get; private set; }
        [field: SerializeField] internal RectTransform backgroundRectTransform { get; private set; }
        [field: SerializeField] internal Image backgroundImage { get; private set; }
        [field: SerializeField] internal Button? messageOptionsButton { get; private set; }
        [field: SerializeField] internal ChatEntryMessageContentElement messageContentElement { get; private set; }
        [field: SerializeField] internal ChatEntryConfigurationSO configurationSo { get; private set; }
        [field: SerializeField] internal RectTransform popupPosition { get; private set; }
        [field: SerializeField] internal GameObject mentionedOutline { get; private set; }
        [field: SerializeField] internal TMP_Text timestamp { get; private set; }
        [field: SerializeField] internal ChatEntryTranslationView translationView { get; private set; }

        public event Action OnTranslateRequest;
        public event Action OnRevertRequest;
        public event Action OnPointerEnterEvent;
        public event Action OnPointerExitEvent;

        private Vector2 backgroundSize;
        private bool popupOpen;

        private void Awake()
        {
            translationView.OnTranslateClicked += () => OnTranslateRequest?.Invoke();
            translationView.OnSeeOriginalClicked += () => OnRevertRequest?.Invoke();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            messageOptionsButton?.gameObject.SetActive(true);
            OnPointerEnterEvent?.Invoke();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!popupOpen)
                messageOptionsButton?.gameObject.SetActive(false);

            OnPointerExitEvent?.Invoke();
        }

        public Vector3 PopupPosition => popupPosition.position;

        public void HideOptionsButton()
        {
            popupOpen = false;
            messageOptionsButton?.gameObject.SetActive(false);
        }

        public void Reset()
        {
            messageOptionsButton?.gameObject.SetActive(false);
        }

        public void SetMessageData(string displayText, ChatMessage originalData, TranslationState translationState)
        {
            usernameElement.SetUsername(originalData.SenderValidatedName, originalData.SenderWalletId);
            messageContentElement.SetMessageContent(displayText);

            if (originalData.SentTimestamp.HasValue)
            {
                timestamp.gameObject.SetActive(true);
                timestamp.text = originalData.SentTimestamp.Value.ToLocalTime().ToString("hh:mm tt", CultureInfo.InvariantCulture);
            }
            else
                timestamp.gameObject.SetActive(false);

            translationView.SetState(translationState);

            backgroundSize = backgroundRectTransform.sizeDelta;
            backgroundSize.y = Mathf.Max(messageContentElement.messageContentRectTransform.sizeDelta.y + configurationSo.BackgroundHeightOffset);
            backgroundSize.y += timestamp.gameObject.activeSelf ? timestamp.rectTransform.sizeDelta.y : 0.0f;
            backgroundSize.x = CalculatePreferredWidth(displayText, originalData);
            backgroundRectTransform.sizeDelta = backgroundSize;
            mentionedOutline.SetActive(originalData.IsMention);

            backgroundImage.color = originalData.IsMention ? backgroundMentionedColor : backgroundDefaultColor;
        }

        /// <summary>
        ///  Sets the chat message data into the chat bubble, adapting the background size accordingly and changing the color & outline if it's a mention
        /// </summary>
        /// <param name="data"> a ChatMessage </param>
        public void SetMessageData(ChatMessage data)
        {
            SetMessageData(data.Message, data, TranslationState.Original);
        }

        public void SetTranslationViewVisibility(bool isVisible)
        {
            translationView.gameObject.SetActive(isVisible);
        }

        private void OnMessageOptionsClicked()
        {
            popupOpen = true;
        }

        private float CalculatePreferredWidth(string displayText, ChatMessage originalMessage)
        {
            int nameLength = originalMessage.SenderValidatedName.Length;
            string walletId = originalMessage.SenderWalletId;
            int walletIdLength = string.IsNullOrEmpty(walletId) ? 0 : walletId.Length;
            int nameTotalLength = nameLength + walletIdLength;
            TMP_Text messageContentText = messageContentElement.messageContentText;

            // We use the displayText to get the textInfo, but the original message for emoji counting.
            messageContentText.SetText(displayText); // Important: Set text first to get accurate textInfo
            int parsedTextLength = messageContentText.textInfo.characterCount;

            var emojisCount = 0;
            var needsEmojiCount = false;

            if (nameTotalLength > parsedTextLength)
            {
                needsEmojiCount = true;
                emojisCount = GetEmojisCount(originalMessage.Message); // Count emojis from original message
            }

            float userNamePreferredWidth = usernameElement.GetUserNamePreferredWidth(configurationSo.BackgroundWidthOffset, configurationSo.VerifiedBadgeWidth);

            if (nameTotalLength > (needsEmojiCount && emojisCount > 0 ? parsedTextLength + emojisCount : parsedTextLength))
                return userNamePreferredWidth;

            // Use the displayText for preferred size calculation
            var preferredValues = messageContentText.GetPreferredValues(displayText, configurationSo.MaxEntryWidth, 0);

            if (preferredValues.x < configurationSo.MaxEntryWidth - configurationSo.BackgroundWidthOffset)
                return Mathf.Max(preferredValues.x + configurationSo.BackgroundWidthOffset, userNamePreferredWidth);

            return configurationSo.MaxEntryWidth;
        }

        private int GetEmojisCount(string message)
        {
            if (string.IsNullOrEmpty(message))
                return 0;

            ReadOnlySpan<char> messageSpan = message.AsSpan();
            int count = 0;

            // Find all occurrences of "\U0"
            for (var i = 0; i < messageSpan.Length - 2; i++)
            {
                if (messageSpan[i] == '\\' &&
                    i + 2 < messageSpan.Length &&
                    messageSpan[i + 1] == 'U' &&
                    messageSpan[i + 2] == '0')
                {
                    count++;
                    i += 2;
                }
            }
            return count;
        }

        public void GreyOut(float opacity)
        {
            messageContentElement.GreyOut(opacity);
        }
    }
}
