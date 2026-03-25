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

        /// <summary>
        /// When false, incoming remote reactions are not shown in the UI lane.
        /// User's own reactions are always displayed.
        /// </summary>
        public bool ShowRemoteUIReactions { get; set; } = true;

        /// <summary>
        /// When false, no world-space reactions (particles above avatar heads) are spawned.
        /// Controlled by the In-World Chat Bubbles &amp; Reactions setting.
        /// </summary>
        public bool WorldReactionsEnabled { get; set; } = true;

        private readonly Func<List<Vector3>>? cachedNearbyGetter;
        private bool debugActive;

        public SituationalReactionService(ChatReactionsConfig config, RectTransform laneRect,
            IAvatarReactionPosition? avatarPosition = null, IReactionMessageBus? reactionBus = null)
        {
            this.config = config;
            this.avatarPosition = avatarPosition;
            this.reactionBus = reactionBus;

            chatReactionSimulation = new ChatReactionUISimulation(config, laneRect);
            worldReactionSimulation = new ChatReactionWorldSimulation(config, avatarPosition);

            if (avatarPosition != null)
            {
                cachedLocalHeadGetter = avatarPosition.GetLocalPlayerHeadPosition;
                cachedNearbyGetter = avatarPosition.GetAllNearbyHeadPositions;
            }

            if (reactionBus != null)
                reactionBus.ReactionReceived += OnRemoteReaction;
        }

        public void Dispose()
        {
            if (reactionBus != null)
                reactionBus.ReactionReceived -= OnRemoteReaction;

            worldReactionSimulation.EndStream();
            chatReactionSimulation.EndDebugUIStream();
            worldReactionSimulation.EndDebugNearby();
            chatReactionSimulation.Dispose();
            worldReactionSimulation.Dispose();
        }

        public void SetDefaultUISpawnRect(RectTransform rect) =>
            chatReactionSimulation.SetDefaultSpawnRect(rect);

        public void Tick(float dt)
        {
            chatReactionSimulation.Tick(dt);
            worldReactionSimulation.Tick(dt);
        }

        public void Draw(Camera cam)
        {
            chatReactionSimulation.Draw(cam);
            worldReactionSimulation.Draw(cam);
        }

        public void TriggerWorldReaction(Vector3 worldPos, int emojiIndex, int count)
        {
            if (!WorldReactionsEnabled) return;
            worldReactionSimulation.TriggerWorldReaction(worldPos, emojiIndex, count);
        }

        /// <summary>
        /// Spawns a burst above the avatar identified by <paramref name="walletId"/>.
        /// No-op if the avatar is not in the scene or the world simulation is inactive.
        /// </summary>
        public void TriggerWorldReactionForAvatar(string walletId, int emojiIndex, int count)
        {
            if (!WorldReactionsEnabled) return;
            if (avatarPosition == null) return;

            Vector3? headPos = avatarPosition.GetHeadPosition(walletId);
            if (headPos.HasValue)
                worldReactionSimulation.TriggerAnchoredReaction(headPos.Value, walletId, emojiIndex, count);
        }

        public void TriggerUIReaction(int emojiIndex, int count)
        {
            chatReactionSimulation.TriggerUIReaction(emojiIndex, count);
            TriggerWorldForLocalPlayer(emojiIndex);
        }

        public void TriggerUIReactionFromRect(RectTransform sourceRect, int emojiIndex, int count)
        {
            chatReactionSimulation.TriggerUIReactionFromRect(sourceRect, emojiIndex, count);
            TriggerWorldForLocalPlayer(emojiIndex);
        }

        public void TriggerDefaultUIReaction()
        {
            int emojiIndex = chatReactionSimulation.GetRandomEmojiIndex();
            chatReactionSimulation.TriggerUIReaction(emojiIndex, config.UILane.StreamBurst);
            TriggerWorldForLocalPlayer(emojiIndex);
        }

        public void TriggerDefaultUIReactionFromRect(RectTransform sourceRect)
        {
            int emojiIndex = chatReactionSimulation.GetRandomEmojiIndex();
            chatReactionSimulation.TriggerUIReactionFromRect(sourceRect, emojiIndex, config.UILane.StreamBurst);
            TriggerWorldForLocalPlayer(emojiIndex);
        }

        public void BeginUIStream(RectTransform sourceRect)
        {
            chatReactionSimulation.BeginUIStream(sourceRect);

            if (cachedLocalHeadGetter != null && WorldReactionsEnabled)
                worldReactionSimulation.BeginStream(cachedLocalHeadGetter, StreamEmojiIndex, AvatarAnchorTable.LOCAL_PLAYER_ID);
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
                worldReactionSimulation.BeginStream(cachedLocalHeadGetter, StreamEmojiIndex, AvatarAnchorTable.LOCAL_PLAYER_ID);
            else
                worldReactionSimulation.EndStream();
        }

        // Debug controls

        public void BeginDebugUIStream(RectTransform? sourceRect = null) =>
            chatReactionSimulation.BeginDebugUIStream(sourceRect);

        public void EndDebugUIStream() =>
            chatReactionSimulation.EndDebugUIStream();

        public void BeginDebugLocalStream()
        {
            if (cachedLocalHeadGetter != null)
                worldReactionSimulation.BeginStream(cachedLocalHeadGetter, StreamEmojiIndex, AvatarAnchorTable.LOCAL_PLAYER_ID);
        }

        public void EndDebugLocalStream() =>
            worldReactionSimulation.EndStream();

        public void BeginDebugNearby()
        {
            if (cachedNearbyGetter == null) return;
            worldReactionSimulation.BeginDebugNearby(cachedNearbyGetter);
            debugActive = true;
        }

        public void EndDebugNearby()
        {
            worldReactionSimulation.EndDebugNearby();
            debugActive = false;
        }

        public void ToggleDebugNearbyReactions()
        {
            if (debugActive) EndDebugNearby();
            else BeginDebugNearby();
        }

        public ChatReactionStats GetStats() =>
            new (
                chatReactionSimulation.AliveCount,
                chatReactionSimulation.PoolCapacity,
                worldReactionSimulation.AliveCount,
                worldReactionSimulation.VisibleCount,
                worldReactionSimulation.VisibleAnchorCount,
                worldReactionSimulation.PoolCapacity,
                avatarPosition?.GetNearbyAvatarCount() ?? 0,
                chatReactionSimulation.IsStreaming,
                worldReactionSimulation.IsStreaming,
                debugActive);

        private int StreamEmojiIndex => -1;

        private void OnRemoteReaction(ReactionReceivedArgs args)
        {
            if (args.Type != ReactionType.Situational) return;

            if (WorldReactionsEnabled)
                TriggerWorldReactionForAvatar(args.WalletId, args.EmojiIndex, args.Count);

            if (ShowRemoteUIReactions)
                chatReactionSimulation.TriggerUIReaction(args.EmojiIndex, args.Count);
        }

        private void TriggerWorldForLocalPlayer(int emojiIndex)
        {
            if (avatarPosition == null) return;

            Vector3? headPos = avatarPosition.GetLocalPlayerHeadPosition();
            if (!headPos.HasValue) return;

            if (WorldReactionsEnabled)
                worldReactionSimulation.TriggerAnchoredReactionLocalPlayer(headPos.Value, emojiIndex, config.WorldLane.BurstCount);

            reactionBus?.SendSituationalReaction(emojiIndex);
        }
    }
}
