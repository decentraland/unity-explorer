using NUnit.Framework;
using System;
using System.Collections;
using System.Threading;
using UnityEngine.Profiling;
using UnityEngine.TestTools;

namespace Utility.Tests
{
    public class EventBusShould
    {
        private const int WARMUP_PUBLISHES = 20_000;
        private const int MEASURED_PUBLISHES = 10_000;

        // Rounds published while the main thread is parked: they prime the pooled-hop free list and
        // grow UniTask's continuation-queue buffers past the window size before measuring.
        private const int PARKED_ROUNDS = 2;

        // Counts GC.Alloc events, not bytes (byte counters are unreliable on the Boehm editor runtime).
        // The unpooled hop emits ~2 events per publish (~20k over the window), the pooled hop near zero; the budget sits between.
        private const int ALLOC_SAMPLE_BUDGET = 2_000;

        // Must all register on the GC.Alloc recorder for the budget assertion to be meaningful.
        private const int CANARY_ALLOCS = 64;

        private static readonly TimeSpan PUMP_TIMEOUT = TimeSpan.FromSeconds(60);

        private int invoked;

        [UnityTest]
        public IEnumerator NotAllocatePerOffMainThreadPublish()
        {
            // Publishing from a dedicated thread guarantees every Publish takes the thread-hop (AddContinuation) branch.
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
                    // Warm-up burst: JITs the publish path.
                    for (var i = 0; i < WARMUP_PUBLISHES; i++)
                        bus.Publish(new TestEvent { Value = i });

                    warmupPublished.Set();

                    if (!warmupDrained.Wait(PUMP_TIMEOUT))
                        throw new TimeoutException("main thread never drained the warm-up publishes");

                    // The main thread is blocked, so the whole round is in flight at once when it finishes.
                    for (var round = 0; round < PARKED_ROUNDS; round++)
                    {
                        for (var i = 0; i < MEASURED_PUBLISHES; i++)
                            bus.Publish(new TestEvent { Value = i });

                        roundPublished[round].Set();

                        if (!roundDrained[round].Wait(PUMP_TIMEOUT))
                            throw new TimeoutException($"main thread never drained parked round {round}");
                    }

                    // FilterToCurrentThread binds the recorder to this thread; the enabled toggle resets the sample count.
                    Recorder gcAllocRecorder = Recorder.Get("GC.Alloc");
                    gcAllocRecorder.FilterToCurrentThread();
                    gcAllocRecorder.enabled = false;
                    gcAllocRecorder.enabled = true;

                    // Canary allocations prove the recorder registers events from this thread.
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

            try
            {
                publisher.Start();

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

                // Join parks the main thread so no drain, recycle, or buffer swap can land inside the measured window.
                Assert.IsTrue(publisher.Join(PUMP_TIMEOUT), "publisher thread did not finish");
                Assert.IsNull(threadError, threadError?.ToString());

                Assert.GreaterOrEqual(measuredAllocSamples, CANARY_ALLOCS,
                    "GC.Alloc recorder registered fewer events than the canary allocated — environment failure, not the allocation regression under test");

                Assert.Less(measuredAllocSamples, ALLOC_SAMPLE_BUDGET,
                    $"off-main-thread Publish emitted {measuredAllocSamples} GC.Alloc events over {MEASURED_PUBLISHES} publishes");

                // Exactly-once delivery: nothing lost or duplicated by the hop.
                const int TOTAL_PUBLISHES = WARMUP_PUBLISHES + ((PARKED_ROUNDS + 1) * MEASURED_PUBLISHES);
                deadline = DateTime.UtcNow + PUMP_TIMEOUT;

                while (Volatile.Read(ref invoked) < TOTAL_PUBLISHES && DateTime.UtcNow < deadline)
                    yield return null;

                Assert.AreEqual(TOTAL_PUBLISHES, Volatile.Read(ref invoked));
            }
            finally
            {
                // Set releases the publisher from any Wait on an early exit; Dispose only after Join,
                // once no other thread can still touch the events.
                warmupPublished.Set();
                warmupDrained.Set();

                for (var i = 0; i < PARKED_ROUNDS; i++)
                {
                    roundPublished[i].Set();
                    roundDrained[i].Set();
                }

                if (publisher.IsAlive)
                    publisher.Join(PUMP_TIMEOUT);

                warmupPublished.Dispose();
                warmupDrained.Dispose();

                for (var i = 0; i < PARKED_ROUNDS; i++)
                {
                    roundPublished[i].Dispose();
                    roundDrained[i].Dispose();
                }
            }
        }

        private struct TestEvent
        {
            public int Value;
        }
    }
}
