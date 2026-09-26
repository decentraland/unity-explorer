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

        internal VideoTemplateData template;

        public bool IsResolved => status == Status.Resolved;

        public bool IsConsumed => status == Status.Consumed;

        public MediaAddress ResolvedAddress => template.ResolvedAddress;

        public bool IsLiveStream => template.Resolved.IsLiveStream;

        public float ResolvedUrlExpiresAt => template.Resolved.ExpiresAtRealtimeSinceStartup;

        /// <summary>
        ///     Seeds preload metadata as already-resolved to skip URL re-resolution.
        ///     Status stays <see cref="Status.Resolved" /> (not Consumed) so the normal consume path still runs.
        /// </summary>
        public void SeedResolved(in VideoTemplateData tpl)
        {
            status = Status.Resolved;
            template = tpl;
        }

        public VideoTemplateData ToTemplateData() =>
            template;

        public async UniTask UrlReachabilityResolveAsync(MediaAddress newMediaAddress, ReportData reportData, CancellationToken ct,
            IUrlResolverService urlResolverService)
        {
            status = Status.Pending;
            template = default;

            if (newMediaAddress.IsLivekitAddress(out _))
            {
                template = new VideoTemplateData(newMediaAddress, newMediaAddress, new ResolvedMediaUrl(string.Empty, isReachable: true));
                status = Status.Resolved;
                return;
            }

            newMediaAddress.IsUrlMediaAddress(out var urlMediaAddress);
            string url = urlMediaAddress!.Url;

            ResolvedMediaUrl resolved = await urlResolverService.ResolveAsync(url, reportData, ct);

            MediaAddress resolvedAddress = resolved.DirectUrl != url
                ? MediaAddress.FromUrlMediaAddress(new UrlMediaAddress(resolved.DirectUrl))
                : newMediaAddress;

            template = new VideoTemplateData(resolvedAddress, newMediaAddress, resolved);
            status = Status.Resolved;
        }

        public bool IsReachableConsume(MediaAddress address)
        {
            status = Status.Consumed;

            // match against the pre-resolution address; ResolvedAddress may have been rewritten during resolution
            if (template.OriginalAddress != address)
            {
                ReportHub.LogWarning(ReportCategory.MEDIA_STREAM, $"Try to consume different url - wanted <{address}>, but was <{template.OriginalAddress}>");
                return false;
            }

            if (template.Resolved.IsReachable) return true;

            ReportHub.LogWarning(ReportCategory.MEDIA_STREAM, $"Try to consume not reachable URL <{template.ResolvedAddress}>");
            return false;
        }
    }
}
