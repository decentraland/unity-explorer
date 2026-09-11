using DCL.SDKComponents.MediaStream.YouTube;
using NUnit.Framework;

namespace DCL.SDKComponents.MediaStream.Tests
{
    public class VideoIdShould
    {
        // -------------------------------------------------------------------------
        // Valid URL shapes
        // -------------------------------------------------------------------------

        [TestCase("https://www.youtube.com/watch?v=dQw4w9WgXcQ", "dQw4w9WgXcQ")]
        [TestCase("http://www.youtube.com/watch?v=dQw4w9WgXcQ", "dQw4w9WgXcQ")]
        [TestCase("https://youtube.com/watch?v=dQw4w9WgXcQ", "dQw4w9WgXcQ")]
        [TestCase("https://m.youtube.com/watch?v=dQw4w9WgXcQ", "dQw4w9WgXcQ")]
        [TestCase("https://www.youtube.com/watch?v=dQw4w9WgXcQ&t=30s", "dQw4w9WgXcQ")]
        [TestCase("https://www.youtube.com/watch?feature=share&v=dQw4w9WgXcQ", "dQw4w9WgXcQ")]
        public void Parse_WatchUrl(string url, string expected)
        {
            VideoId? id = VideoId.TryParse(url);

            Assert.That(id, Is.Not.Null);
            Assert.That(id!.Value.Value, Is.EqualTo(expected));
        }

        [TestCase("https://youtu.be/dQw4w9WgXcQ", "dQw4w9WgXcQ")]
        [TestCase("https://youtu.be/dQw4w9WgXcQ?t=30", "dQw4w9WgXcQ")]
        [TestCase("http://youtu.be/dQw4w9WgXcQ", "dQw4w9WgXcQ")]
        public void Parse_ShortUrl(string url, string expected)
        {
            VideoId? id = VideoId.TryParse(url);

            Assert.That(id, Is.Not.Null);
            Assert.That(id!.Value.Value, Is.EqualTo(expected));
        }

        [TestCase("https://www.youtube.com/live/abc123def4X", "abc123def4X")]
        [TestCase("https://www.youtube.com/shorts/abc123def4X", "abc123def4X")]
        [TestCase("https://www.youtube.com/embed/abc123def4X", "abc123def4X")]
        [TestCase("https://www.youtube.com/v/abc123def4X", "abc123def4X")]
        public void Parse_PathSegment(string url, string expected)
        {
            VideoId? id = VideoId.TryParse(url);

            Assert.That(id, Is.Not.Null);
            Assert.That(id!.Value.Value, Is.EqualTo(expected));
        }

        [Test]
        public void Parse_BareId()
        {
            VideoId? id = VideoId.TryParse("dQw4w9WgXcQ");

            Assert.That(id, Is.Not.Null);
            Assert.That(id!.Value.Value, Is.EqualTo("dQw4w9WgXcQ"));
        }

        [Test]
        public void Parse_IdWithUnderscoreAndHyphen()
        {
            // YouTube IDs use the URL-safe base64 alphabet — must accept _ and -.
            VideoId? id = VideoId.TryParse("https://youtu.be/a_b-c_d-e_f");

            Assert.That(id, Is.Not.Null);
            Assert.That(id!.Value.Value, Is.EqualTo("a_b-c_d-e_f"));
        }

        // -------------------------------------------------------------------------
        // Invalid input
        // -------------------------------------------------------------------------

        [TestCase(null)]
        [TestCase("")]
        [TestCase("not-a-url")]
        [TestCase("https://example.com/video.mp4")]
        [TestCase("https://vimeo.com/123456")]
        [TestCase("https://www.youtube.com/")]
        [TestCase("https://www.youtube.com/watch")]
        [TestCase("https://www.youtube.com/watch?foo=bar")]
        public void Parse_InvalidUrl_ReturnsNull(string? url)
        {
            Assert.That(VideoId.TryParse(url), Is.Null);
        }

        [TestCase("dQw4w9WgXc")]   // 10 chars — too short
        [TestCase("dQw4w9WgXcQQ")] // 12 chars — too long
        [TestCase("dQw4w9WgXc!")]  // invalid char
        [TestCase("https://www.youtube.com/watch?v=tooShort")]
        [TestCase("https://youtu.be/tooShort")]
        public void Parse_WrongLengthOrCharset_ReturnsNull(string url)
        {
            Assert.That(VideoId.TryParse(url), Is.Null);
        }

        // -------------------------------------------------------------------------
        // Equality
        // -------------------------------------------------------------------------

        [Test]
        public void TwoIdsWithSameValue_AreEqual()
        {
            VideoId a = VideoId.TryParse("dQw4w9WgXcQ")!.Value;
            VideoId b = VideoId.TryParse("https://www.youtube.com/watch?v=dQw4w9WgXcQ")!.Value;

            Assert.That(a, Is.EqualTo(b));
            Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
        }
    }
}
