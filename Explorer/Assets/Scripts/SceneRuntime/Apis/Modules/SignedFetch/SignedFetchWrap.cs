using Cysharp.Threading.Tasks;
using DCL.WebRequests;
using JetBrains.Annotations;
using SceneRuntime.Apis.Modules.SignedFetch.Messages;
using System;
using System.Threading;
using UnityEngine.Networking;

namespace SceneRuntime.Apis.Modules.SignedFetch
{
    public class SignedFetchWrap : IDisposable
    {
        private readonly IWebRequestController webController;
        private readonly CancellationTokenSource cancellationTokenSource = new ();

        public SignedFetchWrap(IWebRequestController webController)
        {
            this.webController = webController;
        }

        [UsedImplicitly]
        public object Headers(SignedFetchRequest signedFetchRequest)
        {
            string jsonMetaData = signedFetchRequest.init?.body ?? string.Empty;
            return WebRequestControllerExtensions.Headers(jsonMetaData).AsMutableDictionary();
        }

        [UsedImplicitly]
        public object SignedFetch(string url, string? jsonMetaData)
        {
            async UniTask<GenericPostRequest> ExecuteAsync()
            {
                var result = await webController
                   .SignedFetchAsync(url, jsonMetaData ?? string.Empty, cancellationTokenSource.Token);

                if (result.UnityWebRequest.result is not UnityWebRequest.Result.Success)
                    throw new Exception($"Failed to fetch {url} with status code {result.UnityWebRequest.responseCode}");

                return result;
            }

            return ExecuteAsync().ToPromise();
        }

        public void Dispose()
        {
            cancellationTokenSource.Dispose();
        }
    }
}
