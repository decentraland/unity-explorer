using UnityEngine;

namespace DCL.SyntheticInput.Components
{
    /// <summary>
    ///     Present on the player entity while a synthetic pointer button is down: the screen pixel the delivered
    ///     press landed on, which the pointer is parked at until the release is delivered. A driver owns no OS
    ///     cursor, so without this the pointer between the legs is wherever the hardware mouse happens to sit — and
    ///     that position is what both the reticle ray and the scene-facing PBPrimaryPointerInfo feed are built from.
    ///     Parking the press pixel is the human gesture: a mouse held down keeps its pixel while the camera turns
    ///     under it. <see cref="ExpiryTime" /> bounds a hold whose release never arrives.
    /// </summary>
    public struct SyntheticPointerHold
    {
        /// <summary>Unity screen coordinates (bottom-left origin) of the press this hold belongs to.</summary>
        public Vector2 ScreenPosition;

        /// <summary>Value of Time.time past which the hold is dropped even without a release.</summary>
        public float ExpiryTime;
    }
}
