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
using Utility.Multithreading;

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
        [None(typeof(DeleteEntityIntention), typeof(ISceneFacade), typeof(BannedSceneComponent))]
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
            StartSceneAsync(definitionComponent, partition, scene).Forget();

            World.Add(entity, scene);
        }

        [Query]
        [All(typeof(SmartWearableId))]
        [None(typeof(DeleteEntityIntention), typeof(SmartWearableSceneStarted))]
        private void HandleSmartWearableScenes(Entity entity, in SceneDefinitionComponent definitionComponent, in PartitionComponent partition, in ISceneFacade scene)
        {
            World.Add(entity, new SmartWearableSceneStarted());

            StartSceneAsync(definitionComponent, partition, scene).Forget();
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void ChangeSceneFPS(ref ISceneFacade sceneFacade, in PartitionComponent partition)
        {
            if (!partition.IsDirty) return;

            sceneFacade.SetTargetFPS(realmPartitionSettings.GetSceneUpdateFrequency(in partition));
        }

        private async UniTaskVoid StartSceneAsync(SceneDefinitionComponent definitionComponent, PartitionComponent partition, ISceneFacade scene)
        {
            int fps = realmPartitionSettings.GetSceneUpdateFrequency(partition);

            try
            {
                // 1. JS init must run on a non-main thread
                await DCLTask.SwitchToThreadPool();
                if (destroyCancellationToken.IsCancellationRequested) return;

#if !UNITY_WEBGL
                // Provide basic thread-pool synchronization context
                SynchronizationContext.SetSynchronizationContext(new SynchronizationContext()); // IGNORE_LINE_WEBGL_THREAD_SAFETY_FLAG
#endif

                await scene.StartAsync(fps, destroyCancellationToken);

                // If JS init failed (handler set state to an error) or destroy was triggered, stop here.
                if (scene.SceneStateProvider.IsNotRunningState() || destroyCancellationToken.IsCancellationRequested)
                    return;

                // 2. Register the scene in the cache from the main thread (cache writes are main-thread only)
                await UniTask.SwitchToMainThread(cancellationToken: destroyCancellationToken);

                if (definitionComponent.IsPortableExperience)
                    scenesCache.AddPortableExperienceScene(scene, definitionComponent.IpfsPath.EntityId);
                else
                    scenesCache.Add(scene, definitionComponent.Parcels);

                ReportHub.LogProductionInfo($"Scene '{definitionComponent.Definition.GetLogSceneName()}' started");

                // 3. Run the update loop on the thread pool
                await DCLTask.SwitchToThreadPool();
                if (destroyCancellationToken.IsCancellationRequested) return;

                await scene.UpdateLoopAsync(destroyCancellationToken);
            }
            catch (Exception e)
            {
                ReportHub.LogException(e, GetReportData());
            }
        }
    }
}
