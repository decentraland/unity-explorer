using Arch.Core;
using CommunicationData.URLHelpers;
using DCL.ECSComponents;
using DCL.Character.CharacterMotion.Components;
using DCL.Character.Components;
using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.Settings;
using DCL.Multiplayer.Movement.Settings;
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
using Pulse.Transport;
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

        private static LandscapeData? landscapeData;

        private static LandscapeData cachedLandscapeData
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

        private IPulseMultiplayerService pulseService = null!;
        private PeerIdCache peerIdCache = null!;
        private IReadOnlyEntityParticipantTable participantTable = null!;
        private MovementInbox movementInbox = null!;
        private PulseIncomingProfileAnnouncements incomingProfiles = null!;
        private PulseRemoveIntentions removeIntentions = null!;
        private IRealmData realmData = null!;
        private PulseMultiplayerBus bus = null!;
        private Dictionary<ServerMessage.MessageOneofCase, IPulseMultiplayerService.IncomingMessageHandler> handlers = null!;
        private Action beforeMessage = null!;
        private IPulseMultiplayerService.DisconnectHandler disconnect = null!;
        private string currentRealm = null!;
        private World world = null!;

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

            pulseService.When(s => s.RegisterDisconnectHandler(Arg.Any<IPulseMultiplayerService.DisconnectHandler>()))
                        .Do(ci => disconnect = ci.ArgAt<IPulseMultiplayerService.DisconnectHandler>(0));

            participantTable = Substitute.For<IReadOnlyEntityParticipantTable>();
            world = World.Create();
            movementInbox = new MovementInbox(participantTable, world);

            peerIdCache = new PeerIdCache();
            incomingProfiles = new PulseIncomingProfileAnnouncements();
            removeIntentions = new PulseRemoveIntentions();

            realmData = Substitute.For<IRealmData>();
            realmData.RealmName.Returns(_ => currentRealm);

            var identity = Substitute.For<IWeb3Identity>();
            identity.Address.Returns(new Web3Address(WALLET_1));
            var identityCache = Substitute.For<IWeb3IdentityCache>();
            identityCache.Identity.Returns(identity);

            bus = new PulseMultiplayerBus(pulseService, peerIdCache, movementInbox,
                new ParcelEncoder(cachedLandscapeData.terrainData), incomingProfiles, removeIntentions,
                identityCache, new PulseMultiplayerBus.ReconnectionSettings(),
                Substitute.For<SelfProfile>(), new PulseRealm(realmData));

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

        [TestCase(3u, true)]
        [TestCase(4u, true)]
        [TestCase(5u, true)]
        [TestCase(3u, false)]
        [TestCase(4u, false)]
        [TestCase(5u, false)]
        public void ApplyEmoteStopToNextDeltaWithoutRollingBackMovement(uint stopSequence, bool includePlayerState)
        {
            (Entity _, RemotePlayerMovementComponent component) = RegisterEntity(WALLET_1);
            Handle(PlayerJoinedMessage(7, WALLET_1, REALM_A));
            Handle(EmoteStartedMessage(7));

            ServerMessage teleport = TeleportMessage(7, REALM_A);
            teleport.Teleported.Sequence = 4;
            teleport.Teleported.ServerTick = 4;
            teleport.Teleported.State.PositionYQuantized = 10;
            Handle(teleport);

            movementInbox.DrainToEntities();
            component.Queue!.Clear();
            Assert.IsTrue(bus.IsPeerEmoting(new Web3Address(WALLET_1)));

            Handle(new ServerMessage
            {
                EmoteStopped = new EmoteStopped
                {
                    SubjectId = 7,
                    Sequence = stopSequence,
                    ServerTick = stopSequence,
                    PlayerState = includePlayerState ? new PlayerState { PositionYQuantized = 2 } : null,
                },
            });

            uint baselineSequence = includePlayerState && stopSequence > 4 ? stopSequence : 4;
            Handle(new ServerMessage
            {
                PlayerStateDelta = new PlayerStateDeltaTier0
                {
                    SubjectId = 7,
                    BaselineSeq = baselineSequence,
                    NewSeq = baselineSequence + 1,
                    ServerTick = 6,
                    RotationYQuantized = 90,
                },
            });

            movementInbox.DrainToEntities();
            Assert.AreEqual(1, component.Queue.Count, "The next delta must still match the movement baseline");

            NetworkMovementMessage movement = component.Queue.Dequeue();
            Assert.IsFalse(movement.isEmoting, "A stop must clear the emote flag even when its pose is older");
            Assert.That(movement.position.y, Is.EqualTo(includePlayerState && stopSequence >= 4 ? 2 : 10).Within(PlayerState.PositionYQuantizedStep),
                "An older stop must preserve the newer pose; an equal or newer full state may replace it");
            Assert.That(movement.rotationY, Is.EqualTo(90).Within(PlayerState.RotationYQuantizedStep));
            Assert.IsFalse(bus.IsPeerEmoting(new Web3Address(WALLET_1)));

            using (OwnedBunch<RemoteEmoteStopIntention> bunch = bus.EmoteStopIntentions())
                Assert.AreEqual(1, bunch.Collection().Count);
        }

        [Test]
        public void KeepReplacementWhenTheSupersededSessionLeavesLater()
        {
            Handle(PlayerJoinedMessage(7, WALLET_1, REALM_A));
            Handle(PlayerJoinedMessage(8, WALLET_1, REALM_A));
            DrainAnnouncements();

            Handle(new ServerMessage { PlayerLeft = new PlayerLeft { SubjectId = 7 } });

            using (OwnedBunch<RemoveIntention> bunch = removeIntentions.Bunch())
                Assert.IsFalse(bunch.Available(), "The superseded session must not remove the current avatar");

            Assert.IsTrue(peerIdCache.TryGetPeerId(new Web3Address(WALLET_1), out uint peerId));
            Assert.AreEqual(8, peerId);
            Assert.IsFalse(peerIdCache.TryGetWallet(7, out _));
            Assert.IsTrue(peerIdCache.TryGetWallet(8, out _));
        }

        [Test]
        public void IgnoreSupersededSessionTeleportAfterReplacementJoins()
        {
            Handle(PlayerJoinedMessage(7, WALLET_1, REALM_A));
            Handle(PlayerJoinedMessage(8, WALLET_1, REALM_A));

            Handle(TeleportMessage(7, REALM_B));

            using (OwnedBunch<RemoveIntention> bunch = removeIntentions.Bunch())
                Assert.IsFalse(bunch.Available());

            Assert.IsTrue(peerIdCache.TryGetPeerId(new Web3Address(WALLET_1), out uint peerId));
            Assert.AreEqual(8, peerId);
        }

        [Test]
        public void RemoveReplacementWhenItsOwnSessionLeaves()
        {
            Handle(PlayerJoinedMessage(7, WALLET_1, REALM_A));
            Handle(PlayerJoinedMessage(8, WALLET_1, REALM_A));
            Handle(new ServerMessage { PlayerLeft = new PlayerLeft { SubjectId = 7 } });

            Handle(new ServerMessage { PlayerLeft = new PlayerLeft { SubjectId = 8 } });

            using (OwnedBunch<RemoveIntention> bunch = removeIntentions.Bunch())
                CollectionAssert.AreEquivalent(new[] { new RemoveIntention(WALLET_1, RoomSource.Pulse) }, bunch.Collection());

            Assert.IsFalse(peerIdCache.TryGetPeerId(new Web3Address(WALLET_1), out _));
            Assert.IsFalse(peerIdCache.TryGetWallet(8, out _));
        }

        [Test]
        public void CancelPendingRemovalWhenReplacementJoinsAfterTheLeave()
        {
            Handle(PlayerJoinedMessage(7, WALLET_1, REALM_A));
            Handle(new ServerMessage { PlayerLeft = new PlayerLeft { SubjectId = 7 } });

            Handle(PlayerJoinedMessage(8, WALLET_1, REALM_A));

            using (OwnedBunch<RemoveIntention> bunch = removeIntentions.Bunch())
                Assert.IsFalse(bunch.Available());

            Assert.IsTrue(peerIdCache.TryGetPeerId(new Web3Address(WALLET_1), out uint peerId));
            Assert.AreEqual(8, peerId);
        }

        [Test]
        public void RouteOnlyTheReplacementMovementAndProfile()
        {
            Handle(PlayerJoinedMessage(7, WALLET_1, REALM_A));
            Handle(PlayerJoinedMessage(8, WALLET_1, REALM_A));
            DrainAnnouncements();
            (Entity _, RemotePlayerMovementComponent component) = RegisterEntity(WALLET_1);
            movementInbox.DrainToEntities();
            int queuedMovements = component.Queue!.Count;

            Handle(PlayerStateFullMessage(7, sequence: 2));
            Handle(ProfileAnnouncementMessage(7, 5));
            movementInbox.DrainToEntities();

            Assert.AreEqual(queuedMovements, component.Queue.Count);
            Assert.AreEqual(0, DrainAnnouncements().Count);

            Handle(PlayerStateFullMessage(8, sequence: 2));
            Handle(ProfileAnnouncementMessage(8, 5));
            movementInbox.DrainToEntities();

            Assert.AreEqual(queuedMovements + 1, component.Queue.Count);
            Assert.AreEqual(1, DrainAnnouncements().Count);
        }

        [Test]
        public void IgnoreSupersededDeltaAndEmoteMessages()
        {
            Handle(PlayerJoinedMessage(7, WALLET_1, REALM_A));
            Handle(PlayerJoinedMessage(8, WALLET_1, REALM_A));
            using (OwnedBunch<RemoteEmoteStopIntention> bunch = bus.EmoteStopIntentions())
                Assert.IsTrue(bunch.Available());
            (Entity _, RemotePlayerMovementComponent component) = RegisterEntity(WALLET_1);
            movementInbox.DrainToEntities();
            int queuedMovements = component.Queue!.Count;

            Handle(PlayerStateDeltaMessage(7, 10, 11));
            Handle(EmoteStartedMessage(7));
            Handle(EmoteStoppedMessage(7));
            movementInbox.DrainToEntities();

            Assert.AreEqual(queuedMovements, component.Queue.Count);
            Assert.AreEqual(0, bus.resyncCount);
            Assert.IsFalse(bus.IsPeerEmoting(new Web3Address(WALLET_1)));
            using (OwnedBunch<RemoteEmoteIntention> bunch = bus.EmoteIntentions())
                Assert.IsFalse(bunch.Available());
            using (OwnedBunch<RemoteEmoteStopIntention> bunch = bus.EmoteStopIntentions())
                Assert.IsFalse(bunch.Available());

            Handle(EmoteStartedMessage(8));
            Handle(EmoteStoppedMessage(7));
            Assert.IsTrue(bus.IsPeerEmoting(new Web3Address(WALLET_1)));
            using (OwnedBunch<RemoteEmoteIntention> bunch = bus.EmoteIntentions())
                Assert.IsTrue(bunch.Available());
            using (OwnedBunch<RemoteEmoteStopIntention> bunch = bus.EmoteStopIntentions())
                Assert.IsFalse(bunch.Available());

            Handle(EmoteStoppedMessage(8));
            Assert.IsFalse(bus.IsPeerEmoting(new Web3Address(WALLET_1)));
            using (OwnedBunch<RemoteEmoteStopIntention> bunch = bus.EmoteStopIntentions())
                Assert.IsTrue(bunch.Available());
        }

        [Test]
        public void KeepReplacementResyncPendingWhenAnOldResponseArrives()
        {
            Handle(PlayerJoinedMessage(7, WALLET_1, REALM_A));
            Handle(PlayerStateDeltaMessage(7, 10, 11));
            Assert.AreEqual(1, bus.resyncCount);
            Handle(PlayerJoinedMessage(8, WALLET_1, REALM_A));
            Handle(PlayerStateDeltaMessage(8, 10, 11));
            Assert.AreEqual(2, bus.resyncCount);

            Handle(PlayerStateFullMessage(7, 10));
            Handle(PlayerStateDeltaMessage(8, 10, 11));
            Assert.AreEqual(2, bus.resyncCount);

            Handle(PlayerStateFullMessage(8, 10));
            Handle(PlayerStateDeltaMessage(8, 10, 11));
            Handle(PlayerStateDeltaMessage(8, 20, 21));
            Assert.AreEqual(3, bus.resyncCount);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void KeepLatestReplacementRegardlessOfSupersededLeaveOrder(bool reverseOrder)
        {
            Handle(PlayerJoinedMessage(7, WALLET_1, REALM_A));
            Handle(PlayerJoinedMessage(8, WALLET_1, REALM_A));
            Handle(PlayerJoinedMessage(9, WALLET_1, REALM_A));

            uint first = reverseOrder ? 8u : 7u;
            uint second = reverseOrder ? 7u : 8u;
            Handle(new ServerMessage { PlayerLeft = new PlayerLeft { SubjectId = first } });
            Handle(new ServerMessage { PlayerLeft = new PlayerLeft { SubjectId = second } });
            Handle(new ServerMessage { PlayerLeft = new PlayerLeft { SubjectId = first } });

            using (OwnedBunch<RemoveIntention> bunch = removeIntentions.Bunch())
                Assert.IsFalse(bunch.Available());
            Assert.IsTrue(peerIdCache.TryGetPeerId(new Web3Address(WALLET_1), out uint peerId));
            Assert.AreEqual(9, peerId);

            Handle(new ServerMessage { PlayerLeft = new PlayerLeft { SubjectId = 9 } });
            using (OwnedBunch<RemoveIntention> bunch = removeIntentions.Bunch())
                CollectionAssert.AreEquivalent(new[] { new RemoveIntention(WALLET_1, RoomSource.Pulse) }, bunch.Collection());
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TreatSelfMirrorAndTheConcreteWalletAsOverlappingSessions(bool mirrorJoinsLast)
        {
            string firstWallet = mirrorJoinsLast ? WALLET_1 : PulseMultiplayerBus.SELF_MIRROR_WALLET_ID;
            string secondWallet = mirrorJoinsLast ? PulseMultiplayerBus.SELF_MIRROR_WALLET_ID : WALLET_1;
            Handle(PlayerJoinedMessage(7, firstWallet, REALM_A));
            Handle(PlayerJoinedMessage(8, secondWallet, REALM_A));
            Handle(new ServerMessage { PlayerLeft = new PlayerLeft { SubjectId = 7 } });

            using (OwnedBunch<RemoveIntention> bunch = removeIntentions.Bunch())
                Assert.IsFalse(bunch.Available());
            Assert.IsFalse(peerIdCache.TryGetWallet(7, out _));
            Assert.IsTrue(peerIdCache.TryGetPeerId(new Web3Address(WALLET_1), out uint peerId));
            Assert.AreEqual(8, peerId);
        }

        [Test]
        public void KeepReplacementAfterDuplicateJoinsAndRepeatedOldLeaves()
        {
            Handle(PlayerJoinedMessage(7, WALLET_1, REALM_A));
            Handle(PlayerJoinedMessage(7, WALLET_1, REALM_A));
            Handle(PlayerJoinedMessage(8, WALLET_1, REALM_A));
            Handle(PlayerJoinedMessage(8, WALLET_1, REALM_A));
            Handle(new ServerMessage { PlayerLeft = new PlayerLeft { SubjectId = 7 } });
            Handle(new ServerMessage { PlayerLeft = new PlayerLeft { SubjectId = 7 } });

            using (OwnedBunch<RemoveIntention> bunch = removeIntentions.Bunch())
                Assert.IsFalse(bunch.Available());
            Assert.IsTrue(peerIdCache.TryGetPeerId(new Web3Address(WALLET_1), out uint peerId));
            Assert.AreEqual(8, peerId);
            Assert.IsFalse(peerIdCache.TryGetWallet(7, out _));
        }

        [TestCase(7u, 8u, 7u)]
        [TestCase(7u, 8u, 8u)]
        [TestCase(8u, 7u, 7u)]
        [TestCase(8u, 7u, 8u)]
        public void KeepTheSurvivorInEitherJoinAndLeaveOrder(uint firstJoin, uint secondJoin, uint firstLeave)
        {
            Handle(PlayerJoinedMessage(firstJoin, WALLET_1, REALM_A));
            Handle(PlayerJoinedMessage(secondJoin, WALLET_1, REALM_A));
            uint survivor = firstLeave == 7 ? 8u : 7u;

            Assert.IsTrue(peerIdCache.TryGetWallet(firstJoin, out _));
            Assert.IsTrue(peerIdCache.TryGetWallet(secondJoin, out _));
            Handle(new ServerMessage { PlayerLeft = new PlayerLeft { SubjectId = firstLeave } });

            using (OwnedBunch<RemoveIntention> bunch = removeIntentions.Bunch())
                Assert.IsFalse(bunch.Available());
            Assert.IsTrue(peerIdCache.TryGetPeerId(new Web3Address(WALLET_1), out uint peerId));
            Assert.AreEqual(survivor, peerId);
            Assert.AreEqual(PeerIdCache.LookupResult.Found, peerIdCache.GetWalletInRealm(survivor, REALM_A, out _));

            Handle(new ServerMessage { PlayerLeft = new PlayerLeft { SubjectId = survivor } });
            using (OwnedBunch<RemoveIntention> bunch = removeIntentions.Bunch())
                CollectionAssert.AreEquivalent(new[] { new RemoveIntention(WALLET_1, RoomSource.Pulse) }, bunch.Collection());
        }

        [Test]
        public void RestoreMovementAndResyncForTheSelectedSurvivor()
        {
            Handle(PlayerJoinedMessage(7, WALLET_1, REALM_A));
            Handle(PlayerStateDeltaMessage(7, 10, 11));
            Assert.AreEqual(1, bus.resyncCount);
            Handle(PlayerJoinedMessage(8, WALLET_1, REALM_A));
            (Entity _, RemotePlayerMovementComponent component) = RegisterEntity(WALLET_1);
            movementInbox.DrainToEntities();
            int queuedMovements = component.Queue!.Count;
            Handle(PlayerStateFullMessage(7, 10));
            movementInbox.DrainToEntities();
            Assert.AreEqual(queuedMovements, component.Queue.Count);

            Handle(new ServerMessage { PlayerLeft = new PlayerLeft { SubjectId = 8 } });
            Assert.AreEqual(2, bus.resyncCount);
            movementInbox.DrainToEntities();
            Assert.AreEqual(1, component.Queue.Count);
            Assert.IsTrue(component.Queue.First.resetTimeline);
            Handle(PlayerStateDeltaMessage(7, 10, 11));
            Assert.AreEqual(2, bus.resyncCount);
            Handle(PlayerStateFullMessage(7, 10));
            Handle(PlayerStateDeltaMessage(7, 10, 11));
            movementInbox.DrainToEntities();
            Assert.AreEqual(2, component.Queue.Count);

            Handle(PlayerStateDeltaMessage(7, 20, 21));
            Assert.AreEqual(3, bus.resyncCount);
            using (OwnedBunch<RemoveIntention> bunch = removeIntentions.Bunch())
                Assert.IsFalse(bunch.Available());
        }

        [Test]
        public void FallBackThroughThreeOverlappingSessions()
        {
            Handle(PlayerJoinedMessage(7, WALLET_1, REALM_A));
            Handle(PlayerJoinedMessage(8, WALLET_1, REALM_A));
            Handle(PlayerJoinedMessage(9, WALLET_1, REALM_A));
            Handle(new ServerMessage { PlayerLeft = new PlayerLeft { SubjectId = 9 } });
            Assert.IsTrue(peerIdCache.TryGetPeerId(new Web3Address(WALLET_1), out uint peerId));
            Assert.AreEqual(8, peerId);
            Handle(new ServerMessage { PlayerLeft = new PlayerLeft { SubjectId = 8 } });
            Assert.IsTrue(peerIdCache.TryGetPeerId(new Web3Address(WALLET_1), out peerId));
            Assert.AreEqual(7, peerId);
            using (OwnedBunch<RemoveIntention> bunch = removeIntentions.Bunch())
                Assert.IsFalse(bunch.Available());

            Handle(new ServerMessage { PlayerLeft = new PlayerLeft { SubjectId = 7 } });
            using (OwnedBunch<RemoveIntention> bunch = removeIntentions.Bunch())
                CollectionAssert.AreEquivalent(new[] { new RemoveIntention(WALLET_1, RoomSource.Pulse) }, bunch.Collection());
        }

        [Test]
        public void RemoveTheWalletAfterItsShadowTeleportsAwayAndItsSelectedPeerLeaves()
        {
            Handle(PlayerJoinedMessage(7, WALLET_1, REALM_A));
            Handle(PlayerJoinedMessage(8, WALLET_1, REALM_A));
            Handle(TeleportMessage(7, REALM_B));
            using (OwnedBunch<RemoveIntention> bunch = removeIntentions.Bunch())
                Assert.IsFalse(bunch.Available());
            Assert.IsFalse(peerIdCache.TryGetWallet(7, out _));

            Handle(new ServerMessage { PlayerLeft = new PlayerLeft { SubjectId = 8 } });
            using (OwnedBunch<RemoveIntention> bunch = removeIntentions.Bunch())
                CollectionAssert.AreEquivalent(new[] { new RemoveIntention(WALLET_1, RoomSource.Pulse) }, bunch.Collection());
            Assert.IsFalse(peerIdCache.TryGetPeerId(new Web3Address(WALLET_1), out _));
        }

        [Test]
        public void PromoteTheDestinationRealmSiblingWhenPurgingTheSelectedPeer()
        {
            Handle(PlayerJoinedMessage(7, WALLET_1, REALM_A));
            Handle(PlayerJoinedMessage(8, WALLET_1, REALM_A));
            peerIdCache.Set(new Web3Address(WALLET_1), 7, REALM_B, out _);
            bus.BroadcastTeleport(Vector3.zero);
            currentRealm = REALM_B;
            bus.BroadcastTeleport(Vector3.zero);
            beforeMessage();

            using (OwnedBunch<RemoveIntention> bunch = removeIntentions.Bunch())
                Assert.IsFalse(bunch.Available());
            Assert.IsFalse(peerIdCache.TryGetWallet(8, out _));
            Assert.IsTrue(peerIdCache.TryGetPeerId(new Web3Address(WALLET_1), out uint peerId));
            Assert.AreEqual(7, peerId);
            Assert.AreEqual(PeerIdCache.LookupResult.Found, peerIdCache.GetWalletInRealm(7, REALM_B, out _));
        }

        [Test]
        public void RemoveEachOverlappingWalletOnceOnDisconnect()
        {
            Handle(PlayerJoinedMessage(7, WALLET_1, REALM_A));
            Handle(PlayerJoinedMessage(8, WALLET_1, REALM_A));
            Handle(PlayerJoinedMessage(9, WALLET_2, REALM_A));

            disconnect(DisconnectReason.GRACEFUL);

            using (OwnedBunch<RemoveIntention> bunch = removeIntentions.Bunch())
                CollectionAssert.AreEquivalent(new[]
                {
                    new RemoveIntention(WALLET_1, RoomSource.Pulse),
                    new RemoveIntention(WALLET_2, RoomSource.Pulse),
                }, bunch.Collection());
            Assert.IsFalse(peerIdCache.TryGetWallet(7, out _));
            Assert.IsFalse(peerIdCache.TryGetWallet(8, out _));
            Assert.IsFalse(peerIdCache.TryGetWallet(9, out _));
        }

        [Test]
        public void RefreshAShadowJoinWithoutPromotingOrRoutingIt()
        {
            Handle(PlayerJoinedMessage(7, WALLET_1, REALM_A));
            Handle(PlayerJoinedMessage(8, WALLET_1, REALM_A));
            DrainAnnouncements();
            (Entity _, RemotePlayerMovementComponent component) = RegisterEntity(WALLET_1);
            movementInbox.DrainToEntities();
            int queuedMovements = component.Queue!.Count;

            Handle(PlayerJoinedMessage(7, WALLET_1, REALM_A));
            movementInbox.DrainToEntities();

            Assert.AreEqual(queuedMovements, component.Queue.Count);
            Assert.That(DrainAnnouncements(), Is.Empty);
            Assert.IsTrue(peerIdCache.TryGetPeerId(new Web3Address(WALLET_1), out uint peerId));
            Assert.AreEqual(8, peerId);
        }

        [Test]
        public void RetireTheSelectedPeerOnTeleportWithoutRemovingItsSurvivor()
        {
            Handle(PlayerJoinedMessage(7, WALLET_1, REALM_A));
            Handle(PlayerJoinedMessage(8, WALLET_1, REALM_A));

            Handle(TeleportMessage(8, REALM_B));

            using (OwnedBunch<RemoveIntention> bunch = removeIntentions.Bunch())
                Assert.IsFalse(bunch.Available());
            Assert.IsFalse(peerIdCache.TryGetWallet(8, out _));
            Assert.IsTrue(peerIdCache.TryGetPeerId(new Web3Address(WALLET_1), out uint peerId));
            Assert.AreEqual(7, peerId);

            Handle(new ServerMessage { PlayerLeft = new PlayerLeft { SubjectId = 7 } });
            using (OwnedBunch<RemoveIntention> bunch = removeIntentions.Bunch())
                CollectionAssert.AreEquivalent(new[] { new RemoveIntention(WALLET_1, RoomSource.Pulse) }, bunch.Collection());
        }

        [Test]
        public void RefreshAShadowTeleportWithoutRoutingOrPromotingItsPose()
        {
            Handle(PlayerJoinedMessage(7, WALLET_1, REALM_A));
            Handle(PlayerJoinedMessage(8, WALLET_1, REALM_A));
            (Entity _, RemotePlayerMovementComponent component) = RegisterEntity(WALLET_1);
            movementInbox.DrainToEntities();
            int queuedMovements = component.Queue!.Count;

            Handle(TeleportMessage(7, REALM_A));
            movementInbox.DrainToEntities();

            Assert.AreEqual(queuedMovements, component.Queue.Count);
            Assert.IsTrue(peerIdCache.TryGetPeerId(new Web3Address(WALLET_1), out uint peerId));
            Assert.AreEqual(8, peerId);
            Assert.IsTrue(peerIdCache.TryGetWallet(7, out _));
        }

        [Test]
        public void KeepRejoinProfileAndMovementAfterThePreviousRemovalWasConsumed()
        {
            Handle(PlayerJoinedMessage(7, WALLET_1, REALM_A));
            DrainAnnouncements();
            movementInbox.DrainToEntities();
            movementInbox.RemovePending(WALLET_1);
            Handle(new ServerMessage { PlayerLeft = new PlayerLeft { SubjectId = 7 } });
            List<RemoveIntention> consumedRemovals;
            using (OwnedBunch<RemoveIntention> bunch = removeIntentions.Bunch())
            {
                consumedRemovals = new List<RemoveIntention>(bunch.Collection());
                Assert.AreEqual(1, consumedRemovals.Count);
            }

            Handle(PlayerJoinedMessage(8, WALLET_1, REALM_A));
            incomingProfiles.Remove(consumedRemovals);
            Assert.AreEqual(1, DrainAnnouncements().Count);
            movementInbox.DrainToEntities();
            (Entity _, RemotePlayerMovementComponent component) = RegisterEntity(WALLET_1);
            movementInbox.TryFlushPending(WALLET_1);

            Assert.AreEqual(1, component.Queue!.Count);
            Assert.IsTrue(peerIdCache.TryGetPeerId(new Web3Address(WALLET_1), out uint peerId));
            Assert.AreEqual(8, peerId);
            using (OwnedBunch<RemoveIntention> bunch = removeIntentions.Bunch())
                Assert.IsFalse(bunch.Available());
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RestoreTheSurvivorsCurrentEmoteStateWithoutDispatchingShadowIntentions(bool stopWhileShadowed)
        {
            Handle(PlayerJoinedMessage(7, WALLET_1, REALM_A));
            if (stopWhileShadowed)
                Handle(EmoteStartedMessage(7));
            using (OwnedBunch<RemoteEmoteIntention> bunch = bus.EmoteIntentions())
                Assert.AreEqual(stopWhileShadowed, bunch.Available());
            Handle(PlayerJoinedMessage(8, WALLET_1, REALM_A));

            using (OwnedBunch<RemoteEmoteStopIntention> bunch = bus.EmoteStopIntentions())
                Assert.IsTrue(bunch.Available());

            Handle(stopWhileShadowed ? EmoteStoppedMessage(7) : EmoteStartedMessage(7));

            Assert.IsFalse(bus.IsPeerEmoting(new Web3Address(WALLET_1)));
            using (OwnedBunch<RemoteEmoteIntention> bunch = bus.EmoteIntentions())
                Assert.IsFalse(bunch.Available());
            using (OwnedBunch<RemoteEmoteStopIntention> bunch = bus.EmoteStopIntentions())
                Assert.IsFalse(bunch.Available());

            Handle(new ServerMessage { PlayerLeft = new PlayerLeft { SubjectId = 8 } });
            Handle(PlayerStateFullMessage(7, 10));

            Assert.AreEqual(!stopWhileShadowed, bus.IsPeerEmoting(new Web3Address(WALLET_1)));
        }

        [TestCase(false, false)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        public void ApplyTheStationaryOlderSurvivorThroughTheMovementSystemInEitherJoinOrder(bool survivorJoinsLast, bool movedWhileShadowed)
        {
            (Entity entity, RemotePlayerMovementComponent _) = RegisterEntity(WALLET_1);
            var avatar = new GameObject("Pulse survivor movement test");
            var interpolationSettings = ScriptableObject.CreateInstance<RemotePlayerInterpolationSettings>();
            try
            {
                world.Add(entity, new CharacterTransform(avatar.transform), new HeadIKComponent(),
                    new InterpolationComponent(), new ExtrapolationComponent(), new HandPointAtComponent());
                ref RemotePlayerMovementComponent movement = ref world.Get<RemotePlayerMovementComponent>(entity);
                movement.Initialized = true;
                movement.InitialCooldownTime = 10;
                movement.PastMessage = new NetworkMovementMessage { timestamp = 1000, position = new Vector3(9, 0, 0) };
                avatar.transform.position = movement.PastMessage.position;
                var settings = Substitute.For<IMultiplayerMovementSettings>();
                settings.MoveSendRate.Returns(1f);
                settings.InterpolationSettings.Returns(interpolationSettings);
                using var movementSystem = new RemotePlayersMovementSystem(world, settings, Substitute.For<ICharacterControllerSettings>());
                ServerMessage survivor = PlayerJoinedMessage(7, WALLET_1, REALM_A);
                ServerMessage departing = PlayerJoinedMessage(8, WALLET_1, REALM_A);
                int parcel = new ParcelEncoder(cachedLandscapeData.terrainData).Encode(Vector2Int.zero);
                survivor.PlayerJoined.State.State.ParcelIndex = parcel;
                survivor.PlayerJoined.State.State.PositionXQuantized = 3;
                departing.PlayerJoined.State.ServerTick = 100;
                departing.PlayerJoined.State.State.ParcelIndex = parcel;
                departing.PlayerJoined.State.State.PositionXQuantized = 9;

                Handle(survivorJoinsLast ? departing : survivor);
                movementInbox.DrainToEntities();
                movementSystem.Update(0.01f);
                Handle(survivorJoinsLast ? survivor : departing);
                movementInbox.DrainToEntities();
                movementSystem.Update(0.01f);
                ServerMessage refreshedSurvivor = PlayerStateFullMessage(7, 2);
                refreshedSurvivor.PlayerStateFull.ServerTick = 1;
                refreshedSurvivor.PlayerStateFull.State.ParcelIndex = parcel;
                refreshedSurvivor.PlayerStateFull.State.PositionXQuantized = 6;
                if (movedWhileShadowed)
                {
                    Handle(refreshedSurvivor);
                    movementInbox.DrainToEntities();
                    movementSystem.Update(0.01f);
                    Assert.AreEqual(9, avatar.transform.position.x, PlayerState.PositionXQuantizedStep);
                    Assert.AreEqual(0, avatar.transform.position.y);
                    Assert.AreEqual(0, avatar.transform.position.z);
                }
                Handle(new ServerMessage { PlayerLeft = new PlayerLeft { SubjectId = 8 } });
                movementInbox.DrainToEntities();
                movementSystem.Update(0.01f);

                if (movedWhileShadowed)
                {
                    Handle(refreshedSurvivor);
                    movementInbox.DrainToEntities();
                    movementSystem.Update(0.01f);
                }

                Assert.AreEqual(movedWhileShadowed ? 6 : 3, avatar.transform.position.x, PlayerState.PositionXQuantizedStep);
                Assert.AreEqual(0, avatar.transform.position.y);
                Assert.AreEqual(0, avatar.transform.position.z);
                Assert.AreEqual(0.001, world.Get<RemotePlayerMovementComponent>(entity).PastMessage.timestamp, 0.000001);
                Assert.IsTrue(peerIdCache.TryGetPeerId(new Web3Address(WALLET_1), out uint peerId));
                Assert.AreEqual(7, peerId);
                using (OwnedBunch<RemoveIntention> bunch = removeIntentions.Bunch())
                    Assert.IsFalse(bunch.Available());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(avatar);
                UnityEngine.Object.DestroyImmediate(interpolationSettings);
            }
        }

        [Test]
        public void PreservePromotionRecoveryAcrossADuplicateJoin()
        {
            Handle(PlayerJoinedMessage(7, WALLET_1, REALM_A));
            Handle(PlayerJoinedMessage(8, WALLET_1, REALM_A));
            (Entity _, RemotePlayerMovementComponent component) = RegisterEntity(WALLET_1);
            movementInbox.DrainToEntities();
            Handle(new ServerMessage { PlayerLeft = new PlayerLeft { SubjectId = 8 } });
            Handle(PlayerJoinedMessage(7, WALLET_1, REALM_A));
            ServerMessage refreshed = PlayerStateFullMessage(7, 1);
            refreshed.PlayerStateFull.ServerTick = 1;
            refreshed.PlayerStateFull.State.PositionXQuantized = 6;

            Handle(refreshed);
            movementInbox.DrainToEntities();

            Assert.AreEqual(1, component.Queue!.Count);
            Assert.IsTrue(component.Queue.First.resetTimeline);
            Assert.AreEqual(0.001, component.Queue.First.timestamp, 0.000001);
            Assert.AreEqual(6, component.Queue.First.position.x - (component.Queue.First.parcel.x * 16), PlayerState.PositionXQuantizedStep);
        }

        [Test]
        public void NotResetTheRecoveredTimelineForALateOlderResyncFull()
        {
            Handle(PlayerJoinedMessage(7, WALLET_1, REALM_A));
            Handle(PlayerJoinedMessage(8, WALLET_1, REALM_A));
            (Entity _, RemotePlayerMovementComponent component) = RegisterEntity(WALLET_1);
            movementInbox.DrainToEntities();
            Handle(new ServerMessage { PlayerLeft = new PlayerLeft { SubjectId = 8 } });
            Handle(PlayerStateDeltaMessage(7, 1, 2));
            ServerMessage delayedFull = PlayerStateFullMessage(7, 1);
            delayedFull.PlayerStateFull.ServerTick = 1;
            Handle(delayedFull);
            movementInbox.DrainToEntities();

            Assert.AreEqual(2, component.Queue!.Count);
            NetworkMovementMessage recovered = component.Queue.Dequeue();
            Assert.IsTrue(recovered.resetTimeline);
            Assert.AreEqual(0.002, recovered.timestamp, 0.000001);
            NetworkMovementMessage delayed = component.Queue.Dequeue();
            Assert.IsFalse(delayed.resetTimeline);
            Assert.AreEqual(0.001, delayed.timestamp, 0.000001);

            Handle(PlayerStateDeltaMessage(7, 2, 3));
            movementInbox.DrainToEntities();
            Assert.AreEqual(1, component.Queue.Count);
            Assert.IsFalse(component.Queue.First.resetTimeline);
            Assert.AreEqual(0.003, component.Queue.First.timestamp, 0.000001);
            Assert.AreEqual(1, bus.resyncCount);
        }

        [Test]
        public void RestoreTheLatestShadowEmoteIdAndMaskAfterReplacingAnotherPeersEmote()
        {
            Handle(PlayerJoinedMessage(7, WALLET_1, REALM_A));
            Handle(PlayerJoinedMessage(8, WALLET_1, REALM_A));
            Handle(EmoteStartedMessage(8));
            ServerMessage shadowStart = EmoteStartedMessage(7);
            shadowStart.EmoteStarted.EmoteId = "urn:decentraland:off-chain:base-emotes:dance";
            shadowStart.EmoteStarted.Mask = (int)AvatarEmoteMask.AemUpperBody;
            Handle(shadowStart);
            shadowStart.EmoteStarted.EmoteId = "urn:decentraland:off-chain:base-emotes:clap";
            shadowStart.EmoteStarted.Sequence = 4;
            shadowStart.EmoteStarted.ServerTick = 4;
            Handle(shadowStart);
            Handle(EmoteStoppedMessage(7));
            using (OwnedBunch<RemoteEmoteIntention> bunch = bus.EmoteIntentions())
                CollectionAssert.AreEquivalent(new[]
                {
                    new RemoteEmoteIntention(new URN("urn:decentraland:off-chain:base-emotes:wave"), WALLET_1, 0.002, AvatarEmoteMask.AemFullBody),
                }, bunch.Collection());
            Handle(EmoteStartedMessage(8));

            Handle(new ServerMessage { PlayerLeft = new PlayerLeft { SubjectId = 8 } });

            using (OwnedBunch<RemoteEmoteIntention> bunch = bus.EmoteIntentions())
                CollectionAssert.AreEquivalent(new[]
                {
                    new RemoteEmoteIntention(new URN("urn:decentraland:off-chain:base-emotes:clap"), WALLET_1, 0.004, AvatarEmoteMask.AemUpperBody),
                }, bunch.Collection());
            using (OwnedBunch<RemoteEmoteStopIntention> bunch = bus.EmoteStopIntentions())
                Assert.IsFalse(bunch.Available());
        }

        [TestCase(AvatarEmoteMask.AemFullBody, 2u)]
        [TestCase(AvatarEmoteMask.AemUpperBody, 2u)]
        [TestCase(AvatarEmoteMask.AemFullBody, 3u)]
        [TestCase(AvatarEmoteMask.AemUpperBody, 3u)]
        public void StopTheSelectedAnimationWithoutReplayingACompletedShadowEmote(AvatarEmoteMask mask, uint lateStartSequence)
        {
            Handle(PlayerJoinedMessage(7, WALLET_1, REALM_A));
            ServerMessage start = EmoteStartedMessage(7);
            start.EmoteStarted.Mask = (int)mask;
            Handle(start);
            Handle(PlayerJoinedMessage(8, WALLET_1, REALM_A));
            using (OwnedBunch<RemoteEmoteStopIntention> bunch = bus.EmoteStopIntentions())
                Assert.IsTrue(bunch.Available());
            Handle(EmoteStoppedMessage(7));
            start.EmoteStarted.Sequence = lateStartSequence;
            Handle(start);
            Handle(EmoteStartedMessage(8));

            Handle(new ServerMessage { PlayerLeft = new PlayerLeft { SubjectId = 8 } });

            Assert.IsFalse(bus.IsPeerEmoting(new Web3Address(WALLET_1)));
            using (OwnedBunch<RemoteEmoteIntention> bunch = bus.EmoteIntentions())
                Assert.IsFalse(bunch.Available());
            using (OwnedBunch<RemoteEmoteStopIntention> bunch = bus.EmoteStopIntentions())
            {
                Assert.AreEqual(1, bunch.Collection().Count);
                foreach (RemoteEmoteStopIntention stop in bunch.Collection())
                    Assert.AreEqual(WALLET_1, stop.WalletId);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void StopThePreviousSelectedPeersMaskedEmoteOnANewJoin(bool stopBeforeJoin)
        {
            Handle(PlayerJoinedMessage(7, WALLET_1, REALM_A));
            ServerMessage start = EmoteStartedMessage(7);
            start.EmoteStarted.Mask = (int)AvatarEmoteMask.AemUpperBody;
            Handle(start);
            if (stopBeforeJoin)
                Handle(EmoteStoppedMessage(7));

            Handle(PlayerJoinedMessage(8, WALLET_1, REALM_A));

            using (OwnedBunch<RemoteEmoteIntention> bunch = bus.EmoteIntentions())
                Assert.IsFalse(bunch.Available());
            using (OwnedBunch<RemoteEmoteStopIntention> bunch = bus.EmoteStopIntentions())
                CollectionAssert.AreEquivalent(new[] { new RemoteEmoteStopIntention(WALLET_1, 0.001) }, bunch.Collection());
        }

        [Test]
        public void StartTheNewSelectedEmoteWithoutAnObsoleteQueuedStop()
        {
            Handle(PlayerJoinedMessage(7, WALLET_1, REALM_A));
            Handle(EmoteStartedMessage(7));
            Handle(EmoteStoppedMessage(7));
            Handle(PlayerJoinedMessage(8, WALLET_1, REALM_A));
            ServerMessage start = EmoteStartedMessage(8);
            start.EmoteStarted.EmoteId = "urn:decentraland:off-chain:base-emotes:clap";

            Handle(start);

            using (OwnedBunch<RemoteEmoteStopIntention> bunch = bus.EmoteStopIntentions())
                Assert.IsFalse(bunch.Available());
            using (OwnedBunch<RemoteEmoteIntention> bunch = bus.EmoteIntentions())
                CollectionAssert.AreEquivalent(new[]
                {
                    new RemoteEmoteIntention(new URN("urn:decentraland:off-chain:base-emotes:clap"), WALLET_1, 0.002, AvatarEmoteMask.AemFullBody),
                }, bunch.Collection());
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ForgetThePreviousRegistrationEmoteWhenAPeerIdIsReused(bool disconnectAll)
        {
            Handle(PlayerJoinedMessage(7, WALLET_1, REALM_A));
            Handle(EmoteStartedMessage(7));
            if (disconnectAll)
                disconnect(DisconnectReason.GRACEFUL);
            else
                Handle(new ServerMessage { PlayerLeft = new PlayerLeft { SubjectId = 7 } });
            Handle(PlayerJoinedMessage(7, WALLET_1, REALM_A));
            Handle(PlayerJoinedMessage(8, WALLET_1, REALM_A));

            Handle(new ServerMessage { PlayerLeft = new PlayerLeft { SubjectId = 8 } });

            Assert.IsFalse(bus.IsPeerEmoting(new Web3Address(WALLET_1)));
            using (OwnedBunch<RemoteEmoteIntention> bunch = bus.EmoteIntentions())
                Assert.IsFalse(bunch.Available());
        }

        [TestCase(ServerMessage.MessageOneofCase.PlayerStateFull)]
        [TestCase(ServerMessage.MessageOneofCase.PlayerStateDelta)]
        [TestCase(ServerMessage.MessageOneofCase.Teleported)]
        public void RestoreTheLatestShadowPoseWithoutDispatchingItBeforePromotion(ServerMessage.MessageOneofCase kind)
        {
            Handle(PlayerJoinedMessage(7, WALLET_1, REALM_A));
            Handle(PlayerJoinedMessage(8, WALLET_1, REALM_A));
            (Entity _, RemotePlayerMovementComponent movement) = RegisterEntity(WALLET_1);
            movementInbox.DrainToEntities();
            int queuedMovements = movement.Queue!.Count;
            ServerMessage update;
            switch (kind)
            {
                case ServerMessage.MessageOneofCase.PlayerStateFull:
                    update = PlayerStateFullMessage(7, 2);
                    update.PlayerStateFull.State.PositionYQuantized = 6;
                    break;
                case ServerMessage.MessageOneofCase.Teleported:
                    update = TeleportMessage(7, REALM_A);
                    update.Teleported.State.PositionYQuantized = 6;
                    break;
                default:
                    update = PlayerStateDeltaMessage(7, 1, 2);
                    update.PlayerStateDelta.PositionYQuantized = 6;
                    break;
            }
            Handle(update);
            Handle(PlayerStateFullMessage(7, 1));
            movementInbox.DrainToEntities();
            Assert.AreEqual(queuedMovements, movement.Queue.Count);
            Assert.AreEqual(0, bus.resyncCount);

            Handle(new ServerMessage { PlayerLeft = new PlayerLeft { SubjectId = 8 } });
            movementInbox.DrainToEntities();

            Assert.AreEqual(1, movement.Queue.Count);
            Assert.IsTrue(movement.Queue.First.resetTimeline);
            Assert.AreEqual(6, movement.Queue.First.position.y, PlayerState.PositionYQuantizedStep);
            Assert.AreEqual(1, bus.resyncCount);
            Handle(PlayerStateDeltaMessage(7, 2, 3));
            Assert.AreEqual(1, bus.resyncCount, "The promotion resync uses the cached shadow sequence.");
        }

        [Test]
        public void IgnoreAShadowDeltaGapUntilPromotionResyncRepairsIt()
        {
            Handle(PlayerJoinedMessage(7, WALLET_1, REALM_A));
            Handle(PlayerJoinedMessage(8, WALLET_1, REALM_A));
            ServerMessage consecutive = PlayerStateDeltaMessage(7, 1, 2);
            consecutive.PlayerStateDelta.PositionYQuantized = 6;
            Handle(consecutive);
            ServerMessage gap = PlayerStateDeltaMessage(7, 4, 5);
            gap.PlayerStateDelta.PositionYQuantized = 9;
            Handle(gap);
            ServerMessage staleBaseline = PlayerStateDeltaMessage(7, 1, 6);
            staleBaseline.PlayerStateDelta.PositionYQuantized = 12;
            Handle(staleBaseline);
            Assert.AreEqual(0, bus.resyncCount);
            (Entity _, RemotePlayerMovementComponent movement) = RegisterEntity(WALLET_1);
            movementInbox.DrainToEntities();

            Handle(new ServerMessage { PlayerLeft = new PlayerLeft { SubjectId = 8 } });
            movementInbox.DrainToEntities();
            Assert.AreEqual(6, movement.Queue!.First.position.y, PlayerState.PositionYQuantizedStep);
            Assert.AreEqual(1, bus.resyncCount);
            ServerMessage recovered = PlayerStateFullMessage(7, 6);
            recovered.PlayerStateFull.State.PositionYQuantized = 12;
            Handle(recovered);
            movementInbox.DrainToEntities();

            Assert.AreEqual(1, movement.Queue.Count);
            Assert.IsTrue(movement.Queue.First.resetTimeline);
            Assert.AreEqual(12, movement.Queue.First.position.y, PlayerState.PositionYQuantizedStep);
            Handle(PlayerStateDeltaMessage(7, 6, 7));
            Assert.AreEqual(1, bus.resyncCount);
        }

        [Test]
        public void RetryTheCurrentSelectedEmoteStartAndStop()
        {
            Handle(PlayerJoinedMessage(7, WALLET_1, REALM_A));
            Handle(EmoteStartedMessage(7));
            RemoteEmoteIntention start = TakeEmoteIntention();

            bus.SaveForRetry(start);

            RemoteEmoteIntention retriedStart = TakeEmoteIntention();
            Assert.AreEqual(start, retriedStart);
            Assert.AreSame(bus, retriedStart.Source);
            Assert.AreNotEqual(0, retriedStart.RetryGeneration);
            Handle(EmoteStoppedMessage(7));
            RemoteEmoteStopIntention stop = TakeEmoteStopIntention();

            bus.SaveForRetry(stop);

            RemoteEmoteStopIntention retriedStop = TakeEmoteStopIntention();
            Assert.AreEqual(stop, retriedStop);
            Assert.AreSame(bus, retriedStop.Source);
            Assert.AreNotEqual(start.RetryGeneration, retriedStop.RetryGeneration);
        }

        [Test]
        public void RejectAnOldSelectedPeersRetryAfterSelectionChanges()
        {
            Handle(PlayerJoinedMessage(7, WALLET_1, REALM_A));
            Handle(EmoteStartedMessage(7));
            RemoteEmoteIntention oldStart = TakeEmoteIntention();
            Handle(PlayerJoinedMessage(8, WALLET_1, REALM_A));

            bus.SaveForRetry(oldStart);

            using (OwnedBunch<RemoteEmoteIntention> bunch = bus.EmoteIntentions())
                Assert.IsFalse(bunch.Available());
            RemoteEmoteStopIntention currentStop = TakeEmoteStopIntention();
            Assert.AreNotEqual(oldStart.RetryGeneration, currentStop.RetryGeneration);
            Handle(new ServerMessage { PlayerLeft = new PlayerLeft { SubjectId = 8 } });

            bus.SaveForRetry(currentStop);

            using (OwnedBunch<RemoteEmoteStopIntention> bunch = bus.EmoteStopIntentions())
                Assert.IsFalse(bunch.Available());
            Assert.AreEqual(oldStart.EmoteId, TakeEmoteIntention().EmoteId);
        }

        [Test]
        public void RejectAStartRetryAfterStopAndAStopRetryAfterANewStart()
        {
            Handle(PlayerJoinedMessage(7, WALLET_1, REALM_A));
            Handle(EmoteStartedMessage(7));
            RemoteEmoteIntention oldStart = TakeEmoteIntention();
            Handle(EmoteStoppedMessage(7));
            RemoteEmoteStopIntention oldStop = TakeEmoteStopIntention();

            bus.SaveForRetry(oldStart);

            using (OwnedBunch<RemoteEmoteIntention> bunch = bus.EmoteIntentions())
                Assert.IsFalse(bunch.Available());
            ServerMessage newStart = EmoteStartedMessage(7);
            newStart.EmoteStarted.Sequence = 4;
            newStart.EmoteStarted.ServerTick = 4;
            Handle(newStart);

            bus.SaveForRetry(oldStop);

            using (OwnedBunch<RemoteEmoteStopIntention> bunch = bus.EmoteStopIntentions())
                Assert.IsFalse(bunch.Available());
            Assert.AreEqual(0.004, TakeEmoteIntention().Timestamp, 0.000001);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RejectCapturedRetriesAcrossDetachAndPeerIdReuse(bool disconnectAll)
        {
            Handle(PlayerJoinedMessage(7, WALLET_1, REALM_A));
            Handle(EmoteStartedMessage(7));
            RemoteEmoteIntention oldStart = TakeEmoteIntention();
            Handle(EmoteStoppedMessage(7));
            RemoteEmoteStopIntention oldStop = TakeEmoteStopIntention();
            if (disconnectAll)
                disconnect(DisconnectReason.GRACEFUL);
            else
                Handle(new ServerMessage { PlayerLeft = new PlayerLeft { SubjectId = 7 } });
            Handle(PlayerJoinedMessage(7, WALLET_1, REALM_A));
            Handle(EmoteStartedMessage(7));
            RemoteEmoteIntention currentStart = TakeEmoteIntention();

            bus.SaveForRetry(oldStart);
            bus.SaveForRetry(oldStop);
            bus.SaveForRetry(currentStart);

            Assert.AreEqual(currentStart.RetryGeneration, TakeEmoteIntention().RetryGeneration);
            Assert.AreNotEqual(oldStart.RetryGeneration, currentStart.RetryGeneration);
            using (OwnedBunch<RemoteEmoteStopIntention> bunch = bus.EmoteStopIntentions())
                Assert.IsFalse(bunch.Available());
        }

        [Test]
        public void RejectCapturedRetriesAfterRealmCleanup()
        {
            Handle(PlayerJoinedMessage(7, WALLET_1, REALM_A));
            Handle(EmoteStartedMessage(7));
            RemoteEmoteIntention oldStart = TakeEmoteIntention();
            bus.BroadcastTeleport(Vector3.zero);
            currentRealm = REALM_B;
            bus.BroadcastTeleport(Vector3.zero);

            bus.SaveForRetry(oldStart);

            using (OwnedBunch<RemoteEmoteIntention> bunch = bus.EmoteIntentions())
                Assert.IsFalse(bunch.Available());
        }

        [Test]
        public void KeepSelectedRetriesValidWhileShadowEmotesChange()
        {
            Handle(PlayerJoinedMessage(7, WALLET_1, REALM_A));
            Handle(PlayerJoinedMessage(8, WALLET_1, REALM_A));
            Handle(EmoteStartedMessage(8));
            RemoteEmoteIntention selectedStart = TakeEmoteIntention();
            Handle(EmoteStartedMessage(7));
            Handle(EmoteStoppedMessage(7));

            bus.SaveForRetry(selectedStart);

            Assert.AreEqual(selectedStart.RetryGeneration, TakeEmoteIntention().RetryGeneration);
            Handle(EmoteStoppedMessage(8));
            RemoteEmoteStopIntention selectedStop = TakeEmoteStopIntention();
            ServerMessage shadowStart = EmoteStartedMessage(7);
            shadowStart.EmoteStarted.Sequence = 4;
            Handle(shadowStart);

            bus.SaveForRetry(selectedStop);

            Assert.AreEqual(selectedStop.RetryGeneration, TakeEmoteStopIntention().RetryGeneration);
        }

        [Test]
        public void ReplaceEqualQueuedPayloadsWithTheCurrentRetryGeneration()
        {
            Handle(PlayerJoinedMessage(7, WALLET_1, REALM_A));
            ServerMessage start = EmoteStartedMessage(7);
            Handle(start);
            RemoteEmoteIntention oldStart = TakeEmoteIntention();
            bus.SaveForRetry(oldStart);

            Handle(start);

            RemoteEmoteIntention currentStart = TakeEmoteIntention();
            Assert.AreEqual(oldStart, currentStart);
            Assert.AreNotEqual(oldStart.RetryGeneration, currentStart.RetryGeneration);
            bus.SaveForRetry(oldStart);
            bus.SaveForRetry(currentStart);
            Assert.AreEqual(currentStart.RetryGeneration, TakeEmoteIntention().RetryGeneration);
        }

        private RemoteEmoteIntention TakeEmoteIntention()
        {
            using OwnedBunch<RemoteEmoteIntention> bunch = bus.EmoteIntentions();
            Assert.AreEqual(1, bunch.Collection().Count);
            foreach (RemoteEmoteIntention intention in bunch.Collection())
                return intention;
            throw new InvalidOperationException("Expected one emote intention.");
        }

        private RemoteEmoteStopIntention TakeEmoteStopIntention()
        {
            using OwnedBunch<RemoteEmoteStopIntention> bunch = bus.EmoteStopIntentions();
            Assert.AreEqual(1, bunch.Collection().Count);
            foreach (RemoteEmoteStopIntention intention in bunch.Collection())
                return intention;
            throw new InvalidOperationException("Expected one emote stop intention.");
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

        private static ServerMessage PlayerStateDeltaMessage(uint subjectId, uint baseline, uint sequence) =>
            new ()
            {
                PlayerStateDelta = new PlayerStateDeltaTier0
                {
                    SubjectId = subjectId,
                    BaselineSeq = baseline,
                    NewSeq = sequence,
                    ServerTick = sequence,
                },
            };

        private static ServerMessage EmoteStoppedMessage(uint subjectId) =>
            new ()
            {
                EmoteStopped = new EmoteStopped
                {
                    SubjectId = subjectId,
                    Sequence = 3,
                    ServerTick = 3,
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
