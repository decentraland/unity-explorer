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
        private UrlResolverService service;

        [SetUp]
        public void SetUp()
        {
            webRequestController = Substitute.For<IWebRequestController>();
            service = new UrlResolverService(webRequestController);
        }

        [TestCase("https://drive.google.com/drive/folders/abc123")]
        [TestCase("https://drive.google.com/file/d/")]
        [TestCase("https://drive.google.com/open?foo=bar")]
        public async Task ResolveAsync_WhenGoogleDriveUrl_WithUnextractableFileId_ReturnsUnreachable(string url)
        {
            ResolvedMediaUrl result = await service.ResolveAsync(url, default, CancellationToken.None).AsTask();

            Assert.That(result.DirectUrl, Is.EqualTo(url));
            Assert.That(result.IsReachable, Is.False);
        }

        [TestCase("https://www.youtube.com/watch?v=dQw4w9WgXcQ")]
        [TestCase("https://youtu.be/dQw4w9WgXcQ")]
        [TestCase("https://www.youtube.com/live/abc123def456")]
        public async Task ResolveAsync_WhenYouTubeUrl_AndCancelled_ReturnsUnreachable(string url)
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            ResolvedMediaUrl result = await service.ResolveAsync(url, default, cts.Token).AsTask();

            Assert.That(result.DirectUrl, Is.EqualTo(url));
            Assert.That(result.IsReachable, Is.False);
        }
    }
}
