using Arch.Core;
using DCL.Multiplayer.Profiles.Tables;
using DCL.Utilities;
using LiveKit.Rooms;
using LiveKit.Rooms.Participants;
using System;
using Utility.Arch;

namespace DCL.VoiceChat.Nearby
{
    /// <summary>
    /// Bridges Island Room active-speaker events to <see cref="VoiceChatNametagComponent"/> on avatar entities
    /// for the Nearby (proximity) voice chat stack. Handles both remote players (via <see cref="ActiveSpeakersDiffTracker"/>)
    /// and the local player directly. Sets <see cref="VoiceChatNametagComponent.IsHushed"/> for muted speakers so the
    /// nametag shows the hushed icon only while they are speaking. Suppresses nametag indicators while a Private or
    /// Community call is active, or while Nearby voice chat is SUPPRESSED/DISABLED.
    /// </summary>
    public class NearbyVoiceChatNametagsHandler : IDisposable
    {
        private readonly IRoom islandRoom;
        private readonly IReadOnlyEntityParticipantTable entityParticipantTable;
        private readonly World world;
        private readonly Entity playerEntity;
        private readonly NearbyMuteService muteService;
        private readonly ActiveSpeakersDiffTracker tracker;
        private readonly IDisposable callStatusSubscription;
        private readonly ReactivePropertyExtensions.DisposableSubscription<NearbyVoiceChatState> nearbyStateSubscription;

        private bool disposed;
        private bool suppressed;
        private bool disconnected;
        private bool localPlayerSpeaking;

        public NearbyVoiceChatNametagsHandler(
            IRoom islandRoom,
            IReadOnlyEntityParticipantTable entityParticipantTable,
            World world,
            IReadonlyReactiveProperty<VoiceChatStatus> callStatus,
            Entity playerEntity,
            NearbyMuteService muteService,
            NearbyVoiceChatStateModel nearbyStateModel)
        {
            this.islandRoom = islandRoom;
            this.entityParticipantTable = entityParticipantTable;
            this.world = world;
            this.playerEntity = playerEntity;
            this.muteService = muteService;

            tracker = new ActiveSpeakersDiffTracker(entityParticipantTable, world);

            islandRoom.ActiveSpeakers.Updated += OnActiveSpeakersUpdated;
            islandRoom.Participants.UpdatesFromParticipant += OnParticipantUpdated;
            callStatusSubscription = callStatus.Subscribe(OnCallStatusChanged);
            muteService.MuteStateChanged += OnMuteStateChanged;
            nearbyStateSubscription = nearbyStateModel.State.Subscribe(OnNearbyStateChanged);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;

            callStatusSubscription.Dispose();
            nearbyStateSubscription.Dispose();
            islandRoom.ActiveSpeakers.Updated -= OnActiveSpeakersUpdated;
            islandRoom.Participants.UpdatesFromParticipant -= OnParticipantUpdated;
            muteService.MuteStateChanged -= OnMuteStateChanged;

            tracker.MarkAllRemoving();
            MarkLocalPlayerRemoving();
        }

        private bool IsSuppressed => suppressed || disconnected;

        private void OnActiveSpeakersUpdated()
        {
            if (IsSuppressed) return;

            tracker.Update(islandRoom.ActiveSpeakers);
            ApplyHushedStateToActiveSpeakers();
            UpdateLocalPlayerSpeakingState();
        }

        private void ApplyHushedStateToActiveSpeakers()
        {
            foreach (string speakerId in islandRoom.ActiveSpeakers)
            {
                if (!muteService.IsMuted(speakerId)) continue;
                if (!entityParticipantTable.TryGet(speakerId, out IReadOnlyEntityParticipantTable.Entry entry)) continue;

                world.AddOrSet(entry.Entity, new VoiceChatNametagComponent(true, isHushed: true));
            }
        }

        private void UpdateLocalPlayerSpeakingState()
        {
            string? localIdentity = islandRoom.Participants.LocalParticipant()?.Identity;

            bool isSpeaking = !string.IsNullOrEmpty(localIdentity) && IsIdentityAmongActiveSpeakers(localIdentity);

            if (isSpeaking == localPlayerSpeaking)
                return;

            localPlayerSpeaking = isSpeaking;
            world.AddOrSet(playerEntity, new VoiceChatNametagComponent(isSpeaking));
        }

        private void OnMuteStateChanged(string walletId, bool isMuted)
        {
            if (IsSuppressed) return;
            if (!IsIdentityAmongActiveSpeakers(walletId)) return;

            if (entityParticipantTable.TryGet(walletId, out IReadOnlyEntityParticipantTable.Entry entry))
                world.AddOrSet(entry.Entity, new VoiceChatNametagComponent(true, isHushed: isMuted));
        }

        private bool IsIdentityAmongActiveSpeakers(string identity)
        {
            foreach (string speakerId in islandRoom.ActiveSpeakers)
            {
                if (speakerId == identity)
                    return true;
            }

            return false;
        }

        private void MarkLocalPlayerRemoving()
        {
            localPlayerSpeaking = false;
            world.AddOrSet(playerEntity, new VoiceChatNametagComponent(false) { IsRemoving = true });
        }

        private void OnParticipantUpdated(LKParticipant participant, UpdateFromParticipant update)
        {
            if (update == UpdateFromParticipant.Disconnected)
                tracker.RemoveParticipant(participant.Identity);
        }

        private void OnNearbyStateChanged(NearbyVoiceChatState nearbyState)
        {
            bool isNowDisconnected = nearbyState is NearbyVoiceChatState.SUPPRESSED or NearbyVoiceChatState.DISABLED;

            if (isNowDisconnected && !disconnected)
            {
                disconnected = true;
                tracker.MarkAllRemoving();
                MarkLocalPlayerRemoving();
            }
            else if (!isNowDisconnected && disconnected)
            {
                disconnected = false;

                if (!suppressed)
                {
                    tracker.Update(islandRoom.ActiveSpeakers);
                    ApplyHushedStateToActiveSpeakers();
                    UpdateLocalPlayerSpeakingState();
                }
            }
        }

        private void OnCallStatusChanged(VoiceChatStatus status)
        {
            if (status.IsInCall())
            {
                suppressed = true;
                tracker.MarkAllRemoving();
                MarkLocalPlayerRemoving();
            }
            else if (status.IsNotConnected())
            {
                suppressed = false;

                if (!disconnected)
                {
                    tracker.Update(islandRoom.ActiveSpeakers);
                    ApplyHushedStateToActiveSpeakers();
                    UpdateLocalPlayerSpeakingState();
                }
            }
        }
    }
}
