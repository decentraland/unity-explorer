using DCL.Chat.ChatViewModels;
using DCL.Chat.History;
using DCL.UI.ProfileElements;
using DCL.Utilities;
using DG.Tweening;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Chat
{
    public class ChatEntryView : MonoBehaviour
    {
        private const float PROFILE_BUTTON_Y_OFFSET = -18;
        private const float USERNAME_Y_OFFSET = -13f;

        public delegate void ChatEntryClickedDelegate(string walletAddress, Vector2 contextMenuPosition);

        public ChatEntryClickedDelegate? ChatEntryClicked;

        [field: SerializeField] internal RectTransform rectTransform { get; private set; }
        [field: SerializeField] internal CanvasGroup chatEntryCanvasGroup { get; private set; }

        [field: Header("Elements")]
        [field: SerializeField] private ChatEntryUsernameElement usernameElement { get; set; }
        [field: SerializeField] internal ChatEntryMessageBubbleElement messageBubbleElement { get; private set; }

        [field: Header("Avatar Profile")]
        [field: SerializeField] internal ProfilePictureView ProfilePictureView { get; private set; }
        [field: SerializeField] internal Button profileButton { get; private set; }

        [field: SerializeField] private CanvasGroup usernameElementCanvas;

        private ChatMessage chatMessage;
        private ChatMessageViewModel chatMessageViewModel;
        private readonly Vector3[] cornersCache = new Vector3[4];

        private Action<string, ChatEntryView>? onMessageContextMenuClicked;

        private void Awake()
        {
            profileButton.onClick.AddListener(OnProfileButtonClicked);
            usernameElement.UserNameClicked += OnUsernameClicked;
            messageBubbleElement.messageOptionsButton.onClick.AddListener(() => onMessageContextMenuClicked?.Invoke(chatMessage.Message, this));
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

        public void SetItemData(ChatMessageViewModel viewModel, Action<string, ChatEntryView> onMessageContextMenuClicked, ChatEntryClickedDelegate? onProfileContextMenuClicked)
        {
            SetItemData(viewModel.Message);

            this.onMessageContextMenuClicked = onMessageContextMenuClicked;
            ChatEntryClicked = onProfileContextMenuClicked;

            // Binding is done for non-system messages only
            if (!viewModel.Message.IsSystemMessage)
                ProfilePictureView.Bind(viewModel.ProfileData);
            else
                usernameElement.userName.color = viewModel.ProfileData.Value.ProfileColor;
        }

        private void OnProfileButtonClicked()
        {
            RectTransform buttonRect = profileButton.GetComponent<RectTransform>();
            buttonRect.GetWorldCorners(cornersCache);

            float posX = cornersCache[3].x;
            float posY = cornersCache[3].y + PROFILE_BUTTON_Y_OFFSET;

            OpenProfileContextMenu(posX, posY);
        }

        private void OnUsernameClicked()
        {
            usernameElement.GetRightEdgePosition(cornersCache);

            float posX = cornersCache[3].x;
            float posY = cornersCache[3].y + USERNAME_Y_OFFSET;

            OpenProfileContextMenu(posX, posY);
        }

        private void OpenProfileContextMenu(float posX, float posY)
        {
            ChatEntryClicked?.Invoke(chatMessage.SenderWalletAddress, new Vector2(posX, posY));
        }

        public void GreyOut(float opacity)
        {
            ProfilePictureView.GreyOut(opacity);
            messageBubbleElement.GreyOut(opacity);

            usernameElementCanvas.alpha = 1.0f - opacity;
        }

        public void SetUsernameColor(Color newUserNameColor)
        {
            usernameElement.userName.color = newUserNameColor;
        }
    }
}
