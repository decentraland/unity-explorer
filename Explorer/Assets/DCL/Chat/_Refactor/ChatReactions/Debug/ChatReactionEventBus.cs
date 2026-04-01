#if UNITY_EDITOR
using System;

namespace DCL.Chat.ChatReactions.Debug
{
    public sealed class ChatReactionEventBus : IChatReactionEventBus
    {
        public event Action<ReactionSentEvent>? ReactionSent;
        public event Action<ReactionReceivedEvent>? ReactionReceived;
        public event Action<ReactionFlushedEvent>? ReactionFlushed;

        public void NotifySent(in ReactionSentEvent e) =>
            ReactionSent?.Invoke(e);

        public void NotifyReceived(in ReactionReceivedEvent e) =>
            ReactionReceived?.Invoke(e);

        public void NotifyFlushed(in ReactionFlushedEvent e) =>
            ReactionFlushed?.Invoke(e);

        public void Dispose()
        {
            ReactionSent = null;
            ReactionReceived = null;
            ReactionFlushed = null;
        }
    }
}
#endif
