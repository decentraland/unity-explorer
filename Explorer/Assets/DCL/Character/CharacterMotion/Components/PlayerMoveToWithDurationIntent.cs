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
        public Vector3 StartPosition;
        public Vector3 TargetPosition;
        public Vector3? CameraTarget;
        public Vector3? AvatarTarget;
        public float Duration;
        public float ElapsedTime;

        public PlayerMoveToWithDurationIntent(
            Vector3 startPosition,
            Vector3 targetPosition,
            Vector3? cameraTarget,
            Vector3? avatarTarget,
            float duration)
        {
            StartPosition = startPosition;
            TargetPosition = targetPosition;
            CameraTarget = cameraTarget;
            AvatarTarget = avatarTarget;
            Duration = duration;
            ElapsedTime = 0f;
        }

        public float Progress => Duration > 0f ? Mathf.Clamp01(ElapsedTime / Duration) : 1f;
        public bool IsComplete => ElapsedTime >= Duration;
    }
}

