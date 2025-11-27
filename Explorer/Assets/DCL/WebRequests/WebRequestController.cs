using CDPBridges;
using Cysharp.Threading.Tasks;
using DCL.Web3.Identities;
using DCL.WebRequests.Analytics;
using DCL.WebRequests.ChromeDevtool;
using DCL.WebRequests.RequestsHub;
using System;
using System.Collections.Generic;
using UnityEngine.Networking;
using Utility.Multithreading;

namespace DCL.WebRequests
{
    public class WebRequestController : IWebRequestController
    {
        private readonly IWebRequestsAnalyticsContainer analyticsContainer;
        private readonly IWeb3IdentityCache web3IdentityCache;
        private readonly IRequestHub requestHub;
        private readonly ChromeDevtoolProtocolClient chromeDevtoolProtocolClient;

        IRequestHub IWebRequestController.requestHub => requestHub;

        public WebRequestController(
            IWebRequestsAnalyticsContainer analyticsContainer,
            IWeb3IdentityCache web3IdentityCache,
            IRequestHub requestHub,
            ChromeDevtoolProtocolClient chromeDevtoolProtocolClient
        )
        {
            this.analyticsContainer = analyticsContainer;
            this.web3IdentityCache = web3IdentityCache;
            this.requestHub = requestHub;
            this.chromeDevtoolProtocolClient = chromeDevtoolProtocolClient;
        }

        public async UniTask<TResult?> SendAsync<TWebRequest, TWebRequestArgs, TWebRequestOp, TResult>(RequestEnvelope<TWebRequest, TWebRequestArgs> envelope, TWebRequestOp op)
            where TWebRequestArgs: struct
            where TWebRequest: struct, ITypedWebRequest
            where TWebRequestOp: IWebRequestOp<TWebRequest, TResult>
        {
            await using ExecuteOnMainThreadScope scope = await ExecuteOnMainThreadScope.NewScopeWithReturnOnOriginalThreadAsync();

            // ensure disposal of headersInfo
            using RequestEnvelope<TWebRequest, TWebRequestArgs> _ = envelope;

                TWebRequest request = envelope.InitializedWebRequest(web3IdentityCache);

                // No matter what we must release UnityWebRequest, otherwise it crashes in the destructor
                using UnityWebRequest wr = request.UnityWebRequest;

                try
                {
                    using var pooledHeaders = envelope.Headers(out Dictionary<string, string> headers);
                    string method = request.UnityWebRequest.method!;
                    NotifyWebRequestScope? notifyScope = chromeDevtoolProtocolClient.Status is BridgeStatus.HasListeners
                        ? chromeDevtoolProtocolClient.NotifyWebRequestStart(envelope.CommonArguments.URL.Value, method, headers)
                        : null;

                    try
                    {
                        await request.WithAnalyticsAsync(analyticsContainer, request.SendRequest(envelope.Ct));

                        if (notifyScope.HasValue)
                        {
                            int statusCode = (int)request.UnityWebRequest.responseCode;

                            // TODO avoid allocation?
                            Dictionary<string, string>? responseHeaders = request.UnityWebRequest.GetResponseHeaders();

                            string mimeType = request.UnityWebRequest.GetRequestHeader("Content-Type") ?? "application/octet-stream";
                            int encodedDataLength = (int)request.UnityWebRequest.downloadedBytes;
                            notifyScope.Value.NotifyFinishAsync(statusCode, responseHeaders, mimeType, encodedDataLength).Forget();
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        notifyScope?.NotifyFailed("Cancelled", true);
                        throw;
                    }
                    catch
                    {
                        notifyScope?.NotifyFailed(request.UnityWebRequest.error!, false);
                        throw;
                    }

                    // if no exception is thrown Request is successful and the continuation op can be executed
                    return await op.ExecuteAsync(request, envelope.Ct);

                    // After the operation is executed, the flow may continue in the background thread
                }
                catch (UnityWebRequestException exception)
                {
                    // No result can be concluded in this case
                    if (envelope.ShouldIgnoreResponseError(exception.UnityWebRequest!))
                        return default(TResult);

                    throw;
                }
        }
    }

}
