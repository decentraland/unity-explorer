using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.WebRequests;
using System.Threading;

namespace DCL.SDKComponents.MediaStream
{
    public class OpenMediaPromise
    {
        internal enum Status
        {
            Pending, Resolved, Consumed,
        }

        internal Status status;

        // TODO (Vit): add caching mechanism for resolved promises: <url, isReachable> (on some upper level)
        internal string url;
        internal bool isReachable;

        public bool IsResolved => status == Status.Resolved;

        public async UniTask UrlReachabilityResolveAsync(IWebRequestController webRequestController, string url, ReportData reportData, CancellationToken ct)
        {
            status = Status.Pending;
            isReachable = false;
            this.url = url;

            isReachable = await webRequestController.IsHeadReachableAsync(reportData, URLAddress.FromString(this.url), ct);
            if (!isReachable)
                isReachable = await webRequestController.IsGetReachableAsync(reportData, URLAddress.FromString(this.url), ct);

            ReportHub.Log(ReportCategory.MEDIA_STREAM, $"Resource <{url}> isReachable = <{isReachable}>");

            status = Status.Resolved;
        }

        public bool IsReachableConsume(string url)
        {
            status = Status.Consumed;

            if (this.url != url)
            {
                ReportHub.LogWarning(ReportCategory.MEDIA_STREAM, $"Try to consume different url - wanted <{url}>, but was <{this.url}>");
                return false;
            }

            if (!isReachable)
            {
                ReportHub.LogWarning(ReportCategory.MEDIA_STREAM, $"Try to consume not reachable URL <{this.url}>");
                return false;
            }

            return true;
        }
    }
}
