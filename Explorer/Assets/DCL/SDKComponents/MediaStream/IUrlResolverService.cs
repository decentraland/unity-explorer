using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.WebRequests;
using System.Threading;

namespace DCL.SDKComponents.MediaStream
{
    public readonly struct ResolvedMediaUrl
    {
        public readonly string DirectUrl;
        public readonly bool IsReachable;
        public readonly bool IsLiveStream;
        public readonly float ExpiresAtRealtimeSinceStartup;

        public ResolvedMediaUrl(string directUrl, bool isReachable, bool isLiveStream = false, float expiresAtRealtimeSinceStartup = 0f)
        {
            DirectUrl = directUrl;
            IsReachable = isReachable;
            IsLiveStream = isLiveStream;
            ExpiresAtRealtimeSinceStartup = expiresAtRealtimeSinceStartup;
        }
    }

    public interface IUrlResolverService
    {
        /// <summary>
        ///     Resolves an input URL into a direct media URL that AVPro Video can play.
        ///     Handles YouTube resolution, Google Drive rewriting, and reachability checking.
        /// </summary>
        UniTask<ResolvedMediaUrl> ResolveAsync(string url, ReportData reportData, CancellationToken ct);
    }
}
