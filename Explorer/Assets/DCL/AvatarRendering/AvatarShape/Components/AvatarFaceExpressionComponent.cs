using UnityEngine;

namespace DCL.AvatarRendering.AvatarShape.Components
{
    /// <summary>
    ///     Holds the current face expression state for an avatar.
    ///     The expression is the base layer: eyes and mouth indices are copied into
    ///     <see cref="AvatarBlinkComponent"/> and <see cref="AvatarMouthAnimationComponent"/>
    ///     as the "resting" state that is restored after a blink or mouth pose animation finishes.
    ///     Eyebrows are fully controlled by this component (no other system overrides them).
    ///     Added by <c>AvatarFacialExpressionSystem</c> alongside the blink and mouth components.
    /// </summary>
    public struct AvatarFaceExpressionComponent
    {
        /// <summary>
        ///     The eyebrows renderer (Mask_Eyebrows). Null when setup is pending or the avatar was re-instantiated.
        /// </summary>
        public Renderer EyebrowsRenderer;

        /// <summary>
        ///     Target eyebrows atlas slice index for the current expression.
        /// </summary>
        public int EyebrowsExpressionIndex;

        /// <summary>
        ///     Target eye atlas slice index for the current expression.
        ///     Copied into AvatarBlinkComponent.EyesExpressionIndex when the expression is applied.
        /// </summary>
        public int EyesExpressionIndex;

        /// <summary>
        ///     Target mouth atlas slice index for the current expression.
        ///     Copied into AvatarMouthAnimationComponent.MouthExpressionIndex when the expression is applied.
        /// </summary>
        public int MouthExpressionIndex;

        /// <summary>
        ///     Last applied eyebrows atlas slice index. -1 means no MaterialPropertyBlock override is active.
        /// </summary>
        public int CurrentEyebrowsIndex;

        /// <summary>
        ///     True when the expression values have changed and need to be pushed to the renderers
        ///     and the blink / mouth components this frame.
        /// </summary>
        public bool IsDirty;
    }
}
