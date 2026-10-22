using NUnit.Framework;
using UnityEngine;

namespace DCL.SDKComponents.MediaStream.Tests
{
    public class PresentationBotMetadataShould
    {
        private const string LEGACY_METADATA = "{\"role\":\"presentation\",\"presentationId\":\"p\",\"currentSlide\":2,\"overlay\":{\"x\":0,\"y\":1,\"size\":\"small\"}}";
        private const string SLIDE_1080 = "\"slide\":{\"url\":\"https://cast-presenter-service.decentraland.org/presentations/p/slides/ab12.png\",\"width\":1920,\"height\":1080}";
        private const string CENTERED_VIDEO = "\"slideVideos\":[{\"url\":\"v.mp4\",\"geometry\":{\"x\":480,\"y\":270,\"width\":960,\"height\":540}}]";
        private const string PRESENTER_ADDRESS = "0x0123456789abcdefABCDEF0123456789abcdef01";
        private const string YOUTUBE_URL = "https://www.youtube.com/watch?v=dQw4w9WgXcQ";

        [TestCase("{")]
        [TestCase("")]
        [TestCase(null)]
        [TestCase("{\"slide\":\"x\"}")]
        public void ReturnNull_WhenJsonIsMalformed(string? json)
        {
            // Act
            PresentationBotMetadata? metadata = PresentationBotMetadata.Parse(json);

            // Assert
            Assert.IsNull(metadata);
        }

        [Test]
        public void ParseLegacyMetadata_WhenSlideIsAbsent()
        {
            // Act
            PresentationBotMetadata? metadata = PresentationBotMetadata.Parse(LEGACY_METADATA);

            // Assert
            Assert.IsNotNull(metadata);
            Assert.IsNull(metadata!.slide);
            Assert.IsNotNull(metadata.overlay);
        }

        [Test]
        public void ParseV2Fields_WhenPresent()
        {
            // Arrange
            var json = $"{{{SLIDE_1080},\"presenterIdentity\":\"{PRESENTER_ADDRESS}\",\"playingVideoIndex\":0,{CENTERED_VIDEO}}}";

            // Act
            PresentationBotMetadata? metadata = PresentationBotMetadata.Parse(json);

            // Assert
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
            // Act
            PresentationBotMetadata? metadata = PresentationBotMetadata.Parse($"{{{SLIDE_1080},\"playingVideoIndex\":null}}");

            // Assert
            Assert.IsNotNull(metadata);
            Assert.IsFalse(metadata!.playingVideoIndex.HasValue);
        }

        [TestCase("{\"slide\":{\"url\":\"https://a/b.png\",\"width\":0,\"height\":1080}}")]
        [TestCase("{\"slide\":{\"width\":1920,\"height\":1080}}")]
        public void ReturnNull_WhenSlideHasNoSizeOrUrl(string json)
        {
            // Act
            PresentationBotMetadata? metadata = PresentationBotMetadata.Parse(json);

            // Assert
            Assert.IsNull(metadata);
        }

        [Test]
        public void ReturnNull_WhenSlideIsTooLarge()
        {
            // Act
            PresentationBotMetadata? tooLarge = PresentationBotMetadata.Parse("{\"slide\":{\"url\":\"https://a/b.png\",\"width\":2049,\"height\":1080}}");
            PresentationBotMetadata? atLimit = PresentationBotMetadata.Parse("{\"slide\":{\"url\":\"https://a/b.png\",\"width\":2048,\"height\":1080}}");

            // Assert
            Assert.IsNull(tooLarge);
            Assert.IsNotNull(atLimit);
        }

        [Test]
        public void ReturnNull_WhenSlideUrlIsTooLong()
        {
            // Arrange
            string url = "https://a/" + new string('a', PresentationBotMetadata.MAX_SLIDE_URL_LENGTH + 1 - "https://a/".Length);

            // Act
            PresentationBotMetadata? metadata = PresentationBotMetadata.Parse($"{{\"slide\":{{\"url\":\"{url}\",\"width\":1920,\"height\":1080}}}}");

            // Assert
            Assert.IsNull(metadata);
        }

        [TestCase("0xOTHER", 0)]
        [TestCase("stream:", 193)]
        [TestCase("0x0123456789abcdef0123456789abcdef01234567\\n", 0)]
        public void DropPresenterIdentity_WhenFormatIsInvalid(string prefix, int padding)
        {
            // Act
            PresentationBotMetadata metadata = ParseOrFail(WithPresenterIdentity(prefix + new string('a', padding)));

            // Assert
            Assert.IsNull(metadata.presenterIdentity);
        }

        [TestCase("stream:place:1", 0)]
        [TestCase("0x", 40)]
        public void KeepPresenterIdentity_WhenFormatIsValid(string prefix, int padding)
        {
            // Arrange
            string identity = prefix + new string('a', padding);

            // Act
            PresentationBotMetadata metadata = ParseOrFail(WithPresenterIdentity(identity));

            // Assert
            Assert.AreEqual(identity, metadata.presenterIdentity);
        }

        [TestCase("\"x\":\"NaN\",\"y\":1")]
        [TestCase("\"x\":0,\"y\":\"Infinity\"")]
        public void DropOverlay_WhenCoordinatesAreNotFinite(string coordinates)
        {
            // Act
            PresentationBotMetadata metadata = ParseOrFail($"{{{SLIDE_1080},\"overlay\":{{{coordinates},\"size\":\"small\"}}}}");

            // Assert
            Assert.IsNull(metadata.overlay);
        }

        [Test]
        public void ParseInitialMetadata()
        {
            // Act
            PresentationBotMetadata? metadata = PresentationBotMetadata.Parse("{\"role\":\"presentation\",\"presentationId\":\"p\"}");

            // Assert
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
            // Arrange
            PresentationBotMetadata metadata = ParseOrFail($"{{{SLIDE_1080},\"videoState\":\"{state}\",\"playingVideoIndex\":0,{CENTERED_VIDEO}}}");

            // Act
            bool found = metadata.TryVideoRect(out Vector4 rect);

            // Assert
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
            // Arrange
            PresentationBotMetadata metadata = ParseOrFail($"{{{SLIDE_1080},{fields}}}");

            // Act
            bool found = metadata.TryVideoRect(out _);

            // Assert
            Assert.IsFalse(found);
        }

        [TestCase(null)]
        [TestCase("small")]
        [TestCase("bogus")]
        public void ComputeCameraRect_ForDefaultOverlay(string? size)
        {
            // Act
            Vector4 rect = new PresentationBotMetadata { overlay = new PresentationOverlay { x = 0, y = 1, size = size } }.CameraRect(1920, 1080);

            // Assert
            AssertPixelRect(rect, 1920, 1080, 38, 754, 288);
        }

        [Test]
        public void ComputeCameraRect_ForLargeTopRight()
        {
            // Act
            Vector4 rect = new PresentationBotMetadata { overlay = new PresentationOverlay { x = 1, y = 0, size = "large" } }.CameraRect(1920, 1080);

            // Assert
            AssertPixelRect(rect, 1920, 1080, 1402, 38, 480);
        }

        [Test]
        public void ComputeCameraRect_ForPortraitSlide()
        {
            // Act
            Vector4 rect = new PresentationBotMetadata { overlay = new PresentationOverlay { x = 0.5, y = 0.5, size = "small" } }.CameraRect(1080, 1920);

            // Assert
            AssertPixelRect(rect, 1080, 1920, 458, 878, 162);
        }

        [Test]
        public void ComputeCameraRect_For720pLargeBottomRight()
        {
            // Act
            Vector4 rect = new PresentationBotMetadata { overlay = new PresentationOverlay { x = 1, y = 1, size = "large" } }.CameraRect(1280, 720);

            // Assert
            AssertPixelRect(rect, 1280, 720, 934, 374, 320);
        }

        [Test]
        public void ReturnZeroCameraRect_WhenDiameterBelowTwo()
        {
            // Act
            Vector4 rect = new PresentationBotMetadata { overlay = new PresentationOverlay { x = 0, y = 1, size = "small" } }.CameraRect(1920, 77);

            // Assert
            Assert.AreEqual(Vector4.zero, rect);
        }

        [Test]
        public void ParseClientVideoFields_WhenPresent()
        {
            // Act
            PresentationBotMetadata metadata = ParseOrFail(WithClientVideo("playing", "client", YOUTUBE_URL) + ",\"videoPositionMs\":1500,\"videoPositionAt\":1700000000000}");

            // Assert
            Assert.AreEqual("playing", metadata.videoState);
            Assert.AreEqual(1500L, metadata.videoPositionMs);
            Assert.AreEqual(1700000000000L, metadata.videoPositionAt);
            Assert.AreEqual(YOUTUBE_URL, metadata.slideVideos![0]!.url);
            Assert.AreEqual("client", metadata.slideVideos[0]!.source);
        }

        [Test]
        public void DropVideoPosition_WhenNegative()
        {
            // Act
            PresentationBotMetadata metadata = ParseOrFail($"{{{SLIDE_1080},\"videoPositionMs\":-1,\"videoPositionAt\":-5}}");

            // Assert
            Assert.IsFalse(metadata.videoPositionMs.HasValue);
            Assert.IsFalse(metadata.videoPositionAt.HasValue);
        }

        [Test]
        public void DropVideoPosition_WhenBeyondRange()
        {
            // Act
            PresentationBotMetadata metadata = ParseOrFail($"{{{SLIDE_1080},\"videoPositionMs\":9223372036854775807,\"videoPositionAt\":100000000000001}}");

            // Assert
            Assert.IsFalse(metadata.videoPositionMs.HasValue);
            Assert.IsFalse(metadata.videoPositionAt.HasValue);
        }

        [TestCase("playing", YOUTUBE_URL)]
        [TestCase("paused", "https://drive.google.com/file/d/1AbC-dEf_GhIjKlMnOp/view")]
        [TestCase("playing", "https://player.vimeo.com/external/123456.m3u8?s=abc&logging=false")]
        [TestCase("paused", "https://player.vimeo.com/external/123456.m3u8")]
        public void ReturnClientVideoUrl_WhenUrlIsCanonical(string state, string url)
        {
            // Arrange
            PresentationBotMetadata metadata = ParseOrFail(WithClientVideo(state, "client", url) + "}");

            // Act
            bool found = metadata.TryClientVideoUrl(out string clientUrl);

            // Assert
            Assert.IsTrue(found);
            Assert.AreEqual(url, clientUrl);
        }

        [TestCase("http://www.youtube.com/watch?v=dQw4w9WgXcQ")]
        [TestCase("https://youtube.com/watch?v=dQw4w9WgXcQ")]
        [TestCase("https://www.youtube.com/watch?v=dQw4w9WgXcQQ")]
        [TestCase("https://drive.google.com/file/d/1AbC-dEf_GhIjKlMnOp")]
        [TestCase("https://player.vimeo.com/video/1")]
        [TestCase("https://player.vimeo.com/external/123456.m3u8?s=abc#t=1")]
        [TestCase("https://www.youtube.com/watch?v=dQw4w9WgXcQ#t=1")]
        [TestCase("https://www.youtube.com.evil.com/watch?v=dQw4w9WgXcQ")]
        [TestCase("https://player.vimeo.com/external/123456.m3u8?s=a\\u0000b")]
        [TestCase("https://player.vimeo.com/external/123456.m3u8?s=\\u202Eabc")]
        [TestCase("https://player.vimeo.com/external/123456.m3u8?")]
        public void ReturnNoClientVideoUrl_WhenUrlIsNotCanonical(string url)
        {
            // Arrange
            PresentationBotMetadata metadata = ParseOrFail(WithClientVideo("playing", "client", url) + "}");

            // Act
            bool found = metadata.TryClientVideoUrl(out string clientUrl);

            // Assert
            Assert.IsFalse(found);
            Assert.AreEqual(string.Empty, clientUrl);
        }

        [TestCase(512, true)]
        [TestCase(513, false)]
        public void ReturnClientVideoUrl_OnlyWhenVimeoQueryIsAtMost512Chars(int queryLength, bool expected)
        {
            // Arrange
            string url = "https://player.vimeo.com/external/123456.m3u8?" + new string('a', queryLength);
            PresentationBotMetadata metadata = ParseOrFail(WithClientVideo("playing", "client", url) + "}");

            // Act
            bool found = metadata.TryClientVideoUrl(out _);

            // Assert
            Assert.AreEqual(expected, found);
        }

        [TestCase("{\"videoState\":\"playing\",\"playingVideoIndex\":0,\"slideVideos\":[{\"url\":\"" + YOUTUBE_URL + "\",\"source\":\"stream\"}]}")]
        [TestCase("{\"videoState\":\"playing\",\"playingVideoIndex\":0,\"slideVideos\":[{\"url\":\"" + YOUTUBE_URL + "\"}]}")]
        [TestCase("{\"videoState\":\"idle\",\"playingVideoIndex\":0,\"slideVideos\":[{\"url\":\"" + YOUTUBE_URL + "\",\"source\":\"client\"}]}")]
        [TestCase("{\"videoState\":\"loading\",\"playingVideoIndex\":0,\"slideVideos\":[{\"url\":\"" + YOUTUBE_URL + "\",\"source\":\"client\"}]}")]
        [TestCase("{\"videoState\":\"playing\",\"playingVideoIndex\":1,\"slideVideos\":[{\"url\":\"" + YOUTUBE_URL + "\",\"source\":\"client\"}]}")]
        [TestCase("{\"videoState\":\"playing\",\"playingVideoIndex\":0,\"slideVideos\":[null]}")]
        public void ReturnNoClientVideoUrl_WhenVideoIsNotAPlayingClientVideo(string json)
        {
            // Arrange
            PresentationBotMetadata metadata = ParseOrFail(json);

            // Act
            bool found = metadata.TryClientVideoUrl(out string clientUrl);

            // Assert
            Assert.IsFalse(found);
            Assert.AreEqual(string.Empty, clientUrl);
        }

        [TestCase("playing", 12_500L, 3.5)]
        [TestCase("paused", 99_999L, 1.0)]
        [TestCase("playing", 10_000L, 1.0)]
        public void ComputeTargetPosition_FromAnchor(string state, long nowEpochMs, double expectedSeconds)
        {
            // Arrange
            var metadata = new PresentationBotMetadata { videoState = state, videoPositionMs = 1_000, videoPositionAt = 10_000 };

            // Act
            double? target = metadata.TargetPositionSeconds(nowEpochMs);

            // Assert
            Assert.AreEqual(expectedSeconds, target);
        }

        [Test]
        public void ReturnNoTargetPosition_WhenAnchorIsMissing()
        {
            // Arrange
            var metadata = new PresentationBotMetadata { videoState = "playing", videoPositionMs = 1_000 };

            // Act & Assert
            Assert.IsNull(metadata.TargetPositionSeconds(12_500));
        }

        [Test]
        public void ReturnNoTargetPosition_WhenVideoIsLoading()
        {
            // Arrange
            var metadata = new PresentationBotMetadata { videoState = "loading", videoPositionMs = 1_000, videoPositionAt = 10_000 };

            // Act & Assert
            Assert.IsNull(metadata.TargetPositionSeconds(12_500));
        }

        [Test]
        public void ReturnNoTargetPosition_WhenAnchorIsInTheFuture()
        {
            // Arrange
            var metadata = new PresentationBotMetadata { videoState = "playing", videoPositionMs = 1_000, videoPositionAt = 10_000 };

            // Act
            double? target = metadata.TargetPositionSeconds(8_000);

            // Assert
            Assert.IsNull(target);
        }

        [TestCase("playing", true)]
        [TestCase("paused", false)]
        [TestCase("loading", false)]
        [TestCase(null, false)]
        public void ReportVideoPlaying_OnlyWhilePlaying(string? state, bool expected)
        {
            // Arrange
            var metadata = new PresentationBotMetadata { videoState = state };

            // Act
            bool playing = metadata.IsVideoPlaying();

            // Assert
            Assert.AreEqual(expected, playing);
        }

        [TestCase("client", 0, true)]
        [TestCase("stream", 0, false)]
        [TestCase(null, 0, false)]
        [TestCase("client", 1, false)]
        [TestCase("client", -1, false)]
        public void DetectClientEntry_AtPlayingVideoIndex_WhateverTheState(string? source, int index, bool expected)
        {
            // Arrange
            var metadata = new PresentationBotMetadata
            {
                videoState = "loading",
                playingVideoIndex = index,
                slideVideos = new PresentationSlideVideo?[] { new () { source = source } },
            };

            // Act
            bool isClient = metadata.IsClientEntry();

            // Assert
            Assert.AreEqual(expected, isClient);
        }

        [Test]
        public void DetectNoClientEntry_WhenNoVideoIsPlaying()
        {
            // Arrange
            var metadata = new PresentationBotMetadata { slideVideos = new PresentationSlideVideo?[] { new () { source = "client" } } };

            // Act
            bool isClient = metadata.IsClientEntry();

            // Assert
            Assert.IsFalse(isClient);
        }

        private static string WithClientVideo(string state, string source, string url) =>
            $"{{{SLIDE_1080},\"videoState\":\"{state}\",\"playingVideoIndex\":0,\"slideVideos\":[{{\"url\":\"{url}\",\"source\":\"{source}\",\"geometry\":{{\"x\":480,\"y\":270,\"width\":960,\"height\":540}}}}]";

        private static PresentationBotMetadata ParseOrFail(string json)
        {
            PresentationBotMetadata? metadata = PresentationBotMetadata.Parse(json);
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
