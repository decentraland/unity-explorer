using System.Collections.Generic;
using DCL.Chat.ChatReactions.Configs;
using DCL.Chat.ChatReactions.Networking;
using NUnit.Framework;
using UnityEngine;

namespace DCL.Chat.ChatReactions.Tests
{
    [TestFixture]
    public class ReactionNetworkBroadcasterShould
    {
        private FakeReactionBus fakeBus;
        private ChatReactionsConfig config;
        private ChatReactionsMessageConfig messageConfig;

        [SetUp]
        public void SetUp()
        {
            fakeBus = new FakeReactionBus();
            messageConfig = ScriptableObject.CreateInstance<ChatReactionsMessageConfig>();
            config = ScriptableObject.CreateInstance<ChatReactionsConfig>();
            config.MessageReactions = messageConfig;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(messageConfig);
            Object.DestroyImmediate(config);
        }

        // --- Immediate send (debounce disabled) ---

        [Test]
        public void SendImmediatelyWhenDebounceDisabled()
        {
            messageConfig.NetworkDebounceSeconds = 0f;
            var broadcaster = CreateBroadcaster();

            broadcaster.Broadcast(5);

            Assert.That(fakeBus.SituationalSends.Count, Is.EqualTo(1));
            Assert.That(fakeBus.SituationalSends[0].EmojiIndex, Is.EqualTo(5));
        }

        // --- Debounce buffering ---

        [Test]
        public void BatchMultipleClicksWhenDebounceEnabled()
        {
            messageConfig.NetworkDebounceSeconds = 0.5f;
            messageConfig.NetworkFlushThreshold = 0;
            var broadcaster = CreateBroadcaster();

            broadcaster.Broadcast(1);
            broadcaster.Broadcast(1);
            broadcaster.Broadcast(2);

            Assert.That(fakeBus.SituationalSends.Count, Is.EqualTo(0));

            broadcaster.Tick(0.6f);

            Assert.That(fakeBus.SituationalSends.Count, Is.EqualTo(2));

            var byEmoji = new Dictionary<int, int>();

            foreach (var s in fakeBus.SituationalSends)
                byEmoji[s.EmojiIndex] = s.Count;

            Assert.That(byEmoji[1], Is.EqualTo(2));
            Assert.That(byEmoji[2], Is.EqualTo(1));
        }

        [Test]
        public void AccumulateSameEmojiCount()
        {
            messageConfig.NetworkDebounceSeconds = 1f;
            messageConfig.NetworkFlushThreshold = 0;
            var broadcaster = CreateBroadcaster();

            broadcaster.Broadcast(5);
            broadcaster.Broadcast(5);
            broadcaster.Broadcast(5);
            broadcaster.Tick(1.1f);

            Assert.That(fakeBus.SituationalSends.Count, Is.EqualTo(1));
            Assert.That(fakeBus.SituationalSends[0].EmojiIndex, Is.EqualTo(5));
            Assert.That(fakeBus.SituationalSends[0].Count, Is.EqualTo(3));
        }

        [Test]
        public void TrackMultipleDistinctEmojis()
        {
            messageConfig.NetworkDebounceSeconds = 1f;
            messageConfig.NetworkFlushThreshold = 0;
            var broadcaster = CreateBroadcaster();

            broadcaster.Broadcast(1);
            broadcaster.Broadcast(2);
            broadcaster.Broadcast(3);
            broadcaster.Tick(1.1f);

            Assert.That(fakeBus.SituationalSends.Count, Is.EqualTo(3));
        }

        [Test]
        public void NotFlushBeforeTimerExpires()
        {
            messageConfig.NetworkDebounceSeconds = 0.5f;
            messageConfig.NetworkFlushThreshold = 0;
            var broadcaster = CreateBroadcaster();

            broadcaster.Broadcast(7);
            broadcaster.Tick(0.3f);

            Assert.That(fakeBus.SituationalSends.Count, Is.EqualTo(0));
        }

        [Test]
        public void FlushAfterTimerExpires()
        {
            messageConfig.NetworkDebounceSeconds = 0.5f;
            messageConfig.NetworkFlushThreshold = 0;
            var broadcaster = CreateBroadcaster();

            broadcaster.Broadcast(7);
            broadcaster.Tick(0.6f);

            Assert.That(fakeBus.SituationalSends.Count, Is.EqualTo(1));
            Assert.That(fakeBus.SituationalSends[0].EmojiIndex, Is.EqualTo(7));
        }

        // --- Timer reset ---

        [Test]
        public void ResetTimerOnNewBroadcast()
        {
            messageConfig.NetworkDebounceSeconds = 0.5f;
            messageConfig.NetworkFlushThreshold = 0;
            var broadcaster = CreateBroadcaster();

            broadcaster.Broadcast(1);
            broadcaster.Tick(0.4f);

            Assert.That(fakeBus.SituationalSends.Count, Is.EqualTo(0));

            broadcaster.Broadcast(2); // resets timer to 0.5

            broadcaster.Tick(0.4f); // 0.5 - 0.4 = 0.1 remaining — should NOT flush yet

            Assert.That(fakeBus.SituationalSends.Count, Is.EqualTo(0));

            broadcaster.Tick(0.2f); // 0.1 - 0.2 = -0.1 — NOW it flushes

            Assert.That(fakeBus.SituationalSends.Count, Is.EqualTo(2));
        }

        // --- Buffer reset after flush ---

        [Test]
        public void ResetBufferAfterFlush()
        {
            messageConfig.NetworkDebounceSeconds = 0.5f;
            messageConfig.NetworkFlushThreshold = 0;
            var broadcaster = CreateBroadcaster();

            broadcaster.Broadcast(1);
            broadcaster.Tick(0.6f);

            Assert.That(fakeBus.SituationalSends.Count, Is.EqualTo(1));

            // Tick again — should NOT flush again
            broadcaster.Tick(0.6f);

            Assert.That(fakeBus.SituationalSends.Count, Is.EqualTo(1));
        }

        [Test]
        public void StartNewBufferAfterFlush()
        {
            messageConfig.NetworkDebounceSeconds = 0.5f;
            messageConfig.NetworkFlushThreshold = 0;
            var broadcaster = CreateBroadcaster();

            broadcaster.Broadcast(1);
            broadcaster.Tick(0.6f);
            Assert.That(fakeBus.SituationalSends.Count, Is.EqualTo(1));

            // New buffer cycle
            broadcaster.Broadcast(2);
            broadcaster.Tick(0.6f);

            Assert.That(fakeBus.SituationalSends.Count, Is.EqualTo(2));
            Assert.That(fakeBus.SituationalSends[1].EmojiIndex, Is.EqualTo(2));
        }

        // --- Dispose ---

        [Test]
        public void DiscardBufferedReactionsOnDispose()
        {
            messageConfig.NetworkDebounceSeconds = 1f;
            messageConfig.NetworkFlushThreshold = 0;
            var broadcaster = CreateBroadcaster();

            broadcaster.Broadcast(3);
            broadcaster.Dispose();

            Assert.That(fakeBus.SituationalSends.Count, Is.EqualTo(0));
        }

        [Test]
        public void NotFlushOnDisposeWhenEmpty()
        {
            var broadcaster = CreateBroadcaster();
            broadcaster.Dispose();

            Assert.That(fakeBus.SituationalSends.Count, Is.EqualTo(0));
        }

        // --- Null bus ---

        [Test]
        public void NotSendWhenBusIsNull()
        {
            var broadcaster = new ReactionNetworkBroadcaster(config, null);

            Assert.DoesNotThrow(() => broadcaster.Broadcast(1));
            Assert.DoesNotThrow(() => broadcaster.Tick(1f));
            Assert.DoesNotThrow(() => broadcaster.Dispose());
        }

        // --- Flushed event ---

        [Test]
        public void InvokeFlushedEventWithCorrectTotals()
        {
            messageConfig.NetworkDebounceSeconds = 0.5f;
            messageConfig.NetworkFlushThreshold = 0;

            int capturedUnique = 0;
            int capturedTotal = 0;
            float capturedTimestamp = 0f;

            var broadcaster = CreateBroadcaster();
            broadcaster.Flushed += (unique, total, timestamp) =>
            {
                capturedUnique = unique;
                capturedTotal = total;
                capturedTimestamp = timestamp;
            };

            broadcaster.Broadcast(1);
            broadcaster.Broadcast(1);
            broadcaster.Broadcast(2);
            broadcaster.Tick(0.6f);

            Assert.That(capturedUnique, Is.EqualTo(2));
            Assert.That(capturedTotal, Is.EqualTo(3));
            Assert.That(capturedTimestamp, Is.GreaterThanOrEqualTo(0f));
        }

        private ReactionNetworkBroadcaster CreateBroadcaster() =>
            new (config, fakeBus);
    }
}
