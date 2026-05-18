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

        private SceneByteProgressTracker? progressTracker;

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
            progressTracker = new SceneByteProgressTracker();
            startTime = Time.time;
        }

        protected override void OnDispose()
        {
            if (entitiesUnderObservation != null)
            {
                HashSetPool<Entity>.Release(entitiesUnderObservation);
                entitiesUnderObservation = null;
            }

            progressTracker?.Dispose();
            progressTracker = null;

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

                foreach (Entity entityRef in entitiesUnderObservation!)
                {
                    if (!World.IsAlive(entityRef)
                        || !World.TryGet(entityRef, out GltfContainerComponent gltfContainerComponent))
                    {
                        progressTracker!.CreditDeath(entityRef);
                        toDelete.Add(entityRef);
                        continue;
                    }

                    (long contentLength, float entityProgress) = ReadLoadingState(in gltfContainerComponent);
                    progressTracker!.RegisterIfNew(entityRef, contentLength);

                    if (gltfContainerComponent.State == LoadingState.Loading)
                    {
                        concluded = false;

                        if (contentLength > 0)
                            inProgressWeightedBytes += (long)(entityProgress * contentLength);
                    }
                    else
                    {
                        progressTracker.CreditFinish(entityRef, contentLength);
                        toDelete.Add(entityRef);
                    }
                }

                assetsResolved += toDelete.Count;
                float progress = progressTracker!.ComputeAndClamp(inProgressWeightedBytes, totalAssetsToResolve, assetsResolved);

                for (var i = 0; i < reports!.Value.Count; i++)
                {
                    AsyncLoadProcessReport report = reports.Value[i];
                    report.SetProgress(progress);
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

        private (long contentLength, float progress) ReadLoadingState(in GltfContainerComponent gltfContainerComponent)
        {
            Entity promiseEntity = gltfContainerComponent.Promise.Entity;

            if (!World.IsAlive(promiseEntity)
                || !World.TryGet(promiseEntity, out StreamableLoadingState loadingState))
                return (0, 0f);

            return (loadingState.ContentLength, loadingState.Progress);
        }

        private void GatherEntities(Entity entity, GltfContainerComponent component)
        {
            // No matter to which state component has changed
            entitiesUnderObservation!.Add(entity);
        }
    }
}
