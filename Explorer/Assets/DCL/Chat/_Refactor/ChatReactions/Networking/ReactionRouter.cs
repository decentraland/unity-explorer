using System;

namespace DCL.Chat.ChatReactions
{
    /// <summary>
    /// Central router for all incoming reactions from the network bus.
    /// Subscribes once to <see cref="IReactionMessageBus.ReactionReceived"/> and dispatches
    /// by <see cref="ReactionType"/> so all routing is explicit in one place.
    /// </summary>
    public sealed class ReactionRouter : IDisposable
    {
        private readonly IReactionMessageBus reactionBus;
        private readonly IRemoteReactionTarget situationalTarget;
        private readonly IRemoteReactionTarget messageTarget;

        public ReactionRouter(
            IReactionMessageBus reactionBus,
            IRemoteReactionTarget situationalTarget,
            IRemoteReactionTarget messageTarget)
        {
            this.reactionBus = reactionBus;
            this.situationalTarget = situationalTarget;
            this.messageTarget = messageTarget;

            reactionBus.ReactionReceived += OnReactionReceived;
        }

        public void Dispose()
        {
            reactionBus.ReactionReceived -= OnReactionReceived;
        }

        private void OnReactionReceived(ReactionReceivedArgs args)
        {
            switch (args.Type)
            {
                case ReactionType.Situational:
                    situationalTarget.HandleRemoteReaction(args);
                    break;
                case ReactionType.Message:
                    messageTarget.HandleRemoteReaction(args);
                    break;
            }
        }
    }
}
