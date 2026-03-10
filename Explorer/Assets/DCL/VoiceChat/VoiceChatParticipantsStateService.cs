using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Profiles;
using DCL.Utilities;
using DCL.Web3.Identities;
using LiveKit.Rooms;
using LiveKit.Rooms.Participants;
using LiveKit.Proto;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using UnityEngine;
using UnityEngine.Pool;

namespace DCL.VoiceChat
{
    /// <summary>
    ///     Manages voice chat participant events and state, providing a centralized interface
    ///     for participant-related operations and notifications.
    /// </summary>
    public class VoiceChatParticipantsStateService : IDisposable
    {
        public delegate void ParticipantJoinedDelegate(string participantId, VoiceChatParticipantState participantState);
        public delegate void ParticipantsStateRefreshDelegate(List<(string participantId, VoiceChatParticipantState state)> joinedParticipants, List<string> leftParticipantIds);
        private const string TAG = nameof(VoiceChatParticipantsStateService);
        private const int MAX_CONNECTION_UPDATES = 3;

        private readonly IRoom voiceChatRoom;
        private readonly IWeb3IdentityCache identityCache;

        private readonly HashSet<string> connectedParticipants = new ();
        private readonly ConcurrentDictionary<string, VoiceChatParticipantState> participantStates = new ();
        private readonly ConcurrentDictionary<string, ReactiveProperty<bool>> onlineStatus = new ();
        private readonly HashSet<string> speakers = new ();

        private readonly List<Participant> currentParticipants = new();
        private readonly List<string> participantsToRemove = new();
        private readonly List<(string participantId, VoiceChatParticipantState state)> joinedParticipants = new();

        private bool isDisposed;
        private HashSet<string> activeSpeakers = new ();
        private int connectionUpdateCounter;

        public IReadOnlyCollection<string> ConnectedParticipants => connectedParticipants;
        public IReadOnlyCollection<string> Speakers => speakers;

        public string LocalParticipantId { get; private set; }
        public VoiceChatParticipantState LocalParticipantState { get; private set; }
        public event ParticipantJoinedDelegate? ParticipantJoined;
        public event Action<string>? ParticipantLeft;
        public event Action<int>? SpeakersUpdated;
        /// <summary>
        ///     Raised when participant states are refreshed after connection or reconnection.
        ///     Provides lists of newly joined participants and participants that have left.
        /// </summary>
        public event ParticipantsStateRefreshDelegate? ParticipantsStateRefreshed;

        public VoiceChatParticipantsStateService(
            IRoom voiceChatRoom,
            IWeb3IdentityCache identityCache
            )
        {
            this.voiceChatRoom = voiceChatRoom;
            this.identityCache = identityCache;

            voiceChatRoom.Participants.UpdatesFromParticipant += OnParticipantUpdated;
            voiceChatRoom.ActiveSpeakers.Updated += OnActiveSpeakersUpdated;
            voiceChatRoom.ConnectionUpdated += OnConnectionUpdated;

            identityCache.OnIdentityChanged += OnIdentityChanged;
            identityCache.OnIdentityCleared += OnIdentityCleared;

            LocalParticipantId = identityCache.Identity?.Address ?? string.Empty;

            LocalParticipantState = VoiceChatParticipantState.CreateDefault(LocalParticipantId);
        }

        public void Dispose()
        {
            if (isDisposed) return;

            isDisposed = true;

            voiceChatRoom.Participants.UpdatesFromParticipant -= OnParticipantUpdated;
            voiceChatRoom.ActiveSpeakers.Updated -= OnActiveSpeakersUpdated;
            voiceChatRoom.ConnectionUpdated -= OnConnectionUpdated;

            identityCache.OnIdentityChanged -= OnIdentityChanged;
            identityCache.OnIdentityCleared -= OnIdentityCleared;

            foreach (VoiceChatParticipantState state in participantStates.Values) { DisposeParticipantState(state); }

            participantStates.Clear();

            DisposeParticipantState(LocalParticipantState);

            foreach (ReactiveProperty<bool>? status in onlineStatus.Values) { status.ClearSubscriptionsList(); }

            onlineStatus.Clear();

            ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Disposed");
        }

        private void SetOnlineStatus(string participantId, bool isOnline)
        {
            if (string.IsNullOrEmpty(participantId)) return;

            if (!onlineStatus.TryGetValue(participantId, out ReactiveProperty<bool> status))
            {
                status = new ReactiveProperty<bool>(isOnline);
                onlineStatus[participantId] = status;
            }
            else { status.Value = isOnline; }
        }

        public bool TryGetParticipantState(string participantId, out VoiceChatParticipantState participantState)
        {
            if (participantId == LocalParticipantId)
            {
                participantState = LocalParticipantState;
                return true;
            }
            return participantStates.TryGetValue(participantId, out participantState);
        }

        private void OnParticipantUpdated(Participant participant, UpdateFromParticipant update)
        {
            if (!PlayerLoopHelper.IsMainThread)
            {
                OnParticipantUpdatedAsync().Forget();
                return;
            }

            OnParticipantUpdatedInternal();
            return;

            async UniTaskVoid OnParticipantUpdatedAsync()
            {
                await UniTask.SwitchToMainThread();
                OnParticipantUpdatedInternal();
            }

            void OnParticipantUpdatedInternal()
            {
                switch (update)
                {
                    case UpdateFromParticipant.Connected:
                        if (connectedParticipants.Add(participant.Identity))
                        {
                            VoiceChatParticipantState state;

                            if (participant.Identity == LocalParticipantId)
                            {
                                // Update local participant state
                                RefreshParticipantStateFromMetadata(participant, LocalParticipantState);
                                state = LocalParticipantState;
                            }
                            else { state = CreateParticipantState(participant); }

                            ParticipantJoined?.Invoke(participant.Identity, state);
                            SetOnlineStatus(participant.Identity, true);
                            ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Participant joined: {participant.Identity}");
                        }

                        break;

                    case UpdateFromParticipant.MetadataChanged:
                        ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Metadata changed for {participant.Identity}: {participant.Metadata}");

                        if (participant.Identity == LocalParticipantId)
                        {
                            RefreshParticipantStateFromMetadata(participant, LocalParticipantState);
                        }
                        else
                        {
                            if (TryGetParticipantState(participant.Identity, out VoiceChatParticipantState participantState))
                                RefreshParticipantStateFromMetadata(participant, participantState);
                        }
                        break;

                    case UpdateFromParticipant.Disconnected:
                        if (connectedParticipants.Remove(participant.Identity))
                        {
                            SetOnlineStatus(participant.Identity, false);
                            ParticipantLeft?.Invoke(participant.Identity);
                            activeSpeakers.Remove(participant.Identity);
                            speakers.Remove(participant.Identity);
                            ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Participant left: {participant.Identity}");
                        }

                        break;
                }

                SpeakersUpdated?.Invoke(speakers.Count);
            }
        }

        private void OnActiveSpeakersUpdated()
        {
            if (!PlayerLoopHelper.IsMainThread)
            {
                OnActiveSpeakersUpdatedAsync().Forget();
                return;
            }

            OnActiveSpeakersUpdatedInternal();
            return;

            async UniTaskVoid OnActiveSpeakersUpdatedAsync()
            {
                await UniTask.SwitchToMainThread();
                OnActiveSpeakersUpdatedInternal();
            }

            void OnActiveSpeakersUpdatedInternal()
            {
                var newActiveSpeakers = HashSetPool<string>.Get();

                foreach (string speakerId in voiceChatRoom.ActiveSpeakers)
                {
                    newActiveSpeakers.Add(speakerId);
                    UpdateParticipantSpeaking(speakerId, true);
                }

                var speakersToStop = HashSetPool<string>.Get();
                speakersToStop.UnionWith(activeSpeakers);
                speakersToStop.ExceptWith(newActiveSpeakers);

                foreach (string speakerId in speakersToStop)
                {
                    UpdateParticipantSpeaking(speakerId, false);
                }

                HashSetPool<string>.Release(speakersToStop);
                HashSetPool<string>.Release(activeSpeakers);
                activeSpeakers = newActiveSpeakers;

                return;

                void UpdateParticipantSpeaking(string participantId, bool isSpeaking)
                {
                    if (TryGetParticipantState(participantId, out VoiceChatParticipantState participantState))
                    {
                        participantState.IsSpeaking.Value = isSpeaking;
                    }
                }
            }
        }

        private void OnConnectionUpdated(IRoom room, ConnectionUpdate connectionUpdate, DisconnectReason? disconnectReason = null)
        {
            if (!PlayerLoopHelper.IsMainThread)
            {
                OnConnectionUpdatedAsync().Forget();
                return;
            }

            OnConnectionUpdatedInternal();
            return;

            async UniTaskVoid OnConnectionUpdatedAsync()
            {
                await UniTask.SwitchToMainThread();
                OnConnectionUpdatedInternal();
            }

            void OnConnectionUpdatedInternal()
            {
                switch (connectionUpdate)
                {
                    case ConnectionUpdate.Connected:
                        // We have this because the Connected event is sent several times as part of our reliability logic (to ensure updates reach all users despite Livekit)
                        connectionUpdateCounter++;

                        if (connectionUpdateCounter <= MAX_CONNECTION_UPDATES)
                        {
                            RefreshAllParticipantStates();
                            ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Connection {connectionUpdate} refreshed participant states");
                        }

                        break;
                    case ConnectionUpdate.Reconnected:
                        connectionUpdateCounter = 0;
                        RefreshAllParticipantStates();
                        ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Reconnection {connectionUpdate}, refreshed participant states");
                        break;
                    case ConnectionUpdate.Disconnected:
                        connectionUpdateCounter = 0;
                        HandleDisconnection(disconnectReason);
                        break;
                }
            }
        }

        private void HandleDisconnection(DisconnectReason? disconnectReason)
        {
            bool shouldClearData = VoiceChatDisconnectReasonHelper.IsValidDisconnectReason(disconnectReason);

            if (shouldClearData)
            {
                speakers.Clear();
                connectedParticipants.Clear();
                activeSpeakers.Clear();
                ClearAllParticipantStates();
                SetAllOnlineStatusesToFalse();
                ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Disconnected with reason {disconnectReason}, cleared all participant data and set online statuses to false");
            }
            else
            {
                SetAllOnlineStatusesToFalse();
                ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Disconnected with reason {disconnectReason}, keeping participant data for potential reconnection, set online statuses to false");
            }
        }

        private void SetAllOnlineStatusesToFalse()
        {
            foreach (ReactiveProperty<bool>? status in onlineStatus.Values) { status.Value = false; }
        }

        private ParticipantCallMetadata? ParseParticipantMetadata(string participantId, string? metadataJson)
        {
            if (string.IsNullOrEmpty(metadataJson))
                return null;

            try
            {
                ParticipantCallMetadata metadata = JsonUtility.FromJson<ParticipantCallMetadata>(metadataJson);
                return metadata;
            }
            catch (Exception e)
            {
                ReportHub.LogWarning(ReportCategory.VOICE_CHAT, $"{TAG} Failed to parse metadata for {participantId}: {e.Message}");
                return null;
            }
        }

        private void OnIdentityChanged()
        {
            string newIdentityId = identityCache.Identity?.Address ?? string.Empty;
            LocalParticipantId = newIdentityId;
            LocalParticipantState.Profile = new Profile.CompactInfo(newIdentityId);
            ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Identity changed, updated LocalParticipantId to: {LocalParticipantId}");
        }

        private void OnIdentityCleared()
        {
            LocalParticipantId = string.Empty;
            ResetLocalParticipantState();
            ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Identity cleared, reset LocalParticipantId and state");
        }

        private VoiceChatParticipantState CreateParticipantState(Participant participant)
        {
            var state = VoiceChatParticipantState.CreateDefault(participant.Identity);

            participantStates[participant.Identity] = state;

            RefreshParticipantStateFromMetadata(participant, state);

            return state;
        }

        private static void DisposeParticipantState(VoiceChatParticipantState state)
        {
            state.IsSpeaking.ClearSubscriptionsList();
            state.IsRequestingToSpeak.ClearSubscriptionsList();
            state.IsSpeaker.ClearSubscriptionsList();
            state.Role.ClearSubscriptionsList();
            state.IsMuted.ClearSubscriptionsList();
        }

        private void ClearAllParticipantStates()
        {
            foreach (VoiceChatParticipantState state in participantStates.Values) { DisposeParticipantState(state); }

            participantStates.Clear();
            ResetLocalParticipantState();
        }

        private void RefreshAllParticipantStates()
        {
            currentParticipants.Clear();
            participantsToRemove.Clear();
            joinedParticipants.Clear();

            foreach (var participantId in voiceChatRoom.Participants.RemoteParticipantIdentities())
            {
                currentParticipants.Add(participantId.Value);
            }

            foreach (string participantId in participantStates.Keys)
            {
                if (voiceChatRoom.Participants.RemoteParticipant(participantId) == null)
                    participantsToRemove.Add(participantId);
            }

            foreach (string participantId in participantsToRemove)
            {
                if (participantStates.Remove(participantId, out VoiceChatParticipantState state))
                    DisposeParticipantState(state);

                connectedParticipants.Remove(participantId);
                activeSpeakers.Remove(participantId);
                speakers.Remove(participantId);
                ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Removed disconnected participant during refresh: {participantId}");
            }

            foreach (Participant participant in currentParticipants)
            {
                if (participantStates.TryGetValue(participant.Identity, out VoiceChatParticipantState existingState))
                {
                    RefreshParticipantStateFromMetadata(participant, existingState);
                }
                else
                {
                    VoiceChatParticipantState state = CreateParticipantState(participant);
                    connectedParticipants.Add(participant.Identity);
                    joinedParticipants.Add((participant.Identity, state));
                    ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Participant joined during refresh: {participant.Identity}");
                }
            }

            Participant localParticipant = voiceChatRoom.Participants.LocalParticipant();
            RefreshParticipantStateFromMetadata(localParticipant, LocalParticipantState);
            ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Refreshed local participant state during reconnection");

            ParticipantsStateRefreshed?.Invoke(joinedParticipants, participantsToRemove);
            SpeakersUpdated?.Invoke(speakers.Count);
        }

        private void RefreshParticipantStateFromMetadata(Participant? participant, VoiceChatParticipantState? existingState)
        {
            if (participant == null || existingState == null) return;

            ParticipantCallMetadata? metadata = ParseParticipantMetadata(participant.Identity, participant.Metadata);

            if (!metadata.HasValue) return;

            UpdateParticipantStateFromMetadata(participant.Identity, metadata.Value, existingState);
        }

        private void UpdateParticipantStateFromMetadata(string participantId, ParticipantCallMetadata metadata, VoiceChatParticipantState participantState)
        {
            participantState.Profile = new Profile.CompactInfo(participantId, metadata.name!, metadata.hasClaimedName, metadata.profilePictureUrl!);
            participantState.IsRequestingToSpeak.Value = metadata.isRequestingToSpeak;
            participantState.IsSpeaker.Value = metadata.isSpeaker;
            participantState.Role.Value = metadata.Role;
            participantState.IsMuted.Value = metadata.muted;

            if (metadata.isSpeaker)
                speakers.Add(participantId);
            else
                speakers.Remove(participantId);
        }

        private void ResetLocalParticipantState()
        {
            LocalParticipantState.Profile = new Profile.CompactInfo(string.Empty);
            LocalParticipantState.IsSpeaking.Value = false;
            LocalParticipantState.IsRequestingToSpeak.Value = false;
            LocalParticipantState.IsSpeaker.Value = false;
            LocalParticipantState.IsMuted.Value = false;
            LocalParticipantState.Role.Value = VoiceChatParticipantCommunityRole.NONE;
            ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Reset local participant state to defaults");
        }

        [Serializable]
        [SuppressMessage("ReSharper", "InconsistentNaming")]
        public struct ParticipantCallMetadata
        {
            public string? name;
            public string? profilePictureUrl;
            public bool hasClaimedName;
            public bool isRequestingToSpeak;
            public bool isSpeaker;
            public bool muted;
            public string role;

            public VoiceChatParticipantCommunityRole Role => ParseRole(role);

            private static VoiceChatParticipantCommunityRole ParseRole(string? roleString)
            {
                if (string.IsNullOrEmpty(roleString))
                    return VoiceChatParticipantCommunityRole.NONE;

                return roleString.ToLowerInvariant() switch
                       {
                           "user" => VoiceChatParticipantCommunityRole.USER,
                           "moderator" => VoiceChatParticipantCommunityRole.MODERATOR,
                           "owner" => VoiceChatParticipantCommunityRole.OWNER,
                           _ => VoiceChatParticipantCommunityRole.NONE,
                       };
            }

            public override string ToString() =>
                $"(Name: {name}, HasClaimedName: {hasClaimedName}, ProfilePictureUrl: {profilePictureUrl}, IsRequestingToSpeak: {isRequestingToSpeak}, IsSpeaker: {isSpeaker}, Role: {Role}, IsMuted {muted})";
        }
    }
}
