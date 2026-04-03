using DCL.Chat.ChatReactions.Configs;
using DCL.Emoji;
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
        private readonly EmojiPanelView emojiPanelView;
        private readonly RectTransform emojiPanelRect;
        private readonly Vector2 shortcutsBarOffset;
        private readonly Vector2 emojiPanelOffsetForSituational;

        public ReactionPanelPositioner(
            RectTransform messageSelectorRect,
            EmojiPanelView emojiPanelView,
            ChatReactionsMessageConfig messageConfig)
        {
            this.messageSelectorRect = messageSelectorRect;
            this.emojiPanelView = emojiPanelView;
            emojiPanelRect = (RectTransform)emojiPanelView.transform;
            shortcutsBarOffset = messageConfig.ShortcutsBarOffset;
            emojiPanelOffsetForSituational = messageConfig.EmojiPanelOffset;
        }

        /// <summary>
        /// Positions the message shortcuts bar above the given anchor button,
        /// with its left edge at the button (requires pivot X = 0 on the selector).
        /// Uses the same localPosition pattern as <see cref="PositionEmojiPanelForSituational"/>.
        /// </summary>
        public void PositionShortcutsBarAboveAnchor(RectTransform anchor)
        {
            var parent = (RectTransform)messageSelectorRect.parent;
            Vector3 localPos = parent.InverseTransformPoint(anchor.position);

            messageSelectorRect.localPosition = new Vector3(
                localPos.x + shortcutsBarOffset.x,
                localPos.y + shortcutsBarOffset.y,
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
                localPos.x + emojiPanelOffsetForSituational.x,
                localPos.y + emojiPanelOffsetForSituational.y,
                0f);
        }

        /// <summary>
        /// Positions the emoji panel centered horizontally at its default X,
        /// with its Y aligned to the selector's bottom edge.
        /// Computes the bottom edge from the selector's center pivot and scaled height,
        /// so no manual pixel offset is needed.
        /// </summary>
        public void PositionEmojiPanelForMessage()
        {
            float selectorBottomWorldY = messageSelectorRect.position.y
                - messageSelectorRect.rect.height * 0.5f * messageSelectorRect.lossyScale.y;

            emojiPanelView.PositionCenteredAtWorldY(selectorBottomWorldY);
        }
    }
}
