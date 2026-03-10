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
        public readonly Renderer Ghost;

        public AvatarGhostComponent(Renderer ghost)
        {
            Ghost = ghost;
            Phase = AvatarGhostPhase.Revealing;
            PhaseElapsed = 0f;
        }

    }
}
