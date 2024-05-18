using DCL.ECSComponents;
using DCL.Interaction.Utility;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using RaycastHit = UnityEngine.RaycastHit;

namespace DCL.Interaction.PlayerOriginated.Components
{
    public struct PlayerOriginRaycastResult
    {
        /// <summary>
        ///     Collider is hit and it belongs to an entity
        /// </summary>
        public bool IsValidHit => entityInfo.HasValue;

        private float distance;
        private GlobalColliderEntityInfo? entityInfo;
        private RaycastHit unityRaycastHit;
        private Ray originRay;

        public PlayerOriginRaycastResult(RaycastHit unityRaycastHit) : this()
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
        public void SetupHit(RaycastHit hitInfo, GlobalColliderEntityInfo entityInfo, float distance)
        {
            unityRaycastHit = hitInfo;
            this.entityInfo = entityInfo;
            this.distance = distance;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public GlobalColliderEntityInfo? GetEntityInfo() =>
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
