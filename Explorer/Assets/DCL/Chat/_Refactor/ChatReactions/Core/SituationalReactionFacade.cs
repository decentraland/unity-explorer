using System;
using DCL.Chat.ChatReactions.Configs;
using DCL.Chat.ChatReactions.Networking;
using DCL.Chat.ChatReactions.Simulation.UI;
using DCL.Chat.ChatReactions.Simulation.World;
using UnityEngine;

namespace DCL.Chat.ChatReactions.Core
{
    /// <summary>
    /// Handles local user reaction triggers: UI bursts, defaults, streaming,
    /// and the corresponding world-space + network broadcasting side-effects.
    /// </summary>
    public sealed class SituationalReactionFacade : ISituationalReactionTrigger, IDisposable
    {
        private readonly ChatReactionsConfig config;
        private readonly ChatReactionUISimulation uiSimulation;
        private readonly LocalPlayerWorldReactor worldReactor;
        private readonly ReactionNetworkBroadcaster networkBroadcaster;

        /// <summary>
        /// Fired when the local user triggers a situational reaction.
        /// Parameter: emojiIndex used.
        /// </summary>
        public event Action<int>? UserReactedToSituation;

        /// <summary>
        /// Fired after a local situational reaction is triggered.
        /// Parameters: emojiIndex, count, unscaledTimestamp.
        /// </summary>
        public event Action<int, int, float>? ReactionSent;

        /// <summary>
        /// Fired when the network broadcaster flushes a debounced batch.
        /// Parameters: uniqueEmojiCount, totalCount, unscaledTimestamp.
        /// </summary>
        public event Action<int, int, float>? NetworkFlushed;

        /// <summary>
        /// The broadcaster created and owned by this facade.
        /// Exposed so the simulation loop can drive its <c>Tick</c>.
        /// </summary>
        internal ReactionNetworkBroadcaster NetworkBroadcaster => networkBroadcaster;

        internal SituationalReactionFacade(
            ChatReactionsConfig config,
            ChatReactionUISimulation uiSimulation,
            LocalPlayerWorldReactor worldReactor,
            IReactionMessageBus? reactionBus)
        {
            this.config = config;
            this.uiSimulation = uiSimulation;
            this.worldReactor = worldReactor;

            networkBroadcaster = new ReactionNetworkBroadcaster(
                reactionBus,
                () => config.MessageReactions.NetworkDebounceSeconds,
                (unique, total, ts) => NetworkFlushed?.Invoke(unique, total, ts));
        }

        public void Dispose()
        {
            networkBroadcaster.Dispose();
            worldReactor.EndStream();
        }

        public void TriggerUIReaction(int emojiIndex, int count)
        {
            uiSimulation.TriggerUIReaction(emojiIndex, count);
            AfterLocalTrigger(emojiIndex, count);
        }

        public void TriggerUIReactionFromRect(RectTransform sourceRect, int emojiIndex, int count)
        {
            uiSimulation.TriggerUIReactionFromRect(sourceRect, emojiIndex, count);
            AfterLocalTrigger(emojiIndex, count);
        }

        public void TriggerDefaultUIReaction()
        {
            int emojiIndex = uiSimulation.GetRandomEmojiIndex();
            uiSimulation.TriggerUIReaction(emojiIndex, config.UILane.StreamBurst);
            AfterLocalTrigger(emojiIndex, config.UILane.StreamBurst);
        }

        public void TriggerDefaultUIReactionFromRect(RectTransform sourceRect)
        {
            int emojiIndex = uiSimulation.GetRandomEmojiIndex();
            uiSimulation.TriggerUIReactionFromRect(sourceRect, emojiIndex, config.UILane.StreamBurst);
            AfterLocalTrigger(emojiIndex, config.UILane.StreamBurst);
        }

        public void BeginUIStream(RectTransform sourceRect)
        {
            uiSimulation.BeginUIStream(sourceRect);
            worldReactor.BeginStream();
        }

        public void EndUIStream()
        {
            uiSimulation.EndUIStream();
            worldReactor.EndStream();
        }

        public void ToggleUIStream(RectTransform sourceRect)
        {
            uiSimulation.ToggleUIStream(sourceRect);
            worldReactor.SyncStreamState(uiSimulation.IsStreaming);
        }

        private void AfterLocalTrigger(int emojiIndex, int count)
        {
            worldReactor.TriggerLocalBurst(emojiIndex);
            networkBroadcaster.Broadcast(emojiIndex);
            ReactionSent?.Invoke(emojiIndex, count, Time.unscaledTime);
            UserReactedToSituation?.Invoke(emojiIndex);
        }
    }
}
