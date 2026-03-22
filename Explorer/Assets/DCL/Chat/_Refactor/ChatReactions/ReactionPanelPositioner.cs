using DCL.Chat.ChatReactions.Configs;
using DCL.Emoji;
using UnityEngine;

namespace DCL.Chat.ChatReactions
{
    /// <summary>
    /// Centralizes all positioning logic for reaction UI panels:
    /// the message shortcuts bar and the emoji panel in both
    /// situational and message modes.
    /// </summary>
    public sealed class ReactionPanelPositioner
    {
        private readonly RectTransform messageSelectorRect;
        private readonly EmojiPanelView emojiPanelView;
        private readonly Vector2 shortcutsBarOffset;
        private readonly Vector2 emojiPanelOffset;

        public ReactionPanelPositioner(
            RectTransform messageSelectorRect,
            EmojiPanelView emojiPanelView,
            ChatReactionsMessageConfig messageConfig)
        {
            this.messageSelectorRect = messageSelectorRect;
            this.emojiPanelView = emojiPanelView;
            shortcutsBarOffset = messageConfig.ShortcutsBarOffset;
            emojiPanelOffset = messageConfig.EmojiPanelOffset;
        }

        /// <summary>
        /// Positions the message shortcuts bar horizontally centered in its parent,
        /// vertically above the given anchor button.
        /// </summary>
        public void PositionShortcutsBarAboveAnchor(RectTransform anchor)
        {
            var selectorParent = (RectTransform)messageSelectorRect.parent;
            Vector3 localPos = selectorParent.InverseTransformPoint(anchor.position);

            messageSelectorRect.localPosition = new Vector3(
                shortcutsBarOffset.x,
                localPos.y + shortcutsBarOffset.y,
                0f);
        }

        /// <summary>
        /// Positions the emoji panel relative to the [+] button for situational mode.
        /// </summary>
        public void PositionEmojiPanelForSituational(RectTransform addButton)
        {
            emojiPanelView.MoveTo(addButton.position + (Vector3)emojiPanelOffset);
        }

        /// <summary>
        /// Positions the emoji panel centered horizontally at its default X,
        /// with its bottom aligned to the shortcuts bar's current Y position.
        /// Used in message mode so the panel stays horizontally stable in the chat panel.
        /// </summary>
        public void PositionEmojiPanelForMessage()
        {
            emojiPanelView.PositionCenteredAtWorldY(messageSelectorRect.position.y);
        }
    }
}
