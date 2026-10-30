using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine.Networking;

namespace DCL.WebRequests
{
    /// <summary>
    ///     This interface is used as a constraint for generics and should not be referenced directly
    /// </summary>
    public interface ITypedWebRequest
    {
        UnityWebRequest UnityWebRequest { get; }

        /// <summary>
        ///     Idempotent requests are those that can be safely retried without changing the result.
        /// </summary>
        bool Idempotent { get; }
    }

    public static class TypedWebRequestExtensions
    {
        public static async UniTask SendRequestAsync<T>(this T typedWebRequest, CancellationToken token, long expectedContentLength = -1, IProgress<float>? progressReporter = null) where T: ITypedWebRequest
        {
            if (progressReporter == null)
            {
                // WithCancellation has a special overload for UnityWebRequestAsyncOperation
                // that checks request.result and throws UnityWebRequestException on failure automatically.
                await typedWebRequest.UnityWebRequest.SendWebRequest().WithCancellation(token);
                return;
            }

            UnityWebRequestAsyncOperation op = typedWebRequest.UnityWebRequest.SendWebRequest();

            while (!op.isDone)
            {
                if (expectedContentLength > 0)
                    progressReporter.Report(op.webRequest.downloadedBytes / (float)expectedContentLength);
                else
                    progressReporter.Report(op.progress); // Unreliable for on-the-fly compressed responses, but best-effort fallback

                // UniTask.Yield(token) throws OperationCanceledException on cancel, matching the non-reporting path.
                await UniTask.Yield(token);
            }

            progressReporter.Report(1f);

            // The manual polling loop above bypasses UniTask's automatic error handling,
            // so we must check the result and throw explicitly here.
            UnityWebRequest request = typedWebRequest.UnityWebRequest;

            if (request.result != UnityWebRequest.Result.Success) // This is really important. Other systems react to this exception being triggered.
                throw new UnityWebRequestException(request);
        }
    }
}
