﻿using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.ECSComponents;
using ECS.Abstract;
using ECS.Groups;
using ECS.Unity.GLTFContainer.Components;
using SceneRunner.Scene;
using System.Collections.Generic;
using UnityEngine.Pool;

namespace DCL.SceneReadiness
{
    [UpdateInGroup(typeof(SyncedPostRenderingSystemGroup))]
    public partial class GatherGltfAssetsSystem : BaseUnityLoopSystem
    {
        private const int FRAMES_COUNT = 5;

        private readonly ISceneReadinessReportQueue readinessReportQueue;
        private readonly ISceneData sceneData;

        private IReadOnlyList<SceneReadinessReport>? reports;

        private HashSet<EntityReference>? entitiesUnderObservation;

        private int framesLeft = FRAMES_COUNT;
        private bool concluded;

        internal GatherGltfAssetsSystem(World world, ISceneReadinessReportQueue readinessReportQueue, ISceneData sceneData) : base(world)
        {
            this.readinessReportQueue = readinessReportQueue;
            this.sceneData = sceneData;
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
                // Gather entities
                GatherEntitiesQuery(World);

                framesLeft--;
            }
            else if (!concluded)
            {
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
                    if (gltfContainerComponent.State.Value == LoadingState.Loading)
                    {
                        concluded = false;

                        // no reason to iterate further
                        break;
                    }

                    // remove entity from list - it's loaded, we don't need to check it anymore
                    toDelete.Add(entityRef);
                }

                entitiesUnderObservation.ExceptWith(toDelete);
                ListPool<EntityReference>.Release(toDelete);

                if (concluded)
                {
                    for (var i = 0; i < reports.Count; i++)
                        reports[i].CompletionSource.TrySetResult();
                }
            }
        }

        [Query]
        [All(typeof(GltfContainerComponent))]
        private void GatherEntities(in Entity entity)
        {
            EntityReference entityRef = World.Reference(entity);
            entitiesUnderObservation!.Add(entityRef);
        }
    }
}
