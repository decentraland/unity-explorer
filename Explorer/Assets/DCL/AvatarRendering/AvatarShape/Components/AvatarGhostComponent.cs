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

    /// <summary>
    ///     Terminal tag added once the ghost reveal finishes (Phase reaches <see cref="AvatarGhostPhase.Hidden" />).
    ///     Kept alongside (not replacing) <see cref="AvatarGhostComponent" /> so that AvatarGhostCleanupSystem can
    ///     still find the component and SafeDestroy its GhostMaterial on entity delete. Its sole purpose is to let
    ///     AvatarGhostSystem's per-frame reveal queries filter fully-revealed avatars out of their scanned archetype
    ///     via <c>[None(typeof(AvatarGhostFinishedTag))]</c>, so the steady-state scan set is O(avatars currently
    ///     revealing) instead of O(all avatars ever spawned).
    /// </summary>
    public struct AvatarGhostFinishedTag { }
}
