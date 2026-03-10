using UnityEngine;

namespace DCL.AvatarRendering.AvatarShape.Components
{
    public struct AvatarBlinkComponent
    {
        /// <summary>
        ///     The eye renderer (Mask_Eyes). Null when setup is pending or the avatar was re-instantiated.
        /// </summary>
        public Renderer EyeRenderer;

        /// <summary>
        ///     Accumulated time since the last blink ended.
        /// </summary>
        public float TimeSinceLastBlink;

        /// <summary>
        ///     Randomised interval (seconds) before the next blink begins.
        /// </summary>
        public float NextBlinkTime;

        /// <summary>
        ///     Accumulated time the current blink has been open (closed-eyes state).
        /// </summary>
        public float BlinkTimer;

        /// <summary>
        ///     True while the avatar is in the closed-eyes state.
        /// </summary>
        public bool IsBlinking;
    }
}
