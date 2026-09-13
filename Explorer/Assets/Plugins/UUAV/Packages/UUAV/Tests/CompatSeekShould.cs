using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UUAV.Compat;

namespace UUAV.Tests
{
    /// <summary>
    /// The compat facade is what the explorer drives, and the explorer seeks
    /// as soon as it has asked for media. The core rejects a seek until the
    /// media is open, so the facade has to carry that seek across the gap.
    /// </summary>
    public class CompatSeekShould : UUAVTestBase
    {
        // same convergence band as SeekShould: nearest keyframe plus jitter
        private const double SeekTolerance = 0.75;

        // a seek that was honoured lands within this long after the media
        // opens; playing there from zero would take BlueBandStartSeconds
        private const float HonouredSeekWindow = 2f;

        [UnityTest]
        public IEnumerator HoldASeekIssuedWhileOpeningUntilTheMediaSettles()
        {
            // Arrange: the facade backend over a tap-paced player
            UUAVPlayer player = CreatePlayer(out _);
            var backend = new UUAVBackend(player);
            double target = Fixtures.BlueBandStartSeconds + 0.6;

            // Act: seek while the first byte is still held back
            player.OpenMedia(HeldUrlFor(Fixtures.ToneColorBands));
            player.Play();
            yield return Wait.ForState(player, UUAVState.Opening, StateTimeout, "held open");

            backend.Seek(target);

            Assert.That(player.State, Is.EqualTo(UUAVState.Opening), Wait.Diagnostics(player));
            Assert.That(backend.IsSeeking(), Is.True, "a held seek is still a pending seek");

            float settledAt = float.NaN;
            yield return Wait.Until(
                () =>
                {
                    backend.Tick();
                    if (float.IsNaN(settledAt) && player.State is UUAVState.Ready or UUAVState.Playing)
                    {
                        settledAt = Time.realtimeSinceStartup;
                    }

                    return System.Math.Abs(player.CurrentTime - target) <= SeekTolerance;
                },
                OpenTimeout,
                () => $"clock never converged to {target:F1}s\n{Wait.Diagnostics(player)}"
            );

            // Assert: the clock jumped to the target right after the media
            // opened instead of playing its way there from zero
            Assert.That(Time.realtimeSinceStartup - settledAt, Is.LessThan(HonouredSeekWindow),
                $"the seek was dropped: playback reached {target:F1}s by playing through the file\n{Wait.Diagnostics(player)}");
            Assert.That(player.State, Is.Not.EqualTo(UUAVState.Error), Wait.Diagnostics(player));

            yield return Wait.Until(
                () =>
                {
                    backend.Tick();
                    return backend.IsSeeking() == false;
                },
                StateTimeout,
                () => $"IsSeeking never cleared after the held seek landed\n{Wait.Diagnostics(player)}"
            );
        }

        [UnityTest]
        public IEnumerator KeepTheLastSeekIssuedWhileOpening()
        {
            VideoProbe.RequireGraphics();

            // Arrange
            UUAVPlayer player = CreatePlayer(out _);
            var backend = new UUAVBackend(player);
            double target = Fixtures.BlueBandStartSeconds + 0.6;

            // Act: two held seeks; only the last one may land
            player.OpenMedia(HeldUrlFor(Fixtures.ToneColorBands));
            player.Play();
            yield return Wait.ForState(player, UUAVState.Opening, StateTimeout, "held open");

            backend.Seek(1.0);
            backend.Seek(target);

            yield return Wait.Until(
                () =>
                {
                    backend.Tick();
                    return System.Math.Abs(player.CurrentTime - target) <= SeekTolerance;
                },
                OpenTimeout,
                () => $"clock never converged to {target:F1}s\n{Wait.Diagnostics(player)}"
            );

            // Assert: the presented frame is from the target band, so the
            // earlier target was replaced rather than queued behind it
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
    }
}
