using Arch.SystemGroups;
using DCL.Diagnostics;
using System;
using System.Linq;
using UnityEngine;
using Utility.Pool;
using Utility.ThreadSafePool;

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

        private ISceneStateProvider sceneState;
        private SceneShortInfo sceneShortInfo;

        private SceneExceptionsHandler() { }

        public void Dispose()
        {
            sceneState = null;
            Array.Clear(ecsExceptionsBag, 0, ecsExceptionsBag.Length);
            POOL.Release(this);
        }

        public ISystemGroupExceptionHandler.Action Handle(Exception exception, Type systemGroupType)
        {
            const float INTERVAL = 60;

            // Report exception
            if (exception is EcsSystemException ecsSystemException)
            {
                // Add scene information, we don't add this info in the BaseUnityLoopSystem as we would need to propagate it for all systems
                // and it's inconvenient and cumbersome
                ecsSystemException.reportData.SceneShortInfo = sceneShortInfo;

                ReportHub.LogException(ecsSystemException);
            }
            else
                ReportHub.LogException(exception, new ReportData(ReportCategory.ECS, sceneShortInfo: sceneShortInfo));

            float time = Time.realtimeSinceStartup;

            var validRangeStartIndex = 0;
            int validRangeEndIndex = -1;

            for (var i = 0; i < ecsExceptionsBag.Length; i++)
            {
                ExceptionEntry? e = ecsExceptionsBag[i];

                if (!e.HasValue)
                    break;

                // Detect invalid exceptions
                if (time - e.Value.Time < INTERVAL)
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
                ReportHub.LogException(new SceneExecutionException(ecsExceptionsBag.Select(e => e.Value.Exception).Append(exception), new ReportData(ReportCategory.ECS, sceneShortInfo: sceneShortInfo)));

                // Put the scene into the error state
                sceneState.State = SceneState.EcsError;
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

        public void OnEngineException(Exception exception, string category)
        {
            sceneState.State = SceneState.EngineError;
            ReportHub.LogException(exception, new ReportData(category, sceneShortInfo: sceneShortInfo));
        }

        public void OnJavaScriptException(string message)
        {
            // For javascript no tolerance
            sceneState.State = SceneState.JavaScriptError;

            ReportHub.LogException(new SceneExecutionException(new[] { new Exception(message) },
                new ReportData(ReportCategory.JAVASCRIPT, sceneShortInfo: sceneShortInfo)));
        }

        public static SceneExceptionsHandler Create(ISceneStateProvider sceneState, SceneShortInfo sceneShortInfo)
        {
            SceneExceptionsHandler handler = POOL.Get();
            handler.sceneState = sceneState;
            handler.sceneShortInfo = sceneShortInfo;
            return handler;
        }

        private struct ExceptionEntry
        {
            public float Time;
            public Exception Exception;
        }
    }
}
