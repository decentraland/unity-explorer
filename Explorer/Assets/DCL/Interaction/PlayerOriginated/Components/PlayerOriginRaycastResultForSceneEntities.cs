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
        public bool IsValidHit => EntityInfo.HasValue;

        private float distance;

        public readonly Collider Collider => RaycastHit.collider;

        public RaycastHit RaycastHit { get; private set; }

        public Ray OriginRay { get; private set; }

        public GlobalColliderSceneEntityInfo? EntityInfo { get; private set; }

        public float DistanceToPlayer { get; private set; }

        public PlayerOriginRaycastResultForSceneEntities(RaycastHit unityRaycastHit) : this()
        {
            this.RaycastHit = unityRaycastHit;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Reset()
        {
            RaycastHit = default(RaycastHit);
            EntityInfo = null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetRay(Ray ray)
        {
            OriginRay = ray;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetupHit(RaycastHit hitInfo, GlobalColliderSceneEntityInfo sceneEntityInfo, float distance, float playerDistance)
        {
            RaycastHit = hitInfo;
            this.EntityInfo = sceneEntityInfo;
            this.distance = distance;
            DistanceToPlayer = playerDistance;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly float GetDistance() =>
            distance;
    }
}
