using UnityEngine;

namespace DCL.Chat.ChatMessages
{
    public static class ReactionPillEvents
    {
        public readonly struct ReactionPillClicked
        {
            public readonly string MessageId;
            public readonly int EmojiIndex;

            public ReactionPillClicked(string messageId, int emojiIndex)
            {
                MessageId = messageId;
                EmojiIndex = emojiIndex;
            }
        }

        public readonly struct ReactionPillHoverEnter
        {
            public readonly string MessageId;
            public readonly int EmojiIndex;
            public readonly RectTransform PillRect;

            public ReactionPillHoverEnter(string messageId, int emojiIndex, RectTransform pillRect)
            {
                MessageId = messageId;
                EmojiIndex = emojiIndex;
                PillRect = pillRect;
            }
        }

        public readonly struct ReactionPillHoverExit
        {
            public readonly int EmojiIndex;

            public ReactionPillHoverExit(int emojiIndex)
            {
                EmojiIndex = emojiIndex;
            }
        }
    }
}
