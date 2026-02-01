// DCLTask is designed as WebGL / Desktop friendly
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
        public static async UniTask RunOnThreadPool(
                Func<UniTask> action,
                bool configureAwait = true,
                CancellationToken cancellationToken = default)
        {
            await action();
        }
#else
        public static async UniTask RunOnThreadPool(
                Func<UniTask> action,
                bool configureAwait = true,
                CancellationToken cancellationToken = default) =>
            UniTask.RunOnThreadPool(action, configureAwait, cancellationToken);
#endif
    }
}
