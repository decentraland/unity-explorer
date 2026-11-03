using DCL.SDKComponents.MediaStream.YouTube;
using NUnit.Framework;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace DCL.SDKComponents.MediaStream.Tests
{
    public class HlsManifestBuilderShould
    {
        private const string VIDEO_URL = "https://example.com/v.mp4";
        private const string AUDIO_URL = "https://example.com/a.mp4";

        private static readonly AdaptiveFormatData VIDEO = new (137, VIDEO_URL, "video/mp4; codecs=\"avc1.640028\"", 4_000_000,
            1920, 1080, 30, null, null, 0, 739, 740, 1999, 10_000_000);

        private static readonly AdaptiveFormatData AUDIO = new (140, AUDIO_URL, "audio/mp4; codecs=\"mp4a.40.2\"", 128_000,
            null, null, null, 44100, 2, 0, 631, 632, 1499, 1_000_000);

        [Test]
        public void BuildCoalescedSegmentedPlaylists()
        {
            // Arrange: 6 × 5s fragments with a 10s target ⇒ 3 segments per media playlist
            List<SidxParser.SegmentInfo> video = Fragments(6, 2000, 1000, 5);
            List<SidxParser.SegmentInfo> audio = Fragments(6, 1500, 100, 5);

            // Act
            HlsManifestBuilder.PlaylistSet set = HlsManifestBuilder.Build(VIDEO, AUDIO, 30, video, audio, 10f);

            // Assert
            Assert.That(CountByteRanges(set.Video), Is.EqualTo(3));
            Assert.That(CountByteRanges(set.Audio), Is.EqualTo(3));
            StringAssert.Contains($"#EXT-X-MAP:URI=\"{VIDEO_URL}\",BYTERANGE=\"740@0\"\n", set.Video);
            StringAssert.Contains("#EXT-X-BYTERANGE:2000@2000\n", set.Video);
            StringAssert.Contains("#EXT-X-BYTERANGE:2000@6000\n", set.Video);
            StringAssert.Contains("#EXT-X-BYTERANGE:200@1500\n", set.Audio);
        }

        [Test]
        public void FallBackToSingleSegmentWhenEitherSidxIsEmpty()
        {
            // Arrange
            List<SidxParser.SegmentInfo> video = Fragments(6, 2000, 1000, 5);
            var audio = new List<SidxParser.SegmentInfo>();

            // Act
            HlsManifestBuilder.PlaylistSet set = HlsManifestBuilder.Build(VIDEO, AUDIO, 30, video, audio, 10f);

            // Assert: one segment spanning from the end of the index range to the end of the file
            Assert.That(CountByteRanges(set.Video), Is.EqualTo(1));
            Assert.That(CountByteRanges(set.Audio), Is.EqualTo(1));
            StringAssert.Contains("#EXT-X-BYTERANGE:9998000@2000\n", set.Video);
            StringAssert.Contains("#EXT-X-BYTERANGE:998500@1500\n", set.Audio);
        }

        private static List<SidxParser.SegmentInfo> Fragments(int count, long firstOffset, long size, double duration)
        {
            var fragments = new List<SidxParser.SegmentInfo>(count);

            for (var i = 0; i < count; i++)
                fragments.Add(new SidxParser.SegmentInfo(firstOffset + (i * size), size, duration));

            return fragments;
        }

        private static int CountByteRanges(string playlist) =>
            Regex.Matches(playlist, "#EXT-X-BYTERANGE:").Count;
    }
}
