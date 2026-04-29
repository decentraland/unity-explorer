// DCLTask is designed as WebGL / Desktop friendly
// [IgnoreAsyncNaming]
using System;
using System.Threading;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace Utility.Multithreading
{
    public static class DCLTask
    {
#if UNITY_WEBGL
        public static UniTask SwitchToThreadPool() =>
            UniTask.CompletedTask;
#else
        public static SwitchToThreadPoolAwaitable SwitchToThreadPool()
        {
            return new SwitchToThreadPoolAwaitable();
        }
#endif


#if UNITY_WEBGL
        public static UniTask RunOnThreadPool(
                Func<UniTask> action,
                bool configureAwait = true,
                CancellationToken cancellationToken = default) =>
            action();
#else
        public static UniTask RunOnThreadPool(
                Func<UniTask> action,
                bool configureAwait = true,
                CancellationToken cancellationToken = default) =>
            UniTask.RunOnThreadPool(action, configureAwait, cancellationToken); // IGNORE_LINE_WEBGL_UNITASK_SAFETY_FLAG
#endif


#if UNITY_WEBGL
        public static UniTask<T> RunOnThreadPool<T>(
                Func<T> func,
                bool configureAwait = true,
                CancellationToken cancellationToken = default)
        {
            T result = func();
            return UniTask.FromResult<T>(result);
        }
#else
        public static UniTask<T> RunOnThreadPool<T>(
                Func<T> func,
                bool configureAwait = true,
                CancellationToken cancellationToken = default) =>
            UniTask.RunOnThreadPool(func, configureAwait, cancellationToken); // IGNORE_LINE_WEBGL_UNITASK_SAFETY_FLAG
#endif

        // Basically created for Explorer/Assets/DCL/Infrastructure/SceneRunner/SceneFacade.cs
#if UNITY_WEBGL
        public static UniTask Delay(
                int sleepMS,
                CancellationToken cancellationToken = default) =>
            UniTask.Delay(sleepMS, ct);
#else
        public static System.Threading.Tasks.Task Delay( // IGNORE_LINE_WEBGL_SYSTEM_TASKS_SAFETY_FLAG
                int sleepMS,
                CancellationToken cancellationToken = default) =>
            System.Threading.Tasks.Task.Delay(sleepMS, cancellationToken); // IGNORE_LINE_WEBGL_SYSTEM_TASKS_SAFETY_FLAG
#endif
        // end created for

#if UNITY_WEBGL
        public static UniTask RunOnThreadPool(
                Action action,
                bool configureAwait = true,
                CancellationToken cancellationToken = default)
        {
            action();
            return UniTask.CompletedTask;
        }
#else
        public static UniTask RunOnThreadPool(
                Action action,
                bool configureAwait = true,
                CancellationToken cancellationToken = default) =>
            UniTask.RunOnThreadPool(action, configureAwait, cancellationToken); // IGNORE_LINE_WEBGL_UNITASK_SAFETY_FLAG
#endif
    }
}
