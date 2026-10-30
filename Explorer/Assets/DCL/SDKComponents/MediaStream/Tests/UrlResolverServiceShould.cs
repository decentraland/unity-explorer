using Cysharp.Threading.Tasks;
using DCL.WebRequests;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.TestTools;

namespace DCL.SDKComponents.MediaStream.Tests
{
    public class UrlResolverServiceShould
    {
        private const string SIGNED_VIMEO_URL = "https://player.vimeo.com/external/123456.m3u8?s=secret-token";

        private IWebRequestController webRequestController = null!;
        private IYouTubeUrlResolver youTubeResolver = null!;
        private UrlResolverService service = null!;

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
            const string URL = "https://www.youtube.com/watch?v=dQw4w9WgXcQ";

            youTubeResolver.ResolveAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                           .Returns(UniTask.FromResult<ResolvedYouTubeUrl?>(null));

            ResolvedMediaUrl result = await service.ResolveAsync(URL, default, CancellationToken.None).AsTask();

            Assert.That(result.DirectUrl, Is.EqualTo(URL));
            Assert.That(result.IsReachable, Is.False);
        }

        [Test]
        public async Task ResolveAsync_WhenYouTubeResolverSucceeds_ReturnsDirectUrl()
        {
            const string URL = "https://www.youtube.com/watch?v=dQw4w9WgXcQ";
            const string DIRECT_URL = "https://rr1---sn-example.googlevideo.com/videoplayback?id=xyz";

            var resolved = new ResolvedYouTubeUrl(DIRECT_URL, isLiveStream: false, expiresAtRealtimeSinceStartup: 999f);

            youTubeResolver.ResolveAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                           .Returns(UniTask.FromResult<ResolvedYouTubeUrl?>(resolved));

            ResolvedMediaUrl result = await service.ResolveAsync(URL, default, CancellationToken.None).AsTask();

            Assert.That(result.DirectUrl, Is.EqualTo(DIRECT_URL));
            Assert.That(result.IsReachable, Is.True);
            Assert.That(result.IsLiveStream, Is.False);
            Assert.That(result.ExpiresAtRealtimeSinceStartup, Is.EqualTo(999f));
        }

        [Test]
        public async Task ResolveAsync_WhenYouTubeResolverSucceedsWithLiveStream_SetsLiveStreamFlag()
        {
            const string URL = "https://www.youtube.com/live/abc123def456";
            const string HLS_URL = "https://manifest.googlevideo.com/api/manifest/hls_playlist/id=abc";

            var resolved = new ResolvedYouTubeUrl(HLS_URL, isLiveStream: true, expiresAtRealtimeSinceStartup: 500f);

            youTubeResolver.ResolveAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                           .Returns(UniTask.FromResult<ResolvedYouTubeUrl?>(resolved));

            ResolvedMediaUrl result = await service.ResolveAsync(URL, default, CancellationToken.None).AsTask();

            Assert.That(result.DirectUrl, Is.EqualTo(HLS_URL));
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

        // --- Direct URL reachability ---

        [Test]
        public async Task ResolveAsync_WhenDirectHttpUrl_AndHeadProbeFails_ReturnsUnreachableWithoutThrowing()
        {
            // Arrange
            // A non-loopback http host: with the project's "insecure http not allowed" setting the GET fallback's
            // SendWebRequest throws InvalidOperationException synchronously, which must be answered as unreachable
            const string URL = "http://media.invalid/stream.mp4";

            webRequestController
               .SendAsync<GenericHeadRequest, GenericHeadArguments, WebRequestUtils.NoOp<GenericHeadRequest>, WebRequestUtils.NoResult>(
                    Arg.Any<RequestEnvelope<GenericHeadRequest, GenericHeadArguments>>(),
                    Arg.Any<WebRequestUtils.NoOp<GenericHeadRequest>>(),
                    Arg.Any<long>(),
                    Arg.Any<IProgress<float>?>())
               .Returns(UniTask.FromException<WebRequestUtils.NoResult>(new Exception("HEAD probe failed")));

            // Act
            ResolvedMediaUrl result = await service.ResolveAsync(URL, default, CancellationToken.None).AsTask();

            // Assert
            Assert.That(result.DirectUrl, Is.EqualTo(URL));
            Assert.That(result.IsReachable, Is.False);
        }

        [Test]
        public async Task ResolveAsync_WhenSignedVimeoUrl_SkipsHeadProbe()
        {
            // Arrange
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act
            await service.ResolveAsync(SIGNED_VIMEO_URL, default, cts.Token).AsTask();

            // Assert
            _ = webRequestController.DidNotReceiveWithAnyArgs()
                                    .SendAsync<GenericHeadRequest, GenericHeadArguments, WebRequestUtils.NoOp<GenericHeadRequest>, WebRequestUtils.NoResult>(
                                         default, default, default);
        }

        [Test]
        public async Task ResolveAsync_WhenDirectUrl_LogsItWithoutQuery()
        {
            // Arrange
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            LogAssert.Expect(LogType.Log, new Regex(@"Resource <https://player\.vimeo\.com/external/123456\.m3u8> isReachable"));

            // Act & Assert
            await service.ResolveAsync(SIGNED_VIMEO_URL, default, cts.Token).AsTask();
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
