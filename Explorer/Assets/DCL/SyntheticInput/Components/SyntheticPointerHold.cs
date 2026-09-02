using UnityEngine;

namespace DCL.SyntheticInput.Components
{
    /// <summary>
    ///     <para>
    ///         Present on the player entity while a synthetic pointer button is down: the screen pixel the
    ///         delivered press landed on, which the pointer is parked at until the release is delivered.
    ///     </para>
    ///     <para>
    ///         A driver owns no OS cursor, so without this the pointer between a press and its release is
    ///         wherever the hardware mouse happens to sit — and that position is what both the reticle ray and
    ///         the scene-facing PBPrimaryPointerInfo feed are built from, which left a held-and-turn sweep
    ///         sampling a ray through a pixel the driver never chose. Parking the press pixel is the human
    ///         gesture: a mouse held down keeps its pixel while the camera turns under it, and that is what
    ///         sweeps the sampled ray across the world.
    ///     </para>
    ///     <para>
    ///         <see cref="ExpiryTime" /> bounds a hold whose release never arrives (an abandoned gesture, a
    ///         driver that died), so the pointer is never parked away from the hardware mouse indefinitely.
    ///     </para>
    /// </summary>
    public struct SyntheticPointerHold
    {
        /// <summary>Unity screen coordinates (bottom-left origin) of the press this hold belongs to.</summary>
        public Vector2 ScreenPosition;

        /// <summary>Value of Time.time past which the hold is dropped even without a release.</summary>
        public float ExpiryTime;
    }
}
