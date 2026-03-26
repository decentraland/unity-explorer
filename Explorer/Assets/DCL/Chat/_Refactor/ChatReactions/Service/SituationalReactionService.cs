using System;
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

        private const int STREAM_EMOJI_INDEX = -1;

        /// <summary>
        /// When false, no world-space reactions (particles above avatar heads) are spawned.
        /// Controlled by the In-World Chat Bubbles &amp; Reactions setting.
        /// </summary>
        public bool WorldReactionsEnabled { get; set; } = true;

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
        }

        public void Dispose()
        {
            worldReactionSimulation.EndStream();
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
            reactionBus?.SendSituationalReaction(emojiIndex);
        }
    }
}
