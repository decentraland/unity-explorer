using System;
using DCL.Chat.ChatReactions.Configs;
using UnityEngine;

namespace DCL.Chat.ChatReactions
{
    public sealed class SituationalReactionService : ISituationalReactionTrigger, IRemoteReactionTarget, ISituationalReactionSimulation, IDisposable
    {
        private readonly ChatReactionsConfig config;
        private readonly ChatReactionUISimulation uiSimulation;
        private readonly ChatReactionWorldSimulation worldSimulation;
        private readonly LocalPlayerWorldReactor worldReactor;
        private readonly RemoteReactionReceiver remoteReceiver;
        private readonly ReactionNetworkBroadcaster networkBroadcaster;

        /// <summary>
        /// When false, no world-space reactions (particles above avatar heads) are spawned.
        /// Controlled by the In-World Chat Bubbles &amp; Reactions setting.
        /// </summary>
        public bool WorldReactionsEnabled
        {
            get => worldReactor.WorldReactionsEnabled;
            set => worldReactor.WorldReactionsEnabled = value;
        }

        /// <summary>
        /// When false, incoming remote reactions are not shown in the UI lane.
        /// User's own reactions are always displayed.
        /// </summary>
        public bool ShowRemoteUIReactions { get; set; } = true;

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
        /// Fired after an incoming remote reaction is processed and displayed.
        /// </summary>
        public event Action<ReactionReceivedArgs>? RemoteReactionProcessed;

        /// <summary>
        /// Fired when the network broadcaster flushes a debounced batch.
        /// Parameters: uniqueEmojiCount, totalCount, unscaledTimestamp.
        /// </summary>
        public event Action<int, int, float>? NetworkFlushed;

        public SituationalReactionService(
            ChatReactionsConfig config,
            ChatReactionUISimulation uiSimulation,
            ChatReactionWorldSimulation worldSimulation,
            IAvatarReactionPosition? avatarPosition = null,
            IReactionMessageBus? reactionBus = null)
        {
            this.config = config;
            this.uiSimulation = uiSimulation;
            this.worldSimulation = worldSimulation;

            worldReactor = new LocalPlayerWorldReactor(worldSimulation, config.WorldLane, avatarPosition);

            remoteReceiver = new RemoteReactionReceiver(
                () => config.MessageReactions.ReceiveStaggerInterval,
                ProcessRemoteReaction);

            networkBroadcaster = new ReactionNetworkBroadcaster(
                reactionBus,
                () => config.MessageReactions.NetworkDebounceSeconds,
                (unique, total, ts) => NetworkFlushed?.Invoke(unique, total, ts));
        }

        public void Dispose()
        {
            networkBroadcaster.Dispose();
            worldReactor.EndStream();
            uiSimulation.Dispose();
            worldSimulation.Dispose();
        }

        public void SetDefaultUISpawnRect(RectTransform rect) =>
            uiSimulation.SetDefaultSpawnRect(rect);

        public void Tick(float dt)
        {
            networkBroadcaster.Tick(dt);
            remoteReceiver.Tick(dt);
            uiSimulation.Tick(dt);
            worldSimulation.Tick(dt);
        }

        public void Draw(Camera cam)
        {
            uiSimulation.Draw(cam);
            worldSimulation.Draw(cam);
        }

        public void HandleRemoteReaction(ReactionReceivedArgs args) =>
            remoteReceiver.Enqueue(args);

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

        private void ProcessRemoteReaction(ReactionReceivedArgs args)
        {
            worldReactor.TriggerRemoteBurst(args.WalletId, args.EmojiIndex, args.Count);

            if (ShowRemoteUIReactions)
                uiSimulation.TriggerUIReaction(args.EmojiIndex, args.Count);

            RemoteReactionProcessed?.Invoke(args);
        }
    }
}
