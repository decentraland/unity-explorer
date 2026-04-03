using Arch.Core;
using Arch.SystemGroups;
using DCL.ECSComponents;
using DCL.Optimization.PerformanceBudgeting;
using DCL.RealmNavigation;
using DCL.Utilities;
using ECS.Abstract;
using ECS.Groups;
using ECS.SceneLifeCycle.Reporting;
using ECS.StreamableLoading.Common.Components;
using ECS.Unity.GLTFContainer.Components;
using ECS.Unity.Transforms.Components;
using SceneRunner.Scene;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace ECS.SceneLifeCycle.Systems
{
    [UpdateInGroup(typeof(SyncedPreRenderingSystemGroup))]
    public partial class GatherGltfAssetsSystem : BaseUnityLoopSystem
    {
        private static readonly TimeSpan TIMEOUT = TimeSpan.FromSeconds(60);

        private const int FRAMES_COUNT = 10;

        private readonly ISceneReadinessReportQueue readinessReportQueue;
        private readonly ISceneData sceneData;

        private PooledLoadReportList? reports;

        private HashSet<Entity>? entitiesUnderObservation;

        private bool concluded;
        private int assetsResolved;
        private int totalAssetsToResolve = -1;
        private float startTime;

        private readonly EntityEventBuffer<GltfContainerComponent> eventsBuffer;
        private readonly EntityEventBuffer<GltfContainerComponent>.ForEachDelegate forEachEvent;
        private readonly ISceneStateProvider sceneStateProvider;
        private readonly MemoryBudget memoryBudget;
        private readonly ILoadingStatus loadingStatus;
        private readonly Entity sceneContainerEntity;

        // Byte-weighted progress tracking
        private Dictionary<Entity, long>? contentLengthCache;
        private long completedBytes;
        private long totalBytesExpected;
        private int entitiesWithKnownSize;
        private float maxReportedProgress;

        internal GatherGltfAssetsSystem(World world, ISceneReadinessReportQueue readinessReportQueue,
            ISceneData sceneData, EntityEventBuffer<GltfContainerComponent> eventsBuffer,
            ISceneStateProvider sceneStateProvider, MemoryBudget memoryBudget,
            ILoadingStatus loadingStatus,
            Entity sceneContainerEntity) : base(world)
        {
            this.readinessReportQueue = readinessReportQueue;
            this.sceneData = sceneData;
            this.eventsBuffer = eventsBuffer;
            this.sceneStateProvider = sceneStateProvider;
            this.memoryBudget = memoryBudget;
            this.loadingStatus = loadingStatus;
            this.sceneContainerEntity = sceneContainerEntity;

            forEachEvent = GatherEntities;
        }

        public override void Initialize()
        {
            entitiesUnderObservation = HashSetPool<Entity>.Get();
            contentLengthCache = DictionaryPool<Entity, long>.Get();
            startTime = Time.time;
        }

        protected override void OnDispose()
        {
            if (entitiesUnderObservation != null)
            {
                HashSetPool<Entity>.Release(entitiesUnderObservation);
                entitiesUnderObservation = null;
            }

            if (contentLengthCache != null)
            {
                DictionaryPool<Entity, long>.Release(contentLengthCache);
                contentLengthCache = null;
            }

            sceneData.SceneLoadingConcluded = true;
        }

        protected override void Update(float t)
        {
            if (sceneStateProvider.TickNumber < FRAMES_COUNT)
            {
                eventsBuffer.ForEach(forEachEvent);
            }
            else if (!concluded)
            {
                if (totalAssetsToResolve == -1)
                    totalAssetsToResolve = entitiesUnderObservation!.Count;

                if (reports == null && !readinessReportQueue.TryDequeue(sceneData.Parcels, out reports))
                {
                    Conclude();
                    return;
                }

                concluded = true;

                List<Entity> toDelete = ListPool<Entity>.Get();
                long inProgressWeightedBytes = 0;
                float unknownInProgressWeightedProgress = 0f;

                // iterate over entities

                foreach (Entity entityRef in entitiesUnderObservation!)
                {
                    // if entity has died
                    // or entity no longer contains GltfContainerComponent
                    // continue
                    if (!World.IsAlive(entityRef)
                        || !World.TryGet(entityRef, out GltfContainerComponent gltfContainerComponent))
                    {
                        toDelete.Add(entityRef);
                        continue;
                    }

                    // Cache ContentLength from the promise's StreamableLoadingState while it's alive
                    CacheContentLength(entityRef, in gltfContainerComponent);

                    // if Gltf Container Component has finished loading at least once (it can be reconfigured, we don't care)
                    if (gltfContainerComponent.State == LoadingState.Loading)
                    {
                        // if at least one entity is still loading, we are not done.
                        concluded = false;

                        // Accumulate in-progress weighted bytes
                        if (contentLengthCache!.TryGetValue(entityRef, out long cachedLength) && cachedLength > 0)
                            inProgressWeightedBytes += (long)(GetEntityProgress(entityRef, in gltfContainerComponent) * cachedLength);
                        else
                            unknownInProgressWeightedProgress += GetEntityProgress(entityRef, in gltfContainerComponent);
                    }
                    else
                    {
                        // remove entity from list - it's loaded, we don't need to check it anymore
                        toDelete.Add(entityRef);
                    }
                }

                // Accumulate completed bytes from resolved entities
                for (var i = 0; i < toDelete.Count; i++)
                {
                    Entity entity = toDelete[i];

                    if (contentLengthCache!.TryGetValue(entity, out long len))
                    {
                        if (len > 0)
                            completedBytes += len;

                        contentLengthCache.Remove(entity);
                    }
                }

                assetsResolved += toDelete.Count;
                float progress = ComputeProgress(inProgressWeightedBytes, unknownInProgressWeightedProgress);
                maxReportedProgress = Mathf.Max(maxReportedProgress, progress);

                for (var i = 0; i < reports!.Value.Count; i++)
                {
                    AsyncLoadProcessReport report = reports.Value[i];
                    report.SetProgress(maxReportedProgress);
                }

                entitiesUnderObservation.ExceptWith(toDelete);
                ListPool<Entity>.Release(toDelete);

                // it's an internal timeout
                if (Time.time - startTime > TIMEOUT.TotalSeconds)
                    concluded = true;

                // Memory is filling up, we considered it complete to avoid deadlock
                if (!memoryBudget.IsMemoryNormal())
                {
                    for (var i = 0; i < reports!.Value.Count; i++)
                    {
                        var report = reports.Value[i];
                        report.SetProgress(1);
                    }

                    concluded = true;
                }

                if (concluded)
                {
                    for (var i = 0; i < reports!.Value.Count; i++)
                    {
                        AsyncLoadProcessReport report = reports.Value[i];
                        report.SetProgress(1);
                    }

                    reports.Value.Dispose();
                    reports = null;
                    Conclude();
                }
                loadingStatus.UpdateAssetsLoaded(assetsResolved, totalAssetsToResolve);
                sceneData.SceneLoadingConcluded = concluded;
            }

            void Conclude()
            {
                concluded = true;
                sceneData.SceneLoadingConcluded = true;

                World.Get<TransformComponent>(sceneContainerEntity).Transform.position =
                    sceneData.Geometry.BaseParcelPosition;
            }
        }

        private void CacheContentLength(Entity entityRef, in GltfContainerComponent gltfContainerComponent)
        {
            Entity promiseEntity = gltfContainerComponent.Promise.Entity;

            if (!World.IsAlive(promiseEntity)
                || !World.TryGet(promiseEntity, out StreamableLoadingState loadingState))
                return;

            if (loadingState.ContentLength <= 0)
                return;

            if (!contentLengthCache!.ContainsKey(entityRef))
            {
                contentLengthCache[entityRef] = loadingState.ContentLength;
                totalBytesExpected += loadingState.ContentLength;
                entitiesWithKnownSize++;
            }
        }

        private float GetEntityProgress(Entity entityRef, in GltfContainerComponent gltfContainerComponent)
        {
            Entity promiseEntity = gltfContainerComponent.Promise.Entity;

            if (!World.IsAlive(promiseEntity)
                || !World.TryGet(promiseEntity, out StreamableLoadingState loadingState))
                return 0f;

            return loadingState.Progress;
        }

        private float ComputeProgress(long inProgressWeightedBytes, float unknownInProgressWeightedProgress)
        {
            // Fallback: count-based progress when no byte data is available
            if (entitiesWithKnownSize <= 0 || totalBytesExpected <= 0)
                return totalAssetsToResolve != 0 ? assetsResolved / (float)totalAssetsToResolve : 1f;

            // Estimate unknown assets using average size of known assets
            long avgSize = totalBytesExpected / entitiesWithKnownSize;
            int unknownCount = totalAssetsToResolve - entitiesWithKnownSize;
            long effectiveTotal = totalBytesExpected + avgSize * Math.Max(0, unknownCount);

            // Account for completed unknown-size entities using the average estimate
            int completedKnownCount = entitiesWithKnownSize - contentLengthCache!.Count;
            int completedUnknownCount = Math.Max(0, assetsResolved - completedKnownCount);
            long estimatedCompletedUnknownBytes = avgSize * completedUnknownCount;

            // Account for in-progress unknown-size entities using their per-entity progress * average size
            long estimatedUnknownInProgressBytes = (long)(unknownInProgressWeightedProgress * avgSize);

            return effectiveTotal > 0
                ? Mathf.Clamp01((float)(completedBytes + estimatedCompletedUnknownBytes + inProgressWeightedBytes + estimatedUnknownInProgressBytes) / effectiveTotal)
                : 0f;
        }

        private void GatherEntities(Entity entity, GltfContainerComponent component)
        {
            // No matter to which state component has changed
            entitiesUnderObservation!.Add(entity);
        }
    }
}
