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
            var broadcaster = new ReactionNetworkBroadcaster(fakeBus, () => 0f);

            broadcaster.Broadcast(5);

            Assert.That(fakeBus.SituationalSends.Count, Is.EqualTo(1));
            Assert.That(fakeBus.SituationalSends[0].EmojiIndex, Is.EqualTo(5));
        }

        [Test]
        public void BatchMultipleClicksWhenDebounceEnabled()
        {
            var broadcaster = new ReactionNetworkBroadcaster(fakeBus, () => 0.5f);

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
        public void FlushRemainingOnDispose()
        {
            var broadcaster = new ReactionNetworkBroadcaster(fakeBus, () => 1f);

            broadcaster.Broadcast(3);
            broadcaster.Dispose();

            Assert.That(fakeBus.SituationalSends.Count, Is.EqualTo(1));
            Assert.That(fakeBus.SituationalSends[0].EmojiIndex, Is.EqualTo(3));
        }

        [Test]
        public void NotSendWhenBusIsNull()
        {
            var broadcaster = new ReactionNetworkBroadcaster(null, () => 0f);

            Assert.DoesNotThrow(() => broadcaster.Broadcast(1));
            Assert.DoesNotThrow(() => broadcaster.Tick(1f));
            Assert.DoesNotThrow(() => broadcaster.Dispose());
        }

        [Test]
        public void InvokeOnFlushedCallbackWithCorrectTotals()
        {
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

            broadcaster.Broadcast(1);
            broadcaster.Broadcast(1);
            broadcaster.Broadcast(2);
            broadcaster.Tick(0.6f);

            Assert.That(capturedUnique, Is.EqualTo(2));
            Assert.That(capturedTotal, Is.EqualTo(3));
            Assert.That(capturedTimestamp, Is.GreaterThanOrEqualTo(0f));
        }

        /// <summary>
        /// Minimal fake that records calls. Avoids NSubstitute for value-type arg capture.
        /// </summary>
        private sealed class FakeReactionBus : IReactionMessageBus
        {
            public readonly List<(int EmojiIndex, int Count, float Timestamp)> SituationalSends = new ();

            public event Action<ReactionReceivedArgs>? ReactionReceived;

            public void SendSituationalReaction(int emojiIndex, int count = 1, float overrideTimestamp = 0f) =>
                SituationalSends.Add((emojiIndex, count, overrideTimestamp));

            public void SendMessageReaction(int emojiIndex, string messageId, ReactionChannelRouting routing) { }

            public void Dispose() { }
        }
    }
}
