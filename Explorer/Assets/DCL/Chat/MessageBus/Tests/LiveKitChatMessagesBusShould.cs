using DCL.Chat.History;
using DCL.Communities;
using DCL.FeatureFlags;
using DCL.Friends.UserBlocking;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Multiplayer.Connections.Messaging;
using DCL.Multiplayer.Connections.Messaging.Hubs;
using DCL.Multiplayer.Connections.Messaging.Pipe;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Multiplayer.Connections.Rooms;
using DCL.Profiles;
using DCL.SceneBannedUsers;
using DCL.Web3.Identities;
using Decentraland.Kernel.Comms.Rfc4;
using Global.AppArgs;
using Google.Protobuf;
using LiveKit.Internal.FFIClients.Pools;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Threading;
using ChatMessage = DCL.Chat.History.ChatMessage;
using ChatPacket = Decentraland.Kernel.Comms.Rfc4.Chat;

namespace DCL.Chat.MessageBus.Tests
{
    /// <summary>
    /// Regression coverage for SEC-029: <c>Chat.ForwardedFrom</c> is a server-only stamp, so it must
    /// only be honored when the authenticated LiveKit sender is the trusted message router. Otherwise
    /// any co-located peer can impersonate a wallet and rotate the value to defeat ban, block, dedup
    /// and rate-limiting, all of which key on the resolved sender.
    /// </summary>
    [TestFixture]
    public class LiveKitChatMessagesBusShould
    {
        private const string ROUTING_USER = "message-router-dev-0";
        private const string PEER_WALLET = "0x1111111111111111111111111111111111111111";
        private const string VICTIM_WALLET = "0x2222222222222222222222222222222222222222";
        private const string COMMUNITY_TOPIC = "community:1234";

        private FakeMessagePipesHub pipesHub = null!;
        private IMultiPool multiPool = null!;
        private IUserBlockingCache userBlockingCache = null!;
        private IWeb3IdentityCache identityCache = null!;
        private List<ChatMessage> received = null!;
        private LiveKitChatMessagesBus? bus;

        [SetUp]
        public void SetUp()
        {
            FeatureFlagsConfiguration.Reset();
            OfficialWalletsHelper.Reset();
            FeaturesRegistry.Reset();
            CommunitiesFeatureAccess.Reset();
            RoomMetadataCurrentScene.Reset();

            IAppArgs appArgs = Substitute.For<IAppArgs>();
            identityCache = Substitute.For<IWeb3IdentityCache>();

            FeatureFlagsConfiguration.Initialize(new FeatureFlagsConfiguration(FeatureFlagsResultDto.Empty));
            OfficialWalletsHelper.Initialize(new OfficialWalletsHelper());
            FeaturesRegistry.Initialize(new FeaturesRegistry(appArgs, false));
            CommunitiesFeatureAccess.Initialize(new CommunitiesFeatureAccess(identityCache, appArgs, CancellationToken.None));
            RoomMetadataCurrentScene.InitializeTest();

            pipesHub = new FakeMessagePipesHub();
            multiPool = Substitute.For<IMultiPool>();
            userBlockingCache = Substitute.For<IUserBlockingCache>();
            received = new List<ChatMessage>();
        }

        [TearDown]
        public void TearDown()
        {
            bus?.Dispose();
            bus = null;

            RoomMetadataCurrentScene.Reset();
            CommunitiesFeatureAccess.Reset();
            FeaturesRegistry.Reset();
            OfficialWalletsHelper.Reset();
            FeatureFlagsConfiguration.Reset();
        }

        [Test]
        public void AttributeNearbyMessageToAuthenticatedSenderIgnoringForwardedFrom()
        {
            // Arrange — Island and Scene are peer-to-peer pipes, so no router is ever in their path.
            CreateBus();

            // Act — a peer forges the author of its own nearby message.
            DeliverNearby(fromWallet: PEER_WALLET, forwardedFrom: VICTIM_WALLET, timestamp: 1d);

            // Assert
            Assert.That(received.Count, Is.EqualTo(1));
            Assert.That(received[0].SenderWalletAddress, Is.EqualTo(PEER_WALLET));
        }

        [Test]
        public void IgnoreForwardedFromWhenChatPipeSenderIsNotTheMessageRouter()
        {
            // Arrange
            CreateBus();

            // Act — the same forgery on the pipe that does have a legitimate router path.
            DeliverOnChatPipe(fromWallet: PEER_WALLET, forwardedFrom: VICTIM_WALLET, timestamp: 1d);

            // Assert
            Assert.That(received.Count, Is.EqualTo(1));
            Assert.That(received[0].SenderWalletAddress, Is.EqualTo(PEER_WALLET));
        }

        [Test]
        public void HonorForwardedFromWhenChatPipeSenderIsTheMessageRouter()
        {
            // Arrange — a genuinely relayed community message arrives AS the router participant.
            // Requires the Communities shape to be included, which it is in the Editor.
            CreateBus();

            // Act
            DeliverOnChatPipe(fromWallet: ROUTING_USER, forwardedFrom: VICTIM_WALLET, timestamp: 1d);

            // Assert — real forwarding still resolves to the original sender.
            Assert.That(received.Count, Is.EqualTo(1));
            Assert.That(received[0].SenderWalletAddress, Is.EqualTo(VICTIM_WALLET));
        }

        [Test]
        public void FallBackToRouterIdentityWhenForwardedFromIsMalformed()
        {
            // Arrange
            CreateBus();

            // Act — a malformed or rogue value from the router itself.
            DeliverOnChatPipe(fromWallet: ROUTING_USER, forwardedFrom: "not-a-wallet-address", timestamp: 1d);

            // Assert
            Assert.That(received.Count, Is.EqualTo(1));
            Assert.That(received[0].SenderWalletAddress, Is.EqualTo(ROUTING_USER));
        }

        [Test]
        public void NotGrantFreshDedupSlotsWhenNearbyForwardedFromRotates()
        {
            // Arrange — dedup keys on (sender, timestamp), so a rotating author used to hand one peer
            // an unlimited supply of unused keys at a single timestamp.
            CreateBus();

            // Act
            DeliverNearby(PEER_WALLET, forwardedFrom: VICTIM_WALLET, timestamp: 1d);
            DeliverNearby(PEER_WALLET, forwardedFrom: "0x3333333333333333333333333333333333333333", timestamp: 1d);
            DeliverNearby(PEER_WALLET, forwardedFrom: "0x4444444444444444444444444444444444444444", timestamp: 1d);

            // Assert — all three collapse onto the one authenticated wallet.
            Assert.That(received.Count, Is.EqualTo(1));
        }

        [Test]
        public void DeduplicateAcrossNearbyPipesWhenForwardedFromDiffers()
        {
            // Arrange — the same message reaches the client over both nearby pipes.
            CreateBus();

            // Act — a forged author on the second copy used to defeat the cross-pipe dedup.
            DeliverNearby(PEER_WALLET, forwardedFrom: null, timestamp: 1d);
            Deliver(pipesHub.Scene, PEER_WALLET, VICTIM_WALLET, 1d, RoomSource.Gatekeeper, string.Empty);

            // Assert
            Assert.That(received.Count, Is.EqualTo(1));
        }

        [Test]
        public void KeepNearbyMessageOfBlockedPeerHiddenWhenForwardedFromRotates()
        {
            // Arrange — the local user blocks the peer and hides its messages.
            userBlockingCache.HideChatMessages.Returns(true);
            userBlockingCache.UserIsBlocked(PEER_WALLET).Returns(true);
            CreateBus();

            // Act — the blocked peer claims to be somebody the local user has not blocked.
            DeliverNearby(PEER_WALLET, forwardedFrom: VICTIM_WALLET, timestamp: 1d);

            // Assert
            Assert.That(received, Is.Empty);
        }

        [Test]
        public void AttributeNearbyMessageToAuthenticatedSenderWhenForwardedFromIsAbsent()
        {
            // Arrange
            CreateBus();

            // Act
            DeliverNearby(PEER_WALLET, forwardedFrom: null, timestamp: 1d);

            // Assert
            Assert.That(received.Count, Is.EqualTo(1));
            Assert.That(received[0].SenderWalletAddress, Is.EqualTo(PEER_WALLET));
        }

        // ── Test helpers ─────────────────────────────────────────

        private void CreateBus()
        {
            var messageFactory = new ChatMessageFactory(Substitute.For<IProfileCache>(), identityCache);

            // Zone resolves the routing user to "message-router-dev-0".
            var newBus = new LiveKitChatMessagesBus(pipesHub,
                messageFactory,
                userBlockingCache,
                DecentralandEnvironment.Zone,
                identityCache,
                Substitute.For<IRoomHub>());

            newBus.MessageAdded += (_, _, message) => received.Add(message);
            bus = newBus;
        }

        private void DeliverNearby(string fromWallet, string? forwardedFrom, double timestamp) =>
            Deliver(pipesHub.Island, fromWallet, forwardedFrom, timestamp, RoomSource.Island, string.Empty);

        private void DeliverOnChatPipe(string fromWallet, string? forwardedFrom, double timestamp) =>
            Deliver(pipesHub.Chat, fromWallet, forwardedFrom, timestamp, RoomSource.Island, COMMUNITY_TOPIC);

        private void Deliver(FakeMessagePipe pipe, string fromWallet, string? forwardedFrom, double timestamp,
            RoomSource roomSource, string topic)
        {
            var payload = new ChatPacket { Message = "hello", Timestamp = timestamp };

            if (forwardedFrom != null)
                payload.ForwardedFrom = forwardedFrom;

            pipe.Deliver(Packet.MessageOneofCase.Chat,
                new ReceivedMessage<ChatPacket>(payload, new Packet(), fromWallet, multiPool, roomSource, topic));
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

        private sealed class FakeMessagePipe : IMessagePipe
        {
            private readonly Dictionary<Packet.MessageOneofCase, object> handlers = new ();

            public MessageWrap<T> NewMessage<T>(string topic = "") where T: class, IMessage, new() =>
                throw new NotSupportedException("Receive-only fake");

            public void Subscribe<T>(Packet.MessageOneofCase ofCase, Action<ReceivedMessage<T>> onMessageReceived,
                IMessagePipe.ThreadStrict threadStrict = IMessagePipe.ThreadStrict.MainThreadOnly) where T: class, IMessage, new() =>
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
