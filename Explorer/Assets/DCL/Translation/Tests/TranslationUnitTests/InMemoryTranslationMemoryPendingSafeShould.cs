using NUnit.Framework;
using DCL.Translation.Service;
using DCL.Utilities;

namespace DCL.Translation.Tests
{
    [TestFixture]
    public class InMemoryTranslationMemoryPendingSafeShould
    {
        [Test]
        public void PreferEvictingNonPendingWhenAtCapacity()
        {
            var mem = new InMemoryTranslationMemory(capacity: 2);

            var p = new MessageTranslation("P", LanguageCode.ES);
            p.UpdateState(TranslationState.Pending);
            mem.Set("P", p);

            var o = new MessageTranslation("O", LanguageCode.ES); // Original
            mem.Set("O", o);

            // Now we insert a new one; implementation should trim non-pending first
            var x = new MessageTranslation("X", LanguageCode.ES);
            mem.Set("X", x);

            Assert.IsTrue(mem.TryGet("P", out _), "Pending should survive trims");
            // One of O/X may have been evicted depending on implementation, but capacity must hold
            Assert.LessOrEqual(mem.Count, mem.Capacity);
        }
    }
}