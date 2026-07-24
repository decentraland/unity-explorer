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

        /// <summary>
        ///     The <see cref="SyntheticPointerInput.AimPoint" /> this frame's ray was deliberately built through,
        ///     regardless of whether the ray hit anything; null when the ray came from the cursor or no ray was
        ///     built at all (cursor panning, in-world camera).
        /// </summary>
        public Vector3? SyntheticAimPoint { get; private set; }

        public GlobalColliderSceneEntityInfo? EntityInfo { get; private set; }

        public float? DistanceToPlayer { get; private set; }

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
        public void SetRay(Ray ray, Vector3? syntheticAimPoint = null)
        {
            OriginRay = ray;
            SyntheticAimPoint = syntheticAimPoint;
        }

        /// <summary>No ray was built this frame, so a stale synthetic-aim echo must not survive into diagnostics.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ClearSyntheticAim()
        {
            SyntheticAimPoint = null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetupHit(RaycastHit hitInfo, GlobalColliderSceneEntityInfo sceneEntityInfo, float hitDistance, float? playerDistance)
        {
            RaycastHit = hitInfo;
            this.EntityInfo = sceneEntityInfo;
            distance = hitDistance;
            DistanceToPlayer = playerDistance;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly float GetDistance() =>
            distance;
    }
}
