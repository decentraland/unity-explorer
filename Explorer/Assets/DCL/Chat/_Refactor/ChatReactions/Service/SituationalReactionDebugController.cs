using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Chat.ChatReactions
{
    /// <summary>
    /// Owns debug stream controls and stats aggregation for the reaction simulations.
    /// Separated from the production reaction classes to keep their APIs focused.
    /// </summary>
    public sealed class SituationalReactionDebugController : IDisposable
    {
        private readonly ChatReactionUISimulation uiSimulation;
        private readonly ChatReactionWorldSimulation worldSimulation;
        private readonly IAvatarReactionPosition? avatarPosition;
        private readonly Func<Vector3?>? cachedLocalHeadGetter;
        private readonly Func<List<Vector3>>? cachedNearbyGetter;

        private const int STREAM_EMOJI_INDEX = -1;

        public SituationalReactionDebugController(
            ChatReactionUISimulation uiSimulation,
            ChatReactionWorldSimulation worldSimulation,
            IAvatarReactionPosition? avatarPosition = null)
        {
            this.uiSimulation = uiSimulation;
            this.worldSimulation = worldSimulation;
            this.avatarPosition = avatarPosition;

            if (avatarPosition != null)
            {
                cachedLocalHeadGetter = avatarPosition.GetLocalPlayerHeadPosition;
                cachedNearbyGetter = avatarPosition.GetAllNearbyHeadPositions;
            }
        }

        /// <summary>
        /// Only cleans up debug-owned streams. The production stream (EndStream)
        /// is owned and cleaned up by SituationalReactionFacade.Dispose().
        /// </summary>
        public void Dispose()
        {
            uiSimulation.EndDebugUIStream();
            worldSimulation.EndDebugNearby();
        }

        public void BeginDebugUIStream(RectTransform? sourceRect = null) =>
            uiSimulation.BeginDebugUIStream(sourceRect);

        public void EndDebugUIStream() =>
            uiSimulation.EndDebugUIStream();

        public void BeginDebugLocalStream()
        {
            if (cachedLocalHeadGetter != null)
                worldSimulation.BeginStream(cachedLocalHeadGetter, STREAM_EMOJI_INDEX, AvatarAnchorTable.LOCAL_PLAYER_ID);
        }

        public void EndDebugLocalStream() =>
            worldSimulation.EndStream();

        public void BeginDebugNearby()
        {
            if (cachedNearbyGetter == null) return;
            worldSimulation.BeginDebugNearby(cachedNearbyGetter);
        }

        public void EndDebugNearby() =>
            worldSimulation.EndDebugNearby();

        public ChatReactionStats GetStats(bool debugNearbyActive) =>
            new (
                uiSimulation.AliveCount,
                uiSimulation.PoolCapacity,
                worldSimulation.AliveCount,
                worldSimulation.VisibleCount,
                worldSimulation.VisibleAnchorCount,
                worldSimulation.PoolCapacity,
                avatarPosition?.GetNearbyAvatarCount() ?? 0,
                uiSimulation.IsStreaming,
                worldSimulation.IsStreaming,
                debugNearbyActive,
                worldSimulation.ActiveAnchorCount,
                worldSimulation.AnchorScanLimit,
                worldSimulation.AnchorSlotCapacity);
    }
}
