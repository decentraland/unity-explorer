using DCL.Chat.History;
using DCL.UI.ProfileElements;
using DG.Tweening;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Chat
{
    public class ChatEntryView : MonoBehaviour
    {
        public delegate void ChatEntryClickedDelegate(string walletAddress, Vector2 contextMenuPosition);

        public ChatEntryClickedDelegate? ChatEntryClicked;

        [field: SerializeField] internal RectTransform rectTransform { get; private set; }
        [field: SerializeField] internal CanvasGroup chatEntryCanvasGroup { get; private set; }

        [field: Header("Elements")]
        [field: SerializeField] internal ChatEntryUsernameElement usernameElement { get; private set; }
        [field: SerializeField] internal ChatEntryMessageBubbleElement messageBubbleElement { get; private set; }

        [field: Header("Avatar Profile")]
        [field: SerializeField] internal ProfilePictureView ProfilePictureView { get; private set; }
        [field: SerializeField] internal Button profileButton { get; private set; }

        private ChatMessage chatMessage;

        private void Awake()
        {
            profileButton.onClick.AddListener(OpenContextMenu);
        }

        private void OpenContextMenu()
        {
            ChatEntryClicked?.Invoke(chatMessage.SenderWalletAddress, profileButton.transform.position);
        }

        public void AnimateChatEntry()
        {
            chatEntryCanvasGroup.alpha = 0;
            chatEntryCanvasGroup.DOFade(1, 0.5f);
        }

        public void SetItemData(ChatMessage data)
        {
            chatMessage = data;
            usernameElement.SetUsername(data.SenderValidatedName, data.SenderWalletId);
            messageBubbleElement.SetMessageData(data);
            rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, messageBubbleElement.backgroundRectTransform.sizeDelta.y);
        }
    }
}
