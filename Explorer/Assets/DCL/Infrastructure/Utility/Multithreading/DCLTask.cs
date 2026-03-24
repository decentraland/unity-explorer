// DCLTask is designed as WebGL / Desktop friendly
using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Utility.Multithreading
{
    /// <summary>
    ///     WebGL-compatible replacement for thread-pool dispatch helpers.
    ///     On non-WebGL platforms <see cref="SwitchToThreadPool" />, <see cref="RunOnThreadPool" />,
    ///     and <see cref="RunOnThreadPool{T}" /> forward
    ///     to the real UniTask thread-pool APIs so work executes off the main thread.
    ///     On WebGL (single-threaded) all methods are no-ops that complete immediately on the calling context,
    ///     since the browser has no OS thread pool.
    /// </summary>
    public static class DCLTask
    {
#if UNITY_WEBGL
        public static UniTask SwitchToThreadPool() =>
            UniTask.CompletedTask;
#else
        public static SwitchToThreadPoolAwaitable SwitchToThreadPool() =>
            new ();
#endif

#if UNITY_WEBGL
        public static async UniTask RunOnThreadPool(
            Func<UniTask> action,
            bool configureAwait = true,
            CancellationToken cancellationToken = default)
        {
            await action();
        }
#else
        public static UniTask RunOnThreadPool(
            Func<UniTask> action,
            bool configureAwait = true,
            CancellationToken cancellationToken = default) =>
            UniTask.RunOnThreadPool(action, configureAwait, cancellationToken);
#endif

#if UNITY_WEBGL
        public static async UniTask<T> RunOnThreadPool<T>(
            Func<T> action,
            bool configureAwait = true,
            CancellationToken cancellationToken = default)
        {
            return action();
        }
#else
        public static UniTask<T> RunOnThreadPool<T>(
            Func<T> action,
            bool configureAwait = true,
            CancellationToken cancellationToken = default) =>
            UniTask.RunOnThreadPool(action, configureAwait, cancellationToken);
#endif
    }
}
