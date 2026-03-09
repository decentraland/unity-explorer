using System;
using System.Collections.Generic;
using DCL.Chat.ChatReactions.Configs;
using UnityEngine;

namespace DCL.Chat.ChatReactions
{
    public sealed class SituationalReactionService : ISituationalReactionService, IDisposable
    {
        private readonly ChatReactionsSituationalConfig config;
        private readonly IAvatarReactionPosition? avatarPosition;
        private readonly ChatReactionSimulation chatReactionSimulation;
        private readonly ChatReactionWorldSimulation? worldReactionSimulation;
        private readonly Func<Vector3?>? cachedLocalHeadGetter;

#if UNITY_EDITOR || DEBUG
        private readonly Func<List<Vector3>>? cachedNearbyGetter;
        private bool debugActive;
#endif

        public SituationalReactionService(ChatReactionsSituationalConfig config, RectTransform laneRect,
            IAvatarReactionPosition? avatarPosition = null)
        {
            this.config = config;
            this.avatarPosition = avatarPosition;
            chatReactionSimulation = new ChatReactionSimulation(config, laneRect);
            worldReactionSimulation = new ChatReactionWorldSimulation(config);

            if (avatarPosition != null)
            {
                cachedLocalHeadGetter = avatarPosition.GetLocalPlayerHeadPosition;
#if UNITY_EDITOR || DEBUG
                cachedNearbyGetter = avatarPosition.GetAllNearbyHeadPositions;
#endif
            }
        }

        public void Dispose()
        {
            worldReactionSimulation?.EndStream();
#if UNITY_EDITOR || DEBUG
            chatReactionSimulation.EndDebugUIStream();
            worldReactionSimulation?.EndDebugNearby();
#endif
            chatReactionSimulation.Dispose();
            worldReactionSimulation?.Dispose();
        }

        public void Tick(float dt)
        {
            chatReactionSimulation.Tick(dt);
            worldReactionSimulation?.Tick(dt);
        }

        public void Draw(Camera cam)
        {
            chatReactionSimulation.Draw(cam);
            worldReactionSimulation?.Draw(cam);
        }

        public void TriggerWorldReaction(Vector3 worldPos, int emojiIndex, int count) =>
            worldReactionSimulation?.TriggerWorldReaction(worldPos, emojiIndex, count);

        /// <summary>
        /// Spawns a burst above the avatar identified by <paramref name="walletId"/>.
        /// No-op if the avatar is not in the scene or the world simulation is inactive.
        /// </summary>
        public void TriggerWorldReactionForAvatar(string walletId, int emojiIndex, int count)
        {
            if (worldReactionSimulation == null || avatarPosition == null) return;

            Vector3? headPos = avatarPosition.GetHeadPosition(walletId);
            if (headPos.HasValue)
                worldReactionSimulation.TriggerWorldReaction(headPos.Value, emojiIndex, count);
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
            chatReactionSimulation.TriggerDefaultUIReaction();
            TriggerWorldForLocalPlayer(config.UILane.DefaultEmojiIndex);
        }

        public void TriggerDefaultUIReactionFromRect(RectTransform sourceRect)
        {
            chatReactionSimulation.TriggerDefaultUIReactionFromRect(sourceRect);
            TriggerWorldForLocalPlayer(config.UILane.DefaultEmojiIndex);
        }

        public void BeginUIStream(RectTransform sourceRect)
        {
            chatReactionSimulation.BeginUIStream(sourceRect);

            if (worldReactionSimulation != null && cachedLocalHeadGetter != null)
                worldReactionSimulation.BeginStream(cachedLocalHeadGetter, StreamEmojiIndex);
        }

        public void EndUIStream()
        {
            chatReactionSimulation.EndUIStream();
            worldReactionSimulation?.EndStream();
        }

        public void ToggleUIStream(RectTransform sourceRect)
        {
            chatReactionSimulation.ToggleUIStream(sourceRect);

            if (worldReactionSimulation != null && cachedLocalHeadGetter != null)
            {
                if (chatReactionSimulation.IsStreaming)
                    worldReactionSimulation.BeginStream(cachedLocalHeadGetter, StreamEmojiIndex);
                else
                    worldReactionSimulation.EndStream();
            }
        }

#if UNITY_EDITOR || DEBUG
        public void BeginDebugUIStream(RectTransform? sourceRect = null)
        {
            chatReactionSimulation.BeginDebugUIStream(sourceRect);
        }

        public void EndDebugUIStream()
        {
            chatReactionSimulation.EndDebugUIStream();
        }

        public void BeginDebugLocalStream()
        {
            if (worldReactionSimulation != null && cachedLocalHeadGetter != null)
                worldReactionSimulation.BeginStream(cachedLocalHeadGetter, StreamEmojiIndex);
        }

        public void EndDebugLocalStream()
        {
            worldReactionSimulation?.EndStream();
        }

        public void BeginDebugNearby()
        {
            if (worldReactionSimulation == null || cachedNearbyGetter == null) return;
            worldReactionSimulation.BeginDebugNearby(cachedNearbyGetter);
            debugActive = true;
        }

        public void EndDebugNearby()
        {
            worldReactionSimulation?.EndDebugNearby();
            debugActive = false;
        }

        public void ToggleDebugNearbyReactions()
        {
            if (debugActive) EndDebugNearby();
            else BeginDebugNearby();
        }

        public int UIAliveCount => chatReactionSimulation.AliveCount;
        public int UIPoolCapacity => chatReactionSimulation.PoolCapacity;
        public int WorldAliveCount => worldReactionSimulation?.AliveCount ?? 0;
        public int WorldPoolCapacity => worldReactionSimulation?.PoolCapacity ?? 0;
        public bool IsUIStreaming => chatReactionSimulation.IsStreaming;
        public bool IsWorldStreaming => worldReactionSimulation?.IsStreaming ?? false;
        public bool IsDebugNearbyActive => debugActive;
        public int NearbyAvatarCount => cachedNearbyGetter?.Invoke().Count ?? 0;
#endif

        private int StreamEmojiIndex => config.UILane.RandomEmoji ? -1 : config.UILane.DefaultEmojiIndex;

        private void TriggerWorldForLocalPlayer(int emojiIndex)
        {
            if (worldReactionSimulation == null || avatarPosition == null) return;

            Vector3? headPos = avatarPosition.GetLocalPlayerHeadPosition();
            if (headPos.HasValue)
                worldReactionSimulation.TriggerWorldReaction(headPos.Value, emojiIndex, config.WorldLane.BurstCount);
        }
    }
}
