using System;
using UnityEngine;

namespace DCL.Chat.ChatReactions.Simulation.World
{
    /// <summary>
    /// Subset of world-space particle spawning used by <see cref="LocalPlayerWorldReactor"/>.
    /// Extracted from <see cref="ChatReactionWorldSimulation"/> to enable unit testing.
    /// </summary>
    public interface IWorldReactionSpawner
    {
        void TriggerAnchoredReactionLocalPlayer(Vector3 headPos, int emojiIndex, int count);
        void TriggerAnchoredReaction(Vector3 headPos, string walletId, int emojiIndex, int count);
        void BeginStream(Func<Vector3?> positionGetter, int emojiIndex, string? walletId = null);
        void EndStream();
    }
}
