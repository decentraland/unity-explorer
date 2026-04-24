using DCL.Chat.ChatReactions.Simulation;
using DCL.Chat.ChatReactions.Simulation.UI;
using DCL.Chat.ChatReactions.Simulation.World;
using NUnit.Framework;

namespace DCL.Chat.ChatReactions.Tests
{
    [TestFixture]
    public class DenseParticleStoreShould
    {
        [Test]
        public void FillToCapacityWithTryAdd()
        {
            // Arrange
            var store = new DenseParticleStore<ChatReactionsParticle>(64);

            // Act
            for (int i = 0; i < 64; i++)
                Assert.That(store.TryAdd(new ChatReactionsParticle { alive = 1, emojiIndex = i }), Is.True);

            // Assert
            Assert.That(store.Count, Is.EqualTo(64));
        }

        [Test]
        public void TryAddSucceedsWhenNotFull()
        {
            // Arrange
            var store = new DenseParticleStore<ChatReactionsParticle>(64);

            // Act
            bool added = store.TryAdd(new ChatReactionsParticle { alive = 1, emojiIndex = 0 });

            // Assert
            Assert.That(added, Is.True);
            Assert.That(store.Count, Is.EqualTo(1));
        }

        [Test]
        public void TryAddFailsWhenFull()
        {
            // Arrange
            var store = new DenseParticleStore<ChatReactionsParticle>(64);

            for (int i = 0; i < 64; i++)
                store.TryAdd(new ChatReactionsParticle { alive = 1, emojiIndex = i });

            // Act
            bool added = store.TryAdd(new ChatReactionsParticle { alive = 1, emojiIndex = 999 });

            // Assert
            Assert.That(added, Is.False);
            Assert.That(store.Count, Is.EqualTo(64));
            Assert.That(store.Buffer[0].emojiIndex, Is.EqualTo(0)); // not overwritten
        }

        [Test]
        public void ForceAddOverwritesWhenFull()
        {
            // Arrange
            var store = new DenseParticleStore<ChatReactionsParticle>(64);

            for (int i = 0; i < 64; i++)
                store.ForceAdd(new ChatReactionsParticle { alive = 1, emojiIndex = i });

            // Act
            store.ForceAdd(new ChatReactionsParticle { alive = 1, emojiIndex = 900 });

            // Assert
            Assert.That(store.Count, Is.EqualTo(64));
            Assert.That(store.Buffer[0].emojiIndex, Is.EqualTo(900));
        }

        // Verifies round-robin advances the overwrite cursor so consecutive overflows hit different slots.
        [Test]
        public void OverwriteRoundRobinWhenFull()
        {
            // Arrange
            var store = new DenseParticleStore<ChatReactionsParticle>(64);

            for (int i = 0; i < 64; i++)
                store.ForceAdd(new ChatReactionsParticle { alive = 1, emojiIndex = i });

            Assert.That(store.Count, Is.EqualTo(64));

            // Act & Assert — sequential overflows should advance the cursor
            // First overflow → index 0
            store.ForceAdd(new ChatReactionsParticle { alive = 1, emojiIndex = 900 });
            Assert.That(store.Count, Is.EqualTo(64));
            Assert.That(store.Buffer[0].emojiIndex, Is.EqualTo(900));

            // Second overflow → index 1 (round-robin), NOT index 0 again
            store.ForceAdd(new ChatReactionsParticle { alive = 1, emojiIndex = 901 });
            Assert.That(store.Buffer[1].emojiIndex, Is.EqualTo(901));
            Assert.That(store.Buffer[0].emojiIndex, Is.EqualTo(900));
        }

        [Test]
        public void DistributeOverflowAcrossSlots()
        {
            // Arrange
            var store = new DenseParticleStore<ChatReactionsParticle>(64);

            for (int i = 0; i < 64; i++)
                store.ForceAdd(new ChatReactionsParticle { alive = 1, emojiIndex = i });

            // Act — Overflow 3 times — should hit indices 0, 1, 2 in order
            store.ForceAdd(new ChatReactionsParticle { alive = 1, emojiIndex = 100 });
            store.ForceAdd(new ChatReactionsParticle { alive = 1, emojiIndex = 101 });
            store.ForceAdd(new ChatReactionsParticle { alive = 1, emojiIndex = 102 });

            // Assert
            Assert.That(store.Buffer[0].emojiIndex, Is.EqualTo(100));
            Assert.That(store.Buffer[1].emojiIndex, Is.EqualTo(101));
            Assert.That(store.Buffer[2].emojiIndex, Is.EqualTo(102));

            // Remaining slots untouched
            Assert.That(store.Buffer[3].emojiIndex, Is.EqualTo(3));
            Assert.That(store.Buffer[63].emojiIndex, Is.EqualTo(63));
        }

        [Test]
        public void CompactDeadParticles()
        {
            // Arrange
            var store = new DenseParticleStore<ChatReactionsParticle>(64);
            store.TryAdd(new ChatReactionsParticle { alive = 1, emojiIndex = 0 });
            store.TryAdd(new ChatReactionsParticle { alive = 0, emojiIndex = 1 }); // dead
            store.TryAdd(new ChatReactionsParticle { alive = 1, emojiIndex = 2 });

            // Act
            store.CompactDead();

            // Assert
            Assert.That(store.Count, Is.EqualTo(2));
            Assert.That(store.Buffer[0].emojiIndex, Is.EqualTo(0));
            Assert.That(store.Buffer[1].emojiIndex, Is.EqualTo(2));
        }

        // Verifies that CompactDead resets the overwrite cursor so subsequent overflows start from index 0.
        [Test]
        public void CompactDeadResetsOverwriteCursor()
        {
            // Arrange
            var store = new DenseParticleStore<ChatReactionsParticle>(64);

            // Fill to capacity
            for (int i = 0; i < 64; i++)
                store.ForceAdd(new ChatReactionsParticle { alive = 1, emojiIndex = i });

            // Overflow 3 times — cursor advances to 3
            store.ForceAdd(new ChatReactionsParticle { alive = 1, emojiIndex = 100 });
            store.ForceAdd(new ChatReactionsParticle { alive = 1, emojiIndex = 101 });
            store.ForceAdd(new ChatReactionsParticle { alive = 1, emojiIndex = 102 });

            // Kill 10 particles and compact
            for (int i = 0; i < 10; i++)
                store.Buffer[i] = new ChatReactionsParticle { alive = 0, emojiIndex = store.Buffer[i].emojiIndex };

            store.CompactDead();
            Assert.That(store.Count, Is.EqualTo(54));

            // Refill to capacity
            for (int i = 0; i < 10; i++)
                store.TryAdd(new ChatReactionsParticle { alive = 1, emojiIndex = 500 + i });

            Assert.That(store.Count, Is.EqualTo(64));

            // Act — Next overflow should hit index 0 (cursor was reset), not index 3
            store.ForceAdd(new ChatReactionsParticle { alive = 1, emojiIndex = 999 });

            // Assert
            Assert.That(store.Buffer[0].emojiIndex, Is.EqualTo(999));
        }

        [Test]
        public void CompactDeadOnEmptyStoreIsNoOp()
        {
            // Arrange
            var store = new DenseParticleStore<ChatReactionsParticle>(64);

            // Act
            store.CompactDead();

            // Assert
            Assert.That(store.Count, Is.EqualTo(0));
            Assert.That(store.Capacity, Is.EqualTo(64));
        }

        [Test]
        public void AddAfterCompactFillsGapCorrectly()
        {
            // Arrange
            var store = new DenseParticleStore<ChatReactionsParticle>(64);
            store.TryAdd(new ChatReactionsParticle { alive = 1, emojiIndex = 0 });
            store.TryAdd(new ChatReactionsParticle { alive = 0, emojiIndex = 1 }); // dead
            store.TryAdd(new ChatReactionsParticle { alive = 1, emojiIndex = 2 });
            store.TryAdd(new ChatReactionsParticle { alive = 0, emojiIndex = 3 }); // dead
            store.TryAdd(new ChatReactionsParticle { alive = 1, emojiIndex = 4 });

            store.CompactDead();
            Assert.That(store.Count, Is.EqualTo(3));

            // Act — Add new particles — they should fill after the compacted survivors
            store.TryAdd(new ChatReactionsParticle { alive = 1, emojiIndex = 10 });
            store.TryAdd(new ChatReactionsParticle { alive = 1, emojiIndex = 11 });

            // Assert
            Assert.That(store.Count, Is.EqualTo(5));
            Assert.That(store.Buffer[0].emojiIndex, Is.EqualTo(0));
            Assert.That(store.Buffer[1].emojiIndex, Is.EqualTo(2));
            Assert.That(store.Buffer[2].emojiIndex, Is.EqualTo(4));
            Assert.That(store.Buffer[3].emojiIndex, Is.EqualTo(10));
            Assert.That(store.Buffer[4].emojiIndex, Is.EqualTo(11));
        }

        // Capacity below the minimum threshold is clamped up to the minimum (64).
        [Test]
        public void CapacityReturnsClampedValue()
        {
            var store = new DenseParticleStore<ChatReactionsParticle>(4);
            Assert.That(store.Capacity, Is.EqualTo(64));
        }

        // Verifies the store is generic and works with a different particle struct type.
        [Test]
        public void WorkWithUiParticles()
        {
            // Arrange
            var store = new DenseParticleStore<ChatReactionsUiParticle>(64);
            store.TryAdd(new ChatReactionsUiParticle { alive = 1, emojiIndex = 5 });
            store.TryAdd(new ChatReactionsUiParticle { alive = 0 }); // dead

            // Act
            store.CompactDead();

            // Assert
            Assert.That(store.Count, Is.EqualTo(1));
            Assert.That(store.Buffer[0].emojiIndex, Is.EqualTo(5));
        }
    }
}
