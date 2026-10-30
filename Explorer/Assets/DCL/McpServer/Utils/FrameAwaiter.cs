using Cysharp.Threading.Tasks;
using System;
using System.Collections;
using System.Threading;
using Utility;

namespace DCL.McpServer.Utils
{
    /// <summary>
    ///     End-of-frame awaits for tools whose work must happen once the frame is complete (UI included):
    ///     the back buffer is only whole at <c>WaitForEndOfFrame</c>, which UniTask cannot reach without a
    ///     MonoBehaviour, so a coroutine on the shared runner signals it.
    /// </summary>
    public static class FrameAwaiter
    {
        /// <summary>Completes at the end of the current frame.</summary>
        public static UniTask WaitForEndOfFrameAsync(ICoroutineRunner runner, CancellationToken ct)
        {
            var completion = new UniTaskCompletionSource();
            runner.StartCoroutine(SignalEndOfFrameCoroutine(completion));
            return completion.Task.AttachExternalCancellation(ct);
        }

        /// <summary>Runs <paramref name="atEndOfFrame" /> on the main thread at the end of the current frame and completes with its result.</summary>
        public static UniTask<T> RunAtEndOfFrameAsync<T>(ICoroutineRunner runner, Func<T> atEndOfFrame, CancellationToken ct)
        {
            var completion = new UniTaskCompletionSource<T>();
            runner.StartCoroutine(RunAtEndOfFrameCoroutine(completion, atEndOfFrame));
            return completion.Task.AttachExternalCancellation(ct);
        }

        private static IEnumerator SignalEndOfFrameCoroutine(UniTaskCompletionSource completion)
        {
            yield return GameObjectExtensions.WAIT_FOR_END_OF_FRAME;
            completion.TrySetResult();
        }

        private static IEnumerator RunAtEndOfFrameCoroutine<T>(UniTaskCompletionSource<T> completion, Func<T> atEndOfFrame)
        {
            yield return GameObjectExtensions.WAIT_FOR_END_OF_FRAME;

            T result;

            try { result = atEndOfFrame(); }
            catch (Exception e)
            {
                completion.TrySetException(e);
                yield break;
            }

            completion.TrySetResult(result);
        }
    }
}
