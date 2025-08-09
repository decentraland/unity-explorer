using DCL.Chat.History;
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

        private Vector2 backgroundSize;
        private bool popupOpen;

        public void OnPointerEnter(PointerEventData eventData)
        {
            messageOptionsButton?.gameObject.SetActive(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!popupOpen)
                messageOptionsButton?.gameObject.SetActive(false);
        }

        public Vector3 PopupPosition => popupPosition.position;

        public void HideOptionsButton()
        {
            popupOpen = false;
            messageOptionsButton?.gameObject.SetActive(false);
        }

        /// <summary>
        ///  Sets the chat message data into the chat bubble, adapting the background size accordingly and changing the color & outline if it's a mention
        /// </summary>
        /// <param name="data"> a ChatMessage </param>
        public void SetMessageData(ChatMessage data)
        {
            usernameElement.SetUsername(data.SenderValidatedName, data.SenderWalletId);
            messageContentElement.SetMessageContent(data.Message);

            if (data.SentTimestamp.HasValue)
            {
                timestamp.gameObject.SetActive(true);
                timestamp.text = data.SentTimestamp.Value.ToLocalTime().ToString("hh:mm tt", CultureInfo.InvariantCulture);
            }
            else
                timestamp.gameObject.SetActive(false);

            backgroundSize = backgroundRectTransform.sizeDelta;
            backgroundSize.y = Mathf.Max(messageContentElement.messageContentRectTransform.sizeDelta.y + configurationSo.BackgroundHeightOffset);
            backgroundSize.y += timestamp.gameObject.activeSelf ? timestamp.rectTransform.sizeDelta.y : 0.0f;
            backgroundSize.x = CalculatePreferredWidth(data);
            backgroundRectTransform.sizeDelta = backgroundSize;
            mentionedOutline.SetActive(data.IsMention);

            backgroundImage.color = data.IsMention ? backgroundMentionedColor : backgroundDefaultColor;
            //messageOptionsButton.onClick.AddListener(OnMessageOptionsClicked);
        }

        private void OnMessageOptionsClicked()
        {
            popupOpen = true;
        }

        private float CalculatePreferredWidth(ChatMessage message)
        {
            int nameLength = message.SenderValidatedName.Length;
            string walletId = message.SenderWalletId;
            int walletIdLength = string.IsNullOrEmpty(walletId) ? 0 : walletId.Length;
            int nameTotalLength = nameLength + walletIdLength;
            string messageText = message.Message;
            TMP_Text messageContentText = messageContentElement.messageContentText;
            int parsedTextLength = messageContentText.textInfo.characterCount;

            var emojisCount = 0;
            var needsEmojiCount = false;

            if (nameTotalLength > parsedTextLength)
            {
                needsEmojiCount = true;
                emojisCount = GetEmojisCount(messageText);
            }

            float userNamePreferredWidth = usernameElement.GetUserNamePreferredWidth(configurationSo.BackgroundWidthOffset, configurationSo.VerifiedBadgeWidth);

            if (nameTotalLength > (needsEmojiCount && emojisCount > 0 ? parsedTextLength + emojisCount : parsedTextLength))
                return userNamePreferredWidth;

            Vector2 preferredValues = messageContentText.GetPreferredValues(messageText, configurationSo.MaxEntryWidth, 0);

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
