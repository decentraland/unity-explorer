using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using System;
using System.Threading;

namespace DCL.WebRequests
{
    public readonly struct WebContentInfo
    {
        public readonly ContentType Type;
        public readonly long SizeInBytes;

        private WebContentInfo(ContentType type, long sizeInBytes)
        {
            Type = type;
            SizeInBytes = sizeInBytes;
        }

        public static async UniTask<WebContentInfo> FetchAsync(IWebRequestController webRequestController, Uri url, ReportData reportData, CancellationToken ct)
        {
            using IWebRequest? response = await webRequestController.HeadAsync(new CommonArguments(url, attemptsCount: 1), reportData)
                                                                    .SendAsync(ct);

            return await FetchAsync(response);
        }

        public static UniTask<WebContentInfo> FetchAsync(IWebRequest response) =>
            UniTask.FromResult(new WebContentInfo(
                GetContentType(response),
                GetSizeInBytes(response)
            ));

        private static ContentType GetContentType(IWebRequest request)
        {
            string? contentType = request.Response.GetHeader(WebRequestHeaders.CONTENT_TYPE_HEADER);
            string uriString = request.Url.OriginalString;

            if (contentType == null)
            {
                // For old converter only, which does not return content type
                if (uriString.EndsWith(".ktx2")) return ContentType.KTX2;
                if (uriString.EndsWith(".mp4")) return ContentType.Video;

                return ContentType.Unknown;
            }


            if (contentType == "image/ktx2") return ContentType.KTX2;
            if (contentType.StartsWith("image/")) return ContentType.Image;
            if (contentType.StartsWith("video/")) return ContentType.Video;

            return ContentType.Unknown;
        }

        private static long GetSizeInBytes(IWebRequest request)
        {
            if (long.TryParse(request.Response.GetHeader(WebRequestHeaders.CONTENT_TYPE_HEADER) ?? "NONE", out long length))
                return length;

            return -1;
        }

        public enum ContentType
        {
            Image,
            KTX2,
            Video,
            Unknown
        }
    }
}
