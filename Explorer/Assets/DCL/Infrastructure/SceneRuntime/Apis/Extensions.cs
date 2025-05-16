using Cysharp.Threading.Tasks;
using Microsoft.ClearScript.JavaScript;
using System;

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
        public static object ToDisconnectedPromise<T>(this UniTask<T> uniTask, JsApiWrapper api)
        {
            var completionSource = new UniTaskCompletionSource<T>();

            UniTask.RunOnThreadPool(async () =>
                    {
                        try
                        {
                            T result = await uniTask;

                            if (PlayerLoopHelper.IsMainThread)
                                await UniTask.SwitchToThreadPool();

                            if (api.disposeCts.IsCancellationRequested)
                                return;

                            completionSource.TrySetResult(result);
                        }
                        catch (Exception e)
                        {
                            if (PlayerLoopHelper.IsMainThread)
                                await UniTask.SwitchToThreadPool();

                            if (api.disposeCts.IsCancellationRequested)
                                return;

                            completionSource.TrySetException(e);
                        }
                    })
                   .Forget();

            return completionSource.Task.ToPromise();
        }

        /// <summary>
        ///     <inheritdoc cref="ToDisconnectedPromise{T}" />
        /// </summary>
        public static object ToDisconnectedPromise(this UniTask uniTask, JsApiWrapper api)
        {
            var completionSource = new UniTaskCompletionSource();

            UniTask.RunOnThreadPool(async () =>
                    {
                        try
                        {
                            await uniTask;

                            if (PlayerLoopHelper.IsMainThread)
                                await UniTask.SwitchToThreadPool();

                            if (api.disposeCts.IsCancellationRequested)
                                return;

                            completionSource.TrySetResult();
                        }
                        catch (Exception e)
                        {
                            if (PlayerLoopHelper.IsMainThread)
                                await UniTask.SwitchToThreadPool();

                            if (api.disposeCts.IsCancellationRequested)
                                return;

                            completionSource.TrySetException(e);
                        }
                    })
                   .Forget();

            return completionSource.Task.AsTask().ToPromise();
        }
    }
}
