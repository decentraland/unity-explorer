using Cysharp.Threading.Tasks;
using System;
using Utility.Multithreading;

namespace SceneRuntime.Apis
{
    public static class Extensions
    {
        /// <summary>
        ///     Disconnects from the main thread so it does not wait for the async task to complete
        ///     as it can lead to accidental synchronization by the main thread and to a deadlock subsequently.
        /// </summary>
        public static object ToDisconnectedPromise<T>(this UniTask<T> uniTask, JsApiWrapper api)
        {
            if (api.engine == null)
                throw new InvalidOperationException("JavaScript engine is not available on JsApiWrapper");

            var completionSource = new UniTaskCompletionSource<T>();

            DCLTask.RunOnThreadPool(async () =>
                    {
                        try
                        {
                            T result = await uniTask;

                            if (PlayerLoopHelper.IsMainThread)
                                await DCLTask.SwitchToThreadPool();

                            if (api.disposeCts.IsCancellationRequested)
                                return;

                            completionSource.TrySetResult(result);
                        }
                        catch (Exception e)
                        {
                            if (PlayerLoopHelper.IsMainThread)
                                await DCLTask.SwitchToThreadPool();

                            if (api.disposeCts.IsCancellationRequested)
                                return;

                            completionSource.TrySetException(e);
                        }
                    })
                   .Forget();

            return JSPromiseConverter.ToPromise(completionSource.Task.AsTask(), api.engine);
        }

        /// <summary>
        ///     <inheritdoc cref="ToDisconnectedPromise{T}" />
        /// </summary>
        public static object ToDisconnectedPromise(this UniTask uniTask, JsApiWrapper api)
        {
            if (api.engine == null)
                throw new InvalidOperationException("JavaScript engine is not available on JsApiWrapper");

            var completionSource = new UniTaskCompletionSource();

            DCLTask.RunOnThreadPool(async () =>
                    {
                        try
                        {
                            await uniTask;

                            if (PlayerLoopHelper.IsMainThread)
                                await DCLTask.SwitchToThreadPool();

                            if (api.disposeCts.IsCancellationRequested)
                                return;

                            completionSource.TrySetResult();
                        }
                        catch (Exception e)
                        {
                            if (PlayerLoopHelper.IsMainThread)
                                await DCLTask.SwitchToThreadPool();

                            if (api.disposeCts.IsCancellationRequested)
                                return;

                            completionSource.TrySetException(e);
                        }
                    })
                   .Forget();

            return JSPromiseConverter.ToPromise(completionSource.Task.AsTask(), api.engine);
        }
    }
}
