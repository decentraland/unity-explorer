using DCL.Diagnostics;
using DCL.Multiplayer.Connections.Pulse;
using DCL.Multiplayer.Emotes;
using DCL.Multiplayer.Profiles.Announcements;
using DCL.Multiplayer.Profiles.RemoveIntentions;
using DCL.Profiles.Self;
using DCL.Web3;
using DCL.Web3.Identities;
using Decentraland.Pulse;
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

        // Null until the first broadcast so the spawn teleport doesn't purge current-realm announcements.
        // Written after the TeleportRequest is queued, so a realm probe that reads it reaches the server after the teleport
        private volatile string? lastBroadcastRealm;

        // Routing thread only: hidden peers asked about since our last realm change, whose full state can be adopted
        private readonly HashSet<uint> realmProbes = new ();

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
            PulseRealm pulseRealm)
        {
            this.pulseService = pulseService;
            this.peerIdCache = peerIdCache;
            this.movementInbox = movementInbox;
            this.parcelEncoder = parcelEncoder;
            this.incomingProfiles = incomingProfiles;
            this.removeIntentions = removeIntentions;
            this.identityCache = identityCache;
            this.pulseRealm = pulseRealm;
            this.selfProfile = selfProfile;
            this.settings = settings;
        }

        private Web3Address ResolveSelfMirrorWallet(string userId)
        {
            if (userId != SELF_MIRROR_WALLET_ID)
                return new Web3Address(userId);

            return identityCache.EnsuredIdentity().Address;
        }

        private bool TryGetWalletInCurrentRealm(uint subjectId, string messageType, out Web3Address wallet)
        {
            switch (peerIdCache.GetWalletInRealm(subjectId, pulseRealm.Value, out wallet))
            {
                case PeerIdCache.LookupResult.UnknownPeer:
                    ReportHub.LogWarning(ReportCategory.MULTIPLAYER, $"Received {messageType} from unknown peer {subjectId}");
                    return false;
                case PeerIdCache.LookupResult.RealmMismatch:
                    ProbeRealm(subjectId);

                    // Expected while a realm-change purge is pending, so not a warning
                    ReportHub.Log(ReportCategory.MULTIPLAYER, $"Dropped {messageType} from peer {subjectId} in a different realm");
                    return false;
                default:
                    return true;
            }
        }

        /// <summary>
        ///     A hidden peer that followed us gets no announcement once its TeleportPerformed has left the server's snapshot history.
        ///     The server handles our messages in order and answers a resync only for peers visible in our realm,
        ///     so asking after our TeleportRequest tells whether the peer is here.
        ///     A probe the server drops gets no reply, so every message from a hidden peer asks again.
        /// </summary>
        private void ProbeRealm(uint subjectId)
        {
            if (lastBroadcastRealm != pulseRealm.Value)
                return;

            realmProbes.Add(subjectId);

            // No snapshot carries this sequence, so the answer is a full state
            SendResyncRequest(subjectId, uint.MaxValue);
        }

        private bool TryAdoptProbedPeer(uint subjectId, out Web3Address wallet)
        {
            string realm = pulseRealm.Value;

            if (!realmProbes.Remove(subjectId)
                || lastBroadcastRealm != realm
                || peerIdCache.GetWalletInRealm(subjectId, realm, out _) != PeerIdCache.LookupResult.RealmMismatch
                || !peerIdCache.TryGetWallet(subjectId, out wallet))
            {
                wallet = default(Web3Address);
                return false;
            }

            peerIdCache.Set(wallet, subjectId, realm);
            Reannounce(subjectId, wallet);
            return true;
        }

        /// <summary>
        ///     The server keeps a view across realm changes and announces it only once, so a hidden peer is shown again here.
        /// </summary>
        private void Reannounce(uint subjectId, Web3Address wallet)
        {
            removeIntentions.Cancel(wallet);
            incomingProfiles.Enqueue(wallet, profileVersions.GetValueOrDefault(subjectId));
            pendingResyncs.Remove(subjectId);
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

            pulseService.RegisterBeforeMessageHandler(TryDrainRoutingPurge);
            pulseService.RegisterDisconnectHandler(HandleDisconnect);
            pulseService.RegisterHandshakeHandler(HandshakeAsync);
        }

        private void RemoveAllPeers()
        {
            peerIdCache.RemoveAll(wallet => removeIntentions.Enqueue(wallet));

            lastMovementMessages.Clear();
            pendingResyncs.Clear();
            emotingSubjects.Clear();
            profileVersions.Clear();
            realmProbes.Clear();
        }

        /// <summary>
        ///     Must run on the main thread, before the teleport request is sent, so no new-realm announcements can race the scrub.
        /// </summary>
        private void PurgeDifferentRealmPeers()
        {
            // Everything gathered up to the realm switch belongs to the previous realm
            incomingProfiles.Clear();
            ClearEmoteIntentions();

            // The comparison spares co-teleporting peers whose TeleportPerformed already refreshed the cached realm
            staleWalletsBuffer.Clear();
            peerIdCache.CollectWalletsNotInRealm(pulseRealm.Value, staleWalletsBuffer);

            foreach (string wallet in staleWalletsBuffer)
            {
                removeIntentions.Enqueue(wallet);
                movementInbox.RemovePending(wallet);
            }

            routingPurgeRequested = true;

            ReportHub.Log(ReportCategory.MULTIPLAYER, $"Purged {staleWalletsBuffer.Count} peers from a different realm on teleport to '{pulseRealm.Value}'");
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

            // Peers are hidden, not forgotten, on a realm change; the ones in the realm we arrived in are shown again
            string realm = pulseRealm.Value;
            realmProbes.Clear();

            foreach (KeyValuePair<uint, (uint sequence, NetworkMovementMessage message)> last in lastMovementMessages)
            {
                if (peerIdCache.GetWalletInRealm(last.Key, realm, out Web3Address wallet) == PeerIdCache.LookupResult.Found)
                {
                    Reannounce(last.Key, wallet);
                    Inbox(last.Value.message, wallet);
                }
                else
                    emotingSubjects.Remove(last.Key);
            }
        }

        private void PurgeQueues(uint subjectId)
        {
            lastMovementMessages.Remove(subjectId);
            pendingResyncs.Remove(subjectId);
            emotingSubjects.Remove(subjectId);
            profileVersions.Remove(subjectId);
            realmProbes.Remove(subjectId);
        }

        private void Inbox(NetworkMovementMessage fullMovementMessage, string @for)
        {
            movementInbox.Enqueue(fullMovementMessage, @for);
        }
    }
}
