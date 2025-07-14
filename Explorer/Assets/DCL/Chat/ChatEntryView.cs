using DCL.Chat.History;
using DCL.UI.ProfileElements;
using DG.Tweening;
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

        private ChatMessage chatMessage;
        private readonly Vector3[] cornersCache = new Vector3[4];

        private Color originalUserNameColor;
        private Color originalUserIdColor;
        private Color originalVerifiedIconColor;

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

        private void Awake()
        {
            profileButton.onClick.AddListener(OnProfileButtonClicked);
            usernameElement.UserNameClicked += OnUsernameClicked;
            originalUserIdColor = usernameElement.walletIdText.color;

            if(usernameElement.verifiedIcon != null)
                originalVerifiedIconColor = usernameElement.verifiedIcon.color;
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

        public void GreyOut(bool greyOut, float opacity)
        {
            ProfilePictureView.GreyOut(greyOut, opacity);
            messageBubbleElement.GreyOut(greyOut, opacity);

            usernameElement.userName.color = greyOut ? Color.Lerp(originalUserNameColor, new Color(0.0f, 0.0f, 0.0f, originalUserNameColor.a), opacity) : originalUserNameColor;
            usernameElement.walletIdText.color = greyOut ? Color.Lerp(originalUserIdColor, new Color(0.0f, 0.0f, 0.0f, originalUserIdColor.a), opacity) : originalUserIdColor;

            if(usernameElement.verifiedIcon != null)
                usernameElement.verifiedIcon.color = greyOut ? Color.Lerp(originalVerifiedIconColor, new Color(0.0f, 0.0f, 0.0f, originalVerifiedIconColor.a), opacity) : originalVerifiedIconColor;
        }

        public void SetUsernameColor(Color newUserNameColor)
        {
            originalUserNameColor = newUserNameColor;
            usernameElement.userName.color = newUserNameColor;
        }
    }
}
