using Arch.SystemGroups;
using Diagnostics.ReportsHandling;
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
        private const int ECS_EXCEPTIONS_PER_MINUTE_TOLERANCE = 3;

        private static readonly ThreadSafeObjectPool<SceneExceptionsHandler> POOL = new (() => new SceneExceptionsHandler(), defaultCapacity: PoolConstants.SCENES_COUNT);

        private readonly ExceptionEntry?[] ecsExceptionsBag = new ExceptionEntry?[ECS_EXCEPTIONS_PER_MINUTE_TOLERANCE];

        private ISceneStateProvider sceneState;

        private SceneExceptionsHandler() { }

        public ISystemGroupExceptionHandler.Action Handle(Exception exception, Type systemGroupType)
        {
            const float INTERVAL = 60;

            // Report exception
            if (exception is EcsSystemException ecsSystemException)
                ReportHub.LogException(ecsSystemException);
            else
                ReportHub.LogException(exception, new ReportData(ReportCategory.ECS));

            float time = Time.realtimeSinceStartup;

            int validIndex = -1;
            int i;

            for (i = 0; i < ecsExceptionsBag.Length; i++)
            {
                ExceptionEntry? e = ecsExceptionsBag[i];

                // The first available slot
                if (!e.HasValue)
                    break;

                // Detect invalid exceptions
                if (time - e.Value.Time < INTERVAL)
                    validIndex = i;
            }

            // All tolerance is used
            if (validIndex == ecsExceptionsBag.Length - 1)
            {
                // log an aggregated exception
                ReportHub.LogException(new AggregateException(ecsExceptionsBag.Select(e => e.Value.Exception).Append(exception)), new ReportData(ReportCategory.ECS));

                // Put the scene into the error state
                sceneState.State = SceneState.EcsError;
                return ISystemGroupExceptionHandler.Action.Suspend;
            }

            // Shift the array to the left
            var elementsCopied = 0;

            if (validIndex > -1)
                Array.Copy(ecsExceptionsBag, validIndex, ecsExceptionsBag, 0, elementsCopied = ecsExceptionsBag.Length - validIndex);

            // Clear the rest of the array
            Array.Clear(ecsExceptionsBag, elementsCopied, ecsExceptionsBag.Length - elementsCopied);

            // Write to the first available slot
            ecsExceptionsBag[elementsCopied] = new ExceptionEntry { Time = time, Exception = exception };
            return ISystemGroupExceptionHandler.Action.Continue;
        }

        public void OnEngineException(Exception exception, string category)
        {
            sceneState.State = SceneState.EngineError;
            ReportHub.LogException(exception, new ReportData(category));
        }

        public void OnJavaScriptException(string message)
        {
            // For javascript no tolerance
            // TODO Log a proper exception
            sceneState.State = SceneState.JavaScriptError;
        }

        public void Dispose()
        {
            sceneState = null;
            Array.Clear(ecsExceptionsBag, 0, ecsExceptionsBag.Length);
            POOL.Release(this);
        }

        public static SceneExceptionsHandler Create(ISceneStateProvider sceneState)
        {
            SceneExceptionsHandler handler = POOL.Get();
            handler.sceneState = sceneState;
            return handler;
        }

        private struct ExceptionEntry
        {
            public float Time;
            public Exception Exception;
        }
    }
}
