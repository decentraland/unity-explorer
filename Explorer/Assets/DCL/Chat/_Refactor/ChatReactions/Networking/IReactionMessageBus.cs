using System;

namespace DCL.Chat.ChatReactions.Networking
{
    public interface IReactionMessageBus : IDisposable
    {
        event Action<ReactionReceivedArgs> ReactionReceived;

        void SendSituationalReaction(int emojiIndex, int count = 1, float overrideTimestamp = 0f);

        void SendMessageReaction(int emojiIndex, string messageId, ReactionChannelRouting routing);
    }
}
