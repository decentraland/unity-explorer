using System;
using DCL.Chat.History;

namespace DCL.Chat.ChatReactions.Networking
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
        public readonly bool IsRemoval;

        public ReactionReceivedArgs(string walletId, int emojiIndex, int count, ReactionType type, string messageId, bool isRemoval = false)
        {
            WalletId = walletId;
            EmojiIndex = emojiIndex;
            Count = count;
            Type = type;
            MessageId = messageId;
            IsRemoval = isRemoval;
        }
    }

    public readonly struct ReactionChannelRouting
    {
        public readonly ChatChannel.ChatChannelType ChannelType;
        public readonly string ChannelId;

        public ReactionChannelRouting(ChatChannel.ChatChannelType channelType, string channelId)
        {
            ChannelType = channelType;
            ChannelId = channelId;
        }
    }

    public interface IReactionMessageBus : IDisposable
    {
        event Action<ReactionReceivedArgs> ReactionReceived;

        void SendSituationalReaction(int emojiIndex, int count = 1, float overrideTimestamp = 0f);

        void SendMessageReaction(int emojiIndex, string messageId, ReactionChannelRouting routing);
    }
}
