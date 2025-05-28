using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.WebRequests;
using System.Threading;
using Utility.Types;

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

            if (mediaAddress.IsLivekitAddress(out _))
            {
                isReachable = true;
                status = Status.Resolved;
                return;
            }

            mediaAddress.IsUrlMediaAddress(out var urlMediaAddress);
            string url = urlMediaAddress!.Value.Url;

            Result result = await webRequestController.IsHeadReachableAsync(reportData, URLAddress.FromString(this.url), ct);
            isReachable = result.Success;
            //This is needed because some servers might not handle HEAD requests correctly and return 404 errors, even thou they are perfectly
            if (!isReachable)
                isReachable = await webRequestController.IsGetReachableAsync(reportData, URLAddress.FromString(this.url), ct);

            ReportHub.Log(ReportCategory.MEDIA_STREAM, $"Resource <{url}> isReachable = <{isReachable}>");

            status = Status.Resolved;
        }

        public bool IsReachableConsume(string url)
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
