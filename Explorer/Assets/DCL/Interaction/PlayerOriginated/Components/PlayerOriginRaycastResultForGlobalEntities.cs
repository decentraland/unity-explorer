using DCL.Interaction.Utility;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace DCL.Interaction.PlayerOriginated.Components
{
    public struct PlayerOriginRaycastResultForGlobalEntities
    {
        /// <summary>
        ///     Collider is hit and it belongs to an entity
        /// </summary>
        public bool IsValidHit => entityInfo.HasValue;

        private float distance;
        private GlobalColliderGlobalEntityInfo? entityInfo;
        private RaycastHit unityRaycastHit;
        private Ray originRay;

        public PlayerOriginRaycastResultForGlobalEntities(RaycastHit unityRaycastHit) : this()
        {
            this.unityRaycastHit = unityRaycastHit;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Reset()
        {
            unityRaycastHit = default(RaycastHit);
            entityInfo = null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetRay(Ray ray)
        {
            originRay = ray;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetupHit(RaycastHit hitInfo, GlobalColliderGlobalEntityInfo globalEntityInfo, float distance)
        {
            unityRaycastHit = hitInfo;
            this.entityInfo = globalEntityInfo;
            this.distance = distance;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public GlobalColliderGlobalEntityInfo? GetEntityInfo() =>
            entityInfo;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Collider GetCollider() =>
            unityRaycastHit.collider;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RaycastHit GetRaycastHit() =>
            unityRaycastHit;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Ray GetOriginRay() =>
            originRay;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly float GetDistance() =>
            distance;
    }
}
