using DCL.Diagnostics;
using DCL.Multiplayer.Movement;
using DCL.Multiplayer.Movement.Systems;
using DCL.Multiplayer.Profiles.RemoteAnnouncements;
using DCL.Multiplayer.Profiles.RemoveIntentions;
using DCL.Web3.Identities;
using Decentraland.Pulse;
using System;

namespace DCL.Multiplayer.Connections.Pulse
{
    public partial class PulseMultiplayerBus : IPlayerTeleportBroadcast, IDisposable
    {
        internal const string SELF_MIRROR_WALLET_ID = "self_mirror";

        private const double SERVER_TICKS_TO_MOVEMENT_TIMESTAMP = 0.001;

        private readonly PulseMultiplayerService pulseService;
        private readonly PeerIdCache peerIdCache;
        private readonly MovementInbox movementInbox;
        private readonly ParcelEncoder parcelEncoder;
        private readonly PulseIncomingProfileAnnouncements incomingProfiles;
        private readonly PulseRemoveIntentions removeIntentions;
        private readonly PulseEmotesMessageBus emotesMessageBus;
        private readonly IWeb3IdentityCache identityCache;

        private volatile bool isDisposed;

        public PulseMultiplayerBus(PulseMultiplayerService pulseService,
            PeerIdCache peerIdCache,
            MovementInbox movementInbox,
            ParcelEncoder parcelEncoder,
            PulseIncomingProfileAnnouncements incomingProfiles,
            PulseRemoveIntentions removeIntentions,
            PulseEmotesMessageBus emotesMessageBus,
            IWeb3IdentityCache identityCache)
        {
            this.pulseService = pulseService;
            this.peerIdCache = peerIdCache;
            this.movementInbox = movementInbox;
            this.parcelEncoder = parcelEncoder;
            this.incomingProfiles = incomingProfiles;
            this.removeIntentions = removeIntentions;
            this.emotesMessageBus = emotesMessageBus;
            this.identityCache = identityCache;
        }

        private string ResolveSelfMirrorWallet(string userId)
        {
            if (userId != SELF_MIRROR_WALLET_ID)
                return userId;

            return identityCache.EnsuredIdentity().Address.ToString();
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
        }

        private void RemoveAllPeers()
        {
            peerIdCache.RemoveAll(wallet => removeIntentions.Enqueue(wallet));

            lastMovementMessages.Clear();
            pendingResyncs.Clear();
        }

        private void Inbox(NetworkMovementMessage fullMovementMessage, string @for)
        {
            movementInbox.Enqueue(fullMovementMessage, @for);
        }
    }
}
