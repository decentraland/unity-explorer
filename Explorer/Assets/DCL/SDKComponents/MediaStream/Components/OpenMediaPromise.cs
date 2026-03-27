using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.WebRequests;
using System.Threading;
using UnityEngine.Networking;

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
        internal MediaAddress originalAddress;
        internal bool isReachable;
        internal bool isLiveStream;
        internal float resolvedUrlExpiresAt;

        public bool IsResolved => status == Status.Resolved;

        public bool IsConsumed => status == Status.Consumed;

        public async UniTask UrlReachabilityResolveAsync(IWebRequestController webRequestController, MediaAddress newMediaAddress, ReportData reportData, CancellationToken ct,
            IYouTubeUrlResolver youTubeResolver = null)
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

            // YouTube resolution: resolve to direct stream URL before reachability check
            if (youTubeResolver != null && url.IsYouTubeUrl())
            {
                ResolvedYouTubeUrl? resolved = await youTubeResolver.ResolveAsync(url, ct);

                if (resolved.HasValue)
                {
                    url = resolved.Value.DirectUrl;
                    this.mediaAddress = MediaAddress.FromUrlMediaAddress(new UrlMediaAddress(url));
                    this.isLiveStream = resolved.Value.IsLiveStream;
                    this.resolvedUrlExpiresAt = resolved.Value.ExpiresAtRealtimeSinceStartup;

                    ReportHub.Log(ReportCategory.MEDIA_STREAM, $"[YouTubeResolver] Resolved YouTube URL to direct stream (live={isLiveStream})");
                }
                else
                {
                    ReportHub.LogWarning(ReportCategory.MEDIA_STREAM, $"[YouTubeResolver] Failed to resolve YouTube URL: {url}");
                    status = Status.Resolved;
                    return;
                }
            }

            // Google Drive resolution: simple URL rewrite, no external library needed
            if (url.IsGoogleDriveUrl())
            {
                string directUrl = url.ResolveGoogleDriveDirectUrl();

                if (directUrl != null)
                {
                    url = directUrl;
                    this.mediaAddress = MediaAddress.FromUrlMediaAddress(new UrlMediaAddress(url));
                    ReportHub.Log(ReportCategory.MEDIA_STREAM, $"[GoogleDrive] Resolved to direct URL");

                    // Skip HEAD request for Google Drive — it causes exceptions in Unity's web request handler.
                    // Go directly to GET-based reachability check.
                    isReachable = await IsGetReachableAsync(url, ct);
                    ReportHub.Log(ReportCategory.MEDIA_STREAM, $"Resource <{url}> isReachable = <{isReachable}>");
                    status = Status.Resolved;
                    return;
                }
                else
                {
                    ReportHub.LogWarning(ReportCategory.MEDIA_STREAM, $"[GoogleDrive] Could not extract file ID from URL: {url}");
                }
            }

            isReachable = await webRequestController.IsHeadReachableAsync(reportData, URLAddress.FromString(url), ct, 5, true);
            //This is needed because some servers might not handle HEAD requests correctly and return 404 errors, even thou they are perfectly
            if (!isReachable)
                isReachable = await IsGetReachableAsync(url, ct);

            ReportHub.Log(ReportCategory.MEDIA_STREAM, $"Resource <{url}> isReachable = <{isReachable}>");

            status = Status.Resolved;
        }

        /*The following function is a temporary workardound for Issue #2485
         *The streaming server was taking 3 minutes to fail with timeout in the IsHeadReachableAsync method, by adding a timeout it was getting stuck
         *in the previous IsGetReachable async that was performing a get request, starting the stream and never ending
         *There was no other way other then the new UnityWebRequest to start the request with the webrequestcontroller and interrupt it after a few bytes were received
         * This will be soon replaced by the integration of HTTP2 library that will provide a clearer way to solve the problem
         */
        private async UniTask<bool> IsGetReachableAsync(string url, CancellationToken ct)
        {
            UnityWebRequest isGetReachableRequest = UnityWebRequest.Get(url);
            isGetReachableRequest.SendWebRequest();

            while (isGetReachableRequest.downloadedBytes == 0)
            {
                ct.ThrowIfCancellationRequested();

                if (!string.IsNullOrEmpty(isGetReachableRequest.error))
                    return false;

                await UniTask.Yield();
            }

            isGetReachableRequest.Abort();
            return true;
        }

        public bool IsReachableConsume(MediaAddress address)
        {
            status = Status.Consumed;

            // Compare against the original address (pre-YouTube-resolution) since
            // component.MediaAddress still holds the original YouTube URL
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
