using System;
using System.Collections.Generic;
using DCL.Chat.ChatReactions.Configs;
using DCL.Chat.ChatReactions.Networking;
using DCL.Chat.ChatReactions.Simulation.UI;
using DCL.Chat.ChatReactions.Simulation.World;
using UnityEngine;

namespace DCL.Chat.ChatReactions.Core
{
    /// <summary>
    /// Receives remote situational reactions from the network bus, buffers them
    /// through a stagger interval, and processes each one (world burst + optional UI).
    /// </summary>
    internal sealed class SituationalRemoteTarget : IRemoteReactionTarget
    {
        private const int MAX_EXPAND = 20;

        private readonly ChatReactionsConfig config;
        private readonly LocalPlayerWorldReactor worldReactor;
        private readonly ChatReactionUISimulation uiSimulation;
        private readonly TokenBucketRateLimiter remoteUIBudget;
        private readonly Queue<ReactionReceivedArgs> queue = new (MAX_EXPAND);
        private float staggerTimer;

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
        }

        public void HandleRemoteReaction(ReactionReceivedArgs args)
        {
            int maxDepth = config.MaxReceiveQueueDepth;
            int count = Mathf.Clamp(args.Count, 1, MAX_EXPAND);

            for (int i = 0; i < count; i++)
            {
                if (maxDepth > 0 && queue.Count >= maxDepth)
                    break;

                queue.Enqueue(new ReactionReceivedArgs(
                    args.WalletId, args.EmojiIndex, 1,
                    args.Type, args.MessageId, args.IsRemoval));
            }
        }

        public void Tick(float dt)
        {
            remoteUIBudget.Refill(dt, config.MaxRemoteUIReactionsPerSec);
            DrainQueue(dt);
        }

        private void DrainQueue(float dt)
        {
            if (queue.Count == 0)
            {
                staggerTimer = 0f;
                return;
            }

            float staggerInterval = ComputeEffectiveStagger();

            if (staggerInterval <= 0f)
            {
                while (queue.Count > 0)
                    ProcessRemoteReaction(queue.Dequeue());

                return;
            }

            staggerTimer -= dt;

            while (staggerTimer <= 0f && queue.Count > 0)
            {
                ProcessRemoteReaction(queue.Dequeue());
                staggerTimer += staggerInterval;
            }
        }

        private void ProcessRemoteReaction(ReactionReceivedArgs args)
        {
            worldReactor.TriggerRemoteBurst(args.WalletId, args.EmojiIndex, args.Count);

            bool budgetUnlimited = config.MaxRemoteUIReactionsPerSec <= 0f;

            if (ShowRemoteUIReactions && (budgetUnlimited || remoteUIBudget.TryConsume()))
                uiSimulation.TriggerUIReaction(args.EmojiIndex, args.Count);

            RemoteReactionProcessed?.Invoke(args);
        }

        private float ComputeEffectiveStagger()
        {
            float baseStagger = config.MessageReactions.ReceiveStaggerInterval;
            int maxDepth = config.MaxReceiveQueueDepth;
            int rampStart = config.DynamicStaggerRampStart;

            if (maxDepth <= 0 || queue.Count <= rampStart)
                return baseStagger;

            float minStagger = config.MinStaggerInterval;

            if (queue.Count >= maxDepth)
                return minStagger;

            float t = (float)(queue.Count - rampStart) / (maxDepth - rampStart);
            return Mathf.Lerp(baseStagger, minStagger, t);
        }
    }
}
