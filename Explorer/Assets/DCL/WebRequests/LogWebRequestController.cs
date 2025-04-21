using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.WebRequests.RequestsHub;
using System;
using System.Threading;

namespace DCL.WebRequests
{
    public class LogWebRequestController : IWebRequestController
    {
        private readonly IWebRequestController origin;

        public LogWebRequestController(IWebRequestController origin)
        {
            this.origin = origin;
        }

        IRequestHub IWebRequestController.requestHub => origin.requestHub;

        public async UniTask<IWebRequest> SendAsync(ITypedWebRequest requestWrap, bool detachDownloadHandler, CancellationToken ct)
        {
            RequestEnvelope envelope = requestWrap.Envelope;

            try
            {
                ReportHub.Log(envelope.ReportData, $"WebRequestController send start: {envelope}");
                IWebRequest? result = await origin.SendAsync(requestWrap, detachDownloadHandler, ct);
                ReportHub.Log(envelope.ReportData, $"WebRequestController send finish: {envelope}");
                return result;
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                ReportHub.Log(envelope.ReportData, $"WebRequestController send error: {e}");
                throw; // don't re-throw it as a new exception as we loose the original type in that case
            }
        }

        public async UniTask<PartialDownloadStream> GetPartialAsync(CommonArguments commonArguments, ReportData reportData, PartialDownloadArguments partialArgs, CancellationToken ct, WebRequestHeadersInfo? headersInfo = null)
        {
            try
            {
                ReportHub.Log(reportData, $"WebRequestController {nameof(GetPartialAsync)} start: {commonArguments}");
                PartialDownloadStream? result = await origin.GetPartialAsync(commonArguments, reportData, partialArgs, ct, headersInfo);
                ReportHub.Log(reportData, $"WebRequestController {nameof(GetPartialAsync)} finish: {commonArguments}");
                return result;
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                ReportHub.Log(reportData, $"WebRequestController {nameof(GetPartialAsync)} error: {e}");
                throw; // don't re-throw it as a new exception as we loose the original type in that case
            }
        }
    }
}
