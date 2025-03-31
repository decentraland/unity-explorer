using Best.HTTP;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Web3.Identities;
using DCL.WebRequests.Analytics;
using DCL.WebRequests.RequestsHub;
using System.Threading;
using Utility.Multithreading;

namespace DCL.WebRequests.HTTP2
{
    public class Http2WebRequestController : IWebRequestController
    {
        private readonly IWeb3IdentityCache identityCache;
        private readonly IWebRequestsAnalyticsContainer analyticsContainer;
        private readonly IRequestHub requestHub;

        public Http2WebRequestController(IWebRequestsAnalyticsContainer analyticsContainer, IWeb3IdentityCache identityCache, IRequestHub requestHub)
        {
            this.identityCache = identityCache;
            this.analyticsContainer = analyticsContainer;
            this.requestHub = requestHub;
        }

        public async UniTask<IWebRequest> SendAsync(ITypedWebRequest requestWrap, CancellationToken ct)
        {
            await using ExecuteOnMainThreadScope scope = await ExecuteOnMainThreadScope.NewScopeWithReturnOnOriginalThreadAsync();

            RequestEnvelope envelope = requestWrap.Envelope;

            using ITypedWebRequest _ = requestWrap;
            HTTPRequest nativeRequest = requestWrap.CreateHttp2Request();

            var requestAdapter = new Http2WebRequest(nativeRequest);

            envelope.InitializedWebRequest(identityCache, requestAdapter);
            nativeRequest.RetrySettings.MaxRetries = envelope.CommonArguments.TotalAttempts();

            envelope.OnCreated?.Invoke(requestAdapter);

            try { await ExecuteWithAnalytics(requestWrap, requestAdapter, ct); }
            catch (AsyncHTTPException exception)
            {
                if (!envelope.SuppressErrors)

                    // Print verbose
                    ReportHub.LogError(
                        envelope.ReportData,
                        $"Exception occured on loading {requestWrap.GetType().Name} from {envelope.CommonArguments.URL} with args {requestWrap.ArgsToString()},\n with {envelope}\n"
                    );

                // Dispose adapter on exception asa it won't be returned to the caller
                requestAdapter.Dispose();

                // convert into a common exception
                throw new Http2WebRequestException(requestAdapter, exception);
            }

            return requestAdapter;
        }

        private async UniTask ExecuteWithAnalytics(ITypedWebRequest request, Http2WebRequest adapter, CancellationToken ct)
        {
            analyticsContainer.OnRequestStarted(request, adapter);

            try { await adapter.httpRequest.GetHTTPResponseAsync(ct); }
            finally { analyticsContainer.OnRequestFinished(request, adapter); }
        }

        IRequestHub IWebRequestController.requestHub => requestHub;
    }
}
