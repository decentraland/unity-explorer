using Cysharp.Threading.Tasks;
using DCL.SDKComponents.MediaStream.YouTube;
using System.Threading;

namespace DCL.SDKComponents.MediaStream
{
    internal interface IYouTubeVideoClient
    {
        /// <summary>Returns true if the video is a live stream.</summary>
        UniTask<bool> IsLiveStreamAsync(VideoId videoId, CancellationToken ct);

        UniTask<StreamManifest> GetStreamManifestAsync(VideoId videoId, CancellationToken ct);

        /// <summary>
        ///     Returns a streaming manifest URL for the given video — HLS if YouTube provides it,
        ///     otherwise DASH, otherwise empty. Both formats give AVPro the timing metadata
        ///     it needs for clean A/V sync, unlike YouTube's legacy muxed MP4 (itag=18).
        /// </summary>
        UniTask<string> GetStreamingManifestUrlAsync(VideoId videoId, CancellationToken ct);
    }

    internal class YouTubeVideoClient : IYouTubeVideoClient
    {
        private readonly InnerTubeClient innerTube = new ();

        public async UniTask<bool> IsLiveStreamAsync(VideoId videoId, CancellationToken ct)
        {
            PlayerResponse response = await innerTube.FetchPlayerResponseAsync(videoId, ct);
            return response.IsLive;
        }

        public async UniTask<StreamManifest> GetStreamManifestAsync(VideoId videoId, CancellationToken ct)
        {
            PlayerResponse response = await innerTube.FetchPlayerResponseAsync(videoId, ct);
            return new StreamManifest(response.MuxedStreams, response.VideoOnlyStreams);
        }

        public async UniTask<string> GetStreamingManifestUrlAsync(VideoId videoId, CancellationToken ct)
        {
            PlayerResponse response = await innerTube.FetchPlayerResponseAsync(videoId, ct);

            // HLS preferred for AVPro compatibility (rock-solid HLS support).
            // ANDROID_VR typically returns HLS only for live, DASH for VODs — so DASH
            // is the actual codepath that fixes A/V sync on most VODs.
            if (!string.IsNullOrEmpty(response.HlsManifestUrl))
                return response.HlsManifestUrl!;

            if (!string.IsNullOrEmpty(response.DashManifestUrl))
                return response.DashManifestUrl!;

            return string.Empty;
        }
    }
}
