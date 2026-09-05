using System.Collections.Generic;
using NUnit.Framework;
using DCL.Translation.Service;

namespace DCL.Translation.Tests
{
    [TestFixture]
    public class LRUCacheAdvancedUsageShould
    {
        [Test]
        public void MaintainEvictionOrderFromTrueLRU()
        {
            var evicted = new List<string>();
            var c = new LRUCache<string, int>(3, (k, _) => evicted.Add(k));

            // Insert A,B,C  -> LRU: A
            c.Set("A", 1);
            c.Set("B", 2);
            c.Set("C", 3);

            // Touch A -> order: A (MRU), C, B (LRU)
            Assert.IsTrue(c.TryGetValue("A", out _));

            // Insert D -> evict B
            c.Set("D", 4);

            // Insert E -> evict C (A was MRU, then D)
            c.Set("E", 5);

            CollectionAssert.AreEqual(new[]
            {
                "B", "C"
            }, evicted);
            Assert.IsTrue(c.TryGetValue("A", out _));
            Assert.IsTrue(c.TryGetValue("D", out _));
            Assert.IsTrue(c.TryGetValue("E", out _));
        }

        [Test]
        public void NotInvokeOnEvictedOnClearOrTryRemove()
        {
            int evictCount = 0;
            var c = new LRUCache<string, int>(2, (_, __) => evictCount++);

            c.Set("A", 1);
            c.Set("B", 2);
            // Explicit removal should NOT call onEvicted
            Assert.IsTrue(c.TryRemove("A", out _));
            Assert.AreEqual(0, evictCount);

            // Clear should NOT call onEvicted either
            c.Clear();
            Assert.AreEqual(0, evictCount);
        }

        [Test]
        public void UpdateMakesKeyMostRecent()
        {
            var c = new LRUCache<string, int>(2);
            c.Set("A", 1);
            c.Set("B", 2);

            // Update A -> A becomes MRU, LRU is B
            c.Set("A", 3);

            // Add C -> evicts B
            c.Set("C", 4);

            Assert.IsTrue(c.TryGetValue("A", out int a));
            Assert.AreEqual(3, a);
            Assert.IsFalse(c.TryGetValue("B", out _));
            Assert.IsTrue(c.TryGetValue("C", out _));
        }

        [Test]
        public void RemoveOldestCanExceedTailSafely()
        {
            var c = new LRUCache<string, int>(3);
            c.Set("A", 1);
            c.Set("B", 2);

            int removed = c.RemoveOldest(10); // ask more than present
            Assert.AreEqual(2, removed);
            Assert.AreEqual(0, c.Count);
        }

        [Test]
        public void RemoveWhereWithKeyDrivenPredicate()
        {
            var c = new LRUCache<string, int>(5);
            c.Set("chan:1:msg:1", 1);
            c.Set("chan:1:msg:2", 2);
            c.Set("chan:2:msg:9", 9);

            // Remove all entries for channel 1 by key prefix
            int removed = c.RemoveWhere((k, _) => k.StartsWith("chan:1:"));
            Assert.AreEqual(2, removed);

            Assert.IsFalse(c.TryGetValue("chan:1:msg:1", out _));
            Assert.IsFalse(c.TryGetValue("chan:1:msg:2", out _));
            Assert.IsTrue(c.TryGetValue("chan:2:msg:9", out _));
        }

        [Test]
        public void PeekLeavesRecencyUntouched()
        {
            var c = new LRUCache<string, int>(2);
            c.Set("A", 1);
            c.Set("B", 2);

            Assert.IsTrue(c.TryPeek("A", out _)); // no bump
            // Add C -> LRU is still A
            c.Set("C", 3);

            Assert.IsFalse(c.TryGetValue("A", out _));
            Assert.IsTrue(c.TryGetValue("B", out _));
            Assert.IsTrue(c.TryGetValue("C", out _));
        }

        [Test]
        public void StressCapacityNeverExceededAndNewestRemain()
        {
            var c = new LRUCache<int, int>(capacity: 100, onEvicted: null);
            for (int i = 0; i < 1000; i++)
                c.Set(i, i * 10);

            Assert.AreEqual(100, c.Count);
            // The most recent 100 keys should remain: 900..999
            for (int i = 0; i < 900; i++)
                Assert.IsFalse(c.TryGetValue(i, out _));
            for (int i = 900; i < 1000; i++)
            {
                Assert.IsTrue(c.TryGetValue(i, out int v));
                Assert.AreEqual(i * 10, v);
            }
        }
    }
}