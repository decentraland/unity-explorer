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
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.StreamableLoading.Common;
using SceneRunner.Scene;
using System;
using System.Threading;
using DCL.Chat;
using DCL.Chat.History;
using ECS.SceneLifeCycle.Reporting;
using SceneRunner.EmptyScene;
using UnityEngine;

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

        private readonly ISceneReadinessReportQueue sceneReadinesReportQueue;
        private readonly IChatHistory chatHistory;
        private PooledLoadReportList? reports;

        internal ControlSceneUpdateLoopSystem(World world,
            IRealmPartitionSettings realmPartitionSettings,
            CancellationToken destroyCancellationToken,
            IScenesCache scenesCache,
            ISceneReadinessReportQueue sceneReadinesReportQueue,
            IChatHistory chatHistory) : base(world)
        {
            this.realmPartitionSettings = realmPartitionSettings;
            this.destroyCancellationToken = destroyCancellationToken;
            this.scenesCache = scenesCache;
            this.sceneReadinesReportQueue = sceneReadinesReportQueue;
            this.chatHistory = chatHistory;
        }

        protected override void Update(float t)
        {
            ChangeSceneFPSQuery(World);
            StartSceneQuery(World);
        }

        [Query]
        [None(typeof(DeleteEntityIntention), typeof(ISceneFacade))]
        private void StartScene(in Entity entity, ref AssetPromise<ISceneFacade, GetSceneFacadeIntention> promise,
            ref PartitionComponent partition, ref SceneDefinitionComponent sceneDefinitionComponent)
        {
            // Gracefully consume with the possibility of repetitions (in case the scene loading has failed)
            if (promise.IsConsumed) return;

            if (promise.TryConsume(World, out var result))
            {
                if (result.Succeeded)
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
                        catch (Exception e)
                        {
                            ReportHub.LogException(e, GetReportData());
                        }
                    }

                    RunOnThreadPoolAsync().Forget();

                    if (promise.LoadingIntention.DefinitionComponent.IsPortableExperience)
                    {
                        scenesCache.AddPortableExperienceScene(scene,
                            promise.LoadingIntention.DefinitionComponent.IpfsPath.EntityId);
                    }
                    else
                    {
                        // So we know the scene has started
                        scenesCache.Add(scene, promise.LoadingIntention.DefinitionComponent.Parcels);
                    }

                    World.Add(entity, scene);
                }
                else
                {
                    if (sceneReadinesReportQueue.TryDequeue(
                            sceneDefinitionComponent.Definition.metadata.scene.DecodedParcels, out reports))
                    {
                        for (var i = 0; i < reports!.Value.Count; i++)
                        {
                            var report = reports.Value[i];
                            report.SetProgress(1);
                        }

                        reports.Value.Dispose();
                        reports = null;
                        chatHistory.AddMessage(ChatMessage.NewFromSystem(
                            $"🔴 Scene {sceneDefinitionComponent.Definition.metadata.scene.DecodedBase} failed to load"));
                    }

                    World.Add(entity,
                        new EmptySceneFacade.Args(new SceneShortInfo(
                            sceneDefinitionComponent.Definition.metadata.scene.DecodedBase, "BROKEN SDK7 SCENE")));
                }
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
