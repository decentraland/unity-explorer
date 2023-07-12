using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using SceneRunner.Scene;
using System.Threading;

namespace ECS.SceneLifeCycle.Systems
{
    /// <summary>
    ///     Starts scenes that are already loaded
    /// </summary>
    [UpdateInGroup(typeof(RealmGroup))]
    [UpdateAfter(typeof(ResolveSceneStateByRadiusSystem))]
    [UpdateAfter(typeof(ResolveStaticPointersSystem))]
    public partial class StartSceneSystem : BaseUnityLoopSystem
    {
        private readonly CancellationToken destroyCancellationToken;

        internal StartSceneSystem(World world, CancellationToken destroyCancellationToken) : base(world)
        {
            this.destroyCancellationToken = destroyCancellationToken;
        }

        protected override void Update(float t)
        {
            StartSceneQuery(World);
        }

        [Query]
        [None(typeof(DeleteEntityIntention), typeof(ISceneFacade))]
        private void StartScene(in Entity entity, ref AssetPromise<ISceneFacade, GetSceneFacadeIntention> promise)
        {
            // Gracefully consume with the possibility of repetitions (in case the scene loading has failed)
            if (promise.IsConsumed) return;

            if (promise.TryConsume(World, out StreamableLoadingResult<ISceneFacade> result) && result.Succeeded)
            {
                ISceneFacade scene = result.Asset;

                async UniTaskVoid RunOnThreadPool()
                {
                    await UniTask.SwitchToThreadPool();

                    // Provide basic Thread Pool synchronization context
                    SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());
                    await scene.StartUpdateLoop(30, destroyCancellationToken);
                }

                RunOnThreadPool().Forget();

                // So we know the scene has started
                World.Add(entity, scene);
            }
        }
    }
}
