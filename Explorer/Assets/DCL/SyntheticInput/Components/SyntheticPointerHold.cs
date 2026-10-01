using UnityEngine;

namespace DCL.SyntheticInput.Components
{
    /// <summary>Present on the player entity while a synthetic pointer button is down. Holds the press pixel until the release, because a driver owns no OS cursor.</summary>
    public struct SyntheticPointerHold
    {
        /// <summary>Unity screen coordinates (bottom-left origin) of the press.</summary>
        public Vector2 ScreenPosition;

        /// <summary>Value of Time.time past which the hold is dropped even without a release.</summary>
        public float ExpiryTime;
    }
}
