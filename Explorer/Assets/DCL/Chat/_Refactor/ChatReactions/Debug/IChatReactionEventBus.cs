#if UNITY_EDITOR
using System;

namespace DCL.Chat.ChatReactions
{
    public readonly struct ReactionSentEvent
    {
        public readonly int EmojiIndex;
        public readonly int Count;
        public readonly float Timestamp;
        public readonly ReactionType Type;

        public ReactionSentEvent(int emojiIndex, int count, float timestamp, ReactionType type)
        {
            EmojiIndex = emojiIndex;
            Count = count;
            Timestamp = timestamp;
            Type = type;
        }
    }

    public readonly struct ReactionReceivedEvent
    {
        public readonly string WalletId;
        public readonly int EmojiIndex;
        public readonly int Count;
        public readonly ReactionType Type;
        public readonly string MessageId;
        public readonly bool IsRemoval;

        public ReactionReceivedEvent(string walletId, int emojiIndex, int count, ReactionType type, string messageId, bool isRemoval)
        {
            WalletId = walletId;
            EmojiIndex = emojiIndex;
            Count = count;
            Type = type;
            MessageId = messageId;
            IsRemoval = isRemoval;
        }
    }

    public readonly struct ReactionFlushedEvent
    {
        public readonly int UniqueEmojiCount;
        public readonly int TotalCount;
        public readonly float Timestamp;

        public ReactionFlushedEvent(int uniqueEmojiCount, int totalCount, float timestamp)
        {
            UniqueEmojiCount = uniqueEmojiCount;
            TotalCount = totalCount;
            Timestamp = timestamp;
        }
    }

    public interface IChatReactionEventBus : IDisposable
    {
        event Action<ReactionSentEvent> ReactionSent;
        event Action<ReactionReceivedEvent> ReactionReceived;
        event Action<ReactionFlushedEvent> ReactionFlushed;

        void NotifySent(in ReactionSentEvent e);
        void NotifyReceived(in ReactionReceivedEvent e);
        void NotifyFlushed(in ReactionFlushedEvent e);
    }
}
#endif
