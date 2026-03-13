using System.Collections.Generic;
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
        public readonly List<Renderer> GhostRenderers;

        public AvatarGhostComponent(List<Renderer> ghostRenderers)
        {
            GhostRenderers = ghostRenderers;
            Phase = AvatarGhostPhase.Revealing;
            PhaseElapsed = 0f;
            WearablesHidden = false;
        }
    }
}
