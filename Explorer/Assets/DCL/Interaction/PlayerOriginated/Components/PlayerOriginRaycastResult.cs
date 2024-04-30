using DCL.Interaction.Utility;
using UnityEngine;

namespace DCL.Interaction.PlayerOriginated.Components
{
    public struct PlayerOriginRaycastResult
    {
        /// <summary>
        ///     Collider is hit and it belongs to an entity
        /// </summary>
        public bool IsValidHit => EntityInfo.HasValue;
        public float Distance;

        public GlobalColliderEntityInfo? EntityInfo;

        public RaycastHit UnityRaycastHit;

        public Ray OriginRay;

        public bool TryGetValidHit(out GlobalColliderEntityInfo entityInfo)
        {
            if (IsValidHit)
            {
                entityInfo = EntityInfo!.Value;
                return true;
            }

            entityInfo = new GlobalColliderEntityInfo();
            return false;
        }
    }
}
