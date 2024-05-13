using CRDT.Memory;
using CRDT.Protocol;
using CrdtEcsBridge.OutgoingMessages;
using CrdtEcsBridge.PoolsProviders;
using CrdtEcsBridge.WorldSynchronizer;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Interaction.Utility;
using Microsoft.ClearScript;
using SceneRunner.ECSWorld;
using SceneRunner.Scene;
using SceneRunner.Scene.ExceptionsHandling;
using SceneRuntime;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Utility.Multithreading;

namespace SceneRunner
{
    public class SceneFacade : ISceneFacade
    {
        internal readonly ICRDTMemoryAllocator crdtMemoryAllocator;
        internal readonly ICRDTProtocol crdtProtocol;
        internal readonly ICRDTWorldSynchronizer crdtWorldSynchronizer;
        internal readonly ECSWorldFacade ecsWorldFacade;
        internal readonly IEntityCollidersSceneCache entityCollidersSceneCache;
        internal readonly IInstancePoolsProvider instancePoolsProvider;
        internal readonly IOutgoingCRDTMessagesProvider outgoingCrtdMessagesProvider;
        internal readonly ISceneRuntime runtimeInstance;
        internal readonly ISceneExceptionsHandler sceneExceptionsHandler;

        private readonly SceneInstanceDependencies dependencies;

        private int intervalMS;

        public SceneFacade(ISceneRuntime runtimeInstance, SceneInstanceDependencies dependencies, ISceneData sceneData)
        {
            this.runtimeInstance = runtimeInstance;
            this.dependencies = dependencies;
            this.ecsWorldFacade = dependencies.ECSWorldFacade;
            this.crdtProtocol = dependencies.CRDTProtocol;
            this.outgoingCrtdMessagesProvider = dependencies.OutgoingCRDTMessagesProvider;
            this.crdtWorldSynchronizer =  dependencies.CRDTWorldSynchronizer;
            this.instancePoolsProvider =  dependencies.PoolsProvider;
            this.crdtMemoryAllocator = dependencies.CRDTMemoryAllocator;
            this.sceneExceptionsHandler = dependencies.ExceptionsHandler;
            this.entityCollidersSceneCache = dependencies.EntityCollidersCache;
            SceneData = sceneData;
            EcsExecutor = dependencies.EcsExecutor;
            SceneStateProvider = dependencies.SceneStateProvider;
        }

        public ISceneData SceneData { get; }
        public ISceneStateProvider SceneStateProvider { get; }
        public SceneEcsExecutor EcsExecutor { get; }

        public SceneShortInfo Info => SceneData.SceneShortInfo;
        public bool IsEmpty { get; } = false;

        public void Dispose()
        {
            AssertMainThread(nameof(Dispose), true);

            SceneStateProvider.State = SceneState.Disposing;
            runtimeInstance.SetIsDisposing();

            DisposeInternal();

            SceneStateProvider.State = SceneState.Disposed;
        }

        public void SetTargetFPS(int fps)
        {
            intervalMS = (int)(1000f / fps);
        }

        UniTask ISceneFacade.StartScene() =>
            runtimeInstance.StartScene();

        UniTask ISceneFacade.Tick(float dt) =>
            runtimeInstance.UpdateScene(dt);

        public bool Contains(Vector2Int parcel)
        {
            foreach (Vector2Int sceneParcel in SceneData.Parcels)
            {
                if (sceneParcel != parcel) continue;
                return true;
            }

            return false;
        }

        public async UniTask StartUpdateLoopAsync(int targetFPS, CancellationToken ct)
        {
            AssertMainThread(nameof(StartUpdateLoopAsync));

            if (SceneStateProvider.State != SceneState.NotStarted)
                throw new ThreadStateException($"{nameof(StartUpdateLoopAsync)} is already started!");

            // Process "main.crdt" first
            if (SceneData.StaticSceneMessages.Data.Length > 0)
                runtimeInstance.ApplyStaticMessages(SceneData.StaticSceneMessages.Data);

            SceneStateProvider.SetRunning(new SceneEngineStartInfo(DateTime.Now, (int)MultithreadingUtility.FrameCount));

            SetTargetFPS(targetFPS);

            try
            {
                // Start the scene
                await runtimeInstance.StartScene();
            }
            catch (ScriptEngineException e)
            {
                sceneExceptionsHandler.OnJavaScriptException(e);
                return;
            }

            AssertMainThread(nameof(SceneRuntimeImpl.StartScene));

            var stopWatch = new Stopwatch();
            var deltaTime = 0f;

            try
            {
                while (true)
                {
                    // 1. 'ct' is an external cancellation token
                    // 2. don't try to run the update loop if DisposeAsync was already called
                    if (ct.IsCancellationRequested || SceneStateProvider.State is SceneState.Disposing or SceneState.Disposed)
                        break;

                    stopWatch.Restart();

                    try
                    {
                        // We can't guarantee that the thread is preserved between updates
                        await runtimeInstance.UpdateScene(deltaTime);
                    }
                    catch (ScriptEngineException e)
                    {
                        sceneExceptionsHandler.OnJavaScriptException(e);
                        break;
                    }

                    SceneStateProvider.TickNumber++;

                    AssertMainThread(nameof(SceneRuntimeImpl.UpdateScene));

                    // Passing ct to Task.Delay allows to break the loop immediately
                    // as, otherwise, due to 0 or low FPS it can spin for much longer

                    if (!await IdleWhileRunningAsync(ct))
                        break;

                    int sleepMS = Math.Max(intervalMS - (int)stopWatch.ElapsedMilliseconds, 0);

                    // We can't use Thread.Sleep as EngineAPI is called on the same thread
                    // We can't use UniTask.Delay as this loop has nothing to do with the Unity Player Loop
                    await Task.Delay(sleepMS, ct);
                    AssertMainThread(nameof(Task.Delay));
                    deltaTime = stopWatch.ElapsedMilliseconds / 1000f;
                }
            }
            catch (OperationCanceledException) { }
        }

        private async ValueTask<bool> IdleWhileRunningAsync(CancellationToken ct)
        {
            bool TryComplete()
            {
                if (SceneStateProvider.State != SceneState.Running)
                    return true;

                return false;
            }

            if (TryComplete())
                return false;

            // Support scene freeze (0 FPS, int.MinValue)
            while (intervalMS < 0)
            {
                if (TryComplete())
                    return false;

                // Just idle, don't do anything, need to wait for an actual value
                await Task.Delay(10, ct);
            }

            return true;
        }

        /// <summary>
        ///     Must ensure that the execution does not jump between different threads
        /// </summary>
        [Conditional("UNITY_EDITOR")]
        [Conditional("DEBUG")]
        private static void AssertMainThread(string funcName, bool isMainThread = false)
        {
            if (PlayerLoopHelper.IsMainThread != isMainThread)
                throw new ThreadStateException($"Execution after calling {funcName} must be {(isMainThread ? "on" : "off")} the main thread");
        }

        public void SetIsCurrent(bool isCurrent)
        {
            SceneStateProvider.IsCurrent = isCurrent;
            runtimeInstance.OnSceneIsCurrentChanged(isCurrent);
            ecsWorldFacade.OnSceneIsCurrentChanged(isCurrent);
        }

        public async UniTask DisposeAsync()
        {
            // Because of multithreading Disposing is not synced with the update loop
            // so just mark it as disposed and let the update loop handle the disposal
            SceneStateProvider.State = SceneState.Disposing;

            // TODO do it better
            runtimeInstance.SetIsDisposing();

            await UniTask.SwitchToMainThread(PlayerLoopTiming.Initialization);

            DisposeInternal();

            SceneStateProvider.State = SceneState.Disposed;
        }
        private void DisposeInternal()
        {
            runtimeInstance.Dispose();
            dependencies.Dispose();
        }
    }
}
