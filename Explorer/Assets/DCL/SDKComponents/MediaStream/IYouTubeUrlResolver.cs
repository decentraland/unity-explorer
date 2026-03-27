using Cysharp.Threading.Tasks;
using System.Threading;

namespace DCL.SDKComponents.MediaStream
{
    public readonly struct ResolvedYouTubeUrl
    {
        public readonly string DirectUrl;
        public readonly bool IsLiveStream;
        public readonly float ExpiresAtRealtimeSinceStartup;

        public ResolvedYouTubeUrl(string directUrl, bool isLiveStream, float expiresAtRealtimeSinceStartup)
        {
            DirectUrl = directUrl;
            IsLiveStream = isLiveStream;
            ExpiresAtRealtimeSinceStartup = expiresAtRealtimeSinceStartup;
        }
    }

    public interface IYouTubeUrlResolver
    {
        /// <summary>
        ///     Resolves a YouTube URL into a direct stream URL that AVPro Video can play.
        ///     Returns null if resolution fails (DRM, private, unavailable, etc.).
        /// </summary>
        UniTask<ResolvedYouTubeUrl?> ResolveAsync(string youtubeUrl, CancellationToken ct);
    }
}
