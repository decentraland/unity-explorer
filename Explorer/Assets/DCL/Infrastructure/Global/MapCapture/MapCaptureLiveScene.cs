using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Ipfs;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.SceneRunner.Scene;
using DCL.Utilities;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.Reporting;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.SceneLifeCycle.Systems;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using SceneRunner;
using SceneRunner.Scene;
using System;
using System.Collections.Generic;
using System.Threading;
using Unity.Mathematics;
using UnityEngine;
using Utility;
using Utility.Multithreading;

namespace Global.MapCapture
{
    /// <summary>
    ///     Runs one deployed scene the way the client does, JavaScript included, without the client's scene life
    ///     cycle: the definition comes from the registry, the facade from the scene factory, and the update loop
    ///     is started directly. Readiness is the same report the loading screen waits on.
    /// </summary>
    public class MapCaptureLiveSceneLoader : IDisposable
    {
        private const int SCENE_FPS = 30;

        private readonly World world;
        private readonly IDecentralandUrlsSource urls;
        private readonly ISceneFactory sceneFactory;
        private readonly LoadSceneSystemLogic loadLogic;
        private readonly ISceneReadinessReportQueue readinessReportQueue;
        private readonly List<SceneEntityDefinition> definitionsBuffer = new ();

        private MapCaptureLiveScene? current;

        public MapCaptureLiveSceneLoader(World world, IDecentralandUrlsSource urls, ISceneFactory sceneFactory, LoadSceneSystemLogic loadLogic, ISceneReadinessReportQueue readinessReportQueue)
        {
            this.world = world;
            this.urls = urls;
            this.sceneFactory = sceneFactory;
            this.loadLogic = loadLogic;
            this.readinessReportQueue = readinessReportQueue;
        }

        /// <summary>
        ///     A scene still alive when the capture is torn down is disposed synchronously: its V8 engine must not
        ///     outlive the player, or the editor hangs leaving play mode.
        /// </summary>
        public void Dispose()
        {
            current?.Dispose();
            current = null;
        }

        /// <summary>Loads and starts the scene that occupies <paramref name="parcel" />, or returns null when no scene does.</summary>
        public async UniTask<MapCaptureLiveScene?> LoadAsync(Vector2Int parcel, CancellationToken ct)
        {
            SceneEntityDefinition? definition = await FetchDefinitionAsync(parcel, ct);

            if (definition == null)
                return null;

            var ipfsPath = new IpfsPath(definition.id, URLDomain.FromString(urls.Url(DecentralandUrl.Content)));
            SceneDefinitionComponent component = SceneDefinitionComponentFactory.CreateFromDefinition(definition, ipfsPath, false, limitHeightByParcels: true);

            // Enqueued before the first tick: the scene's GLTF gathering only waits for its models when a report is
            // waiting for one of its parcels, otherwise it concludes after ten ticks regardless.
            AsyncLoadProcessReport report = AsyncLoadProcessReport.Create(ct);
            readinessReportQueue.Enqueue(parcel, report);

            ISceneFacade facade = await loadLogic.FlowAsync(world, sceneFactory, new GetSceneFacadeIntention(component, ISSDescriptor.NONE),
                ReportCategory.SCENE_LOADING, PartitionComponent.TOP_PRIORITY, ct);

            // Never current: a current scene may drive the camera rig and the player, and the capture owns both.
            var loopCancellation = CancellationTokenSource.CreateLinkedTokenSource(ct);
            RunUpdateLoopAsync(facade, loopCancellation.Token).Forget();

            current = new MapCaptureLiveScene(facade, component, report, loopCancellation);
            return current;
        }

        /// <summary>Resolves the registry request through the capture world's own definition loader.</summary>
        private async UniTask<SceneEntityDefinition?> FetchDefinitionAsync(Vector2Int parcel, CancellationToken ct)
        {
            definitionsBuffer.Clear();
            var pointers = new List<int2> { new (parcel.x, parcel.y) };
            string url = urls.Url(DecentralandUrl.EntitiesActive);

            var promise = AssetPromise<SceneDefinitions, GetSceneDefinitionList>.Create(world,
                new GetSceneDefinitionList(definitionsBuffer, pointers, new CommonLoadingArguments(url)), PartitionComponent.TOP_PRIORITY);

            StreamableLoadingResult<SceneDefinitions> result;

            try
            {
                while (!promise.TryConsume(world, out result))
                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }
            catch (OperationCanceledException)
            {
                promise.ForgetLoading(world);
                throw;
            }

            if (!result.Succeeded)
                throw new InvalidOperationException($"The registry request for ({parcel.x},{parcel.y}) failed: {result.Exception}");

            foreach (SceneEntityDefinition definition in result.Asset.Value)
                if (definition.Contains(parcel))
                    return definition;

            return null;
        }

        private static async UniTaskVoid RunUpdateLoopAsync(ISceneFacade scene, CancellationToken ct)
        {
            try
            {
                await DCLTask.SwitchToThreadPool();

                // The scene loop awaits on this context; the thread pool has none by default.
                SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());
                await scene.StartUpdateLoopAsync(SCENE_FPS, ct);
            }
            catch (OperationCanceledException) { }
            catch (Exception e) { ReportHub.LogException(e, ReportCategory.SCENE_LOADING); }
        }
    }

    /// <summary>A started scene and what the capture needs to frame, judge and tear it down.</summary>
    public class MapCaptureLiveScene : IUniTaskAsyncDisposable, IDisposable
    {
        private const int POLL_INTERVAL_MS = 250;

        public readonly ISceneFacade Facade;
        public readonly SceneDefinitionComponent Definition;

        private readonly AsyncLoadProcessReport report;
        private readonly CancellationTokenSource loopCancellation;

        private bool disposed;

        public bool Failed => Facade.SceneStateProvider.IsNotRunningState();

        public MapCaptureLiveScene(ISceneFacade facade, SceneDefinitionComponent definition, AsyncLoadProcessReport report, CancellationTokenSource loopCancellation)
        {
            Facade = facade;
            Definition = definition;
            this.report = report;
            this.loopCancellation = loopCancellation;
        }

        /// <summary>Lets the scene loop finish before releasing the engine; the normal end of a capture.</summary>
        public async UniTask DisposeAsync()
        {
            if (disposed) return;

            disposed = true;
            loopCancellation.SafeCancelAndDispose();
            await Facade.DisposeAsync();
        }

        /// <summary>Releases the engine at once, for a teardown that cannot wait on the scene loop.</summary>
        public void Dispose()
        {
            if (disposed) return;

            disposed = true;
            loopCancellation.SafeCancelAndDispose();
            Facade.Dispose();
        }

        /// <summary>
        ///     Waits until the scene's models have loaded, the scene failed, or <paramref name="timeoutSec" /> passed.
        ///     True only in the first case.
        /// </summary>
        public async UniTask<bool> WaitUntilReadyAsync(float timeoutSec, CancellationToken ct)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSec;

            while (Time.realtimeSinceStartup < deadline)
            {
                if (Failed)
                    return false;

                if (report.GetStatus().TaskStatus == UniTaskStatus.Succeeded)
                    return true;

                await UniTask.Delay(POLL_INTERVAL_MS, cancellationToken: ct);
            }

            return false;
        }
    }
}
