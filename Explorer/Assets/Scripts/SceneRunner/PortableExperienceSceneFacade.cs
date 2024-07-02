using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using Microsoft.ClearScript;
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
    public class PortableExperienceSceneFacade : ISceneFacade
    {
        internal readonly SceneInstanceDependencies.WithRuntimeAndJsAPIBase deps;

        public ISceneStateProvider SceneStateProvider => deps.SyncDeps.SceneStateProvider;
        public SceneEcsExecutor EcsExecutor => deps.SyncDeps.EcsExecutor;

        private ISceneRuntime runtimeInstance => deps.Runtime;
        private ISceneExceptionsHandler sceneExceptionsHandler => deps.SyncDeps.ExceptionsHandler;

        public ISceneData SceneData { get; }

        public bool IsEmpty => false;

        public SceneShortInfo Info => SceneData.SceneShortInfo;

        private int intervalMS;

        public PortableExperienceSceneFacade(
            ISceneData sceneData,
            SceneInstanceDependencies.WithRuntimeAndJsAPIBase deps)
        {
            this.deps = deps;
            SceneData = sceneData;
        }

        public void Dispose()
        {
            MultithreadingUtility.AssertMainThread(nameof(Dispose), true);

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

        public bool IsSceneReady()
        {
            return SceneData.SceneLoadingConcluded;
        }

        public async UniTask StartUpdateLoopAsync(int targetFPS, CancellationToken ct)
        {
            MultithreadingUtility.AssertMainThread(nameof(StartUpdateLoopAsync));

            if (SceneStateProvider.State != SceneState.NotStarted)
                throw new ThreadStateException($"{nameof(StartUpdateLoopAsync)} is already started!");

            // Process "main.crdt" first
            if (SceneData.StaticSceneMessages.Data.Length > 0)
                runtimeInstance.ApplyStaticMessages(SceneData.StaticSceneMessages.Data);

            SceneStateProvider.SetRunning(new SceneEngineStartInfo(DateTime.Now, (int)MultithreadingUtility.FrameCount));

            SetTargetFPS(targetFPS);
            SetIsCurrent(true);

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

            MultithreadingUtility.AssertMainThread(nameof(SceneRuntimeImpl.StartScene));

            var stopWatch = new Stopwatch();
            var deltaTime = 0f;

            try
            {
                while (true)
                {
                    // 1. 'ct' is an external cancellation token
                    if (ct.IsCancellationRequested) break;

                    // 2. don't try to run the update loop if the scene is not running
                    if (SceneStateProvider.State is SceneState.Disposing
                        or SceneState.Disposed
                        or SceneState.JavaScriptError
                        or SceneState.EngineError)
                        break;

                    stopWatch.Restart();

                    try
                    {
                        // We can't guarantee that the thread is preserved between updates
                        await runtimeInstance.UpdateScene(deltaTime);
                    }
                    catch (ScriptEngineException e) { sceneExceptionsHandler.OnJavaScriptException(e); }

                    SceneStateProvider.TickNumber++;

                    MultithreadingUtility.AssertMainThread(nameof(SceneRuntimeImpl.UpdateScene));

                    // Passing ct to Task.Delay allows to break the loop immediately
                    // as, otherwise, due to 0 or low FPS it can spin for much longer

                    if (!await IdleWhileRunningAsync(ct))
                        break;

                    int sleepMS = Math.Max(intervalMS - (int)stopWatch.ElapsedMilliseconds, 0);

                    // We can't use Thread.Sleep as EngineAPI is called on the same thread
                    // We can't use UniTask.Delay as this loop has nothing to do with the Unity Player Loop
                    await Task.Delay(sleepMS, ct);
                    MultithreadingUtility.AssertMainThread(nameof(Task.Delay));
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

        public void SetIsCurrent(bool isCurrent)
        {
            SceneStateProvider.IsCurrent = isCurrent;
            runtimeInstance.OnSceneIsCurrentChanged(isCurrent);
            deps.SyncDeps.ECSWorldFacade.OnSceneIsCurrentChanged(isCurrent);
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
            deps.Dispose();
        }
    }
}
