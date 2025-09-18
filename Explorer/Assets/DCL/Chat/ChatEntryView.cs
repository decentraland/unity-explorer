using DCL.Chat.History;
using DCL.UI.ProfileElements;
using DG.Tweening;
using System;
using System.Globalization;
using DCL.Chat.ChatViewModels;
using DCL.Translation.Models;
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
        private Func<bool> IsTranslationActivated;
        public event Action<string> OnTranslateRequested;
        public event Action<string> OnRevertRequested;
        private bool isPointerInside;

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

        private ReactivePropertyExtensions.DisposableSubscription<ProfileThumbnailViewModel.WithColor>? profileSubscription;

        private ChatMessage chatMessage;
        private ChatMessageViewModel currentViewModel;
        private readonly Vector3[] cornersCache = new Vector3[4];

        private void Awake()
        {
            profileButton.onClick.AddListener(OnProfileButtonClicked);
            usernameElement.UserNameClicked += OnUsernameClicked;

            messageBubbleElement.OnPointerEnterEvent += HandlePointerEnter;
            messageBubbleElement.OnPointerExitEvent += HandlePointerExit;
            
            messageBubbleElement.messageOptionsButton.onClick.AddListener(() =>
            {
                if (currentViewModel != null)
                {
                    onMessageContextMenuClicked?.Invoke(currentViewModel.Message.MessageId, this);
                }
            });

            messageBubbleElement.OnTranslateRequest += () =>
            {
                if (currentViewModel != null)
                    OnTranslateRequested?.Invoke(currentViewModel.Message.MessageId);
            };

            messageBubbleElement.OnRevertRequest += () =>
            {
                if (currentViewModel != null)
                    OnRevertRequested?.Invoke(currentViewModel.Message.MessageId);
            };
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
                dateDividerText.text = GetDateRepresentation(data.SentTimestamp!.Value.Date);

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
        
        public void SetItemData(ChatMessageViewModel viewModel,
            Action<string, ChatEntryView> onMessageContextMenuClicked,
            ChatEntryClickedDelegate? onProfileContextMenuClicked,
            Func<bool> IsTranslationActivated)
        {
            currentViewModel = viewModel;
            this.IsTranslationActivated = IsTranslationActivated;
            chatMessage = viewModel.Message;
            usernameElement.SetUsername(chatMessage.SenderValidatedName, chatMessage.SenderWalletId);
            messageBubbleElement.SetMessageData(viewModel.DisplayText, chatMessage, viewModel.TranslationState);

            UpdateTranslationViewVisibility();

            dateDividerElement.gameObject.SetActive(viewModel.ShowDateDivider);
            if (viewModel.ShowDateDivider)
                dateDividerText.text = GetDateRepresentation(chatMessage.SentTimestamp!.Value.Date);

            rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, messageBubbleElement.backgroundRectTransform.sizeDelta.y);

            this.onMessageContextMenuClicked = onMessageContextMenuClicked;
            ChatEntryClicked = onProfileContextMenuClicked;

            // Binding is done for non-system messages only
            if (!viewModel.Message.IsSystemMessage)
                ProfilePictureView.Bind(viewModel.ProfileData);
            else
                ProfilePictureView.SetImage(viewModel.ProfileData.Value.Thumbnail.Sprite!);

            profileSubscription?.Dispose();
            profileSubscription = viewModel.ProfileData.UseCurrentValueAndSubscribeToUpdate(usernameElement.userName, (vM, text) => text.color = vM.ProfileColor, viewModel.cancellationToken);
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

        private void HandlePointerEnter()
        {
            isPointerInside = true;
            UpdateTranslationViewVisibility();
        }

        private void HandlePointerExit()
        {
            isPointerInside = false;
            UpdateTranslationViewVisibility();
        }


        public void Reset()
        {
            if (!isPointerInside)
                messageBubbleElement.Reset();
        }

        // private void UpdateTranslationViewVisibility()
        // {
        //     if (currentViewModel == null ||
        //         currentViewModel.Message.IsSystemMessage ||
        //         currentViewModel.Message.IsSentByOwnUser ||
        //         !IsTranslationActivated())
        //     {
        //         messageBubbleElement.SetTranslationViewVisibility(false);
        //         return;
        //     }
        //
        //     bool isVisible =
        //         currentViewModel.TranslationState == TranslationState.Success ||
        //         currentViewModel.TranslationState == TranslationState.Pending ||
        //         currentViewModel.TranslationState == TranslationState.Failed ||
        //         (isPointerInside && (currentViewModel.TranslationState == TranslationState.Original || 
        //                              currentViewModel.TranslationState == TranslationState.Failed));
        //
        //     messageBubbleElement.SetTranslationViewVisibility(isVisible);
        // }
        
        private void UpdateTranslationViewVisibility()
        {
            // Handle universal conditions where the view should always be hidden ---
            if (currentViewModel == null ||
                currentViewModel.Message.IsSystemMessage ||
                !IsTranslationActivated())
            {
                messageBubbleElement.SetTranslationViewVisibility(false);
                return;
            }

            // Handle the special case for the user's own messages ---
            if (currentViewModel.Message.IsSentByOwnUser)
            {
                // For our own messages, we ONLY want to show the translation view
                // if the translation process has already been started (via the context menu).
                bool isTranslationInProgressOrFinished =
                    currentViewModel.TranslationState == TranslationState.Pending ||
                    currentViewModel.TranslationState == TranslationState.Success ||
                    currentViewModel.TranslationState == TranslationState.Failed;

                messageBubbleElement.SetTranslationViewVisibility(isTranslationInProgressOrFinished);
                return; // Logic for own messages is complete.
            }

            // If not an own message, use the original logic for other users' messages
            bool isVisibleForOthers =
                currentViewModel.TranslationState == TranslationState.Success ||
                currentViewModel.TranslationState == TranslationState.Pending ||
                currentViewModel.TranslationState == TranslationState.Failed ||
                (isPointerInside && currentViewModel.TranslationState == TranslationState.Original);

            messageBubbleElement.SetTranslationViewVisibility(isVisibleForOthers);
        }

        private void OnDestroy()
        {
            if (messageBubbleElement != null)
            {
                messageBubbleElement.OnPointerEnterEvent -= HandlePointerEnter;
                messageBubbleElement.OnPointerExitEvent -= HandlePointerExit;
            }
        }
    }
}
