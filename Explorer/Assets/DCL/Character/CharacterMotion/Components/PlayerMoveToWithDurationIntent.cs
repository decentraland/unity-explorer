using Cysharp.Threading.Tasks;
using UnityEngine;

namespace DCL.CharacterMotion.Components
{
    /// <summary>
    /// Added to the player entity when a smooth movement over duration is requested.
    /// The movement bypasses physics and directly interpolates the transform position.
    /// Removed after the movement is completed.
    /// </summary>
    public struct PlayerMoveToWithDurationIntent
    {
        public readonly Vector3 StartPosition;
        public readonly Vector3 TargetPosition;
        public readonly Vector3? CameraTarget;
        public readonly Vector3? AvatarTarget;
        public readonly float Duration;
        public readonly UniTaskCompletionSource<bool> CompletionSource;
        public float ElapsedTime;

        /// <summary>
        /// Tracks the position from the last frame for animation speed calculation.
        /// </summary>
        public Vector3 LastFramePosition;

        public PlayerMoveToWithDurationIntent(
            Vector3 startPosition,
            Vector3 targetPosition,
            Vector3? cameraTarget,
            Vector3? avatarTarget,
            UniTaskCompletionSource<bool> completionSource,
            float duration)
        {
            StartPosition = startPosition;
            TargetPosition = targetPosition;
            CameraTarget = cameraTarget;
            AvatarTarget = avatarTarget;
            Duration = duration;
            ElapsedTime = 0f;
            LastFramePosition = startPosition;
            CompletionSource = completionSource;
        }

        public float Progress => Duration > 0f ? Mathf.Clamp01(ElapsedTime / Duration) : 1f;
        public bool IsComplete => ElapsedTime >= Duration;
    }
}

