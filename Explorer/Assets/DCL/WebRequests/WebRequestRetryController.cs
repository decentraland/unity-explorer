using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.WebRequests.RequestsHub;
using Sentry;
using System;
using System.Text;
using System.Threading;

namespace DCL.WebRequests
{
    public class WebRequestRetryController : IWebRequestController
    {
        private readonly IWebRequestController origin;

        public WebRequestRetryController(IWebRequestController origin)
        {
            this.origin = origin;
        }

        private static readonly ThreadLocal<StringBuilder> BREADCRUMB_BUILDER = new (() => new StringBuilder(150));

        public async UniTask<TResult?> SendAsync<TWebRequest, TWebRequestArgs, TWebRequestOp, TResult>(RequestEnvelope<TWebRequest, TWebRequestArgs> envelope, TWebRequestOp op) where TWebRequest: struct, ITypedWebRequest where TWebRequestArgs: struct where TWebRequestOp: IWebRequestOp<TWebRequest, TResult>
        {
            var attemptNumber = 0;
            RetryPolicy retryPolicy = envelope.CommonArguments.RetryPolicy;

            while (true)
            {
                try
                {
                    attemptNumber++;
                    return await origin.SendAsync<TWebRequest, TWebRequestArgs, TWebRequestOp, TResult>(envelope, op);
                }
                catch (UnityWebRequestException exception)
                {
                    if (!envelope.SuppressErrors)

                        // Print verbose
                        ReportHub.LogError(
                            envelope.ReportData,
                            $"Exception (code {exception.ResponseCode}) occured on loading {typeof(TWebRequest).Name} from {envelope.CommonArguments.URL} with {envelope}\n"
                            + $"Attempt: {attemptNumber}/{retryPolicy.maxRetriesCount + 1}"
                        );

                    (bool canBeRepeated, TimeSpan retryDelay) = WebRequestUtils.CanBeRepeated(attemptNumber, retryPolicy, envelope.IsIdempotent(), exception);

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

        IRequestHub IWebRequestController.requestHub => origin.requestHub;
    }
}
