using DCL.Optimization.PerformanceBudgeting;
using NSubstitute;
using NUnit.Framework;
using System.Collections.Generic;
using Utility.Multithreading;
using static ECS.StreamableLoading.Cache.Tests.RefCountingCacheShould;

namespace ECS.StreamableLoading.Cache.Tests
{
    /// <summary>
    ///     Covers <see cref="RefCountStreamableCacheBase{TAssetData,TAsset,TLoadingIntention}.Unload" />. Budgeted
    ///     callers (CacheCleaner.UnloadCache) evict a small fixed chunk per frame, so a full O(N log N) sort of the
    ///     resident list on every call — while under memory pressure — is wasted work: eviction must select the
    ///     least-recently-used disposable entries with linear min-scans and must not sort (nor even touch) the whole
    ///     list. The full-purge fast-track (triggered by maxUnloadAmount >= listedCache.Count; the unbudgeted
    ///     hot-reload path passes int.MaxValue, which always satisfies it) must drain every disposable entry in one pass.
    /// </summary>
    [TestFixture]
    public class RefCountStreamableCacheUnloadShould
    {
        private const int N = 64;

        [TearDown]
        public void TearDown()
        {
            MultithreadingUtility.ResetFrameCount();
            TestData.TotalCount.Value = 0;
            TestData.ReferencedCount.Value = 0;
        }

        // Insert N disposable entries with distinct, strictly ascending LastUsedFrame values (frame == id),
        // in ascending insertion order — so a descending sort visibly reorders the list.
        private static TestCache BuildAscendingCache(out Dictionary<int, TestData> byId)
        {
            var cache = new TestCache();
            byId = new Dictionary<int, TestData>();

            for (var id = 0; id < N; id++)
            {
                MultithreadingUtility.SetFrameCount(id);

                var data = new TestData(new TestAsset());
                TestData.TotalCount.Value++;

                var intent = new TestLoadingIntent(id);
                cache.Add(intent, data);
                cache.AddReference(intent, data); // referenceCount 1, LastUsedFrame = id
                data.Dereference();               // referenceCount 0 (disposable), LastUsedFrame still = id

                byId[id] = data;
            }

            return cache;
        }

        private static IPerformanceBudget UnlimitedBudget()
        {
            IPerformanceBudget budget = Substitute.For<IPerformanceBudget>();
            budget.TrySpendBudget().Returns(true);
            return budget;
        }

        // [Performance] The early-out guard: with nothing to evict, Unload must not touch the resident list at all.
        // Red on revert: the pre-fix unconditional listedCache.Sort(...) reorders the list to descending even though
        // maxUnloadAmount == 0 evicts nothing (an O(N log N) sort per idle call).
        [Test]
        public void NotTouchTheListWhenMaxUnloadAmountIsZero()
        {
            TestCache cache = BuildAscendingCache(out _);

            var snapshot = new List<(TestLoadingIntent intention, TestData asset)>(cache.listedCache);

            cache.Unload(UnlimitedBudget(), 0);

            CollectionAssert.AreEqual(snapshot, cache.listedCache,
                "Unload(_, 0) must be a no-op; the pre-fix code sorts the whole list regardless of maxUnloadAmount.");
            Assert.That(cache.cache.Count, Is.EqualTo(N));

            cache.Dispose();
        }

        // [Performance] A single eviction (chunk == 1, the real CacheCleaner path) must not globally sort the list.
        // Parity: the least-recently-used entry (frame 0) is the victim, exactly one entry is removed.
        // Red on revert: the pre-fix Sort leaves the residual list strictly DESCENDING by LastUsedFrame; the linear
        // swap-remove leaves it non-monotonic (proves the O(N log N) sort did not run).
        [Test]
        public void EvictLeastRecentlyUsedWithoutSortingWholeList()
        {
            TestCache cache = BuildAscendingCache(out Dictionary<int, TestData> byId);

            cache.Unload(UnlimitedBudget(), 1);

            // Parity — the single least-recently-used entry (frame 0) is gone, everything else survives.
            Assert.That(cache.cache.Count, Is.EqualTo(N - 1));
            Assert.That(cache.cache.ContainsKey(new TestLoadingIntent(0)), Is.False, "LRU (frame 0) must be the victim");
            Assert.That(byId[0].DestroyCalled, Is.EqualTo(1));

            for (var id = 1; id < N; id++)
                Assert.That(byId[id].DestroyCalled, Is.EqualTo(0), $"entry {id} must survive a single eviction");

            // Perf signal — the residual list must NOT be fully sorted (which a whole-list Sort would leave behind).
            var strictlyDescending = true;

            for (var i = 1; i < cache.listedCache.Count; i++)
                if (cache.listedCache[i].asset.LastUsedFrame >= cache.listedCache[i - 1].asset.LastUsedFrame)
                {
                    strictlyDescending = false;
                    break;
                }

            Assert.That(strictlyDescending, Is.False,
                "A single-entry eviction must not sort the entire resident list (the pre-fix O(N log N) Sort does).");

            cache.Dispose();
        }

        // [Performance] Full-purge fast-track (triggered by maxUnloadAmount >= listedCache.Count; the unbudgeted
        // hot-reload path passes int.MaxValue, which always satisfies it): every disposable entry is drained in one
        // pass while every still-referenced entry survives, and cache/listedCache stay consistent. Red on a broken
        // bulk path: dropping a referenced survivor, keeping a disposable entry, or leaving listedCache out of sync
        // with the dictionary all fail here.
        // NOTE: this test and StopFullPurgeWhenBudgetIsExhausted also pass on pre-opt code — they are correctness
        // guards for the bulk path, NOT revert-falsifiers. The real revert-detectors are the two min-scan tests
        // (NotTouchTheListWhenMaxUnloadAmountIsZero and EvictLeastRecentlyUsedWithoutSortingWholeList).
        [Test]
        public void PurgeEveryDisposableEntryWhenMaxUnloadAmountIsMaxValue()
        {
            TestCache cache = BuildAscendingCache(out Dictionary<int, TestData> byId);

            // Re-reference every third entry so it is NOT disposable; it must survive the purge.
            var referenced = new HashSet<int>();

            for (var id = 0; id < N; id += 3)
            {
                cache.AddReference(new TestLoadingIntent(id), byId[id]); // referenceCount back to 1
                referenced.Add(id);
            }

            cache.Unload(UnlimitedBudget(), int.MaxValue);

            Assert.That(cache.cache.Count, Is.EqualTo(referenced.Count),
                "only the still-referenced entries may remain after a full purge");
            Assert.That(cache.listedCache.Count, Is.EqualTo(referenced.Count),
                "listedCache must be compacted in lock-step with the dictionary");

            for (var id = 0; id < N; id++)
            {
                bool survives = referenced.Contains(id);
                Assert.That(cache.cache.ContainsKey(new TestLoadingIntent(id)), Is.EqualTo(survives), $"entry {id}");
                Assert.That(byId[id].DestroyCalled, Is.EqualTo(survives ? 0 : 1), $"entry {id} destroy count");
            }

            // Every surviving list entry must still be present in the dictionary (no dangling swap-remove residue).
            foreach ((TestLoadingIntent intention, TestData _) in cache.listedCache)
                Assert.That(cache.cache.ContainsKey(intention), Is.True, "listedCache entry missing from dictionary");

            cache.Dispose();
        }

        // [Performance] The full-purge fast-track still honours the frame-time budget: once TrySpendBudget starts
        // returning false it stops evicting and keeps every remaining entry, leaving the two collections consistent.
        // Red on a broken bulk path that ignores the budget: it would drain all N and free far more than K.
        [Test]
        public void StopFullPurgeWhenBudgetIsExhausted()
        {
            TestCache cache = BuildAscendingCache(out _);

            const int K = 10;

            IPerformanceBudget budget = Substitute.For<IPerformanceBudget>();
            var calls = 0;
            budget.TrySpendBudget().Returns(_ => calls++ < K);

            cache.Unload(budget, int.MaxValue);

            Assert.That(cache.cache.Count, Is.EqualTo(N - K), "exactly K evictions fit the budget");
            Assert.That(cache.listedCache.Count, Is.EqualTo(N - K),
                "listedCache must retain the un-evicted entries when the budget runs out mid-purge");

            foreach ((TestLoadingIntent intention, TestData _) in cache.listedCache)
                Assert.That(cache.cache.ContainsKey(intention), Is.True, "listedCache entry missing from dictionary");

            cache.Dispose();
        }
    }
}
