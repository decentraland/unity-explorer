using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace UUAV.Tests
{
    /// <summary>
    /// Kills the out-of-process uuav-helper with SIGKILL and verifies the
    /// crash contract: the client respawns the helper on its own, and an open
    /// player either has its playback restored in place or degrades to Error
    /// for the host to retry - hanging in between is the only failure. Each
    /// test ends with the runtime verified healthy, so fixture ordering does
    /// not matter.
    /// </summary>
    public class HelperProcessRecoveryShould : UUAVTestBase
    {
        private const float RecoveryTimeout = 30f;

        [UnityTest]
        public IEnumerator RespawnAndRestorePlaybackAfterKillWhilePlaying()
        {
            // Arrange
            UUAVPlayer player = CreatePlayer(out AudioTapBehaviour audio);
            yield return OpenPlayAndAwaitPlaying(player, UrlFor(Fixtures.ToneColorBands));
            yield return AwaitClockRunning(player);
            int pid = RequireHelperPid();

            // Act
            HelperProcess.Kill(pid);

            // Assert: the client brings up a fresh helper on its own...
            yield return AwaitRespawn(pid);

            // ...the open player ends up restored or explicitly failed...
            yield return AwaitRestoredOrDegraded(player);
            if (player.State == UUAVState.Error)
            {
                player.OpenMedia(UrlFor(Fixtures.ToneColorBands));
                player.Play();
                yield return Wait.ForState(player, UUAVState.Playing, OpenTimeout, "reopen after degrade");
                yield return AwaitClockRunning(player);
            }

            // ...with the audio path alive, not just the clock
            yield return AwaitAudible(player, audio);
        }

        [UnityTest]
        public IEnumerator ServeAReopenOnTheSamePlayerAfterRecovery()
        {
            // Arrange: crash the helper under a playing player
            UUAVPlayer player = CreatePlayer(out AudioTapBehaviour audio);
            yield return OpenPlayAndAwaitPlaying(player, UrlFor(Fixtures.ToneColorBands));
            yield return AwaitClockRunning(player);
            int pid = RequireHelperPid();
            HelperProcess.Kill(pid);
            yield return AwaitRespawn(pid);

            // Act: a host-driven reopen must work whatever state the crash
            // left the player in
            player.OpenMedia(UrlFor(Fixtures.AudioOnly));
            player.Play();

            // Assert
            yield return Wait.ForState(player, UUAVState.Playing, OpenTimeout, "reopen after recovery");
            yield return AwaitClockRunning(player);
            yield return AwaitAudible(player, audio);
        }

        [UnityTest]
        public IEnumerator RecoverFromAKillWhileIdle()
        {
            // Arrange: no media open anywhere
            int pid = RequireHelperPid();

            // Act
            HelperProcess.Kill(pid);
            yield return AwaitRespawn(pid);

            // Assert: the respawned helper serves a brand-new player
            UUAVPlayer player = CreatePlayer(out _);
            yield return OpenPlayAndAwaitPlaying(player, UrlFor(Fixtures.ToneColorBands));
            yield return AwaitClockRunning(player);
        }

        private static int RequireHelperPid()
        {
            int? pid = HelperProcess.FindPid();
            if (pid == null)
            {
                Assert.Inconclusive("no uuav-helper process found; the runtime may be running in-process on this platform");
            }

            return pid ?? 0;
        }

        private static IEnumerator AwaitRespawn(int killedPid)
        {
            // pid discovery shells out to pgrep/ps, so poll gently
            yield return Wait.Until(
                () => UUAVDebug.Query().Lifecycle == UUAVDebug.Lifecycle.Running
                      && HelperProcess.FindPid() is { } fresh
                      && fresh != killedPid,
                RecoveryTimeout,
                () => $"helper never respawned (lifecycle={UUAVDebug.Query().Lifecycle}, pid={HelperProcess.FindPid()})",
                pollInterval: 0.5f
            );
        }

        // resolves once the player is either explicitly failed or playing
        // with a moving clock again; called after the respawn completed so a
        // client-side extrapolated clock cannot fake the "restored" outcome
        private static IEnumerator AwaitRestoredOrDegraded(UUAVPlayer player)
        {
            double sampledTime = -1;
            yield return Wait.Until(
                () =>
                {
                    if (player.State == UUAVState.Error)
                    {
                        return true;
                    }

                    if (player.State != UUAVState.Playing)
                    {
                        return false;
                    }

                    if (sampledTime < 0)
                    {
                        sampledTime = player.CurrentTime;
                        return false;
                    }

                    // the fixture loops nowhere here: a wrap-free advance or
                    // a post-restore rewind both count as a live clock
                    return System.Math.Abs(player.CurrentTime - sampledTime) > 0.5;
                },
                RecoveryTimeout,
                () => $"player neither restored nor degraded to Error after the respawn\n{Wait.Diagnostics(player)}"
            );
        }

        private static IEnumerator AwaitAudible(UUAVPlayer player, AudioTapBehaviour audio)
        {
            audio.ResetSignalObservation();
            audio.BeginCapture(1f);
            yield return Wait.ForCapture(audio, 1f);
            audio.CopyCapture(out float[] samples, out int sampleCount, out int channels);
            List<float> rms = AudioAnalysis.WindowRms(samples, sampleCount, channels, audio.SampleRate);
            Assert.That(
                AudioAnalysis.FirstLoudWindow(rms),
                Is.GreaterThanOrEqualTo(0),
                $"playback restored the clock but not the audio\n{Wait.Diagnostics(player)}"
            );
        }
    }
}
