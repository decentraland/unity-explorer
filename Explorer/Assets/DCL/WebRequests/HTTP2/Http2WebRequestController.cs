using Best.HTTP;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Web3.Authenticators;
using DCL.Web3.Identities;
using DCL.WebRequests.RequestsHub;
using System;
using System.Threading;
using Utility.Multithreading;

namespace DCL.WebRequests.HTTP2
{
    public class Http2WebRequestController : IWebRequestController
    {
        private readonly IWeb3IdentityCache identityCache;

        public Http2WebRequestController(IWeb3IdentityCache identityCache)
        {
            this.identityCache = identityCache;
        }

        public async UniTask<IWebRequest> SendAsync(ITypedWebRequest requestWrap, CancellationToken ct)
        {
            await using ExecuteOnMainThreadScope scope = await ExecuteOnMainThreadScope.NewScopeWithReturnOnOriginalThreadAsync();

            RequestEnvelope envelope = requestWrap.Envelope;

            // TODO proper disposal
            using ITypedWebRequest _ = requestWrap;
            HTTPRequest nativeRequest = requestWrap.CreateHttp2Request();

            var requestAdapter = new Http2WebRequest(nativeRequest);

            envelope.InitializedWebRequest(identityCache, requestAdapter);
            nativeRequest.RetrySettings.MaxRetries = envelope.CommonArguments.TotalAttempts();

            try { await nativeRequest.GetHTTPResponseAsync(ct); }
            catch (AsyncHTTPException exception)
            {
                if (!envelope.SuppressErrors)

                    // Print verbose
                    ReportHub.LogError(
                        envelope.ReportData,
                        $"Exception occured on loading {requestWrap.GetType().Name} from {envelope.CommonArguments.URL} with args {requestWrap.ArgsToString()},\n with {envelope}\n"
                    );

                // TODO convert into a common exception
            }

            return requestAdapter;
        }

        IRequestHub IWebRequestController.requestHub => throw new NotImplementedException();
    }
}
