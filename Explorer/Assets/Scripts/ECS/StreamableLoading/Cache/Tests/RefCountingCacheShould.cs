using DCL.Diagnostics;
using DCL.Optimization.PerformanceBudgeting;
using ECS.StreamableLoading.Common.Components;
using NSubstitute;
using NUnit.Framework;
using System.Collections.Generic;
using System.Threading;
using Unity.Profiling;

namespace ECS.StreamableLoading.Cache.Tests
{
    public class RefCountingCacheShould
    {
        private TestCache cache;

        [SetUp]
        public void Setup()
        {
            cache = new TestCache();
        }

        [TearDown]
        public void TearDown()
        {
            cache.Dispose();

            // Reset counter for subsequent tests
            cache.InCacheCount.Value = 0;
            TestData.TotalCount.Value = 0;
            TestData.ReferencedCount.Value = 0;
        }

        [Test]
        public void AddToCache()
        {
            var asset = new TestAsset();
            var intent = new TestLoadingIntent(1);
            var data = new TestData(asset);

            cache.Add(intent, data);

            Assert.IsTrue(cache.cache.TryGetValue(intent, out TestData? result));
            Assert.AreEqual(data, result);
            Assert.That(cache.InCacheCount.Value, Is.EqualTo(1));

            // check listed cache
            CollectionAssert.AreEqual(new[] { (intent, data) }, cache.listedCache);
        }

        [Test]
        public void DestroyAllOnDispose()
        {
            const int ASSETS_COUNT = 10;

            var dataArray = new TestData[ASSETS_COUNT];

            for (var i = 0; i < ASSETS_COUNT; i++)
            {
                var asset = new TestAsset();
                var intent = new TestLoadingIntent(i);
                var data = new TestData(asset);

                dataArray[i] = data;

                cache.Add(intent, data);
            }

            cache.Dispose();

            foreach (TestData testData in dataArray)
                Assert.That(testData.DestroyCalled, Is.EqualTo(1));

            // Check cache
            Assert.That(cache.InCacheCount.Value, Is.EqualTo(0));
            Assert.That(cache.cache.Count, Is.EqualTo(0));
            Assert.That(cache.listedCache.Count, Is.EqualTo(0));
        }

        [Test]
        public void AddReference([Values(1, 100, 10000)] int iterationsCount)
        {
            var asset = new TestAsset();
            var intent = new TestLoadingIntent(1);
            var data = new TestData(asset);

            //total count is increased externally
            TestData.TotalCount.Value++;

            cache.Add(intent, data);

            for (var i = 0; i < iterationsCount; i++) { cache.AddReference(intent, data); }

            Assert.That(data.referenceCount, Is.EqualTo(iterationsCount));
            Assert.That(TestData.ReferencedCount.Value, Is.EqualTo(1));
        }

        [Test]
        public void UnloadNotReferenced()
        {
            var asset1 = new TestAsset();
            var intent1 = new TestLoadingIntent(1);
            var data1 = new TestData(asset1);

            //total count is increased externally
            TestData.TotalCount.Value++;

            cache.Add(intent1, data1);
            cache.AddReference(intent1, data1);

            var asset2 = new TestAsset();
            var intent2 = new TestLoadingIntent(2);
            var data2 = new TestData(asset2);

            cache.Add(intent2, data2);
            cache.AddReference(intent2, data2);
            data2.Dereference();

            //total count is increased externally
            TestData.TotalCount.Value++;

            Assert.That(data2.referenceCount, Is.EqualTo(0));

            IPerformanceBudget? budget = Substitute.For<IPerformanceBudget>();
            budget.TrySpendBudget().Returns(true);

            cache.Unload(budget, 1000);

            // Destroy called
            Assert.That(data2.DestroyCalled, Is.EqualTo(1));
            Assert.That(cache.InCacheCount.Value, Is.EqualTo(1));
            Assert.That(TestData.TotalCount.Value, Is.EqualTo(1));

            // Not destroyed
            Assert.That(data1.DestroyCalled, Is.EqualTo(0));

            CollectionAssert.AreEqual(new[] { new KeyValuePair<TestLoadingIntent, TestData>(intent1, data1) }, cache.cache);

            // Check listed cache
            CollectionAssert.AreEqual(new[] { (intent1, data1) }, cache.listedCache);
        }

        public class TestLoadingIntent : ILoadingIntention
        {
            public readonly int Value;

            public CancellationTokenSource CancellationTokenSource { get; } = new ();

            public CommonLoadingArguments CommonArguments { get; set; }

            public TestLoadingIntent(int value)
            {
                Value = value;
            }
        }

        public class TestAsset
        {
            public int Value;
        }

        public class TestData : StreamableRefCountData<TestAsset>
        {
            public static ProfilerCounterValue<int> TotalCount = new (ProfilerCategory.Memory, "TEST TOTAL COUNT", ProfilerMarkerDataUnit.Count);
            public static ProfilerCounterValue<int> ReferencedCount = new (ProfilerCategory.Memory, "TEST REFERENCED COUNT", ProfilerMarkerDataUnit.Count);

            public int DestroyCalled;

            protected override ref ProfilerCounterValue<int> totalCount => ref TotalCount;

            protected override ref ProfilerCounterValue<int> referencedCount => ref ReferencedCount;

            public TestData(TestAsset asset, string reportCategory = ReportCategory.STREAMABLE_LOADING) : base(asset, reportCategory) { }

            protected override void DestroyObject()
            {
                DestroyCalled++;
            }
        }

        public class TestCache : RefCountStreamableCacheBase<TestData, TestAsset, TestLoadingIntent>
        {
            public ProfilerCounterValue<int> InCacheCount = new (ProfilerCategory.Memory, "TEST IN CACHE COUNT", ProfilerMarkerDataUnit.Count);

            protected override ref ProfilerCounterValue<int> inCacheCount => ref InCacheCount;

            public override bool Equals(TestLoadingIntent x, TestLoadingIntent y) =>
                x.Value == y.Value;

            public override int GetHashCode(TestLoadingIntent obj) =>
                obj.Value.GetHashCode();
        }
    }
}
