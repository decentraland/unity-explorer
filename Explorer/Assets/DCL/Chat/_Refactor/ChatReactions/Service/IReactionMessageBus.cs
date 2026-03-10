using System;

namespace DCL.Chat.ChatReactions
{
    public enum ReactionType
    {
        Situational = 0,
        Message = 1,
    }

    public readonly struct ReactionReceivedArgs
    {
        public readonly string WalletId;
        public readonly int EmojiIndex;
        public readonly int Count;
        public readonly ReactionType Type;
        public readonly string MessageId;

        public ReactionReceivedArgs(string walletId, int emojiIndex, int count, ReactionType type, string messageId)
        {
            WalletId = walletId;
            EmojiIndex = emojiIndex;
            Count = count;
            Type = type;
            MessageId = messageId;
        }
    }

    public interface IReactionMessageBus : IDisposable
    {
        event Action<ReactionReceivedArgs> ReactionReceived;

        void SendSituationalReaction(int emojiIndex);

        void SendMessageReaction(int emojiIndex, string messageId);
    }
}
