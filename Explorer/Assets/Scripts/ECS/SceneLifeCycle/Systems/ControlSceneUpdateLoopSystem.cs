using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using ECS.Prioritization;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using Realm;
using SceneRunner.Scene;
using System.Threading;

namespace ECS.SceneLifeCycle.Systems
{
    /// <summary>
    ///     Starts the scene or changes fps of its execution
    /// </summary>
    [UpdateInGroup(typeof(RealmGroup))]
    [UpdateAfter(typeof(ResolveSceneStateByRadiusSystem))]
    [UpdateAfter(typeof(ResolveStaticPointersSystem))]
    public partial class ControlSceneUpdateLoopSystem : BaseUnityLoopSystem
    {
        private readonly IRealmPartitionSettings realmPartitionSettings;
        private readonly CancellationToken destroyCancellationToken;

        internal ControlSceneUpdateLoopSystem(World world,
            IRealmPartitionSettings realmPartitionSettings,
            CancellationToken destroyCancellationToken) : base(world)
        {
            this.realmPartitionSettings = realmPartitionSettings;
            this.destroyCancellationToken = destroyCancellationToken;
        }

        protected override void Update(float t)
        {
            ChangeSceneFPSQuery(World);
            StartSceneQuery(World);
        }

        [Query]
        [None(typeof(DeleteEntityIntention), typeof(ISceneFacade))]
        private void StartScene(in Entity entity, ref AssetPromise<ISceneFacade, GetSceneFacadeIntention> promise, ref PartitionComponent partition)
        {
            // Gracefully consume with the possibility of repetitions (in case the scene loading has failed)
            if (promise.IsConsumed) return;

            if (promise.TryConsume(World, out StreamableLoadingResult<ISceneFacade> result) && result.Succeeded)
            {
                ISceneFacade scene = result.Asset;
                int fps = realmPartitionSettings.GetSceneUpdateFrequency(in partition);

                async UniTaskVoid RunOnThreadPool()
                {
                    await UniTask.SwitchToThreadPool();
                    if (destroyCancellationToken.IsCancellationRequested) return;

                    // Provide basic Thread Pool synchronization context
                    SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());

                    // FPS is set by another system
                    await scene.StartUpdateLoop(fps, destroyCancellationToken);
                }

                RunOnThreadPool().Forget();

                // So we know the scene has started
                World.Add(entity, scene);
            }
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void ChangeSceneFPS(ref ISceneFacade sceneFacade, ref PartitionComponent partition)
        {
            if (!partition.IsDirty) return;

            sceneFacade.SetTargetFPS(realmPartitionSettings.GetSceneUpdateFrequency(in partition));
        }
    }
}
