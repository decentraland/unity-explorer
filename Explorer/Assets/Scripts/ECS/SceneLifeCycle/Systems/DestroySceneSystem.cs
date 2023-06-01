using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using ECS.Abstract;
using ECS.SceneLifeCycle.Components;

namespace ECS.SceneLifeCycle.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(SceneLoadingSystem))]
    public partial class DestroySceneSystem : BaseUnityLoopSystem
    {
        public DestroySceneSystem(World world) : base(world) { }

        protected override void Update(float t)
        {
            DestroySceneQuery(World);
        }

        [Query]
        [All(typeof(DeleteSceneIntention))]
        private void DestroyScene(in Entity entity)
        {
            if (World.Has<LiveSceneComponent>(entity))
            {
                var liveScene = World.Get<LiveSceneComponent>(entity);

                liveScene.CancellationToken.Cancel();
                liveScene.SceneFacade.DisposeAsync();
                World.Destroy(entity);
            }
            else if (World.Has<SceneLoadingComponent>(entity))
            {
                var sceneLoading = World.Get<SceneLoadingComponent>(entity);

                sceneLoading.CancellationTokenSource.Cancel();
                sceneLoading.State = SceneLoadingState.Canceled;
                World.Destroy(entity);
            }
        }
    }
}
