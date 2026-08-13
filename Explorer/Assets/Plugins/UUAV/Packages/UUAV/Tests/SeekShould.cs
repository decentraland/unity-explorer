using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace UUAV.Tests
{
    public class SeekShould : UUAVTestBase
    {
        // seeks land on the nearest decodable point, not the exact sample;
        // the fixture has a keyframe every second, so half of that plus
        // clock jitter is a fair convergence band
        private const double SeekTolerance = 0.75;

        [UnityTest]
        public IEnumerator ConvergeForwardAndPresentTheTargetFrame()
        {
            VideoProbe.RequireGraphics();

            // Arrange
            UUAVPlayer player = CreatePlayer(out _);
            yield return OpenPlayAndAwaitPlaying(player, UrlFor(Fixtures.ToneColorBands));
            yield return AwaitClockRunning(player);

            // Act: into the middle of the blue band
            double target = Fixtures.BlueBandStartSeconds + 0.6;
            player.Seek(target);
            yield return AwaitSeekConverged(player, target);

            // Assert: the presented frame proves the demuxer moved, not just
            // the clock; presentation legitimately lags a seek by a few
            // frames, so poll instead of sampling once
            Color pixel = default;
            yield return Wait.Until(
                () =>
                {
                    RenderTexture? surface = player.CurrentTexture;
                    if (surface is null)
                    {
                        return false;
                    }

                    pixel = VideoProbe.ReadCenterPixel(surface);
                    return VideoProbe.IsDominantChannel(pixel, 2);
                },
                5f,
                () => $"expected a blue frame at {target:F1}s, last sampled {pixel}\n{Wait.Diagnostics(player)}"
            );
        }

        [UnityTest]
        public IEnumerator KeepClockAdvancingAfterSeek()
        {
            // Arrange
            UUAVPlayer player = CreatePlayer(out _);
            yield return OpenPlayAndAwaitPlaying(player, UrlFor(Fixtures.ToneColorBands));
            yield return AwaitClockRunning(player);

            // Act
            player.Seek(3.0);
            yield return AwaitSeekConverged(player, 3.0);
            double before = player.CurrentTime;
            yield return Wait.SecondsRealtime(0.5f);

            // Assert
            Assert.That(player.CurrentTime, Is.GreaterThan(before + 0.2), Wait.Diagnostics(player));
        }

        [UnityTest]
        public IEnumerator ConvergeBackward()
        {
            // Arrange: start from deep in the file
            UUAVPlayer player = CreatePlayer(out _);
            yield return OpenPlayAndAwaitPlaying(player, UrlFor(Fixtures.ToneColorBands));
            yield return AwaitClockRunning(player);
            player.Seek(4.0);
            yield return AwaitSeekConverged(player, 4.0);

            // Act
            player.Seek(1.0);

            // Assert
            yield return AwaitSeekConverged(player, 1.0);
        }

        [UnityTest]
        public IEnumerator ApplyWhilePausedAndStayPaused()
        {
            // Arrange
            UUAVPlayer player = CreatePlayer(out _);
            yield return OpenPlayAndAwaitPlaying(player, UrlFor(Fixtures.ToneColorBands));
            yield return AwaitClockRunning(player);
            player.Pause();
            yield return Wait.ForState(player, UUAVState.Paused, StateTimeout, "before paused seek");

            // Act
            player.Seek(4.0);
            yield return AwaitSeekConverged(player, 4.0);

            // Assert: repositioned but still paused, clock frozen at the target
            Assert.That(player.State, Is.EqualTo(UUAVState.Paused), Wait.Diagnostics(player));
            double frozenAt = player.CurrentTime;
            yield return Wait.SecondsRealtime(0.5f);
            Assert.That(player.CurrentTime, Is.EqualTo(frozenAt).Within(0.1), "clock ran while paused");
        }

        [UnityTest]
        public IEnumerator CoalesceRapidSeeksToTheLastTarget()
        {
            // Arrange
            UUAVPlayer player = CreatePlayer(out _);
            yield return OpenPlayAndAwaitPlaying(player, UrlFor(Fixtures.ToneColorBands));
            yield return AwaitClockRunning(player);

            // Act: a same-frame burst; the async seek api coalesces
            var burst = new List<double> { 4.8, 0.7, 3.9, 1.5, 5.1, 0.4, 4.4, 2.0, 3.3, 2.5 };
            foreach (double target in burst)
            {
                player.Seek(target);
            }

            // Assert
            yield return AwaitSeekConverged(player, 2.5);
            Assert.That(player.State, Is.Not.EqualTo(UUAVState.Error), Wait.Diagnostics(player));
        }

        [UnityTest]
        public IEnumerator TreatSeekBeyondDurationAsEndNotError()
        {
            // Arrange
            UUAVPlayer player = CreatePlayer(out _);
            yield return OpenPlayAndAwaitPlaying(player, UrlFor(Fixtures.ToneColorBands));
            yield return AwaitClockRunning(player);

            // Act
            player.Seek(Fixtures.DurationSeconds + 100.0);

            // Assert: clamping near the end and ending are both sane; Error is not
            yield return Wait.Until(
                () => player.State == UUAVState.Ended
                      || player.State == UUAVState.Error
                      || player.CurrentTime >= Fixtures.DurationSeconds - 1.5,
                StateTimeout,
                () => $"seek beyond duration neither clamped nor ended\n{Wait.Diagnostics(player)}"
            );
            Assert.That(player.State, Is.Not.EqualTo(UUAVState.Error), Wait.Diagnostics(player));
        }

        [UnityTest]
        public IEnumerator KeepAudioFlowingAfterSeek()
        {
            // Arrange
            UUAVPlayer player = CreatePlayer(out AudioTapBehaviour audio);
            yield return OpenPlayAndAwaitPlaying(player, UrlFor(Fixtures.ToneColorBands));
            yield return AwaitClockRunning(player);
            yield return AwaitAudibleSignal(player, audio);

            // Act: a seek flushes the audio path; capture what follows
            player.Seek(1.0);
            yield return AwaitSeekConverged(player, 1.0);
            audio.BeginCapture(1f);
            yield return Wait.ForCapture(audio, 1f);

            // Assert: sound came back and stayed - a brief post-flush gap is
            // legitimate, a dead audio path is the regression this catches
            audio.CopyCapture(out float[] samples, out int sampleCount, out int channels);
            List<float> rms = AudioAnalysis.WindowRms(samples, sampleCount, channels, audio.SampleRate);
            int firstLoud = AudioAnalysis.FirstLoudWindow(rms);
            Assert.That(firstLoud, Is.GreaterThanOrEqualTo(0), $"audio never resumed after seek\n{Wait.Diagnostics(player)}");
            Assert.That(
                AudioAnalysis.LongestSilenceSeconds(rms, firstLoud),
                Is.LessThanOrEqualTo(0.25f),
                $"audio dropped out again after resuming\n{Wait.Diagnostics(player)}"
            );
        }

        private static IEnumerator AwaitSeekConverged(UUAVPlayer player, double target)
        {
            yield return Wait.Until(
                () => System.Math.Abs(player.CurrentTime - target) <= SeekTolerance,
                StateTimeout,
                () => $"clock never converged to {target:F1}s\n{Wait.Diagnostics(player)}"
            );
        }
    }
}
