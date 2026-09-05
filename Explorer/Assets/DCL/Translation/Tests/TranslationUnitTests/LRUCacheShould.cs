using NUnit.Framework;
using DCL.Translation.Service;

namespace DCL.Translation.Tests
{
    [TestFixture]
    public class LRUCacheShould
    {
        private LRUCache<string, int> cache;

        [SetUp]
        public void SetUp()
        {
            cache = new LRUCache<string, int>(capacity: 3);
        }

        [Test]
        public void InsertAndRetrieveValues()
        {
            cache.Set("A", 1);
            cache.Set("B", 2);

            Assert.IsTrue(cache.TryGetValue("A", out int a));
            Assert.AreEqual(1, a);

            Assert.IsTrue(cache.TryGetValue("B", out int b));
            Assert.AreEqual(2, b);

            Assert.AreEqual(2, cache.Count);
        }

        [Test]
        public void UpdateExistingWithoutGrowingCount()
        {
            cache.Set("X", 10);
            cache.Set("X", 20);

            Assert.AreEqual(1, cache.Count);
            Assert.IsTrue(cache.TryGetValue("X", out int x));
            Assert.AreEqual(20, x);
        }

        [Test]
        public void EvictExactlyOneOnOverflow()
        {
            // Fill: A,B,C
            cache.Set("A", 1);
            cache.Set("B", 2);
            cache.Set("C", 3);

            // Touch A to make A MRU; tail becomes B
            Assert.IsTrue(cache.TryGetValue("A", out _));

            // Insert D -> should evict B
            cache.Set("D", 4);

            Assert.IsFalse(cache.TryGetValue("B", out _), "B should be evicted");
            Assert.IsTrue(cache.TryGetValue("A", out _), "A should still be present");
            Assert.IsTrue(cache.TryGetValue("C", out _), "C should still be present");
            Assert.IsTrue(cache.TryGetValue("D", out _), "D was just inserted");

            Assert.AreEqual(3, cache.Count);
        }

        [Test]
        public void PeekDoesNotBumpRecency()
        {
            cache.Set("A", 1);
            cache.Set("B", 2);
            cache.Set("C", 3);

            // Peek A (should NOT change LRU ordering)
            Assert.IsTrue(cache.TryPeek("A", out _));

            // Insert D. Since the tail is still A (never 'Get'), A should be evicted
            cache.Set("D", 4);

            Assert.IsFalse(cache.TryGetValue("A", out _));
            Assert.IsTrue(cache.TryGetValue("B", out _));
            Assert.IsTrue(cache.TryGetValue("C", out _));
            Assert.IsTrue(cache.TryGetValue("D", out _));
        }

        [Test]
        public void RemoveSpecificKey()
        {
            cache.Set("A", 1);
            cache.Set("B", 2);

            Assert.IsTrue(cache.TryRemove("A", out int removed));
            Assert.AreEqual(1, removed);
            Assert.AreEqual(1, cache.Count);

            Assert.IsFalse(cache.TryGetValue("A", out _));
            Assert.IsTrue(cache.TryGetValue("B", out _));
        }

        [Test]
        public void RemoveOldestRemovesFromTail()
        {
            cache.Set("A", 1);
            cache.Set("B", 2);
            cache.Set("C", 3);

            // Touch B so order becomes B,C,A (A is now oldest)
            Assert.IsTrue(cache.TryGetValue("B", out _));

            int removed = cache.RemoveOldest(1);
            Assert.AreEqual(1, removed);

            // A should be gone
            Assert.IsFalse(cache.TryGetValue("A", out _));
            Assert.IsTrue(cache.TryGetValue("B", out _));
            Assert.IsTrue(cache.TryGetValue("C", out _));
        }

        [Test]
        public void RemoveWhereByPredicate()
        {
            cache.Set("A", 1);
            cache.Set("B", 2);
            cache.Set("C", 3);

            // Remove entries whose value is even
            int removed = cache.RemoveWhere((k, v) => v % 2 == 0);
            Assert.AreEqual(1, removed);

            Assert.IsTrue(cache.TryGetValue("A", out _));
            Assert.IsFalse(cache.TryGetValue("B", out _)); // removed
            Assert.IsTrue(cache.TryGetValue("C", out _));
        }

        [Test]
        public void ClearRemovesEverything()
        {
            cache.Set("A", 1);
            cache.Set("B", 2);

            cache.Clear();
            Assert.AreEqual(0, cache.Count);
            Assert.IsFalse(cache.TryGetValue("A", out _));
            Assert.IsFalse(cache.TryGetValue("B", out _));
        }

        [Test]
        public void FireOnEvictedForEachEviction()
        {
            int evicted = 0;
            var c = new LRUCache<string, int>(capacity: 2, onEvicted: (_, __) => evicted++);

            c.Set("A", 1);
            c.Set("B", 2);
            c.Set("C", 3); // evicts A
            c.Set("D", 4); // evicts B

            Assert.AreEqual(2, evicted);
        }
    }
}