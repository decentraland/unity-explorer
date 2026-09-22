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
        ///     True when a hover enter was appended for the hovered entity. Set on the frame the entity became
        ///     hovered and kept until the hover ends.
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
