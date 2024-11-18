using Arch.Core;
using Arch.SystemGroups;
using DCL.AsyncLoadReporting;
using DCL.ECSComponents;
using ECS.Abstract;
using ECS.Groups;
using ECS.SceneLifeCycle.Reporting;
using ECS.Unity.GLTFContainer.Components;
using SceneRunner.Scene;
using System.Collections.Generic;
using DCL.Optimization.PerformanceBudgeting;
using DCL.UserInAppInitializationFlow;
using ECS.Unity.Transforms.Components;
using UnityEngine;
using UnityEngine.Pool;

namespace ECS.SceneLifeCycle.Systems
{
    [UpdateInGroup(typeof(SyncedPreRenderingSystemGroup))]
    public partial class GatherGltfAssetsSystem : BaseUnityLoopSystem
    {
        private const int FRAMES_COUNT = 90;

        private readonly ISceneReadinessReportQueue readinessReportQueue;
        private readonly ISceneData sceneData;

        private PooledLoadReportList? reports;

        private HashSet<EntityReference>? entitiesUnderObservation;

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
            entitiesUnderObservation = HashSetPool<EntityReference>.Get();
            startTime = Time.time;
        }

        public override void Dispose()
        {
            if (entitiesUnderObservation != null)
            {
                HashSetPool<EntityReference>.Release(entitiesUnderObservation);
                entitiesUnderObservation = null;
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

                List<EntityReference> toDelete = ListPool<EntityReference>.Get();

                // iterate over entities

                foreach (EntityReference entityRef in entitiesUnderObservation!)
                {
                    // if entity has died
                    // or entity no longer contains GltfContainerComponent
                    // continue
                    if (!entityRef.IsAlive(World)
                        || !World.TryGet(entityRef, out GltfContainerComponent gltfContainerComponent))
                    {
                        toDelete.Add(entityRef);
                        continue;
                    }

                    // if Gltf Container Component has finished loading at least once (it can be reconfigured, we don't care)
                    if (gltfContainerComponent.State == LoadingState.Loading)
                        // if at least one entity is still loading, we are not done.
                        concluded = false;
                    else
                        // remove entity from list - it's loaded, we don't need to check it anymore
                        toDelete.Add(entityRef);
                }

                assetsResolved += toDelete.Count;
                float progress = totalAssetsToResolve != 0 ? assetsResolved / (float)totalAssetsToResolve : 1;

                for (var i = 0; i < reports!.Value.Count; i++)
                {
                    AsyncLoadProcessReport report = reports.Value[i];
                    report.SetProgress(progress);
                }

                entitiesUnderObservation.ExceptWith(toDelete);
                ListPool<EntityReference>.Release(toDelete);

                // If is still not concluded apply certain timeout to be in sync with `WaitForSceneReadiness`
                if (Time.time - startTime > WaitForSceneReadiness.TIMEOUT.TotalSeconds)
                    concluded = true;

                // Memory is full. Assets may be on deadlock. Show broken state of scene
                if (memoryBudget.GetMemoryUsageStatus() == MemoryUsageStatus.FULL)
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

        private void GatherEntities(Entity entity, GltfContainerComponent component)
        {
            // No matter to which state component has changed
            EntityReference entityRef = World.Reference(entity);
            entitiesUnderObservation!.Add(entityRef);
        }
    }
}
