using UnityEngine;

namespace DCL.AvatarRendering.AvatarShape.Components
{
    public enum AvatarGhostPhase
    {
        Revealing,
        Visible,
        RevealTransition,
        Hidden,
    }

    public struct AvatarGhostComponent
    {
        public AvatarGhostPhase Phase;
        public float PhaseElapsed;
        public bool WearablesHidden;
        public readonly Renderer GhostRenderer;
        public readonly Material GhostMaterial;

        public AvatarGhostComponent(Renderer ghostRenderer, Material ghostMaterial)
        {
            GhostRenderer = ghostRenderer;
            GhostMaterial = ghostMaterial;
            Phase = AvatarGhostPhase.Revealing;
            PhaseElapsed = 0f;
            WearablesHidden = false;
        }
    }
}
