#if UNITY_EDITOR_LINUX
using NUnit.Framework;

namespace DCL.VideoPlayback.Tests
{
    public class FfmpegLogParserShould
    {
        private const string SHOW_INFO_LINE =
            "[Parsed_showinfo_1 @ 0x7fb614010cc0] n:   7 pts:   3584 pts_time:0.28    duration:    512 duration_time:0.04    fmt:bgra cl:unspecified sar:1/1 s:640x360 i:P iskey:0 type:P checksum:9622D387 plane_checksum:[9622D387] mean:[158] stdev:[121.1]";

        private const string VIDEO_STREAM_LINE =
            "  Stream #0:0[0x1](und): Video: h264 (Constrained Baseline) (avc1 / 0x31637661), yuv420p(progressive), 640x360 [SAR 1:1 DAR 16:9], 1761 kb/s, 25 fps, 25 tbr, 12800 tbn (default)";

        private const string DASH_VIDEO_STREAM_LINE =
            "  Stream #0:9: Video: h264 (High) (avc1 / 0x31637661), yuv420p(progressive), 3840x2160 [SAR 1:1 DAR 16:9], 4578 kb/s, 30 fps, 30 tbr, 30 tbn (default)";

        private const string TS_VIDEO_STREAM_LINE =
            "  Stream #0:0[0x0]: Video: h264 (Constrained Baseline) ([27][0][0][0] / 0x001B), yuv420p, 640x360 [SAR 1:1 DAR 16:9], 25 fps, 25 tbr, 90k tbn, start 1.423222";

        private const string AUDIO_STREAM_LINE =
            "  Stream #0:1[0x2](und): Audio: aac (LC) (mp4a / 0x6134706D), 44100 Hz, mono, fltp, 69 kb/s (default)";

        [Test]
        public void ParseShowInfoFrameIndexTimeAndSize()
        {
            // Act
            bool parsed = FfmpegLogParser.TryParseShowInfo(SHOW_INFO_LINE, out FfmpegLogParser.ShowInfo info);

            // Assert
            Assert.IsTrue(parsed);
            Assert.AreEqual(7, info.Index);
            Assert.IsTrue(info.HasTime);
            Assert.AreEqual(0.28, info.Time, 1e-9);
            Assert.AreEqual(640, info.Width);
            Assert.AreEqual(360, info.Height);
        }

        [Test]
        public void FlagShowInfoWithoutTimestamp()
        {
            // Arrange
            const string LINE = "[Parsed_showinfo_1 @ 0x1] n:   3 pts:    N/A pts_time:N/A     duration: N/A fmt:bgra sar:1/1 s:320x180 i:P";

            // Act
            bool parsed = FfmpegLogParser.TryParseShowInfo(LINE, out FfmpegLogParser.ShowInfo info);

            // Assert
            Assert.IsTrue(parsed);
            Assert.AreEqual(3, info.Index);
            Assert.IsFalse(info.HasTime);
            Assert.AreEqual(320, info.Width);
        }

        [Test]
        public void IgnoreLinesThatAreNotShowInfo()
        {
            Assert.IsFalse(FfmpegLogParser.TryParseShowInfo(VIDEO_STREAM_LINE, out _));
            Assert.IsFalse(FfmpegLogParser.TryParseShowInfo("[Parsed_showinfo_1 @ 0x1] config in time_base: 1/12800, frame_rate: 25/1", out _));
        }

        [Test]
        public void ParseDurationInSeconds()
        {
            // Act
            bool parsed = FfmpegLogParser.TryParseDuration("  Duration: 00:10:34.50, start: 0.000000, bitrate: 0 kb/s", out double seconds);

            // Assert
            Assert.IsTrue(parsed);
            Assert.AreEqual(634.5, seconds, 1e-9);
        }

        [Test]
        public void ReportZeroDurationForLiveSources()
        {
            // Act
            bool parsed = FfmpegLogParser.TryParseDuration("  Duration: N/A, start: 0.000000, bitrate: N/A", out double seconds);

            // Assert
            Assert.IsTrue(parsed);
            Assert.AreEqual(0d, seconds);
        }

        [Test]
        public void ParseVideoStreamWithResolutionAndFrameRate()
        {
            // Act
            bool parsed = FfmpegLogParser.TryParseStream(VIDEO_STREAM_LINE, out ProbeStream stream);

            // Assert
            Assert.IsTrue(parsed);
            Assert.IsTrue(stream.IsVideo);
            Assert.AreEqual(0, stream.Index);
            Assert.AreEqual(640, stream.Width);
            Assert.AreEqual(360, stream.Height);
            Assert.AreEqual(25d, stream.Fps, 1e-9);
        }

        [Test]
        public void NotMistakeHexCodecTagsForResolution()
        {
            // Act
            FfmpegLogParser.TryParseStream(DASH_VIDEO_STREAM_LINE, out ProbeStream dash);
            FfmpegLogParser.TryParseStream(TS_VIDEO_STREAM_LINE, out ProbeStream ts);

            // Assert
            Assert.AreEqual(9, dash.Index);
            Assert.AreEqual(3840, dash.Width);
            Assert.AreEqual(2160, dash.Height);
            Assert.AreEqual(640, ts.Width);
            Assert.AreEqual(360, ts.Height);
        }

        [Test]
        public void ParseAudioStreamIndex()
        {
            // Act
            bool parsed = FfmpegLogParser.TryParseStream(AUDIO_STREAM_LINE, out ProbeStream stream);

            // Assert
            Assert.IsTrue(parsed);
            Assert.IsFalse(stream.IsVideo);
            Assert.AreEqual(1, stream.Index);
        }

        [Test]
        public void IgnoreOutputStreamLines()
        {
            Assert.IsFalse(FfmpegLogParser.TryParseStream("  Stream #1:0(und): Audio: pcm_f32le, 48000 Hz, stereo, flt, 3072 kb/s (default)", out _));
            Assert.IsFalse(FfmpegLogParser.TryParseStream("Stream mapping:", out _));
        }
    }
}
#endif
