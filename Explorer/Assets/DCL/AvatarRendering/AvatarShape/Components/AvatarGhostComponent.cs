using UnityEngine;

namespace DCL.AvatarRendering.AvatarShape.Components
{
    /// <summary>
    ///     Phase of the ghost reveal/hide animation. Reveal (0→2) runs when ghost appears;
    ///     Hide (2→0) runs when wearables are ready, before enabling the full avatar.
    /// </summary>
    public enum AvatarGhostPhase
    {
        Revealing,
        Visible,
        Hiding,
        Hidden,
    }

    /// <summary>
    ///     Controls the ghost renderer visibility on <see cref="UnityInterface.AvatarBase" />.
    ///     Animates the material's RevealPosition: 0→2 (reveal) then 2→0 (hide) before wearables are shown.
    /// </summary>
    public struct AvatarGhostComponent
    {
        public const float REVEAL_DURATION_SEC = 1f;
        public const float HIDE_DURATION_SEC = 1f;
        public const float REVEAL_TARGET = 2f;
        public const float HIDE_TARGET = 0f;

        public static readonly int REVEAL_POSITION_SHADER_ID = Shader.PropertyToID("_RevealPosition");

        public bool Enabled;
        public AvatarGhostPhase Phase;
        public float RevealPosition;
        public float PhaseElapsed;

        public readonly GameObject Ghost;

        public AvatarGhostComponent(GameObject ghost)
        {
            Enabled = true;
            Ghost = ghost;
            Phase = AvatarGhostPhase.Revealing;
            RevealPosition = HIDE_TARGET;
            PhaseElapsed = 0f;
        }

        public void Disable()
        {
            if (Ghost != null)
                Ghost.SetActive(false);
            Enabled = false;
        }

        /// <summary>
        ///     Sets the material's _RevealPosition (float3). Only the y component is animated for the reveal effect.
        /// </summary>
        public void ApplyRevealPositionToMaterial()
        {
            if (Ghost == null) return;

            Renderer r = Ghost.GetComponent<Renderer>();

            if (r != null && r.material != null)
                r.material.SetVector(REVEAL_POSITION_SHADER_ID, new Vector4(0, RevealPosition, 0, 0));
        }
    }
}
