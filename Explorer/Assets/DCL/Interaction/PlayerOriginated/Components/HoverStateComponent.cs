using UnityEngine;

namespace DCL.Interaction.PlayerOriginated.Components
{
    public struct HoverStateComponent
    {
        /// <summary>
        ///     Collider that was hit last frame
        /// </summary>
        public Collider? LastHitCollider;
        public bool IsAtDistance;

        public HoverStateComponent(Collider? lastHitCollider, bool isAtDistance)
        {
            LastHitCollider = lastHitCollider;
            IsAtDistance = isAtDistance;
        }

        public readonly bool HasCollider => LastHitCollider is not null;

        public void Setup(Collider collider, bool isAtDistance)
        {
            LastHitCollider = collider;
            IsAtDistance = isAtDistance;
        }

        public void Reset()
        {
            LastHitCollider = null;
            IsAtDistance = false;
        }
    }
}
