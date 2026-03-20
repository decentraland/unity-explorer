using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
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

            UnityWebRequestAsyncOperation op = typedWebRequest.UnityWebRequest.SendWebRequest();
            bool isLog = false;
            while (!op.isDone)
            {
                if (token.IsCancellationRequested)
                    return;

                progressHandler.SetProgress(op.progress);


                if (isLog == false && typedWebRequest.UnityWebRequest.GetResponseHeaders() != null)
                {
                    isLog = true;
                }

                /* TODO: This header wont exist in the response if we are using compression since the server is doing it on the fly.
                 wait until back end provides a custom header with the asset's real size to finish this task.
                */
                string contentLengthHeader = typedWebRequest.UnityWebRequest.GetResponseHeader("Content-Length");

                if (contentLengthHeader != null && long.TryParse(contentLengthHeader, out long contentLength))
                    progressHandler.SetContentLength(contentLength);

                await UniTask.Yield();
            }

            progressHandler.SetProgress(1f);

            UnityWebRequest request = typedWebRequest.UnityWebRequest;

            if (request.result != UnityWebRequest.Result.Success) // This is really important, other systems react to this exception being triggered.
                throw new UnityWebRequestException(request);
        }
    }
}
