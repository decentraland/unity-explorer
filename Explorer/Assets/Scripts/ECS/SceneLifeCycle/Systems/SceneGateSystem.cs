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
        private int currentLoadedScenes;

        public SceneGateSystem(World world) : base(world)
        {
            scenesToLoad = 4;
        }

        protected override void Update(float t)
        {
            AddSceneGateQuery(World);
            
            scenePartitions.Sort(static (a, b) => a.partitionComponent.RawSqrDistance.CompareTo(b.partitionComponent.RawSqrDistance));

            for (int index = 0; index < scenePartitions.Count && index < scenesToLoad; index++)
            {
                var sceneDataA = scenePartitions[index];

                if (sceneDataA.partitionComponent.RawSqrDistance is >= 102400 or < 0) continue;

                
                UnityEngine.Debug.Log("ABOUT TO ANALYZE " + sceneDataA.id.Definition.metadata.scene.DecodedBase);

                if (!SceneHasLoaded(sceneDataA.entity) && currentLoadedScenes < scenesToLoad)
                {
                    UnityEngine.Debug.Log("JUANI A SCENE WILL LOAD " + sceneDataA.id.Definition.metadata.scene.DecodedBase);
                    sceneDataA.SceneGateComponent.canLoad = true;
                    currentLoadedScenes++;
                }else if (!SceneHasLoaded(sceneDataA.entity) && currentLoadedScenes >= scenesToLoad)
                {
                    UnityEngine.Debug.Log("HERE IS WHERE THE UNLOAD SHOULD BE TRIGGERED " + sceneDataA.id.Definition.metadata.scene.DecodedBase);
                    //We are wanting to load more scenes than we are memory allowed. What to do?
                    //1. Wait for a scene to unload 
                    //2. Use distances+1 to unload scenes, as we normally do
                }
            }
        }
        
        private bool SceneHasLoaded(in Entity entity)
        {
            return World.Has<ISceneFacade>(entity) || World.Has<AssetPromise<ISceneFacade, GetSceneFacadeIntention>>(entity);
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