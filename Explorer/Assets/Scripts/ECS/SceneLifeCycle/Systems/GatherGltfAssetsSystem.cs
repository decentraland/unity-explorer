using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.AsyncLoadReporting;
using DCL.ECSComponents;
using ECS.Abstract;
using ECS.Groups;
using ECS.SceneLifeCycle.Reporting;
using ECS.Unity.GLTFContainer.Components;
using SceneRunner.Scene;
using System.Collections.Generic;
using UnityEngine.Pool;

namespace ECS.SceneLifeCycle.Systems
{
    [UpdateInGroup(typeof(SyncedPostRenderingSystemGroup))]
    public partial class GatherGltfAssetsSystem : BaseUnityLoopSystem
    {
        private const int FRAMES_COUNT = 60;

        private readonly ISceneReadinessReportQueue readinessReportQueue;
        private readonly ISceneData sceneData;

        private PooledLoadReportList? reports;

        private HashSet<EntityReference>? entitiesUnderObservation;

        private int framesLeft = FRAMES_COUNT;
        private bool concluded;
        private int assetsResolved;
        private int totalAssetsToResolve = -1;

        private readonly EntityEventBuffer<GltfContainerComponent> eventsBuffer;
        private readonly EntityEventBuffer<GltfContainerComponent>.ForEachDelegate forEachEvent;

        internal GatherGltfAssetsSystem(World world, ISceneReadinessReportQueue readinessReportQueue, ISceneData sceneData, EntityEventBuffer<GltfContainerComponent> eventsBuffer) : base(world)
        {
            this.readinessReportQueue = readinessReportQueue;
            this.sceneData = sceneData;
            this.eventsBuffer = eventsBuffer;

            forEachEvent = GatherEntities;
        }

        public override void Initialize()
        {
            entitiesUnderObservation = HashSetPool<EntityReference>.Get();
        }

        public override void Dispose()
        {
            HashSetPool<EntityReference>.Release(entitiesUnderObservation);
            entitiesUnderObservation = null;
        }

        protected override void Update(float t)
        {
            if (framesLeft > 0)
            {
                eventsBuffer.ForEach(forEachEvent);
                framesLeft--;
            }
            else if (!concluded)
            {
                if (totalAssetsToResolve == -1)
                    totalAssetsToResolve = entitiesUnderObservation!.Count;

                if (reports == null && !readinessReportQueue.TryDequeue(sceneData.Parcels, out reports))
                {
                    // if there is no report to dequeue, nothing to do
                    concluded = true;
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
                    {
                        concluded = false;

                        // no reason to iterate further
                        break;
                    }

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

                if (concluded)
                {
                    for (var i = 0; i < reports.Value.Count; i++)
                        reports.Value[i].SetProgress(1f);

                    reports.Value.Dispose();
                    reports = null;
                }
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
