using NUnit.Framework;
using DCL.Translation.Service;
using DCL.Utilities;

namespace DCL.Translation.Tests
{
    [TestFixture]
    public class InMemoryTranslationCacheAdvancedShould
    {
        [Test]
        public void KeepSecondaryIndexInSyncOnEviction()
        {
            // capacity 2 to force eviction quickly
            var cache = new InMemoryTranslationCache(capacity: 2);

            // Fill with two langs for m1
            cache.Set("m1", LanguageCode.ES, default);
            cache.Set("m1", LanguageCode.DE, default);

            // Touch ES so DE becomes LRU
            Assert.IsTrue(cache.TryGet("m1", LanguageCode.ES, out _));

            // Insert new key -> evicts (m1,DE)
            cache.Set("m2", LanguageCode.ES, default);

            // Now RemoveAllForMessage("m1") should remove ONLY the remaining (m1,ES)
            int removed = cache.RemoveAllForMessage("m1");
            Assert.AreEqual(1, removed);

            Assert.IsFalse(cache.TryGet("m1", LanguageCode.ES, out _));
            Assert.IsTrue(cache.TryGet("m2", LanguageCode.ES, out _));
        }

        [Test]
        public void ClearResetsIndexCompletely()
        {
            var cache = new InMemoryTranslationCache(capacity: 4);
            cache.Set("m1", LanguageCode.ES, default);
            cache.Set("m1", LanguageCode.DE, default);
            cache.Set("m2", LanguageCode.ES, default);

            cache.Clear();

            Assert.AreEqual(0, cache.Count);
            // Should behave as empty and not crash on RemoveAll
            Assert.AreEqual(0, cache.RemoveAllForMessage("m1"));
        }
    }
}