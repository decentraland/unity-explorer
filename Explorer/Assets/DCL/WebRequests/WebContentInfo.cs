using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine.Networking;

namespace DCL.WebRequests
{
    public class WebContentInfo
    {
        public readonly ContentType Type;
        public readonly long SizeInBytes;

        public WebContentInfo(ContentType type, long sizeInBytes)
        {
            Type = type;
            SizeInBytes = sizeInBytes;
        }

        public static async Task<WebContentInfo> FetchAsync(string url, CancellationToken ct = default)
        {
            var request = UnityWebRequest.Head(url);
            await request.SendWebRequest().WithCancellation(ct);

            return await FetchAsync(request);
        }

        public static Task<WebContentInfo> FetchAsync(UnityWebRequest request)
        {
            if (request.result != UnityWebRequest.Result.Success)
                throw new Exception("Failed to fetch web content info: " + request.error);

            return Task.FromResult(new WebContentInfo(
                GetContentType(request),
                GetSizeInBytes(request)
            ));
        }

        private static ContentType GetContentType(UnityWebRequest request)
        {
            string? contentType = request.GetResponseHeader("Content-Type");

            if (contentType == null)
            {
                // For old converter only, which does not return content type
                if(request.url.EndsWith(".ktx2")) return ContentType.KTX2;
                if(request.url.EndsWith(".mp4")) return ContentType.Video;

                return ContentType.Unknown;
            }


            if (contentType == "image/ktx2") return ContentType.KTX2;
            if (contentType.StartsWith("image/")) return ContentType.Image;
            if (contentType.StartsWith("video/")) return ContentType.Video;

            return ContentType.Unknown;
        }

        private static long GetSizeInBytes(UnityWebRequest request)
        {
            if (long.TryParse(request.GetResponseHeader("Content-Length") ?? "NONE", out long length))
            {
                return length;
            }

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
