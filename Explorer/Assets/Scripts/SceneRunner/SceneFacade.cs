using CRDT.Memory;
using CRDT.Protocol;
using CrdtEcsBridge.Engine;
using CrdtEcsBridge.OutgoingMessages;
using CrdtEcsBridge.WorldSynchronizer;
using Cysharp.Threading.Tasks;
using DCL.Interaction.Utility;
using SceneRunner.ECSWorld;
using SceneRunner.Scene;
using SceneRunner.Scene.ExceptionsHandling;
using SceneRuntime;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
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
        private readonly ISceneStateProvider sceneStateProvider;

        private int intervalMS;

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

        public ISceneData SceneData { get; }

        public void SetTargetFPS(int fps)
        {
            intervalMS = (int)(1000f / fps);
        }

        UniTask ISceneFacade.StartScene() =>
            runtimeInstance.StartScene();

        UniTask ISceneFacade.Tick(float dt) =>
            runtimeInstance.UpdateScene(dt);

        public async UniTask StartUpdateLoop(int targetFPS, CancellationToken ct)
        {
            AssertIsNotMainThread(nameof(StartUpdateLoop));

            if (sceneStateProvider.State != SceneState.NotStarted)
                throw new ThreadStateException($"{nameof(StartUpdateLoop)} is already started!");

            // Process "main.crdt" first
            if (SceneData.StaticSceneMessages.Data.Length > 0)
                runtimeInstance.ApplyStaticMessages(SceneData.StaticSceneMessages.Data);

            sceneStateProvider.SetRunning(new SceneEngineStartInfo(DateTime.Now, (int)MultithreadingUtility.FrameCount));

            SetTargetFPS(targetFPS);

            // Start the scene

            await runtimeInstance.StartScene();
            AssertIsNotMainThread(nameof(SceneRuntimeImpl.StartScene));

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

                    // We can't guarantee that the thread is preserved between updates
                    await runtimeInstance.UpdateScene(deltaTime);
                    sceneStateProvider.TickNumber++;

                    AssertIsNotMainThread(nameof(SceneRuntimeImpl.UpdateScene));

                    // Passing ct to Task.Delay allows to break the loop immediately
                    // as, otherwise, due to 0 or low FPS it can spin for much longer

                    if (!await IdleWhileRunning(ct))
                        break;

                    int sleepMS = Math.Max(intervalMS - (int)stopWatch.ElapsedMilliseconds, 0);

                    // We can't use Thread.Sleep as EngineAPI is called on the same thread
                    // We can't use UniTask.Delay as this loop has nothing to do with the Unity Player Loop
                    await Task.Delay(sleepMS, ct);
                    AssertIsNotMainThread(nameof(Task.Delay));
                    deltaTime = stopWatch.ElapsedMilliseconds / 1000f;
                }
            }
            catch (OperationCanceledException) { }
        }

        private async ValueTask<bool> IdleWhileRunning(CancellationToken ct)
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
        /// Must ensure that the execution does not jump between different threads
        /// </summary>
        [Conditional("UNITY_EDITOR")]
        [Conditional("DEBUG")]
        private static void AssertIsNotMainThread(string funcName)
        {
            if (PlayerLoopHelper.IsMainThread)
                throw new ThreadStateException($"Execution after calling {funcName} must be off the main thread");
        }

        public async UniTask DisposeAsync()
        {
            // Because of multithreading Disposing is not synced with the update loop
            // so just mark it as disposed and let the update loop handle the disposal
            sceneStateProvider.State = SceneState.Disposing;

            // TODO do it better
            runtimeInstance.SetIsDisposing();

            await UniTask.SwitchToMainThread(PlayerLoopTiming.Initialization);

            runtimeInstance.Dispose();
            ecsWorldFacade.Dispose();
            crdtProtocol.Dispose();
            outgoingCrtdMessagesProvider.Dispose();
            crdtWorldSynchronizer.Dispose();
            instancePoolsProvider.Dispose();
            crdtMemoryAllocator.Dispose();
            sceneExceptionsHandler.Dispose();
            entityCollidersSceneCache.Dispose();

            sceneStateProvider.State = SceneState.Disposed;
        }
    }
}
