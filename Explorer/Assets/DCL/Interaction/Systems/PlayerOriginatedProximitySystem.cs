using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using CrdtEcsBridge.Physics;
using DCL.ECSComponents;
using DCL.Interaction.PlayerOriginated;
using DCL.Interaction.PlayerOriginated.Components;
using DCL.Interaction.Utility;
using DCL.Optimization.Pools;
using ECS.Abstract;
using ECS.SceneLifeCycle;
using SceneRunner.Scene;
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
        private static readonly float FOV_ANGLE_COS_THRES = Mathf.Cos(PROXIMITY_FOV_ANGLE_DEGREES * 0.5f * Mathf.Deg2Rad);

        /// <summary>
        ///     Buffer that holds colliders result for OverlapSphereNonAlloc.
        /// </summary>
        private static readonly Collider[] COLLIDER_BUFFER = new Collider[32];

        /// <summary>
        ///     Precomputed squared threshold
        /// </summary>
        private readonly float sqrFovAngleCosThres = FOV_ANGLE_COS_THRES * FOV_ANGLE_COS_THRES;

        private readonly IEntityCollidersGlobalCache collidersGlobalCache;
        private readonly IScenesCache scenesCache;
        private readonly PlayerInteractionEntity playerInteractionEntity;

        internal PlayerOriginatedProximitySystem(World world,
            IEntityCollidersGlobalCache collidersGlobalCache,
            IScenesCache scenesCache,
            PlayerInteractionEntity playerInteractionEntity) : base(world)
        {
            this.collidersGlobalCache = collidersGlobalCache;
            this.scenesCache = scenesCache;
            this.playerInteractionEntity = playerInteractionEntity;
        }

        protected override void Update(float t) =>
            ProximityCheckQuery(World!);

        [Query]
        [All(typeof(CharacterController))]
        private void ProximityCheck(CharacterController characterController)
        {
            // Use CharacterController center as origin
            Vector3 playerControllerCenterPosition = characterController.transform.TransformPoint(characterController.center);

            if (!IsPlayerInValidScene())
                return;

            // Flatten forward direction for horizontal FOV
            Vector3 playerFlatForward = Vector3.ProjectOnPlane(characterController.transform.forward, Vector3.up).normalized;

            ref ProximityResultForSceneEntities proximityResultForSceneEntities = ref playerInteractionEntity.ProximityResultForSceneEntities;
            proximityResultForSceneEntities.Reset();

            int hitCount = Physics.OverlapSphereNonAlloc(
                playerControllerCenterPosition,
                PROXIMITY_DEFAULT_MAX_DISTANCE,
                COLLIDER_BUFFER,
                PhysicsLayers.PLAYER_PROXIMITY_MASK
            );

            uint highestPriority = 0;

            for (int i = 0; i < hitCount; i++)
            {
                // Skip if no matching scene entity
                if (!collidersGlobalCache.TryGetSceneEntity(COLLIDER_BUFFER[i], out GlobalColliderSceneEntityInfo sceneEntityInfo))
                    continue;

                // Skip if scene entity has no pointer events of proximity type
                if (!sceneEntityInfo.TryGetPointerEvents(out PBPointerEvents? pointerEvents)
                    || !HasProximityEvent(in pointerEvents!))
                    continue;

                // Compute vector from player to target's collider closest point
                Vector3 toTargetVec = GetSafeClosestPoint(COLLIDER_BUFFER[i], playerControllerCenterPosition) - playerControllerCenterPosition;

                float sqrDistanceToPlayer = toTargetVec.sqrMagnitude;

                // Get minimum max player distance and highest priority among pointer events entries
                GetMaxDistanceAndHighestPriority(pointerEvents!, out float maxPlayerDistance, out uint priority);
                float sqrMaxPlayerDistance = maxPlayerDistance * maxPlayerDistance;

                // Skip if no pointer event is close enough
                if (sqrDistanceToPlayer > sqrMaxPlayerDistance)
                    continue;

                // The dot calculation is done without normalizing to be as cheap as we can (normalization == sqrt)
                Vector3 flatToTarget = Vector3.ProjectOnPlane(toTargetVec, Vector3.up);

                // Skip if zero-length vector
                float flatSqrMag = flatToTarget.sqrMagnitude;

                if (flatSqrMag < 0.000001f)
                    continue;

                float dot = Vector3.Dot(playerFlatForward, flatToTarget);
                float sqrDot = dot * dot;

                // Skip if outside FOV cone
                if (sqrDot < flatSqrMag * sqrFovAngleCosThres)
                    continue;

                // Skip if target is not in front
                if (dot <= 0f)
                    continue;

                // Skip if we already have a higher priority result
                if (priority < highestPriority)
                    continue;

                float closestDistanceToPlayer = proximityResultForSceneEntities.DistanceToPlayer;
                float sqrClosestDistanceToPlayer = closestDistanceToPlayer * closestDistanceToPlayer;

                // Skip if we already have a closer result with same priority
                if (priority == highestPriority
                    && sqrDistanceToPlayer >= sqrClosestDistanceToPlayer)
                    continue;

                // Finally sqrt only for valid candidates
                float distanceToPlayer = Mathf.Sqrt(sqrDistanceToPlayer);
                Vector3 toTargetDir = toTargetVec / distanceToPlayer;

                bool hasHit = Physics.Raycast(
                    playerControllerCenterPosition,
                    toTargetDir,
                    out RaycastHit hit,
                    distanceToPlayer,
                    PhysicsLayers.PLAYER_PROXIMITY_MASK);

                // Skip if obstructed by any collider (except itself)
                if (hasHit && hit.collider != COLLIDER_BUFFER[i])
                    continue;

                // New closest unobstructed entity can be set
                proximityResultForSceneEntities.Set(sceneEntityInfo, COLLIDER_BUFFER[i], distanceToPlayer);
                highestPriority = priority;
            }

            return;

            bool IsPlayerInValidScene() =>
                scenesCache.TryGetByParcel(playerControllerCenterPosition.ToParcel(), out ISceneFacade currentScene)
                && currentScene is { IsEmpty: false, SceneData: { SceneLoadingConcluded: true } };
        }

        private bool HasProximityEvent(in PBPointerEvents pointerEvents)
        {
            for (int i = 0; i < pointerEvents.PointerEvents.Count; i++)
                if (pointerEvents.PointerEvents[i].InteractionType == InteractionType.Proximity)
                    return true;

            return false;
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

        /// <summary>
        /// Computes the minimum <c>MaxPlayerDistance</c> and maximum <c>Priority</c>
        /// across all entries in the given <see cref="PBPointerEvents"/>.
        /// </summary>
        /// <param name="pointerEvents">
        /// The pointer events collection to evaluate.
        /// </param>
        /// <param name="maxPlayerDistance">
        /// Outputs the smallest <c>MaxPlayerDistance</c> found, or <see cref="float.MaxValue"/>
        /// if the collection is empty.
        /// </param>
        /// <param name="highestPriority">
        /// Outputs the greatest <c>Priority</c> found, or <c>0</c> if the collection is empty.
        /// </param>
        private void GetMaxDistanceAndHighestPriority(
            PBPointerEvents pointerEvents,
            out float maxPlayerDistance,
            out uint highestPriority
        )
        {
            var events = pointerEvents.PointerEvents;
            int count = events.Count;

            maxPlayerDistance = float.MaxValue;
            highestPriority = 0;

            for (int i = 0; i < count; i++)
            {
                var info = events[i].EventInfo;

                float maxDistance = info.MaxPlayerDistance;
                if (maxDistance < maxPlayerDistance)
                    maxPlayerDistance = maxDistance;

                uint priority = info.Priority;
                if (priority > highestPriority)
                    highestPriority = priority;
            }
        }
    }
}
