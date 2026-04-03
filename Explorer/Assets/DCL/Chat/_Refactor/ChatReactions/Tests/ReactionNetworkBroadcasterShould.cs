using System;
using System.Collections.Generic;
using DCL.Chat.ChatReactions.Networking;
using NUnit.Framework;

namespace DCL.Chat.ChatReactions.Tests
{
    [TestFixture]
    public class ReactionNetworkBroadcasterShould
    {
        private FakeReactionBus fakeBus;

        [SetUp]
        public void SetUp()
        {
            fakeBus = new FakeReactionBus();
        }

        [Test]
        public void SendImmediatelyWhenDebounceDisabled()
        {
            // Arrange
            var broadcaster = new ReactionNetworkBroadcaster(fakeBus, () => 0f);

            // Act
            broadcaster.Broadcast(5);

            // Assert
            Assert.That(fakeBus.SituationalSends.Count, Is.EqualTo(1));
            Assert.That(fakeBus.SituationalSends[0].EmojiIndex, Is.EqualTo(5));
        }

        // Verifies that rapid clicks are coalesced into a single batched send per emoji after the debounce window.
        [Test]
        public void BatchMultipleClicksWhenDebounceEnabled()
        {
            // Arrange
            var broadcaster = new ReactionNetworkBroadcaster(fakeBus, () => 0.5f);

            broadcaster.Broadcast(1);
            broadcaster.Broadcast(1);
            broadcaster.Broadcast(2);

            Assert.That(fakeBus.SituationalSends.Count, Is.EqualTo(0));

            // Act
            broadcaster.Tick(0.6f);

            // Assert
            Assert.That(fakeBus.SituationalSends.Count, Is.EqualTo(2));

            var byEmoji = new Dictionary<int, int>();

            foreach (var s in fakeBus.SituationalSends)
                byEmoji[s.EmojiIndex] = s.Count;

            Assert.That(byEmoji[1], Is.EqualTo(2));
            Assert.That(byEmoji[2], Is.EqualTo(1));
        }

        [Test]
        public void DiscardBufferedReactionsOnDispose()
        {
            // Arrange
            var broadcaster = new ReactionNetworkBroadcaster(fakeBus, () => 1f);
            broadcaster.Broadcast(3);

            // Act
            broadcaster.Dispose();

            // Assert
            Assert.That(fakeBus.SituationalSends.Count, Is.EqualTo(0));
        }

        [Test]
        public void NotSendWhenBusIsNull()
        {
            // Arrange
            var broadcaster = new ReactionNetworkBroadcaster(null, () => 0f);

            // Act & Assert
            Assert.DoesNotThrow(() => broadcaster.Broadcast(1));
            Assert.DoesNotThrow(() => broadcaster.Tick(1f));
            Assert.DoesNotThrow(() => broadcaster.Dispose());
        }

        [Test]
        public void InvokeOnFlushedCallbackWithCorrectTotals()
        {
            // Arrange
            int capturedUnique = 0;
            int capturedTotal = 0;
            float capturedTimestamp = 0f;

            void OnFlushed(int unique, int total, float timestamp)
            {
                capturedUnique = unique;
                capturedTotal = total;
                capturedTimestamp = timestamp;
            }

            var broadcaster = new ReactionNetworkBroadcaster(fakeBus, () => 0.5f, OnFlushed);

            // Act
            broadcaster.Broadcast(1);
            broadcaster.Broadcast(1);
            broadcaster.Broadcast(2);
            broadcaster.Tick(0.6f);

            // Assert
            Assert.That(capturedUnique, Is.EqualTo(2));
            Assert.That(capturedTotal, Is.EqualTo(3));
            Assert.That(capturedTimestamp, Is.GreaterThanOrEqualTo(0f));
        }

    }
}
