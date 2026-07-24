using DCL.Chat.ChatReactions.Configs;
using DCL.Chat.ChatReactions.Networking;
using DCL.Friends.UserBlocking;
using DCL.Multiplayer.Connections.Messaging;
using DCL.Multiplayer.Connections.Messaging.Hubs;
using DCL.Multiplayer.Connections.Messaging.Pipe;
using DCL.Multiplayer.Connections.Rooms;
using DCL.Web3.Identities;
using Decentraland.Kernel.Comms.Rfc4;
using Google.Protobuf;
using LiveKit.Internal.FFIClients.Pools;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Chat.ChatReactions.Tests
{
    [TestFixture]
    public class MultiplayerReactionMessageBusShould
    {
        private FakeMessagePipesHub pipesHub = null!;
        private IMultiPool multiPool = null!;
        private List<ReactionReceivedArgs> received = null!;
        private ChatReactionsConfig config = null!;
        private MultiplayerReactionMessageBus? bus;

        [SetUp]
        public void SetUp()
        {
            pipesHub = new FakeMessagePipesHub();
            multiPool = Substitute.For<IMultiPool>();
            received = new List<ReactionReceivedArgs>();
            config = ScriptableObject.CreateInstance<ChatReactionsConfig>();
        }

        [TearDown]
        public void TearDown()
        {
            bus?.Dispose();
            UnityEngine.Object.DestroyImmediate(config);
        }

        [Test]
        public void ClampSituationalCountAtIntake()
        {
            // Arrange
            CreateBus();

            // Act — a malicious sender claims an absurd batch size
            DeliverSituational("0xattacker", emojiIndex: 1, count: int.MaxValue, timestamp: 1f);

            // Assert — clamped to the intake hard maximum (50)
            Assert.That(received.Count, Is.EqualTo(1));
            Assert.That(received[0].Count, Is.EqualTo(50));
        }

        [Test]
        public void RejectSituationalIndexOutsideValidRange()
        {
            // Arrange
            CreateBus(maxValidEmojiIndex: 100);

            // Act
            DeliverSituational("0xattacker", emojiIndex: 100, count: 1, timestamp: 1f);
            DeliverSituational("0xattacker", emojiIndex: -1, count: 1, timestamp: 2f);

            // Assert
            Assert.That(received, Is.Empty);
        }

        [Test]
        public void RejectChatReactionIndexBeyondConfiguredAtlasSize()
        {
            // Arrange — atlas holds 100 tiles; wire values above it must be rejected at intake
            CreateBus(maxValidEmojiIndex: 100);

            // Act
            DeliverChatReaction("0xattacker", wireEmojiIndex: 4000, messageId: "msg1");
            DeliverChatReaction("0xattacker", wireEmojiIndex: 99, messageId: "msg1");

            // Assert
            Assert.That(received.Count, Is.EqualTo(1));
            Assert.That(received[0].EmojiIndex, Is.EqualTo(99));
        }

        [Test]
        public void RateLimitChatReactionFloodFromOneSender()
        {
            // Arrange
            CreateBus();

            // Act — one sender floods 50 distinct reactions (distinct dedup keys) in one frame
            for (int emoji = 0; emoji < 50; emoji++)
                DeliverChatReaction("0xattacker", wireEmojiIndex: emoji, messageId: "msg1");

            // Assert — only the burst allowance passes; the rest is dropped
            Assert.That(received.Count, Is.EqualTo(8));
        }

        [Test]
        public void RateLimitChatReactionToggleFloodFromOneSender()
        {
            // Arrange
            CreateBus();

            // Act — one sender rapidly toggles add/remove on the SAME message + emoji. Each
            // accepted packet evicts the opposite toggle's dedup key, so add/remove/add/...
            // always passes dedup — only the rate limiter can stop this flood.
            for (int i = 0; i < 40; i++)
            {
                int wireEmojiIndex = i % 2 == 0 ? 1 : ~1;
                DeliverChatReaction("0xattacker", wireEmojiIndex: wireEmojiIndex, messageId: "msg1");
            }

            // Assert — only the burst allowance passes; the rest is dropped
            Assert.That(received.Count, Is.EqualTo(8));
        }

        [Test]
        public void NotRateLimitModestTrafficFromDistinctSenders()
        {
            // Arrange
            CreateBus();

            // Act — three senders each add four reactions
            for (int sender = 0; sender < 3; sender++)
            for (int emoji = 0; emoji < 4; emoji++)
                DeliverChatReaction($"0xwallet{sender}", wireEmojiIndex: emoji, messageId: "msg1");

            // Assert
            Assert.That(received.Count, Is.EqualTo(12));
        }

        [Test]
        public void RateLimitSituationalFloodFromOneSender()
        {
            // Arrange
            CreateBus();

            // Act — distinct timestamps defeat dedup, so only the rate limiter can stop this
            for (int i = 0; i < 60; i++)
                DeliverSituational("0xattacker", emojiIndex: 1, count: 1, timestamp: i + 1f);

            // Assert — only the burst allowance passes
            Assert.That(received.Count, Is.EqualTo(20));
        }

        [Test]
        public void DeduplicateSameChatReactionAcrossPipes()
        {
            // Arrange
            CreateBus();

            // Act — the same packet arrives via both island and scene pipes
            DeliverChatReaction("0xsender", wireEmojiIndex: 1, messageId: "msg1");
            DeliverChatReaction("0xsender", wireEmojiIndex: 1, messageId: "msg1", pipe: pipesHub.Scene);

            // Assert
            Assert.That(received.Count, Is.EqualTo(1));
        }

        // ── Test helpers ─────────────────────────────────────────

        private void CreateBus(int maxValidEmojiIndex = 4096)
        {
            var newBus = new MultiplayerReactionMessageBus(
                pipesHub,
                Substitute.For<IUserBlockingCache>(),
                Substitute.For<IWeb3IdentityCache>(),
                routingUser: "message-router-test-0",
                config: config,
                maxValidEmojiIndex: maxValidEmojiIndex);

            newBus.ReactionReceived += args => received.Add(args);
            bus = newBus;
        }

        private void DeliverSituational(string fromWallet, int emojiIndex, int count, float timestamp)
        {
            var payload = new Reaction { EmojiIndex = emojiIndex, Count = count, Timestamp = timestamp };
            pipesHub.Island.Deliver(Packet.MessageOneofCase.Reaction,
                new ReceivedMessage<Reaction>(payload, new Packet(), fromWallet, multiPool, RoomSource.ISLAND, string.Empty));
        }

        private void DeliverChatReaction(string fromWallet, int wireEmojiIndex, string messageId, FakeMessagePipe? pipe = null)
        {
            var payload = new ChatReaction { EmojiIndex = wireEmojiIndex, MessageId = messageId, Address = string.Empty };
            (pipe ?? pipesHub.Island).Deliver(Packet.MessageOneofCase.ChatReaction,
                new ReceivedMessage<ChatReaction>(payload, new Packet(), fromWallet, multiPool, RoomSource.ISLAND, string.Empty));
        }

        private sealed class FakeMessagePipesHub : IMessagePipesHub
        {
            public readonly FakeMessagePipe Island = new ();
            public readonly FakeMessagePipe Scene = new ();
            public readonly FakeMessagePipe Chat = new ();

            public IMessagePipe ScenePipe() => Scene;

            public IMessagePipe IslandPipe() => Island;

            public IMessagePipe ChatPipe() => Chat;

            public void Dispose() { }
        }

        internal sealed class FakeMessagePipe : IMessagePipe
        {
            private readonly Dictionary<Packet.MessageOneofCase, object> handlers = new ();

            public MessageWrap<T> NewMessage<T>(string topic = "") where T: class, IMessage, new() =>
                throw new NotSupportedException("Receive-only fake");

            public void Subscribe<T>(Packet.MessageOneofCase ofCase, Action<ReceivedMessage<T>> onMessageReceived,
                IMessagePipe.ThreadStrict threadStrict = IMessagePipe.ThreadStrict.MAIN_THREAD_ONLY) where T: class, IMessage, new() =>
                handlers[ofCase] = onMessageReceived;

            public void Deliver<T>(Packet.MessageOneofCase ofCase, ReceivedMessage<T> message) where T: class, IMessage, new()
            {
                if (handlers.TryGetValue(ofCase, out object? handler))
                    ((Action<ReceivedMessage<T>>)handler).Invoke(message);
            }

            public void Dispose() { }
        }
    }
}
