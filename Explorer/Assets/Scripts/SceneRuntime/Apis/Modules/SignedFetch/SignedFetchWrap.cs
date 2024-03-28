using DCL.WebRequests;
using JetBrains.Annotations;
using SceneRuntime.Apis.Modules.SignedFetch.Messages;
using System;
using System.Threading;

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
        public object SignedFetch(string url, string jsonMetaData) =>
            webController
               .SignedFetchAsync(url, jsonMetaData, cancellationTokenSource.Token)
               .ToPromise(); //TODO headers support

        [UsedImplicitly]
        public object SignedFetch(SignedFetchRequest signedFetchRequest) =>
            SignedFetch(signedFetchRequest.url, signedFetchRequest.init?.body ?? string.Empty);

        public void Dispose()
        {
            cancellationTokenSource.Dispose();
        }
    }
}
