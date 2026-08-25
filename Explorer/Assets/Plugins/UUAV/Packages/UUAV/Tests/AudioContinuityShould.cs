using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace UUAV.Tests
{
    /// <summary>
    /// "No silent gaps" is asserted through three independent signals: the
    /// actual DSP output content, the player's DSP counters, and the native
    /// jitter-ring stats (one underrun per audible gap by design). A gap has
    /// to fool all three to slip through.
    /// </summary>
    public class AudioContinuityShould : UUAVTestBase
    {
        [UnityTest]
        public IEnumerator ProduceNoSilentGapsDuringSteadyPlayback()
        {
            // Arrange: only start measuring once sound actually flows
            UUAVPlayer player = CreatePlayer(out AudioTapBehaviour audio);
            yield return OpenPlayAndAwaitPlaying(player, UrlFor(Fixtures.ToneColorBands));
            player.Looping = true;
            yield return AwaitClockRunning(player);
            yield return AwaitAudibleSignal(player, audio);

            // a slow open can leave the clock deep into the 6s fixture; pull
            // it back so the 3s capture cannot straddle the loop boundary,
            // whose (bounded) gap is a different test's business
            if (player.CurrentTime > 2.0)
            {
                player.Seek(0.5);
                yield return Wait.Until(
                    () => player.CurrentTime < 2.0,
                    StateTimeout,
                    () => $"seek back to the file start never converged\n{Wait.Diagnostics(player)}"
                );
            }

            // Act
            audio.BeginCapture(3f);
            yield return Wait.ForCapture(audio, 3f);

            // Assert: every window of an already-flowing sine must stay loud
            audio.CopyCapture(out float[] samples, out int sampleCount, out int channels);
            List<float> rms = AudioAnalysis.WindowRms(samples, sampleCount, channels, audio.SampleRate);
            Assert.That(rms.Count, Is.GreaterThan(50), "capture is too short to judge");
            int firstLoud = AudioAnalysis.FirstLoudWindow(rms);
            Assert.That(firstLoud, Is.GreaterThanOrEqualTo(0).And.LessThanOrEqualTo(5), "capture started silent");
            Assert.That(
                AudioAnalysis.CountSilentWindows(rms, firstLoud),
                Is.Zero,
                $"silent gaps inside steady playback (longest {AudioAnalysis.LongestSilenceSeconds(rms, firstLoud) * 1000f:F0}ms)"
                + $"\n{Wait.Diagnostics(player)}"
            );
        }

        [UnityTest]
        public IEnumerator ReportNoStarvationInCountersDuringSteadyPlayback()
        {
            // Arrange
            UUAVPlayer player = CreatePlayer(out AudioTapBehaviour audio);
            yield return OpenPlayAndAwaitPlaying(player, UrlFor(Fixtures.ToneColorBands));
            player.Looping = true;
            yield return AwaitClockRunning(player);
            yield return AwaitAudibleSignal(player, audio);

            bool hasNativeStats = UUAVDebug.TryGetAudioStats(player.PlayerId, out AudioStats before);
            player.CopyDspStats(out long requestedBefore, out long returnedBefore, out long silencedBefore);

            // Act
            yield return Wait.SecondsRealtime(2f);

            // Assert
            player.CopyDspStats(out long requestedAfter, out long returnedAfter, out long silencedAfter);
            if (audio.PumpMode == false)
            {
                long requestedDelta = requestedAfter - requestedBefore;
                Assert.That(requestedDelta, Is.GreaterThan(0), "the DSP stopped asking for audio");
                Assert.That(returnedAfter - returnedBefore, Is.EqualTo(requestedDelta), $"native reads came up short\n{Wait.Diagnostics(player)}");
                Assert.That(silencedAfter - silencedBefore, Is.Zero, $"DSP callbacks were silenced mid-playback\n{Wait.Diagnostics(player)}");
            }

            if (hasNativeStats && UUAVDebug.TryGetAudioStats(player.PlayerId, out AudioStats after))
            {
                Assert.That(
                    after.JitterUnderruns - before.JitterUnderruns,
                    Is.Zero,
                    $"the jitter ring underran: each underrun is an audible gap\n{Wait.Diagnostics(player)}"
                );
            }
        }

        [UnityTest]
        public IEnumerator KeepTheLoopBoundaryGapBounded()
        {
            // Arrange
            UUAVPlayer player = CreatePlayer(out AudioTapBehaviour audio);
            yield return OpenPlayAndAwaitPlaying(player, UrlFor(Fixtures.ToneColorBands));
            player.Looping = true;
            yield return AwaitClockRunning(player);
            yield return AwaitAudibleSignal(player, audio);

            // Act: park just before the end so the capture spans the wrap
            player.Seek(Fixtures.DurationSeconds - 1.5);
            yield return Wait.Until(
                () => player.CurrentTime > Fixtures.DurationSeconds - 2.5,
                StateTimeout,
                () => $"seek towards the loop point never converged\n{Wait.Diagnostics(player)}"
            );
            audio.BeginCapture(3f);
            yield return Wait.ForCapture(audio, 3f);

            // Assert: looping is not required to be sample-gapless, but the
            // gap must stay short of audible-glitch territory
            audio.CopyCapture(out float[] samples, out int sampleCount, out int channels);
            List<float> rms = AudioAnalysis.WindowRms(samples, sampleCount, channels, audio.SampleRate);
            int firstLoud = AudioAnalysis.FirstLoudWindow(rms);
            Assert.That(firstLoud, Is.GreaterThanOrEqualTo(0), $"no audio around the loop boundary\n{Wait.Diagnostics(player)}");
            Assert.That(
                AudioAnalysis.LongestSilenceSeconds(rms, firstLoud),
                Is.LessThanOrEqualTo(0.12f),
                $"the loop boundary gap is audible\n{Wait.Diagnostics(player)}"
            );
            Assert.That(player.CurrentTime, Is.LessThan(Fixtures.DurationSeconds - 1.0), "playback never wrapped during the capture");
        }

        [UnityTest]
        public IEnumerator GoSilentAfterEnded()
        {
            // Arrange
            UUAVPlayer player = CreatePlayer(out AudioTapBehaviour audio);
            yield return OpenPlayAndAwaitPlaying(player, UrlFor(Fixtures.ToneColorBands));
            yield return AwaitClockRunning(player);

            // hearing the tone first proves the tap works; without this the
            // final all-silent assertion would pass vacuously on a dead tap
            yield return AwaitAudibleSignal(player, audio);
            player.Seek(Fixtures.DurationSeconds - 1.0);
            yield return Wait.ForState(player, UUAVState.Ended, StateTimeout, "playing to the end");

            // Act: let the jitter ring drain, then listen
            yield return Wait.SecondsRealtime(0.5f);
            audio.BeginCapture(0.5f);
            yield return Wait.ForCapture(audio, 0.5f);

            // Assert: no stale ring content replaying after the end
            audio.CopyCapture(out float[] samples, out int sampleCount, out int channels);
            List<float> rms = AudioAnalysis.WindowRms(samples, sampleCount, channels, audio.SampleRate);
            Assert.That(
                AudioAnalysis.FirstLoudWindow(rms),
                Is.EqualTo(-1),
                $"audio kept playing after Ended\n{Wait.Diagnostics(player)}"
            );
        }
    }
}
