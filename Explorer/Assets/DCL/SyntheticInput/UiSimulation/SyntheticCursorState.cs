using UnityEngine;

namespace DCL.SyntheticInput.UiSimulation
{
    /// <summary>
    ///     What the synthetic input layer tells the cursor systems while it steers the pointer: skip the
    ///     OS-cursor warps instead of fighting the injected positions, and take the pointer's position from the
    ///     gesture rather than from the hardware mouse — the automation mouse is a device of its own, which the
    ///     cursor system's single cached <c>Mouse</c> never resolves, so without this the injected pointer moved
    ///     the UI stack while the world reticle stayed wherever the OS cursor sat.
    ///     Two writers state a position: a running virtual-device gesture, and a held synthetic press, which
    ///     parks the pointer at its own pixel so the ray a scene samples follows the gesture rather than the
    ///     hardware mouse the driver never touched.
    ///     Static by design (the same pattern as PhysicsTickProvider): retail builds never write it, and the frame
    ///     stamps make both signals expire on their own — an aborted gesture leaves no residue to sweep.
    /// </summary>
    public static class SyntheticCursorState
    {
        private static int suppressOsWarpUntilFrame = -1;
        private static int pointerPositionUntilFrame = -1;
        private static Vector2 pointerPosition;

        /// <summary>An automation gesture is steering the pointer this frame; skip OS-cursor warps.</summary>
        public static bool SuppressOsWarp => UnityEngine.Time.frameCount <= suppressOsWarpUntilFrame;

        /// <summary>Keeps the suppression alive through the next frame; re-assert it every gesture frame.</summary>
        public static void AssertSuppressionThisFrame() =>
            suppressOsWarpUntilFrame = UnityEngine.Time.frameCount + 1;

        /// <summary>
        ///     The position an automation gesture queued for the pointer, in Unity screen coordinates, while that
        ///     position is still current. False once the gesture stops re-asserting it, which hands the pointer
        ///     back to the hardware mouse.
        /// </summary>
        public static bool TryGetPointerPosition(out Vector2 position)
        {
            position = pointerPosition;
            return UnityEngine.Time.frameCount <= pointerPositionUntilFrame;
        }

        /// <summary>
        ///     Publishes the position the gesture queued this frame, alive through the next one — the queued state
        ///     event is only consumed by the following input update, so the extra frame keeps the cursor and the
        ///     device from disagreeing across that boundary.
        /// </summary>
        public static void AssertPointerPositionThisFrame(Vector2 position)
        {
            pointerPosition = position;
            pointerPositionUntilFrame = UnityEngine.Time.frameCount + 1;
        }

        /// <summary>Test hygiene only: editor tests share frames, so an asserted state would leak between fixtures.</summary>
        internal static void Reset()
        {
            suppressOsWarpUntilFrame = -1;
            pointerPositionUntilFrame = -1;
            pointerPosition = Vector2.zero;
        }
    }
}
