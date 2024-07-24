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

        public HoverStateComponent(bool isAtDistance, Collider? lastHitCollider, bool hasCollider)
        {
            IsAtDistance = isAtDistance;
            LastHitCollider = lastHitCollider;
            HasCollider = hasCollider;
        }

        public void AssignCollider(Collider collider)
        {
            LastHitCollider = collider;
            HasCollider = true;
        }

        public void Clear()
        {
            LastHitCollider = null;
            IsAtDistance = false;
            HasCollider = false;
        }
    }
}
