using DCL.Diagnostics;
using DCL.Multiplayer.Connections.Pulse;
using DCL.Multiplayer.Emotes;
using DCL.Multiplayer.Profiles.Announcements;
using DCL.Multiplayer.Profiles.RemoveIntentions;
using DCL.Profiles.Self;
using DCL.Web3;
using DCL.Web3.Identities;
using Decentraland.Pulse;
using ECS;
using System;
using System.Collections.Generic;

namespace DCL.Multiplayer.Movement
{
    public partial class PulseMultiplayerBus : IMovementMessageBus, IEmotesMessageBus, IDisposable
    {
        internal const string SELF_MIRROR_WALLET_ID = "self_mirror";

        private const double SERVER_TICKS_TO_MOVEMENT_TIMESTAMP = 0.001;

        private readonly IPulseMultiplayerService pulseService;
        private readonly PeerIdCache peerIdCache;
        private readonly MovementInbox movementInbox;
        private readonly ParcelEncoder parcelEncoder;
        private readonly PulseIncomingProfileAnnouncements incomingProfiles;
        private readonly PulseRemoveIntentions removeIntentions;
        private readonly IWeb3IdentityCache identityCache;

        private volatile bool isDisposed;
        private volatile bool routingPurgeRequested;

        // BroadcastTeleport is main-thread-only, so single-threaded reuse is safe
        private readonly HashSet<string> staleWalletsBuffer = new ();

        internal long ResyncCount { get; private set; }

        internal long EmoteStateMismatchCount { get; private set; }

        public PulseMultiplayerBus(IPulseMultiplayerService pulseService,
            PeerIdCache peerIdCache,
            MovementInbox movementInbox,
            ParcelEncoder parcelEncoder,
            PulseIncomingProfileAnnouncements incomingProfiles,
            PulseRemoveIntentions removeIntentions,
            IWeb3IdentityCache identityCache,
            ReconnectionSettings settings,
            ISelfProfile selfProfile,
            IRealmData realmData)
        {
            this.pulseService = pulseService;
            this.peerIdCache = peerIdCache;
            this.movementInbox = movementInbox;
            this.parcelEncoder = parcelEncoder;
            this.incomingProfiles = incomingProfiles;
            this.removeIntentions = removeIntentions;
            this.identityCache = identityCache;
            this.realmData = realmData;
            this.selfProfile = selfProfile;
            this.settings = settings;
        }

        private Web3Address ResolveSelfMirrorWallet(string userId)
        {
            if (userId != SELF_MIRROR_WALLET_ID)
                return new Web3Address(userId);

            return identityCache.EnsuredIdentity().Address;
        }

        public void Dispose()
        {
            isDisposed = true;
            pulseService.UnregisterAllHandlers();
        }

        public void SubscribeToIncomingMessages()
        {
            pulseService.RegisterSyncHandler(ServerMessage.MessageOneofCase.PlayerJoined, HandlePlayerJoined);
            pulseService.RegisterSyncHandler(ServerMessage.MessageOneofCase.PlayerLeft, HandlePlayerLeft);
            pulseService.RegisterSyncHandler(ServerMessage.MessageOneofCase.PlayerStateFull, HandlePlayerStateFull);
            pulseService.RegisterSyncHandler(ServerMessage.MessageOneofCase.PlayerStateDelta, HandlePlayerStateDelta);
            pulseService.RegisterSyncHandler(ServerMessage.MessageOneofCase.PlayerProfileVersionAnnounced, HandleProfileAnnouncement);
            pulseService.RegisterSyncHandler(ServerMessage.MessageOneofCase.Teleported, HandleTeleport);
            pulseService.RegisterSyncHandler(ServerMessage.MessageOneofCase.EmoteStarted, HandleEmoteStarted);
            pulseService.RegisterSyncHandler(ServerMessage.MessageOneofCase.EmoteStopped, HandleEmoteStopped);

            pulseService.RegisterDisconnectHandler(HandleDisconnect);
            pulseService.RegisterHandshakeHandler(HandshakeAsync);
        }

        private void RemoveAllPeers()
        {
            peerIdCache.RemoveAll(wallet => removeIntentions.Enqueue(wallet));

            lastMovementMessages.Clear();
            pendingResyncs.Clear();
            emotingSubjects.Clear();
        }

        /// <summary>
        ///     Must run on the main thread, before the teleport request is sent, so no new-realm announcements can race the scrub.
        /// </summary>
        private void PurgeDifferentRealmPeers()
        {
            staleWalletsBuffer.Clear();
            peerIdCache.CollectWalletsNotInRealm(realmData.RealmName, staleWalletsBuffer);

            if (staleWalletsBuffer.Count == 0)
                return;

            foreach (string wallet in staleWalletsBuffer)
            {
                removeIntentions.Enqueue(wallet);
                movementInbox.RemovePending(wallet);
            }

            incomingProfiles.RemoveWallets(staleWalletsBuffer);
            RemoveEmoteIntentionsFor(staleWalletsBuffer);

            routingPurgeRequested = true;

            ReportHub.Log(ReportCategory.MULTIPLAYER, $"Purged {staleWalletsBuffer.Count} peers from a different realm on teleport to '{realmData.RealmName}'");
        }

        /// <summary>
        ///     Routing-thread collections may only be mutated on the routing thread; deferring their purge to the
        ///     next incoming message is safe because stale entries are inert until a message arrives.
        /// </summary>
        private void TryDrainRoutingPurge()
        {
            if (!routingPurgeRequested)
                return;

            routingPurgeRequested = false;

            peerIdCache.RemoveWhereNotInRealm(realmData.RealmName, PurgeQueues);
        }

        private void PurgeQueues(uint subjectId)
        {
            lastMovementMessages.Remove(subjectId);
            pendingResyncs.Remove(subjectId);
            emotingSubjects.Remove(subjectId);
        }

        private void Inbox(NetworkMovementMessage fullMovementMessage, string @for)
        {
            movementInbox.Enqueue(fullMovementMessage, @for);
        }
    }
}
