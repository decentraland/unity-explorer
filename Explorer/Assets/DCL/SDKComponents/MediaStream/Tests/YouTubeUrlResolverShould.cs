using Cysharp.Threading.Tasks;
using DCL.SDKComponents.MediaStream.YouTube;
using NSubstitute;
using NUnit.Framework;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DCL.SDKComponents.MediaStream.Tests
{
    public class YouTubeUrlResolverShould
    {
        private IYouTubeVideoClient client;
        private float fakeTime;
        private YouTubeUrlResolver resolver;

        [SetUp]
        public void SetUp()
        {
            client = Substitute.For<IYouTubeVideoClient>();
            fakeTime = 100f;
            resolver = new YouTubeUrlResolver(client, () => fakeTime);
        }

        // -------------------------------------------------------------------------
        // SelectBestStream
        // -------------------------------------------------------------------------

        [Test]
        public void SelectBestStream_WhenEmpty_ReturnsNull()
        {
            IStreamInfo result = YouTubeUrlResolver.SelectBestStream(new List<IStreamInfo>());

            Assert.That(result, Is.Null);
        }

        [Test]
        public void SelectBestStream_WhenNoMp4Streams_ReturnsNull()
        {
            IVideoStreamInfo webm = MakeStream(1080, 5_000_000, mp4: false);

            IStreamInfo result = YouTubeUrlResolver.SelectBestStream(new[] { webm });

            Assert.That(result, Is.Null);
        }

        [Test]
        public void SelectBestStream_WhenNoVideoStreams_ReturnsNull()
        {
            // IStreamInfo that is NOT IVideoStreamInfo (audio-only mock)
            IStreamInfo audioOnly = Substitute.For<IStreamInfo>();
            audioOnly.Container.Returns(Container.Mp4);

            IStreamInfo result = YouTubeUrlResolver.SelectBestStream(new[] { audioOnly });

            Assert.That(result, Is.Null);
        }

        [Test]
        public void SelectBestStream_SingleMp4Stream_ReturnsThatStream()
        {
            IVideoStreamInfo stream = MakeStream(1080, 5_000_000);

            IStreamInfo result = YouTubeUrlResolver.SelectBestStream(new[] { stream });

            Assert.That(result, Is.SameAs(stream));
        }

        [Test]
        public void SelectBestStream_Prefers1080pOver720p()
        {
            IVideoStreamInfo s720 = MakeStream(720, 3_000_000);
            IVideoStreamInfo s1080 = MakeStream(1080, 5_000_000);

            IStreamInfo result = YouTubeUrlResolver.SelectBestStream(new[] { s720, s1080 });

            Assert.That(result, Is.SameAs(s1080));
        }

        [Test]
        public void SelectBestStream_Prefers1080pOver1440p()
        {
            // Resolution above PREFERRED_HEIGHT (1080) is scored as height=0
            IVideoStreamInfo s1440 = MakeStream(1440, 10_000_000);
            IVideoStreamInfo s1080 = MakeStream(1080, 5_000_000);

            IStreamInfo result = YouTubeUrlResolver.SelectBestStream(new[] { s1440, s1080 });

            Assert.That(result, Is.SameAs(s1080));
        }

        [Test]
        public void SelectBestStream_SameResolution_PrefersHigherBitrate()
        {
            IVideoStreamInfo lowBitrate = MakeStream(1080, 3_000_000);
            IVideoStreamInfo highBitrate = MakeStream(1080, 8_000_000);

            IStreamInfo result = YouTubeUrlResolver.SelectBestStream(new[] { lowBitrate, highBitrate });

            Assert.That(result, Is.SameAs(highBitrate));
        }

        [Test]
        public void SelectBestStream_SkipsNonMp4EvenWithHigherResolution()
        {
            IVideoStreamInfo mp4720 = MakeStream(720, 3_000_000);
            IVideoStreamInfo webm1080 = MakeStream(1080, 8_000_000, mp4: false);

            IStreamInfo result = YouTubeUrlResolver.SelectBestStream(new[] { mp4720, webm1080 });

            Assert.That(result, Is.SameAs(mp4720));
        }

        // -------------------------------------------------------------------------
        // ResolveAsync — URL parsing & cancellation
        // -------------------------------------------------------------------------

        [TestCase("not-a-youtube-url")]
        [TestCase("https://example.com/video.mp4")]
        [TestCase("https://vimeo.com/123456")]
        [TestCase("")]
        public async Task ResolveAsync_WhenInvalidUrl_ReturnsNull(string url)
        {
            ResolvedYouTubeUrl? result = await resolver.ResolveAsync(url, CancellationToken.None).AsTask();

            Assert.That(result, Is.Null);
        }

        [TestCase("https://www.youtube.com/watch?v=dQw4w9WgXcQ")]
        [TestCase("https://youtu.be/dQw4w9WgXcQ")]
        [TestCase("https://www.youtube.com/live/abc123def456")]
        public async Task ResolveAsync_WhenCancelled_ReturnsNull(string url)
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            ResolvedYouTubeUrl? result = await resolver.ResolveAsync(url, cts.Token).AsTask();

            Assert.That(result, Is.Null);
        }

        // -------------------------------------------------------------------------
        // ResolveAsync — live stream path
        // -------------------------------------------------------------------------

        [Test]
        public async Task ResolveAsync_WhenUrlHintsLive_SkipsVideoInfoCheckAndReturnsHlsUrl()
        {
            const string url = "https://www.youtube.com/live/abc123def456";
            const string hlsUrl = "https://manifest.googlevideo.com/hls/manifest.m3u8";

            client.GetStreamingManifestUrlAsync(Arg.Any<VideoId>(), Arg.Any<CancellationToken>())
                  .Returns(UniTask.FromResult(hlsUrl));

            ResolvedYouTubeUrl? result = await resolver.ResolveAsync(url, CancellationToken.None).AsTask();

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Value.DirectUrl, Is.EqualTo(hlsUrl));
            Assert.That(result.Value.IsLiveStream, Is.True);

            // Live URL hint skips the IsLiveStreamAsync check entirely
            await client.DidNotReceive().IsLiveStreamAsync(Arg.Any<VideoId>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task ResolveAsync_WhenHlsUrlIsEmpty_ReturnsNull()
        {
            const string url = "https://www.youtube.com/live/abc123def456";

            client.GetStreamingManifestUrlAsync(Arg.Any<VideoId>(), Arg.Any<CancellationToken>())
                  .Returns(UniTask.FromResult(string.Empty));

            ResolvedYouTubeUrl? result = await resolver.ResolveAsync(url, CancellationToken.None).AsTask();

            Assert.That(result, Is.Null);
        }

        [Test]
        public async Task ResolveAsync_WhenVodIsLive_ReturnsHlsUrl()
        {
            const string url = "https://www.youtube.com/watch?v=dQw4w9WgXcQ";
            const string hlsUrl = "https://manifest.googlevideo.com/hls/manifest.m3u8";

            client.IsLiveStreamAsync(Arg.Any<VideoId>(), Arg.Any<CancellationToken>())
                  .Returns(UniTask.FromResult(true));

            client.GetStreamingManifestUrlAsync(Arg.Any<VideoId>(), Arg.Any<CancellationToken>())
                  .Returns(UniTask.FromResult(hlsUrl));

            ResolvedYouTubeUrl? result = await resolver.ResolveAsync(url, CancellationToken.None).AsTask();

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Value.DirectUrl, Is.EqualTo(hlsUrl));
            Assert.That(result.Value.IsLiveStream, Is.True);
        }

        // -------------------------------------------------------------------------
        // ResolveAsync — cache
        // -------------------------------------------------------------------------

        [Test]
        public async Task ResolveAsync_WhenCalledTwiceWithSameUrl_CallsClientOnlyOnce()
        {
            const string url = "https://www.youtube.com/live/abc123def456";

            client.GetStreamingManifestUrlAsync(Arg.Any<VideoId>(), Arg.Any<CancellationToken>())
                  .Returns(UniTask.FromResult("https://manifest.googlevideo.com/hls/manifest.m3u8"));

            await resolver.ResolveAsync(url, CancellationToken.None).AsTask();
            await resolver.ResolveAsync(url, CancellationToken.None).AsTask();

            await client.Received(1).GetStreamingManifestUrlAsync(Arg.Any<VideoId>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task ResolveAsync_WhenCacheExpired_ReResolvesUrl()
        {
            const string url = "https://www.youtube.com/live/abc123def456";

            client.GetStreamingManifestUrlAsync(Arg.Any<VideoId>(), Arg.Any<CancellationToken>())
                  .Returns(UniTask.FromResult("https://manifest.googlevideo.com/hls/manifest.m3u8"));

            await resolver.ResolveAsync(url, CancellationToken.None).AsTask();

            // Advance time past TTL
            fakeTime += YouTubeUrlResolver.CACHE_TTL_SECONDS + 1f;

            await resolver.ResolveAsync(url, CancellationToken.None).AsTask();

            await client.Received(2).GetStreamingManifestUrlAsync(Arg.Any<VideoId>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task ResolveAsync_WhenCacheStillValid_ReturnsCachedResult()
        {
            const string url = "https://www.youtube.com/live/abc123def456";
            const string hlsUrl = "https://manifest.googlevideo.com/hls/manifest.m3u8";

            client.GetStreamingManifestUrlAsync(Arg.Any<VideoId>(), Arg.Any<CancellationToken>())
                  .Returns(UniTask.FromResult(hlsUrl));

            await resolver.ResolveAsync(url, CancellationToken.None).AsTask();

            // Advance time but stay within TTL
            fakeTime += YouTubeUrlResolver.CACHE_TTL_SECONDS - 60f;

            ResolvedYouTubeUrl? result = await resolver.ResolveAsync(url, CancellationToken.None).AsTask();

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Value.DirectUrl, Is.EqualTo(hlsUrl));
            await client.Received(1).GetStreamingManifestUrlAsync(Arg.Any<VideoId>(), Arg.Any<CancellationToken>());
        }

        // -------------------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------------------

        private static IVideoStreamInfo MakeStream(int height, long bitrate, bool mp4 = true)
        {
            IVideoStreamInfo stream = Substitute.For<IVideoStreamInfo>();
            stream.Container.Returns(mp4 ? Container.Mp4 : Container.WebM);
            stream.VideoResolution.Returns(new VideoResolution(height * 16 / 9, height));
            stream.Bitrate.Returns(new Bitrate(bitrate));
            return stream;
        }
    }
}
