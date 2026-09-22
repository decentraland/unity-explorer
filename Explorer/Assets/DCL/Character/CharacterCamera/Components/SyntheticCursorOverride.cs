using UnityEngine;

namespace DCL.Character.CharacterCamera.Components
{
    /// <summary>
    ///     The pointer position and OS-cursor warp suppression that an automation gesture asserts on the camera
    ///     entity. A gesture drives a virtual mouse that the cursor system's cached <c>Mouse</c> device never
    ///     resolves, so the gesture states the pointer here. Each assertion lasts through the next frame, because
    ///     queued virtual-device state is consumed only by the next input update.
    /// </summary>
    public struct SyntheticCursorOverride
    {
        /// <summary>The asserted pointer position, in Unity screen coordinates (bottom-left origin).</summary>
        public Vector2 PointerPosition;

        /// <summary>The last frame on which <see cref="PointerPosition" /> is asserted, inclusive.</summary>
        public int PointerPositionUntilFrame;

        /// <summary>The last frame on which OS-cursor warps are suppressed, inclusive.</summary>
        public int SuppressOsWarpUntilFrame;

        public static SyntheticCursorOverride Inactive => new ()
        {
            PointerPositionUntilFrame = -1,
            SuppressOsWarpUntilFrame = -1,
        };

        public readonly bool SuppressOsWarp => UnityEngine.Time.frameCount <= SuppressOsWarpUntilFrame;

        public readonly bool TryGetPointerPosition(out Vector2 position)
        {
            position = PointerPosition;
            return UnityEngine.Time.frameCount <= PointerPositionUntilFrame;
        }

        public void AssertPointerPositionThisFrame(Vector2 position)
        {
            PointerPosition = position;
            PointerPositionUntilFrame = UnityEngine.Time.frameCount + 1;
        }

        public void AssertSuppressionThisFrame() =>
            SuppressOsWarpUntilFrame = UnityEngine.Time.frameCount + 1;
    }
}
