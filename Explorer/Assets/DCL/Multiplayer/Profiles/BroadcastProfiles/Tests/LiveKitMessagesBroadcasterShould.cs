using DCL.Multiplayer.Connections.GateKeeper.Rooms;
using DCL.Multiplayer.Connections.Messaging;
using DCL.Multiplayer.Connections.Messaging.Hubs;
using DCL.Multiplayer.Connections.Messaging.Pipe;
using DCL.Multiplayer.Connections.Pulse;
using DCL.Multiplayer.Connections.Rooms;
using DCL.LiveKit.Public;
using DCL.Web3;
using Decentraland.Kernel.Comms.Rfc4;
using LiveKit.Rooms;
using LiveKit.Rooms.Participants;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Threading;

namespace DCL.Multiplayer.Profiles.BroadcastProfiles.Tests
{
    /// <summary>
    ///     Regression coverage for unity-explorer#9337: a wallet with a live <see cref="PeerIdCache" />
    ///     session must be excluded from the targeted LiveKit fan-out (<see cref="LiveKitMessagesBroadcaster.Send{TInput,TMessage}" />),
    ///     while <see cref="LiveKitMessagesBroadcaster.SendProfileAnnouncement{TInput,TMessage}" /> still
    ///     reaches the whole room untargeted so peers missing from <c>announcedWallets</c> get materialized.
    /// </summary>
    [TestFixture]
    public class LiveKitMessagesBroadcasterShould
    {
        private const string REALM = "realm-a";
        private const uint PEER_ID = 7;
        private const string WALLET = "0x0000000000000000000000000000000000000001";
        private const string WALLET_MISSING = "0x0000000000000000000000000000000000000002";

        private static void NoOpBuild(string args, AnnounceProfileVersion message) { }

        // Pre-cancelled so MessageWrap.SendAndDisposeAsync returns immediately after its first await
        // (DCLTask.SwitchToThreadPool) without ever touching IDataPipe.PublishData - keeps this a pure
        // unit test of the recipient-computation logic without needing a working LiveKit data pipe.
        private static readonly CancellationToken CancelledToken = new (canceled: true);

        private PeerIdCache peerIdCache;
        private IMessagePipe islandPipe;
        private IMessagePipe scenePipe;
        private LiveKitMessagesBroadcaster broadcaster;

        private List<string> lastIslandRecipients;
        private List<string> lastSceneRecipients;

        [SetUp]
        public void SetUp()
        {
            var sceneRoom = Substitute.For<IGateKeeperSceneRoom>();
            var room = Substitute.For<IRoom>();
            var participants = Substitute.For<IParticipantsHub>();
            room.Participants.Returns(participants);
            sceneRoom.Room().Returns(room);
            // RemoteParticipant(AUTH_SERVER_IDENTITY) left unstubbed -> NSubstitute returns null,
            // matching "no gatekeeper/authoritative-server participant present" for these cases.

            islandPipe = Substitute.For<IMessagePipe>();
            scenePipe = Substitute.For<IMessagePipe>();

            islandPipe.NewMessage<AnnounceProfileVersion>().Returns(_ =>
            {
                lastIslandRecipients = new List<string>();
                return new MessageWrap<AnnounceProfileVersion>(new AnnounceProfileVersion(), null!, lastIslandRecipients, null!, null!, string.Empty, 0);
            });

            scenePipe.NewMessage<AnnounceProfileVersion>().Returns(_ =>
            {
                lastSceneRecipients = new List<string>();
                return new MessageWrap<AnnounceProfileVersion>(new AnnounceProfileVersion(), null!, lastSceneRecipients, null!, null!, string.Empty, 0);
            });

            var messagePipesHub = Substitute.For<IMessagePipesHub>();
            messagePipesHub.IslandPipe().Returns(islandPipe);
            messagePipesHub.ScenePipe().Returns(scenePipe);

            peerIdCache = new PeerIdCache();

            // TimeSpan.Zero: SendProfileAnnouncement always takes the untargeted/fallback branch;
            // the two Send-only cases below never call SendProfileAnnouncement, so the interval is inert for them.
            broadcaster = new LiveKitMessagesBroadcaster(sceneRoom, messagePipesHub, new PulseActivation(true), peerIdCache, TimeSpan.Zero);
        }

        // A wallet with a live PeerIdCache session (i.e. its Pulse join was actually processed) must not
        // also receive the *targeted* LiveKit announce/movement/emote path, since MovementMessageBusProxy
        // already fans out to both Pulse and LiveKit unconditionally.
        [Test]
        public void SkipPeerIdCacheLiveWalletInSendPath()
        {
            broadcaster.Add(WALLET, RoomSource.Island);
            peerIdCache.Set(new Web3Address(WALLET), PEER_ID, REALM);

            broadcaster.Send<string, AnnounceProfileVersion>(NoOpBuild, string.Empty, LKDataPacketKind.KindReliable, CancelledToken);

            islandPipe.DidNotReceive().NewMessage<AnnounceProfileVersion>();
            scenePipe.DidNotReceive().NewMessage<AnnounceProfileVersion>();
        }

        // Companion to the skip case: proves the exclusion above is genuinely driven by the live
        // PeerIdCache session (i.e. WALLET really was recruited via Add, not silently dropped) - once
        // that Pulse session ends, the same recruited membership resumes receiving the targeted fan-out.
        [Test]
        public void ResumeTargetingWalletAfterPulseSessionEnds()
        {
            broadcaster.Add(WALLET, RoomSource.Island);
            peerIdCache.Set(new Web3Address(WALLET), PEER_ID, REALM);
            peerIdCache.Remove(PEER_ID);

            broadcaster.Send<string, AnnounceProfileVersion>(NoOpBuild, string.Empty, LKDataPacketKind.KindReliable, CancelledToken);

            islandPipe.Received(1).NewMessage<AnnounceProfileVersion>();
            CollectionAssert.Contains(lastIslandRecipients, WALLET);
        }

        // Case (b): fallback materialization. The low-cadence untargeted broadcast reaches every peer
        // physically in the room - including ones missing from announcedWallets - without the SEND
        // call itself widening the broadcaster's own targeted fan-out. On the receiving side (wired via
        // LiveKitRemoteAnnouncements.OnMessageReceived -> broadcaster.Add, reproduced directly here since
        // that plumbing lives in a different class) a materialized-but-Pulse-live peer still does not
        // enter the *targeted* recipient set - assembled as one flow so the set-membership invariant
        // ("materialized, but excluded from fan-out") is checked on the same broadcaster instance.
        [Test]
        public void FallbackAnnounceReachesWholeRoomButMaterializedPeerStaysOutOfTargetedFanOut()
        {
            broadcaster.SendProfileAnnouncement<string, AnnounceProfileVersion>(NoOpBuild, string.Empty, LKDataPacketKind.KindReliable, CancelledToken);

            islandPipe.Received(1).NewMessage<AnnounceProfileVersion>();
            scenePipe.Received(1).NewMessage<AnnounceProfileVersion>();

            Assert.IsEmpty(lastIslandRecipients,
                "the fallback must be untargeted (no AddSpecialRecipient calls) so it reaches every peer " +
                "physically in the room, not just announcedWallets members");
            Assert.IsEmpty(lastSceneRecipients);

            islandPipe.ClearReceivedCalls();
            scenePipe.ClearReceivedCalls();

            // Receive side: the previously-missing peer is now materialized into announcedWallets.
            // Add is unconditional recruitment by design - membership alone is harmless once the
            // targeted fan-out is filtered by the PeerIdCache check in Send.
            broadcaster.Add(WALLET_MISSING, RoomSource.Island);

            // ...but it is ALSO Pulse-live (its join was in fact processed by Pulse - it just never
            // announced over LiveKit before, which is why it was missing above). Assert the set
            // membership post-fallback: materialized, but excluded from the targeted fan-out.
            peerIdCache.Set(new Web3Address(WALLET_MISSING), PEER_ID, REALM);

            broadcaster.Send<string, AnnounceProfileVersion>(NoOpBuild, string.Empty, LKDataPacketKind.KindReliable, CancelledToken);

            islandPipe.DidNotReceive().NewMessage<AnnounceProfileVersion>();

            // Prove the exclusion is PeerIdCache-driven (i.e. WALLET_MISSING really was recruited by
            // Add, not silently dropped by some unrelated path): once its Pulse session ends, the same
            // materialized membership resumes receiving the targeted LiveKit fan-out.
            peerIdCache.Remove(PEER_ID);

            broadcaster.Send<string, AnnounceProfileVersion>(NoOpBuild, string.Empty, LKDataPacketKind.KindReliable, CancelledToken);

            islandPipe.Received(1).NewMessage<AnnounceProfileVersion>();
            CollectionAssert.Contains(lastIslandRecipients, WALLET_MISSING);
        }
    }
}
