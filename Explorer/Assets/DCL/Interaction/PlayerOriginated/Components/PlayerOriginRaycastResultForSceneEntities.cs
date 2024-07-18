using DCL.Interaction.Utility;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace DCL.Interaction.PlayerOriginated.Components
{
    public struct PlayerOriginRaycastResultForSceneEntities
    {
        /// <summary>
        ///     Collider is hit and it belongs to an entity
        /// </summary>
        public bool IsValidHit => entityInfo.HasValue;

        private float distance;
        private GlobalColliderSceneEntityInfo? entityInfo;
        private RaycastHit unityRaycastHit;
        private Ray originRay;

        public PlayerOriginRaycastResultForSceneEntities(RaycastHit unityRaycastHit) : this()
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
        public void SetupHit(RaycastHit hitInfo, GlobalColliderSceneEntityInfo sceneEntityInfo, float distance)
        {
            unityRaycastHit = hitInfo;
            this.entityInfo = sceneEntityInfo;
            this.distance = distance;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public GlobalColliderSceneEntityInfo? GetEntityInfo() =>
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
