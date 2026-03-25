using DCL.Chat.ChatReactions;
using NUnit.Framework;

namespace DCL.Chat.ChatReactions.Tests
{
    [TestFixture]
    public class DenseParticleStoreShould
    {
        [Test]
        public void AddParticlesUpToCapacity()
        {
            var store = new DenseParticleStore<ChatReactionsParticle>(4);
            store.Add(new ChatReactionsParticle { alive = 1, emojiIndex = 0 });
            store.Add(new ChatReactionsParticle { alive = 1, emojiIndex = 1 });
            Assert.That(store.Count, Is.EqualTo(2));
        }

        [Test]
        public void OverwriteIndexZeroWhenFull()
        {
            var store = new DenseParticleStore<ChatReactionsParticle>(64);

            for (int i = 0; i < 64; i++)
                store.Add(new ChatReactionsParticle { alive = 1, emojiIndex = i });

            Assert.That(store.Count, Is.EqualTo(64));

            store.Add(new ChatReactionsParticle { alive = 1, emojiIndex = 999 });
            Assert.That(store.Count, Is.EqualTo(64));
            Assert.That(store.Buffer[0].emojiIndex, Is.EqualTo(999));
        }

        [Test]
        public void CompactDeadParticles()
        {
            var store = new DenseParticleStore<ChatReactionsParticle>(64);
            store.Add(new ChatReactionsParticle { alive = 1, emojiIndex = 0 });
            store.Add(new ChatReactionsParticle { alive = 0, emojiIndex = 1 }); // dead
            store.Add(new ChatReactionsParticle { alive = 1, emojiIndex = 2 });

            store.CompactDead();

            Assert.That(store.Count, Is.EqualTo(2));
            Assert.That(store.Buffer[0].emojiIndex, Is.EqualTo(0));
            Assert.That(store.Buffer[1].emojiIndex, Is.EqualTo(2));
        }

        [Test]
        public void WorkWithUiParticles()
        {
            var store = new DenseParticleStore<ChatReactionsUiParticle>(64);
            store.Add(new ChatReactionsUiParticle { alive = 1, emojiIndex = 5 });
            store.Add(new ChatReactionsUiParticle { alive = 0 }); // dead

            store.CompactDead();

            Assert.That(store.Count, Is.EqualTo(1));
            Assert.That(store.Buffer[0].emojiIndex, Is.EqualTo(5));
        }
    }
}
