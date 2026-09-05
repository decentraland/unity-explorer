using UnityEngine;

namespace DCL.CharacterMotion.Components
{
    /// <summary>
    /// Added to the player entity when a double-click on the floor triggers point-and-click navigation.
    /// The system drives <see cref="MovementInputComponent"/> toward the target using the normal
    /// physics pipeline. Removed when the avatar arrives, is stuck, or manual input is detected.
    /// </summary>
    public struct PointAndClickMovementComponent
    {
        /// <summary>World-space position the player double-clicked.</summary>
        public Vector3 TargetPosition;

        /// <summary>Seconds elapsed since the last stuck-check snapshot was taken.</summary>
        public float StuckCheckElapsed;

        /// <summary>Character XZ position recorded at the start of each stuck-check interval.</summary>
        public Vector3 PositionAtLastStuckCheck;
    }
}
