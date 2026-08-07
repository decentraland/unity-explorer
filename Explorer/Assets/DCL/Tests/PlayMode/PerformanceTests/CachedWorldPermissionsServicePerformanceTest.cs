using Cysharp.Threading.Tasks;
using DCL.PrivateWorlds;
using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Unity.PerformanceTesting;
using UnityEngine.TestTools;

namespace DCL.Tests.PlayMode.PerformanceTests
{
    /// <summary>
    ///     Verifies CachedWorldPermissionsService collapses per-recycle private-world access checks to one fetch
    ///     per (world, wallet) within TTL, while a wallet change, a transient CheckFailed, and TTL expiry each
    ///     force a re-fetch so access is never shown stale.
    /// </summary>
    public class CachedWorldPermissionsServicePerformanceTest
    {
        private sealed class StubPermissions : IWorldPermissionsService
        {
            public int TotalCalls;
            public readonly Dictionary<string, int> CallsPerWorld = new ();
            public Func<string, int, WorldAccessCheckResult> ResultFor = (_, _) => WorldAccessCheckResult.Allowed;

            public UniTask<WorldAccessCheckContext> CheckWorldAccessAsync(string worldName, CancellationToken ct)
            {
                TotalCalls++;
                CallsPerWorld.TryGetValue(worldName, out int c);
                int callIndexForWorld = c + 1;
                CallsPerWorld[worldName] = callIndexForWorld;
                return UniTask.FromResult(new WorldAccessCheckContext { Result = ResultFor(worldName, callIndexForWorld) });
            }

            public UniTask<WorldAccessInfo> GetWorldPermissionsAsync(string worldName, CancellationToken ct) =>
                throw new NotImplementedException();

            public UniTask<ValidatePasswordResult> ValidatePasswordAsync(string worldName, string password, CancellationToken ct) =>
                UniTask.FromResult(ValidatePasswordResult.Ok);
        }

        [UnityTest]
        [Performance]
        public IEnumerator ScrollRecycle_5PrivateWorlds_11Passes_OneFetchPerWorld_WalletChangeAndFailureInvalidate() =>
            UniTask.ToCoroutine(async () =>
            {
                var inner = new StubPermissions();
                string[] wallet = { "0xAAA" };
                float[] fakeTime = { 0f };
                string[] worlds = { "w1", "w2", "w3", "w4", "w5" };

                var decorator = new CachedWorldPermissionsService(
                    inner,
                    () => wallet[0],
                    TimeSpan.FromSeconds(30),
                    () => fakeTime[0]);

                for (var pass = 0; pass < 11; pass++)
                    foreach (string world in worlds)
                    {
                        WorldAccessCheckContext ctx = await decorator.CheckWorldAccessAsync(world, CancellationToken.None);
                        Assert.AreEqual(WorldAccessCheckResult.Allowed, ctx.Result);
                    }

                Assert.AreEqual(5, inner.TotalCalls, "within TTL, distinct-world fetch count must equal the number of worlds, not recycles");

                wallet[0] = "0xBBB";
                foreach (string world in worlds)
                    await decorator.CheckWorldAccessAsync(world, CancellationToken.None);

                Assert.AreEqual(10, inner.TotalCalls, "an identity switch must invalidate the cache");

                inner.ResultFor = (name, callIndex) =>
                    name == "w6" && callIndex == 1 ? WorldAccessCheckResult.CheckFailed : WorldAccessCheckResult.Allowed;

                WorldAccessCheckContext first = await decorator.CheckWorldAccessAsync("w6", CancellationToken.None);
                WorldAccessCheckContext second = await decorator.CheckWorldAccessAsync("w6", CancellationToken.None);

                Assert.AreEqual(WorldAccessCheckResult.CheckFailed, first.Result);
                Assert.AreEqual(WorldAccessCheckResult.Allowed, second.Result, "CheckFailed must not be cached; retry returns fresh result");
                Assert.AreEqual(2, inner.CallsPerWorld["w6"], "CheckFailed must not be cached (two real fetches)");
                Assert.AreEqual(12, inner.TotalCalls);

                fakeTime[0] += 31f;
                await decorator.CheckWorldAccessAsync("w1", CancellationToken.None);
                Assert.AreEqual(13, inner.TotalCalls, "an expired entry must be re-fetched");

                await decorator.CheckWorldAccessAsync("  W1 ", CancellationToken.None);
                await decorator.CheckWorldAccessAsync("w1", CancellationToken.None);
                Assert.AreEqual(13, inner.TotalCalls, "normalized keys must hit the same cache entry");

                Measure.Method(() =>
                        {
                            for (var pass = 0; pass < 11; pass++)
                                foreach (string world in worlds)
                                    decorator.CheckWorldAccessAsync(world, CancellationToken.None).GetAwaiter().GetResult();
                        })
                       .WarmupCount(3)
                       .MeasurementCount(10)
                       .GC()
                       .Run();
            });
    }
}
