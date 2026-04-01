using System;
using System.Collections.Generic;
using DCL.Chat.ChatReactions.Networking;
using NUnit.Framework;

namespace DCL.Chat.ChatReactions.Tests
{
    [TestFixture]
    public class ReactionRouterShould
    {
        private FakeReactionBus fakeBus;
        private FakeRemoteReactionTarget situationalTarget;
        private FakeRemoteReactionTarget messageTarget;
        private ReactionRouter router;

        [SetUp]
        public void SetUp()
        {
            fakeBus = new FakeReactionBus();
            situationalTarget = new FakeRemoteReactionTarget();
            messageTarget = new FakeRemoteReactionTarget();
            router = new ReactionRouter(fakeBus, situationalTarget, messageTarget);
        }

        [TearDown]
        public void TearDown()
        {
            router.Dispose();
        }

        [Test]
        public void DispatchSituationalReactionToCorrectTarget()
        {
            var args = MakeArgs(ReactionType.Situational);

            fakeBus.Fire(args);

            Assert.That(situationalTarget.Received.Count, Is.EqualTo(1));
            Assert.That(messageTarget.Received.Count, Is.EqualTo(0));
        }

        [Test]
        public void DispatchMessageReactionToCorrectTarget()
        {
            var args = MakeArgs(ReactionType.Message);

            fakeBus.Fire(args);

            Assert.That(messageTarget.Received.Count, Is.EqualTo(1));
            Assert.That(situationalTarget.Received.Count, Is.EqualTo(0));
        }

        [Test]
        public void NotDispatchAfterDispose()
        {
            router.Dispose();

            fakeBus.Fire(MakeArgs(ReactionType.Situational));
            fakeBus.Fire(MakeArgs(ReactionType.Message));

            Assert.That(situationalTarget.Received.Count, Is.EqualTo(0));
            Assert.That(messageTarget.Received.Count, Is.EqualTo(0));
        }

        private static ReactionReceivedArgs MakeArgs(ReactionType type) =>
            new ("0xTestWallet", 1, 1, type, "msg-001");

        private sealed class FakeReactionBus : IReactionMessageBus
        {
            public event Action<ReactionReceivedArgs>? ReactionReceived;

            public void Fire(ReactionReceivedArgs args) => ReactionReceived?.Invoke(args);

            public void SendSituationalReaction(int emojiIndex, int count = 1, float overrideTimestamp = 0f) { }

            public void SendMessageReaction(int emojiIndex, string messageId, ReactionChannelRouting routing) { }

            public void Dispose() { }
        }

        private sealed class FakeRemoteReactionTarget : IRemoteReactionTarget
        {
            public readonly List<ReactionReceivedArgs> Received = new ();

            public void HandleRemoteReaction(ReactionReceivedArgs args) => Received.Add(args);
        }
    }
}
