using Arch.Core;
using Arch.SystemGroups;
using DCL.ECSComponents;
using DCL.Optimization.PerformanceBudgeting;
using DCL.RealmNavigation;
using DCL.Utilities;
using ECS.Abstract;
using ECS.Groups;
using ECS.SceneLifeCycle.Reporting;
using ECS.StreamableLoading.AssetBundles;
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

        private int FRAMES_COUNT = 90;

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

        private StaticSceneAssetBundle staticSceneAssetBundle;

        internal GatherGltfAssetsSystem(World world, ISceneReadinessReportQueue readinessReportQueue,
            ISceneData sceneData, EntityEventBuffer<GltfContainerComponent> eventsBuffer,
            ISceneStateProvider sceneStateProvider, MemoryBudget memoryBudget,
            ILoadingStatus loadingStatus,
            Entity sceneContainerEntity,
            StaticSceneAssetBundle staticSceneAssetBundle) : base(world)
        {
            this.readinessReportQueue = readinessReportQueue;
            this.sceneData = sceneData;
            this.eventsBuffer = eventsBuffer;
            this.sceneStateProvider = sceneStateProvider;
            this.memoryBudget = memoryBudget;
            this.loadingStatus = loadingStatus;
            this.sceneContainerEntity = sceneContainerEntity;
            this.staticSceneAssetBundle = staticSceneAssetBundle;

            forEachEvent = GatherEntities;
        }

        public override void Initialize()
        {
            entitiesUnderObservation = HashSetPool<Entity>.Get();
            startTime = Time.time;
        }

        protected override void OnDispose()
        {
            if (entitiesUnderObservation != null)
            {
                HashSetPool<Entity>.Release(entitiesUnderObservation);
                entitiesUnderObservation = null;
            }
            sceneData.SceneLoadingConcluded = true;
        }

        protected override void Update(float t)
        {
            bool shouldWait;

            if (staticSceneAssetBundle is { Supported: true })
            {
                // If supported but not initialized, and no entities under observation, wait
                shouldWait = !staticSceneAssetBundle.AssetBundleData.IsInitialized || entitiesUnderObservation.Count == 0;
            }
            else
            {
                // If not supported, run for frame count
                shouldWait = sceneStateProvider.TickNumber < FRAMES_COUNT;
            }

            if (shouldWait)
                eventsBuffer.ForEach(forEachEvent);
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
                    reports.Value.Dispose();
                    reports = null;
                    Conclude();
                }
                loadingStatus.UpdateAssetsLoaded(assetsResolved, totalAssetsToResolve);
                sceneData.SceneLoadingConcluded = concluded;
            }

            void Conclude()
            {
                UnityEngine.Debug.Log($"JUANI COMPLETED AT {sceneStateProvider.TickNumber}");
                concluded = true;
                sceneData.SceneLoadingConcluded = true;

                World.Get<TransformComponent>(sceneContainerEntity).Transform.position =
                    sceneData.Geometry.BaseParcelPosition;
            }
        }

        private void GatherEntities(Entity entity, GltfContainerComponent component)
        {
            // No matter to which state component has changed
            entitiesUnderObservation!.Add(entity);
        }
    }
}
