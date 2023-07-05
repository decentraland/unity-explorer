using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using ECS.Abstract;
using ECS.SceneLifeCycle.Components;

namespace ECS.SceneLifeCycle.Systems
{
    [UpdateInGroup(typeof(SceneLifeCycleGroup))]
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
            World.Destroy(entity);
        }
    }
}
