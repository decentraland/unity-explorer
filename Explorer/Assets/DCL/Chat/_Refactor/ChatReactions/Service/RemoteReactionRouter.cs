using System;

namespace DCL.Chat.ChatReactions
{
    /// <summary>
    /// Routes incoming remote reactions from the network bus to the service.
    /// Owns the bus subscription lifecycle.
    /// </summary>
    public sealed class RemoteReactionRouter : IDisposable
    {
        private readonly IRemoteReactionTarget service;
        private readonly IReactionMessageBus reactionBus;

        public RemoteReactionRouter(IRemoteReactionTarget service, IReactionMessageBus reactionBus)
        {
            this.service = service;
            this.reactionBus = reactionBus;

            reactionBus.ReactionReceived += OnRemoteReaction;
        }

        public void Dispose()
        {
            reactionBus.ReactionReceived -= OnRemoteReaction;
        }

        private void OnRemoteReaction(ReactionReceivedArgs args)
        {
            if (args.Type != ReactionType.Situational) return;
            service.HandleRemoteReaction(args);
        }
    }
}
