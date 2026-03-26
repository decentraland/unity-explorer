using System;
using System.Collections.Generic;
using DCL.Chat.ChatReactions.Configs;
using UnityEngine;

namespace DCL.Chat.ChatReactions
{
    public sealed class SituationalReactionService : ISituationalReactionService, IDisposable
    {
        private readonly ChatReactionsConfig config;
        private readonly IAvatarReactionPosition? avatarPosition;
        private readonly ChatReactionUISimulation chatReactionSimulation;
        private readonly ChatReactionWorldSimulation worldReactionSimulation;
        private readonly Func<Vector3?>? cachedLocalHeadGetter;
        private readonly IReactionMessageBus? reactionBus;
        private readonly SituationalReactionDebouncer? debouncer;

        private const int STREAM_EMOJI_INDEX = -1;
        private const int MAX_RECEIVE_EXPAND = 20;

        private readonly Queue<ReactionReceivedArgs> receiveQueue = new (MAX_RECEIVE_EXPAND);
        private float receiveStaggerTimer;

        /// <summary>
        /// When false, no world-space reactions (particles above avatar heads) are spawned.
        /// Controlled by the In-World Chat Bubbles &amp; Reactions setting.
        /// </summary>
        public bool WorldReactionsEnabled { get; set; } = true;

        /// <summary>
        /// When false, incoming remote reactions are not shown in the UI lane.
        /// User's own reactions are always displayed.
        /// </summary>
        public bool ShowRemoteUIReactions { get; set; } = true;

        public SituationalReactionService(
            ChatReactionsConfig config,
            ChatReactionUISimulation uiSimulation,
            ChatReactionWorldSimulation worldSimulation,
            IAvatarReactionPosition? avatarPosition = null,
            IReactionMessageBus? reactionBus = null)
        {
            this.config = config;
            this.chatReactionSimulation = uiSimulation;
            this.worldReactionSimulation = worldSimulation;
            this.avatarPosition = avatarPosition;
            this.reactionBus = reactionBus;

            if (avatarPosition != null)
                cachedLocalHeadGetter = avatarPosition.GetLocalPlayerHeadPosition;

            if (reactionBus != null)
            {
                debouncer = new SituationalReactionDebouncer(
                    FlushDebouncedReactions,
                    () => config.MessageReactions.NetworkDebounceSeconds);
            }
        }

        public void Dispose()
        {
            debouncer?.Dispose();
            worldReactionSimulation.EndStream();
            chatReactionSimulation.Dispose();
            worldReactionSimulation.Dispose();
        }

        public void SetDefaultUISpawnRect(RectTransform rect) =>
            chatReactionSimulation.SetDefaultSpawnRect(rect);

        public void Tick(float dt)
        {
            debouncer?.Tick(dt);
            DrainReceiveQueue(dt);
            chatReactionSimulation.Tick(dt);
            worldReactionSimulation.Tick(dt);
        }

        public void Draw(Camera cam)
        {
            chatReactionSimulation.Draw(cam);
            worldReactionSimulation.Draw(cam);
        }

        public void HandleRemoteReaction(ReactionReceivedArgs args)
        {
            int count = Mathf.Clamp(args.Count, 1, MAX_RECEIVE_EXPAND);

            for (int i = 0; i < count; i++)
            {
                receiveQueue.Enqueue(new ReactionReceivedArgs(
                    args.WalletId, args.EmojiIndex, 1,
                    args.Type, args.MessageId, args.IsRemoval));
            }
        }

        public void TriggerWorldReaction(Vector3 worldPos, int emojiIndex, int count)
        {
            if (!WorldReactionsEnabled) return;
            worldReactionSimulation.TriggerWorldReaction(worldPos, emojiIndex, count);
        }

        public void TriggerWorldReactionForAvatar(string walletId, int emojiIndex, int count)
        {
            if (!WorldReactionsEnabled) return;
            if (avatarPosition == null) return;

            Vector3? headPos = avatarPosition.GetHeadPosition(walletId);
            if (headPos.HasValue)
                worldReactionSimulation.TriggerAnchoredReaction(headPos.Value, walletId, emojiIndex, count);
        }

        public void TriggerRemoteUIReaction(int emojiIndex, int count) =>
            chatReactionSimulation.TriggerUIReaction(emojiIndex, count);

        public void TriggerUIReaction(int emojiIndex, int count)
        {
            chatReactionSimulation.TriggerUIReaction(emojiIndex, count);
            TriggerLocalPlayerWorldReaction(emojiIndex);
            BroadcastToNetwork(emojiIndex);
        }

        public void TriggerUIReactionFromRect(RectTransform sourceRect, int emojiIndex, int count)
        {
            chatReactionSimulation.TriggerUIReactionFromRect(sourceRect, emojiIndex, count);
            TriggerLocalPlayerWorldReaction(emojiIndex);
            BroadcastToNetwork(emojiIndex);
        }

        public void TriggerDefaultUIReaction()
        {
            int emojiIndex = chatReactionSimulation.GetRandomEmojiIndex();
            chatReactionSimulation.TriggerUIReaction(emojiIndex, config.UILane.StreamBurst);
            TriggerLocalPlayerWorldReaction(emojiIndex);
            BroadcastToNetwork(emojiIndex);
        }

        public void TriggerDefaultUIReactionFromRect(RectTransform sourceRect)
        {
            int emojiIndex = chatReactionSimulation.GetRandomEmojiIndex();
            chatReactionSimulation.TriggerUIReactionFromRect(sourceRect, emojiIndex, config.UILane.StreamBurst);
            TriggerLocalPlayerWorldReaction(emojiIndex);
            BroadcastToNetwork(emojiIndex);
        }

        public void BeginUIStream(RectTransform sourceRect)
        {
            chatReactionSimulation.BeginUIStream(sourceRect);

            if (cachedLocalHeadGetter != null && WorldReactionsEnabled)
                worldReactionSimulation.BeginStream(cachedLocalHeadGetter, STREAM_EMOJI_INDEX, AvatarAnchorTable.LOCAL_PLAYER_ID);
        }

        public void EndUIStream()
        {
            chatReactionSimulation.EndUIStream();
            worldReactionSimulation.EndStream();
        }

        public void ToggleUIStream(RectTransform sourceRect)
        {
            chatReactionSimulation.ToggleUIStream(sourceRect);

            bool shouldStreamWorld = chatReactionSimulation.IsStreaming && WorldReactionsEnabled;

            if (cachedLocalHeadGetter == null) return;

            if (shouldStreamWorld)
                worldReactionSimulation.BeginStream(cachedLocalHeadGetter, STREAM_EMOJI_INDEX, AvatarAnchorTable.LOCAL_PLAYER_ID);
            else
                worldReactionSimulation.EndStream();
        }

        private void TriggerLocalPlayerWorldReaction(int emojiIndex)
        {
            if (!WorldReactionsEnabled) return;
            if (avatarPosition == null) return;

            Vector3? headPos = avatarPosition.GetLocalPlayerHeadPosition();
            if (headPos.HasValue)
                worldReactionSimulation.TriggerAnchoredReactionLocalPlayer(headPos.Value, emojiIndex, config.WorldLane.BurstCount);
        }

        private void BroadcastToNetwork(int emojiIndex)
        {
            if (debouncer != null)
                debouncer.Add(emojiIndex);
            else
                reactionBus?.SendSituationalReaction(emojiIndex);
        }

        private void FlushDebouncedReactions(Dictionary<int, int> emojiCounts)
        {
            float baseTimestamp = Time.unscaledTime;
            int offset = 0;

            foreach (var kvp in emojiCounts)
            {
                reactionBus?.SendSituationalReaction(kvp.Key, kvp.Value, baseTimestamp + offset * 0.001f);
                offset++;
            }
        }

        private void DrainReceiveQueue(float dt)
        {
            float interval = config.MessageReactions.ReceiveStaggerInterval;

            if (interval <= 0f)
            {
                while (receiveQueue.Count > 0)
                    ProcessQueuedReaction(receiveQueue.Dequeue());
                return;
            }

            if (receiveQueue.Count == 0)
            {
                receiveStaggerTimer = 0f;
                return;
            }

            receiveStaggerTimer -= dt;

            while (receiveStaggerTimer <= 0f && receiveQueue.Count > 0)
            {
                ProcessQueuedReaction(receiveQueue.Dequeue());
                receiveStaggerTimer += interval;
            }
        }

        private void ProcessQueuedReaction(ReactionReceivedArgs args)
        {
            TriggerWorldReactionForAvatar(args.WalletId, args.EmojiIndex, args.Count);

            if (ShowRemoteUIReactions)
                TriggerRemoteUIReaction(args.EmojiIndex, args.Count);
        }
    }
}
