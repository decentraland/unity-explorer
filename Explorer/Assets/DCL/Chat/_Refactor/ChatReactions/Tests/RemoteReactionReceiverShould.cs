using System.Collections.Generic;
using DCL.Chat.ChatReactions.Configs;
using DCL.Chat.ChatReactions.Core;
using DCL.Chat.ChatReactions.Networking;
using NUnit.Framework;
using UnityEngine;

namespace DCL.Chat.ChatReactions.Tests
{
    [TestFixture]
    public class RemoteReactionReceiverShould
    {
        private List<ReactionReceivedArgs> processed;
        private ChatReactionsConfig config;
        private ChatReactionsMessageConfig messageConfig;

        [SetUp]
        public void SetUp()
        {
            processed = new List<ReactionReceivedArgs>();
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

        [Test]
        public void DrainAllImmediatelyWhenStaggerDisabled()
        {
            var receiver = CreateReceiver(baseStagger: 0f);

            receiver.Enqueue(MakeArgs("wallet_a", emojiIndex: 1, count: 1));
            receiver.Enqueue(MakeArgs("wallet_b", emojiIndex: 2, count: 1));

            receiver.Tick(0.016f);

            Assert.That(processed.Count, Is.EqualTo(2));
            Assert.That(processed[0].EmojiIndex, Is.EqualTo(1));
            Assert.That(processed[1].EmojiIndex, Is.EqualTo(2));
        }

        // Verifies the stagger timer allows only one drain per interval, spacing out processing across ticks.
        [Test]
        public void StaggerDrainOnePerInterval()
        {
            var receiver = CreateReceiver(baseStagger: 0.1f);

            receiver.Enqueue(MakeArgs("wallet_a", emojiIndex: 1, count: 1));
            receiver.Enqueue(MakeArgs("wallet_b", emojiIndex: 2, count: 1));
            receiver.Enqueue(MakeArgs("wallet_c", emojiIndex: 3, count: 1));

            // First tick (0.05s): timer 0 - 0.05 = -0.05 <= 0 → drain #1, timer becomes 0.05
            receiver.Tick(0.05f);
            Assert.That(processed.Count, Is.EqualTo(1));

            // Second tick (0.04s): timer 0.05 - 0.04 = 0.01 > 0 → no drain
            receiver.Tick(0.04f);
            Assert.That(processed.Count, Is.EqualTo(1), "Should not drain — timer still positive");

            // Third tick (0.02s): timer 0.01 - 0.02 = -0.01 <= 0 → drain #2
            receiver.Tick(0.02f);
            Assert.That(processed.Count, Is.EqualTo(2));
        }

        // Verifies that a high count is clamped to the max-expand limit, producing individual items.
        [Test]
        public void ClampCountToMaxExpand()
        {
            var receiver = CreateReceiver(baseStagger: 0f);

            receiver.Enqueue(MakeArgs("wallet_a", emojiIndex: 7, count: 50));
            receiver.Tick(0.016f);

            Assert.That(processed.Count, Is.EqualTo(20));

            for (int i = 0; i < processed.Count; i++)
            {
                Assert.That(processed[i].EmojiIndex, Is.EqualTo(7));
                Assert.That(processed[i].Count, Is.EqualTo(1));
            }
        }

        // Ensures the stagger timer resets to zero when the queue empties, so the next enqueue drains immediately.
        [Test]
        public void ResetStaggerTimerWhenQueueEmpties()
        {
            var receiver = CreateReceiver(baseStagger: 0.1f);

            receiver.Enqueue(MakeArgs("wallet_a", emojiIndex: 1, count: 1));
            receiver.Tick(0.2f);

            receiver.Enqueue(MakeArgs("wallet_b", emojiIndex: 2, count: 1));
            receiver.Tick(0.001f);

            Assert.That(processed.Count, Is.EqualTo(2));
        }

        [Test]
        public void DropEntriesWhenQueueExceedsMaxDepth()
        {
            // maxDepth = 3, enqueue 5 items — only first 3 should be queued
            var receiver = CreateReceiver(baseStagger: 0f, maxQueueDepth: 3);

            for (int i = 0; i < 5; i++)
                receiver.Enqueue(MakeArgs("wallet", emojiIndex: i, count: 1));

            receiver.Tick(0.016f);

            Assert.That(processed.Count, Is.EqualTo(3));
            Assert.That(processed[0].EmojiIndex, Is.EqualTo(0));
            Assert.That(processed[1].EmojiIndex, Is.EqualTo(1));
            Assert.That(processed[2].EmojiIndex, Is.EqualTo(2));
        }

        [Test]
        public void AllowUnlimitedQueueWhenMaxDepthIsZero()
        {
            // maxDepth = 0 means unlimited — all 25 items should be queued (20 from expand clamp)
            var receiver = CreateReceiver(baseStagger: 0f, maxQueueDepth: 0);

            receiver.Enqueue(MakeArgs("wallet", emojiIndex: 1, count: 25));
            receiver.Tick(0.016f);

            Assert.That(processed.Count, Is.EqualTo(20)); // clamped by MAX_EXPAND, not depth
        }

        [Test]
        public void CapExpandedCountAtMaxDepth()
        {
            // maxDepth = 5, enqueue count=10 — expansion should stop at depth 5
            var receiver = CreateReceiver(baseStagger: 0f, maxQueueDepth: 5);

            receiver.Enqueue(MakeArgs("wallet", emojiIndex: 3, count: 10));
            receiver.Tick(0.016f);

            Assert.That(processed.Count, Is.EqualTo(5));
        }

        [Test]
        public void UseBaseStaggerWhenQueueBelowRampStart()
        {
            // rampStart = 10, maxDepth = 50 — with only 2 items, should use base stagger (0.1)
            var receiver = CreateReceiver(baseStagger: 0.1f, maxQueueDepth: 50, rampStart: 10);

            receiver.Enqueue(MakeArgs("wallet_a", emojiIndex: 1, count: 1));
            receiver.Enqueue(MakeArgs("wallet_b", emojiIndex: 2, count: 1));

            // tick 0.05s — base stagger 0.1s, should drain 1 (timer starts at 0, goes negative)
            receiver.Tick(0.05f);
            Assert.That(processed.Count, Is.EqualTo(1));

            // tick 0.04s — timer should still be positive (0.05), no drain
            receiver.Tick(0.04f);
            Assert.That(processed.Count, Is.EqualTo(1), "Base stagger should prevent drain");
        }

        [Test]
        public void UseMinStaggerWhenQueueAtMaxDepth()
        {
            // minStagger = 0, rampStart = 2, maxDepth = 5
            // Fill queue to maxDepth → stagger should be 0 (drain all immediately)
            var receiver = CreateReceiver(baseStagger: 0.1f, maxQueueDepth: 5, rampStart: 2, minStagger: 0f);

            for (int i = 0; i < 5; i++)
                receiver.Enqueue(MakeArgs("wallet", emojiIndex: i, count: 1));

            receiver.Tick(0.001f);

            Assert.That(processed.Count, Is.EqualTo(5), "Min stagger 0 should drain all immediately");
        }

        [Test]
        public void FallBackToBaseStaggerWhenRampStartExceedsMaxDepth()
        {
            // rampStart = 100 > maxDepth = 50 — ramp is effectively disabled, use base stagger
            var receiver = CreateReceiver(baseStagger: 0.1f, maxQueueDepth: 50, rampStart: 100);

            for (int i = 0; i < 50; i++)
                receiver.Enqueue(MakeArgs("wallet", emojiIndex: i, count: 1));

            // With base stagger 0.1, a single 0.05 tick should drain only 1
            receiver.Tick(0.05f);
            Assert.That(processed.Count, Is.EqualTo(1), "Should use base stagger when rampStart >= maxDepth");
        }

        private RemoteReactionReceiver CreateReceiver(
            float baseStagger = 0f,
            int maxQueueDepth = 0,
            int rampStart = 0,
            float minStagger = 0f)
        {
            config.MaxReceiveQueueDepth = maxQueueDepth;
            config.DynamicStaggerRampStart = rampStart;
            config.MinStaggerInterval = minStagger;
            messageConfig.ReceiveStaggerInterval = baseStagger;
            return new RemoteReactionReceiver(config, processed.Add);
        }

        private static ReactionReceivedArgs MakeArgs(string wallet, int emojiIndex, int count) =>
            new (wallet, emojiIndex, count, ReactionType.Situational, string.Empty);
    }
}
