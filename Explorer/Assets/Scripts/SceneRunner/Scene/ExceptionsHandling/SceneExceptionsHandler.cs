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
        internal const int ECS_EXCEPTIONS_PER_MINUTE_TOLERANCE = 3;

        private static readonly ThreadSafeObjectPool<SceneExceptionsHandler> POOL = new (() => new SceneExceptionsHandler(), defaultCapacity: PoolConstants.SCENES_COUNT);

        private readonly ExceptionEntry?[] ecsExceptionsBag = new ExceptionEntry?[ECS_EXCEPTIONS_PER_MINUTE_TOLERANCE];
        private SceneShortInfo sceneShortInfo;

        private ISceneStateProvider? sceneState;

        private SceneExceptionsHandler() { }

        public void Dispose()
        {
            sceneState = null;
            Array.Clear(ecsExceptionsBag, 0, ecsExceptionsBag.Length);
            POOL.Release(this);
        }

        public ISystemGroupExceptionHandler.Action Handle(Exception exception, Type systemGroupType) =>
            Handle(exception, new ReportData(ReportCategory.ECS, sceneShortInfo: sceneShortInfo));

        public void OnEngineException(Exception exception, string category)
        {
            // Can be already disposed of
            if (sceneState == null) return;

            if (Handle(exception, new ReportData(category, sceneShortInfo: sceneShortInfo)) != ISystemGroupExceptionHandler.Action.Continue)
                sceneState!.State = SceneState.EngineError;
        }

        public void OnJavaScriptException(Exception exception)
        {
            // Can be already disposed of
            if (sceneState == null) return;

            if (Handle(exception, new ReportData(ReportCategory.JAVASCRIPT, sceneShortInfo: sceneShortInfo, sceneTickNumber: sceneState.TickNumber)) != ISystemGroupExceptionHandler.Action.Continue)
                sceneState!.State = SceneState.JavaScriptError;
        }

        public async UniTask<T> ReportAndRethrowException<T>(UniTask<T> task)
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

        public async UniTask ReportAndRethrowException(UniTask task)
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

        private ISystemGroupExceptionHandler.Action Handle(Exception exception, ReportData reportData)
        {
            // 60 seconds
            const float INTERVAL_TICKS = 60 * 10000 * 1000;

            // Report exception
            if (exception is EcsSystemException ecsSystemException)
            {
                // Add scene information, we don't add this info in the BaseUnityLoopSystem as we would need to propagate it for all systems
                // and it's inconvenient and cumbersome
                ecsSystemException.reportData.SceneShortInfo = sceneShortInfo;

                ReportHub.LogException(ecsSystemException);
            }
            else
                ReportHub.LogException(exception, reportData);

            long time = DateTime.UtcNow.Ticks;

            var validRangeStartIndex = 0;
            int validRangeEndIndex = -1;

            lock (ecsExceptionsBag)
            {
                for (var i = 0; i < ecsExceptionsBag.Length; i++)
                {
                    ExceptionEntry? e = ecsExceptionsBag[i];

                    if (!e.HasValue)
                        break;

                    // Detect invalid exceptions
                    if (time - e.Value.Time < INTERVAL_TICKS)
                    {
                        validRangeStartIndex = i;
                        validRangeEndIndex = i;

                        for (++i; i < ecsExceptionsBag.Length; i++)
                        {
                            e = ecsExceptionsBag[i];

                            if (!e.HasValue)
                                break;

                            validRangeEndIndex = i;
                        }

                        break;
                    }
                }

                // All tolerance is used
                if (validRangeEndIndex == ecsExceptionsBag.Length - 1 && validRangeStartIndex == 0)
                {
                    // log an aggregated exception
                    ReportHub.LogException(new SceneExecutionException(ecsExceptionsBag.Select(e => e!.Value.Exception).Append(exception), new ReportData(ReportCategory.ECS, sceneShortInfo: sceneShortInfo)));

                    // Put the scene into the error state
                    sceneState!.State = SceneState.EcsError;
                    return ISystemGroupExceptionHandler.Action.Suspend;
                }

                // Shift the array to the left
                if (validRangeStartIndex > -1)
                    Array.Copy(ecsExceptionsBag, validRangeStartIndex, ecsExceptionsBag, 0, validRangeEndIndex - validRangeStartIndex + 1);

                int firstEmptySlot = validRangeEndIndex - validRangeStartIndex + 1;

                // Clear the rest of the array
                Array.Clear(ecsExceptionsBag, firstEmptySlot, ecsExceptionsBag.Length - firstEmptySlot);

                // Write to the first available slot
                ecsExceptionsBag[firstEmptySlot] = new ExceptionEntry { Time = time, Exception = exception };

                return ISystemGroupExceptionHandler.Action.Continue;
            }
        }

        private struct ExceptionEntry
        {
            public long Time;
            public Exception Exception;
        }
    }
}
