using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using DCL.LOD.Components;
using DCL.Roads.Components;
using ECS.Abstract;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.IncreasingRadius;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.SceneLifeCycle.Systems;
using ECS.StreamableLoading.Common;
using SceneRunner.Scene;
using System;
using System.Linq;
using UnityEngine;

namespace ECS.SceneLifeCycle.Debug
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(LoadSceneSystem))]
    public partial class AbortSceneLoadingDebugSystem : BaseUnityLoopSystem
    {
        private readonly ElementBinding<SceneAbortKind> abortKind;
        private readonly ElementBinding<Vector2Int> coords;

        public AbortSceneLoadingDebugSystem(World world, DebugWidgetBuilder debugWidgetBuilder) : base(world) { }

        protected override void Update(float t)
        {
            if (abortKind.Value == SceneAbortKind.NONE)
                return;
        }

        [Query]
        [All(typeof(PartitionComponent))]
        [None(typeof(AssetPromise<ISceneFacade, GetSceneFacadeIntention>), typeof(SceneLODInfo), typeof(RoadInfo), typeof(EmptySceneComponent))]
        private void TryAbortLoading(Entity entity, in SceneDefinitionComponent sceneDefinitionComponent, in VisualSceneState visualSceneState)
        {
            if (visualSceneState.CurrentVisualSceneState == VisualSceneStateEnum.SHOWING_SCENE && sceneDefinitionComponent.Parcels.Contains(coords.Value)) { World.Add(AssetPromise<ISceneFacade, GetSceneFacadeIntention>.CreateFinalized()); }
        }
    }
}
