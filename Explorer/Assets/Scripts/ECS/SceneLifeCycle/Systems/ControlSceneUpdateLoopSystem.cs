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
        [None(typeof(DeleteEntityIntention))]
        private void StartScene(ISceneFacade scene, ref VisualSceneState visualSceneState,
            ref PartitionComponent partition)
        {
            if (!visualSceneState.isDirty) return;

            if (!visualSceneState.currentVisualSceneState.Equals(VisualSceneStateEnum.SHOWING_SCENE)) return;

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

            visualSceneState.isDirty = false;
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void ChangeSceneFPS(ref ISceneFacade sceneFacade, ref SceneDefinitionComponent sceneDefinition,
            ref PartitionComponent partition, ref VisualSceneState visualSceneState)
        {
            if (!partition.IsDirty) return;
            if (sceneDefinition.IsEmpty) return; // Never tweak FPS of empty scenes
            if (!visualSceneState.currentVisualSceneState.Equals(VisualSceneStateEnum.SHOWING_SCENE))
                return; // Never tweak FPS if LODS are present

            sceneFacade.SetTargetFPS(realmPartitionSettings.GetSceneUpdateFrequency(in partition));
        }
    }
}
