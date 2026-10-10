using NUnit.Framework;
using DCL.Translation.Service;
using DCL.Utilities;

namespace DCL.Translation.Tests
{
    [TestFixture]
    public class InMemoryTranslationMemoryShould
    {
        private InMemoryTranslationMemory mem;

        [SetUp]
        public void SetUp()
        {
            // tiny capacity to force eviction scenarios
            mem = new InMemoryTranslationMemory(capacity: 3);
        }

        [Test]
        public void StoreAndFetchState()
        {
            var mt = new MessageTranslation("m1", LanguageCode.ES);
            mem.Set("m1", mt);

            Assert.IsTrue(mem.TryGet("m1", out var fetched));
            Assert.AreSame(mt, fetched);
            Assert.AreEqual(1, mem.Count);
            Assert.AreEqual(3, mem.Capacity);
        }

        [Test]
        public void BumpRecencyOnGetAndEvictLRU()
        {
            // A,B,C fill
            mem.Set("A", new MessageTranslation("A", LanguageCode.ES));
            mem.Set("B", new MessageTranslation("B", LanguageCode.ES));
            mem.Set("C", new MessageTranslation("C", LanguageCode.ES));

            // Touch A so A becomes MRU, LRU becomes B
            Assert.IsTrue(mem.TryGet("A", out _));

            // Insert D -> should evict B
            mem.Set("D", new MessageTranslation("D", LanguageCode.ES));

            Assert.IsFalse(mem.TryGet("B", out _));
            Assert.IsTrue(mem.TryGet("A", out _));
            Assert.IsTrue(mem.TryGet("C", out _));
            Assert.IsTrue(mem.TryGet("D", out _));
            Assert.AreEqual(3, mem.Count);
        }

        [Test]
        public void RemoveSpecificMessageState()
        {
            mem.Set("A", new MessageTranslation("A", LanguageCode.ES));
            mem.Set("B", new MessageTranslation("B", LanguageCode.ES));

            Assert.IsTrue(mem.Remove("A"));
            Assert.AreEqual(1, mem.Count);
            Assert.IsFalse(mem.TryGet("A", out _));
            Assert.IsTrue(mem.TryGet("B", out _));
        }

        [Test]
        public void RemoveOldestSafeSkipsPending()
        {
            var a = new MessageTranslation("A", LanguageCode.ES);
            a.UpdateState(TranslationState.Pending);
            var b = new MessageTranslation("B", LanguageCode.ES); // Original
            var c = new MessageTranslation("C", LanguageCode.ES); // Original

            mem.Set("A", a);
            mem.Set("B", b);
            mem.Set("C", c);

            // Request removal of 2 oldest, but skip Pending
            int removed = mem.RemoveOldestSafe(count: 2);

            // Only B and/or C should be removed, A (Pending) stays
            Assert.AreEqual(2, removed);
            Assert.IsTrue(mem.TryGet("A", out _));
            // exactly one of B/C may remain depending on LRU order, but count is 1 now
            Assert.AreEqual(1, mem.Count);
        }

        [Test]
        public void SetTranslatedResultUpdatesExisting()
        {
            var mt = new MessageTranslation("m1", LanguageCode.ES);
            mt.UpdateState(TranslationState.Pending);
            mem.Set("m1", mt);

            mem.SetTranslatedResult("m1", new TranslationResult
                ("hola", LanguageCode.ES, false));

            Assert.IsTrue(mem.TryGet("m1", out var fetched));
            Assert.AreEqual(TranslationState.Success, fetched.State);
            Assert.AreEqual("hola", fetched.TranslatedBody);
            Assert.AreEqual(LanguageCode.ES, fetched.DetectedSourceLanguage);
        }

        [Test]
        public void ClearRemovesEverything()
        {
            mem.Set("A", new MessageTranslation("A", LanguageCode.ES));
            mem.Set("B", new MessageTranslation("B", LanguageCode.ES));

            mem.Clear();
            Assert.AreEqual(0, mem.Count);
            Assert.IsFalse(mem.TryGet("A", out _));
            Assert.IsFalse(mem.TryGet("B", out _));
        }
    }
}