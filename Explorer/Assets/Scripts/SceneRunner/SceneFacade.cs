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
        internal readonly ISceneRuntime runtimeInstance;
        internal readonly ECSWorldFacade ecsWorldFacade;
        internal readonly ICRDTProtocol crdtProtocol;
        internal readonly IOutgoingCRDTMessagesProvider outgoingCrtdMessagesProvider;
        internal readonly ICRDTWorldSynchronizer crdtWorldSynchronizer;
        internal readonly IInstancePoolsProvider instancePoolsProvider;
        internal readonly ICRDTMemoryAllocator crdtMemoryAllocator;
        internal readonly ISceneExceptionsHandler sceneExceptionsHandler;
        internal readonly IEntityCollidersSceneCache entityCollidersSceneCache;
        internal readonly ISceneStateProvider sceneStateProvider;

        private int intervalMS;

        public ISceneData SceneData { get; }

        public SceneShortInfo Info => SceneData.SceneShortInfo;

        public SceneFacade(
            ISceneRuntime runtimeInstance,
            ECSWorldFacade ecsWorldFacade,
            ICRDTProtocol crdtProtocol,
            IOutgoingCRDTMessagesProvider outgoingCrtdMessagesProvider,
            ICRDTWorldSynchronizer crdtWorldSynchronizer,
            IInstancePoolsProvider instancePoolsProvider,
            ICRDTMemoryAllocator crdtMemoryAllocator,
            ISceneExceptionsHandler sceneExceptionsHandler,
            ISceneStateProvider sceneStateProvider,
            IEntityCollidersSceneCache entityCollidersSceneCache,
            ISceneData sceneData)
        {
            this.runtimeInstance = runtimeInstance;
            this.ecsWorldFacade = ecsWorldFacade;
            this.crdtProtocol = crdtProtocol;
            this.outgoingCrtdMessagesProvider = outgoingCrtdMessagesProvider;
            this.crdtWorldSynchronizer = crdtWorldSynchronizer;
            this.instancePoolsProvider = instancePoolsProvider;
            this.crdtMemoryAllocator = crdtMemoryAllocator;
            this.sceneExceptionsHandler = sceneExceptionsHandler;
            this.sceneStateProvider = sceneStateProvider;
            this.entityCollidersSceneCache = entityCollidersSceneCache;
            SceneData = sceneData;
        }

        public void Dispose()
        {
            AssertMainThread(nameof(Dispose), true);

            sceneStateProvider.State = SceneState.Disposing;
            runtimeInstance.SetIsDisposing();

            DisposeInternal();

            sceneStateProvider.State = SceneState.Disposed;
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

            if (sceneStateProvider.State != SceneState.NotStarted)
                throw new ThreadStateException($"{nameof(StartUpdateLoopAsync)} is already started!");

            // Process "main.crdt" first
            if (SceneData.StaticSceneMessages.Data.Length > 0)
                runtimeInstance.ApplyStaticMessages(SceneData.StaticSceneMessages.Data);

            sceneStateProvider.SetRunning(new SceneEngineStartInfo(DateTime.Now, (int)MultithreadingUtility.FrameCount));

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
                    if (ct.IsCancellationRequested || sceneStateProvider.State is SceneState.Disposing or SceneState.Disposed)
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

                    sceneStateProvider.TickNumber++;

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
                if (sceneStateProvider.State != SceneState.Running)
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
            sceneStateProvider.IsCurrent = isCurrent;
            runtimeInstance.OnSceneIsCurrentChanged(isCurrent);
            ecsWorldFacade.OnSceneIsCurrentChanged(isCurrent);
        }

        public async UniTask DisposeAsync()
        {
            // Because of multithreading Disposing is not synced with the update loop
            // so just mark it as disposed and let the update loop handle the disposal
            sceneStateProvider.State = SceneState.Disposing;

            // TODO do it better
            runtimeInstance.SetIsDisposing();

            await UniTask.SwitchToMainThread(PlayerLoopTiming.Initialization);

            DisposeInternal();

            sceneStateProvider.State = SceneState.Disposed;
        }

        private void DisposeInternal()
        {
            runtimeInstance.Dispose();
            ecsWorldFacade.Dispose();
            crdtProtocol.Dispose();
            outgoingCrtdMessagesProvider.Dispose();
            crdtWorldSynchronizer.Dispose();
            instancePoolsProvider.Dispose();
            crdtMemoryAllocator.Dispose();
            sceneExceptionsHandler.Dispose();
            entityCollidersSceneCache.Dispose();
        }
    }
}
