using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DCL.Chat
{
    public class ChatEntryMessageBubbleElement : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private const float BACKGROUND_HEIGHT_OFFSET = 56;
        private const float BACKGROUND_WIDTH_OFFSET = 56;
        private const float MAX_ENTRY_WIDTH = 246;

        [field: SerializeField] internal ChatEntryUsernameElement usernameElement { get; private set; }
        [field: SerializeField] internal RectTransform backgroundRectTransform { get; private set; }
        [field: SerializeField] internal Button? messageOptionsButton { get; private set; }
        [field: SerializeField] internal ChatEntryMessageContentElement messageContentElement { get; private set; }


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
            backgroundSize.y = Mathf.Max(messageContentElement.messageContentRectTransform.sizeDelta.y + BACKGROUND_HEIGHT_OFFSET);
            backgroundSize.x = CalculatePreferredWidth(data);
            backgroundRectTransform.sizeDelta = backgroundSize;
        }

        private float CalculatePreferredWidth(ChatMessage message)
        {
            int nameLenght = message.SenderValidatedName.Length + (string.IsNullOrEmpty(message.SenderWalletId) ? 0 : message.SenderWalletId.Length);
            int emojisCount = GetEmojisCount(message.Message);

            var messageContentText = messageContentElement.messageContentText;

            if (nameLenght > (emojisCount > 0 ? messageContentText.GetParsedText().Length + emojisCount : messageContentText.GetParsedText().Length)) { return usernameElement.GetUserNamePreferredWidth(BACKGROUND_WIDTH_OFFSET); }

            if (messageContentText.GetPreferredValues(message.Message, MAX_ENTRY_WIDTH, 0).x < MAX_ENTRY_WIDTH - BACKGROUND_WIDTH_OFFSET)
                return messageContentText.GetPreferredValues(message.Message, MAX_ENTRY_WIDTH, 0).x + BACKGROUND_WIDTH_OFFSET;

            return MAX_ENTRY_WIDTH;
        }

        private int GetEmojisCount(string message) =>
            message.Split("\\U0").Length - 1;

    }
}
