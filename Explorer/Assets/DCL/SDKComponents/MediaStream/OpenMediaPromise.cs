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
            Pending,
            Resolved,
            Consumed,
        }

        internal Status status;

        // TODO (Vit): add caching mechanism for resolved promises: <url, isReachable> (on some upper level)
        internal MediaAddress mediaAddress;
        internal bool isReachable;

        public bool IsResolved => status == Status.Resolved;

        public async UniTask UrlReachabilityResolveAsync(IWebRequestController webRequestController, MediaAddress newMediaAddress, ReportData reportData, CancellationToken ct)
        {
            status = Status.Pending;
            isReachable = false;
            this.mediaAddress = newMediaAddress;

            if (mediaAddress.MediaKind is MediaAddress.Kind.LIVEKIT)
            {
                isReachable = true;
                status = Status.Resolved;
                return;
            }

            string url = mediaAddress.Url;
            isReachable = await webRequestController.IsHeadReachableAsync(reportData, URLAddress.FromString(url), ct);
            //This is needed because some servers might not handle HEAD requests correctly and return 404 errors, even thou they are perfectly
            if (!isReachable)
                isReachable = await webRequestController.IsGetReachableAsync(reportData, URLAddress.FromString(url), ct);

            ReportHub.Log(ReportCategory.MEDIA_STREAM, $"Resource <{url}> isReachable = <{isReachable}>");

            status = Status.Resolved;
        }

        public bool IsReachableConsume(MediaAddress address)
        {
            status = Status.Consumed;

            if (this.mediaAddress != address)
            {
                ReportHub.LogWarning(ReportCategory.MEDIA_STREAM, $"Try to consume different url - wanted <{address}>, but was <{this.mediaAddress}>");
                return false;
            }

            if (!isReachable)
            {
                ReportHub.LogWarning(ReportCategory.MEDIA_STREAM, $"Try to consume not reachable URL <{this.mediaAddress}>");
                return false;
            }

            return true;
        }
    }
}
