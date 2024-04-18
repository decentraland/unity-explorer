using UnityEngine;

namespace DCL.Interaction.PlayerOriginated.Components
{
    public struct HoverStateComponent
    {
        /// <summary>
        ///     Collider that was hit last frame
        /// </summary>
        public Collider LastHitCollider;
        public bool IsAtDistance;
        public bool IsHoverOver;
        public bool HasCollider;
    }
}
