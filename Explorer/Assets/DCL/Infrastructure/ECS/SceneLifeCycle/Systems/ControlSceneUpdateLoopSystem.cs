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
using ECS.StreamableLoading.AssetBundles.InitialSceneState;
using ECS.StreamableLoading.Common;
using SceneRunner.Scene;
using System;
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
        }

        [Query]
        [None(typeof(DeleteEntityIntention), typeof(ISceneFacade), typeof(BannedSceneComponent))]
        private void HandleNotCreatedScenes(in Entity entity, ref AssetPromise<ISceneFacade, GetSceneFacadeIntention> promise,
            ref PartitionComponent partition, ref SceneDefinitionComponent sceneDefinitionComponent, ref InitialSceneStateDescriptor initialSceneState)
        {
            // Gracefully consume with the possibility of repetitions (in case the scene loading has failed)
            if (promise.IsConsumed)
            {
                // In case there is a failed promise (and it was not re-started) notify the readiness queue accordingly
                if (promise.TryGetResult(World, out var consumedResult) && !consumedResult.Succeeded)
                    SceneUtils.ReportException(consumedResult.Exception!, promise.LoadingIntention.DefinitionComponent.Parcels, sceneReadinessReportQueue);

                return;
            }

            if (!initialSceneState.IsDownloadedAndReady())
                return;

            if (promise.TryConsume(World, out var result) && result.Succeeded)
            {
                var scene = result.Asset!;

                var fps = realmPartitionSettings.GetSceneUpdateFrequency(in partition);

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
                    catch (Exception e) { ReportHub.LogException(e, GetReportData()); }
                }

                RunOnThreadPoolAsync().Forget();
                ReportHub.LogProductionInfo($"Scene '{sceneDefinitionComponent.Definition.GetLogSceneName()}' started");

                if (promise.LoadingIntention.DefinitionComponent.IsPortableExperience)
                {
                    scenesCache.AddPortableExperienceScene(scene, promise.LoadingIntention.DefinitionComponent.IpfsPath.EntityId);
                }
                else
                {
                    // So we know the scene has started
                    scenesCache.Add(scene, promise.LoadingIntention.DefinitionComponent.Parcels);
                }
                World.Add(entity, scene);
            }
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void ChangeSceneFPS(ref ISceneFacade sceneFacade,
            ref PartitionComponent partition)
        {
            if (!partition.IsDirty) return;

            sceneFacade.SetTargetFPS(realmPartitionSettings.GetSceneUpdateFrequency(in partition));
        }
    }
}
