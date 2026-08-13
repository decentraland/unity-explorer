using NUnit.Framework;
using SceneRunner.Scene;
using System;
using System.Diagnostics;
using System.Threading;
using Unity.PerformanceTesting;

namespace SceneRunner.Tests
{
    /// <summary>
    ///     Verifies <see cref="SceneStateProviderExtensions.IsNotRunningState" />'s lock-free read (via
    ///     Atomic's implicit conversion) has exact semantic parity with a Monitor-guarded read, is measurably
    ///     faster, and never observes a torn or invalid enum value under concurrent reads and writes.
    /// </summary>
    [Category("Performance")]
    public class SceneStateLockFreeReadShould
    {
        private static volatile bool sink;

        private static bool LockedIsNotRunning(ISceneStateProvider provider)
        {
            SceneState state = provider.State.Value();

            return state
                is SceneState.Disposing
                or SceneState.Disposed
                or SceneState.JavaScriptError
                or SceneState.EngineError;
        }

        [Test]
        [Performance]
        public void IsNotRunningState_LockFreeRead_FasterThanLockedAndNeverTears()
        {
            var provider = new SceneStateProvider();

            foreach (SceneState value in Enum.GetValues(typeof(SceneState)))
            {
                provider.State.Set(value);
                Assert.AreEqual(LockedIsNotRunning(provider), provider.IsNotRunningState(),
                    $"semantic parity must hold for {value}");
            }

            provider.State.Set(SceneState.Running);

            const long WARMUP = 100_000;
            const long ITER = 10_000_000;

            var warm = false;
            for (long i = 0; i < WARMUP; i++) { warm ^= provider.IsNotRunningState(); warm ^= LockedIsNotRunning(provider); }
            sink = warm;

            var acc = false;
            var sw = Stopwatch.StartNew();
            for (long i = 0; i < ITER; i++) acc ^= provider.IsNotRunningState();
            sw.Stop();
            sink = acc;
            double lockFreeNs = sw.Elapsed.TotalMilliseconds * 1e6 / ITER;

            acc = false;
            sw.Restart();
            for (long i = 0; i < ITER; i++) acc ^= LockedIsNotRunning(provider);
            sw.Stop();
            sink = acc;
            double lockedNs = sw.Elapsed.TotalMilliseconds * 1e6 / ITER;

            Measure.Custom(new SampleGroup("LockFreeRead_ns", SampleUnit.Nanosecond), lockFreeNs);
            Measure.Custom(new SampleGroup("LockedRead_ns", SampleUnit.Nanosecond), lockedNs);

            Assert.LessOrEqual(lockFreeNs, 0.5 * lockedNs,
                $"lock-free read must be >=2x faster (lockFree={lockFreeNs:F2}ns, locked={lockedNs:F2}ns)");

            var valid = new bool[256];
            foreach (SceneState value in Enum.GetValues(typeof(SceneState)))
                valid[(byte)value] = true;

            SceneState[] cycle =
            {
                SceneState.Starting, SceneState.Running, SceneState.Disposing,
                SceneState.Disposed, SceneState.JavaScriptError, SceneState.EngineError,
            };

            var stop = false;
            var invalid = 0;
            var exceptions = 0;
            const int READERS = 4;
            var readCounts = new long[READERS];

            var writer = new Thread(() =>
            {
                var i = 0;
                while (!Volatile.Read(ref stop))
                {
                    provider.State.Set(cycle[i % cycle.Length]);
                    i++;
                }
            });

            var readers = new Thread[READERS];
            for (var r = 0; r < READERS; r++)
            {
                int index = r;
                readers[r] = new Thread(() =>
                {
                    long reads = 0;
                    try
                    {
                        while (!Volatile.Read(ref stop))
                        {
                            bool notRunning = provider.IsNotRunningState();
                            SceneState observed = provider.State;

                            if (!valid[(byte)observed])
                                Interlocked.Increment(ref invalid);

                            sink ^= notRunning;
                            reads++;
                        }
                    }
                    catch { Interlocked.Increment(ref exceptions); }

                    readCounts[index] = reads;
                });
            }

            writer.Start();
            foreach (Thread reader in readers) reader.Start();

            Thread.Sleep(2000);
            Volatile.Write(ref stop, true);

            writer.Join();
            foreach (Thread reader in readers) reader.Join();

            Assert.AreEqual(0, invalid, "no torn/invalid enum byte may ever be observed");
            Assert.AreEqual(0, exceptions, "lock-free reads must not throw");

            for (var r = 0; r < READERS; r++)
                Assert.Greater(readCounts[r], 1_000_000, $"reader {r} should perform >1e6 reads in 2s");
        }
    }
}
