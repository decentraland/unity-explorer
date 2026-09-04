using UnityEngine;

namespace DCL.Character.CharacterCamera.Components
{
    /// <summary>
    ///     <para>
    ///         Sits beside <see cref="CursorComponent" /> on the camera entity only while an automation driver is
    ///         installed; retail builds never add it. An automation gesture steers a virtual mouse of its own, which
    ///         the cursor system's single cached <c>Mouse</c> never resolves, so the gesture states the pointer here
    ///         instead: while a position is asserted, the cursor system takes it over the hardware mouse and skips
    ///         its OS-cursor warps, so the UI raycast, the cursor style and the world reticle ray all describe the
    ///         same pointer.
    ///     </para>
    ///     <para>
    ///         Both signals are frame-stamped and expire on their own, the same contract as the pipeline post in
    ///         <c>SyntheticPointerInput</c>: a gesture that stops re-asserting hands the pointer back to the hardware
    ///         mouse, and an aborted gesture leaves no residue to sweep.
    ///     </para>
    /// </summary>
    public struct SyntheticCursorOverride
    {
        /// <summary>The pointer position an automation gesture queued, in Unity screen coordinates (bottom-left origin).</summary>
        public Vector2 PointerPosition;

        /// <summary>The last <see cref="UnityEngine.Time.frameCount" /> on which <see cref="PointerPosition" /> is the pointer.</summary>
        public int PointerPositionUntilFrame;

        /// <summary>The last <see cref="UnityEngine.Time.frameCount" /> on which OS-cursor warps are skipped.</summary>
        public int SuppressOsWarpUntilFrame;

        /// <summary>The component as installed: nothing asserted, so the hardware mouse owns the pointer.</summary>
        public static SyntheticCursorOverride Inactive => new ()
        {
            PointerPositionUntilFrame = -1,
            SuppressOsWarpUntilFrame = -1,
        };

        /// <summary>An automation gesture is steering the pointer this frame; the cursor system skips its OS-cursor warps.</summary>
        public readonly bool SuppressOsWarp => UnityEngine.Time.frameCount <= SuppressOsWarpUntilFrame;

        /// <summary>
        ///     The position an automation gesture queued for the pointer, while that position is still current.
        ///     False once the gesture stops re-asserting it, which hands the pointer back to the hardware mouse.
        /// </summary>
        public readonly bool TryGetPointerPosition(out Vector2 position)
        {
            position = PointerPosition;
            return UnityEngine.Time.frameCount <= PointerPositionUntilFrame;
        }

        /// <summary>
        ///     Publishes the position the gesture queued this frame, alive through the next one: the queued device
        ///     state is only consumed by the following input update, so the extra frame keeps the cursor and the
        ///     device from disagreeing across that boundary.
        /// </summary>
        public void AssertPointerPositionThisFrame(Vector2 position)
        {
            PointerPosition = position;
            PointerPositionUntilFrame = UnityEngine.Time.frameCount + 1;
        }

        /// <summary>Keeps the warp suppression alive through the next frame; re-asserted every gesture frame.</summary>
        public void AssertSuppressionThisFrame() =>
            SuppressOsWarpUntilFrame = UnityEngine.Time.frameCount + 1;
    }
}
