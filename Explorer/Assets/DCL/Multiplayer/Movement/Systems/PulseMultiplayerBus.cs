using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Movement;
using DCL.Multiplayer.Movement.Systems;
using DCL.Multiplayer.Profiles.RemoteAnnouncements;
using DCL.Multiplayer.Profiles.RemoveIntentions;
using DCL.Utilities.Extensions;
using DCL.Web3.Identities;
using System;
using System.Threading;

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

        private bool isDisposed;

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
        }

        public void SubscribeToIncomingMessages(CancellationToken ct)
        {
            UniTask.WhenAll(SubscribeToPlayerJoinedAsync(ct),
                        SubscribeToTeleportsAsync(ct),
                        SubscribeToPlayerStateFullAsync(ct),
                        SubscribeToPlayerStateDeltaAsync(ct),
                        SubscribeToProfileAnnouncementsAsync(ct),
                        SubscribeToPlayerLeftAsync(ct),
                        SubscribeToEmoteStartedAsync(ct),
                        SubscribeToEmoteStoppedAsync(ct),
                        MonitorDisconnectsAsync(ct))
                   .SuppressToResultAsync(ReportCategory.MULTIPLAYER)
                   .Forget();
        }

        private void RemoveAllPeers()
        {
            foreach (string wallet in peerIdCache.Wallets)
                removeIntentions.Enqueue(wallet);

            peerIdCache.Clear();
            lastMovementMessages.Clear();
            pendingResyncs.Clear();
        }

        private void Inbox(NetworkMovementMessage fullMovementMessage, string @for)
        {
            movementInbox.TryEnqueue(fullMovementMessage, @for);
        }
    }
}
