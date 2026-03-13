using UnityEngine;

namespace DCL.AvatarRendering.AvatarShape.Components
{
    /// <summary>
    ///     Holds per-avatar state for mouth phoneme animation.
    ///     Added by AvatarFacialAnimationSystem to fully instantiated avatars.
    /// </summary>
    public struct AvatarMouthAnimationComponent
    {
        /// <summary>
        ///     The mouth renderer (Mask_Mouth). Null when setup is pending or the avatar was re-instantiated.
        /// </summary>
        public Renderer MouthRenderer;

        /// <summary>
        ///     The text currently being animated through phonemes. Null when idle.
        /// </summary>
        public string? AnimatingText;

        /// <summary>
        ///     Current character position within <see cref="AnimatingText"/>.
        /// </summary>
        public int CharacterIndex;

        /// <summary>
        ///     Accumulated time the current character's phoneme has been displayed.
        /// </summary>
        public float CharacterTimer;

        /// <summary>
        ///     Last applied phoneme slice index into the phoneme Texture2DArray.
        ///     -1 means no MaterialPropertyBlock override is active (default material).
        /// </summary>
        public int CurrentPhonemeIndex;

        /// <summary>
        ///     Base mouth atlas slice index from the current face expression.
        ///     Applied when phoneme animation ends to restore the expression resting state.
        ///     -1 means revert to the material's default texture (idle mouth).
        /// </summary>
        public int MouthExpressionIndex;

    }
}
