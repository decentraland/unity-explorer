#if UNITY_EDITOR_LINUX
using NUnit.Framework;

namespace DCL.VideoPlayback.Tests
{
    public class FfmpegCommandLineShould
    {
        private static MediaProbe VideoAndAudio() =>
            new (10d, new[] { ProbeStream.Video(0, 1280, 720, 30d), ProbeStream.Audio(1) });

        [Test]
        public void MapTheSelectedStreamsIntoBothPipes()
        {
            // Act
            string args = FfmpegCommandLine.PlaybackArguments("/tmp/clip.mp4", VideoAndAudio(), 0d, 1f, 48000, 2, "/tmp/v.fifo", "/tmp/a.fifo");

            // Assert
            StringAssert.StartsWith("-hide_banner -nostdin -nostats -loglevel info -y -i \"/tmp/clip.mp4\"", args);
            StringAssert.Contains("-map 0:0 -vf \"scale=w='min(iw,1920)':h='min(ih,1080)':force_original_aspect_ratio=decrease:force_divisible_by=2,showinfo\" -fps_mode passthrough -f image2pipe -c:v bmp -pix_fmt bgra \"/tmp/v.fifo\"", args);
            StringAssert.Contains("-map 0:1 -f f32le -ar 48000 -ac 2 \"/tmp/a.fifo\"", args);
            StringAssert.DoesNotContain("-ss", args);
            StringAssert.DoesNotContain("atempo", args);
            StringAssert.DoesNotContain("-reconnect", args);
        }

        [Test]
        public void SeekBeforeOpeningTheInput()
        {
            // Act
            string args = FfmpegCommandLine.PlaybackArguments("/tmp/clip.mp4", VideoAndAudio(), 12.5d, 1f, 48000, 2, "/tmp/v.fifo", "/tmp/a.fifo");

            // Assert
            StringAssert.Contains("-ss 12.500 -i \"/tmp/clip.mp4\"", args);
        }

        [Test]
        public void StretchAudioTempoForNonUnitRates()
        {
            Assert.AreEqual("atempo=2.000", FfmpegCommandLine.AtempoChain(2f));
            Assert.AreEqual("atempo=0.500,atempo=0.500", FfmpegCommandLine.AtempoChain(0.25f));
            Assert.AreEqual(string.Empty, FfmpegCommandLine.AtempoChain(1f));
            Assert.AreEqual("atempo=4.000", FfmpegCommandLine.AtempoChain(9f));
            StringAssert.Contains("-af \"atempo=1.500\"", FfmpegCommandLine.PlaybackArguments("/tmp/clip.mp4", VideoAndAudio(), 0d, 1.5f, 48000, 2, "/tmp/v.fifo", "/tmp/a.fifo"));
        }

        [Test]
        public void SkipTheAudioOutputWhenTheSourceHasNoAudio()
        {
            // Arrange
            var probe = new MediaProbe(10d, new[] { ProbeStream.Video(0, 1280, 720, 30d) });

            // Act
            string args = FfmpegCommandLine.PlaybackArguments("/tmp/clip.mp4", probe, 0d, 1f, 48000, 2, "/tmp/v.fifo", "/tmp/a.fifo");

            // Assert
            StringAssert.DoesNotContain("f32le", args);
            StringAssert.Contains("-map 0:0", args);
        }

        [Test]
        public void SkipTheVideoOutputForAudioOnlySources()
        {
            // Arrange
            var probe = new MediaProbe(10d, new[] { ProbeStream.Audio(0) });

            // Act
            string args = FfmpegCommandLine.PlaybackArguments("/tmp/clip.m4a", probe, 0d, 1f, 48000, 2, null, "/tmp/a.fifo");

            // Assert
            StringAssert.DoesNotContain("image2pipe", args);
            StringAssert.Contains("-map 0:0 -f f32le", args);
        }

        [Test]
        public void AddReconnectAndTimeoutOptionsForNetworkSources()
        {
            // Act
            string playback = FfmpegCommandLine.PlaybackArguments("https://cdn.example.com/live.m3u8", VideoAndAudio(), 0d, 1f, 48000, 2, "/tmp/v.fifo", "/tmp/a.fifo");
            string probe = FfmpegCommandLine.ProbeArguments("https://cdn.example.com/live.m3u8");

            // Assert
            StringAssert.Contains("-reconnect 1 -reconnect_streamed 1 -reconnect_on_network_error 1 -reconnect_delay_max 5 -rw_timeout 15000000 -i", playback);
            StringAssert.Contains("-rw_timeout 15000000 -i \"https://cdn.example.com/live.m3u8\"", probe);
            Assert.IsTrue(FfmpegCommandLine.IsNetworkSource("rtmp://host/app"));
            Assert.IsFalse(FfmpegCommandLine.IsNetworkSource("/var/media/clip.mp4"));
        }

        [Test]
        public void QuoteArgumentsForTheShellStyleSplitter()
        {
            Assert.AreEqual("\"plain\"", FfmpegCommandLine.Quote("plain"));
            Assert.AreEqual("\"a \\\"b\\\" $c\"", FfmpegCommandLine.Quote("a \"b\" $c"));
            Assert.AreEqual("\"back\\\\slash\"", FfmpegCommandLine.Quote("back\\slash"));
            Assert.AreEqual("\"https://h/p?x=1&y=2\"", FfmpegCommandLine.Quote("https://h/p?x=1&y=2"));
        }

        [Test]
        public void ClampRatesToTheSupportedSpan()
        {
            Assert.AreEqual(FfmpegCommandLine.MIN_RATE, FfmpegCommandLine.ClampRate(0f));
            Assert.AreEqual(FfmpegCommandLine.MAX_RATE, FfmpegCommandLine.ClampRate(100f));
            Assert.AreEqual(1f, FfmpegCommandLine.ClampRate(float.NaN));
            Assert.AreEqual(1.25f, FfmpegCommandLine.ClampRate(1.25f));
        }
    }
}
#endif
