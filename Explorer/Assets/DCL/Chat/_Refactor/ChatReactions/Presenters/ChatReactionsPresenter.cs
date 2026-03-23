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
    /// Each mode uses its own selector view instance so positioning is handled by the hierarchy.
    /// </summary>
    public sealed class ChatReactionsPresenter : IDisposable
    {
        private enum ReactionMode { Situational, Message }

        private readonly ChatReactionButtonPresenter buttonPresenter;
        private readonly ChatReactionsSelectorPresenter situationalSelectorPresenter;
        private readonly ChatReactionsSelectorPresenter messageSelectorPresenter;
        private readonly ISituationalReactionService reactionService;
        private readonly ChatReactionsAtlasConfig atlasConfig;
        private readonly EmojiPanelPresenter emojiPanelPresenter;
        private readonly ChatClickDetectionHandler clickDetectionHandler;
        private readonly ReactionPanelPositioner panelPositioner;
        private readonly RectTransform buttonRect;

        private bool emojiPanelOpenedByReactions;
        private ReactionMode currentMode = ReactionMode.Situational;

        // Message mode callbacks — valid only when currentMode == Message
        private Action<int>? messageReactionHandler;
        private Action? messageDismissHandler;
        private Transform? messageAnchor;

        public ChatReactionsPresenter(
            ChatReactionButtonView buttonView,
            ChatReactionsSelectorView situationalSelectorView,
            ChatReactionsSelectorView messageSelectorView,
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

            panelPositioner = new ReactionPanelPositioner(
                messageSelectorView.RectTransform,
                emojiPanelView,
                messageReactionsConfig);

            situationalSelectorPresenter = new ChatReactionsSelectorPresenter(
                situationalSelectorView,
                recentsService,
                atlasConfig,
                situationalSelectorView.ItemPrefab,
                fixedDefaults);

            messageSelectorPresenter = new ChatReactionsSelectorPresenter(
                messageSelectorView,
                recentsService,
                atlasConfig,
                messageSelectorView.ItemPrefab,
                fixedDefaults);

            buttonPresenter = new ChatReactionButtonPresenter(buttonView);
            buttonRect = buttonPresenter.ButtonRect;

            // Click-outside detection: ignore the button, both selectors, and emoji panel.
            clickDetectionHandler = new ChatClickDetectionHandler(
                situationalSelectorView.transform,
                buttonView.transform,
                emojiPanelView.transform);

            clickDetectionHandler.AddIgnoredTransform(messageSelectorView.transform);

            clickDetectionHandler.OnClickOutside += OnClickOutside;
            clickDetectionHandler.Pause();

            buttonPresenter.ButtonClicked += OnButtonClicked;
            situationalSelectorPresenter.ReactionClicked += OnSelectorReactionClicked;
            situationalSelectorPresenter.AddClicked += OnAddClicked;
            messageSelectorPresenter.ReactionClicked += OnSelectorReactionClicked;
            messageSelectorPresenter.AddClicked += OnAddClicked;
        }

        private bool IsInMessageMode => currentMode == ReactionMode.Message;

        private ChatReactionsSelectorPresenter ActivePresenter =>
            IsInMessageMode ? messageSelectorPresenter : situationalSelectorPresenter;

        /// <summary>
        /// Opens the message selector bar near a message's reaction button anchor.
        /// </summary>
        public void ShowForMessage(RectTransform anchor, Action<int> onReaction, Action onDismiss)
        {
            HideBar();

            currentMode = ReactionMode.Message;
            messageReactionHandler = onReaction;
            messageDismissHandler = onDismiss;
            messageAnchor = anchor;

            clickDetectionHandler.AddIgnoredTransform(anchor);

            messageSelectorPresenter.Show();
            panelPositioner.PositionShortcutsBarAboveAnchor(anchor);
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
            if (IsInMessageMode)
            {
                HideBar();
                return;
            }

            if (situationalSelectorPresenter.IsVisible)
            {
                HideBar();
            }
            else
            {
                situationalSelectorPresenter.Show();
                clickDetectionHandler.Resume();
            }
        }

        private void OnSelectorReactionClicked(int atlasIndex)
        {
            HideEmojiPanel();
            DispatchReaction(atlasIndex);
            ActivePresenter.RecordUsage(atlasIndex);
        }

        private void OnClickOutside()
        {
            HideBar();
        }

        private void HideBar()
        {
            HideEmojiPanel();
            situationalSelectorPresenter.Hide();
            messageSelectorPresenter.Hide();
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
            currentMode = ReactionMode.Situational;

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
                messageSelectorPresenter.Hide();
                panelPositioner.PositionEmojiPanelForMessage();
            }
            else
            {
                RectTransform addBtn = situationalSelectorPresenter.View.AddButtonRect;
                panelPositioner.PositionEmojiPanelForSituational(addBtn);
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
            if (!TryResolveEmojiAtlasIndex(emojiUnicode, out int atlasIndex)) return;

            DispatchReaction(atlasIndex);
            ActivePresenter.RecordUsage(atlasIndex);
        }

        private bool TryResolveEmojiAtlasIndex(string emojiUnicode, out int atlasIndex)
        {
            atlasIndex = -1;

            if (string.IsNullOrEmpty(emojiUnicode)) return false;
            if (!EmojiCodepointHelper.TryGetSingleCodepoint(emojiUnicode, out uint codepoint)) return false;

            atlasIndex = atlasConfig.GetTileIndexFromUnicode(codepoint);
            return atlasIndex >= 0;
        }

        /// <summary>
        /// Routes a reaction to the appropriate handler based on the current mode.
        /// In message mode, invokes the message reaction callback.
        /// In situational mode, triggers a UI particle reaction.
        /// </summary>
        private void DispatchReaction(int atlasIndex)
        {
            if (IsInMessageMode)
                messageReactionHandler?.Invoke(atlasIndex);
            else
                reactionService.TriggerUIReactionFromRect(buttonRect, atlasIndex, count: 1);
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
            situationalSelectorPresenter.ReactionClicked -= OnSelectorReactionClicked;
            situationalSelectorPresenter.AddClicked -= OnAddClicked;
            messageSelectorPresenter.ReactionClicked -= OnSelectorReactionClicked;
            messageSelectorPresenter.AddClicked -= OnAddClicked;

            buttonPresenter.Dispose();
            situationalSelectorPresenter.Dispose();
            messageSelectorPresenter.Dispose();
        }
    }
}
