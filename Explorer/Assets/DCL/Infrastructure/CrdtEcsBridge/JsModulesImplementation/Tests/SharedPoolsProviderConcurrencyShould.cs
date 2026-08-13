using CrdtEcsBridge.PoolsProviders;
using NUnit.Framework;
using System;
using System.Buffers;
using System.Diagnostics;
using System.Threading;
using Unity.PerformanceTesting;

namespace CrdtEcsBridge.JsModulesImplementation.Tests
{
    /// <summary>
    ///     Verifies that concurrent Rent/Return on <see cref="SharedPoolsProvider" />'s ArrayPool-backed pools
    ///     never aliases a buffer across threads and always returns a cleared buffer, and that throughput holds
    ///     up against a manually-locked ArrayPool used here only as a synchronization-overhead reference point.
    /// </summary>
    [Category("Performance")]
    public class SharedPoolsProviderConcurrencyShould
    {
        private const int THREADS = 8;
        private const int BUFFER_SIZE = 2048;

        [Test]
        [Performance]
        public void ConcurrentRentReturn_NoAliasing_AndOutthroughputsLockedBaseline()
        {
            var provider = new SharedPoolsProvider();

            const int ALIAS_ITERS = 100_000;
            var violations = 0;

            RunThreads(THREADS, threadIndex =>
            {
                var sentinel = (byte)(threadIndex + 1);

                for (var i = 0; i < ALIAS_ITERS; i++)
                {
                    PoolableByteArray b = provider.GetSerializedStateBytesPool(BUFFER_SIZE);

                    b.Array[0] = sentinel;
                    b.Array[1023] = sentinel;
                    b.Array[2047] = sentinel;

                    Thread.SpinWait(50);

                    if (b.Array[0] != sentinel || b.Array[1023] != sentinel || b.Array[2047] != sentinel)
                        Interlocked.Increment(ref violations);

                    b.Dispose();
                }
            });

            Assert.AreEqual(0, violations, "a rented buffer must not be aliased across threads");

            for (var round = 0; round < 32; round++)
            {
                PoolableByteArray b = provider.GetSerializedStateBytesPool(BUFFER_SIZE);

                foreach (byte cell in b.Span)
                    Assert.AreEqual(0, cell, "re-rented buffer must be cleared");

                b.Span.Fill(0xFF);
                b.Dispose();
            }

            const int OPS_PER_THREAD = 300_000;

            double lockFreeOpsPerSec = MeasureOpsPerSec(OPS_PER_THREAD, () =>
            {
                PoolableByteArray b = provider.GetSerializedStateBytesPool(BUFFER_SIZE);
                b.Dispose();
            });

            var baselinePool = ArrayPool<byte>.Create();

            double lockedOpsPerSec = MeasureOpsPerSec(OPS_PER_THREAD, () =>
            {
                byte[] rented;
                lock (baselinePool) { rented = baselinePool.Rent(BUFFER_SIZE); }
                lock (baselinePool) { baselinePool.Return(rented, true); }
            });

            Measure.Custom(new SampleGroup("LockFree_opsPerSec", SampleUnit.Undefined), lockFreeOpsPerSec);
            Measure.Custom(new SampleGroup("Locked_opsPerSec", SampleUnit.Undefined), lockedOpsPerSec);

            Assert.GreaterOrEqual(lockFreeOpsPerSec, 1.5 * lockedOpsPerSec,
                $"lock-free provider should out-throughput the locked baseline by >=1.5x (lockFree={lockFreeOpsPerSec:F0}, locked={lockedOpsPerSec:F0})");
        }

        private static double MeasureOpsPerSec(int opsPerThread, Action op)
        {
            var sw = Stopwatch.StartNew();

            RunThreads(THREADS, _ =>
            {
                for (var i = 0; i < opsPerThread; i++)
                    op();
            });

            sw.Stop();
            long totalOps = (long)THREADS * opsPerThread;
            return totalOps / Math.Max(sw.Elapsed.TotalSeconds, 1e-6);
        }

        private static void RunThreads(int count, Action<int> body)
        {
            using var barrier = new Barrier(count);
            var threads = new Thread[count];

            for (var t = 0; t < count; t++)
            {
                int index = t;
                threads[t] = new Thread(() =>
                {
                    barrier.SignalAndWait();
                    body(index);
                });
                threads[t].Start();
            }

            foreach (Thread thread in threads)
                thread.Join();
        }
    }
}
