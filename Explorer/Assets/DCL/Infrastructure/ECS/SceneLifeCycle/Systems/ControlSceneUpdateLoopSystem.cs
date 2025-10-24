using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using ECS.Prioritization;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.Reporting;
using ECS.SceneLifeCycle.SceneDefinition;
using SceneRunner.Scene;
using System;
using System.Threading;
using ScenePromise = ECS.StreamableLoading.Common.AssetPromise<SceneRunner.Scene.ISceneFacade, ECS.SceneLifeCycle.Components.GetSceneFacadeIntention>;

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
        private readonly ISceneReadinessReportQueue sceneReadinessReportQueue;

        private readonly IScenesCache scenesCache;

        internal ControlSceneUpdateLoopSystem(World world,
            IRealmPartitionSettings realmPartitionSettings,
            CancellationToken destroyCancellationToken,
            IScenesCache scenesCache,
            ISceneReadinessReportQueue sceneReadinessReportQueue) : base(world)
        {
            this.realmPartitionSettings = realmPartitionSettings;
            this.destroyCancellationToken = destroyCancellationToken;
            this.scenesCache = scenesCache;
            this.sceneReadinessReportQueue = sceneReadinessReportQueue;
        }

        protected override void Update(float t)
        {
            ChangeSceneFPSQuery(World);
            HandleNotCreatedScenesQuery(World);
            HandleSmartWearableScenesQuery(World);
        }

        [Query]
        [None(typeof(DeleteEntityIntention), typeof(ISceneFacade). typeof(BannedSceneComponent))]
        private void HandleNotCreatedScenes(in Entity entity, ref ScenePromise promise, in PartitionComponent partition, in SceneDefinitionComponent definitionComponent)
        {
            // Gracefully consume with the possibility of repetitions (in case the scene loading has failed)
            if (promise.IsConsumed)
            {
                // In case there is a failed promise (and it was not re-started) notify the readiness queue accordingly
                if (promise.TryGetResult(World, out var consumedResult) && !consumedResult.Succeeded)
                    SceneUtils.ReportException(consumedResult.Exception!, definitionComponent.Parcels, sceneReadinessReportQueue);

                return;
            }

            if (!promise.TryConsume(World, out var result) || !result.Succeeded) return;

            ISceneFacade scene = result.Asset!;
            StartScene(definitionComponent, partition, scene);

            World.Add(entity, scene);
        }

        [Query]
        [All(typeof(SmartWearableId))]
        [None(typeof(DeleteEntityIntention), typeof(SmartWearableSceneStarted))]
        private void HandleSmartWearableScenes(Entity entity, in SceneDefinitionComponent definitionComponent, in PartitionComponent partition, in ISceneFacade scene)
        {
            World.Add(entity, new SmartWearableSceneStarted());

            StartScene(definitionComponent, partition, scene);
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void ChangeSceneFPS(ref ISceneFacade sceneFacade, in PartitionComponent partition)
        {
            if (!partition.IsDirty) return;

            sceneFacade.SetTargetFPS(realmPartitionSettings.GetSceneUpdateFrequency(in partition));
        }

        private void StartScene(SceneDefinitionComponent definitionComponent, PartitionComponent partition, ISceneFacade scene)
        {
            int fps = realmPartitionSettings.GetSceneUpdateFrequency(partition);
            RunOnThreadPoolAsync().Forget();

            // So we know the scene has started
            if (definitionComponent.IsPortableExperience)
                scenesCache.AddPortableExperienceScene(scene, definitionComponent.IpfsPath.EntityId);
            else
                scenesCache.Add(scene, definitionComponent.Parcels);

            ReportHub.LogProductionInfo($"Scene '{definitionComponent.Definition.GetLogSceneName()}' started");

            return;

            async UniTaskVoid RunOnThreadPoolAsync()
            {
                try
                {
                    await UniTask.SwitchToThreadPool();
                    if (destroyCancellationToken.IsCancellationRequested) return;

                    // Provide basic Thread Pool synchronization context
                    SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());

                    // FPS is set by another system
                    await scene.StartUpdateLoopAsync(fps, destroyCancellationToken);
                }
                catch (Exception e)
                {
                    ReportHub.LogException(e, GetReportData());
                }
            }
        }
    }
}
