using System;
using DCL.Chat.ChatReactions.Configs;
using DCL.Chat.ChatServices;
using DCL.Emoji;
using UnityEngine;

namespace DCL.Chat.ChatReactions
{
    /// <summary>
    /// Top-level presenter that coordinates the reaction button, shortcuts bar,
    /// and emoji panel. Supports two modes:
    /// - Situational mode (default): bar opens from bottom button, emoji clicks fire particles.
    /// - Message mode: bar opens near a message's reaction button, emoji clicks toggle reactions.
    /// </summary>
    public sealed class ChatReactionsPresenter : IDisposable
    {
        private readonly ChatReactionButtonPresenter buttonPresenter;
        private readonly ChatReactionsSelectorPresenter selectorPresenter;
        private readonly ISituationalReactionService reactionService;
        private readonly ChatReactionsAtlasConfig atlasConfig;
        private readonly EmojiPanelPresenter emojiPanelPresenter;
        private readonly ChatClickDetectionHandler clickDetectionHandler;
        private readonly ChatReactionsSelectorView selectorView;
        private readonly RectTransform selectorRect;
        private readonly RectTransform addButtonRect;
        private readonly RectTransform buttonRect;
        private readonly Vector2 shortcutsBarOffset;

        // Original anchored position — restored when leaving message mode
        private readonly Vector2 originalAnchoredPosition;

        private bool emojiPanelOpenedByReactions;

        // Message mode fields — null means situational mode is active
        private Action<int>? messageReactionHandler;
        private Action? messageDismissHandler;
        private Transform? messageAnchor;

        public ChatReactionsPresenter(
            ChatReactionButtonView buttonView,
            ChatReactionsSelectorView selectorView,
            ISituationalReactionService reactionService,
            ChatReactionsAtlasConfig atlasConfig,
            ChatReactionRecentsService recentsService,
            int[] fixedDefaults,
            EmojiPanelView emojiPanelView,
            EmojiPanelPresenter emojiPanelPresenter,
            ChatReactionsMessageConfig messageReactionsConfig)
        {
            this.reactionService = reactionService;
            this.emojiPanelPresenter = emojiPanelPresenter;
            this.atlasConfig = atlasConfig;
            this.selectorView = selectorView;
            selectorRect = selectorView.RectTransform;
            shortcutsBarOffset = messageReactionsConfig.ShortcutsBarOffset;

            // Save original layout position so we can restore it after message mode
            originalAnchoredPosition = selectorRect.anchoredPosition;

            addButtonRect = selectorView.AddButtonRect;

            selectorPresenter = new ChatReactionsSelectorPresenter(
                selectorView,
                recentsService,
                atlasConfig,
                selectorView.ItemPrefab,
                fixedDefaults);

            buttonPresenter = new ChatReactionButtonPresenter(buttonView);
            buttonRect = buttonPresenter.ButtonRect;

            // Click-outside detection: target is the selector, ignore the button and emoji panel.
            clickDetectionHandler = new ChatClickDetectionHandler(
                selectorView.transform,
                buttonView.transform,
                emojiPanelView.transform);

            clickDetectionHandler.OnClickOutside += OnClickOutside;
            clickDetectionHandler.Pause();

            buttonPresenter.ButtonClicked += OnButtonClicked;
            selectorPresenter.ReactionClicked += OnSelectorReactionClicked;
            selectorPresenter.AddClicked += OnAddClicked;
        }

        private bool IsInMessageMode => messageReactionHandler != null;

        /// <summary>
        /// Opens the shortcuts bar near a message's reaction button anchor.
        /// The bar is centered horizontally on the anchor and placed above it.
        /// Emoji clicks will invoke <paramref name="onReaction"/> instead of firing particles.
        /// When the bar closes (click-outside, toggle, or situational button), <paramref name="onDismiss"/> is called.
        /// </summary>
        public void ShowForMessage(RectTransform anchor, Action<int> onReaction, Action onDismiss)
        {
            // Close any existing bar state first
            HideBar();

            messageReactionHandler = onReaction;
            messageDismissHandler = onDismiss;
            messageAnchor = anchor;

            // Add the anchor to click-handler ignore set so clicking the same button toggles
            clickDetectionHandler.AddIgnoredTransform(anchor);

            // Position bar centered above the anchor button
            PositionBarAboveAnchor(anchor);

            selectorPresenter.Show();
            clickDetectionHandler.Resume();
        }

        /// <summary>
        /// Closes the bar if currently in message mode. No-op in situational mode.
        /// </summary>
        public void CloseForMessage()
        {
            if (IsInMessageMode)
                HideBar();
        }

        private void OnButtonClicked()
        {
            // If in message mode, close it first — the bottom button always operates in situational mode
            if (IsInMessageMode)
            {
                HideBar();
                return;
            }

            if (selectorPresenter.IsVisible)
            {
                HideBar();
            }
            else
            {
                selectorPresenter.Show();
                clickDetectionHandler.Resume();
            }
        }

        private void OnSelectorReactionClicked(int atlasIndex)
        {
            if (IsInMessageMode)
            {
                messageReactionHandler!.Invoke(atlasIndex);
            }
            else
            {
                reactionService.TriggerUIReactionFromRect(buttonRect, atlasIndex, count: 1);
            }

            selectorPresenter.RecordUsage(atlasIndex);
        }

        private void OnClickOutside()
        {
            HideBar();
        }

        private void HideBar()
        {
            HideEmojiPanel();
            selectorPresenter.Hide();
            clickDetectionHandler.Pause();
            ClearMessageMode();
        }

        private void ClearMessageMode()
        {
            if (!IsInMessageMode) return;

            if (messageAnchor != null)
                clickDetectionHandler.RemoveIgnoredTransform(messageAnchor);

            Action? dismiss = messageDismissHandler;
            messageReactionHandler = null;
            messageDismissHandler = null;
            messageAnchor = null;

            // Restore bar to its original layout position for situational mode
            selectorRect.anchoredPosition = originalAnchoredPosition;

            // Notify the caller (ChatMessageFeedPresenter) so it can clear its pending state
            dismiss?.Invoke();
        }

        private void OnAddClicked()
        {
            if (emojiPanelOpenedByReactions)
            {
                HideEmojiPanel();
                return;
            }

            ShowEmojiPanel();
        }

        private void ShowEmojiPanel()
        {
            if (IsInMessageMode)
            {
                // In message mode, position emoji panel centered at the bar's height
                // using MoveToWorldY to keep the default horizontal position
                emojiPanelPresenter.MovePanelToWorldY(selectorRect.position.y);
            }
            else
            {
                emojiPanelPresenter.MovePanel(addButtonRect.position);
            }

            emojiPanelPresenter.EmojiSelected += OnEmojiPanelEmojiSelected;
            emojiPanelPresenter.SetPanelVisibility(true);
            emojiPanelOpenedByReactions = true;
        }

        private void HideEmojiPanel()
        {
            if (!emojiPanelOpenedByReactions) return;

            emojiPanelPresenter.EmojiSelected -= OnEmojiPanelEmojiSelected;
            emojiPanelPresenter.SetPanelVisibility(false);
            emojiPanelOpenedByReactions = false;
        }

        private void OnEmojiPanelEmojiSelected(string emojiUnicode)
        {
            if (string.IsNullOrEmpty(emojiUnicode)) return;

            if (!EmojiCodepointHelper.TryGetSingleCodepoint(emojiUnicode, out uint codepoint)) return;

            int atlasIndex = atlasConfig.GetTileIndexFromUnicode(codepoint);
            if (atlasIndex < 0) return;

            if (IsInMessageMode)
            {
                messageReactionHandler!.Invoke(atlasIndex);
            }
            else
            {
                reactionService.TriggerUIReactionFromRect(buttonRect, atlasIndex, count: 1);
            }

            selectorPresenter.RecordUsage(atlasIndex);
        }

        public void Show()
        {
            HideBar();
            buttonPresenter.Show();
        }

        public void Hide()
        {
            HideBar();
            buttonPresenter.Hide();
        }

        public void Dispose()
        {
            HideEmojiPanel();
            ClearMessageMode();
            clickDetectionHandler.OnClickOutside -= OnClickOutside;
            clickDetectionHandler.Dispose();
            buttonPresenter.ButtonClicked -= OnButtonClicked;
            selectorPresenter.ReactionClicked -= OnSelectorReactionClicked;
            selectorPresenter.AddClicked -= OnAddClicked;

            buttonPresenter.Dispose();
            selectorPresenter.Dispose();
        }

        /// <summary>
        /// Positions the shortcuts bar centered horizontally above the anchor,
        /// offset vertically by <see cref="shortcutsBarOffset"/>.y.
        /// </summary>
        private void PositionBarAboveAnchor(RectTransform anchor)
        {
            Vector3 anchorPos = anchor.position;

            // Get the bar's width in world-space so we can center it
            float barWorldWidth = selectorRect.rect.width * selectorRect.lossyScale.x;

            selectorRect.position = new Vector3(
                anchorPos.x - barWorldWidth * 0.5f + shortcutsBarOffset.x,
                anchorPos.y + shortcutsBarOffset.y,
                anchorPos.z);
        }
    }
}
