#if UNITY_EDITOR_LINUX
using NUnit.Framework;
using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using UnityEngine;
using UnityEngine.TestTools;

namespace DCL.VideoPlayback.Tests
{
    /// <summary>
    /// Drives the Linux backend against a real ffmpeg on synthetic clips
    /// generated at fixture time. Skipped where no ffmpeg is installed.
    /// </summary>
    public class LinuxMediaPlayerShould
    {
        private const double OPEN_TIMEOUT_SECONDS = 15d;
        private const int FIXTURE_TIMEOUT_MS = 60000;

        private static string ffmpeg = null!;
        private static string fixtureDir = null!;
        private static string clip3s = null!;
        private static string clip1s = null!;
        private static string dashManifest = null!;
        private static string hlsPlaylist = null!;
        private static string audioOnly = null!;
        private static string garbage = null!;

        private LinuxMediaPlayer player = null!;

        [OneTimeSetUp]
        public void GenerateFixtures()
        {
            string? binary = FfmpegCommandLine.ResolveBinary(Application.dataPath);
            if (binary == null) Assert.Ignore("ffmpeg is not available on this machine");
            ffmpeg = binary!;

            fixtureDir = Path.Combine(Path.GetTempPath(), $"videoplayback-linux-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path.Combine(fixtureDir, "dash"));
            Directory.CreateDirectory(Path.Combine(fixtureDir, "hls"));

            clip3s = Path.Combine(fixtureDir, "clip3s.mp4");
            clip1s = Path.Combine(fixtureDir, "clip1s.mp4");
            dashManifest = Path.Combine(fixtureDir, "dash", "clip.mpd");
            hlsPlaylist = Path.Combine(fixtureDir, "hls", "clip.m3u8");
            audioOnly = Path.Combine(fixtureDir, "audio.m4a");
            garbage = Path.Combine(fixtureDir, "garbage.mp4");

            RunFfmpeg($"-y -f lavfi -i testsrc2=duration=3:size=640x360:rate=25 -f lavfi -i sine=frequency=440:duration=3 -c:v mpeg4 -q:v 5 -pix_fmt yuv420p -c:a aac -shortest \"{clip3s}\"");
            RunFfmpeg($"-y -f lavfi -i testsrc2=duration=1:size=320x180:rate=25 -f lavfi -i sine=frequency=440:duration=1 -c:v mpeg4 -q:v 5 -pix_fmt yuv420p -c:a aac -shortest \"{clip1s}\"");
            RunFfmpeg($"-y -i \"{clip3s}\" -c copy -f dash -seg_duration 1 \"{dashManifest}\"");
            RunFfmpeg($"-y -i \"{clip3s}\" -c:v mpeg2video -q:v 5 -c:a aac -f hls -hls_time 1 -hls_list_size 0 \"{hlsPlaylist}\"");
            RunFfmpeg($"-y -f lavfi -i sine=frequency=440:duration=2 -c:a aac \"{audioOnly}\"");

            var noise = new byte[4096];
            for (var i = 0; i < noise.Length; i++) noise[i] = (byte)(i * 31 + 7);
            File.WriteAllBytes(garbage, noise);
        }

        [OneTimeTearDown]
        public void DeleteFixtures()
        {
            if (fixtureDir != null && Directory.Exists(fixtureDir))
                Directory.Delete(fixtureDir, true);
        }

        [SetUp]
        public void SetUp()
        {
            player = new LinuxMediaPlayer();
        }

        [TearDown]
        public void TearDown()
        {
            player.Dispose();
        }

        [Test]
        public void ReportDurationNativeResolutionAndFirstFrameAfterOpening()
        {
            // Act
            bool accepted = player.OpenMedia(clip3s, true);
            bool framed = Pump(() => player.Texture != null, OPEN_TIMEOUT_SECONDS);

            // Assert
            Assert.IsTrue(accepted);
            Assert.IsTrue(framed, "no frame arrived");
            Assert.IsTrue(player.IsOpen);
            Assert.AreEqual(ErrorCode.None, player.LastError);
            Assert.AreEqual(3d, player.Duration, 0.1d);
            Assert.AreEqual(640, player.Texture!.width);
            Assert.AreEqual(360, player.Texture.height);
            Assert.IsFalse(player.IsBuffering);
            Assert.IsFalse(player.IsSeeking);
            Assert.IsTrue(player.IsPlaying);
        }

        [Test]
        public void ReportBufferingUntilTheFirstFrameArrives()
        {
            // Act
            player.OpenMedia(clip3s, true);
            bool bufferingWhileOpening = player.IsBuffering;
            Pump(() => player.Texture != null, OPEN_TIMEOUT_SECONDS);

            // Assert
            Assert.IsTrue(bufferingWhileOpening);
            Assert.IsFalse(player.IsBuffering);
        }

        [Test]
        public void AdvanceTimeWhilePlayingAndHoldItWhilePaused()
        {
            // Arrange
            player.OpenMedia(clip3s, true);
            Assert.IsTrue(Pump(() => player.Texture != null, OPEN_TIMEOUT_SECONDS));

            // Act
            PumpFor(0.4d);
            double whilePlaying = player.CurrentTimeSeconds;
            player.Pause();
            double atPause = player.CurrentTimeSeconds;
            PumpFor(0.3d);
            double afterPausedWait = player.CurrentTimeSeconds;
            player.Play();
            PumpFor(0.3d);
            double afterResume = player.CurrentTimeSeconds;

            // Assert
            Assert.Greater(whilePlaying, 0.2d);
            Assert.IsTrue(player.IsPlaying);
            Assert.AreEqual(atPause, afterPausedWait, 0.001d);
            Assert.Greater(afterResume, atPause + 0.15d);
        }

        [Test]
        public void ExposePausedStateAndKeepTheDecoderHeld()
        {
            // Arrange
            player.OpenMedia(clip3s, true);
            Assert.IsTrue(Pump(() => player.Texture != null, OPEN_TIMEOUT_SECONDS));

            // Act
            player.Pause();
            PumpFor(0.2d);

            // Assert
            Assert.IsTrue(player.IsPaused);
            Assert.IsFalse(player.IsPlaying);
            Assert.IsFalse(player.IsBuffering);
            Assert.IsFalse(player.IsFinished);
        }

        [Test]
        public void SeekToTheRequestedPosition()
        {
            // Arrange
            player.OpenMedia(clip3s, true);
            Assert.IsTrue(Pump(() => player.Texture != null, OPEN_TIMEOUT_SECONDS));

            // Act
            player.Seek(2d);
            bool seekingReported = player.IsSeeking;
            bool settled = Pump(() => !player.IsSeeking, OPEN_TIMEOUT_SECONDS);

            // Assert
            Assert.IsTrue(seekingReported);
            Assert.IsTrue(settled, "seek never settled");
            Assert.AreEqual(2d, player.CurrentTimeSeconds, 0.15d);
            Assert.AreEqual(ErrorCode.None, player.LastError);
            Assert.IsTrue(player.IsPlaying);
        }

        [Test]
        public void ShowTheSoughtFrameWhilePaused()
        {
            // Arrange
            player.OpenMedia(clip3s, false);
            Assert.IsTrue(Pump(() => player.Texture != null, OPEN_TIMEOUT_SECONDS));

            // Act
            player.Seek(1.5d);
            bool settled = Pump(() => !player.IsSeeking, OPEN_TIMEOUT_SECONDS);

            // Assert
            Assert.IsTrue(settled);
            Assert.IsTrue(player.IsPaused);
            Assert.AreEqual(1.5d, player.CurrentTimeSeconds, 0.15d);
        }

        [Test]
        public void ApplyAPositionRequestedBeforeTheOpenCompletes()
        {
            // Act
            player.OpenMedia(clip3s, true);
            player.Seek(2d);
            bool settled = Pump(() => player.Texture != null && !player.IsSeeking, OPEN_TIMEOUT_SECONDS);

            // Assert
            Assert.IsTrue(settled);
            Assert.AreEqual(2d, player.CurrentTimeSeconds, 0.2d);
        }

        [Test]
        public void LoopAtTheEndOfTheStream()
        {
            // Arrange
            player.OpenMedia(clip1s, true);
            player.IsLooping = true;
            Assert.IsTrue(Pump(() => player.Texture != null, OPEN_TIMEOUT_SECONDS));

            // Act
            var wrapped = false;
            var maxTime = 0d;
            double previous = player.CurrentTimeSeconds;
            var stopwatch = Stopwatch.StartNew();

            while (stopwatch.Elapsed.TotalSeconds < 3.5d)
            {
                player.UpdateTexture();
                double now = player.CurrentTimeSeconds;
                if (now < previous - 0.3d) wrapped = true;
                if (now > maxTime) maxTime = now;
                previous = now;
                Thread.Sleep(5);
            }

            // Assert
            Assert.IsTrue(wrapped, "time never wrapped back to the start");
            Assert.IsFalse(player.IsFinished);
            Assert.IsTrue(player.IsPlaying);
            Assert.LessOrEqual(maxTime, 1.1d);
            Assert.AreEqual(ErrorCode.None, player.LastError);
        }

        [Test]
        public void FinishAtTheEndOfTheStreamWhenNotLooping()
        {
            // Arrange
            player.OpenMedia(clip1s, true);

            // Act
            bool finished = Pump(() => player.IsFinished, OPEN_TIMEOUT_SECONDS);

            // Assert
            Assert.IsTrue(finished, "stream never finished");
            Assert.IsFalse(player.IsPlaying);
            Assert.IsFalse(player.IsBuffering);
            Assert.AreEqual(1d, player.CurrentTimeSeconds, 0.15d);
            Assert.AreEqual(ErrorCode.None, player.LastError);
        }

        [Test]
        public void RestartFromTheBeginningWhenPlayedAfterFinishing()
        {
            // Arrange
            player.OpenMedia(clip1s, true);
            Assert.IsTrue(Pump(() => player.IsFinished, OPEN_TIMEOUT_SECONDS));

            // Act
            player.Play();
            bool restarted = Pump(() => player.IsPlaying && !player.IsSeeking && player.CurrentTimeSeconds < 0.5d, OPEN_TIMEOUT_SECONDS);

            // Assert
            Assert.IsTrue(restarted);
            Assert.IsFalse(player.IsFinished);
        }

        [Test]
        public void StopRewindsAndPauses()
        {
            // Arrange
            player.OpenMedia(clip3s, true);
            Assert.IsTrue(Pump(() => player.Texture != null, OPEN_TIMEOUT_SECONDS));
            PumpFor(0.5d);

            // Act
            player.Stop();
            bool settled = Pump(() => !player.IsSeeking, OPEN_TIMEOUT_SECONDS);

            // Assert
            Assert.IsTrue(settled);
            Assert.IsTrue(player.IsPaused);
            Assert.IsFalse(player.IsPlaying);
            Assert.IsFalse(player.IsFinished);
            Assert.AreEqual(0d, player.CurrentTimeSeconds, 0.05d);
            Assert.IsTrue(player.IsOpen);
        }

        [Test]
        public void PlayADashManifest()
        {
            // Act
            bool accepted = player.OpenMedia(dashManifest, true);
            bool framed = Pump(() => player.Texture != null, OPEN_TIMEOUT_SECONDS);

            // Assert
            Assert.IsTrue(accepted);
            Assert.IsTrue(framed, "no frame arrived from the DASH manifest");
            Assert.AreEqual(3d, player.Duration, 0.2d);
            Assert.AreEqual(640, player.Texture!.width);
            Assert.AreEqual(ErrorCode.None, player.LastError);
        }

        [Test]
        public void PlayAnHlsPlaylist()
        {
            // Act
            bool accepted = player.OpenMedia(hlsPlaylist, true);
            bool framed = Pump(() => player.Texture != null, OPEN_TIMEOUT_SECONDS);

            // Assert
            Assert.IsTrue(accepted);
            Assert.IsTrue(framed, "no frame arrived from the HLS playlist");
            Assert.AreEqual(3d, player.Duration, 0.2d);
            Assert.AreEqual(640, player.Texture!.width);
            Assert.AreEqual(ErrorCode.None, player.LastError);
        }

        [Test]
        public void PlayAnAudioOnlySource()
        {
            // Arrange
            var buffer = new float[2048];
            var heard = false;

            // Act
            bool accepted = player.OpenMedia(audioOnly, true);
            bool ready = Pump(() => player.IsOpen && !player.IsBuffering, OPEN_TIMEOUT_SECONDS);
            var stopwatch = Stopwatch.StartNew();

            while (stopwatch.Elapsed.TotalSeconds < 1d && !heard)
            {
                player.UpdateTexture();
                player.ReadAudioSamples(buffer, 2);

                foreach (float sample in buffer)
                    if (Math.Abs(sample) > 0.05f)
                        heard = true;

                Thread.Sleep(10);
            }

            // Assert
            Assert.IsTrue(accepted);
            Assert.IsTrue(ready, "audio-only source never became ready");
            Assert.IsNull(player.Texture);
            Assert.IsTrue(heard, "no audible samples were drained");
            Assert.AreEqual(2d, player.Duration, 0.2d);
        }

        [Test]
        public void FeedAudioSamplesAndFollowTheAudioClock()
        {
            // Arrange
            var buffer = new float[1024];
            var heard = false;
            long consumedFrames = 0;
            player.OpenMedia(clip3s, true);
            Assert.IsTrue(Pump(() => player.Texture != null, OPEN_TIMEOUT_SECONDS));
            int sampleRate = AudioSettings.GetConfiguration().sampleRate;
            if (sampleRate <= 0) sampleRate = 48000;

            // Act: stand in for the mixer at roughly real time.
            var stopwatch = Stopwatch.StartNew();

            while (stopwatch.Elapsed.TotalSeconds < 0.6d)
            {
                player.UpdateTexture();
                player.ReadAudioSamples(buffer, 2);
                consumedFrames += buffer.Length / 2;

                foreach (float sample in buffer)
                    if (Math.Abs(sample) > 0.05f)
                        heard = true;

                Thread.Sleep((int)(1000d * buffer.Length / 2 / sampleRate));
            }

            player.UpdateTexture();

            // Assert
            Assert.IsTrue(heard, "no audible samples were drained");
            Assert.AreEqual(consumedFrames / (double)sampleRate, player.CurrentTimeSeconds, 0.15d);
        }

        [Test]
        public void StretchTheClockAtAHigherPlaybackRate()
        {
            // Arrange
            player.OpenMedia(clip3s, true);
            Assert.IsTrue(Pump(() => player.Texture != null, OPEN_TIMEOUT_SECONDS));

            // Act
            player.PlaybackRate = 2f;
            Assert.IsTrue(Pump(() => !player.IsSeeking, OPEN_TIMEOUT_SECONDS));
            double before = player.CurrentTimeSeconds;
            PumpFor(0.5d);
            double advanced = player.CurrentTimeSeconds - before;

            // Assert
            Assert.AreEqual(2f, player.PlaybackRate);
            Assert.AreEqual(1d, advanced, 0.3d);
        }

        [Test]
        public void ResizeTheTextureToTheNewSourceOnReopen()
        {
            // Arrange
            player.OpenMedia(clip3s, true);
            Assert.IsTrue(Pump(() => player.Texture != null, OPEN_TIMEOUT_SECONDS));
            Texture2D first = player.Texture!;

            // Act
            player.OpenMedia(clip1s, true);
            bool resized = Pump(() => player.Texture != null && player.Texture.width == 320, OPEN_TIMEOUT_SECONDS);

            // Assert
            Assert.IsTrue(resized, "texture never took the new source's size");
            Assert.AreSame(first, player.Texture);
            Assert.AreEqual(180, player.Texture!.height);
        }

        [Test]
        public void ReportLoadFailedForAnUndecodableSource()
        {
            // Arrange
            LogAssert.Expect(LogType.Error, new Regex("LoadFailed .*garbage\\.mp4"));

            // Act
            bool accepted = player.OpenMedia(garbage, true);
            bool failed = Pump(() => player.LastError != ErrorCode.None, OPEN_TIMEOUT_SECONDS);

            // Assert
            Assert.IsTrue(accepted);
            Assert.IsTrue(failed, "no error was reported");
            Assert.AreEqual(ErrorCode.LoadFailed, player.LastError);
            Assert.IsFalse(player.IsPlaying);
            Assert.IsFalse(player.IsBuffering);
            Assert.IsFalse(player.IsOpen);
        }

        [Test]
        public void RejectAMissingLocalFile()
        {
            // Arrange
            LogAssert.Expect(LogType.Error, new Regex("Unplayable source"));

            // Act
            bool accepted = player.OpenMedia("/nonexistent/videoplayback-linux-test.mp4", true);

            // Assert
            Assert.IsFalse(accepted);
            Assert.AreEqual(ErrorCode.LoadFailed, player.LastError);
        }

        [Test]
        public void ClearEverythingOnClose()
        {
            // Arrange
            player.OpenMedia(clip3s, true);
            Assert.IsTrue(Pump(() => player.Texture != null, OPEN_TIMEOUT_SECONDS));

            // Act
            player.Close();

            // Assert
            Assert.IsFalse(player.IsOpen);
            Assert.IsFalse(player.IsPlaying);
            Assert.AreEqual(0d, player.Duration);
            Assert.AreEqual(0d, player.CurrentTimeSeconds);
            Assert.AreEqual(ErrorCode.None, player.LastError);
        }

        private bool Pump(Func<bool> until, double timeoutSeconds)
        {
            var stopwatch = Stopwatch.StartNew();

            while (stopwatch.Elapsed.TotalSeconds < timeoutSeconds)
            {
                player.UpdateTexture();
                if (until()) return true;
                Thread.Sleep(5);
            }

            player.UpdateTexture();
            return until();
        }

        private void PumpFor(double seconds)
        {
            var stopwatch = Stopwatch.StartNew();

            while (stopwatch.Elapsed.TotalSeconds < seconds)
            {
                player.UpdateTexture();
                Thread.Sleep(5);
            }
        }

        private static void RunFfmpeg(string arguments)
        {
            var psi = new ProcessStartInfo
            {
                FileName = ffmpeg,
                Arguments = "-hide_banner -nostdin -loglevel error " + arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
            };

            using var process = Process.Start(psi);
            Assert.IsNotNull(process, "ffmpeg did not start");
            string stderr = process!.StandardError.ReadToEnd();
            Assert.IsTrue(process.WaitForExit(FIXTURE_TIMEOUT_MS), "fixture generation timed out");
            Assert.AreEqual(0, process.ExitCode, $"fixture generation failed: {stderr}");
        }
    }
}
#endif
