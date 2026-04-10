using System;
using DCL.Chat.ChatReactions.Configs;
using DCL.Chat.ChatReactions.Networking;
using DCL.Chat.ChatReactions.Simulation.UI;
using DCL.Chat.ChatReactions.Simulation.World;

namespace DCL.Chat.ChatReactions.Core
{
    /// <summary>
    /// Receives remote situational reactions from the network bus, buffers them
    /// through a stagger interval, and processes each one (world burst + optional UI).
    /// </summary>
    internal sealed class SituationalRemoteTarget : IRemoteReactionTarget
    {
        private readonly ChatReactionsConfig config;
        private readonly RemoteReactionReceiver remoteReceiver;
        private readonly LocalPlayerWorldReactor worldReactor;
        private readonly ChatReactionUISimulation uiSimulation;
        private readonly TokenBucketRateLimiter remoteUIBudget;

        /// <summary>
        /// When false, incoming remote reactions are not shown in the UI lane.
        /// </summary>
        public bool ShowRemoteUIReactions { get; set; } = true;

        /// <summary>
        /// Fired after an incoming remote reaction is processed and displayed.
        /// </summary>
        public event Action<ReactionReceivedArgs>? RemoteReactionProcessed;

        public SituationalRemoteTarget(
            ChatReactionsConfig config,
            LocalPlayerWorldReactor worldReactor,
            ChatReactionUISimulation uiSimulation)
        {
            this.config = config;
            this.worldReactor = worldReactor;
            this.uiSimulation = uiSimulation;

            remoteUIBudget = new TokenBucketRateLimiter(config.MaxRemoteUIReactionsPerSec);

            remoteReceiver = new RemoteReactionReceiver(config, ProcessRemoteReaction);
        }

        public void HandleRemoteReaction(ReactionReceivedArgs args) =>
            remoteReceiver.Enqueue(args);

        public void Tick(float dt)
        {
            remoteUIBudget.Refill(dt, config.MaxRemoteUIReactionsPerSec);
            remoteReceiver.Tick(dt);
        }

        private void ProcessRemoteReaction(ReactionReceivedArgs args)
        {
            worldReactor.TriggerRemoteBurst(args.WalletId, args.EmojiIndex, args.Count);

            bool budgetUnlimited = config.MaxRemoteUIReactionsPerSec <= 0f;

            if (ShowRemoteUIReactions && (budgetUnlimited || remoteUIBudget.TryConsume()))
                uiSimulation.TriggerUIReaction(args.EmojiIndex, args.Count);

            RemoteReactionProcessed?.Invoke(args);
        }
    }
}
