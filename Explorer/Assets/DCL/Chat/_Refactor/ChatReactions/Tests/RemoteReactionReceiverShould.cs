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
            messageConfig.NetworkFlushThreshold = 10;
            config.MaxPerAvatarQueued = 0;
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
            var target = CreateTarget(stagger: 0f);

            target.HandleRemoteReaction(MakeArgs("wallet_a", emojiIndex: 1, count: 1));
            target.HandleRemoteReaction(MakeArgs("wallet_b", emojiIndex: 2, count: 1));

            target.Tick(0.016f);

            Assert.That(processed.Count, Is.EqualTo(2));
            Assert.That(processed.Exists(p => p.EmojiIndex == 1 && p.WalletId == "wallet_a"), Is.True);
            Assert.That(processed.Exists(p => p.EmojiIndex == 2 && p.WalletId == "wallet_b"), Is.True);
        }

        [Test]
        public void StaggerDrainsOneParticlePerIntervalForSingleAvatar()
        {
            var target = CreateTarget(stagger: 0.1f);

            target.HandleRemoteReaction(MakeArgs("wallet_a", emojiIndex: 1, count: 3));

            target.Tick(0.05f);
            Assert.That(processed.Count, Is.EqualTo(1), "First particle pops on first tick");

            target.Tick(0.04f);
            Assert.That(processed.Count, Is.EqualTo(1), "Timer still positive — no drain");

            target.Tick(0.02f);
            Assert.That(processed.Count, Is.EqualTo(2), "Timer expired — second particle pops");
        }

        [Test]
        public void TwoAvatarsDrainInParallelAtSameRate()
        {
            var target = CreateTarget(stagger: 0.1f);

            target.HandleRemoteReaction(MakeArgs("wallet_a", emojiIndex: 1, count: 3));
            target.HandleRemoteReaction(MakeArgs("wallet_b", emojiIndex: 2, count: 3));

            target.Tick(0.05f);
            Assert.That(processed.Count, Is.EqualTo(2), "Both avatars pop one particle on the same tick");

            int aCount = 0;
            int bCount = 0;
            foreach (var p in processed)
            {
                if (p.WalletId == "wallet_a") aCount++;
                else if (p.WalletId == "wallet_b") bCount++;
            }
            Assert.That(aCount, Is.EqualTo(1));
            Assert.That(bCount, Is.EqualTo(1));
        }

        [Test]
        public void ClampCountToNetworkFlushThreshold()
        {
            messageConfig.NetworkFlushThreshold = 10;
            var target = CreateTarget(stagger: 0f);

            target.HandleRemoteReaction(MakeArgs("wallet_a", emojiIndex: 7, count: 50));
            target.Tick(0.016f);

            Assert.That(processed.Count, Is.EqualTo(10), "Batch clamped to NetworkFlushThreshold");

            foreach (var p in processed)
            {
                Assert.That(p.EmojiIndex, Is.EqualTo(7));
                Assert.That(p.Count, Is.EqualTo(1));
            }
        }

        [Test]
        public void DropOldestWhenPerAvatarCapExceeded()
        {
            config.MaxPerAvatarQueued = 3;
            var target = CreateTarget(stagger: 0f);

            for (int i = 0; i < 5; i++)
                target.HandleRemoteReaction(MakeArgs("wallet", emojiIndex: i, count: 1));

            target.Tick(0.016f);

            Assert.That(processed.Count, Is.EqualTo(3), "Queue capped at 3");
            Assert.That(processed[0].EmojiIndex, Is.EqualTo(2), "Oldest two dropped, queue holds emojis 2,3,4");
            Assert.That(processed[1].EmojiIndex, Is.EqualTo(3));
            Assert.That(processed[2].EmojiIndex, Is.EqualTo(4));
        }

        [Test]
        public void PerAvatarCapDoesNotAffectOtherAvatars()
        {
            config.MaxPerAvatarQueued = 2;
            var target = CreateTarget(stagger: 0f);

            for (int i = 0; i < 5; i++)
                target.HandleRemoteReaction(MakeArgs("wallet_a", emojiIndex: i, count: 1));

            target.HandleRemoteReaction(MakeArgs("wallet_b", emojiIndex: 99, count: 1));

            target.Tick(0.016f);

            int aCount = 0;
            int bCount = 0;
            foreach (var p in processed)
            {
                if (p.WalletId == "wallet_a") aCount++;
                else if (p.WalletId == "wallet_b") bCount++;
            }

            Assert.That(aCount, Is.EqualTo(2), "wallet_a capped at 2");
            Assert.That(bCount, Is.EqualTo(1), "wallet_b unaffected");
        }

        [Test]
        public void ZeroPerAvatarCapMeansUnlimited()
        {
            config.MaxPerAvatarQueued = 0;
            messageConfig.NetworkFlushThreshold = 10;
            var target = CreateTarget(stagger: 0f);

            target.HandleRemoteReaction(MakeArgs("wallet", emojiIndex: 1, count: 10));

            target.Tick(0.016f);

            Assert.That(processed.Count, Is.EqualTo(10));
        }

        [Test]
        public void FreshCascadeTimerStartsAtZeroAfterAvatarDrainsAndReturns()
        {
            var target = CreateTarget(stagger: 0.1f);

            target.HandleRemoteReaction(MakeArgs("wallet_a", emojiIndex: 1, count: 1));

            target.Tick(0.05f);
            Assert.That(processed.Count, Is.EqualTo(1));

            target.Tick(1.0f); // long idle — cascade already removed

            target.HandleRemoteReaction(MakeArgs("wallet_a", emojiIndex: 2, count: 1));
            target.Tick(0.001f);

            Assert.That(processed.Count, Is.EqualTo(2), "Fresh cascade starts with timer 0 and drains on first tick");
        }

        [Test]
        public void MultipleBatchesFromSameAvatarQueueUp()
        {
            var target = CreateTarget(stagger: 0.1f);

            target.HandleRemoteReaction(MakeArgs("wallet", emojiIndex: 1, count: 2));
            target.HandleRemoteReaction(MakeArgs("wallet", emojiIndex: 2, count: 2));

            target.Tick(1.0f); // long enough to drain all four

            Assert.That(processed.Count, Is.EqualTo(4));
            Assert.That(processed[0].EmojiIndex, Is.EqualTo(1));
            Assert.That(processed[1].EmojiIndex, Is.EqualTo(1));
            Assert.That(processed[2].EmojiIndex, Is.EqualTo(2));
            Assert.That(processed[3].EmojiIndex, Is.EqualTo(2));
        }

        private SituationalRemoteTarget CreateTarget(float stagger)
        {
            config.SituationalReceiveStaggerInterval = stagger;

            var spawner = Substitute.For<IWorldReactionSpawner>();
            var avatarPos = Substitute.For<IAvatarReactionPosition>();
            var worldReactor = new LocalPlayerWorldReactor(spawner, config.WorldLane, avatarPos);
            worldReactor.WorldReactionsEnabled = false;

            var target = new SituationalRemoteTarget(config, worldReactor, null!);
            target.ShowRemoteUIReactions = false;
            target.RemoteReactionProcessed += args => processed.Add(args);
            return target;
        }

        private static ReactionReceivedArgs MakeArgs(string wallet, int emojiIndex, int count) =>
            new (wallet, emojiIndex, count, ReactionType.Situational, string.Empty);
    }
}
