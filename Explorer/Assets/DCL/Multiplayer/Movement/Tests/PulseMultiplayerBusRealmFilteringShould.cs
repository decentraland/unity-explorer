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
using System;
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
        private Action beforeMessage;
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

            pulseService.When(s => s.RegisterBeforeMessageHandler(Arg.Any<Action>()))
                        .Do(ci => beforeMessage = ci.ArgAt<Action>(0));

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

            // Baseline broadcast: the purge triggers only when the realm differs from the previous broadcast
            bus.BroadcastTeleport(Vector3.zero);

            currentRealm = REALM_B;
            bus.BroadcastTeleport(Vector3.zero);

            using (OwnedBunch<RemoveIntention> bunch = removeIntentions.Bunch())
            {
                CollectionAssert.AreEquivalent(
                    new[] { new RemoveIntention(WALLET_1, RoomSource.Pulse), new RemoveIntention(WALLET_2, RoomSource.Pulse) },
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
            bus.BroadcastTeleport(Vector3.zero);

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

            bus.BroadcastTeleport(Vector3.zero);

            currentRealm = REALM_B;
            bus.BroadcastTeleport(Vector3.zero);

            // A reused subject id must not inherit the previous owner's emoting state
            Handle(PlayerJoinedMessage(7, WALLET_2, REALM_B));

            Assert.IsTrue(peerIdCache.TryGetWallet(7, out Web3Address wallet));
            Assert.IsTrue(wallet.Equals(WALLET_2));
            Assert.IsFalse(bus.IsPeerEmoting(new Web3Address(WALLET_2)));
        }

        [Test]
        public void RemovePeerOnTeleportToDifferentRealm()
        {
            Handle(PlayerJoinedMessage(7, WALLET_1, REALM_A));
            DrainAnnouncements();

            // No PlayerLeft is issued for a peer that changes realms within the same tick range
            Handle(TeleportMessage(7, REALM_B));

            Assert.IsFalse(peerIdCache.TryGetWallet(7, out _));

            using (OwnedBunch<RemoveIntention> bunch = removeIntentions.Bunch())
                CollectionAssert.AreEquivalent(new[] { new RemoveIntention(WALLET_1, RoomSource.Pulse) }, bunch.Collection());
        }

        [Test]
        public void KeepPeerAnnouncingDestinationRealmViaTeleport()
        {
            Handle(PlayerJoinedMessage(7, WALLET_1, REALM_A));
            DrainAnnouncements();

            bus.BroadcastTeleport(Vector3.zero);

            currentRealm = REALM_B;

            // A co-teleporting peer is not re-announced; its TeleportPerformed lands before the local broadcast
            Handle(TeleportMessage(7, REALM_B));

            bus.BroadcastTeleport(Vector3.zero);

            using (OwnedBunch<RemoveIntention> bunch = removeIntentions.Bunch())
                Assert.IsFalse(bunch.Available());

            Assert.IsTrue(peerIdCache.TryGetWallet(7, out Web3Address wallet));
            Assert.IsTrue(wallet.Equals(WALLET_1));

            // The deferred routing purge must keep the peer too
            Handle(PlayerStateFullMessage(7, sequence: 3));
            Assert.IsTrue(peerIdCache.TryGetWallet(7, out _));
        }

        [Test]
        public void DropTeleportWithEmptyRealm()
        {
            Handle(PlayerJoinedMessage(7, WALLET_1, REALM_A));

            Handle(TeleportMessage(7, string.Empty));

            Assert.IsTrue(peerIdCache.TryGetWallet(7, out _));

            using (OwnedBunch<RemoveIntention> bunch = removeIntentions.Bunch())
                Assert.IsFalse(bunch.Available());
        }

        [Test]
        public void ProcessPlayerLeftForStaleRealmPeer()
        {
            Handle(PlayerJoinedMessage(7, WALLET_1, REALM_A));

            currentRealm = REALM_B;

            Handle(new ServerMessage { PlayerLeft = new PlayerLeft { SubjectId = 7 } });

            using (OwnedBunch<RemoveIntention> bunch = removeIntentions.Bunch())
                CollectionAssert.AreEquivalent(new[] { new RemoveIntention(WALLET_1, RoomSource.Pulse) }, bunch.Collection());

            Assert.IsFalse(peerIdCache.TryGetWallet(7, out _));
        }

        // Regression coverage for unity-explorer#9337 (join-epoch guard, potential-fix.patch site 3+4):
        // a PlayerLeft for a superseded session (a wallet that already re-joined under a new subject id)
        // must not delete the freshly re-joined avatar. At pin, HandlePlayerLeft enqueues the remove
        // unconditionally from the dangling forward entry, and PeerIdCache.Remove(7) then deletes the
        // *live* wallet->peerId reverse mapping too (peersByWallet[7] still resolves to the wallet even
        // though walletsByPeerId[wallet] already points at 9) - exactly the "re-join cancels the stale
        // pending leave" gap the report's [INVISIBLE_AVATAR] diagnosis (03b82789c) named.
        [Test]
        public void IgnoreStalePlayerLeftForSupersededSession()
        {
            Handle(PlayerJoinedMessage(7, WALLET_1, REALM_A));
            DrainAnnouncements();

            // Wallet re-joins under a new subject id (e.g. a reconnect burst) before the old session's
            // leave is processed; the routing thread always serializes these messages in arrival order.
            Handle(PlayerJoinedMessage(9, WALLET_1, REALM_A));
            DrainAnnouncements();

            // Late/re-ordered leave for the superseded session (subject id 7).
            Handle(new ServerMessage { PlayerLeft = new PlayerLeft { SubjectId = 7 } });

            using (OwnedBunch<RemoveIntention> bunch = removeIntentions.Bunch())
                Assert.IsFalse(bunch.Available(),
                    "A PlayerLeft for a superseded session must not delete the peer's live re-joined avatar.");

            Assert.IsTrue(peerIdCache.TryGetWallet(9, out Web3Address wallet));
            Assert.IsTrue(wallet.Equals(WALLET_1));

            Assert.IsTrue(peerIdCache.TryGetPeerId(new Web3Address(WALLET_1), out uint currentPeerId),
                "The re-join's reverse mapping (wallet -> current peer id) must survive the stale leave; " +
                "at pin, PeerIdCache.Remove(7) deletes the *live* session's wallet->peerId entry too.");
            Assert.AreEqual(9u, currentPeerId);
        }

        // Companion to IgnoreStalePlayerLeftForSupersededSession: the guard must only reject leaves for
        // superseded sessions, not swallow every future leave for a wallet that has ever re-joined.
        [Test]
        public void ProcessPlayerLeftForCurrentSessionAfterRejoin()
        {
            Handle(PlayerJoinedMessage(7, WALLET_1, REALM_A));
            DrainAnnouncements();

            Handle(PlayerJoinedMessage(9, WALLET_1, REALM_A));
            DrainAnnouncements();

            // Stale leave for the superseded session - ignored (see IgnoreStalePlayerLeftForSupersededSession).
            Handle(new ServerMessage { PlayerLeft = new PlayerLeft { SubjectId = 7 } });

            // The eventual leave for the CURRENT session (9) must still be processed normally.
            Handle(new ServerMessage { PlayerLeft = new PlayerLeft { SubjectId = 9 } });

            using (OwnedBunch<RemoveIntention> bunch = removeIntentions.Bunch())
                CollectionAssert.AreEquivalent(new[] { new RemoveIntention(WALLET_1, RoomSource.Pulse) }, bunch.Collection());

            Assert.IsFalse(peerIdCache.TryGetWallet(9, out _));
        }

        private void Handle(ServerMessage serverMessage)
        {
            byte[] bytes = serverMessage.ToByteArray();
            Assert.IsTrue(IncomingMessage.TryCreate(new PeerId(1), bytes, out IncomingMessage incoming));

            using (incoming)
            {
                // Mirrors the service routing loop: the before-message hook runs ahead of every handler
                beforeMessage();
                handlers[serverMessage.MessageCase](incoming);
            }
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
                                 ci[1] = new IReadOnlyEntityParticipantTable.Entry(wallet, entity, RoomSource.Pulse);
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

        private static ServerMessage TeleportMessage(uint subjectId, string realm) =>
            new ()
            {
                Teleported = new TeleportPerformed
                {
                    SubjectId = subjectId,
                    Sequence = 2,
                    ServerTick = 2,
                    Realm = realm,
                    State = new PlayerState(),
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
