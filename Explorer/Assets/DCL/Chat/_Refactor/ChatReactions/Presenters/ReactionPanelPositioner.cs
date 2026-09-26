using DCL.Chat.ChatReactions.Configs;
using UnityEngine;

namespace DCL.Chat.ChatReactions.Presenters
{
    /// <summary>
    /// Centralizes all positioning logic for reaction UI panels:
    /// the message shortcuts bar and the emoji panel in both
    /// situational and message modes.
    /// </summary>
    public sealed class ReactionPanelPositioner
    {
        private readonly RectTransform messageSelectorRect;
        private readonly RectTransform emojiPanelRect;
        private readonly ChatReactionsMessageConfig config;

        public ReactionPanelPositioner(
            RectTransform messageSelectorRect,
            RectTransform emojiPanelRect,
            ChatReactionsMessageConfig messageConfig)
        {
            this.messageSelectorRect = messageSelectorRect;
            this.emojiPanelRect = emojiPanelRect;
            config = messageConfig;
        }

        /// <summary>
        /// Positions the message shortcuts bar above the given anchor button,
        /// with its left edge at the button (requires pivot X = 0 on the selector).
        /// Uses the same localPosition pattern as <see cref="PositionEmojiPanelForSituational"/>.
        /// </summary>
        public void PositionShortcutsBarAboveAnchor(RectTransform anchor, bool isOwnMessage)
        {
            var parent = (RectTransform)messageSelectorRect.parent;
            Vector3 localPos = parent.InverseTransformPoint(anchor.position);
            Vector2 offset = isOwnMessage ? config.ShortcutsBarOffsetOwnMessage : config.ShortcutsBarOffset;

            messageSelectorRect.localPosition = new Vector3(
                localPos.x + offset.x,
                localPos.y + offset.y,
                0f);
        }

        /// <summary>
        /// Positions the emoji panel relative to the [+] button for situational mode.
        /// Uses local-space conversion so the offset remains resolution-independent.
        /// </summary>
        public void PositionEmojiPanelForSituational(RectTransform addButton)
        {
            var panelParent = (RectTransform)emojiPanelRect.parent;
            Vector3 localPos = panelParent.InverseTransformPoint(addButton.position);

            emojiPanelRect.localPosition = new Vector3(
                localPos.x + config.EmojiPanelOffset.x,
                localPos.y + config.EmojiPanelOffset.y,
                0f);
        }

        /// <summary>
        /// Positions the emoji panel with its left edge aligned to the message
        /// selector bar's left edge, using the same InverseTransformPoint pattern
        /// as <see cref="PositionShortcutsBarAboveAnchor"/>.
        /// Compensates for the panel's centered pivot by shifting right by half its width.
        /// </summary>
        public void PositionEmojiPanelForMessage()
        {
            var panelParent = (RectTransform)emojiPanelRect.parent;
            Vector3 localPos = panelParent.InverseTransformPoint(messageSelectorRect.position);

            float halfWidth = emojiPanelRect.rect.width * 0.5f;

            emojiPanelRect.localPosition = new Vector3(
                localPos.x + halfWidth + config.EmojiPanelMessageOffset.x,
                localPos.y + config.EmojiPanelMessageOffset.y,
                0f);
        }
    }
}
