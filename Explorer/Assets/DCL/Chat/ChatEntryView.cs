using DCL.Chat.ChatMessages;
using DCL.Chat.ChatReactions.Configs;
using DCL.Chat.History;
using DCL.FeatureFlags;
using DCL.UI.ProfileElements;
using DG.Tweening;
using System;
using System.Globalization;
using DCL.Chat.ChatViewModels;
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
        private const string DATE_DIVIDER_YESTERDAY = "Yesterday";

        public delegate void ChatEntryClickedDelegate(string walletAddress, Vector2 contextMenuPosition);

        public ChatEntryClickedDelegate? ChatEntryClicked;
        private Action<string, ChatEntryView>? onMessageContextMenuClicked;
        private Func<bool>? IsTranslationActivated;
        private Func<bool>? IsAutoTranslationEnabled;
        public Action<string>? OnTranslateRequested;
        public Action<string>? OnRevertRequested;

        // Wired once in Awake (reads currentViewModel); the feed view assigns a cached forwarder
        // per offer instead of a fresh per-bind closure on the button.
        public Action<string, ChatEntryView>? OnReactionButtonClicked;

        private bool isPointerInside;

        // Last-bound content identity. When the SAME pooled view model with an unchanged Version
        // is re-offered (RefreshAllShownItem), the expensive rebuild is skipped.
        private ChatMessageViewModel? lastBoundViewModel;
        private int lastBoundVersion;

        // What username/wallet/official was actually last rendered into this cell, so the
        // profile-update callback re-renders only on a real change from the screen (see below).
        private RenderedNameGate renderedNameGate;

                [field: SerializeField] internal RectTransform rectTransform { get; private set; } = null!;
                [field: SerializeField] internal CanvasGroup chatEntryCanvasGroup { get; private set; } = null!;

        [field: Header("Elements")]
                [field: SerializeField] internal ChatEntryUsernameElement usernameElement { get; set; } = null!;
                [field: SerializeField] internal ChatEntryMessageBubbleElement messageBubbleElement { get; private set; } = null!;
                [field: SerializeField] internal RectTransform dateDividerElement { get; private set; } = null!;
                [field: SerializeField] internal TMP_Text dateDividerText { get; private set; } = null!;

        [field: Header("Reactions")]
        [field: SerializeField] internal MessageReactionsView? messageReactionsView { get; private set; }

        [field: Header("Avatar Profile")]
                [field: SerializeField] internal ProfilePictureView ProfilePictureView { get; private set; } = null!;
                [field: SerializeField] internal Button profileButton { get; private set; } = null!;

                [field: SerializeField] private CanvasGroup usernameElementCanvas = null!;

        private ReactivePropertyExtensions.DisposableSubscription<ProfileThumbnailViewModel>? profileSubscription;
        private ReactivePropertyExtensions.DisposableSubscription<ProfileOptionalBasicInfo>? profileDataSubscription;

        private ChatMessage chatMessage;
                private ChatMessageViewModel currentViewModel = null!;
        private readonly Vector3[] cornersCache = new Vector3[4];

        internal bool IsSentByOwnUser => currentViewModel?.Message.IsSentByOwnUser ?? false;

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

            // Wire the reaction button once per cell lifetime (mirrors messageOptionsButton above)
            // instead of RemoveAllListeners + a fresh capturing closure on every bind.
            if (messageBubbleElement.reactionButton != null)
            {
                messageBubbleElement.reactionButton.onClick.AddListener(() =>
                {
                    if (currentViewModel != null)
                        OnReactionButtonClicked?.Invoke(currentViewModel.Message.MessageId, this);
                });
            }
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
            Func<bool> IsAutoTranslationEnabled = null!)
        {
            // Change-gate: when the same reference-stable pooled view model is re-offered unchanged
            // (RefreshAllShownItem), refresh only the cheap per-offer delegates + translation-icon
            // visibility and skip the expensive SetMessageData / UpdateReactions / subscription
            // rebuild. GreyOut / AnimateChatEntry run outside this method, so they still apply.
            if (ReferenceEquals(viewModel, lastBoundViewModel) && viewModel.Version == lastBoundVersion)
            {
                currentViewModel = viewModel;
                chatMessage = viewModel.Message;
                this.IsTranslationActivated = IsTranslationActivated;
                this.IsAutoTranslationEnabled = IsAutoTranslationEnabled;
                this.onMessageContextMenuClicked = onMessageContextMenuClicked;
                ChatEntryClicked = onProfileContextMenuClicked;

                // OnGetItemByIndex calls Reset() (clearing the reactions view) before every
                // SetItemData, so reactions + height must be re-applied even on the gated path;
                // SetMessageData (TMP mesh), username, and subscription rebuild stay skipped.
                if (messageReactionsView != null && FeatureFlagsConfiguration.Instance.IsEnabled(FeatureFlagsStrings.CHAT_REACTIONS_ENABLED))
                {
                    messageReactionsView.CurrentMessageId = viewModel.Message.MessageId;
                    messageReactionsView.UpdateReactions(viewModel.Reactions);
                }

                RecalculateHeight();

                // Depends on the (possibly changed) IsAutoTranslationEnabled result / hover state,
                // neither of which bumps Version, so recompute it here to preserve parity.
                UpdateTranslationViewVisibility();
                return;
            }

            // Latch is re-armed only after a fully successful bind (end of method); clearing it
            // before the first visual write keeps a mid-bind exception from leaving the gate on a
            // half-rendered cell — the next offer then does a full corrective rebind, not a false hit.
            lastBoundViewModel = null;

            // Detach the previous view model's subscriptions before any visual write, so a mid-bind
            // exception cannot leave the old profile still streaming into this cell.
            profileSubscription?.Dispose();
            profileDataSubscription?.Dispose();

            currentViewModel = viewModel;
            this.IsTranslationActivated = IsTranslationActivated;
            this.IsAutoTranslationEnabled = IsAutoTranslationEnabled;
            chatMessage = viewModel.Message;
            usernameElement.SetUsername(chatMessage.SenderValidatedName, chatMessage.SenderWalletId, chatMessage.IsSenderOfficial);
            renderedNameGate.SetRendered(chatMessage.SenderValidatedName, chatMessage.SenderWalletId, chatMessage.IsSenderOfficial);
            messageBubbleElement.SetMessageData(viewModel.DisplayText, chatMessage, viewModel.TranslationState);

            UpdateTranslationViewVisibility();

            dateDividerElement.gameObject.SetActive(viewModel.ShowDateDivider);
            if (viewModel.ShowDateDivider)
                dateDividerText.text = GetDateRepresentation(chatMessage.SentTimestamp!.Value.Date);

            if (messageReactionsView != null && FeatureFlagsConfiguration.Instance.IsEnabled(FeatureFlagsStrings.CHAT_REACTIONS_ENABLED))
            {
                messageReactionsView.CurrentMessageId = viewModel.Message.MessageId;
                messageReactionsView.UpdateReactions(viewModel.Reactions);
            }

            RecalculateHeight();

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
                ProfilePictureView.SetImage(viewModel.ProfileData.Value.Sprite!);

            profileSubscription = viewModel.ProfileData.UseCurrentValueAndSubscribeToUpdate(usernameElement.userName, (vM, text) => text.color = vM.ProfileColor, viewModel.cancellationToken);

            profileDataSubscription = viewModel.ProfileOptionalBasicInfo.UseCurrentValueAndSubscribeToUpdate(this, (profileInfo, view) =>
            {
                view.profileButton.interactable = profileInfo.DataIsPresent;

                if (profileInfo.DataIsPresent)
                {
                    view.usernameElement.UserNameClicked += OnUsernameClicked;

                    // Fires synchronously with the present value, then on every later profile update;
                    // in steady state the first value equals what SetMessageData just rendered, so
                    // re-apply the expensive username/width pipeline only when the profile differs from
                    // what is CURRENTLY on screen — tracked by renderedNameGate, not the immutable
                    // snapshot, so an async name -> A -> name round-trip still re-renders the revert.
                    if (view.renderedNameGate.ShouldRender(profileInfo.UserName, profileInfo.UserWalletId, profileInfo.IsOfficial))
                    {
                        view.usernameElement.SetUsername(profileInfo.UserName, profileInfo.UserWalletId, profileInfo.IsOfficial);
                        view.messageBubbleElement.UpdateName(viewModel.DisplayText, chatMessage, profileInfo.UserName, profileInfo.UserWalletId!);
                    }
                }
                else
                    view.usernameElement.UserNameClicked -= OnUsernameClicked;
            }, viewModel.cancellationToken);

            lastBoundViewModel = viewModel;
            lastBoundVersion = viewModel.Version;
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


        /// <summary>
        /// Recalculates the entry height including reactions.
        /// Called automatically by SetItemData; call again externally if reactions change after binding.
        /// </summary>
        public void RecalculateHeight()
        {
            float reactionsHeight = messageReactionsView != null ? messageReactionsView.CurrentHeight : 0f;
            rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
                messageBubbleElement.backgroundRectTransform.sizeDelta.y + reactionsHeight);
        }

        /// <summary>
        /// Initializes the reactions view if present (system message prefabs lack one).
        /// Safe to call on every bind — the view's internal guard prevents re-initialization.
        /// </summary>
        public void InitializeReactions(ChatReactionsAtlasConfig atlasConfig, string walletAddress,
            ChatReactionsMessageConfig config, ViewEventBus eventBus)
        {
            messageReactionsView?.Initialize(atlasConfig, walletAddress, config, eventBus);
        }

        public void Reset()
        {
            if (!isPointerInside)
                messageBubbleElement.Reset();

            messageReactionsView?.Clear();
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
