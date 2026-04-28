using DCL.Multiplayer.Connections.Pulse;
using DCL.Multiplayer.Emotes;
using DCL.Multiplayer.Profiles.Announcements;
using DCL.Multiplayer.Profiles.RemoveIntentions;
using DCL.Profiles.Self;
using DCL.Web3;
using DCL.Web3.Identities;
using Decentraland.Pulse;
using System;

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
            ISelfProfile selfProfile)
        {
            this.pulseService = pulseService;
            this.peerIdCache = peerIdCache;
            this.movementInbox = movementInbox;
            this.parcelEncoder = parcelEncoder;
            this.incomingProfiles = incomingProfiles;
            this.removeIntentions = removeIntentions;
            this.identityCache = identityCache;
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

        private void Inbox(NetworkMovementMessage fullMovementMessage, string @for)
        {
            movementInbox.Enqueue(fullMovementMessage, @for);
        }
    }
}
