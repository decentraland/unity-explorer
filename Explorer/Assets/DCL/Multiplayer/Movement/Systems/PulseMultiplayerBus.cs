using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Emotes;
using DCL.Multiplayer.Movement;
using DCL.Multiplayer.Movement.Systems;
using DCL.Multiplayer.Profiles.RemoteAnnouncements;
using DCL.Multiplayer.Profiles.RemoveIntentions;
using DCL.Utilities.Extensions;
using System;
using System.Threading;

namespace DCL.Multiplayer.Connections.Pulse
{
    public partial class PulseMultiplayerBus : IDisposable
    {
        private const double SERVER_TICKS_TO_MOVEMENT_TIMESTAMP = 0.001;

        private readonly PulseMultiplayerService pulseService;
        private readonly PeerIdCache peerIdCache;
        private readonly MovementInbox movementInbox;
        private readonly ParcelEncoder parcelEncoder;
        private readonly PulseIncomingProfileAnnouncements incomingProfiles;
        private readonly PulseRemoveIntentions removeIntentions;
        private readonly PulseEmotesMessageBus emotesMessageBus;

        private bool isDisposed;

        public PulseMultiplayerBus(PulseMultiplayerService pulseService,
            PeerIdCache peerIdCache,
            MovementInbox movementInbox,
            ParcelEncoder parcelEncoder,
            PulseIncomingProfileAnnouncements incomingProfiles,
            PulseRemoveIntentions removeIntentions,
            PulseEmotesMessageBus emotesMessageBus)
        {
            this.pulseService = pulseService;
            this.peerIdCache = peerIdCache;
            this.movementInbox = movementInbox;
            this.parcelEncoder = parcelEncoder;
            this.incomingProfiles = incomingProfiles;
            this.removeIntentions = removeIntentions;
            this.emotesMessageBus = emotesMessageBus;
        }

        public void Dispose()
        {
            isDisposed = true;
        }

        public void SubscribeToIncomingMessages(CancellationToken ct)
        {
            UniTask.WhenAll(SubscribeToPlayerJoinedAsync(ct),
                        SubscribeToPlayerStateFullAsync(ct),
                        SubscribeToPlayerStateDeltaAsync(ct),
                        SubscribeToProfileAnnouncementsAsync(ct),
                        SubscribeToPlayerLeftAsync(ct),
                        SubscribeToEmoteStartedAsync(ct),
                        SubscribeToEmoteStoppedAsync(ct))
                   .SuppressToResultAsync(ReportCategory.MULTIPLAYER)
                   .Forget();
        }

        private void Inbox(NetworkMovementMessage fullMovementMessage, string @for)
        {
            movementInbox.TryEnqueue(fullMovementMessage, @for);
        }
    }
}