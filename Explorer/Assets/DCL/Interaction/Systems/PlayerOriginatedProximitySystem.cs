using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using Castle.Core.Internal;
using CrdtEcsBridge.Physics;
using DCL.ECSComponents;
using DCL.Interaction.PlayerOriginated;
using DCL.Interaction.PlayerOriginated.Components;
using DCL.Interaction.Utility;
using DCL.Optimization.Pools;
using ECS.Abstract;
using ECS.SceneLifeCycle;
using SceneRunner.Scene;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Utility;
using RaycastHit = UnityEngine.RaycastHit;

namespace DCL.Interaction.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class PlayerOriginatedProximitySystem : BaseUnityLoopSystem
    {
        /// <summary>
        ///     Angle defining the FOV horizontal angle in front of the player.
        /// </summary>
        private const float PROXIMITY_FOV_ANGLE_DEGREES = 120f;

        /// <summary>
        ///     Maximum distance of the FOV sphere slice in units.
        /// </summary>
        private const float PROXIMITY_DEFAULT_MAX_DISTANCE = 3f;

        /// <summary>
        ///     Max buffer size for OverlapSphereNonAlloc.
        /// </summary>
        private const int PROXIMITY_COLLIDERS_MAX_SIZE = 32;

        /// <summary>
        ///     Default buffer size for OverlapSphereNonAlloc.
        /// </summary>
        private const int PROXIMITY_COLLIDERS_DEFAULT_SIZE = 4;

        /// <summary>
        ///     Threshold used to calculate whether an entity falls inside the FOV sphere slice.
        /// </summary>
        private readonly float fovAngleCosThres = Mathf.Cos(PROXIMITY_FOV_ANGLE_DEGREES * 0.5f * Mathf.Deg2Rad);

        private readonly IEntityCollidersGlobalCache collidersGlobalCache;
        private readonly IScenesCache scenesCache;
        private readonly PlayerInteractionEntity playerInteractionEntity;
        private readonly IComponentPool<Collider[]> collidersBufferPool;

        internal PlayerOriginatedProximitySystem(World world,
            IEntityCollidersGlobalCache collidersGlobalCache,
            IScenesCache scenesCache,
            PlayerInteractionEntity playerInteractionEntity) : base(world)
        {
            this.collidersGlobalCache = collidersGlobalCache;
            this.scenesCache = scenesCache;
            this.playerInteractionEntity = playerInteractionEntity;

            collidersBufferPool = new ComponentPool.WithFactory<Collider[]>(
                createFunc: () => new Collider[PROXIMITY_COLLIDERS_MAX_SIZE],
                defaultCapacity: PROXIMITY_COLLIDERS_DEFAULT_SIZE,
                maxSize: PROXIMITY_COLLIDERS_MAX_SIZE
            );
        }

        protected override void Update(float t) =>
            ProximityCheckQuery(World!);

        [Query]
        [All(typeof(CharacterController))]
        private void ProximityCheck(CharacterController characterController)
        {
            // Use CharacterController center as origin
            Vector3 playerControllerCenterPosition = characterController.transform.TransformPoint(characterController.center);

            // Flatten forward direction for horizontal FOV
            Vector3 playerFlatForward = Vector3.ProjectOnPlane(characterController.transform.forward, Vector3.up).normalized;

            if (!IsPlayerInValidScene())
                return;

            ref ProximityResultForSceneEntities proximityResultForSceneEntities = ref playerInteractionEntity.ProximityResultForSceneEntities;
            proximityResultForSceneEntities.Reset();

            Collider[] buffer = collidersBufferPool.Get();

            uint highestPriority = 0;

            try
            {
                int hitCount = Physics.OverlapSphereNonAlloc(
                    playerControllerCenterPosition,
                    PROXIMITY_DEFAULT_MAX_DISTANCE,
                    buffer,
                    PhysicsLayers.PLAYER_PROXIMITY_MASK
                );

                for (int i = 0; i < hitCount; i++)
                {
                    // Skip if no matching scene entity
                    if (!collidersGlobalCache.TryGetSceneEntity(buffer[i], out GlobalColliderSceneEntityInfo sceneEntityInfo))
                        continue;

                    // Skip if scene entity has no pointer events of proximity type
                    if (!sceneEntityInfo.TryGetPointerEvents(out PBPointerEvents? pointerEvents)
                        || !HasProximityEvent(in pointerEvents!))
                        continue;

                    // Compute vector from player to target's collider closest point
                    Vector3 toTargetVec = GetSafeClosestPoint(buffer[i], playerControllerCenterPosition) - playerControllerCenterPosition;

                    float distanceToPlayer = toTargetVec.magnitude;
                    Vector3 toTargetDir = toTargetVec / distanceToPlayer; // Normalization
                    Vector3 flatToTarget = Vector3.ProjectOnPlane(toTargetDir, Vector3.up).normalized;

                    // TODO (Maurizio) remove after tests
#if UNITY_EDITOR
                    DrawFOV(
                        playerControllerCenterPosition,
                        playerFlatForward,
                        PROXIMITY_DEFAULT_MAX_DISTANCE,
                        Mathf.Acos(fovAngleCosThres) * Mathf.Rad2Deg
                    );
#endif

                    // Skip if outside horizontal FOV
                    if (Vector3.Dot(playerFlatForward, flatToTarget) < fovAngleCosThres)
                        continue;

                    // Skip if we already have a higher priority result
                    uint priorityCandidate = GetHighestPriority(pointerEvents!);
                    if (priorityCandidate < highestPriority)
                        continue;

                    // Skip if we already have a closer result with same priority
                    if (priorityCandidate == highestPriority
                        && distanceToPlayer >= proximityResultForSceneEntities.DistanceToPlayer)
                        continue;

                    //TODO (Maurizio) maybe we should exclude interactable objects from hit?

                    bool hasHit = Physics.Raycast(
                        playerControllerCenterPosition,
                        toTargetDir,
                        out RaycastHit hit,
                        distanceToPlayer,
                        PhysicsLayers.PLAYER_PROXIMITY_MASK);

                    // Skip if obstructed by any collider (except itself)
                    if (hasHit && hit.collider != buffer[i])
                        continue;

                    // New closest unobstructed entity can be set
                    proximityResultForSceneEntities.Set(sceneEntityInfo, buffer[i], distanceToPlayer);
                    highestPriority = priorityCandidate;

                    // TODO (Maurizio) remove after tests
#if UNITY_EDITOR
                    Debug.DrawLine(
                        playerControllerCenterPosition,
                        buffer[i].bounds.ClosestPoint(playerControllerCenterPosition),
                        Color.red
                    );
#endif
                }
            }
            finally
            {
                collidersBufferPool.Release(buffer);
            }

            return;

            bool IsPlayerInValidScene() =>
                scenesCache.TryGetByParcel(playerControllerCenterPosition.ToParcel(), out ISceneFacade currentScene)
                && !currentScene.IsEmpty;

            bool HasProximityEvent(in PBPointerEvents pointerEvents)
            {
                for (int i = 0; i < pointerEvents.PointerEvents.Count; i++)
                    if (pointerEvents.PointerEvents[i].InteractionType == InteractionType.Proximity)
                        return true;

                return false;
            }
        }

        /// <summary>
        /// Returns a reliable closest point from a collider to an origin point.
        /// Collider.ClosestPoint() is supported only for primitive colliders and convex mesh colliders.
        /// So in case of a non-convex mesh collider this returns the closest point on the collider's AABB.
        /// </summary>
        /// <param name="collider">Target collider</param>
        /// <param name="origin">Point to measure from</param>
        /// <returns>Closest safe point on or around the collider</returns>
        private Vector3 GetSafeClosestPoint(Collider collider, Vector3 origin) =>
            collider is MeshCollider { convex: false } ? collider.bounds.ClosestPoint(origin) : collider.ClosestPoint(origin);

        private uint GetHighestPriority(PBPointerEvents pointerEvents)
        {
            uint highestPriority = 0;
            int count = pointerEvents.PointerEvents.Count;

            for  (int i = 0; i < count; i++)
            {
                uint priority = pointerEvents.PointerEvents[i].EventInfo.Priority;
                if (priority > highestPriority)
                    highestPriority = priority;
            }

            return highestPriority;
        }

#if UNITY_EDITOR
        void DrawFOV(Vector3 origin, Vector3 flatForward, float maxDistance, float fovAngle)
        {
            int segments = 20; // smoothness of arc
            float halfFov = fovAngle;

            Quaternion leftRot = Quaternion.AngleAxis(-halfFov, Vector3.up);
            Quaternion rightRot = Quaternion.AngleAxis(halfFov, Vector3.up);

            Vector3 leftDir = leftRot * flatForward;
            Vector3 rightDir = rightRot * flatForward;

            // Draw boundaries
            Debug.DrawLine(origin, origin + (leftDir * maxDistance), Color.blue);
            Debug.DrawLine(origin, origin + (rightDir * maxDistance), Color.blue);

            // Draw arc
            Vector3 prevPoint = origin + leftDir * maxDistance;

            for (int i = 1; i <= segments; i++)
            {
                float lerp = i / (float)segments;
                float angle = Mathf.Lerp(-halfFov, halfFov, lerp);
                Vector3 dir = Quaternion.AngleAxis(angle, Vector3.up) * flatForward;
                Vector3 nextPoint = origin + dir * maxDistance;

                Debug.DrawLine(prevPoint, nextPoint, Color.blue);
                prevPoint = nextPoint;
            }
        }
#endif
    }
}
