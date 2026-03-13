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
        ///     True while the avatar is animating through the blink sequence.
        /// </summary>
        public bool IsBlinking;

        /// <summary>
        ///     Current index within the blink animation sequence (see AvatarFacialExpressionSystem.BLINK_SEQUENCE).
        /// </summary>
        public int FrameIndex;

        /// <summary>
        ///     Accumulated time the current blink frame has been displayed.
        /// </summary>
        public float FrameTimer;

        /// <summary>
        ///     Last applied eye atlas slice index. -1 means no MaterialPropertyBlock override is active (default material).
        /// </summary>
        public int CurrentEyeIndex;

        /// <summary>
        ///     Base eye atlas slice index from the current face expression.
        ///     Applied when the blink animation ends to restore the expression resting state.
        ///     -1 means revert to the material's default texture (open eyes).
        /// </summary>
        public int EyesExpressionIndex;
    }
}
