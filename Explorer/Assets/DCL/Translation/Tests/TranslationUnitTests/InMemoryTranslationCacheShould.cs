using NUnit.Framework;
using DCL.Translation.Service;
using DCL.Utilities;

namespace DCL.Translation.Tests
{
    [TestFixture]
    public class InMemoryTranslationCacheShould
    {
        private InMemoryTranslationCache cache;

        [SetUp]
        public void SetUp()
        {
            cache = new InMemoryTranslationCache(capacity: 3);
        }

        [Test]
        public void StoreAndFetchPerMessageAndLanguage()
        {
            string id = "m1";

            Assert.IsFalse(cache.TryGet(id, LanguageCode.ES, out _));

            cache.Set(id, LanguageCode.ES, default); // value not used in this test
            cache.Set(id, LanguageCode.DE, default);

            Assert.IsTrue(cache.TryGet(id, LanguageCode.ES, out _));
            Assert.IsTrue(cache.TryGet(id, LanguageCode.DE, out _));
            Assert.AreEqual(2, cache.Count);
        }

        [Test]
        public void RemoveAllLanguagesForMessage()
        {
            cache.Set("m1", LanguageCode.ES, default);
            cache.Set("m1", LanguageCode.DE, default);
            cache.Set("m2", LanguageCode.ES, default);

            int removed = cache.RemoveAllForMessage("m1");
            Assert.AreEqual(2, removed);

            Assert.IsFalse(cache.TryGet("m1", LanguageCode.ES, out _));
            Assert.IsFalse(cache.TryGet("m1", LanguageCode.DE, out _));
            Assert.IsTrue(cache.TryGet("m2", LanguageCode.ES, out _));
        }

        [Test]
        public void EvictLeastRecentlyUsedAcrossLanguages()
        {
            // capacity 3
            cache.Set("m1", LanguageCode.ES, default); // (m1,ES)
            cache.Set("m1", LanguageCode.DE, default); // (m1,DE)
            cache.Set("m2", LanguageCode.ES, default); // (m2,ES)

            // Touch (m1,ES) to make it MRU; LRU tail becomes (m1,DE)
            Assert.IsTrue(cache.TryGet("m1", LanguageCode.ES, out _));

            // Insert a new key, should evict (m1,DE)
            cache.Set("m3", LanguageCode.ES, default);

            Assert.IsFalse(cache.TryGet("m1", LanguageCode.DE, out _));
            Assert.IsTrue(cache.TryGet("m1", LanguageCode.ES, out _));
            Assert.IsTrue(cache.TryGet("m2", LanguageCode.ES, out _));
            Assert.IsTrue(cache.TryGet("m3", LanguageCode.ES, out _));
        }
    }
}