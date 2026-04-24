using System;
using System.Collections.Generic;
using DCL.Chat.ChatReactions.Networking;

namespace DCL.Chat.ChatReactions.Tests
{
    /// <summary>
    /// Shared test double that records all outgoing calls and allows manual event firing.
    /// Avoids NSubstitute for value-type arg capture on <see cref="IReactionMessageBus"/>.
    /// </summary>
    internal sealed class FakeReactionBus : IReactionMessageBus
    {
        public readonly List<(int EmojiIndex, int Count, float Timestamp)> SituationalSends = new ();
        public readonly List<(int EmojiIndex, string MessageId, ReactionChannelRouting Routing)> MessageSends = new ();

        public event Action<ReactionReceivedArgs>? ReactionReceived;

        public void Fire(ReactionReceivedArgs args) => ReactionReceived?.Invoke(args);

        public void SendSituationalReaction(int emojiIndex, int count = 1, float overrideTimestamp = 0f) =>
            SituationalSends.Add((emojiIndex, count, overrideTimestamp));

        public void SendMessageReaction(int emojiIndex, string messageId, ReactionChannelRouting routing) =>
            MessageSends.Add((emojiIndex, messageId, routing));

        public void Dispose() { }
    }
}
