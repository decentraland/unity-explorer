using NUnit.Framework;
using System;
using System.Collections;
using System.Threading;
using UnityEngine.Profiling;
using UnityEngine.TestTools;
using Utility;

namespace DCL.Tests.Editor
{
    public class EventBusShould
    {
        private const int WARMUP_PUBLISHES = 20_000;
        private const int MEASURED_PUBLISHES = 10_000;

        // Rounds published while the main thread is parked (blocked, never yielding): each forces
        // MEASURED_PUBLISHES continuations in flight simultaneously, which primes the pooled-hop
        // free list to >= the window size and grows the then-active half of UniTask's
        // double-buffered ContinuationQueue past the window. The queue swaps its two arrays after
        // every non-empty drain, so two rounds grow both physical arrays regardless of how the
        // editor pumped the initial burst.
        private const int PARKED_ROUNDS = 2;

        // The window counts GC.Alloc profiler samples on the publishing thread — allocation events,
        // not bytes, because byte counters are unreliable on the Boehm editor runtime
        // (GC.GetAllocatedBytesForCurrentThread is inert; GC.GetTotalMemory hides small-object
        // churn behind lazy sweep). The unpooled hop emits 2 events per publish (closure + delegate,
        // ~20k over the window); the pooled hop emits none — its worst case is ~10 regrowth events
        // if an unrelated editor continuation swaps the queue's buffers between the parked rounds;
        // pool misses are impossible because the rounds prime the pool deterministically. The budget
        // sits between the two, so the assertion discriminates under every editor-pump schedule.
        private const int ALLOC_SAMPLE_BUDGET = 2_000;

        // Must all register on the GC.Alloc recorder for the budget assertion to be meaningful.
        private const int CANARY_ALLOCS = 64;

        private static readonly TimeSpan PUMP_TIMEOUT = TimeSpan.FromSeconds(60);

        private int invoked;

        [UnityTest]
        public IEnumerator NotAllocatePerOffMainThreadPublish()
        {
            //Arrange — a main-thread-invoking bus with one subscriber; publishing from a dedicated
            // thread guarantees every Publish takes the thread-hop (AddContinuation) branch.
            invoked = 0;
            var bus = new EventBus(invokeSubscribersOnMainThread: true);
            using IDisposable subscription = bus.Subscribe<TestEvent>(_ => Interlocked.Increment(ref invoked));

            var measuredAllocSamples = -1;
            Exception? threadError = null;
            var warmupPublished = new ManualResetEventSlim(false);
            var warmupDrained = new ManualResetEventSlim(false);
            var roundPublished = new ManualResetEventSlim[PARKED_ROUNDS];
            var roundDrained = new ManualResetEventSlim[PARKED_ROUNDS];

            for (var i = 0; i < PARKED_ROUNDS; i++)
            {
                roundPublished[i] = new ManualResetEventSlim(false);
                roundDrained[i] = new ManualResetEventSlim(false);
            }

            var publisher = new Thread(() =>
            {
                try
                {
                    // Warm-up burst: JITs the publish path; the editor may drain it on any schedule.
                    for (var i = 0; i < WARMUP_PUBLISHES; i++)
                        bus.Publish(new TestEvent { Value = i });

                    warmupPublished.Set();

                    if (!warmupDrained.Wait(PUMP_TIMEOUT))
                        throw new TimeoutException("main thread never drained the warm-up publishes");

                    // Parked rounds: the main thread is blocked (no drains can run), so the whole
                    // round is in flight at once when it finishes.
                    for (var round = 0; round < PARKED_ROUNDS; round++)
                    {
                        for (var i = 0; i < MEASURED_PUBLISHES; i++)
                            bus.Publish(new TestEvent { Value = i });

                        roundPublished[round].Set();

                        if (!roundDrained[round].Wait(PUMP_TIMEOUT))
                            throw new TimeoutException($"main thread never drained parked round {round}");
                    }

                    // Same recorder pattern as UnityEngine.TestTools' AllocatingGCMemory
                    // constraint, run on the publishing thread: FilterToCurrentThread binds it to
                    // this thread and the enabled toggle resets the sample count. The whole window
                    // sits inside one editor frame because the main thread is parked in Join.
                    Recorder gcAllocRecorder = Recorder.Get("GC.Alloc");
                    gcAllocRecorder.FilterToCurrentThread();
                    gcAllocRecorder.enabled = false;
                    gcAllocRecorder.enabled = true;

                    // Canary allocations: the budget assertion would be vacuous on a recorder that
                    // does not register events from this thread.
                    object? canarySink = null;

                    for (var i = 0; i < CANARY_ALLOCS; i++)
                        canarySink = new object();

                    for (var i = 0; i < MEASURED_PUBLISHES; i++)
                        bus.Publish(new TestEvent { Value = i });

                    gcAllocRecorder.enabled = false;
                    measuredAllocSamples = gcAllocRecorder.sampleBlockCount;
                    GC.KeepAlive(canarySink);
                }
                catch (Exception e) { threadError = e; }
            });

            publisher.Start();

            // Pump the editor loop until the warm-up continuations have all run on the main thread.
            DateTime deadline = DateTime.UtcNow + PUMP_TIMEOUT;

            while ((!warmupPublished.IsSet || Volatile.Read(ref invoked) < WARMUP_PUBLISHES) && DateTime.UtcNow < deadline)
                yield return null;

            Assert.IsNull(threadError, threadError?.ToString());

            Assert.AreEqual(WARMUP_PUBLISHES, Volatile.Read(ref invoked),
                "editor loop did not pump the queued main-thread continuations — environment failure, not the allocation regression under test");

            warmupDrained.Set();

            for (var round = 0; round < PARKED_ROUNDS; round++)
            {
                // A blocking Wait (no yield) is what parks the editor loop while the round publishes.
                bool roundReady = roundPublished[round].Wait(PUMP_TIMEOUT);
                Assert.IsNull(threadError, threadError?.ToString());
                Assert.IsTrue(roundReady, $"publisher thread never finished parked round {round}");

                int expected = WARMUP_PUBLISHES + ((round + 1) * MEASURED_PUBLISHES);
                deadline = DateTime.UtcNow + PUMP_TIMEOUT;

                while (Volatile.Read(ref invoked) < expected && DateTime.UtcNow < deadline)
                    yield return null;

                Assert.AreEqual(expected, Volatile.Read(ref invoked),
                    $"editor loop did not drain parked round {round} — environment failure, not the allocation regression under test");

                roundDrained[round].Set();
            }

            //Act — the recorder is bound to the publishing thread and needs no pumping; the bounded
            // Join parks the main thread so no drain, recycle, or buffer swap can land inside the window.
            Assert.IsTrue(publisher.Join(PUMP_TIMEOUT), "publisher thread did not finish");
            Assert.IsNull(threadError, threadError?.ToString());

            //Assert
            Assert.GreaterOrEqual(measuredAllocSamples, CANARY_ALLOCS,
                "GC.Alloc recorder registered fewer events than the canary allocated — environment failure, not the allocation regression under test");

            Assert.Less(measuredAllocSamples, ALLOC_SAMPLE_BUDGET,
                $"off-main-thread Publish emitted {measuredAllocSamples} GC.Alloc events over {MEASURED_PUBLISHES} publishes");

            // Drain the measured publishes: exactly-once delivery — nothing lost or duplicated by the hop.
            const int TOTAL_PUBLISHES = WARMUP_PUBLISHES + ((PARKED_ROUNDS + 1) * MEASURED_PUBLISHES);
            deadline = DateTime.UtcNow + PUMP_TIMEOUT;

            while (Volatile.Read(ref invoked) < TOTAL_PUBLISHES && DateTime.UtcNow < deadline)
                yield return null;

            Assert.AreEqual(TOTAL_PUBLISHES, Volatile.Read(ref invoked));
        }

        private struct TestEvent
        {
            public int Value;
        }
    }
}
