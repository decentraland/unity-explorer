#if UNITY_EDITOR_LINUX
using NUnit.Framework;

namespace DCL.VideoPlayback.Tests
{
    public class MediaProbeShould
    {
        [Test]
        public void PickTheLargestRenditionWithinTheCap()
        {
            // Arrange
            ProbeStream[] streams =
            {
                ProbeStream.Video(0, 1024, 576, 30d),
                ProbeStream.Video(1, 1280, 720, 30d),
                ProbeStream.Video(2, 1920, 1080, 30d),
                ProbeStream.Video(3, 3840, 2160, 30d),
                ProbeStream.Audio(4),
            };

            // Act
            var probe = new MediaProbe(634d, streams);

            // Assert
            Assert.AreEqual(2, probe.VideoStream);
            Assert.AreEqual(4, probe.AudioStream);
            Assert.AreEqual(1920, probe.Width);
            Assert.AreEqual(1080, probe.Height);
            Assert.AreEqual(30d, probe.Fps);
            Assert.IsFalse(probe.IsLive);
        }

        [Test]
        public void FallBackToTheSmallestRenditionWhenAllExceedTheCap()
        {
            // Arrange
            ProbeStream[] streams =
            {
                ProbeStream.Video(0, 3840, 2160, 60d),
                ProbeStream.Video(1, 2560, 1440, 60d),
            };

            // Act
            int selected = MediaProbe.SelectVideo(streams, MediaProbe.MAX_WIDTH, MediaProbe.MAX_HEIGHT);

            // Assert
            Assert.AreEqual(1, selected);
        }

        [Test]
        public void ReportNoVideoForAudioOnlySources()
        {
            // Arrange
            ProbeStream[] streams = { ProbeStream.Audio(0) };

            // Act
            var probe = new MediaProbe(120d, streams);

            // Assert
            Assert.IsFalse(probe.HasVideo);
            Assert.IsTrue(probe.HasAudio);
            Assert.AreEqual(0, probe.AudioStream);
            Assert.AreEqual(MediaProbe.DEFAULT_FPS, probe.Fps);
        }

        [Test]
        public void TreatMissingDurationAsLive()
        {
            // Act
            var probe = new MediaProbe(0d, new[] { ProbeStream.Video(0, 1280, 720, 25d) });

            // Assert
            Assert.IsTrue(probe.IsLive);
            Assert.IsFalse(probe.HasAudio);
        }

        [Test]
        public void ReportProbeFailureForUnreadableSource()
        {
            // Arrange
            string? ffmpeg = FfmpegCommandLine.ResolveBinary(UnityEngine.Application.dataPath);
            if (ffmpeg == null) Assert.Ignore("ffmpeg is not available on this machine");

            // Act & Assert
            MediaOpenException error = Assert.Throws<MediaOpenException>(() => MediaProbe.Run(ffmpeg!, "/nonexistent/videoplayback-probe-test.mp4", 10000));
            StringAssert.Contains("found no streams", error.Message);
        }
    }
}
#endif
