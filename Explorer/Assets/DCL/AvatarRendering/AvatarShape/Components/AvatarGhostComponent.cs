using UnityEngine;

namespace DCL.AvatarRendering.AvatarShape.Components
{
    public enum AvatarGhostPhase
    {
        GhostRevealingTransition,
        Visible,
        FullAvatarRevealing,
        Hidden,
    }

    public struct AvatarGhostComponent
    {
        public AvatarGhostPhase Phase;
        public float PhaseElapsed;
        public bool WearablesHidden;
        public readonly Material GhostMaterial;

        public AvatarGhostComponent(Material ghostMaterial)
        {
            GhostMaterial = ghostMaterial;
            Phase = AvatarGhostPhase.GhostRevealingTransition;
            PhaseElapsed = 0f;
            WearablesHidden = false;
        }
    }
}
