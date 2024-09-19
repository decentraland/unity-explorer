using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Optimization.Pools;
using DCL.Optimization.ThreadSafePool;
using System;
using System.Linq;

namespace SceneRunner.Scene.ExceptionsHandling
{
    /// <summary>
    ///     Decides if scene should be put into an error state
    /// </summary>
    public class SceneExceptionsHandler : ISceneExceptionsHandler
    {
        // Experiment with this, maybe tolerance should be 0
        internal const int JAVASCRIPT_EXCEPTIONS_PER_MINUTE_TOLERANCE = 30;
        internal const int ENGINE_EXCEPTIONS_PER_MINUTE_TOLERANCE = 3;

        private static readonly ThreadSafeObjectPool<SceneExceptionsHandler> POOL = new (() => new SceneExceptionsHandler(), defaultCapacity: PoolConstants.SCENES_COUNT);

        private readonly ExceptionEntry?[] javascriptRegisteredExceptions = new ExceptionEntry?[JAVASCRIPT_EXCEPTIONS_PER_MINUTE_TOLERANCE];
        private readonly ExceptionEntry?[] engineRegisteredExceptions = new ExceptionEntry?[ENGINE_EXCEPTIONS_PER_MINUTE_TOLERANCE];
        private SceneShortInfo sceneShortInfo;

        private ISceneStateProvider? sceneState;

        private SceneExceptionsHandler() { }

        public void Dispose()
        {
            sceneState = null;
            Array.Clear(javascriptRegisteredExceptions, 0, javascriptRegisteredExceptions.Length);
            Array.Clear(engineRegisteredExceptions, 0, engineRegisteredExceptions.Length);
            POOL.Release(this);
        }

        public ISystemGroupExceptionHandler.Action Handle(Exception exception, Type systemGroupType) =>
            Handle(exception, new ReportData(ReportCategory.ECS, sceneShortInfo: sceneShortInfo), engineRegisteredExceptions);

        public void OnEngineException(Exception exception, string category)
        {
            // Can be already disposed of
            if (sceneState == null) return;

            if (Handle(exception, new ReportData(category, sceneShortInfo: sceneShortInfo), engineRegisteredExceptions) != ISystemGroupExceptionHandler.Action.Continue)
                sceneState!.State = SceneState.EngineError;
        }

        public void OnJavaScriptException(Exception exception)
        {
            // Can be already disposed of
            if (sceneState == null) return;

            // Stopping the scene execution after a javascript exception causes some scenes to not work at all, since they intentionally have execution errors. ie: events-board-api goerli 78,1.
            // In order to keep the scenes running we need to increase the error tolerance. Scenes that exceed will need a fix on the scene level.
            // This works different from the old kernel. The sdk7 runtime just does a try/catch: https://github.com/decentraland/scene-runtime/blob/adbf9e78c3b5d85d619c41494a177dfd7b6b5581/src/worker-sdk7/sdk7-runtime.ts#L119-L130
            if (Handle(exception, new ReportData(ReportCategory.JAVASCRIPT, sceneShortInfo: sceneShortInfo, sceneTickNumber: sceneState.TickNumber), javascriptRegisteredExceptions) != ISystemGroupExceptionHandler.Action.Continue)
                sceneState!.State = SceneState.JavaScriptError;
        }

        public async UniTask<T> ReportAndRethrowExceptionAsync<T>(UniTask<T> task)
        {
            try { return await task; }
            catch (OperationCanceledException)
            {
                // don't report cancellation exceptions
                throw;
            }
            catch (Exception e)
            {
                ReportApiException(e);
                throw;
            }
        }

        public async UniTask ReportAndRethrowExceptionAsync(UniTask task)
        {
            try { await task; }
            catch (OperationCanceledException)
            {
                // don't report cancellation exceptions
                throw;
            }
            catch (Exception e)
            {
                ReportApiException(e);
                throw;
            }
        }

        public void ReportApiException(Exception e)
        {
            ReportHub.LogException(e, new ReportData(ReportCategory.JAVASCRIPT, sceneShortInfo: sceneShortInfo, sceneTickNumber: sceneState!.TickNumber));
        }

        public static SceneExceptionsHandler Create(ISceneStateProvider sceneState, SceneShortInfo sceneShortInfo)
        {
            SceneExceptionsHandler handler = POOL.Get();
            handler.sceneState = sceneState;
            handler.sceneShortInfo = sceneShortInfo;
            return handler;
        }

        private ISystemGroupExceptionHandler.Action Handle(Exception exception, ReportData reportData, ExceptionEntry?[] registeredExceptions)
        {
            // 60 seconds
            const float INTERVAL_TICKS = 60 * 10000 * 1000;

            ReportException(exception, reportData);

            long time = DateTime.UtcNow.Ticks;

            var validRangeStartIndex = 0;
            int validRangeEndIndex = -1;

            lock (registeredExceptions)
            {
                for (var i = 0; i < registeredExceptions.Length; i++)
                {
                    ExceptionEntry? e = registeredExceptions[i];

                    if (!e.HasValue)
                        break;

                    // Detect invalid exceptions
                    if (time - e.Value.Time < INTERVAL_TICKS)
                    {
                        validRangeStartIndex = i;
                        validRangeEndIndex = i;

                        for (++i; i < registeredExceptions.Length; i++)
                        {
                            e = registeredExceptions[i];

                            if (!e.HasValue)
                                break;

                            validRangeEndIndex = i;
                        }

                        break;
                    }
                }

                // All tolerance is used
                if (exception is TimeoutException || (validRangeEndIndex == registeredExceptions.Length - 1 && validRangeStartIndex == 0))
                {
                    // log an aggregated exception
                    ReportHub.LogException(new SceneExecutionException(registeredExceptions.Select(e => e!.Value.Exception).Append(exception), new ReportData(ReportCategory.ECS, sceneShortInfo: sceneShortInfo)));

                    // Put the scene into the error state
                    sceneState!.State = SceneState.EcsError;
                    return ISystemGroupExceptionHandler.Action.Suspend;
                }

                // Shift the array to the left
                if (validRangeStartIndex > -1)
                    Array.Copy(registeredExceptions, validRangeStartIndex, registeredExceptions, 0, validRangeEndIndex - validRangeStartIndex + 1);

                int firstEmptySlot = validRangeEndIndex - validRangeStartIndex + 1;

                // Clear the rest of the array
                Array.Clear(registeredExceptions, firstEmptySlot, registeredExceptions.Length - firstEmptySlot);

                // Write to the first available slot
                registeredExceptions[firstEmptySlot] = new ExceptionEntry { Time = time, Exception = exception };

                return ISystemGroupExceptionHandler.Action.Continue;
            }
        }

        private void ReportException(Exception exception, ReportData reportData)
        {
            if (exception is EcsSystemException ecsSystemException)
            {
                // Add scene information, we don't add this info in the BaseUnityLoopSystem as we would need to propagate it for all systems
                // and it's inconvenient and cumbersome
                ecsSystemException.reportData.SceneShortInfo = sceneShortInfo;

                ReportHub.LogException(ecsSystemException);
            }
            else
                ReportHub.LogException(exception, reportData);
        }

        private struct ExceptionEntry
        {
            public long Time;
            public Exception Exception;
        }
    }
}
