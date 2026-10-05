using NUnit.Framework;
using UnityEngine;

namespace DCL.SDKComponents.MediaStream.Tests
{
    public class PresentationLayoutShould
    {
        private const string LEGACY_METADATA = "{\"role\":\"presentation\",\"presentationId\":\"p\",\"currentSlide\":2,\"overlay\":{\"x\":0,\"y\":1,\"size\":\"small\"}}";
        private const string SLIDE_1080 = "\"slide\":{\"url\":\"https://cast-presenter-service.decentraland.org/presentations/p/slides/ab12.png\",\"width\":1920,\"height\":1080}";
        private const string CENTERED_VIDEO = "\"slideVideos\":[{\"url\":\"v.mp4\",\"geometry\":{\"x\":480,\"y\":270,\"width\":960,\"height\":540}}]";
        private const string PRESENTER_ADDRESS = "0x0123456789abcdefABCDEF0123456789abcdef01";

        [TestCase("{")]
        [TestCase("")]
        [TestCase(null)]
        [TestCase("{\"slide\":\"x\"}")]
        public void ReturnNull_WhenJsonIsMalformed(string? json)
        {
            PresentationBotMetadata? metadata = PresentationLayout.Parse(json);

            Assert.IsNull(metadata);
        }

        [Test]
        public void ParseLegacyMetadata_WhenSlideIsAbsent()
        {
            PresentationBotMetadata? metadata = PresentationLayout.Parse(LEGACY_METADATA);

            Assert.IsNotNull(metadata);
            Assert.IsNull(metadata!.slide);
            Assert.IsNotNull(metadata.overlay);
        }

        [Test]
        public void ParseV2Fields_WhenPresent()
        {
            var json = $"{{{SLIDE_1080},\"presenterIdentity\":\"{PRESENTER_ADDRESS}\",\"playingVideoIndex\":0,{CENTERED_VIDEO}}}";

            PresentationBotMetadata? metadata = PresentationLayout.Parse(json);

            Assert.IsNotNull(metadata);
            Assert.AreEqual("https://cast-presenter-service.decentraland.org/presentations/p/slides/ab12.png", metadata!.slide!.url);
            Assert.AreEqual(1920, metadata.slide.width);
            Assert.AreEqual(1080, metadata.slide.height);
            Assert.AreEqual(PRESENTER_ADDRESS, metadata.presenterIdentity);
            Assert.AreEqual(0, metadata.playingVideoIndex);
        }

        [Test]
        public void ParseNullPlayingVideoIndex_AsNoValue()
        {
            PresentationBotMetadata? metadata = PresentationLayout.Parse($"{{{SLIDE_1080},\"playingVideoIndex\":null}}");

            Assert.IsNotNull(metadata);
            Assert.IsFalse(metadata!.playingVideoIndex.HasValue);
        }

        [TestCase("{\"slide\":{\"url\":\"https://a/b.png\",\"width\":0,\"height\":1080}}")]
        [TestCase("{\"slide\":{\"width\":1920,\"height\":1080}}")]
        public void ReturnNull_WhenSlideHasNoSizeOrUrl(string json)
        {
            PresentationBotMetadata? metadata = PresentationLayout.Parse(json);

            Assert.IsNull(metadata);
        }

        [Test]
        public void ReturnNull_WhenSlideIsTooLarge()
        {
            PresentationBotMetadata? tooLarge = PresentationLayout.Parse("{\"slide\":{\"url\":\"https://a/b.png\",\"width\":2049,\"height\":1080}}");
            PresentationBotMetadata? atLimit = PresentationLayout.Parse("{\"slide\":{\"url\":\"https://a/b.png\",\"width\":2048,\"height\":1080}}");

            Assert.IsNull(tooLarge);
            Assert.IsNotNull(atLimit);
        }

        [Test]
        public void ReturnNull_WhenSlideUrlIsTooLong()
        {
            string url = "https://a/" + new string('a', PresentationLayout.MAX_SLIDE_URL_LENGTH + 1 - "https://a/".Length);

            PresentationBotMetadata? metadata = PresentationLayout.Parse($"{{\"slide\":{{\"url\":\"{url}\",\"width\":1920,\"height\":1080}}}}");

            Assert.IsNull(metadata);
        }

        [TestCase("0xOTHER", 0)]
        [TestCase("stream:", 193)]
        [TestCase("0x0123456789abcdef0123456789abcdef01234567\\n", 0)]
        public void DropPresenterIdentity_WhenFormatIsInvalid(string prefix, int padding)
        {
            PresentationBotMetadata metadata = ParseOrFail(WithPresenterIdentity(prefix + new string('a', padding)));

            Assert.IsNull(metadata.presenterIdentity);
        }

        [TestCase("stream:place:1", 0)]
        [TestCase("0x", 40)]
        public void KeepPresenterIdentity_WhenFormatIsValid(string prefix, int padding)
        {
            string identity = prefix + new string('a', padding);

            PresentationBotMetadata metadata = ParseOrFail(WithPresenterIdentity(identity));

            Assert.AreEqual(identity, metadata.presenterIdentity);
        }

        [TestCase("\"x\":\"NaN\",\"y\":1")]
        [TestCase("\"x\":0,\"y\":\"Infinity\"")]
        public void DropOverlay_WhenCoordinatesAreNotFinite(string coordinates)
        {
            PresentationBotMetadata metadata = ParseOrFail($"{{{SLIDE_1080},\"overlay\":{{{coordinates},\"size\":\"small\"}}}}");

            Assert.IsNull(metadata.overlay);
        }

        [Test]
        public void ParseInitialMetadata()
        {
            PresentationBotMetadata? metadata = PresentationLayout.Parse("{\"role\":\"presentation\",\"presentationId\":\"p\"}");

            Assert.IsNotNull(metadata);
            Assert.IsNull(metadata!.slide);
            Assert.IsNull(metadata.presenterIdentity);
            Assert.IsFalse(metadata.playingVideoIndex.HasValue);
            Assert.IsNull(metadata.slideVideos);
            Assert.IsNull(metadata.overlay);
        }

        [TestCase("playing")]
        [TestCase("loading")]
        [TestCase("paused")]
        public void ReturnVideoRect_RegardlessOfVideoState(string state)
        {
            PresentationBotMetadata metadata = ParseOrFail($"{{{SLIDE_1080},\"videoState\":\"{state}\",\"playingVideoIndex\":0,{CENTERED_VIDEO}}}");

            bool found = PresentationLayout.TryVideoRect(metadata, out Vector4 rect);

            Assert.IsTrue(found);
            Assert.AreEqual(new Vector4(0.25f, 0.25f, 0.5f, 0.5f), rect);
        }

        [TestCase("\"playingVideoIndex\":null," + CENTERED_VIDEO)]
        [TestCase("\"playingVideoIndex\":1," + CENTERED_VIDEO)]
        [TestCase("\"playingVideoIndex\":-1," + CENTERED_VIDEO)]
        [TestCase("\"playingVideoIndex\":0,\"slideVideos\":[null]")]
        [TestCase("\"playingVideoIndex\":0,\"slideVideos\":[{}]")]
        [TestCase("\"playingVideoIndex\":0,\"slideVideos\":[{\"geometry\":{\"x\":480,\"y\":270,\"width\":\"NaN\",\"height\":540}}]")]
        [TestCase("\"playingVideoIndex\":0,\"slideVideos\":[{\"geometry\":{\"x\":\"Infinity\",\"y\":270,\"width\":960,\"height\":540}}]")]
        public void ReturnNoVideoRect_WhenPlayingVideoIsInvalid(string fields)
        {
            PresentationBotMetadata metadata = ParseOrFail($"{{{SLIDE_1080},{fields}}}");

            bool found = PresentationLayout.TryVideoRect(metadata, out _);

            Assert.IsFalse(found);
        }

        [TestCase(null)]
        [TestCase("small")]
        [TestCase("bogus")]
        public void ComputeCameraRect_ForDefaultOverlay(string? size)
        {
            Vector4 rect = PresentationLayout.CameraRect(new PresentationOverlay { x = 0, y = 1, size = size }, 1920, 1080);

            AssertPixelRect(rect, 1920, 1080, 38, 754, 288);
        }

        [Test]
        public void ComputeCameraRect_ForLargeTopRight()
        {
            Vector4 rect = PresentationLayout.CameraRect(new PresentationOverlay { x = 1, y = 0, size = "large" }, 1920, 1080);

            AssertPixelRect(rect, 1920, 1080, 1402, 38, 480);
        }

        [Test]
        public void ComputeCameraRect_ForPortraitSlide()
        {
            Vector4 rect = PresentationLayout.CameraRect(new PresentationOverlay { x = 0.5, y = 0.5, size = "small" }, 1080, 1920);

            AssertPixelRect(rect, 1080, 1920, 458, 878, 162);
        }

        [Test]
        public void ComputeCameraRect_For720pLargeBottomRight()
        {
            Vector4 rect = PresentationLayout.CameraRect(new PresentationOverlay { x = 1, y = 1, size = "large" }, 1280, 720);

            AssertPixelRect(rect, 1280, 720, 934, 374, 320);
        }

        [Test]
        public void ReturnZeroCameraRect_WhenDiameterBelowTwo()
        {
            Vector4 rect = PresentationLayout.CameraRect(new PresentationOverlay { x = 0, y = 1, size = "small" }, 1920, 77);

            Assert.AreEqual(Vector4.zero, rect);
        }

        private static PresentationBotMetadata ParseOrFail(string json)
        {
            PresentationBotMetadata? metadata = PresentationLayout.Parse(json);
            Assert.IsNotNull(metadata, json);
            return metadata!;
        }

        private static string WithPresenterIdentity(string identity) =>
            $"{{{SLIDE_1080},\"presenterIdentity\":\"{identity}\"}}";

        private static void AssertPixelRect(Vector4 rect, int width, int height, int left, int top, int diameter)
        {
            Assert.AreEqual(left, Mathf.RoundToInt(rect.x * width), "left");
            Assert.AreEqual(top, Mathf.RoundToInt(rect.y * height), "top");
            Assert.AreEqual(diameter, Mathf.RoundToInt(rect.z * width), "diameter / width");
            Assert.AreEqual(diameter, Mathf.RoundToInt(rect.w * height), "diameter / height");
        }
    }
}
