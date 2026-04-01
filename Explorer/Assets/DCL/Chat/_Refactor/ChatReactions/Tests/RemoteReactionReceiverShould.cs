using System.Collections.Generic;
using DCL.Chat.ChatReactions.Core;
using DCL.Chat.ChatReactions.Networking;
using NUnit.Framework;

namespace DCL.Chat.ChatReactions.Tests
{
    [TestFixture]
    public class RemoteReactionReceiverShould
    {
        private List<ReactionReceivedArgs> processed;
        private RemoteReactionReceiver receiver;

        [SetUp]
        public void SetUp()
        {
            processed = new List<ReactionReceivedArgs>();
        }

        [Test]
        public void DrainAllImmediatelyWhenStaggerDisabled()
        {
            receiver = new RemoteReactionReceiver(() => 0f, processed.Add);

            receiver.Enqueue(MakeArgs("wallet_a", emojiIndex: 1, count: 1));
            receiver.Enqueue(MakeArgs("wallet_b", emojiIndex: 2, count: 1));
            receiver.Tick(0.016f);

            Assert.That(processed.Count, Is.EqualTo(2));
            Assert.That(processed[0].EmojiIndex, Is.EqualTo(1));
            Assert.That(processed[1].EmojiIndex, Is.EqualTo(2));
        }

        [Test]
        public void StaggerDrainOnePerInterval()
        {
            receiver = new RemoteReactionReceiver(() => 0.1f, processed.Add);

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

        [Test]
        public void ClampCountToMaxExpand()
        {
            receiver = new RemoteReactionReceiver(() => 0f, processed.Add);

            receiver.Enqueue(MakeArgs("wallet_a", emojiIndex: 7, count: 50));
            receiver.Tick(0.016f);

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
            receiver = new RemoteReactionReceiver(() => 0.1f, processed.Add);

            receiver.Enqueue(MakeArgs("wallet_a", emojiIndex: 1, count: 1));
            receiver.Tick(0.2f);

            receiver.Enqueue(MakeArgs("wallet_b", emojiIndex: 2, count: 1));
            receiver.Tick(0.001f);
            Assert.That(processed.Count, Is.EqualTo(2));
        }

        private static ReactionReceivedArgs MakeArgs(string wallet, int emojiIndex, int count) =>
            new (wallet, emojiIndex, count, ReactionType.Situational, string.Empty);
    }
}
