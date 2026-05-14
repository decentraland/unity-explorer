using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.SDKComponents.MediaStream.YouTube;
using DCL.WebRequests;
using System.Threading;
using UnityEngine.Networking;

namespace DCL.SDKComponents.MediaStream
{
    public class UrlResolverService : IUrlResolverService
    {
        private readonly IWebRequestController webRequestController;
        private readonly IYouTubeUrlResolver youTubeResolver;

        public UrlResolverService(IWebRequestController webRequestController)
            : this(webRequestController, new YouTubeUrlResolver()) { }

        internal UrlResolverService(IWebRequestController webRequestController, IYouTubeUrlResolver youTubeResolver)
        {
            this.webRequestController = webRequestController;
            this.youTubeResolver = youTubeResolver;
        }

        public async UniTask<ResolvedMediaUrl> ResolveAsync(string url, ReportData reportData, CancellationToken ct)
        {
            // YouTube resolution
            if (url.IsYouTubeUrl())
                return await ResolveYouTubeAsync(url, ct);

            // Google Drive resolution
            if (url.IsGoogleDriveUrl())
                return await ResolveGoogleDriveAsync(url, ct);

            // Direct URL — just check reachability
            return await ResolveDirectUrlAsync(url, reportData, ct);
        }

        private async UniTask<ResolvedMediaUrl> ResolveYouTubeAsync(string url, CancellationToken ct)
        {
            YouTubeTrace.Log("urlResolver.youtube START");
            ResolvedYouTubeUrl? resolved = await youTubeResolver.ResolveAsync(url, ct);
            YouTubeTrace.Log($"urlResolver.youtube END resolved={resolved != null}");

            if (resolved == null)
            {
                ReportHub.LogWarning(ReportCategory.MEDIA_STREAM, $"[YouTube] Failed to resolve: {url}");
                return new ResolvedMediaUrl(url, isReachable: false);
            }

            string directUrl = resolved.Value.DirectUrl;
            ReportHub.Log(ReportCategory.MEDIA_STREAM, $"[YouTube] Resolved to direct stream (live={resolved.Value.IsLiveStream})");

            return new ResolvedMediaUrl(
                directUrl,
                isReachable: true,
                isLiveStream: resolved.Value.IsLiveStream,
                expiresAtRealtimeSinceStartup: resolved.Value.ExpiresAtRealtimeSinceStartup
            );
        }

        private async UniTask<ResolvedMediaUrl> ResolveGoogleDriveAsync(string url, CancellationToken ct)
        {
            string directUrl = url.ResolveGoogleDriveDirectUrl();

            if (directUrl == null)
            {
                ReportHub.LogWarning(ReportCategory.MEDIA_STREAM, $"[GoogleDrive] Could not extract file ID from URL: {url}");
                return new ResolvedMediaUrl(url, isReachable: false);
            }

            ReportHub.Log(ReportCategory.MEDIA_STREAM, $"[GoogleDrive] Resolved to direct URL");

            // Skip HEAD request for Google Drive — it causes exceptions in Unity's web request handler.
            bool isReachable = await IsGetReachableAsync(directUrl, ct);
            ReportHub.Log(ReportCategory.MEDIA_STREAM, $"Resource <{directUrl}> isReachable = <{isReachable}>");

            return new ResolvedMediaUrl(directUrl, isReachable);
        }

        private async UniTask<ResolvedMediaUrl> ResolveDirectUrlAsync(string url, ReportData reportData, CancellationToken ct)
        {
            bool isReachable = await webRequestController.IsHeadReachableAsync(reportData, URLAddress.FromString(url), ct, 5, true);

            // Fallback: some servers don't handle HEAD requests correctly
            if (!isReachable)
                isReachable = await IsGetReachableAsync(url, ct);

            ReportHub.Log(ReportCategory.MEDIA_STREAM, $"Resource <{url}> isReachable = <{isReachable}>");

            return new ResolvedMediaUrl(url, isReachable);
        }

        private static async UniTask<bool> IsGetReachableAsync(string url, CancellationToken ct)
        {
            UnityWebRequest request = UnityWebRequest.Get(url);

            try
            {
                request.SendWebRequest();

                while (request.downloadedBytes == 0)
                {
                    if (ct.IsCancellationRequested)
                        return false;

                    if (!string.IsNullOrEmpty(request.error))
                        return false;

                    await UniTask.Yield();
                }

                return true;
            }
            finally
            {
                request.Abort();
                request.Dispose();
            }
        }
    }
}
