using System;

namespace DCL.Chat.ChatReactions.Networking
{
    /// <summary>
    ///     Inert bus used while the chat-reactions feature flag is off. It subscribes to no
    ///     message pipe and drops every send, so no reaction traffic is decoded, retained or
    ///     emitted while the feature is unavailable.
    /// </summary>
    public sealed class NullReactionMessageBus : IReactionMessageBus
    {
        /// <summary>
        ///     Never raised — nothing is subscribed, so no reaction can arrive.
        /// </summary>
        public event Action<ReactionReceivedArgs> ReactionReceived
        {
            add { }
            remove { }
        }

        public void Dispose() { }

        public void SendSituationalReaction(int emojiIndex, int count = 1, float overrideTimestamp = 0f) { }

        public void SendMessageReaction(int emojiIndex, string messageId, ReactionChannelRouting routing) { }
    }
}
