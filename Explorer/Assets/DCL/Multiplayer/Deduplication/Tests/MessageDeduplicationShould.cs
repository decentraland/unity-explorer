using NUnit.Framework;
using System;

namespace DCL.Multiplayer.Deduplication.Tests
{
    [TestFixture]
    public class MessageDeduplicationShould
    {
        private const string WALLET = "0x0000000000000000000000000000000000000001";
        private const string OTHER_WALLET = "0x0000000000000000000000000000000000000002";

        [Test]
        public void RetainEveryStampWhenUnbounded()
        {
            // Arrange
            var deduplication = new MessageDeduplication<int>();

            // Act
            for (int i = 0; i < 5_000; i++)
                deduplication.Register(WALLET, i);

            // Assert
            Assert.That(deduplication.Contains(WALLET, 0), Is.True, "The first stamp must survive when no capacity is set");
            Assert.That(deduplication.Contains(WALLET, 4_999), Is.True);
        }

        [Test]
        public void TreatTheUnboundedConstantAsNoLimit()
        {
            // Arrange
            var deduplication = new MessageDeduplication<int>(MessageDeduplication<int>.UNBOUNDED_CAPACITY);

            // Act
            for (int i = 0; i < 1_000; i++)
                deduplication.Register(WALLET, i);

            // Assert
            Assert.That(deduplication.Contains(WALLET, 0), Is.True);
        }

        [Test]
        public void DropTheWindowWhenCapacityIsReached()
        {
            // Arrange
            var deduplication = new MessageDeduplication<int>(capacity: 4);

            // Act — the fifth registration finds the set full, so the window restarts
            for (int i = 0; i < 5; i++)
                deduplication.Register(WALLET, i);

            // Assert
            Assert.That(deduplication.Contains(WALLET, 4), Is.True, "The stamp that restarted the window is registered");

            for (int i = 0; i < 4; i++)
                Assert.That(deduplication.Contains(WALLET, i), Is.False, $"Stamp {i} should have been dropped with the window");
        }

        [Test]
        public void KeepRegisteringAcrossManyDroppedWindows()
        {
            // Arrange
            var deduplication = new MessageDeduplication<int>(capacity: 4);

            // Act — ten times the capacity
            for (int i = 0; i < 40; i++)
                deduplication.Register(WALLET, i);

            // Assert
            Assert.That(deduplication.Contains(WALLET, 39), Is.True);
        }

        [Test]
        public void DropTheWindowWhenThePeriodElapsed()
        {
            // Arrange — a negative period makes every registration observe an elapsed window
            var deduplication = new MessageDeduplication<int>(TimeSpan.FromTicks(-1));

            // Act
            deduplication.Register(WALLET, 1);
            deduplication.Register(WALLET, 2);

            // Assert
            Assert.That(deduplication.Contains(WALLET, 1), Is.False);
            Assert.That(deduplication.Contains(WALLET, 2), Is.True);
        }

        [Test]
        public void DistinguishStampsByWallet()
        {
            // Arrange
            var deduplication = new MessageDeduplication<int>(capacity: 4);

            // Act
            deduplication.Register(WALLET, 1);

            // Assert
            Assert.That(deduplication.Contains(OTHER_WALLET, 1), Is.False);
        }

        [Test]
        public void StopContainingRemovedStamps()
        {
            // Arrange
            var deduplication = new MessageDeduplication<int>(capacity: 4);
            deduplication.Register(WALLET, 1);

            // Act
            deduplication.Remove(WALLET, 1);

            // Assert
            Assert.That(deduplication.Contains(WALLET, 1), Is.False);
        }
    }
}
