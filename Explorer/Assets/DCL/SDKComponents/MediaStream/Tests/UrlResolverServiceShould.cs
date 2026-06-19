#if AV_PRO_PRESENT
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.WebRequests;
using NSubstitute;
using NUnit.Framework;
using System.Threading;
using System.Threading.Tasks;

namespace DCL.SDKComponents.MediaStream.Tests
{
    public class UrlResolverServiceShould
    {
        private IWebRequestController webRequestController;
        private IYouTubeUrlResolver youTubeResolver;
        private UrlResolverService service;

        [SetUp]
        public void SetUp()
        {
            webRequestController = Substitute.For<IWebRequestController>();
            youTubeResolver = Substitute.For<IYouTubeUrlResolver>();
            service = new UrlResolverService(webRequestController, youTubeResolver);
        }

        // --- YouTube routing ---

        [TestCase("https://www.youtube.com/watch?v=dQw4w9WgXcQ")]
        [TestCase("https://youtu.be/dQw4w9WgXcQ")]
        [TestCase("https://www.youtube.com/live/abc123def456")]
        [TestCase("https://www.youtube.com/shorts/abc123def456")]
        public async Task ResolveAsync_WhenYouTubeUrl_DelegatesToYouTubeResolver(string url)
        {
            youTubeResolver.ResolveAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                           .Returns(UniTask.FromResult<ResolvedYouTubeUrl?>(null));

            await service.ResolveAsync(url, default, CancellationToken.None).AsTask();

            await youTubeResolver.Received(1).ResolveAsync(url, Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task ResolveAsync_WhenYouTubeResolverReturnsNull_ReturnsUnreachable()
        {
            const string url = "https://www.youtube.com/watch?v=dQw4w9WgXcQ";

            youTubeResolver.ResolveAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                           .Returns(UniTask.FromResult<ResolvedYouTubeUrl?>(null));

            ResolvedMediaUrl result = await service.ResolveAsync(url, default, CancellationToken.None).AsTask();

            Assert.That(result.DirectUrl, Is.EqualTo(url));
            Assert.That(result.IsReachable, Is.False);
        }

        [Test]
        public async Task ResolveAsync_WhenYouTubeResolverSucceeds_ReturnsDirectUrl()
        {
            const string url = "https://www.youtube.com/watch?v=dQw4w9WgXcQ";
            const string directUrl = "https://rr1---sn-example.googlevideo.com/videoplayback?id=xyz";

            var resolved = new ResolvedYouTubeUrl(directUrl, isLiveStream: false, expiresAtRealtimeSinceStartup: 999f);

            youTubeResolver.ResolveAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                           .Returns(UniTask.FromResult<ResolvedYouTubeUrl?>(resolved));

            ResolvedMediaUrl result = await service.ResolveAsync(url, default, CancellationToken.None).AsTask();

            Assert.That(result.DirectUrl, Is.EqualTo(directUrl));
            Assert.That(result.IsReachable, Is.True);
            Assert.That(result.IsLiveStream, Is.False);
            Assert.That(result.ExpiresAtRealtimeSinceStartup, Is.EqualTo(999f));
        }

        [Test]
        public async Task ResolveAsync_WhenYouTubeResolverSucceedsWithLiveStream_SetsLiveStreamFlag()
        {
            const string url = "https://www.youtube.com/live/abc123def456";
            const string hlsUrl = "https://manifest.googlevideo.com/api/manifest/hls_playlist/id=abc";

            var resolved = new ResolvedYouTubeUrl(hlsUrl, isLiveStream: true, expiresAtRealtimeSinceStartup: 500f);

            youTubeResolver.ResolveAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                           .Returns(UniTask.FromResult<ResolvedYouTubeUrl?>(resolved));

            ResolvedMediaUrl result = await service.ResolveAsync(url, default, CancellationToken.None).AsTask();

            Assert.That(result.DirectUrl, Is.EqualTo(hlsUrl));
            Assert.That(result.IsReachable, Is.True);
            Assert.That(result.IsLiveStream, Is.True);
        }

        // --- Google Drive routing ---

        [TestCase("https://drive.google.com/drive/folders/abc123")]
        [TestCase("https://drive.google.com/file/d/")]
        [TestCase("https://drive.google.com/open?foo=bar")]
        public async Task ResolveAsync_WhenGoogleDriveUrl_WithUnextractableFileId_ReturnsUnreachable(string url)
        {
            ResolvedMediaUrl result = await service.ResolveAsync(url, default, CancellationToken.None).AsTask();

            Assert.That(result.DirectUrl, Is.EqualTo(url));
            Assert.That(result.IsReachable, Is.False);
        }

        // --- Cancellation ---

        [TestCase("https://www.youtube.com/watch?v=dQw4w9WgXcQ")]
        [TestCase("https://youtu.be/dQw4w9WgXcQ")]
        [TestCase("https://www.youtube.com/live/abc123def456")]
        public async Task ResolveAsync_WhenYouTubeUrl_AndCancelled_ReturnsUnreachable(string url)
        {
            youTubeResolver.ResolveAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                           .Returns(UniTask.FromResult<ResolvedYouTubeUrl?>(null));

            using var cts = new CancellationTokenSource();
            cts.Cancel();

            ResolvedMediaUrl result = await service.ResolveAsync(url, default, cts.Token).AsTask();

            Assert.That(result.DirectUrl, Is.EqualTo(url));
            Assert.That(result.IsReachable, Is.False);
        }
    }
}
#endif
