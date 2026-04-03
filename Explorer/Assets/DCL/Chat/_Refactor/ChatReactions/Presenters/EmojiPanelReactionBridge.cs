using System;
using DCL.Chat.ChatReactions.Configs;
using DCL.Chat.ChatReactions.Core;
using DCL.Emoji;
using UnityEngine;

namespace DCL.Chat.ChatReactions.Presenters
{
    /// <summary>
    /// Bridges the full emoji picker panel with the reaction system.
    /// Manages open/close lifecycle, positions the panel for situational vs message modes,
    /// and resolves unicode emoji selections to atlas tile indices.
    /// </summary>
    internal sealed class EmojiPanelReactionBridge : IDisposable
    {
        private readonly EmojiPanelPresenter emojiPanelPresenter;
        private readonly ReactionPanelPositioner panelPositioner;
        private readonly ChatReactionsAtlasConfig atlasConfig;

        private bool isOpen;

        /// <summary>
        /// Fired when the user selects an emoji from the panel.
        /// Parameter: resolved atlas tile index.
        /// </summary>
        public event Action<int>? EmojiSelected;

        public bool IsOpen => isOpen;

        public EmojiPanelReactionBridge(
            EmojiPanelPresenter emojiPanelPresenter,
            ReactionPanelPositioner panelPositioner,
            ChatReactionsAtlasConfig atlasConfig)
        {
            this.emojiPanelPresenter = emojiPanelPresenter;
            this.panelPositioner = panelPositioner;
            this.atlasConfig = atlasConfig;
        }

        /// <summary>
        /// Opens the emoji panel positioned relative to the [+] button in the situational bar.
        /// </summary>
        public void ShowForSituational(RectTransform addButtonRect)
        {
            panelPositioner.PositionEmojiPanelForSituational(addButtonRect);
            Open();
        }

        /// <summary>
        /// Opens the emoji panel positioned below the message shortcuts bar.
        /// </summary>
        public void ShowForMessage()
        {
            panelPositioner.PositionEmojiPanelForMessage();
            Open();
        }

        public void Hide()
        {
            if (!isOpen) return;

            emojiPanelPresenter.EmojiSelected -= OnEmojiSelected;
            emojiPanelPresenter.SetPanelVisibility(false);
            isOpen = false;
        }

        /// <summary>
        /// Toggles the panel. If open, hides it. If closed, opens in the appropriate mode.
        /// </summary>
        public void Toggle(RectTransform addButtonRect, bool isMessageMode)
        {
            if (isOpen)
            {
                Hide();
                return;
            }

            if (isMessageMode)
                ShowForMessage();
            else
                ShowForSituational(addButtonRect);
        }

        public void Dispose()
        {
            Hide();
        }

        private void Open()
        {
            emojiPanelPresenter.EmojiSelected -= OnEmojiSelected;
            emojiPanelPresenter.EmojiSelected += OnEmojiSelected;
            emojiPanelPresenter.SetPanelVisibility(true);
            isOpen = true;
        }

        private void OnEmojiSelected(string emojiUnicode)
        {
            if (string.IsNullOrEmpty(emojiUnicode)) return;
            if (!EmojiCodepointHelper.TryGetSingleCodepoint(emojiUnicode, out uint codepoint)) return;

            int atlasIndex = atlasConfig.GetTileIndexFromUnicode(codepoint);
            if (atlasIndex < 0) return;

            EmojiSelected?.Invoke(atlasIndex);
        }
    }
}
