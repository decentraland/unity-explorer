using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Utilities;
using DCL.Web3.Identities;
using JetBrains.Annotations;
using LiveKit.Rooms;
using LiveKit.Rooms.Participants;
using LiveKit.Proto;
using System;
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
        [SuppressMessage("ReSharper", "InconsistentNaming")]
        public enum UserCommunityRoleMetadata
        {
            none,
            user,
            moderator,
            owner,
        }

        public delegate void ParticipantJoinedDelegate(string participantId, ParticipantState participantState);
        public delegate void ParticipantLeftDelegate(string participantId);
        public delegate void SpeakersUpdatedDelegate(int speakers);
        public delegate void ParticipantsStateRefreshDelegate(List<(string participantId, ParticipantState state)> joinedParticipants, List<string> leftParticipantIds);
        private const string TAG = nameof(VoiceChatParticipantsStateService);
        private const int MAX_CONNECTION_UPDATES = 3;

        private readonly IRoom voiceChatRoom;
        private readonly IWeb3IdentityCache identityCache;

        private readonly HashSet<string> connectedParticipants = new ();
        private readonly Dictionary<string, ParticipantState> participantStates = new ();
        private readonly Dictionary<string, ReactiveProperty<bool>> onlineStatus = new ();
        private readonly HashSet<string> speakers = new ();

        private bool isDisposed;
        private HashSet<string> activeSpeakers = new ();
        private int connectionUpdateCounter;

        public IReadOnlyCollection<string> ConnectedParticipants => connectedParticipants;
        public IReadOnlyCollection<string> Speakers => speakers;

        public string LocalParticipantId { get; private set; }

        public ParticipantState LocalParticipantState { get; private set; }

        /// <summary>
        ///     Raised when a new participant joins the voice chat room.
        /// </summary>
        public event ParticipantJoinedDelegate? ParticipantJoined;

        /// <summary>
        ///     Raised when a participant leaves the voice chat room.
        /// </summary>
        public event ParticipantLeftDelegate? ParticipantLeft;

        /// <summary>
        ///     Raised when participant states are refreshed after connection or reconnection.
        ///     Provides lists of newly joined participants and participants that have left.
        /// </summary>
        public event ParticipantsStateRefreshDelegate? ParticipantsStateRefreshed;

        public event SpeakersUpdatedDelegate? SpeakersUpdated;

        public VoiceChatParticipantsStateService(
            IRoom voiceChatRoom,
            IWeb3IdentityCache identityCache)
        {
            this.voiceChatRoom = voiceChatRoom;
            this.identityCache = identityCache;

            voiceChatRoom.Participants.UpdatesFromParticipant += OnParticipantUpdated;
            voiceChatRoom.ActiveSpeakers.Updated += OnActiveSpeakersUpdated;
            voiceChatRoom.ConnectionUpdated += OnConnectionUpdated;

            identityCache.OnIdentityChanged += OnIdentityChanged;
            identityCache.OnIdentityCleared += OnIdentityCleared;

            LocalParticipantId = identityCache.Identity?.Address ?? string.Empty;

            LocalParticipantState = new ParticipantState(
                LocalParticipantId,
                new ReactiveProperty<bool>(false),
                new ReactiveProperty<string?>(null),
                new ReactiveProperty<bool?>(false),
                new ReactiveProperty<string?>(null),
                new ReactiveProperty<bool>(false),
                new ReactiveProperty<bool>(false),
                new ReactiveProperty<UserCommunityRoleMetadata>(UserCommunityRoleMetadata.none),
                new ReactiveProperty<bool>(false)
            );
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

            foreach (ParticipantState state in participantStates.Values) { DisposeParticipantState(state); }

            participantStates.Clear();

            DisposeParticipantState(LocalParticipantState);

            foreach (ReactiveProperty<bool>? status in onlineStatus.Values) { status.ClearSubscriptionsList(); }

            onlineStatus.Clear();

            ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Disposed");
        }

        private void SetOnlineStatus(string participantId, bool isOnline)
        {
            if (!onlineStatus.TryGetValue(participantId, out ReactiveProperty<bool> status))
            {
                status = new ReactiveProperty<bool>(isOnline);
                onlineStatus[participantId] = status;
            }
            else { status.Value = isOnline; }
        }

        public ParticipantState? GetParticipantState(string participantId) =>
            participantId == LocalParticipantId ? LocalParticipantState : participantStates.GetValueOrDefault(participantId);

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
                            ParticipantState state;

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
                        OnParticipantMetadataChanged(participant.Identity, participant.Metadata);
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

                    if (!activeSpeakers.Contains(speakerId)) { UpdateParticipantSpeaking(speakerId, true); }
                }

                foreach (string oldSpeakerId in activeSpeakers)
                {
                    if (!newActiveSpeakers.Contains(oldSpeakerId)) { UpdateParticipantSpeaking(oldSpeakerId, false); }
                }

                HashSetPool<string>.Release(activeSpeakers);
                activeSpeakers = newActiveSpeakers;
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

        private void OnParticipantMetadataChanged(string participantId, string metadata)
        {
            ParticipantCallMetadata? callMetadata = ParseParticipantMetadata(participantId, metadata);

            if (!callMetadata.HasValue) return;

            var parsedMetadata = callMetadata.Value;
                    ParticipantState? participantState = GetParticipantState(participantId);

                    if (participantState == null) return;

                    participantState.WalletId = participantId;
                    participantState.Name.Value = parsedMetadata.name;
                    participantState.HasClaimedName.Value = parsedMetadata.hasClaimedName;
                    participantState.ProfilePictureUrl.Value = parsedMetadata.profilePictureUrl;
                    participantState.IsRequestingToSpeak.Value = parsedMetadata.isRequestingToSpeak;
                    participantState.IsSpeaker.Value = parsedMetadata.isSpeaker;
                    participantState.Role.Value = parsedMetadata.Role;

                    if (parsedMetadata.isSpeaker)
                        speakers.Add(participantId);
                    else
                        speakers.Remove(participantId);
        }

        private void OnIdentityChanged()
        {
            string newIdentityId = identityCache.Identity?.Address ?? string.Empty;
            LocalParticipantId = newIdentityId;
            LocalParticipantState.WalletId = newIdentityId;
            ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Identity changed, updated LocalParticipantId to: {LocalParticipantId}");
        }

        private void OnIdentityCleared()
        {
            LocalParticipantId = string.Empty;
            LocalParticipantState.WalletId = string.Empty;
            ResetLocalParticipantState();
            ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Identity cleared, reset LocalParticipantId and state");
        }

        private ParticipantState CreateParticipantState(Participant participant)
        {
            ParticipantCallMetadata? metadata = ParseParticipantMetadata(participant.Identity, participant.Metadata);

            var state = new ParticipantState(
                participant.Identity,
                new ReactiveProperty<bool>(false),
                new ReactiveProperty<string?>(metadata?.name),
                new ReactiveProperty<bool?>(metadata?.hasClaimedName ?? false),
                new ReactiveProperty<string?>(metadata?.profilePictureUrl),
                new ReactiveProperty<bool>(metadata?.isRequestingToSpeak ?? false),
                new ReactiveProperty<bool>(metadata?.isSpeaker ?? false),
                new ReactiveProperty<UserCommunityRoleMetadata>(metadata?.Role ?? UserCommunityRoleMetadata.none),
                new ReactiveProperty<bool>(false)
            );

            participantStates[participant.Identity] = state;
            return state;
        }

        private void RemoveParticipantState(string participantId)
        {
            if (participantStates.TryGetValue(participantId, out ParticipantState state))
            {
                DisposeParticipantState(state);
                participantStates.Remove(participantId);
            }
        }

        private void DisposeParticipantState(ParticipantState state)
        {
            state.IsSpeaking.ClearSubscriptionsList();
            state.Name.ClearSubscriptionsList();
            state.HasClaimedName.ClearSubscriptionsList();
            state.ProfilePictureUrl.ClearSubscriptionsList();
            state.IsRequestingToSpeak.ClearSubscriptionsList();
            state.IsSpeaker.ClearSubscriptionsList();
            state.Role.ClearSubscriptionsList();
        }

        private void ClearAllParticipantStates()
        {
            foreach (ParticipantState state in participantStates.Values) { DisposeParticipantState(state); }

            participantStates.Clear();
            ResetLocalParticipantState();
        }

        private void RefreshAllParticipantStates()
        {
            var currentParticipants = new List<Participant>();

            foreach (var participantId in voiceChatRoom.Participants.RemoteParticipantIdentities())
            {
                currentParticipants.Add(participantId.Value);
            }

            var participantsToRemove = new List<string>();

            foreach (string participantId in participantStates.Keys)
            {
                if (voiceChatRoom.Participants.RemoteParticipant(participantId) == null) { participantsToRemove.Add(participantId); }
            }

            foreach (string participantId in participantsToRemove)
            {
                RemoveParticipantState(participantId);
                connectedParticipants.Remove(participantId);
                activeSpeakers.Remove(participantId);
                ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Removed disconnected participant during refresh: {participantId}");
            }

            var joinedParticipants = new List<(string participantId, ParticipantState state)>();

            foreach (Participant participant in currentParticipants)
            {
                if (participantStates.TryGetValue(participant.Identity, out ParticipantState existingState)) { RefreshParticipantState(participant, existingState); }
                else
                {
                    ParticipantState state = CreateParticipantState(participant);
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

        private void RefreshParticipantState(Participant participant, ParticipantState existingState)
        {
            RefreshParticipantStateFromMetadata(participant, existingState);
        }

        private void RefreshParticipantStateFromMetadata(Participant participant, ParticipantState existingState)
        {
            ParticipantCallMetadata? metadata = ParseParticipantMetadata(participant.Identity, participant.Metadata);

            existingState.Name.Value = metadata?.name;
            existingState.HasClaimedName.Value = metadata?.hasClaimedName ?? false;
            existingState.ProfilePictureUrl.Value = metadata?.profilePictureUrl;
            existingState.IsRequestingToSpeak.Value = metadata?.isRequestingToSpeak ?? false;
            existingState.IsSpeaker.Value = metadata?.isSpeaker ?? false;
            existingState.Role.Value = metadata?.Role ?? UserCommunityRoleMetadata.none;

            if (existingState.IsSpeaker.Value)
                speakers.Add(existingState.WalletId);
            else
                speakers.Remove(existingState.WalletId);
        }

        private void UpdateParticipantSpeaking(string participantId, bool isSpeaking)
        {
            ParticipantState? participantState = GetParticipantState(participantId);

            if (participantState != null) { participantState.IsSpeaking.Value = isSpeaking; }
        }

        private void ResetLocalParticipantState()
        {
            LocalParticipantState.IsSpeaking.Value = false;
            LocalParticipantState.Name.Value = null;
            LocalParticipantState.HasClaimedName.Value = false;
            LocalParticipantState.ProfilePictureUrl.Value = null;
            LocalParticipantState.IsRequestingToSpeak.Value = false;
            LocalParticipantState.IsSpeaker.Value = false;
            LocalParticipantState.Role.Value = UserCommunityRoleMetadata.none;
            ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Reset local participant state to defaults");
        }

        public class ParticipantState
        {
            public string WalletId { get; set; }
            public ReactiveProperty<bool> IsSpeaking { get; set; }
            public ReactiveProperty<string?> Name { get; set; }
            public ReactiveProperty<bool?> HasClaimedName { get; set; }
            public ReactiveProperty<string?> ProfilePictureUrl { get; set; }
            public ReactiveProperty<bool> IsRequestingToSpeak { get; set; }
            public ReactiveProperty<bool> IsSpeaker { get; set; }
            public ReactiveProperty<bool> IsMuted { get; set; }
            public ReactiveProperty<UserCommunityRoleMetadata> Role { get; set; }

            public ParticipantState(string walletId, ReactiveProperty<bool> isSpeaking, ReactiveProperty<string?> name, ReactiveProperty<bool?> hasClaimedName, ReactiveProperty<string?> profilePictureUrl,
                ReactiveProperty<bool> isRequestingToSpeak, ReactiveProperty<bool> isSpeaker, ReactiveProperty<UserCommunityRoleMetadata> role, ReactiveProperty<bool> isMuted)
            {
                WalletId = walletId;
                IsSpeaking = isSpeaking;
                Name = name;
                HasClaimedName = hasClaimedName;
                ProfilePictureUrl = profilePictureUrl;
                IsRequestingToSpeak = isRequestingToSpeak;
                IsSpeaker = isSpeaker;
                Role = role;
                IsMuted = isMuted;
            }
        }

        [Serializable]
        [UsedImplicitly]
        [SuppressMessage("ReSharper", "InconsistentNaming")]
        public struct ParticipantCallMetadata
        {
            public string? name;
            public string? profilePictureUrl;
            public bool hasClaimedName;
            public bool isRequestingToSpeak;
            public bool isSpeaker;
            public string role;

            public UserCommunityRoleMetadata Role => ParseRole(role);

            private static UserCommunityRoleMetadata ParseRole(string? roleString)
            {
                if (string.IsNullOrEmpty(roleString))
                    return UserCommunityRoleMetadata.none;

                return roleString.ToLowerInvariant() switch
                       {
                           "user" => UserCommunityRoleMetadata.user,
                           "moderator" => UserCommunityRoleMetadata.moderator,
                           "owner" => UserCommunityRoleMetadata.owner,
                           _ => UserCommunityRoleMetadata.none,
                       };
            }

            public override string ToString() =>
                $"(Name: {name}, HasClaimedName: {hasClaimedName}, ProfilePictureUrl: {profilePictureUrl}, IsRequestingToSpeak: {isRequestingToSpeak}, IsSpeaker: {isSpeaker}, Role: {Role})";
        }
    }
}
