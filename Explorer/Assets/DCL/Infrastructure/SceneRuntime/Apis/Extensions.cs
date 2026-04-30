using Cysharp.Threading.Tasks;
using Microsoft.ClearScript.JavaScript;
using System;
using System.Threading;

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
            JsApiCompletionGate gate = api.completionGate!;
            CancellationToken ct = api.disposeCts.Token;

            UniTask.RunOnThreadPool(async () =>
                    {
                        // Hold the gate for the entire body. The disposer waits for the gate to
                        // drain before releasing V8, so TrySetResult / TrySetException — which
                        // synchronously inline ClearScript's CompletePromise back into V8 —
                        // are guaranteed to run against a live engine.
                        if (!gate.TryEnter())
                            return;

                        try
                        {
                            T result = await uniTask.AttachExternalCancellation(gate.FinalizationGraceCt);

                            if (PlayerLoopHelper.IsMainThread)
                                await UniTask.SwitchToThreadPool();

                            if (ct.IsCancellationRequested)
                                return;

                            completionSource.TrySetResult(result);
                        }
                        catch (Exception e)
                        {
                            if (PlayerLoopHelper.IsMainThread)
                                await UniTask.SwitchToThreadPool();

                            if (ct.IsCancellationRequested)
                                return;

                            completionSource.TrySetException(e);
                        }
                        finally { gate.Exit(); }
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
            JsApiCompletionGate gate = api.completionGate!;
            CancellationToken ct = api.disposeCts.Token;

            UniTask.RunOnThreadPool(async () =>
                    {
                        if (!gate.TryEnter())
                            return;

                        try
                        {
                            await uniTask.AttachExternalCancellation(gate.FinalizationGraceCt);

                            if (PlayerLoopHelper.IsMainThread)
                                await UniTask.SwitchToThreadPool();

                            if (ct.IsCancellationRequested)
                                return;

                            completionSource.TrySetResult();
                        }
                        catch (Exception e)
                        {
                            if (PlayerLoopHelper.IsMainThread)
                                await UniTask.SwitchToThreadPool();

                            if (ct.IsCancellationRequested)
                                return;

                            completionSource.TrySetException(e);
                        }
                        finally { gate.Exit(); }
                    })
                   .Forget();

            return completionSource.Task.AsTask().ToPromise();
        }
    }
}
