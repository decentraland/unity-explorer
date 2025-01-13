using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DCL.Chat
{


    public class ChatEntryMessageBubbleElement : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [field: SerializeField] internal ChatEntryUsernameElement usernameElement { get; private set; }
        [field: SerializeField] internal RectTransform backgroundRectTransform { get; private set; }
        [field: SerializeField] internal Button? messageOptionsButton { get; private set; }
        [field: SerializeField] internal ChatEntryMessageContentElement messageContentElement { get; private set; }
        [field: SerializeField] internal ChatEntryConfigurationSO configurationSo { get; private set; }

        private float backgroundHeightOffset => configurationSo.BackgroundHeightOffset;
        private float backgroundWidthOffset => configurationSo.BackgroundWidthOffset;
        private float maxEntryWidth => configurationSo.MaxEntryWidth;
        private float verifiedBadgeWidth => configurationSo.VerifiedBadgeWidth;

        private Vector2 backgroundSize;

        public void OnPointerEnter(PointerEventData eventData)
        {
            messageOptionsButton?.gameObject.SetActive(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            messageOptionsButton?.gameObject.SetActive(false);
        }

        public void SetMessageData(ChatMessage data)
        {
            usernameElement.SetUsername(data.SenderValidatedName, data.SenderWalletId);
            messageContentElement.SetMessageContent(data.Message);

            backgroundSize = backgroundRectTransform.sizeDelta;
            backgroundSize.y = Mathf.Max(messageContentElement.messageContentRectTransform.sizeDelta.y + backgroundHeightOffset);
            backgroundSize.x = CalculatePreferredWidth(data);
            backgroundRectTransform.sizeDelta = backgroundSize;
        }

        private float CalculatePreferredWidth(ChatMessage message)
        {
            int nameLenght = message.SenderValidatedName.Length + (string.IsNullOrEmpty(message.SenderWalletId) ? 0 : message.SenderWalletId.Length);
            int emojisCount = GetEmojisCount(message.Message);

            var messageContentText = messageContentElement.messageContentText;

            if (nameLenght > (emojisCount > 0 ? messageContentText.GetParsedText().Length + emojisCount : messageContentText.GetParsedText().Length))
                return usernameElement.GetUserNamePreferredWidth(backgroundWidthOffset, verifiedBadgeWidth);

            if (messageContentText.GetPreferredValues(message.Message, maxEntryWidth, 0).x < maxEntryWidth - backgroundWidthOffset)
                return messageContentText.GetPreferredValues(message.Message, maxEntryWidth, 0).x + backgroundWidthOffset;

            return maxEntryWidth;
        }

        private int GetEmojisCount(string message) =>
            message.Split("\\U0").Length - 1;

    }
}
