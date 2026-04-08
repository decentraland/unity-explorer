using Cysharp.Threading.Tasks;
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
        public static async UniTask SendRequest<T>(this T typedWebRequest, CancellationToken token, IStreamableLoadingProgressHandler? progressHandler = null) where T: ITypedWebRequest
        {
            if (progressHandler == null)
            {
                // WithCancellation has a special overload for UnityWebRequestAsyncOperation
                // that checks request.result and throws UnityWebRequestException on failure automatically.
                await typedWebRequest.UnityWebRequest.SendWebRequest().WithCancellation(token);
                return;
            }

            long contentLength = progressHandler.ContentLength;
            UnityWebRequestAsyncOperation op = typedWebRequest.UnityWebRequest.SendWebRequest();

            while (!op.isDone)
            {
                if (token.IsCancellationRequested)
                    return;

                if (contentLength > 0)
                    progressHandler.SetProgress(op.webRequest.downloadedBytes / (float)contentLength);
                else
                    progressHandler.SetProgress(op.progress); // Unreliable for on-the-fly compressed responses, but best-effort fallback

                await UniTask.Yield();
            }

            progressHandler.SetProgress(1f);

            // The manual polling loop above bypasses UniTask's automatic error handling,
            // so we must check the result and throw explicitly here.
            UnityWebRequest request = typedWebRequest.UnityWebRequest;

            if (request.result != UnityWebRequest.Result.Success) // This is really important. Other systems react to this exception being triggered.
                throw new UnityWebRequestException(request);
        }
    }
}
