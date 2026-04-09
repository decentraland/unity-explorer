using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
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

            ResolvedMediaUrl resolved = await urlResolverService.ResolveAsync(url, reportData, ct);

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

            // Compare against the original address (pre-resolution) since
            // component.MediaAddress still holds the original URL
            MediaAddress compareAddress = originalAddress.IsEmpty ? this.mediaAddress : originalAddress;

            if (compareAddress != address)
            {
                ReportHub.LogWarning(ReportCategory.MEDIA_STREAM, $"Try to consume different url - wanted <{address}>, but was <{compareAddress}>");
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
