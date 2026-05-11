using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.PluginSystem.World;
using Microsoft.ClearScript;
using RichTypes;
using SceneRunner.Admins;
using SceneRunner.Scene;
using SceneRunner.Scene.ExceptionsHandling;
using SceneRuntime;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Utility;
using Utility.Multithreading;

namespace SceneRunner
{
    public class SceneFacade : ISceneFacade
    {
        private const int HANG_WATCHDOG_POLL_INTERVAL_MS = 1000;
        private const int UPDATE_HANG_INTERRUPT_THRESHOLD_MS = 10000;
        private const int START_HANG_INTERRUPT_THRESHOLD_MS = 30000;

        // Cap dt passed to JS so a slow tick (e.g. host-parking on first iteration) can't feed an
        // absurd value into scene code that does dt-based stepping/integration, which would otherwise
        // spin synchronously inside V8 for hundreds of iterations and trip the hang watchdog.
        private const float MAX_DELTA_TIME = 0.1f;

        internal readonly SceneInstanceDependencies.WithRuntimeAndJsAPIBase deps;

        public ISceneStateProvider SceneStateProvider => deps.SyncDeps.SceneStateProvider;
        public SceneEcsExecutor EcsExecutor => deps.SyncDeps.EcsExecutor;
        public PersistentEntities PersistentEntities => deps.SyncDeps.ECSWorldFacade.PersistentEntities;

        internal ISceneRuntime runtimeInstance => deps.Runtime;
        private ISceneExceptionsHandler sceneExceptionsHandler => deps.SyncDeps.ExceptionsHandler;

        public ISceneData SceneData { get; }

        public bool IsEmpty => false;

        public SceneShortInfo Info => SceneData.SceneShortInfo;

        public Option<ISceneAdmins> SceneAdmins => deps.SceneAdmins;

        private int intervalMS;

        private readonly InterlockedFlag sceneCodeIsRunning = new ();

        // Hot-path watchdog state: set to Stopwatch.GetTimestamp() at the start of each UpdateScene
        // tick, reset to 0 when the tick completes.
        // Read from a separate watchdog continuation to detect hangs
        private long tickStartTimestamp;

        public SceneFacade(
            ISceneData sceneData,
            SceneInstanceDependencies.WithRuntimeAndJsAPIBase deps)
        {
            this.deps = deps;
            SceneData = sceneData;
        }

        public void Initialize()
        {
            deps.SyncDeps.ECSWorldFacade.Initialize();
        }

        /// <remarks>
        /// <see cref="SceneFacade"/> is a component in the global scene as an
        /// <see cref="ISceneFacade"/>. It owns its <see cref="SceneRuntimeImpl"/> through its
        /// <see cref="deps"/> field, which in turns owns its <see cref="V8ScriptEngine"/>. So that also
        /// shall be the chain of Dispose calls.
        /// </remarks>
        public void Dispose()
        {
            MultithreadingUtility.AssertMainThread(nameof(Dispose), true);

            SceneStateProvider.State.Set(SceneState.Disposing);
            runtimeInstance.SetIsDisposing();

            DisposeInternal();

            SceneStateProvider.State.Set(SceneState.Disposed);
        }

        public void SetTargetFPS(int fps)
        {
            intervalMS = (int)(1000f / fps);
        }

        UniTask ISceneFacade.StartScene() =>
            runtimeInstance.StartScene();

        UniTask ISceneFacade.Tick(float dt) =>
            runtimeInstance.UpdateScene(dt);

        public bool Contains(Vector2Int parcel) =>
            SceneData.SceneEntityDefinition.Contains(parcel);

        public bool IsSceneReady() =>
            SceneData.SceneLoadingConcluded;

        /// <summary>
        ///     Initializes the scene and runs JS startup code. Returns when the scene's onStart has completed.
        ///     Must be called from a non-main thread (V8 execution requirement).
        ///     If JS init throws, the scene is left in its current state and the exception is reported via
        ///     <see cref="sceneExceptionsHandler"/>; callers should observe <see cref="SceneStateProvider"/>
        ///     before proceeding to <see cref="UpdateLoopAsync"/>.
        /// </summary>
        public async UniTask<SceneState> StartAsync(int targetFPS, CancellationToken ct)
        {
            MultithreadingUtility.AssertMainThread(nameof(StartAsync));

            if (SceneStateProvider.State != SceneState.NotStarted)
                throw new ThreadStateException($"{nameof(StartAsync)} is already started!");

            // Process "main.crdt" first
            if (SceneData.StaticSceneMessages.Data.Length > 0)
                runtimeInstance.ApplyStaticMessages(SceneData.StaticSceneMessages.Data);

            SceneStateProvider.SetRunning(new SceneEngineStartInfo(DateTime.Now, (int)MultithreadingUtility.FrameCount));

            SetTargetFPS(targetFPS);

            sceneCodeIsRunning.Set();
            DCLInterlocked.Exchange(ref tickStartTimestamp, Stopwatch.GetTimestamp());

            // One-shot watchdog: if JS init doesn't complete within START_HANG_INTERRUPT_THRESHOLD_MS,
            // interrupt the engine and mark the scene as failed.
            var watchdogCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            // This is a safeguard to prevent the JS thread to become stuck keeping the scene state as running when it failed
            // See: https://github.com/decentraland/unity-explorer/issues/8654
            // https://github.com/decentraland/unity-explorer/issues/8493
            RunHangWatchdogAsync(START_HANG_INTERRUPT_THRESHOLD_MS, watchdogCts.Token).Forget();

            try
            {
                // Start the scene (runs JS onStart)
                await runtimeInstance.StartScene();
            }
            catch (ScriptEngineException e) { sceneExceptionsHandler.OnJavaScriptException(e); }
            finally
            {
                Interlocked.Exchange(ref tickStartTimestamp, 0);
                watchdogCts.SafeCancelAndDispose();
                sceneCodeIsRunning.Reset();
            }

            MultithreadingUtility.AssertMainThread(nameof(SceneRuntimeImpl.StartScene));

            return SceneStateProvider.State.Value();
        }

        /// <summary>
        ///     Runs the per-tick update loop. Must be called after <see cref="StartAsync"/> completes
        ///     and the scene state is still Running.
        /// </summary>
        public async UniTask UpdateLoopAsync(CancellationToken ct)
        {
            MultithreadingUtility.AssertMainThread(nameof(UpdateLoopAsync));

            if (SceneStateProvider.IsNotRunningState())
                return;

            var stopWatch = new Stopwatch();
            var deltaTime = 0f;

            var watchdogCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            // This is a safeguard to prevent the JS thread to become stuck keeping the scene state as running when it failed
            // See: https://github.com/decentraland/unity-explorer/issues/8654
            // https://github.com/decentraland/unity-explorer/issues/8493
            RunHangWatchdogAsync(UPDATE_HANG_INTERRUPT_THRESHOLD_MS, watchdogCts.Token).Forget();

            try
            {
                while (true)
                {
                    // 1. 'ct' is an external cancellation token
                    if (ct.IsCancellationRequested) break;

                    // 2. don't try to run the update loop if the scene is not running
                    if (SceneStateProvider.IsNotRunningState())
                        break;

                    stopWatch.Restart();

                    sceneCodeIsRunning.Set();
                    Interlocked.Exchange(ref tickStartTimestamp, Stopwatch.GetTimestamp());

                    try
                    {
                        // We can't guarantee that the thread is preserved between updates
                        await runtimeInstance.UpdateScene(deltaTime);
                    }
                    catch (ScriptEngineException e) { sceneExceptionsHandler.OnJavaScriptException(e); }
                    finally
                    {
                        Interlocked.Exchange(ref tickStartTimestamp, 0);
                        sceneCodeIsRunning.Reset();
                    }

                    SceneStateProvider.TickNumber++;

                    MultithreadingUtility.AssertMainThread(nameof(SceneRuntimeImpl.UpdateScene));

                    // Passing ct to Task.Delay allows to break the loop immediately
                    // as, otherwise, due to 0 or low FPS it can spin for much longer

                    if (!await IdleWhileRunningAsync(ct))
                        break;

                    int sleepMS = Math.Max(intervalMS - (int)stopWatch.ElapsedMilliseconds, 0);

                    // We can't use Thread.Sleep as EngineAPI is called on the same thread // IGNORE_LINE_WEBGL_THREAD_SAFETY_FLAG
                    // We can't use UniTask.Delay as this loop has nothing to do with the Unity Player Loop
                    await DCLTask.Delay(sleepMS, ct);
                    MultithreadingUtility.AssertMainThread(nameof(DCLTask.Delay));
                    // Some scenes fail when delta time is large, locking the JS thread
                    // See: https://github.com/decentraland/unity-explorer/issues/8654
                    // https://github.com/decentraland/unity-explorer/issues/8493
                    deltaTime = Math.Min(stopWatch.ElapsedMilliseconds / 1000f, MAX_DELTA_TIME);
                }
            }
            catch (OperationCanceledException) { }
            finally { watchdogCts.SafeCancelAndDispose(); }
        }

        private async UniTaskVoid RunHangWatchdogAsync(int thresholdMs, CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    await DCLTask.Delay(HANG_WATCHDOG_POLL_INTERVAL_MS, ct);

                    long startTs = Interlocked.Read(ref tickStartTimestamp);
                    if (startTs == 0) continue; // No tick in progress

                    long elapsedMs = (Stopwatch.GetTimestamp() - startTs) * 1000 / Stopwatch.Frequency;
                    if (elapsedMs < thresholdMs) continue; // Tick still under threshold

                    string? id = SceneData.SceneEntityDefinition.id;

                    ReportHub.LogError(ReportCategory.SCENE_LOADING, $"Scene '{id}' hung for {elapsedMs}ms (threshold={thresholdMs}ms) — interrupting engine and marking scene as JavaScriptError");

                    // Mark scene as failed first so the loop's IsNotRunningState() check breaks on the
                    // next iteration after the interrupt unwinds the await.
                    // Don't override dispose/error states
                    if (!SceneStateProvider.IsNotRunningState())
                        SceneStateProvider.State.Set(SceneState.JavaScriptError);

                    // Force V8 to throw ScriptInterruptedException from the running JS at the next safe point.
                    // The exception propagates out of the in-flight call -> caught by the caller's catch (ScriptEngineException) block.
                    runtimeInstance.Interrupt();

                    return; // Stop watchdog after issuing interrupt
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception e) { ReportHub.LogException(e, ReportCategory.SCENE_LOADING); }
        }

        private async
#if UNITY_WEBGL
            Cysharp.Threading.Tasks.UniTask<bool>
#else
            ValueTask<bool> // IGNORE_LINE_WEBGL_SYSTEM_TASKS_SAFETY_FLAG
#endif
            IdleWhileRunningAsync(CancellationToken ct)
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
                await DCLTask.Delay(10, ct);
            }

            return true;
        }

        public void SetIsCurrent(bool isCurrent)
        {
            SceneStateProvider.IsCurrent = isCurrent;
            runtimeInstance.OnSceneIsCurrentChanged(isCurrent);
            deps.SyncDeps.ECSWorldFacade.OnSceneIsCurrentChanged(isCurrent);
        }

        /// <remarks>
        /// <see cref="SceneFacade"/> is a component in the global scene as an
        /// <see cref="ISceneFacade"/>. It owns its <see cref="SceneRuntimeImpl"/> through its
        /// <see cref="deps"/> field, which in turns owns its <see cref="V8ScriptEngine"/>. So that also
        /// shall be the chain of Dispose calls.
        /// </remarks>
        public async UniTask DisposeAsync()
        {
            // Because of multithreading Disposing is not synced with the update loop
            // so just mark it as disposed and let the update loop handle the disposal
            SceneStateProvider.State.Set(SceneState.Disposing);

            runtimeInstance.SetIsDisposing();

            await UniTask.SwitchToMainThread(PlayerLoopTiming.Initialization);

            // Let the scene loop finish gracefully to prevent synchronous exceptions:
            // Microsoft.ClearScript.ScriptEngineException
            // Error: Cannot access a disposed object.
            while (sceneCodeIsRunning)
                await UniTask.Yield(PlayerLoopTiming.Initialization);

            DisposeInternal();
            SceneData.InitialSceneStateInfo.Dispose();

            SceneStateProvider.State.Set(SceneState.Disposed);
        }

        private void DisposeInternal()
        {
            deps.Dispose();
        }
    }
}
