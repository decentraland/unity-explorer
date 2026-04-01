using System;
using DCL.Chat.ChatReactions.Configs;
using UnityEngine;

namespace DCL.Chat.ChatReactions
{
    /// <summary>
    /// Bridges local UI reaction triggers to world-space particle spawns for the local player.
    /// Encapsulates avatar position lookup, stream management, and the enabled toggle.
    /// </summary>
    internal sealed class LocalPlayerWorldReactor
    {
        private readonly ChatReactionWorldSimulation worldSimulation;
        private readonly ChatReactionsWorldLaneConfig worldConfig;
        private readonly IAvatarReactionPosition? avatarPosition;
        private readonly Func<Vector3?>? cachedLocalHeadGetter;

        private const int STREAM_EMOJI_INDEX = -1;

        public bool WorldReactionsEnabled { get; set; } = true;

        public LocalPlayerWorldReactor(
            ChatReactionWorldSimulation worldSimulation,
            ChatReactionsWorldLaneConfig worldConfig,
            IAvatarReactionPosition? avatarPosition)
        {
            this.worldSimulation = worldSimulation;
            this.worldConfig = worldConfig;
            this.avatarPosition = avatarPosition;

            if (avatarPosition != null)
                cachedLocalHeadGetter = avatarPosition.GetLocalPlayerHeadPosition;
        }

        /// <summary>
        /// Spawns a burst of world particles above the local player's head.
        /// No-op if world reactions are disabled or the avatar hasn't loaded.
        /// </summary>
        public void TriggerLocalBurst(int emojiIndex)
        {
            if (!WorldReactionsEnabled || avatarPosition == null) return;

            Vector3? headPos = avatarPosition.GetLocalPlayerHeadPosition();

            if (headPos.HasValue)
                worldSimulation.TriggerAnchoredReactionLocalPlayer(headPos.Value, emojiIndex, worldConfig.BurstCount);
        }

        /// <summary>
        /// Begins streaming world particles above the local player.
        /// </summary>
        public void BeginStream()
        {
            if (cachedLocalHeadGetter != null && WorldReactionsEnabled)
                worldSimulation.BeginStream(cachedLocalHeadGetter, STREAM_EMOJI_INDEX, AvatarAnchorTable.LOCAL_PLAYER_ID);
        }

        /// <summary>
        /// Ends the local player's world particle stream.
        /// </summary>
        public void EndStream()
        {
            worldSimulation.EndStream();
        }

        /// <summary>
        /// Toggles world stream based on whether UI streaming is active.
        /// </summary>
        public void SyncStreamState(bool uiIsStreaming)
        {
            if (cachedLocalHeadGetter == null) return;

            if (uiIsStreaming && WorldReactionsEnabled)
                worldSimulation.BeginStream(cachedLocalHeadGetter, STREAM_EMOJI_INDEX, AvatarAnchorTable.LOCAL_PLAYER_ID);
            else
                worldSimulation.EndStream();
        }

        /// <summary>
        /// Spawns world particles for a remote reaction above the specified avatar.
        /// </summary>
        public void TriggerRemoteBurst(string walletId, int emojiIndex, int count)
        {
            if (!WorldReactionsEnabled || avatarPosition == null) return;

            Vector3? headPos = avatarPosition.GetHeadPosition(walletId);

            if (headPos.HasValue)
                worldSimulation.TriggerAnchoredReaction(headPos.Value, walletId, emojiIndex, count);
        }
    }
}
