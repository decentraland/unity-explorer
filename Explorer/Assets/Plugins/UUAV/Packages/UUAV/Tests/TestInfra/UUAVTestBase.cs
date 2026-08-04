using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace UUAV.Tests
{
    /// <summary>
    /// Shared PlayMode harness: verifies the native runtime is up, hosts the
    /// fixture http server, and tracks created players so every test hands
    /// the runtime back with the native player count it started with.
    /// </summary>
    public abstract class UUAVTestBase
    {
        /// <summary>The first open also pays helper spawn and http warm-up.</summary>
        protected const float OpenTimeout = 30f;

        protected const float StateTimeout = 10f;

        private readonly List<UUAVPlayer> createdPlayers = new List<UUAVPlayer>();
        private FixtureServer? server;
        private ILogHandler? previousLogHandler;
        private ulong baselinePlayersCount;

        private static string FixturesDirectory =>
            Path.Combine(Application.dataPath, "Plugins/UUAV/Packages/UUAV/Tests/Fixtures~");

        [OneTimeSetUp]
        public void UUAVBaseOneTimeSetUp()
        {
            UUAVDebug.Info info = UUAVDebug.Query();
            if (info.NativeLibLoaded == false || info.Initialized == false)
            {
                Assert.Fail(
                    $"UUAV runtime is not initialized (libLoaded={info.NativeLibLoaded}, initialized={info.Initialized}, "
                    + $"lifecycle={info.Lifecycle}, graphics={SystemInfo.graphicsDeviceType}). "
                    + "The PlayMode suite needs the native runtime; on macOS the editor must run Metal."
                );
            }

            server = new FixtureServer(FixturesDirectory);

            // the suite legitimately provokes native error logs (rejected
            // opens, helper crashes) that arrive from playback threads whole
            // frames after the call that caused them, so the framework blames
            // whatever test is current. LogAssert cannot ignore them either:
            // every test phase gets a fresh LogScope that resets
            // ignoreFailingMessages. Muffling [UUAV]-prefixed errors at the
            // logger is the only deterministic seam; correctness is asserted
            // through states, stats and captured audio instead. Any other
            // error still fails tests as usual.
            previousLogHandler = Debug.unityLogger.logHandler;
            Debug.unityLogger.logHandler = new UUAVErrorMuffler(previousLogHandler);
        }

        [OneTimeTearDown]
        public void UUAVBaseOneTimeTearDown()
        {
            if (previousLogHandler != null)
            {
                Debug.unityLogger.logHandler = previousLogHandler;
                previousLogHandler = null;
            }

            server?.Dispose();
            server = null;
        }

        [SetUp]
        public void UUAVBaseSetUp()
        {
            baselinePlayersCount = UUAVDebug.Query().PlayersCount;
        }

        [UnityTearDown]
        public IEnumerator UUAVBaseTearDown()
        {
            foreach (UUAVPlayer player in createdPlayers)
            {
                if (player != null)
                {
                    Object.Destroy(player.gameObject);
                }
            }

            createdPlayers.Clear();
            yield return null;

            // uuav_player_free runs in OnDestroy; give the native side a
            // moment before treating a lingering count as a leak
            float start = Time.realtimeSinceStartup;
            while (UUAVDebug.Query().PlayersCount > baselinePlayersCount && Time.realtimeSinceStartup - start < 5f)
            {
                yield return null;
            }

            Assert.That(
                UUAVDebug.Query().PlayersCount,
                Is.LessThanOrEqualTo(baselinePlayersCount),
                "the test leaked native players"
            );
        }

        protected string UrlFor(string fixtureName)
        {
            if (server == null)
            {
                throw new System.InvalidOperationException("fixture server is not running");
            }

            return server.UrlFor(fixtureName);
        }

        protected UUAVPlayer CreatePlayer(out AudioTapBehaviour audio)
        {
            // assembled inactive so everything is in place before
            // UUAVPlayer.Awake runs. The AudioSource is disabled on purpose:
            // sibling script filters cannot observe each other's buffers
            // (see AudioTapBehaviour), so the player's DSP path is switched
            // off entirely and the tap's pump thread becomes the sole audio
            // consumer - it paces the media clock and records real output.
            var gameObject = new GameObject("UUAVPlayer_UnderTest");
            gameObject.SetActive(false);
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.enabled = false;
            audio = gameObject.AddComponent<AudioTapBehaviour>();
            UUAVPlayer player = gameObject.AddComponent<UUAVPlayer>();
            gameObject.SetActive(true);

            createdPlayers.Add(player);
            Assert.That(player.PlayerId, Is.Not.Zero, "native player creation failed");
            return player;
        }

        protected IEnumerator OpenAndAwaitReady(UUAVPlayer player, string url)
        {
            player.OpenMedia(url);
            yield return Wait.Until(
                () => player.State is UUAVState.Ready or UUAVState.Playing,
                OpenTimeout,
                () => $"opening {url}\n{Wait.Diagnostics(player)}"
            );
        }

        protected IEnumerator OpenPlayAndAwaitPlaying(UUAVPlayer player, string url)
        {
            player.OpenMedia(url);
            player.Play();
            yield return Wait.ForState(player, UUAVState.Playing, OpenTimeout, $"open+play {url}");
        }

        protected static IEnumerator AwaitClockRunning(UUAVPlayer player)
        {
            yield return Wait.Until(
                () => player.CurrentTime > 0.2,
                StateTimeout,
                () => $"media clock never started\n{Wait.Diagnostics(player)}"
            );
        }

        protected static IEnumerator AwaitAudibleSignal(UUAVPlayer player, AudioTapBehaviour audio)
        {
            yield return Wait.Until(
                () => audio.HasObservedSignal,
                StateTimeout,
                () => "playback never produced audible output "
                      + $"(tap callbacks={audio.DspCallbackCount}, peak={audio.PeakAmplitude:F3}, "
                      + $"pump={audio.PumpMode}, decided={audio.ModeDecided})\n{Wait.Diagnostics(player)}"
            );
        }

        // drops the runtime's own error lines; everything else - including
        // exceptions and errors from any other system - passes through and
        // fails tests as usual
        private sealed class UUAVErrorMuffler : ILogHandler
        {
            private readonly ILogHandler inner;

            public UUAVErrorMuffler(ILogHandler inner)
            {
                this.inner = inner;
            }

            public void LogFormat(LogType logType, Object context, string format, params object[] args)
            {
                if (logType == LogType.Error && args is { Length: 1 } && args[0] is string message && message.Contains("[UUAV]"))
                {
                    return;
                }

                inner.LogFormat(logType, context, format, args);
            }

            public void LogException(System.Exception exception, Object context)
            {
                inner.LogException(exception, context);
            }
        }
    }
}
