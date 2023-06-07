using CRDT.Memory;
using CRDT.Protocol;
using CrdtEcsBridge.Engine;
using CrdtEcsBridge.OutgoingMessages;
using CrdtEcsBridge.WorldSynchronizer;
using Cysharp.Threading.Tasks;
using SceneRunner.ECSWorld;
using SceneRunner.Scene;
using SceneRuntime;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace SceneRunner
{
    public class SceneFacade : ISceneFacade
    {
        internal readonly ISceneRuntime runtimeInstance;
        internal readonly ECSWorldFacade ecsWorldFacade;
        internal readonly ICRDTProtocol crdtProtocol;
        internal readonly IOutgoingCRTDMessagesProvider outgoingCrtdMessagesProvider;
        internal readonly ICRDTWorldSynchronizer crdtWorldSynchronizer;
        internal readonly IInstancePoolsProvider instancePoolsProvider;
        internal readonly ICRDTMemoryAllocator crdtMemoryAllocator;

        private int intervalMS;
        private bool started;

        private bool disposed;

        private readonly UniTaskCompletionSource completionSource;

        public SceneFacade(
            ISceneRuntime runtimeInstance,
            ECSWorldFacade ecsWorldFacade,
            ICRDTProtocol crdtProtocol,
            IOutgoingCRTDMessagesProvider outgoingCrtdMessagesProvider,
            ICRDTWorldSynchronizer crdtWorldSynchronizer,
            IInstancePoolsProvider instancePoolsProvider,
            ICRDTMemoryAllocator crdtMemoryAllocator)
        {
            this.runtimeInstance = runtimeInstance;
            this.ecsWorldFacade = ecsWorldFacade;
            this.crdtProtocol = crdtProtocol;
            this.outgoingCrtdMessagesProvider = outgoingCrtdMessagesProvider;
            this.crdtWorldSynchronizer = crdtWorldSynchronizer;
            this.instancePoolsProvider = instancePoolsProvider;
            this.crdtMemoryAllocator = crdtMemoryAllocator;

            completionSource = new UniTaskCompletionSource();
        }

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

            if (started)
                throw new ThreadStateException($"{nameof(StartUpdateLoop)} is already started!");

            started = true;

            SetTargetFPS(targetFPS);

            // Start the scene

            await runtimeInstance.StartScene();
            AssertIsNotMainThread(nameof(SceneRuntimeImpl.StartScene));

            var stopWatch = new Stopwatch();
            var deltaTime = 0f;

            while (true)
            {
                if (ct.IsCancellationRequested)
                {
                    completionSource.TrySetCanceled();
                    break;
                }

                if (Volatile.Read(ref disposed))
                {
                    completionSource.TrySetResult();
                    break;
                }

                stopWatch.Restart();

                // We can't guarantee that the thread is preserved between updates
                await runtimeInstance.UpdateScene(deltaTime);

                AssertIsNotMainThread(nameof(SceneRuntimeImpl.UpdateScene));

                var sleepMS = Math.Max(intervalMS - (int)stopWatch.ElapsedMilliseconds, 0);

                // We can't use Thread.Sleep as EngineAPI is called on the same thread
                // We can't use UniTask.Delay as this loop has nothing to do with the Unity Player Loop
                await Task.Delay(sleepMS); // No CancellationToken -> Cancel gracefully
                AssertIsNotMainThread(nameof(Task.Delay));
                deltaTime = stopWatch.ElapsedMilliseconds / 1000f;
            }
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
            Volatile.Write(ref disposed, true);

            // TODO do it better
            runtimeInstance.SetIsDisposing();

            await completionSource.Task.SuppressCancellationThrow();
            await UniTask.Yield(PlayerLoopTiming.Initialization);

            runtimeInstance.Dispose();
            ecsWorldFacade.Dispose();
            crdtProtocol.Dispose();
            outgoingCrtdMessagesProvider.Dispose();
            crdtWorldSynchronizer.Dispose();
            instancePoolsProvider.Dispose();
            crdtMemoryAllocator.Dispose();
        }
    }
}
