using Arch.Core;
using DCL.Landscape.Settings;
using DCL.Multiplayer.Connections.Pulse;
using DCL.Multiplayer.Connections.Rooms;
using DCL.Multiplayer.Emotes;
using DCL.Multiplayer.Profiles.Announcements;
using DCL.Multiplayer.Profiles.Bunches;
using DCL.Multiplayer.Profiles.RemoveIntentions;
using DCL.Multiplayer.Profiles.Tables;
using DCL.Profiles.Self;
using DCL.Web3;
using DCL.Web3.Identities;
using Decentraland.Pulse;
using ECS;
using Google.Protobuf;
using NSubstitute;
using NUnit.Framework;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.TestTools;
using Utility.PriorityQueue;

namespace DCL.Multiplayer.Movement.Tests
{
    [TestFixture]
    public class PulseMultiplayerBusRealmFilteringShould
    {
        private const string REALM_A = "realm-a";
        private const string REALM_B = "realm-b";
        private const string WALLET_1 = "0x0000000000000000000000000000000000000001";
        private const string WALLET_2 = "0x0000000000000000000000000000000000000002";

        private static LandscapeData landscapeData;

        private static LandscapeData LandscapeData
        {
            get
            {
                if (landscapeData == null)
                {
                    string[] guids = AssetDatabase.FindAssets("t:LandscapeData");
                    landscapeData = AssetDatabase.LoadAssetAtPath<LandscapeData>(AssetDatabase.GUIDToAssetPath(guids[0]));
                }

                return landscapeData;
            }
        }

        private IPulseMultiplayerService pulseService;
        private PeerIdCache peerIdCache;
        private IReadOnlyEntityParticipantTable participantTable;
        private MovementInbox movementInbox;
        private PulseIncomingProfileAnnouncements incomingProfiles;
        private PulseRemoveIntentions removeIntentions;
        private IRealmData realmData;
        private PulseMultiplayerBus bus;
        private Dictionary<ServerMessage.MessageOneofCase, IPulseMultiplayerService.IncomingMessageHandler> handlers;
        private string currentRealm;
        private World world;

        [SetUp]
        public void SetUp()
        {
            // Filtered/mismatched messages log through ReportHub - not a failure condition here
            LogAssert.ignoreFailingMessages = true;

            currentRealm = REALM_A;

            handlers = new Dictionary<ServerMessage.MessageOneofCase, IPulseMultiplayerService.IncomingMessageHandler>();
            pulseService = Substitute.For<IPulseMultiplayerService>();

            pulseService.When(s => s.RegisterSyncHandler(Arg.Any<ServerMessage.MessageOneofCase>(), Arg.Any<IPulseMultiplayerService.IncomingMessageHandler>()))
                        .Do(ci => handlers[ci.ArgAt<ServerMessage.MessageOneofCase>(0)] = ci.ArgAt<IPulseMultiplayerService.IncomingMessageHandler>(1));

            participantTable = Substitute.For<IReadOnlyEntityParticipantTable>();
            world = World.Create();
            movementInbox = new MovementInbox(participantTable, world);

            peerIdCache = new PeerIdCache();
            incomingProfiles = new PulseIncomingProfileAnnouncements();
            removeIntentions = new PulseRemoveIntentions();

            realmData = Substitute.For<IRealmData>();
            realmData.RealmName.Returns(_ => currentRealm);

            bus = new PulseMultiplayerBus(pulseService, peerIdCache, movementInbox,
                new ParcelEncoder(LandscapeData.terrainData), incomingProfiles, removeIntentions,
                Substitute.For<IWeb3IdentityCache>(), new PulseMultiplayerBus.ReconnectionSettings(),
                Substitute.For<ISelfProfile>(), realmData);

            bus.SubscribeToIncomingMessages();
        }

        [TearDown]
        public void TearDown()
        {
            LogAssert.ignoreFailingMessages = false;
            bus.Dispose();
            world.Dispose();
        }

        [Test]
        public void AcceptJoinFromCurrentRealm()
        {
            Handle(PlayerJoinedMessage(7, WALLET_1, REALM_A));

            Assert.IsTrue(peerIdCache.TryGetWallet(7, out Web3Address wallet));
            Assert.IsTrue(wallet.Equals(WALLET_1));
            Assert.AreEqual(1, DrainAnnouncements().Count);
        }

        [Test]
        public void DropJoinFromDifferentRealm()
        {
            Handle(PlayerJoinedMessage(7, WALLET_1, REALM_B));

            Assert.IsFalse(peerIdCache.TryGetWallet(7, out _));
            Assert.AreEqual(0, DrainAnnouncements().Count);
        }

        [Test]
        public void DropJoinWithEmptyRealm()
        {
            Handle(PlayerJoinedMessage(7, WALLET_1, string.Empty));

            Assert.IsFalse(peerIdCache.TryGetWallet(7, out _));
            Assert.AreEqual(0, DrainAnnouncements().Count);
        }

        [Test]
        public void DropSubjectKeyedMessagesAfterRealmFlip()
        {
            Handle(PlayerJoinedMessage(7, WALLET_1, REALM_A));
            DrainAnnouncements();

            (Entity _, RemotePlayerMovementComponent component) = RegisterEntity(WALLET_1);
            movementInbox.DrainToEntities();
            Assert.AreEqual(1, component.Queue!.Count);

            currentRealm = REALM_B;

            Handle(ProfileAnnouncementMessage(7, 5));
            Assert.AreEqual(0, DrainAnnouncements().Count);

            Handle(PlayerStateFullMessage(7, sequence: 2));
            movementInbox.DrainToEntities();
            Assert.AreEqual(1, component.Queue!.Count);
        }

        [Test]
        public void PurgeDifferentRealmPeersOnTeleport()
        {
            Handle(PlayerJoinedMessage(7, WALLET_1, REALM_A));
            Handle(PlayerJoinedMessage(8, WALLET_2, REALM_A));
            Handle(EmoteStartedMessage(7));

            // Park both movements as pending (no entities registered yet)
            movementInbox.DrainToEntities();

            currentRealm = REALM_B;
            bus.BroadcastTeleport(Vector3.zero);

            using (OwnedBunch<RemoveIntention> bunch = removeIntentions.Bunch())
            {
                CollectionAssert.AreEquivalent(
                    new[] { new RemoveIntention(WALLET_1, RoomSource.PULSE), new RemoveIntention(WALLET_2, RoomSource.PULSE) },
                    bunch.Collection());
            }

            Assert.AreEqual(0, DrainAnnouncements().Count);

            using (OwnedBunch<RemoteEmoteIntention> emoteBunch = bus.EmoteIntentions())
                Assert.IsFalse(emoteBunch.Available());

            (Entity _, RemotePlayerMovementComponent component) = RegisterEntity(WALLET_1);
            movementInbox.TryFlushPending(WALLET_1);
            Assert.AreEqual(0, component.Queue!.Count);
        }

        [Test]
        public void NotPurgeOnIntraRealmTeleport()
        {
            Handle(PlayerJoinedMessage(7, WALLET_1, REALM_A));

            bus.BroadcastTeleport(Vector3.zero);

            using (OwnedBunch<RemoveIntention> bunch = removeIntentions.Bunch())
                Assert.IsFalse(bunch.Available());

            Assert.IsTrue(peerIdCache.TryGetWallet(7, out _));
            Assert.AreEqual(1, DrainAnnouncements().Count);
        }

        [Test]
        public void DrainRoutingPurgeBeforeHandlingNextMessage()
        {
            Handle(PlayerJoinedMessage(7, WALLET_1, REALM_A));
            Handle(EmoteStartedMessage(7));
            Assert.IsTrue(bus.IsPeerEmoting(new Web3Address(WALLET_1)));

            currentRealm = REALM_B;
            bus.BroadcastTeleport(Vector3.zero);

            // A reused subject id must not inherit the previous owner's emoting state
            Handle(PlayerJoinedMessage(7, WALLET_2, REALM_B));

            Assert.IsTrue(peerIdCache.TryGetWallet(7, out Web3Address wallet));
            Assert.IsTrue(wallet.Equals(WALLET_2));
            Assert.IsFalse(bus.IsPeerEmoting(new Web3Address(WALLET_2)));
        }

        [Test]
        public void ProcessPlayerLeftForStaleRealmPeer()
        {
            Handle(PlayerJoinedMessage(7, WALLET_1, REALM_A));

            currentRealm = REALM_B;

            Handle(new ServerMessage { PlayerLeft = new PlayerLeft { SubjectId = 7 } });

            using (OwnedBunch<RemoveIntention> bunch = removeIntentions.Bunch())
                CollectionAssert.AreEquivalent(new[] { new RemoveIntention(WALLET_1, RoomSource.PULSE) }, bunch.Collection());

            Assert.IsFalse(peerIdCache.TryGetWallet(7, out _));
        }

        private void Handle(ServerMessage serverMessage)
        {
            byte[] bytes = serverMessage.ToByteArray();
            Assert.IsTrue(IncomingMessage.TryCreate(new PeerId(1), bytes, out IncomingMessage incoming));

            using (incoming)
                handlers[serverMessage.MessageCase](incoming);
        }

        private List<RemoteAnnouncement> DrainAnnouncements()
        {
            var list = new List<RemoteAnnouncement>();
            incomingProfiles.Fill(list);
            return list;
        }

        private (Entity entity, RemotePlayerMovementComponent component) RegisterEntity(string wallet)
        {
            var queuePool = new ObjectPool<SimplePriorityQueue<NetworkMovementMessage, double>>(
                () => new SimplePriorityQueue<NetworkMovementMessage, double>());

            var component = new RemotePlayerMovementComponent(queuePool);
            Entity entity = world.Create(component);

            participantTable.TryGet(wallet, out Arg.Any<IReadOnlyEntityParticipantTable.Entry>())
                            .Returns(ci =>
                             {
                                 ci[1] = new IReadOnlyEntityParticipantTable.Entry(wallet, entity, RoomSource.PULSE);
                                 return true;
                             });

            return (entity, component);
        }

        private static ServerMessage PlayerJoinedMessage(uint subjectId, string wallet, string realm) =>
            new ()
            {
                PlayerJoined = new PlayerJoined
                {
                    UserId = wallet,
                    ProfileVersion = 1,
                    Realm = realm,
                    State = new PlayerStateFull
                    {
                        SubjectId = subjectId,
                        Sequence = 1,
                        ServerTick = 1,
                        State = new PlayerState(),
                    },
                },
            };

        private static ServerMessage PlayerStateFullMessage(uint subjectId, uint sequence) =>
            new ()
            {
                PlayerStateFull = new PlayerStateFull
                {
                    SubjectId = subjectId,
                    Sequence = sequence,
                    ServerTick = 2,
                    State = new PlayerState(),
                },
            };

        private static ServerMessage ProfileAnnouncementMessage(uint subjectId, int version) =>
            new ()
            {
                PlayerProfileVersionAnnounced = new PlayerProfileVersionsAnnounced
                {
                    SubjectId = subjectId,
                    Version = version,
                },
            };

        private static ServerMessage EmoteStartedMessage(uint subjectId) =>
            new ()
            {
                EmoteStarted = new EmoteStarted
                {
                    SubjectId = subjectId,
                    Sequence = 2,
                    ServerTick = 2,
                    EmoteId = "urn:decentraland:off-chain:base-emotes:wave",
                    PlayerState = new PlayerState(),
                },
            };
    }
}
