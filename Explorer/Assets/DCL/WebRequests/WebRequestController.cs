using CDPBridges;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Web3.Identities;
using DCL.WebRequests.Analytics;
using DCL.WebRequests.ChromeDevtool;
using DCL.WebRequests.RequestsHub;
using Sentry;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using UnityEngine.Networking;
using Utility.Multithreading;

namespace DCL.WebRequests
{
    public class WebRequestController : IWebRequestController
    {
        private static readonly ThreadLocal<StringBuilder> BREADCRUMB_BUILDER = new (() => new StringBuilder(150));
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

            RetryPolicy retryPolicy = envelope.CommonArguments.RetryPolicy;
            var attemptNumber = 0;

            // ensure disposal of headersInfo
            using RequestEnvelope<TWebRequest, TWebRequestArgs> _ = envelope;

            while (true)
            {
                TWebRequest request = envelope.InitializedWebRequest(web3IdentityCache);
                bool idempotent = request.IsIdempotent(envelope.signInfo);

                // No matter what we must release UnityWebRequest, otherwise it crashes in the destructor
                using UnityWebRequest wr = request.UnityWebRequest;

                try
                {
                    attemptNumber++;

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

                    if (!envelope.SuppressErrors)

                        // Print verbose
                        ReportHub.LogError(
                            envelope.ReportData,
                            $"Exception (code {exception.ResponseCode}) occured on loading {typeof(TWebRequest).Name} from {envelope.CommonArguments.URL} with {envelope}\n"
                            + $"Attempt: {attemptNumber}/{retryPolicy.maxRetriesCount + 1}"
                        );

                    (bool canBeRepeated, TimeSpan retryDelay) = WebRequestUtils.CanBeRepeated(attemptNumber, retryPolicy, idempotent, exception);

                    if (!canBeRepeated && !envelope.IgnoreIrrecoverableErrors)
                    {
                        // Ignore the file error as we always try to read from the file first
                        if (!envelope.CommonArguments.URL.Value.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
                            SentrySdk.AddBreadcrumb($"{envelope.ReportData.Category}: Irrecoverable exception (code {exception.ResponseCode}) occured on executing {envelope.GetBreadcrumbString(BREADCRUMB_BUILDER.Value)}", level: BreadcrumbLevel.Info);

                        throw;
                    }

                    await UniTask.Delay(retryDelay, DelayType.Realtime, cancellationToken: envelope.Ct);
                }
            }
        }
    }
}
