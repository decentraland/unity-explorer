using UnityEngine;

namespace DCL.Chat.ChatReactions
{
    /// <summary>
    /// Public contract for triggering GPU-instanced emoji burst reactions.
    /// Consumed by chat UI buttons and any other system that needs to fire reactions
    /// without knowing about the underlying particle simulation.
    /// </summary>
    public interface ISituationalReactionService
    {
        /// <summary>Spawn a burst of emoji particles rising from the bottom of the reaction lane.</summary>
        void TriggerUIReaction(int emojiIndex, int count);

        /// <summary>Spawn a burst of emoji particles rising from the center of a specific UI rect (e.g. a reaction button).</summary>
        void TriggerUIReactionFromRect(RectTransform sourceRect, int emojiIndex, int count);

        /// <summary>Spawn a burst using the configured default emoji and count, rising from the lane bottom.</summary>
        void TriggerDefaultUIReaction();

        /// <summary>Spawn a burst using the configured default emoji and count, rising from a specific UI rect.</summary>
        void TriggerDefaultUIReactionFromRect(RectTransform sourceRect);

        /// <summary>Begin continuous emission from a source rect (hold-to-emit).</summary>
        void BeginUIStream(RectTransform sourceRect);

        /// <summary>Stop continuous emission.</summary>
        void EndUIStream();

        /// <summary>Toggle continuous emission on/off from a source rect.</summary>
        void ToggleUIStream(RectTransform sourceRect);

        /// <summary>
        /// Spawn a burst of emoji particles rising from <paramref name="worldPos"/> in world space
        /// (e.g. above an avatar's head). The position must be pre-resolved by the caller.
        /// </summary>
        void TriggerWorldReaction(Vector3 worldPos, int emojiIndex, int count);

        /// <summary>
        /// Spawns a burst above the avatar identified by <paramref name="walletId"/>.
        /// No-op if the avatar is not in the scene or the world simulation is inactive.
        /// </summary>
        void TriggerWorldReactionForAvatar(string walletId, int emojiIndex, int count);
    }
}
