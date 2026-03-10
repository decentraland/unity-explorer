using System.Collections.Generic;
using UnityEngine;

namespace DCL.Chat.ChatReactions
{
    /// <summary>
    /// Provides avatar head positions for the reaction particle system.
    /// Abstracts the ECS world and avatar bone lookup so the reaction layer
    /// stays decoupled from avatar rendering internals.
    /// </summary>
    public interface IAvatarReactionPosition
    {
        /// <summary>
        /// Returns the head position of the local player's avatar in world space,
        /// or <c>null</c> if the avatar has not loaded yet.
        /// </summary>
        Vector3? GetLocalPlayerHeadPosition();

        /// <summary>
        /// Returns the head position of the avatar identified by <paramref name="walletId"/>
        /// in world space, or <c>null</c> if that avatar is not currently in the scene.
        /// </summary>
        Vector3? GetHeadPosition(string walletId);

        /// <summary>
        /// Returns head positions of all nearby avatars (remote players) currently
        /// in the scene. The returned list is reused across calls — do not cache it.
        /// </summary>
        List<Vector3> GetAllNearbyHeadPositions();

        /// <summary>
        /// Count from the last <see cref="GetAllNearbyHeadPositions"/> call.
        /// Zero-cost — does not re-query the ECS world.
        /// </summary>
        int LastNearbyCount { get; }
    }
}
