using Arch.Core;
using DCL.Multiplayer.Profiles.Announcements;
using DCL.Multiplayer.Profiles.Entities;
using DCL.Multiplayer.Profiles.RemoteProfiles;
using DCL.Multiplayer.Profiles.RemoveIntentions;
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Unity.PerformanceTesting;

namespace DCL.Tests.PlayMode.PerformanceTests
{
    /// <summary>
    ///     <c>MultiplayerProfilesSystem.Update</c> calls <see cref="RemoteEntitiesExtensions.Remove"/> every
    ///     frame once loading completes. <c>IRemoveIntentions.NewBunchAvailable()</c> is a racy, lock-free
    ///     pre-check (mirrors <c>RemoteProfiles.NewBunchAvailable()</c>) that lets an empty-set frame — the
    ///     overwhelmingly common case at ~60-120 Hz — skip constructing an <c>OwnedBunch&lt;RemoveIntention&gt;</c>,
    ///     whose ctor acquires+releases the backing <c>MutexSync</c> (a kernel <see cref="System.Threading.Mutex"/>).
    ///
    ///     <para>The tests below cover: (a) the empty-set fast path actually elides the lock round-trip,
    ///     (b) the racy pre-check never drops an intention (a stale read is just picked up next frame), and
    ///     (c) the lock still serializes concurrent writers against the drain without torn state.</para>
    /// </summary>
    [Category("Performance")]
    public class RemoveIntentionsFastPathPerformanceTest
    {

        private sealed class RecordingRemoteEntities : IRemoteEntities
        {
            public readonly List<string> Consumed = new (16_384);
            public int RemoveCalls;

            public void Initialize(RemoteAvatarCollider remoteAvatarCollider) { }

            public void TryCreateOrUpdate(IReadOnlyCollection<RemoteProfile> list, World world) { }

            public void Remove(IReadOnlyCollection<RemoveIntention> list, World world)
            {
                RemoveCalls++;

                foreach (RemoveIntention intention in list)
                    Consumed.Add(intention.WalletId);
            }

            public void ForceRemoveAll(World world) { }
        }

        private sealed class NoOpAnnouncements : IRemoteAnnouncements
        {
            public void Fill(List<RemoteAnnouncement> announcements) { }

            public void Remove(IReadOnlyCollection<RemoveIntention> removeIntentions) { }
        }

        private static double MedianOf(string sampleGroupName)
        {
            List<double> samples = PerformanceTest.Active.SampleGroups
                                                  .Single(g => g.Name == sampleGroupName)
                                                  .Samples
                                                  .OrderBy(x => x)
                                                  .ToList();

            int n = samples.Count;
            return n % 2 == 1 ? samples[n / 2] : (samples[(n / 2) - 1] + samples[n / 2]) * 0.5d;
        }

        /// <summary>
        ///     (a) Steady-state cost. Measures the real per-frame entry point with an EMPTY intention set
        ///     (the overwhelmingly common case) against a forced lock round-trip via <c>Bunch()</c>. CI-safe:
        ///     the assertion compares the two sample-group medians directly rather than a wall-clock absolute.
        ///     If the <c>NewBunchAvailable()</c> pre-check stops eliding the lock, the two medians converge.
        /// </summary>
        [Test, Performance]
        public void EmptySet_Remove_ElidesMutexRoundTrip()
        {
            World world = World.Create();
            var intentions = new PulseRemoveIntentions();
            var entities = new RecordingRemoteEntities();
            var announcements = new NoOpAnnouncements();

            Measure.Method(() => RemoteEntitiesExtensions.Remove(entities, announcements, intentions, world))
                   .SampleGroup("remove_empty_fastpath")
                   .WarmupCount(5)
                   .MeasurementCount(50)
                   .IterationsPerMeasurement(2000)
                   .GC()
                   .Run();

            Measure.Method(() =>
                    {
                        using (intentions.Bunch()) { }
                    })
                   .SampleGroup("bunch_mutex_roundtrip")
                   .WarmupCount(5)
                   .MeasurementCount(50)
                   .IterationsPerMeasurement(2000)
                   .GC()
                   .Run();

            world.Dispose();

            double fast = MedianOf("remove_empty_fastpath");
            double slow = MedianOf("bunch_mutex_roundtrip");
            double ratio = slow / fast;

            TestContext.WriteLine($"empty-set fast path median = {fast:F6} ms / 2000 iters");
            TestContext.WriteLine($"mutex round-trip  median = {slow:F6} ms / 2000 iters");
            TestContext.WriteLine($"speedup ratio = {ratio:F2}x (higher = pre-check is eliding more work)");

            Assert.That(fast, Is.LessThan(slow),
                "Empty-set Remove must be cheaper than a forced mutex round-trip; the racy pre-check is not eliding the lock.");

            Assert.That(entities.RemoveCalls, Is.Zero, "Empty-set fast path must not invoke IRemoteEntities.Remove at all.");
        }

        /// <summary>
        ///     (b) Correctness under the racy fast path (lost-wakeup guard). A producer enqueues M distinct
        ///     intentions in bursts with random gaps while a consumer loop calls the production
        ///     <see cref="RemoteEntitiesExtensions.Remove"/> each simulated frame. Asserts every intention is
        ///     consumed exactly once and none is lost: the volatile/racy pre-check may observe a publish stale
        ///     for one frame, but never drops it — it is simply picked up the next frame.
        /// </summary>
        [Test]
        public void RacyFastPath_ConsumesEveryIntentionExactlyOnce()
        {
            const int M = 1000;

            World world = World.Create();
            var intentions = new PulseRemoveIntentions();
            var entities = new RecordingRemoteEntities();
            var announcements = new NoOpAnnouncements();

            var produced = new string[M];
            for (var i = 0; i < M; i++) produced[i] = $"0xwallet{i:D5}";

            var producerDone = false;

            var producer = new Thread(() =>
            {
                var rnd = new System.Random(0xC0FFEE);

                foreach (string wallet in produced)
                {
                    intentions.Enqueue(wallet);

                    if (rnd.Next(5) == 0)
                        Thread.Sleep(0);
                }

                Volatile.Write(ref producerDone, true);
            }) { IsBackground = true };

            producer.Start();

            var guard = 0;

            while (!Volatile.Read(ref producerDone) || intentions.NewBunchAvailable())
            {
                RemoteEntitiesExtensions.Remove(entities, announcements, intentions, world);

                if (++guard > 20_000_000)
                    Assert.Fail("Consumer failed to converge — possible lost wakeup / stuck pre-check.");

                Thread.Yield();
            }

            RemoteEntitiesExtensions.Remove(entities, announcements, intentions, world);

            producer.Join();
            world.Dispose();

            Assert.That(entities.Consumed.Count, Is.EqualTo(M),
                "Every enqueued intention must be consumed exactly once (none lost by the racy pre-check).");
            Assert.That(entities.Consumed.Distinct().Count(), Is.EqualTo(M),
                "No intention may be consumed twice or dropped — exactly-once delivery.");
        }

        /// <summary>
        ///     (c) Concurrent-writer stress. Two writer threads simultaneously enqueue disjoint ranges while the
        ///     consumer drains, exercising the <c>MutexSync</c> lock as the serialization point
        ///     between concurrent <c>Enqueue</c> (HashSet.Add) and the drain. A broken lock would surface as a
        ///     torn HashSet (lost/duplicated adds) or an exception. Asserts the final entity state is the full
        ///     2N union, each consumed exactly once.
        /// </summary>
        [Test]
        public void ConcurrentWriters_NoTornStateAllConsumedOnce()
        {
            const int N = 5000;

            World world = World.Create();
            var intentions = new PulseRemoveIntentions();
            var entities = new RecordingRemoteEntities();
            var announcements = new NoOpAnnouncements();

            var doneA = false;
            var doneB = false;

            var writerA = new Thread(() =>
            {
                for (var i = 0; i < N; i++) intentions.Enqueue($"0xA{i:D6}");
                Volatile.Write(ref doneA, true);
            }) { IsBackground = true };

            var writerB = new Thread(() =>
            {
                for (var i = 0; i < N; i++) intentions.Enqueue($"0xB{i:D6}");
                Volatile.Write(ref doneB, true);
            }) { IsBackground = true };

            writerA.Start();
            writerB.Start();

            var guard = 0;

            while (!Volatile.Read(ref doneA) || !Volatile.Read(ref doneB) || intentions.NewBunchAvailable())
            {
                RemoteEntitiesExtensions.Remove(entities, announcements, intentions, world);

                if (++guard > 40_000_000)
                    Assert.Fail("Consumer failed to converge under concurrent writers.");

                Thread.Yield();
            }

            RemoteEntitiesExtensions.Remove(entities, announcements, intentions, world);

            writerA.Join();
            writerB.Join();
            world.Dispose();

            Assert.That(entities.Consumed.Count, Is.EqualTo(2 * N),
                "the MutexSync lock must serialize concurrent Enqueue against the drain — no lost or torn adds.");
            Assert.That(entities.Consumed.Distinct().Count(), Is.EqualTo(2 * N),
                "Final entity state must be the full 2N union, each consumed exactly once — no lost or duplicated adds.");
        }
    }
}
