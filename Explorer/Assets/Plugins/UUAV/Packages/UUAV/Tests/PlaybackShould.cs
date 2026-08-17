using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace UUAV.Tests
{
    public class PlaybackShould : UUAVTestBase
    {
        [UnityTest]
        public IEnumerator AdvanceClockWithRealTime()
        {
            // Arrange
            UUAVPlayer player = CreatePlayer(out _);
            yield return OpenPlayAndAwaitPlaying(player, UrlFor(Fixtures.ToneColorBands));
            yield return AwaitClockRunning(player);

            // Act
            double before = player.CurrentTime;
            yield return Wait.SecondsRealtime(2f);
            double advanced = player.CurrentTime - before;

            // Assert: catches both a stalled clock and a runaway one
            Assert.That(advanced, Is.GreaterThan(1.0).And.LessThan(3.0), Wait.Diagnostics(player));
        }

        [UnityTest]
        public IEnumerator PresentFrames()
        {
            VideoProbe.RequireGraphics();

            // Arrange
            UUAVPlayer player = CreatePlayer(out _);
            yield return OpenPlayAndAwaitPlaying(player, UrlFor(Fixtures.ToneColorBands));
            yield return AwaitClockRunning(player);

            // Act
            yield return Wait.Until(
                () => player.CurrentTexture != null,
                StateTimeout,
                () => $"no output surface was created\n{Wait.Diagnostics(player)}"
            );
            RenderTexture? surface = player.CurrentTexture;
            if (surface is null)
            {
                Assert.Fail("output surface disappeared between polls");
                yield break;
            }

            Color pixel = VideoProbe.ReadCenterPixel(surface);

            // Assert: the clock started inside the red band, so a presented
            // frame is red; a black or garbage frame fails
            Assert.That(surface.width, Is.EqualTo(Fixtures.Width));
            Assert.That(surface.height, Is.EqualTo(Fixtures.Height));
            Assert.That(
                VideoProbe.IsDominantChannel(pixel, 0),
                Is.True,
                $"expected a red frame, sampled {pixel}\n{Wait.Diagnostics(player)}"
            );
        }

        [UnityTest]
        public IEnumerator FreezeClockOnPauseAndResume()
        {
            // Arrange
            UUAVPlayer player = CreatePlayer(out _);
            yield return OpenPlayAndAwaitPlaying(player, UrlFor(Fixtures.ToneColorBands));
            yield return AwaitClockRunning(player);

            // Act
            player.Pause();
            yield return Wait.ForState(player, UUAVState.Paused, StateTimeout, "after Pause");
            double pausedAt = player.CurrentTime;
            yield return Wait.SecondsRealtime(0.5f);

            // Assert
            Assert.That(player.CurrentTime, Is.EqualTo(pausedAt).Within(0.1), "clock kept running while paused");

            // Act: resuming continues from where it froze
            player.Play();
            yield return Wait.ForState(player, UUAVState.Playing, StateTimeout, "after resume");
            yield return Wait.Until(
                () => player.CurrentTime > pausedAt + 0.2,
                StateTimeout,
                () => $"clock did not resume\n{Wait.Diagnostics(player)}"
            );
        }

        [UnityTest]
        public IEnumerator ReachEndedAtEndOfMedia()
        {
            // Arrange
            UUAVPlayer player = CreatePlayer(out _);
            yield return OpenPlayAndAwaitPlaying(player, UrlFor(Fixtures.ToneColorBands));
            yield return AwaitClockRunning(player);

            // Act: jump near the end instead of sitting through the fixture
            player.Seek(Fixtures.DurationSeconds - 1.0);

            // Assert
            yield return Wait.ForState(player, UUAVState.Ended, StateTimeout, "after playing past the end");
        }

        [UnityTest]
        public IEnumerator WrapInsteadOfEndingWhenLooping()
        {
            // Arrange
            UUAVPlayer player = CreatePlayer(out _);
            yield return OpenPlayAndAwaitPlaying(player, UrlFor(Fixtures.ToneColorBands));
            yield return AwaitClockRunning(player);

            // Act: looping is an async intent; give the playback thread a
            // moment to apply it before racing it against the end of file
            player.Looping = true;
            yield return Wait.SecondsRealtime(0.25f);
            player.Seek(Fixtures.DurationSeconds - 0.8);

            // Assert: the clock passes the end and comes back low
            yield return Wait.Until(
                () => player.CurrentTime < 2.0 && player.State == UUAVState.Playing,
                StateTimeout + 5f,
                () => $"playback never wrapped\n{Wait.Diagnostics(player)}"
            );
            Assert.That(player.State, Is.EqualTo(UUAVState.Playing), Wait.Diagnostics(player));
        }

        [UnityTest]
        public IEnumerator HonorPlaybackRate()
        {
            // Arrange
            UUAVPlayer player = CreatePlayer(out _);
            yield return OpenPlayAndAwaitPlaying(player, UrlFor(Fixtures.ToneColorBands));
            yield return AwaitClockRunning(player);

            // Act: measure equal wall-clock windows at 1x and 2x
            double normalStart = player.CurrentTime;
            yield return Wait.SecondsRealtime(1f);
            double normalDelta = player.CurrentTime - normalStart;

            player.PlaybackRate = 2.0;
            yield return Wait.SecondsRealtime(0.25f);
            double fastStart = player.CurrentTime;
            yield return Wait.SecondsRealtime(1f);
            double fastDelta = player.CurrentTime - fastStart;

            // Assert: the ratio isolates the rate from shared clock jitter
            Assert.That(normalDelta, Is.GreaterThan(0.5), Wait.Diagnostics(player));
            Assert.That(fastDelta / normalDelta, Is.GreaterThan(1.4).And.LessThan(2.8), Wait.Diagnostics(player));
        }
    }
}
