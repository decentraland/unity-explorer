using System;
using DCL.Chat.ChatReactions.Configs;
using DCL.Chat.ChatReactions.Core;
using DCL.Chat.ChatReactions.Views;
using DCL.Chat.ChatServices;
using DCL.Emoji;
using DCL.Prefs;
using DCL.Settings.Settings;
using UnityEngine;

namespace DCL.Chat.ChatReactions.Presenters
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
        private readonly ISituationalReactionTrigger reactionService;
        private readonly ChatClickDetectionHandler clickDetectionHandler;
        private readonly EmojiPanelReactionBridge emojiPanelBridge;
        private readonly ReactionPanelPositioner panelPositioner;
        private readonly RectTransform buttonRect;
        private readonly ChatSettingsAsset chatSettingsAsset;
        private readonly ChatReactionsSelectorView situationalSelectorView;
        private readonly ChatReactionRecentsService recentsService;
        private ReactionMode currentMode = ReactionMode.Situational;

        // Message mode callbacks — valid only when currentMode == Message
        private Action<int>? messageReactionHandler;
        private Action? messageDismissHandler;
        private Transform? messageAnchor;

        public ChatReactionsPresenter(
            ChatReactionButtonView buttonView,
            ChatReactionsSelectorView situationalSelectorView,
            ChatReactionsSelectorView messageSelectorView,
            ISituationalReactionTrigger reactionService,
            ChatReactionsAtlasConfig atlasConfig,
            ChatReactionRecentsService recentsService,
            int[] fixedDefaults,
            EmojiPanelView emojiPanelView,
            EmojiPanelPresenter emojiPanelPresenter,
            ChatReactionsMessageConfig messageReactionsConfig,
            ChatSettingsAsset chatSettingsAsset,
            RectTransform messageAreaRect)
        {
            this.reactionService = reactionService;
            this.chatSettingsAsset = chatSettingsAsset;
            this.situationalSelectorView = situationalSelectorView;
            this.recentsService = recentsService;

            panelPositioner = new ReactionPanelPositioner(
                messageSelectorView.RectTransform,
                messageAreaRect,
                emojiPanelView,
                messageReactionsConfig);

            emojiPanelBridge = new EmojiPanelReactionBridge(
                emojiPanelPresenter, panelPositioner, atlasConfig);

            emojiPanelBridge.EmojiSelected += OnEmojiPanelSelected;

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

            if (situationalSelectorView.ShowOthersToggle != null)
            {
                situationalSelectorView.ShowOthersToggle.Toggle.onValueChanged.AddListener(OnShowOthersToggleChanged);
                chatSettingsAsset.ChatReactionsEnabledChanged += OnReactionsEnabledSettingChanged;
            }
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
                situationalSelectorView.SetOptionsVisible(true);
                situationalSelectorPresenter.Show();
                SyncShowOthersToggle();
                clickDetectionHandler.Resume();
            }
        }

        private void OnSelectorReactionClicked(int atlasIndex)
        {
            DispatchReaction(atlasIndex);
            ActivePresenter.RecordUsage(atlasIndex);

            if (IsInMessageMode)
                HideBar();
            else
                emojiPanelBridge.Hide();
        }

        private void OnClickOutside()
        {
            HideBar();
        }

        private void HideBar()
        {
            emojiPanelBridge.Hide();
            situationalSelectorPresenter.Hide();
            messageSelectorPresenter.Hide();
            clickDetectionHandler.Pause();
            ClearMessageMode();
            recentsService.FlushIfDirty();
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
            if (IsInMessageMode && !emojiPanelBridge.IsOpen)
                messageSelectorPresenter.Hide();

            emojiPanelBridge.Toggle(
                situationalSelectorPresenter.View.AddButtonRect,
                IsInMessageMode);
        }

        private void OnEmojiPanelSelected(int atlasIndex)
        {
            DispatchReaction(atlasIndex);
            ActivePresenter.RecordUsage(atlasIndex);

            if (IsInMessageMode)
                HideBar();
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

        private void SyncShowOthersToggle()
        {
            SetShowOthersToggleSilently(chatSettingsAsset.chatReactionsEnabled);
        }

        private void OnShowOthersToggleChanged(bool enabled)
        {
            chatSettingsAsset.SetReactionsEnabled(enabled);
            DCLPlayerPrefs.SetBool(DCLPrefKeys.SETTINGS_CHAT_REACTIONS_ENABLED, enabled, save: true);
        }

        private void OnReactionsEnabledSettingChanged(bool enabled)
        {
            SetShowOthersToggleSilently(enabled);
        }

        private void SetShowOthersToggleSilently(bool value)
        {
            if (situationalSelectorView.ShowOthersToggle == null) return;

            situationalSelectorView.ShowOthersToggle.Toggle.SetIsOnWithoutNotify(value);
            situationalSelectorView.ShowOthersToggle.SetToggleGraphics(value);
        }

        public void Dispose()
        {
            recentsService.FlushIfDirty();
            emojiPanelBridge.EmojiSelected -= OnEmojiPanelSelected;
            emojiPanelBridge.Dispose();
            ClearMessageMode();
            clickDetectionHandler.OnClickOutside -= OnClickOutside;
            clickDetectionHandler.Dispose();
            buttonPresenter.ButtonClicked -= OnButtonClicked;
            situationalSelectorPresenter.ReactionClicked -= OnSelectorReactionClicked;
            situationalSelectorPresenter.AddClicked -= OnAddClicked;
            messageSelectorPresenter.ReactionClicked -= OnSelectorReactionClicked;
            messageSelectorPresenter.AddClicked -= OnAddClicked;

            if (situationalSelectorView.ShowOthersToggle != null)
            {
                situationalSelectorView.ShowOthersToggle.Toggle.onValueChanged.RemoveListener(OnShowOthersToggleChanged);
                chatSettingsAsset.ChatReactionsEnabledChanged -= OnReactionsEnabledSettingChanged;
            }

            buttonPresenter.Dispose();
            situationalSelectorPresenter.Dispose();
            messageSelectorPresenter.Dispose();
        }
    }
}
