using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Metadata;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.SceneDefinition;
using Realm;
using SceneRunner;
using SceneRunner.Scene;
using UnityEngine;

namespace ECS.SceneLifeCycle.Systems
{
    [UpdateInGroup(typeof(RealmGroup))]
    [UpdateAfter(typeof(ResolveSceneStateByRadiusSystem))]
    [UpdateAfter(typeof(ResolveStaticPointersSystem))]
    public partial class UpdateLODLevelSystem : BaseUnityLoopSystem
    {
        public UpdateLODLevelSystem(World world) : base(world)
        {
        }

        protected override void Update(float t)
        {
            UpdateLODLevelQuery(World);
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void UpdateLODLevel(ref VisualSceneState visualSceneState, ref SceneLOD sceneLOD,
            ref PartitionComponent partition, ref SceneDefinitionComponent sceneDefinitionComponent)
        {
            if (sceneDefinitionComponent.IsEmpty) return; //We dont update LODs of empty scenes
            if (!visualSceneState.currentVisualSceneState.Equals(VisualSceneStateEnum.SHOWING_LOD)) return;
            if (!visualSceneState.isDirty) return;

            sceneLOD.UpdateLOD(partition.Bucket);

            visualSceneState.isDirty = false;
        }
    }
}