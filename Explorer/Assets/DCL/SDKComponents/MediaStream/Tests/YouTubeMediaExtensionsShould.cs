using NUnit.Framework;

namespace DCL.SDKComponents.MediaStream.Tests
{
    public class YouTubeMediaExtensionsShould
    {
        [TestCase("https://www.youtube.com/watch?v=dQw4w9WgXcQ")]
        [TestCase("https://youtube.com/watch?v=dQw4w9WgXcQ")]
        [TestCase("https://www.youtube.com/watch?v=dQw4w9WgXcQ&list=PLrAXtmErZgOeiKm4sgNOknGvNjby9efdf")]
        [TestCase("https://youtu.be/dQw4w9WgXcQ")]
        [TestCase("https://youtu.be/dQw4w9WgXcQ?t=42")]
        [TestCase("https://www.youtube.com/live/abc123def")]
        [TestCase("https://www.youtube.com/shorts/abc123def")]
        public void DetectYouTubeUrls(string url)
        {
            Assert.That(url.IsYouTubeUrl(), Is.True);
        }

        [TestCase("https://www.google.com")]
        [TestCase("https://vimeo.com/123456")]
        [TestCase("https://example.com/video.mp4")]
        [TestCase("https://drive.google.com/file/d/abc123/view")]
        [TestCase("https://player.vimeo.com/external/552481870.m3u8")]
        [TestCase("livekit-video://current-stream")]
        [TestCase("")]
        public void RejectNonYouTubeUrls(string url)
        {
            Assert.That(url.IsYouTubeUrl(), Is.False);
        }
    }
}
