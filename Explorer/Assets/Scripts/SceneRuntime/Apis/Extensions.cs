using Cysharp.Threading.Tasks;
using Microsoft.ClearScript.JavaScript;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SceneRuntime.Apis
{
    public static class Extensions
    {
        private static object ToPromise<T>(this UniTask<T> uniTask) =>
            uniTask.AsTask()!.ToPromise()!;

        /// <summary>
        ///     Disconnects from the main thread so it does not wait for the async task to complete
        ///     as it can lead to accidental synchronization by the main thread and to a dead lock subsequently.
        /// </summary>
        public static object ToDisconnectedPromise<T>(this UniTask<T> uniTask)
        {
            var completionSource = new UniTaskCompletionSource<T>();

            UniTask.RunOnThreadPool(async () =>
                    {
                        try
                        {
                            T result = await uniTask;
                            await UniTask.SwitchToThreadPool();
                            completionSource.TrySetResult(result);
                        }
                        catch (Exception e)
                        {
                            await UniTask.SwitchToThreadPool();
                            completionSource.TrySetException(e);
                        }
                    })
                   .Forget();

            return completionSource.Task.ToPromise();
        }

        /// <summary>
        ///     <inheritdoc cref="ToDisconnectedPromise{T}" />
        /// </summary>
        public static object ToDisconnectedPromise(this UniTask uniTask)
        {
            var completionSource = new UniTaskCompletionSource();

            UniTask.RunOnThreadPool(async () =>
                    {
                        try
                        {
                            await uniTask;
                            await UniTask.SwitchToThreadPool();
                            completionSource.TrySetResult();
                        }
                        catch (Exception e)
                        {
                            await UniTask.SwitchToThreadPool();
                            completionSource.TrySetException(e);
                        }
                    })
                   .Forget();

            return completionSource.Task.AsTask().ToPromise();
        }
    }
}
