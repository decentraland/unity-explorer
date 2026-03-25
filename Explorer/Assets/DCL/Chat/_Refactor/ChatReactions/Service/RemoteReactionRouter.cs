using System;

namespace DCL.Chat.ChatReactions
{
    /// <summary>
    /// Routes incoming remote reactions from the network bus to the appropriate
    /// UI and world triggers. Owns the bus subscription lifecycle.
    /// </summary>
    public sealed class RemoteReactionRouter : IDisposable
    {
        private readonly ISituationalReactionService service;
        private readonly IReactionMessageBus reactionBus;

        /// <summary>
        /// When false, incoming remote reactions are not shown in the UI lane.
        /// User's own reactions are always displayed.
        /// </summary>
        public bool ShowRemoteUIReactions { get; set; } = true;

        public RemoteReactionRouter(ISituationalReactionService service, IReactionMessageBus reactionBus)
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

            service.TriggerWorldReactionForAvatar(args.WalletId, args.EmojiIndex, args.Count);

            if (ShowRemoteUIReactions)
                service.TriggerRemoteUIReaction(args.EmojiIndex, args.Count);
        }
    }
}
