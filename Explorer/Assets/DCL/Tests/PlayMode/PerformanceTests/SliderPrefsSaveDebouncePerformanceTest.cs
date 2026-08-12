using DCL.Prefs;
using NUnit.Framework;
using System.Collections;
using System.Threading;
using Unity.PerformanceTesting;
using UnityEngine;
using UnityEngine.TestTools;

namespace DCL.Tests.PlayMode.PerformanceTests
{
    /// <summary>
    ///     Verifies that PrefsSaveDebouncer coalesces a full drag's worth of onValueChanged ticks into a single
    ///     DCLPlayerPrefs.Save(), preserving the final value, and that FlushIfPending flushes synchronously.
    ///     Uses a stub save action; no real DCLPlayerPrefs statics are touched.
    /// </summary>
    public class SliderPrefsSaveDebouncePerformanceTest
    {
        [UnityTest]
        [Performance]
        public IEnumerator SixtyDragTicks_CoalesceToSingleSave_AndFlushPreservesFinalValue()
        {
            int saveCount = 0;
            float valueAtSave = -1f;
            float current = 0f;

            using var debouncer = new PrefsSaveDebouncer(() =>
            {
                Interlocked.Increment(ref saveCount);
                valueAtSave = current;
            }, debounceMs: 50);

            for (var i = 0; i < 60; i++)
            {
                current = i;
                debouncer.RequestSave();
            }

            float deadline = UnityEngine.Time.realtimeSinceStartup + 5f;
            while (Volatile.Read(ref saveCount) == 0 && UnityEngine.Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.AreEqual(1, Volatile.Read(ref saveCount), "60 drag ticks must coalesce into exactly one Save call");
            Assert.AreEqual(59f, valueAtSave, "the coalesced save must observe the final tick's value");

            float wait = UnityEngine.Time.realtimeSinceStartup + 0.15f;
            while (UnityEngine.Time.realtimeSinceStartup < wait)
                yield return null;

            Assert.AreEqual(1, Volatile.Read(ref saveCount), "the debounce timer must not fire a second time");

            current = 123f;
            debouncer.RequestSave();
            debouncer.RequestSave();
            debouncer.RequestSave();
            debouncer.FlushIfPending();

            Assert.AreEqual(2, Volatile.Read(ref saveCount), "FlushIfPending must flush the pending save synchronously");
            Assert.AreEqual(123f, valueAtSave, "flush must persist the latest value");

            float wait2 = UnityEngine.Time.realtimeSinceStartup + 0.15f;
            while (UnityEngine.Time.realtimeSinceStartup < wait2)
                yield return null;

            Assert.AreEqual(2, Volatile.Read(ref saveCount), "no further fire after a flush cleared the pending flag");

            Measure.Method(() => debouncer.RequestSave())
                   .WarmupCount(5)
                   .MeasurementCount(20)
                   .GC()
                   .Run();

            debouncer.FlushIfPending();
        }
    }
}
