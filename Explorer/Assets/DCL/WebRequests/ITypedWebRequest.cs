using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;
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
                await typedWebRequest.UnityWebRequest.SendWebRequest().WithCancellation(token);
                return;
            }

            using var headTimeoutCts = CancellationTokenSource.CreateLinkedTokenSource(token);
            headTimeoutCts.CancelAfter(500);
            var contentLength = await FetchContentLengthAsync(typedWebRequest.UnityWebRequest.url, headTimeoutCts.Token);

            progressHandler.SetContentLength(contentLength);
            UnityWebRequestAsyncOperation op = typedWebRequest.UnityWebRequest.SendWebRequest();

            while (!op.isDone)
            {
                if (token.IsCancellationRequested)
                {
                    return;
                }

                if (contentLength != -1)
                    progressHandler.SetProgress(op.webRequest.downloadedBytes / (float)contentLength);
                else
                    progressHandler.SetProgress(op.progress); // Unreliable for on-the-fly compressed responses, but best-effort fallback

                await UniTask.Yield();
            }

            progressHandler.SetProgress(1f);

            UnityWebRequest request = typedWebRequest.UnityWebRequest;

            if (request.result != UnityWebRequest.Result.Success) // This is really important. Other systems react to this exception being triggered.
                throw new UnityWebRequestException(request);
        }

        private static async UniTask<long> FetchContentLengthAsync(string url, CancellationToken ct)
        {
            const string DECOMPRESSED_CONTENT_LENGTH_HEADER = "x-decompressed-content-length";

            if (url.StartsWith("file://"))
                return -1;

            using var headRequest = UnityWebRequest.Head(url);

            try
            {
                await headRequest.SendWebRequest().WithCancellation(ct);

                if (headRequest.result != UnityWebRequest.Result.Success)
                    return -1;

                string header = headRequest.GetResponseHeader(DECOMPRESSED_CONTENT_LENGTH_HEADER);

                if (header != null && long.TryParse(header, out long contentLength))
                    return contentLength;
            }
            catch (OperationCanceledException)
            {
                // Timeout expired — proceed without content length
            }

            return -1;
        }
    }
}
