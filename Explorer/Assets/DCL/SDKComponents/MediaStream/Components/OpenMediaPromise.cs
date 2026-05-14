using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.SDKComponents.MediaStream.YouTube;
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

        internal MediaAddress mediaAddress;
        internal MediaAddress originalAddress;
        internal bool isReachable;
        internal bool isLiveStream;
        internal float resolvedUrlExpiresAt;

        public bool IsResolved => status == Status.Resolved;

        public bool IsConsumed => status == Status.Consumed;

        public async UniTask UrlReachabilityResolveAsync(MediaAddress newMediaAddress, ReportData reportData, CancellationToken ct,
            IUrlResolverService urlResolverService)
        {
            status = Status.Pending;
            isReachable = false;
            isLiveStream = false;
            resolvedUrlExpiresAt = 0f;
            this.mediaAddress = newMediaAddress;
            this.originalAddress = newMediaAddress;

            if (mediaAddress.IsLivekitAddress(out _))
            {
                isReachable = true;
                status = Status.Resolved;
                return;
            }

            mediaAddress.IsUrlMediaAddress(out var urlMediaAddress);
            string url = urlMediaAddress!.Url;

            YouTubeTrace.Log($"promise.resolve START url={url}");
            ResolvedMediaUrl resolved = await urlResolverService.ResolveAsync(url, reportData, ct);
            YouTubeTrace.Log($"promise.resolve END reachable={resolved.IsReachable} live={resolved.IsLiveStream} rewrote={resolved.DirectUrl != url}");

            isReachable = resolved.IsReachable;
            isLiveStream = resolved.IsLiveStream;
            resolvedUrlExpiresAt = resolved.ExpiresAtRealtimeSinceStartup;

            if (resolved.DirectUrl != url)
                this.mediaAddress = MediaAddress.FromUrlMediaAddress(new UrlMediaAddress(resolved.DirectUrl));

            status = Status.Resolved;
        }

        public bool IsReachableConsume(MediaAddress address)
        {
            status = Status.Consumed;

            // mediaAddress may be rewritten to a resolved direct URL (see UrlReachabilityResolveAsync),
            // so compare against originalAddress which always holds the pre-resolution value
            // that matches what the component stores in its MediaAddress field
            if (originalAddress != address)
            {
                ReportHub.LogWarning(ReportCategory.MEDIA_STREAM, $"Try to consume different url - wanted <{address}>, but was <{originalAddress}>");
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
