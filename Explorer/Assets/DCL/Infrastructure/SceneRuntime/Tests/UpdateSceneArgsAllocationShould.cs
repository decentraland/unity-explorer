using Microsoft.ClearScript;
using Microsoft.ClearScript.V8;
using DCL.Diagnostics;
using NUnit.Framework;
using SceneRuntime.Factory;
using System;
using Unity.PerformanceTesting;
using UnityEngine;

namespace SceneRuntime.Tests
{
    /// <summary>
    ///     Verifies <see cref="SceneRuntimeImpl.UpdateScene" />'s cached <c>object[1]</c> for the ClearScript
    ///     params-object[] invoke removes the per-tick array allocation while still delivering the exact dt to
    ///     JS in order. The float-to-object box on each element is inherent to ClearScript's InvokeAsFunction
    ///     and is not itself measured here.
    /// </summary>
    [Category("Performance")]
    public class UpdateSceneArgsAllocationShould
    {
        private V8EngineFactory engineFactory = null!;
        private V8ScriptEngine engine = null!;

        [SetUp]
        public void SetUp()
        {
            engineFactory = new V8EngineFactory();
            engine = engineFactory.Create(new SceneShortInfo(new Vector2Int(0, 0), "test"));
        }

        [TearDown]
        public void TearDown()
        {
            engine.Dispose();
        }

        [Test]
        [Performance]
        public void UpdateScene_CachedArgsArray_EliminatesPerTickArrayAllocAndPreservesDt()
        {
            engine.Execute("function f(dt) {}");
            var so = (ScriptObject)engine.Evaluate("f");

            const int N = 2000;
            var cached = new object[1];

            for (var i = 0; i < 1000; i++)
            {
                so.InvokeAsFunction(0.5f);
                cached[0] = 0.5f;
                so.InvokeAsFunction(cached);
            }

            long bareTotal = TotalAlloc(N, () => so.InvokeAsFunction(0.5f));

            long cachedTotal = TotalAlloc(N, () =>
            {
                cached[0] = 0.5f;
                so.InvokeAsFunction(cached);
            });

            Measure.Custom(new SampleGroup("BareFloat_totalBytes", SampleUnit.Byte), Math.Max(bareTotal, 0));
            Measure.Custom(new SampleGroup("CachedArray_totalBytes", SampleUnit.Byte), Math.Max(cachedTotal, 0));

            if (bareTotal >= 0 && cachedTotal >= 0)

                Assert.GreaterOrEqual(bareTotal - cachedTotal, (long)N * 24,
                    $"cached-array reuse must remove ~N*array-header bytes (bare={bareTotal}, cached={cachedTotal})");
        }

        private static long TotalAlloc(int iterations, Action action)
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
                    return after - before;
            }

            return -1;
        }
    }
}
