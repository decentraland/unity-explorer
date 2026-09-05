using DCL.Chat.ChatReactions.Networking;
using NUnit.Framework;

namespace DCL.Chat.ChatReactions.Tests
{
    [TestFixture]
    public class PerSenderRateLimiterShould
    {
        [Test]
        public void AllowBurstThenBlock()
        {
            // Arrange
            var limiter = new PerSenderRateLimiter(tokensPerSecond: 1f, burstCapacity: 5f, maxTrackedSenders: 16);

            // Act & Assert
            for (int i = 0; i < 5; i++)
                Assert.That(limiter.TryPass("wallet_a", now: 0f), Is.True, $"Burst message {i} should pass");

            Assert.That(limiter.TryPass("wallet_a", now: 0f), Is.False, "Message beyond burst should be dropped");
        }

        [Test]
        public void RefillTokensOverTime()
        {
            // Arrange
            var limiter = new PerSenderRateLimiter(tokensPerSecond: 1f, burstCapacity: 5f, maxTrackedSenders: 16);

            for (int i = 0; i < 5; i++)
                limiter.TryPass("wallet_a", now: 0f);

            Assert.That(limiter.TryPass("wallet_a", now: 0f), Is.False);

            // Act & Assert — one second refills exactly one token
            Assert.That(limiter.TryPass("wallet_a", now: 1f), Is.True);
            Assert.That(limiter.TryPass("wallet_a", now: 1f), Is.False);
        }

        [Test]
        public void CapRefillAtBurstCapacity()
        {
            // Arrange
            var limiter = new PerSenderRateLimiter(tokensPerSecond: 1f, burstCapacity: 3f, maxTrackedSenders: 16);
            limiter.TryPass("wallet_a", now: 0f);

            // Act & Assert — a long idle period must not accumulate more than the burst capacity
            for (int i = 0; i < 3; i++)
                Assert.That(limiter.TryPass("wallet_a", now: 1000f), Is.True, $"Refilled message {i} should pass");

            Assert.That(limiter.TryPass("wallet_a", now: 1000f), Is.False);
        }

        [Test]
        public void TrackSendersIndependently()
        {
            // Arrange
            var limiter = new PerSenderRateLimiter(tokensPerSecond: 1f, burstCapacity: 2f, maxTrackedSenders: 16);

            limiter.TryPass("wallet_a", now: 0f);
            limiter.TryPass("wallet_a", now: 0f);
            Assert.That(limiter.TryPass("wallet_a", now: 0f), Is.False, "wallet_a exhausted");

            // Act & Assert
            Assert.That(limiter.TryPass("wallet_b", now: 0f), Is.True, "wallet_b has its own budget");
        }

        [Test]
        public void EvictStaleSendersWhenTableIsFull()
        {
            // Arrange — staleness horizon is burstCapacity / tokensPerSecond = 2 seconds
            var limiter = new PerSenderRateLimiter(tokensPerSecond: 1f, burstCapacity: 2f, maxTrackedSenders: 2);

            limiter.TryPass("wallet_a", now: 0f);
            limiter.TryPass("wallet_b", now: 0f);

            // Act & Assert — both tracked senders are stale by now, so a new sender takes a freed slot
            Assert.That(limiter.TryPass("wallet_c", now: 10f), Is.True);
        }

        [Test]
        public void DropNewSendersWhenTableIsFullOfFreshOnes()
        {
            // Arrange
            var limiter = new PerSenderRateLimiter(tokensPerSecond: 1f, burstCapacity: 2f, maxTrackedSenders: 2);

            limiter.TryPass("wallet_a", now: 0f);
            limiter.TryPass("wallet_b", now: 0f);

            // Act & Assert — the table is full and nothing is stale: fail closed
            Assert.That(limiter.TryPass("wallet_c", now: 0.5f), Is.False);
        }
    }
}
