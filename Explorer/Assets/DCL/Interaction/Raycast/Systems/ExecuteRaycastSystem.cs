using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CRDT;
using CrdtEcsBridge.Components;
using CrdtEcsBridge.Components.ResetExtensions;
using CrdtEcsBridge.ECSToCRDTWriter;
using CrdtEcsBridge.Physics;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Interaction.Raycast.Components;
using DCL.Interaction.Utility;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Optimization.Pools;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using ECS.Prioritization;
using ECS.Prioritization.Components;
using ECS.Unity.Transforms.Components;
using SceneRunner.Scene;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using Utility;
using RaycastHit = UnityEngine.RaycastHit;

namespace DCL.Interaction.Raycast.Systems
{
    [UpdateInGroup(typeof(RaycastGroup))]
    [UpdateAfter(typeof(InitializeRaycastSystem))]
    public partial class ExecuteRaycastSystem : BaseUnityLoopSystem
    {
        private static readonly RaycastHit[] SHARED_RAYCAST_HIT_ARRAY = new RaycastHit[10];

        private readonly IReleasablePerformanceBudget budget;
        private readonly IEntityCollidersSceneCache collidersSceneCache;
        private readonly IECSToCRDTWriter ecsToCRDTWriter;
        private readonly IReadOnlyDictionary<CRDTEntity, Entity> entitiesMap;
        private readonly byte raycastBucketThreshold;
        private readonly IComponentPool<PBRaycastResult> raycastComponentPool;
        private readonly IComponentPool<ECSComponents.RaycastHit> raycastHitPool;
        private readonly ISceneStateProvider sceneStateProvider;

        private readonly ISceneData sceneData;

        private List<OrderedData> orderedData;
        private List<OrderedData> specialEntitiesData;

        internal ExecuteRaycastSystem(World world,
            ISceneData sceneData,
            IReleasablePerformanceBudget budget,
            byte raycastBucketThreshold,
            IComponentPool<ECSComponents.RaycastHit> raycastHitPool,
            IComponentPool<PBRaycastResult> raycastComponentPool,
            IEntityCollidersSceneCache collidersSceneCache,
            IReadOnlyDictionary<CRDTEntity, Entity> entitiesMap,
            IECSToCRDTWriter ecsToCRDTWriter,
            ISceneStateProvider sceneStateProvider) : base(world)
        {
            this.sceneData = sceneData;
            this.budget = budget;
            this.raycastBucketThreshold = raycastBucketThreshold;
            this.raycastHitPool = raycastHitPool;
            this.raycastComponentPool = raycastComponentPool;
            this.collidersSceneCache = collidersSceneCache;
            this.entitiesMap = entitiesMap;
            this.ecsToCRDTWriter = ecsToCRDTWriter;
            this.sceneStateProvider = sceneStateProvider;
        }

        public override void Initialize()
        {
            orderedData = ListPool<OrderedData>.Get();
            specialEntitiesData = ListPool<OrderedData>.Get();
        }

        public override void Dispose()
        {
            ListPool<OrderedData>.Release(orderedData);
            ListPool<OrderedData>.Release(specialEntitiesData);
        }

        protected override void Update(float t)
        {
            BudgetAndExecute(sceneData.Geometry.BaseParcelPosition);
        }

        /// <summary>
        ///     Executes raycast if there is enough budget available.
        /// </summary>
        private void BudgetAndExecute(Vector3 scenePosition)
        {
            // Process only not executed raycasts which bucket is not farther than the max allowed distance
            orderedData.Clear();

            GatherSpecialEntitiesRaycastIntentsQuery(World);
            GatherRaycastIntentsQuery(World);

            // Sort raycasts by distance to the scene root
            orderedData.Sort(static (p1, p2) => DistanceBasedComparer.INSTANCE.Compare(p1.Partition, p2.Partition));

            // Execute raycasts while budget is available, starting for special entities raycasts
            for (var i = 0; i < specialEntitiesData.Count; i++)
            {
                OrderedData data = specialEntitiesData[i];

                if (budget.TrySpendBudget())
                    Raycast(scenePosition, data.CRDTEntity, ref data.Component.Value, data.SDKComponent, in data.TransformComponent);
                else break;
            }

            for (var i = 0; i < orderedData.Count; i++)
            {
                OrderedData data = orderedData[i];

                if (budget.TrySpendBudget())
                    Raycast(scenePosition, data.CRDTEntity, ref data.Component.Value, data.SDKComponent, in data.TransformComponent);
                else break;
            }
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void GatherRaycastIntents(ref CRDTEntity crdtEntity, ref PartitionComponent partitionComponent,
            ref PBRaycast raycast, ref RaycastComponent raycastComponent, ref TransformComponent transformComponent)
        {
            if (raycastComponent.Executed) return;
            if (partitionComponent.Bucket > raycastBucketThreshold) return;

            // Filter out invalid type
            if (raycast.QueryType == RaycastQueryType.RqtNone) return;

            orderedData.Add(new OrderedData
            {
                Partition = partitionComponent,
                Component = new ManagedTypePointer<RaycastComponent>(ref raycastComponent),
                SDKComponent = raycast,
                TransformComponent = transformComponent,
                CRDTEntity = crdtEntity,
            });
        }


        [Query]
        [None(typeof(DeleteEntityIntention), typeof(PartitionComponent))]
        private void GatherSpecialEntitiesRaycastIntents(ref CRDTEntity crdtEntity,
            ref PBRaycast raycast, ref RaycastComponent raycastComponent, ref TransformComponent transformComponent)
        {
            if (raycastComponent.Executed) return;

            // Filter out invalid type
            if (raycast.QueryType == RaycastQueryType.RqtNone) return;

            specialEntitiesData.Add(new OrderedData
            {
                Partition = null,
                Component = new ManagedTypePointer<RaycastComponent>(ref raycastComponent),
                SDKComponent = raycast,
                TransformComponent = transformComponent,
                CRDTEntity = crdtEntity,
            });
        }

        private void Raycast(Vector3 scenePos, CRDTEntity crdtEntity, ref RaycastComponent raycastComponent, PBRaycast sdkComponent, in TransformComponent transformComponent)
        {
            if (!sdkComponent.TryCreateRay(World, entitiesMap, scenePos, in transformComponent, out Ray ray))
            {
                ReportHub.LogWarning(GetReportCategory(), "Raycast error: Raycast data is malformed.");
                return;
            }

            ColliderLayer sdkCollisionMask = sdkComponent.GetCollisionMask();
            int collisionMask = PhysicsLayers.CreateUnityLayerMaskFromSDKMask(sdkCollisionMask);

            // It will be released by the writer
            PBRaycastResult result = raycastComponentPool.Get();

            result.Timestamp = sdkComponent.Timestamp;
            result.Direction.Set(ray.direction);
            result.GlobalOrigin.Set(ray.origin);
            result.TickNumber = sceneStateProvider.TickNumber;

            Array.Clear(SHARED_RAYCAST_HIT_ARRAY, 0, SHARED_RAYCAST_HIT_ARRAY.Length);

            // The range of Unity Layers is narrower than the range of SDK Layers
            // so we need to raycast against all (even if the query type is hit first) and then filter our each individual raycast hit
            int hitsCount = Physics.RaycastNonAlloc(ray, SHARED_RAYCAST_HIT_ARRAY, sdkComponent.MaxDistance, collisionMask);

            switch (sdkComponent.QueryType)
            {
                case RaycastQueryType.RqtHitFirst:
                    SetClosestQualifiedHit(result, SHARED_RAYCAST_HIT_ARRAY.AsSpan(0, hitsCount), sdkCollisionMask, scenePos, transformComponent.Cached.WorldPosition, ray.direction);
                    break;
                case RaycastQueryType.RqtQueryAll:
                    SetAllQualifiedHits(result, SHARED_RAYCAST_HIT_ARRAY.AsSpan(0, hitsCount), sdkCollisionMask, scenePos, transformComponent.Cached.WorldPosition, ray.direction);
                    break;
            }

            raycastComponent.Executed = !sdkComponent.Continuous;

            ecsToCRDTWriter.PutMessage(result, crdtEntity);
        }

        private void SetClosestQualifiedHit(PBRaycastResult raycastResult, Span<RaycastHit> hits, ColliderLayer collisionMask, Vector3 scenePos, Vector3 globalOrigin,
            Vector3 rayDirection)
        {
            RaycastHit? closestQualifiedHit = null;
            CRDTEntity foundEntity = -1;

            for (var i = 0; i < hits.Length; i++)
            {
                RaycastHit hit = hits[i];

                Collider collider = hit.collider;

                if (!TryGetQualifiedEntity(collider, collisionMask, out foundEntity)) continue;

                if (closestQualifiedHit == null || hit.distance < closestQualifiedHit.Value.distance)
                    closestQualifiedHit = hit;
            }

            if (closestQualifiedHit != null)
            {
                ECSComponents.RaycastHit sdkHit = raycastHitPool.Get();
                RaycastHit qualifiedHit = closestQualifiedHit.Value;
                sdkHit.FillSDKRaycastHit(scenePos, qualifiedHit, qualifiedHit.collider.name, foundEntity, globalOrigin, rayDirection);
                raycastResult.Hits.Add(sdkHit);
            }
        }

        private void SetAllQualifiedHits(PBRaycastResult raycastResult, Span<RaycastHit> hits, ColliderLayer collisionMask, Vector3 scenePos, Vector3 globalOrigin,
            Vector3 rayDirection)
        {
            for (var i = 0; i < hits.Length; i++)
            {
                RaycastHit hit = hits[i];

                Collider collider = hit.collider;

                if (!TryGetQualifiedEntity(collider, collisionMask, out CRDTEntity foundEntity)) continue;

                ECSComponents.RaycastHit sdkHit = raycastHitPool.Get();
                sdkHit.FillSDKRaycastHit(scenePos, hit, hit.collider.name, foundEntity, globalOrigin, rayDirection);
                raycastResult.Hits.Add(sdkHit);
            }
        }

        private bool TryGetQualifiedEntity(Collider collider, ColliderLayer collisionMask, out CRDTEntity foundEntity)
        {
            foundEntity = -1;

            // Player is always qualified
            if (RaycastUtils.IsPlayer(collider))
            {
                foundEntity = SpecialEntitiesID.PLAYER_ENTITY;
                return true;
            }

            // If the collider is not a character, we need to check if it's in the collision mask
            if (!collidersSceneCache.TryGetEntity(collider, out ColliderEntityInfo entityInfo))

                // Can't do anything without collider info, just skip
                return false;

            bool isQualified = RaycastUtils.IsSDKLayerInCollisionMask(entityInfo.SDKLayer, collisionMask);

            if (!isQualified) return false;

            foundEntity = entityInfo.SDKEntity;
            return true;
        }

        private struct OrderedData
        {
            public PartitionComponent? Partition;
            public ManagedTypePointer<RaycastComponent> Component;
            public TransformComponent TransformComponent;
            public CRDTEntity CRDTEntity;
            public PBRaycast SDKComponent;
        }
    }
}
