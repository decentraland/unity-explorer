using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using ECS.Prioritization;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.StreamableLoading.Common;
using SceneRunner.Scene;
using System.Threading;

namespace ECS.SceneLifeCycle.Systems
{
    /// <summary>
    ///     Starts the scene or changes fps of its execution
    /// </summary>
    [UpdateInGroup(typeof(RealmGroup))]
    [UpdateAfter(typeof(ResolveStaticPointersSystem))]
    public partial class ControlSceneUpdateLoopSystem : BaseUnityLoopSystem
    {
        private readonly IRealmPartitionSettings realmPartitionSettings;
        private readonly CancellationToken destroyCancellationToken;

        private readonly IScenesCache scenesCache;

        internal ControlSceneUpdateLoopSystem(World world,
            IRealmPartitionSettings realmPartitionSettings,
            CancellationToken destroyCancellationToken,
            IScenesCache scenesCache) : base(world)
        {
            this.realmPartitionSettings = realmPartitionSettings;
            this.destroyCancellationToken = destroyCancellationToken;
            this.scenesCache = scenesCache;
        }

        protected override void Update(float t)
        {
            ChangeSceneFPSQuery(World);
            StartSceneQuery(World);
        }

        [Query]
        [None(typeof(DeleteEntityIntention), typeof(ISceneFacade))]
        private void StartScene(in Entity entity, ref AssetPromise<ISceneFacade, GetSceneFacadeIntention> promise,
            ref PartitionComponent partition)
        {
            // Gracefully consume with the possibility of repetitions (in case the scene loading has failed)
            if (promise.IsConsumed) return;

            if (promise.TryConsume(World, out var result) && result.Succeeded)
            {
                var scene = result.Asset;

                var fps = realmPartitionSettings.GetSceneUpdateFrequency(in partition);

                async UniTaskVoid RunOnThreadPoolAsync()
                {
                    await UniTask.SwitchToThreadPool();
                    if (destroyCancellationToken.IsCancellationRequested) return;

                    // Provide basic Thread Pool synchronization context
                    SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());

                    // FPS is set by another system
                    await scene.StartUpdateLoopAsync(fps, destroyCancellationToken);
                }

                RunOnThreadPoolAsync().Forget();

                // So we know the scene has started
                scenesCache.Add(scene, promise.LoadingIntention.DefinitionComponent.Parcels);
                World.Add(entity, scene);
            }
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void ChangeSceneFPS(ref ISceneFacade sceneFacade, ref SceneDefinitionComponent sceneDefinition,
            ref PartitionComponent partition)
        {
            if (!partition.IsDirty) return;
            if (sceneDefinition.IsEmpty) return; // Never tweak FPS of empty scenes

            sceneFacade.SetTargetFPS(realmPartitionSettings.GetSceneUpdateFrequency(in partition));
        }
    }
}
