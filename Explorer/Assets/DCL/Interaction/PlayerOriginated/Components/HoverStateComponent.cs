using UnityEngine;

namespace DCL.Interaction.PlayerOriginated.Components
{
    public struct HoverStateComponent
    {
        /// <summary>
        ///     Collider that was hit last frame
        /// </summary>
        public Collider? LastHitCollider { get; private set; }
        public bool HasCollider { get; private set; }

        public bool IsAtDistance;

        public bool IsCursorInteraction;

        /// <summary>
        ///     A hover enter was appended for the hovered entity when it became hovered: at least one of its
        ///     pointer-event entries qualified by distance on that frame. The leave that completes the enter is
        ///     gated on this, not on <see cref="IsAtDistance" />, which follows the last entry iterated and the
        ///     current frame's range.
        /// </summary>
        public bool HoverEnterIssued;

        public HoverStateComponent(bool isAtDistance, Collider? lastHitCollider, bool hasCollider, bool isCursorInteraction)
        {
            IsAtDistance = isAtDistance;
            LastHitCollider = lastHitCollider;
            HasCollider = hasCollider;
            IsCursorInteraction = isCursorInteraction;
            HoverEnterIssued = false;
        }

        public void AssignCollider(Collider collider, bool isAtDistance, bool isCursorInteraction)
        {
            LastHitCollider = collider;
            HasCollider = true;
            IsAtDistance = isAtDistance;
            IsCursorInteraction = isCursorInteraction;
        }

        public void Clear()
        {
            LastHitCollider = null;
            IsAtDistance = false;
            HasCollider = false;
            IsCursorInteraction = false;
            HoverEnterIssued = false;
        }
    }
}
