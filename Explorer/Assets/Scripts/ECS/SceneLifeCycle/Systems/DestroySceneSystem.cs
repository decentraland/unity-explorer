using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using ECS.Abstract;
using ECS.SceneLifeCycle.Components;

namespace ECS.SceneLifeCycle.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(StartSceneSystem))]
    public partial class DestroySceneSystem : BaseUnityLoopSystem
    {
        public DestroySceneSystem(World world) : base(world) { }

        protected override void Update(float t)
        {
            DestroyLiveSceneQuery(World);
            DestroyLoadingSceneQuery(World);
        }

        [Query]
        [All(typeof(DeleteSceneIntention))]
        private void DestroyLiveScene(in Entity entity, ref LiveSceneComponent liveSceneComponent)
        {
            liveSceneComponent.CancellationTokenSource.Cancel();
            World.Destroy(entity);
        }

        [Query]
        [All(typeof(DeleteSceneIntention))]
        private void DestroyLoadingScene(in Entity entity, ref SceneLoadingComponent sceneLoadingComponent)
        {
            sceneLoadingComponent.CancellationTokenSource.Cancel();
            World.Destroy(entity);
        }
    }
}
