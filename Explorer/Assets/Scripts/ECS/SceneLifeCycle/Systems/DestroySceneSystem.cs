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
            DestroyLiveSceneQuery(World);
            DestroyLoadingSceneQuery(World);
        }

        [Query]
        [All(typeof(DeleteSceneIntention), typeof(LiveSceneComponent))]
        private void DestroyLiveScene(in Entity entity)
        {
            var liveScene = World.Get<LiveSceneComponent>(entity);

            liveScene.CancellationToken?.Cancel();
            liveScene.SceneFacade?.DisposeAsync();
            World.Destroy(entity);
        }

        [Query]
        [All(typeof(DeleteSceneIntention), typeof(SceneLoadingComponent))]
        private void DestroyLoadingScene(in Entity entity)
        {
            var sceneLoading = World.Get<SceneLoadingComponent>(entity);

            sceneLoading.CancellationTokenSource.Cancel();
            World.Destroy(entity);
        }
    }
}
