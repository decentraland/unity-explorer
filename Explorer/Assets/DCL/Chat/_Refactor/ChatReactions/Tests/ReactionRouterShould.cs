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
            // Arrange
            var args = MakeArgs(ReactionType.Situational);

            // Act
            fakeBus.Fire(args);

            // Assert
            Assert.That(situationalTarget.Received.Count, Is.EqualTo(1));
            Assert.That(messageTarget.Received.Count, Is.EqualTo(0));
        }

        [Test]
        public void DispatchMessageReactionToCorrectTarget()
        {
            // Arrange
            var args = MakeArgs(ReactionType.Message);

            // Act
            fakeBus.Fire(args);

            // Assert
            Assert.That(messageTarget.Received.Count, Is.EqualTo(1));
            Assert.That(situationalTarget.Received.Count, Is.EqualTo(0));
        }

        [Test]
        public void NotDispatchAfterDispose()
        {
            // Arrange
            router.Dispose();

            // Act
            fakeBus.Fire(MakeArgs(ReactionType.Situational));
            fakeBus.Fire(MakeArgs(ReactionType.Message));

            // Assert
            Assert.That(situationalTarget.Received.Count, Is.EqualTo(0));
            Assert.That(messageTarget.Received.Count, Is.EqualTo(0));
        }

        private static ReactionReceivedArgs MakeArgs(ReactionType type) =>
            new ("0xTestWallet", 1, 1, type, "msg-001");

        private sealed class FakeRemoteReactionTarget : IRemoteReactionTarget
        {
            public readonly List<ReactionReceivedArgs> Received = new ();

            public void HandleRemoteReaction(ReactionReceivedArgs args) => Received.Add(args);
        }
    }
}
