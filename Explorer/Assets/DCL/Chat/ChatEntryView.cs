using DCL.Chat.History;
using DCL.UI.ProfileElements;
using DG.Tweening;
using System;
using System.Globalization;
using DCL.Chat.ChatViewModels;
using DCL.Utilities;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Chat
{
    public class ChatEntryView : MonoBehaviour
    {
        private const float PROFILE_BUTTON_Y_OFFSET = -18;
        private const float USERNAME_Y_OFFSET = -13f;
        private const string DATE_DIVIDER_TODAY = "Today";
        private const string DATE_DIVIDER_YESTERDAY = "Today";

        public delegate void ChatEntryClickedDelegate(string walletAddress, Vector2 contextMenuPosition);

        public ChatEntryClickedDelegate? ChatEntryClicked;
        private Action<string, ChatEntryView>? onMessageContextMenuClicked;

        [field: SerializeField] internal RectTransform rectTransform { get; private set; }
        [field: SerializeField] internal CanvasGroup chatEntryCanvasGroup { get; private set; }

        [field: Header("Elements")]
        [field: SerializeField] private ChatEntryUsernameElement usernameElement { get; set; }
        [field: SerializeField] internal ChatEntryMessageBubbleElement messageBubbleElement { get; private set; }
        [field: SerializeField] internal RectTransform dateDividerElement { get; private set; }
        [field: SerializeField] internal TMP_Text dateDividerText { get; private set; }

        [field: Header("Avatar Profile")]
        [field: SerializeField] internal ProfilePictureView ProfilePictureView { get; private set; }
        [field: SerializeField] internal Button profileButton { get; private set; }

        [field: SerializeField] private CanvasGroup usernameElementCanvas;

        private ChatMessage chatMessage;
        private readonly Vector3[] cornersCache = new Vector3[4];

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

        public void SetItemData(ChatMessage data, bool showDateDivider)
        {
            chatMessage = data;
            usernameElement.SetUsername(data.SenderValidatedName, data.SenderWalletId);
            messageBubbleElement.SetMessageData(data);

            dateDividerElement.gameObject.SetActive(showDateDivider);

            if (showDateDivider)
                dateDividerText.text = GetDateRepresentation(DateTime.FromOADate(data.SentTimestamp).Date);

            rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, messageBubbleElement.backgroundRectTransform.sizeDelta.y);
        }

        private string GetDateRepresentation(DateTime date)
        {
            if(date == DateTime.Today)
                return DATE_DIVIDER_TODAY;
            else if (date == DateTime.Today.AddDays(-1.0))
                return DATE_DIVIDER_YESTERDAY;
            else if(date.Year == DateTime.Today.Year)
                return date.ToString("ddd, d MMM", CultureInfo.InvariantCulture);
            else
                return date.ToString("ddd, d MMM, yyyy", CultureInfo.InvariantCulture);
        }

        public void SetItemData(ChatMessageViewModel viewModel, Action<string, ChatEntryView> onMessageContextMenuClicked, ChatEntryClickedDelegate? onProfileContextMenuClicked)
        {
            SetItemData(viewModel.Message, viewModel.ShowDateDivider);

            this.onMessageContextMenuClicked = onMessageContextMenuClicked;
            ChatEntryClicked = onProfileContextMenuClicked;

            // Binding is done for non-system messages only
            if (!viewModel.Message.IsSystemMessage)
                ProfilePictureView.Bind(viewModel.ProfileData);

            viewModel.ProfileData.UseCurrentValueAndSubscribeToUpdate(usernameElement.userName, (vM, text) => text.color = vM.ProfileColor, viewModel.cancellationToken);
        }

        private void OnProfileButtonClicked()
        {
            RectTransform buttonRect = profileButton.GetComponent<RectTransform>();
            buttonRect.GetWorldCorners(cornersCache);

            float posX = cornersCache[3].x;
            float posY = cornersCache[3].y + PROFILE_BUTTON_Y_OFFSET;

            OpenContextMenu(posX, posY);
        }

        private void OnUsernameClicked()
        {
            usernameElement.GetRightEdgePosition(cornersCache);

            float posX = cornersCache[3].x;
            float posY = cornersCache[3].y + USERNAME_Y_OFFSET;

            OpenContextMenu(posX, posY);
        }

        private void OpenContextMenu(float posX, float posY)
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
