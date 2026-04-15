using System.Collections.Generic;
using DCL.Chat.ChatReactions.Configs;
using DCL.Chat.ChatReactions.Core;
using DCL.Chat.ChatReactions.Networking;
using DCL.Chat.ChatReactions.Simulation.World;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

namespace DCL.Chat.ChatReactions.Tests
{
    [TestFixture]
    public class SituationalRemoteTargetQueueShould
    {
        private List<ReactionReceivedArgs> processed;
        private ChatReactionsConfig config;
        private ChatReactionsMessageConfig messageConfig;
        private ChatReactionsWorldLaneConfig worldLaneConfig;

        [SetUp]
        public void SetUp()
        {
            processed = new List<ReactionReceivedArgs>();
            messageConfig = ScriptableObject.CreateInstance<ChatReactionsMessageConfig>();
            worldLaneConfig = ScriptableObject.CreateInstance<ChatReactionsWorldLaneConfig>();
            config = ScriptableObject.CreateInstance<ChatReactionsConfig>();
            config.MessageReactions = messageConfig;
            config.WorldLane = worldLaneConfig;
            config.MaxRemoteUIReactionsPerSec = 0f;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(messageConfig);
            Object.DestroyImmediate(worldLaneConfig);
            Object.DestroyImmediate(config);
        }

        [Test]
        public void DrainAllImmediatelyWhenStaggerDisabled()
        {
            var target = CreateTarget(baseStagger: 0f);

            target.HandleRemoteReaction(MakeArgs("wallet_a", emojiIndex: 1, count: 1));
            target.HandleRemoteReaction(MakeArgs("wallet_b", emojiIndex: 2, count: 1));

            target.Tick(0.016f);

            Assert.That(processed.Count, Is.EqualTo(2));
            Assert.That(processed[0].EmojiIndex, Is.EqualTo(1));
            Assert.That(processed[1].EmojiIndex, Is.EqualTo(2));
        }

        [Test]
        public void StaggerDrainOnePerInterval()
        {
            var target = CreateTarget(baseStagger: 0.1f);

            target.HandleRemoteReaction(MakeArgs("wallet_a", emojiIndex: 1, count: 1));
            target.HandleRemoteReaction(MakeArgs("wallet_b", emojiIndex: 2, count: 1));
            target.HandleRemoteReaction(MakeArgs("wallet_c", emojiIndex: 3, count: 1));

            // First tick (0.05s): timer 0 - 0.05 = -0.05 <= 0 → drain #1, timer becomes 0.05
            target.Tick(0.05f);
            Assert.That(processed.Count, Is.EqualTo(1));

            // Second tick (0.04s): timer 0.05 - 0.04 = 0.01 > 0 → no drain
            target.Tick(0.04f);
            Assert.That(processed.Count, Is.EqualTo(1), "Should not drain — timer still positive");

            // Third tick (0.02s): timer 0.01 - 0.02 = -0.01 <= 0 → drain #2
            target.Tick(0.02f);
            Assert.That(processed.Count, Is.EqualTo(2));
        }

        [Test]
        public void ClampCountToMaxExpand()
        {
            var target = CreateTarget(baseStagger: 0f);

            target.HandleRemoteReaction(MakeArgs("wallet_a", emojiIndex: 7, count: 50));
            target.Tick(0.016f);

            Assert.That(processed.Count, Is.EqualTo(20));

            for (int i = 0; i < processed.Count; i++)
            {
                Assert.That(processed[i].EmojiIndex, Is.EqualTo(7));
                Assert.That(processed[i].Count, Is.EqualTo(1));
            }
        }

        [Test]
        public void ResetStaggerTimerWhenQueueEmpties()
        {
            var target = CreateTarget(baseStagger: 0.1f);

            target.HandleRemoteReaction(MakeArgs("wallet_a", emojiIndex: 1, count: 1));
            target.Tick(0.2f);

            target.HandleRemoteReaction(MakeArgs("wallet_b", emojiIndex: 2, count: 1));
            target.Tick(0.001f);

            Assert.That(processed.Count, Is.EqualTo(2));
        }

        [Test]
        public void DropEntriesWhenQueueExceedsMaxDepth()
        {
            var target = CreateTarget(baseStagger: 0f, maxQueueDepth: 3);

            for (int i = 0; i < 5; i++)
                target.HandleRemoteReaction(MakeArgs("wallet", emojiIndex: i, count: 1));

            target.Tick(0.016f);

            Assert.That(processed.Count, Is.EqualTo(3));
            Assert.That(processed[0].EmojiIndex, Is.EqualTo(0));
            Assert.That(processed[1].EmojiIndex, Is.EqualTo(1));
            Assert.That(processed[2].EmojiIndex, Is.EqualTo(2));
        }

        [Test]
        public void AllowUnlimitedQueueWhenMaxDepthIsZero()
        {
            var target = CreateTarget(baseStagger: 0f, maxQueueDepth: 0);

            target.HandleRemoteReaction(MakeArgs("wallet", emojiIndex: 1, count: 25));
            target.Tick(0.016f);

            Assert.That(processed.Count, Is.EqualTo(20)); // clamped by MAX_EXPAND, not depth
        }

        [Test]
        public void CapExpandedCountAtMaxDepth()
        {
            var target = CreateTarget(baseStagger: 0f, maxQueueDepth: 5);

            target.HandleRemoteReaction(MakeArgs("wallet", emojiIndex: 3, count: 10));
            target.Tick(0.016f);

            Assert.That(processed.Count, Is.EqualTo(5));
        }

        [Test]
        public void UseBaseStaggerWhenQueueBelowRampStart()
        {
            var target = CreateTarget(baseStagger: 0.1f, maxQueueDepth: 50, rampStart: 10);

            target.HandleRemoteReaction(MakeArgs("wallet_a", emojiIndex: 1, count: 1));
            target.HandleRemoteReaction(MakeArgs("wallet_b", emojiIndex: 2, count: 1));

            target.Tick(0.05f);
            Assert.That(processed.Count, Is.EqualTo(1));

            target.Tick(0.04f);
            Assert.That(processed.Count, Is.EqualTo(1), "Base stagger should prevent drain");
        }

        [Test]
        public void UseMinStaggerWhenQueueAtMaxDepth()
        {
            var target = CreateTarget(baseStagger: 0.1f, maxQueueDepth: 5, rampStart: 2, minStagger: 0f);

            for (int i = 0; i < 5; i++)
                target.HandleRemoteReaction(MakeArgs("wallet", emojiIndex: i, count: 1));

            target.Tick(0.001f);

            Assert.That(processed.Count, Is.EqualTo(5), "Min stagger 0 should drain all immediately");
        }

        [Test]
        public void FallBackToBaseStaggerWhenRampStartExceedsMaxDepth()
        {
            var target = CreateTarget(baseStagger: 0.1f, maxQueueDepth: 50, rampStart: 100);

            for (int i = 0; i < 50; i++)
                target.HandleRemoteReaction(MakeArgs("wallet", emojiIndex: i, count: 1));

            target.Tick(0.05f);
            Assert.That(processed.Count, Is.EqualTo(1), "Should use base stagger when rampStart >= maxDepth");
        }

        private SituationalRemoteTarget CreateTarget(
            float baseStagger = 0f,
            int maxQueueDepth = 0,
            int rampStart = 0,
            float minStagger = 0f)
        {
            config.MaxReceiveQueueDepth = maxQueueDepth;
            config.DynamicStaggerRampStart = rampStart;
            config.MinStaggerInterval = minStagger;
            messageConfig.ReceiveStaggerInterval = baseStagger;

            var spawner = Substitute.For<IWorldReactionSpawner>();
            var avatarPos = Substitute.For<IAvatarReactionPosition>();
            var worldReactor = new LocalPlayerWorldReactor(spawner, config.WorldLane, avatarPos);
            worldReactor.WorldReactionsEnabled = false;

            // UI simulation is never reached: ShowRemoteUIReactions is set to false below.
            var target = new SituationalRemoteTarget(config, worldReactor, null!);
            target.ShowRemoteUIReactions = false;
            target.RemoteReactionProcessed += args => processed.Add(args);
            return target;
        }

        private static ReactionReceivedArgs MakeArgs(string wallet, int emojiIndex, int count) =>
            new (wallet, emojiIndex, count, ReactionType.Situational, string.Empty);
    }
}
