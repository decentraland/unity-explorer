using System.Collections.Generic;
using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Metadata;
using DCL.LOD.Components;
using DCL.Roads.Components;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.IncreasingRadius;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.StreamableLoading.Common;
using SceneRunner.Scene;
using UnityEngine;

namespace ECS.SceneLifeCycle.Systems
{
    [UpdateInGroup(typeof(RealmGroup))]
    [UpdateBefore(typeof(ResolveSceneStateByIncreasingRadiusSystem))]
    [UpdateAfter(typeof(ResolveVisualSceneStateSystem))]
    public partial class SceneGateSystem : BaseUnityLoopSystem
    {
        private readonly List<SceneData> scenePartitions = new ();

        private readonly int scenesToLoad;

        public SceneGateSystem(World world) : base(world)
        {
            scenesToLoad = 4;
        }

        protected override void Update(float t)
        {
            AddSceneGateQuery(World);
            scenePartitions.Sort(static (a, b) => a.partitionComponent.RawSqrDistance.CompareTo(b.partitionComponent.RawSqrDistance));

            int currentLoadedScenes = 0;
            for (int index = 0; index < scenePartitions.Count; index++)
            {
                var sceneDataA = scenePartitions[index];

                if (sceneDataA.partitionComponent.RawSqrDistance is >= 102400 or < 0) continue;

                if (currentLoadedScenes < scenesToLoad)
                {
                    sceneDataA.SceneGateComponent.canLoad = true;
                    currentLoadedScenes++;
                }
                else
                {
                    //Means it loaded and requires unload
                    if (World.Has<ISceneFacade>(sceneDataA.entity)
                        || World.Has<AssetPromise<ISceneFacade, GetSceneFacadeIntention>>(sceneDataA.entity))
                    {
                        World.Add(sceneDataA.entity, DeleteEntityIntention.DeferredDeletion);
                        sceneDataA.SceneGateComponent.canLoad = false;
                    }
                }
            }

            if (Input.GetKeyDown(KeyCode.P))
            {
                foreach (var scenePartition in scenePartitions)
                {
                    if (scenePartition.partitionComponent.RawSqrDistance < 0)
                        return;
                    UnityEngine.Debug.Log(Time.frameCount + " " +
                                          scenePartition.id.Definition.id + " " + scenePartition.id.Definition.metadata.scene.DecodedBase + " " + scenePartition.partitionComponent.RawSqrDistance);
                }
            }
        }

        [Query]
        [None(typeof(SceneGateComponent))]
        public void AddSceneGate(in Entity entity, ref SceneDefinitionComponent sceneDefinitionComponent,
            ref PartitionComponent partitionComponent, ref VisualSceneState visualSceneState)
        {
            var sceneGateComponent = new SceneGateComponent();
            scenePartitions.Add(new SceneData(sceneDefinitionComponent, partitionComponent, entity, sceneGateComponent));
            World.Add(entity, sceneGateComponent);
        }

        private class SceneData
        {
            public readonly SceneDefinitionComponent id;
            public readonly PartitionComponent partitionComponent;
            public readonly SceneGateComponent SceneGateComponent;
            public readonly Entity entity;

            public SceneData(SceneDefinitionComponent id, PartitionComponent partitionComponent, Entity entity, SceneGateComponent sceneGateComponent)
            {
                this.id = id;
                this.partitionComponent = partitionComponent;
                this.entity = entity;
                SceneGateComponent = sceneGateComponent;
            }
        }
    }

    public class SceneGateComponent
    {
        public bool canLoad;
    }
}