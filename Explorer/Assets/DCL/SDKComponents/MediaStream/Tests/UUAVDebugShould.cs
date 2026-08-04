using NUnit.Framework;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.TestTools;
using UUAV;

namespace DCL.SDKComponents.MediaStream.Tests
{
    /// <summary>
    ///     Covers the read-only surface the "Media Player" debug tab renders.
    ///     Both the message ring and the player registry are static process
    ///     state: the ring tests flood it past capacity so the outcome is
    ///     independent of ordering, and the registry test unregisters in a
    ///     finally.
    /// </summary>
    public class UUAVDebugShould
    {
        private const int CAPACITY = 10;

        [Test]
        public void KeepTheLastMessagesInOrder()
        {
            for (var i = 0; i < 15; i++)
                UUAVDebug.Push($"m{i}");

            List<string> messages = new ();
            UUAVDebug.CopyRecentMessages(messages);

            Assert.That(messages, Has.Count.EqualTo(CAPACITY));

            for (var i = 0; i < CAPACITY; i++)
                Assert.That(messages[i], Is.EqualTo($"m{i + 15 - CAPACITY}"));
        }

        // Whether or not the native library is present, the stats getters
        // must degrade to false: missing/stale binary is caught and cached,
        // and a loaded binary without an initialized runtime reports an
        // error consumed internally.
        [Test]
        public void ReportAudioStatsUnavailableWithoutThrowing()
        {
            Assert.That(() => UUAVDebug.TryGetAudioStats(playerId: 12345, out AudioStats stats), Throws.Nothing);
            Assert.That(UUAVDebug.TryGetAudioStats(playerId: 12345, out AudioStats _), Is.False);

            // id 0 = native creation failed; short-circuits before the FFI
            Assert.That(UUAVDebug.TryGetAudioStats(playerId: 0, out AudioStats zeroed), Is.False);
            Assert.That(zeroed.JitterUnderruns, Is.EqualTo(0));
        }

        [Test]
        public void ReportEngineAudioStatsUnavailableWithoutThrowing()
        {
            Assert.That(() => UUAVDebug.TryGetEngineAudioStats(out EngineAudioStats _), Throws.Nothing);
            Assert.That(UUAVDebug.TryGetEngineAudioStats(out EngineAudioStats _), Is.False);
        }

        // CopyPlayers reads UUAVPlayer.State, an unguarded P/Invoke, for every
        // registered instance, and the debug panel is its only caller: it has
        // to fill the panel whether or not the native library is there. A
        // component Unity never awoke holds id 0, the same shape as one whose
        // native player failed to create, so it takes the guarded path.
        [Test]
        public void SnapshotAPlayerWithNoNativeIdWithoutThrowing()
        {
            var host = new GameObject(nameof(SnapshotAPlayerWithNoNativeIdWithoutThrowing));
            var player = host.AddComponent<UUAVPlayer>();
            UUAVDebug.Register(player);

            try
            {
                List<UUAVDebug.PlayerInfo> snapshot = new ();
                Assert.That(() => UUAVDebug.CopyPlayers(snapshot), Throws.Nothing);

                Assert.That(snapshot, Has.Count.EqualTo(1));
                Assert.That(snapshot[0].PlayerId, Is.EqualTo(0));
                Assert.That(snapshot[0].State, Is.EqualTo(UUAVState.Unknown));
                Assert.That(snapshot[0].HasAudioStats, Is.False);
            }
            finally
            {
                // drop the entry first: the registry is static process state.
                // OnDestroy then runs against a component whose Awake never
                // assigned its AudioSource and logs for it, an EditMode
                // artifact rather than a failure
                UUAVDebug.Unregister(player);
                LogAssert.ignoreFailingMessages = true;
                Object.DestroyImmediate(host);
                LogAssert.ignoreFailingMessages = false;
            }
        }

        [Test]
        public void SurviveConcurrentPushes()
        {
            var tasks = new Task[4];

            for (var t = 0; t < tasks.Length; t++)
            {
                int worker = t;

                tasks[t] = Task.Run(() =>
                {
                    for (var i = 0; i < 100; i++)
                        UUAVDebug.Push($"w{worker}-{i}");
                });
            }

            Task.WaitAll(tasks);

            List<string> messages = new ();
            UUAVDebug.CopyRecentMessages(messages);

            Assert.That(messages, Has.Count.EqualTo(CAPACITY));
            Assert.That(messages, Has.None.Null);
        }
    }
}
