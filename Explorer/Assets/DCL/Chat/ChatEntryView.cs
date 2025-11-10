using DCL.Chat.History;
using DCL.UI.ProfileElements;
using DG.Tweening;
using System;
using System.Globalization;
using DCL.Chat.ChatViewModels;
using DCL.FeatureFlags;
using DCL.Translation;
using DCL.Utilities;
using MVC;
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
        private Func<bool>? IsTranslationActivated;
        private Func<bool>? IsAutoTranslationEnabled;
        public event Action<string> OnTranslateRequested;
        public event Action<string> OnRevertRequested;
        private bool isPointerInside;

        [field: SerializeField] internal RectTransform rectTransform { get; private set; }
        [field: SerializeField] internal CanvasGroup chatEntryCanvasGroup { get; private set; }

        [field: Header("Elements")]
        [field: SerializeField] internal ChatEntryUsernameElement usernameElement { get; set; }
        [field: SerializeField] internal ChatEntryMessageBubbleElement messageBubbleElement { get; private set; }
        [field: SerializeField] internal RectTransform dateDividerElement { get; private set; }
        [field: SerializeField] internal TMP_Text dateDividerText { get; private set; }

        [field: Header("Avatar Profile")]
        [field: SerializeField] internal ProfilePictureView ProfilePictureView { get; private set; }
        [field: SerializeField] internal Button profileButton { get; private set; }

        [field: SerializeField] private CanvasGroup usernameElementCanvas;

        private ReactivePropertyExtensions.DisposableSubscription<ProfileThumbnailViewModel.WithColor>? profileSubscription;
        private ReactivePropertyExtensions.DisposableSubscription<ProfileOptionalBasicInfo>? profileDataSubscription;

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
            Func<bool> IsTranslationActivated,
            Func<bool> IsAutoTranslationEnabled = null)
        {
            currentViewModel = viewModel;
            this.IsTranslationActivated = IsTranslationActivated;
            this.IsAutoTranslationEnabled = IsAutoTranslationEnabled;
            chatMessage = viewModel.Message;
            usernameElement.SetUsername(chatMessage.SenderValidatedName, chatMessage.SenderWalletId, chatMessage.IsSenderOfficial);
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
            {
                ProfilePictureView.Bind(viewModel.ProfileData);

                if (!chatMessage.SenderWalletAddress.Equals(ViewDependencies.CurrentIdentity?.Address.ToString()))
                    ProfilePictureView.ConfigureThumbnailClickData(OnUsernameClicked, chatMessage.SenderWalletAddress);
            }
            else
                ProfilePictureView.SetImage(viewModel.ProfileData.Value.Thumbnail.Sprite!);

            profileSubscription?.Dispose();
            profileSubscription = viewModel.ProfileData.UseCurrentValueAndSubscribeToUpdate(usernameElement.userName, (vM, text) => text.color = vM.ProfileColor, viewModel.cancellationToken);

            profileDataSubscription?.Dispose();
            profileDataSubscription = viewModel.ProfileOptionalBasicInfo.UseCurrentValueAndSubscribeToUpdate(this, (profileInfo, view) =>
            {
                view.profileButton.interactable = profileInfo.DataIsPresent;

                if (profileInfo.DataIsPresent)
                {
                    view.usernameElement.UserNameClicked += OnUsernameClicked;
                    view.usernameElement.SetUsername(profileInfo.UserName, profileInfo.UserWalletId, profileInfo.IsOfficial);
                }
                else
                    view.usernameElement.UserNameClicked -= OnUsernameClicked;
            }, viewModel.cancellationToken);
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

        private void UpdateTranslationViewVisibility()
        {
            // Handle universal conditions where the view should ALWAYS be hidden.
            if (currentViewModel == null || IsTranslationActivated == null || !IsTranslationActivated())
            {
                messageBubbleElement.SetTranslationViewVisibility(false);
                return;
            }

            // Universally show the 'Pending' state (spinner) for immediate feedback.
            // This rule applies to ALL message types (own, system, others) and takes precedence.
            if (currentViewModel.TranslationState == TranslationState.Pending)
            {
                messageBubbleElement.SetTranslationViewVisibility(true);
                return;
            }

            // Handle the special case for the user's OWN messages (for non-pending states).
            if (currentViewModel.Message.IsSentByOwnUser)
            {
                // For own messages, the translation icon (for Success/Failed states) should only
                // appear on hover, as the translation was triggered manually.
                bool isTranslationFinished =
                    currentViewModel.TranslationState == TranslationState.Success ||
                    currentViewModel.TranslationState == TranslationState.Failed;

                messageBubbleElement.SetTranslationViewVisibility(isTranslationFinished && isPointerInside);
                return;
            }

            // Handle ALL OTHER messages (other users' and system messages).
            if (IsAutoTranslationEnabled != null && IsAutoTranslationEnabled())
            {
                // With auto-translate ON, the UI should be clean. The icon is only visible on hover
                // to allow reverting or seeing the original text.
                messageBubbleElement.SetTranslationViewVisibility(isPointerInside);
            }
            else
            {
                // With auto-translate OFF, the icon is visible if the message has been translated
                // (Success/Failed) or if the user is hovering to initiate a manual translation.
                bool isVisible =
                    currentViewModel.TranslationState == TranslationState.Success ||
                    currentViewModel.TranslationState == TranslationState.Failed ||
                    (isPointerInside && currentViewModel.TranslationState == TranslationState.Original);

                messageBubbleElement.SetTranslationViewVisibility(isVisible);
            }
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
