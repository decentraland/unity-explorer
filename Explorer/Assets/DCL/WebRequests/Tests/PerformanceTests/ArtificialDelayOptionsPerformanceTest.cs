using Cysharp.Threading.Tasks;
using NUnit.Framework;
using System;
using System.Threading;
using Unity.PerformanceTesting;

namespace DCL.WebRequests.Tests.PerformanceTests
{
    /// <summary>
    /// Guards fix #17: <see cref="ArtificialDelayOptions.ElementBindingOptions.GetOptionsAsync"/> used to enter
    /// <c>ExecuteOnMainThreadScope.NewScopeWithReturnOnOriginalThreadAsync()</c> — a forced main-thread hop plus
    /// two PlayerPrefs reads — on EVERY web request, purely to read a debug toggle that is OFF by default. The
    /// fix caches the toggle (kept live via the binding change event) and returns a synchronously-completed
    /// <c>UniTask.FromResult</c>.
    /// <para>
    /// The tests falsify the fix along three axes:
    /// (a) allocation — N calls must allocate ~nothing (a completed <c>UniTask&lt;(float,bool)&gt;</c> is a struct);
    /// (b) no main-thread hop — a call issued from a ThreadPool thread must complete SYNCHRONOUSLY
    ///     (<c>Status == Succeeded</c>). The pre-fix path returns <c>Pending</c> off-thread because it awaits
    ///     <c>SwitchToMainThread</c>, so this is the decisive discriminator;
    /// (c) live toggle — after the value changes, the very next call must return the new tuple (no stale cache).
    /// </para>
    /// </summary>
    [Category("Performance")]
    public class ArtificialDelayOptionsPerformanceTest
    {
        private const int N = 1000;

        private ArtificialDelayOptions.ElementBindingOptions options = null!;

        [SetUp]
        public void SetUp()
        {
            // Default ctor reads the two PlayerPrefs-backed settings on the main thread and seeds the cache.
            options = new ArtificialDelayOptions.ElementBindingOptions();
            // Deterministic starting point regardless of any previously-persisted debug prefs.
            options.ApplyValues(false, 10f);
        }

        [TearDown]
        public void TearDown()
        {
            // Reset the persisted debug toggle back to its shipped defaults so the test leaves no residue.
            options.ApplyValues(false, 10f);
        }

        /// <summary>
        /// (a) Allocation: priming once, then N GetOptionsAsync calls must add ~zero managed bytes and every
        /// returned task must already be completed. A regression to the async main-thread-scope path reintroduces
        /// per-call state-machine churn and (off the main thread) a non-completed task.
        /// </summary>
        [Test, Performance]
        public void GetOptionsAsync_IsAllocationFreeAndSynchronous()
        {
            // Prime: JIT the path and let any one-time allocation settle before measuring.
            for (int i = 0; i < 64; i++)
                Assert.AreEqual(UniTaskStatus.Succeeded, options.GetOptionsAsync(CancellationToken.None).Status);

            // No boxing/allocating asserts inside the measured region: accumulate primitives, assert afterwards.
            int notCompleted = 0;
            float sink = 0f;

            long before = GC.GetTotalMemory(false);

            for (int i = 0; i < N; i++)
            {
                UniTask<(float ArtificialDelaySeconds, bool UseDelay)> task = options.GetOptionsAsync(CancellationToken.None);

                if (task.Status != UniTaskStatus.Succeeded)
                    notCompleted++;

                (float ArtificialDelaySeconds, bool UseDelay) r = task.GetAwaiter().GetResult();
                sink += r.ArtificialDelaySeconds;
            }

            long delta = GC.GetTotalMemory(false) - before;

            Assert.AreEqual(0, notCompleted, "Every GetOptionsAsync must complete synchronously (no main-thread hop)");
            Assert.IsTrue(float.IsFinite(sink));

            Measure.Custom(new SampleGroup("GetOptionsAsync.TotalAllocatedBytes", SampleUnit.Byte), delta);

            // A completed UniTask<T> over a value tuple is heap-allocation free. Allow a tiny slack for the
            // GC counter's own bookkeeping but reject anything resembling per-call boxing (the pre-fix path).
            Assert.Less(delta, 4L * N, $"Expected near-zero allocation over {N} calls, got {delta} bytes");
        }

        /// <summary>
        /// (b) No main-thread hop — the decisive discriminator. Issued from a ThreadPool thread the call must
        /// complete synchronously. The pre-fix implementation awaits <c>UniTask.SwitchToMainThread()</c> off the
        /// main thread and therefore returns a <c>Pending</c> task here.
        /// </summary>
        [Test, Performance]
        public void GetOptionsAsync_CompletesSynchronously_OffMainThread()
        {
            UniTaskStatus observedStatus = UniTaskStatus.Pending;
            (float ArtificialDelaySeconds, bool UseDelay) result = default;
            Exception? failure = null;

            var worker = new Thread(() =>
            {
                try
                {
                    UniTask<(float ArtificialDelaySeconds, bool UseDelay)> task = options.GetOptionsAsync(CancellationToken.None);
                    observedStatus = task.Status;

                    if (observedStatus == UniTaskStatus.Succeeded)
                        result = task.GetAwaiter().GetResult();
                }
                catch (Exception e) { failure = e; }
            });

            worker.Start();
            Assert.IsTrue(worker.Join(TimeSpan.FromSeconds(5)), "Worker thread did not finish — the call blocked on a main-thread hop");

            Assert.IsNull(failure, $"GetOptionsAsync threw off the main thread: {failure}");
            Assert.AreEqual(UniTaskStatus.Succeeded, observedStatus,
                "Off the main thread GetOptionsAsync must complete synchronously (no ExecuteOnMainThreadScope hop)");
            Assert.AreEqual(10f, result.ArtificialDelaySeconds);
            Assert.IsFalse(result.UseDelay);
        }

        /// <summary>
        /// (c) Live toggle — after the value changes the very next call must observe the new tuple. Falsifies a
        /// broken/stale cache that would never pick the debug edit up.
        /// </summary>
        [Test, Performance]
        public void GetOptionsAsync_ReflectsToggle_WithinOneRequest()
        {
            (float ArtificialDelaySeconds, bool UseDelay) before = options.GetOptionsAsync(CancellationToken.None).GetAwaiter().GetResult();
            Assert.IsFalse(before.UseDelay);
            Assert.AreEqual(10f, before.ArtificialDelaySeconds);

            // Mid-run change of the debug toggle (enable false->true, delay 10->3).
            options.ApplyValues(true, 3f);

            (float ArtificialDelaySeconds, bool UseDelay) after = options.GetOptionsAsync(CancellationToken.None).GetAwaiter().GetResult();
            Assert.IsTrue(after.UseDelay, "Toggle change was not reflected — cache is stale");
            Assert.AreEqual(3f, after.ArtificialDelaySeconds, "Delay change was not reflected — cache is stale");
        }
    }
}
