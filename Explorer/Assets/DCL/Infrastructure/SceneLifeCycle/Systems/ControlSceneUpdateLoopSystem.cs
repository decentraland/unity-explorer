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
        // Fallback so a scene whose comms room never connects still starts; the normal release is the room connecting
        private static readonly TimeSpan SCENE_ROOM_CONNECT_TIMEOUT = TimeSpan.FromSeconds(15);

        private readonly IRealmPartitionSettings realmPartitionSettings;
        private readonly CancellationToken destroyCancellationToken;
        private readonly ISceneReadinessReportQueue sceneReadinessReportQueue;

        private readonly IScenesCache scenesCache;
        private readonly IRealmData realmData;
        private readonly ISceneRoomStatus sceneRoomStatus;

        internal ControlSceneUpdateLoopSystem(World world,
            IRealmPartitionSettings realmPartitionSettings,
            CancellationToken destroyCancellationToken,
            IScenesCache scenesCache,
            ISceneReadinessReportQueue sceneReadinessReportQueue,
            IRealmData realmData,
            ISceneRoomStatus sceneRoomStatus) : base(world)
        {
            this.realmPartitionSettings = realmPartitionSettings;
            this.destroyCancellationToken = destroyCancellationToken;
            this.scenesCache = scenesCache;
            this.sceneReadinessReportQueue = sceneReadinessReportQueue;
            this.realmData = realmData;
            this.sceneRoomStatus = sceneRoomStatus;
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
            StartAndUpdateSceneAsync(definitionComponent, partition, scene).Forget();

            World.Add(entity, scene);
        }

        [Query]
        [All(typeof(SmartWearableId))]
        [None(typeof(DeleteEntityIntention), typeof(SmartWearableSceneStarted))]
        private void HandleSmartWearableScenes(Entity entity, in SceneDefinitionComponent definitionComponent, in PartitionComponent partition, in ISceneFacade scene)
        {
            World.Add(entity, new SmartWearableSceneStarted());

            StartAndUpdateSceneAsync(definitionComponent, partition, scene).Forget();
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void ChangeSceneFPS(ref ISceneFacade sceneFacade, in PartitionComponent partition)
        {
            if (!partition.IsDirty) return;

            sceneFacade.SetTargetFPS(realmPartitionSettings.GetSceneUpdateFrequency(in partition));
        }

        private async UniTaskVoid StartAndUpdateSceneAsync(SceneDefinitionComponent definitionComponent, PartitionComponent partition, ISceneFacade scene)
        {
            int fps = realmPartitionSettings.GetSceneUpdateFrequency(partition);

            try
            {
                if (definitionComponent.IsPortableExperience)
                    scenesCache.AddPortableExperienceScene(scene, definitionComponent.IpfsPath.EntityId);
                else
                    scenesCache.Add(scene, definitionComponent.Parcels);

                // Peer CRDT state must land before the scene runs: local state produced first wins the CRDT merge and the synced entities are dropped
                await WaitForSceneRoomIfWorldAsync(definitionComponent);

                ReportHub.LogProductionInfo($"Scene '{definitionComponent.Definition.GetLogSceneName()}' started");

                await DCLTask.SwitchToThreadPool();

                if (destroyCancellationToken.IsCancellationRequested) return;

#if !UNITY_WEBGL

                // Provide basic thread-pool synchronization context
                SynchronizationContext.SetSynchronizationContext(new SynchronizationContext()); // IGNORE_LINE_WEBGL_THREAD_SAFETY_FLAG
#endif

                await scene.StartUpdateLoopAsync(fps, destroyCancellationToken);
            }
            catch (OperationCanceledException) { }
            catch (Exception e) { ReportHub.LogException(e, GetReportData()); }
        }

        private async UniTask WaitForSceneRoomIfWorldAsync(SceneDefinitionComponent definitionComponent)
        {
            // Genesis exploration shouldn't pay comms-connect latency on every scene
            if (definitionComponent.IsPortableExperience || !realmData.Configured || !realmData.IsWorld())
                return;

            // Only the teleport destination (marked by a pending readiness report) is gated: the room targets the player's scene only, so other scenes of the world would wait in vain
            if (!sceneReadinessReportQueue.HasReport(definitionComponent.Parcels))
                return;

            string sceneId = definitionComponent.Definition.id!;

            if (sceneRoomStatus.IsSceneRoomSettled(sceneId))
                return;

            try { await UniTask.WaitUntil(() => sceneRoomStatus.IsSceneRoomSettled(sceneId), cancellationToken: destroyCancellationToken).Timeout(SCENE_ROOM_CONNECT_TIMEOUT); }
            catch (TimeoutException)
            {
                ReportHub.LogWarning(GetReportData(), $"Scene '{definitionComponent.Definition.GetLogSceneName()}' started before its comms room connected");
            }
        }
    }
}
