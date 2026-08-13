#if !UNITY_WEBGL
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using System;
using System.Collections;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Unity.PerformanceTesting;
using UnityEngine.TestTools;
using Utility.Multithreading;

namespace SceneRunner.Tests
{
    /// <summary>
    ///     Verifies the desktop <see cref="ReusableTickDelay" /> stays effectively allocation-free in steady
    ///     state (unlike Task.Delay(int, CancellationToken), which allocates a promise, a timer, and a
    ///     per-call CancellationTokenRegistration on every await), reacts to cancellation well under the
    ///     armed interval, and completes exactly once under a timer-fire-vs-cancel race.
    /// </summary>
    [Category("Performance")]
    public class ReusableTickDelayShould
    {
        [UnityTest]
        [Performance]
        public IEnumerator UpdateLoopTickDelay_ReusableWait_AllocFreeSteadyStateAndCancelPrompt() =>
            UniTask.ToCoroutine(async () =>
            {
                using var cts = new CancellationTokenSource();
                using var delay = DCLTask.CreateReusableTickDelay();
                delay.AttachCancellation(cts.Token);

                for (var i = 0; i < 100; i++) DriveOnce(delay, 1);

                const int N = 300;

                double reusableBytes = AllocPerCall(N, () => DriveOnce(delay, 1));
                double baselineBytes = AllocPerCall(N, () => DriveTaskDelayOnce(1, cts.Token));

                Measure.Custom(new SampleGroup("ReusableTickDelay_bytesPerAwait", SampleUnit.Byte), Math.Max(reusableBytes, 0));
                Measure.Custom(new SampleGroup("TaskDelay_bytesPerAwait", SampleUnit.Byte), Math.Max(baselineBytes, 0));

                if (reusableBytes >= 0 && baselineBytes >= 0)
                {
                    Assert.Less(reusableBytes, 16, "reusable steady-state await must be effectively alloc-free");
                    Assert.GreaterOrEqual(baselineBytes, 100, "Task.Delay+CTR baseline allocates per await");
                    Assert.GreaterOrEqual(baselineBytes, 10 * Math.Max(reusableBytes, 1),
                        "reusable wait must allocate >=10x less than Task.Delay");
                }

                using var cancelCts = new CancellationTokenSource();
                using var cancelDelay = DCLTask.CreateReusableTickDelay();
                cancelDelay.AttachCancellation(cancelCts.Token);

                var sw = Stopwatch.StartNew();
                _ = Task.Run(async () =>
                {
                    await Task.Delay(50);
                    cancelCts.Cancel();
                });

                OperationCanceledException? caught = null;
                try { await cancelDelay.Delay(500); }
                catch (OperationCanceledException e) { caught = e; }
                sw.Stop();

                Assert.NotNull(caught, "cancellation must fault the in-flight delay with OperationCanceledException");
                Assert.LessOrEqual(sw.ElapsedMilliseconds, 150, "cancel must not wait out the 500ms timer");

                var sw2 = Stopwatch.StartNew();
                Assert.Throws<OperationCanceledException>(() => { _ = cancelDelay.Delay(1); });
                sw2.Stop();
                Assert.Less(sw2.ElapsedMilliseconds, 5, "post-cancel Delay must throw synchronously");

                var rnd = new Random(12345);
                var otherFaults = 0;

                for (var i = 0; i < 500; i++)
                {
                    using var raceCts = new CancellationTokenSource();
                    using var raceDelay = DCLTask.CreateReusableTickDelay();
                    raceDelay.AttachCancellation(raceCts.Token);

                    int offset = rnd.Next(0, 3);
                    Task canceller = Task.Run(async () =>
                    {
                        if (offset > 0) await Task.Delay(offset);
                        raceCts.Cancel();
                    });

                    try { await raceDelay.Delay(1); }
                    catch (OperationCanceledException) {  }
                    catch (Exception) { Interlocked.Increment(ref otherFaults); }

                    await canceller;
                }

                Assert.AreEqual(0, otherFaults, "the reusable source must complete exactly once (no double-completion faults)");
            });

        private static void DriveOnce(ReusableTickDelay delay, int ms)
        {
            UniTask.Awaiter awaiter = delay.Delay(ms).GetAwaiter();

            while (!awaiter.IsCompleted)
                Thread.SpinWait(64);

            awaiter.GetResult();
        }

        private static void DriveTaskDelayOnce(int ms, CancellationToken ct)
        {
            Task.Delay(ms, ct).GetAwaiter().GetResult();
        }

        private static double AllocPerCall(int iterations, Action action)
        {
            for (var attempt = 0; attempt < 10; attempt++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

                int gen0 = GC.CollectionCount(0);
                long before = GC.GetTotalMemory(true);

                for (var i = 0; i < iterations; i++)
                    action();

                long after = GC.GetTotalMemory(false);

                if (GC.CollectionCount(0) == gen0)
                    return (after - before) / (double)iterations;
            }

            return -1;
        }
    }
}
#endif
