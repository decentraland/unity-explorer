using System;
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
        private readonly RemoteReactionReceiver remoteReceiver;
        private readonly LocalPlayerWorldReactor worldReactor;
        private readonly ChatReactionUISimulation uiSimulation;
        private readonly TokenBucketRateLimiter remoteUIBudget;
        private readonly Func<float> getRemoteUIBudgetRate;

        /// <summary>
        /// When false, incoming remote reactions are not shown in the UI lane.
        /// </summary>
        public bool ShowRemoteUIReactions { get; set; } = true;

        /// <summary>
        /// Fired after an incoming remote reaction is processed and displayed.
        /// </summary>
        public event Action<ReactionReceivedArgs>? RemoteReactionProcessed;

        public SituationalRemoteTarget(
            Func<float> getBaseStagger,
            Func<int> getMaxQueueDepth,
            Func<int> getRampStart,
            Func<float> getMinStagger,
            Func<float> getRemoteUIBudgetRate,
            LocalPlayerWorldReactor worldReactor,
            ChatReactionUISimulation uiSimulation)
        {
            this.worldReactor = worldReactor;
            this.uiSimulation = uiSimulation;
            this.getRemoteUIBudgetRate = getRemoteUIBudgetRate;

            float initialRate = getRemoteUIBudgetRate();
            remoteUIBudget = new TokenBucketRateLimiter(initialRate);

            remoteReceiver = new RemoteReactionReceiver(
                getBaseStagger, getMaxQueueDepth, getRampStart, getMinStagger,
                ProcessRemoteReaction);
        }

        public void HandleRemoteReaction(ReactionReceivedArgs args) =>
            remoteReceiver.Enqueue(args);

        public void Tick(float dt)
        {
            remoteUIBudget.Refill(dt, getRemoteUIBudgetRate());
            remoteReceiver.Tick(dt);
        }

        private void ProcessRemoteReaction(ReactionReceivedArgs args)
        {
            worldReactor.TriggerRemoteBurst(args.WalletId, args.EmojiIndex, args.Count);

            bool budgetUnlimited = getRemoteUIBudgetRate() <= 0f;

            if (ShowRemoteUIReactions && (budgetUnlimited || remoteUIBudget.TryConsume()))
                uiSimulation.TriggerUIReaction(args.EmojiIndex, args.Count);

            RemoteReactionProcessed?.Invoke(args);
        }
    }
}
