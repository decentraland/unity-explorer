using Cysharp.Threading.Tasks;
using System.Threading;
using YoutubeExplode;
using YoutubeExplode.Videos;
using YoutubeExplode.Videos.Streams;

namespace DCL.SDKComponents.MediaStream
{
    internal interface IYouTubeVideoClient
    {
        /// <summary>Returns true if the video is a live stream (Duration == null).</summary>
        UniTask<bool> IsLiveStreamAsync(VideoId videoId, CancellationToken ct);

        UniTask<StreamManifest> GetStreamManifestAsync(VideoId videoId, CancellationToken ct);

        UniTask<string> GetHttpLiveStreamUrlAsync(VideoId videoId, CancellationToken ct);
    }

    internal class YoutubeClientAdapter : IYouTubeVideoClient
    {
        private readonly YoutubeClient client = new ();

        public async UniTask<bool> IsLiveStreamAsync(VideoId videoId, CancellationToken ct)
        {
            Video video = await client.Videos.GetAsync(videoId, ct);
            return video.Duration == null;
        }

        public async UniTask<StreamManifest> GetStreamManifestAsync(VideoId videoId, CancellationToken ct) =>
            await client.Videos.Streams.GetManifestAsync(videoId, ct);

        public async UniTask<string> GetHttpLiveStreamUrlAsync(VideoId videoId, CancellationToken ct) =>
            await client.Videos.Streams.GetHttpLiveStreamUrlAsync(videoId, ct);
    }
}
