using NUnit.Framework;
using System.Threading;
using System.Threading.Tasks;

namespace DCL.SDKComponents.MediaStream.Tests
{
    public class YouTubeUrlResolverShould
    {
        private YouTubeUrlResolver resolver;

        [SetUp]
        public void SetUp()
        {
            resolver = new YouTubeUrlResolver();
        }

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
    }
}
