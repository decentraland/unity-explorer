using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.WebRequests;
using System;
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

        private static readonly string[] MEDIA_CONTENT_TYPE_PREFIXES =
        {
            "video/",
            "audio/",
            "application/vnd.apple.mpegurl",
            "application/x-mpegurl",
            "application/dash+xml",
            "application/octet-stream",
            "binary/octet-stream",
        };

        internal Status status;

        // TODO (Vit): add caching mechanism for resolved promises: <url, isReachable> (on some upper level)
        internal MediaAddress mediaAddress;
        internal bool isReachable;

        public bool IsResolved => status == Status.Resolved;

        public bool IsConsumed => status == Status.Consumed;

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
            string url = urlMediaAddress!.Url;

            HeadReachabilityResult headResult = await webRequestController.HeadReachabilityAsync(reportData, URLAddress.FromString(url), ct, 5);

            if (headResult.IsReachable)
            {
                isReachable = IsMediaContentType(url, headResult.ContentType);
            }
            else
            {
                // HEAD might not be supported by some streaming platforms — fall back to GET
                isReachable = await IsGetReachableAndMediaTypeAsync(url, ct);
            }

            ReportHub.Log(ReportCategory.MEDIA_STREAM, $"Resource <{url}> isReachable = <{isReachable}>");

            status = Status.Resolved;
        }

        internal static bool IsMediaContentType(string url, string contentType)
        {
            // No content type header — let AVProVideo try (some CDNs omit it)
            if (string.IsNullOrEmpty(contentType))
                return true;

            string lowerContentType = contentType.ToLowerInvariant();

            for (int i = 0; i < MEDIA_CONTENT_TYPE_PREFIXES.Length; i++)
            {
                if (lowerContentType.StartsWith(MEDIA_CONTENT_TYPE_PREFIXES[i], StringComparison.Ordinal))
                    return true;
            }

            ReportHub.LogError(ReportCategory.MEDIA_STREAM,
                $"URL <{url}> returned non-media Content-Type <{contentType}>. "
                + "Use a direct media URL (.mp4, .m3u8, .webm) instead of a webpage URL.");

            return false;
        }

        /*The following function is a temporary workaround for Issue #2485
         *The streaming server was taking 3 minutes to fail with timeout in the IsHeadReachableAsync method, by adding a timeout it was getting stuck
         *in the previous IsGetReachable async that was performing a get request, starting the stream and never ending
         *There was no other way other then the new UnityWebRequest to start the request with the webrequestcontroller and interrupt it after a few bytes were received
         * This will be soon replaced by the integration of HTTP2 library that will provide a clearer way to solve the problem
         */
        private static async UniTask<bool> IsGetReachableAndMediaTypeAsync(string url, CancellationToken ct)
        {
            UnityWebRequest getRequest = UnityWebRequest.Get(url);

            try
            {
                getRequest.SendWebRequest().ToUniTask(cancellationToken: ct);

                while (getRequest.downloadedBytes == 0)
                {
                    if (!string.IsNullOrEmpty(getRequest.error))
                        return false;

                    await UniTask.Yield();
                }

                string contentType = getRequest.GetResponseHeader("Content-Type");
                return IsMediaContentType(url, contentType);
            }
            catch (Exception)
            {
                return false;
            }
            finally
            {
                getRequest.Abort();
                getRequest.Dispose();
            }
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
