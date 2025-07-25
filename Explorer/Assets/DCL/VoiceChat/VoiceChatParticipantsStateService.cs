#nullable enable
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
        public delegate void ParticipantsStateRefreshDelegate(List<(string participantId, ParticipantState state)> joinedParticipants, List<string> leftParticipantIds);
        private const string TAG = nameof(VoiceChatParticipantsStateService);

        private readonly IRoom voiceChatRoom;
        private readonly IWeb3IdentityCache identityCache;

        private readonly HashSet<string> connectedParticipants = new ();
        private readonly Dictionary<string, ParticipantState> participantStates = new ();
        private readonly Dictionary<string, ReactiveProperty<bool>> onlineStatus = new ();

        private bool isDisposed;
        private HashSet<string> activeSpeakers = new ();

        public IReadOnlyCollection<string> ConnectedParticipants => connectedParticipants;
        public IReadOnlyCollection<string> ActiveSpeakers => activeSpeakers;

        public string LocalParticipantId { get; private set; }

        public ParticipantState LocalParticipantState { get; private set; }

        /// <summary>
        ///     Raised when a new participant joins the voice chat room.
        /// </summary>
        public event ParticipantJoinedDelegate ParticipantJoined;

        /// <summary>
        ///     Raised when a participant leaves the voice chat room.
        /// </summary>
        public event ParticipantLeftDelegate ParticipantLeft;

        /// <summary>
        ///     Raised when participant states are refreshed after connection or reconnection.
        ///     Provides lists of newly joined participants and participants that have left.
        /// </summary>
        public event ParticipantsStateRefreshDelegate ParticipantsStateRefreshed;

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
            CreateLocalParticipantState();
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

            foreach (ReactiveProperty<bool>? status in onlineStatus.Values) { status.Dispose(); }

            onlineStatus.Clear();

            ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Disposed");
        }

        /// <summary>
        ///     Gets the online status for a participant. Creates a new reactive property if it doesn't exist.
        /// </summary>
        public IReadonlyReactiveProperty<bool> GetOnlineStatus(string participantId)
        {
            if (!onlineStatus.TryGetValue(participantId, out ReactiveProperty<bool> status))
            {
                status = new ReactiveProperty<bool>(false);
                onlineStatus[participantId] = status;
            }

            return status;
        }

        /// <summary>
        ///     Sets the online status for a participant. Creates a new reactive property if it doesn't exist.
        /// </summary>
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

        /// <summary>
        ///     Gets both online status and participant state in one call.
        /// </summary>
        public (IReadonlyReactiveProperty<bool> IsOnline, ParticipantState? State) GetParticipantInfo(string participantId) =>
            (GetOnlineStatus(participantId), GetParticipantState(participantId));

        private void OnParticipantUpdated(Participant participant, UpdateFromParticipant update)
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
                        RemoveParticipantState(participant.Identity);
                        ParticipantLeft?.Invoke(participant.Identity);
                        activeSpeakers.Remove(participant.Identity);
                        ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Participant left: {participant.Identity}");
                    }

                    break;
            }
        }

        private void OnActiveSpeakersUpdated()
        {
            var newActiveSpeakers = new HashSet<string>();

            foreach (string speakerId in voiceChatRoom.ActiveSpeakers)
            {
                newActiveSpeakers.Add(speakerId);

                if (!activeSpeakers.Contains(speakerId)) { UpdateParticipantSpeaking(speakerId, true); }
            }

            foreach (string oldSpeakerId in activeSpeakers)
            {
                if (!newActiveSpeakers.Contains(oldSpeakerId)) { UpdateParticipantSpeaking(oldSpeakerId, false); }
            }

            activeSpeakers = newActiveSpeakers;
        }

        private void OnConnectionUpdated(IRoom room, ConnectionUpdate connectionUpdate, DisconnectReason? disconnectReason = null)
        {
            switch (connectionUpdate)
            {
                case ConnectionUpdate.Connected:
                case ConnectionUpdate.Reconnected:
                    RefreshAllParticipantStates();
                    ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Connection {connectionUpdate}, refreshed participant states");
                    break;

                case ConnectionUpdate.Disconnected:
                    HandleDisconnection(disconnectReason);
                    break;
            }
        }

        private void HandleDisconnection(DisconnectReason? disconnectReason)
        {
            bool shouldClearData = ShouldClearDataOnDisconnect(disconnectReason);

            if (shouldClearData)
            {
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
                ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Raw metadata for {participantId}: {metadataJson}");
                ParticipantCallMetadata metadata = JsonUtility.FromJson<ParticipantCallMetadata>(metadataJson);
                ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Parsed metadata for {participantId}: {metadata}");
                return metadata;
            }
            catch (Exception e)
            {
                ReportHub.LogWarning(ReportCategory.VOICE_CHAT, $"{TAG} Failed to parse metadata for {participantId}: {e.Message}");
                return null;
            }
        }

        private void ApplyMetadataToParticipantState(ParticipantState participantState, string participantId, ParticipantCallMetadata metadata)
        {
            participantState.WalletId = participantId;
            participantState.Name.Value = metadata.name;
            participantState.HasClaimedName.Value = metadata.hasClaimedName;
            participantState.ProfilePictureUrl.Value = metadata.profilePictureUrl;
            participantState.IsRequestingToSpeak.Value = metadata.isRequestingToSpeak;
            participantState.IsSpeaker.Value = metadata.isSpeaker;
            participantState.Role.Value = metadata.role;
        }

        private bool ShouldClearDataOnDisconnect(DisconnectReason? disconnectReason)
        {
            if (!disconnectReason.HasValue)
                return false;

            return disconnectReason.Value switch
                   {
                       DisconnectReason.RoomDeleted => true,
                       DisconnectReason.RoomClosed => true,
                       DisconnectReason.ParticipantRemoved => true,
                       DisconnectReason.DuplicateIdentity => true,
                       DisconnectReason.ServerShutdown => true,
                       DisconnectReason.ClientInitiated => true,
                       DisconnectReason.JoinFailure => true,
                       DisconnectReason.UserRejected => true,
                       DisconnectReason.SignalClose => true,
                       DisconnectReason.ConnectionTimeout => true,
                       DisconnectReason.StateMismatch => false,
                       DisconnectReason.Migration => false,
                       DisconnectReason.UnknownReason => false,
                       DisconnectReason.UserUnavailable => false,
                       DisconnectReason.SipTrunkFailure => false,
                       _ => false,
                   };
        }

        private void OnParticipantMetadataChanged(string participantId, string metadata)
        {
            ParticipantCallMetadata? callMetadata = ParseParticipantMetadata(participantId, metadata);

            if (callMetadata.HasValue) { UpdateParticipantMetadata(participantId, callMetadata.Value); }
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

        public Participant? GetParticipant(string identity) =>
            voiceChatRoom.Participants.RemoteParticipant(identity);

        public IReadOnlyCollection<string> GetRemoteParticipantIdentities() =>
            voiceChatRoom.Participants.RemoteParticipantIdentities();

        public bool IsParticipantSpeaking(string participantId) =>
            activeSpeakers.Contains(participantId);

        public bool IsParticipantConnected(string participantId) =>
            connectedParticipants.Contains(participantId);

        public bool IsParticipantSpeaker(string participantId) =>
            GetParticipantState(participantId)?.IsSpeaker.Value ?? false;

        private ParticipantState CreateParticipantState(Participant participant)
        {
            ParticipantCallMetadata? metadata = ParseParticipantMetadata(participant.Identity, participant.Metadata);

            var state = new ParticipantState(
                participant.Identity,
                new ReactiveProperty<bool>(false),
                new ReactiveProperty<string?>(metadata?.name),
                new ReactiveProperty<bool?>(metadata?.hasClaimedName ?? false),
                new ReactiveProperty<string?>(metadata?.profilePictureUrl),
                new ReactiveProperty<bool?>(metadata?.isRequestingToSpeak ?? false),
                new ReactiveProperty<bool>(metadata?.isSpeaker ?? false),
                new ReactiveProperty<UserCommunityRoleMetadata>(metadata?.role ?? UserCommunityRoleMetadata.none)
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
            state.IsSpeaking?.Dispose();
            state.Name?.Dispose();
            state.HasClaimedName?.Dispose();
            state.ProfilePictureUrl?.Dispose();
            state.IsRequestingToSpeak?.Dispose();
            state.IsSpeaker?.Dispose();
            state.Role?.Dispose();
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

            Participant localParticipant = voiceChatRoom.Participants.LocalParticipant();

            currentParticipants.Add(localParticipant);

            foreach (string participantId in voiceChatRoom.Participants.RemoteParticipantIdentities())
            {
                Participant participant = voiceChatRoom.Participants.RemoteParticipant(participantId);
                currentParticipants.Add(participant);
            }

            var participantsToRemove = new List<string>();

            foreach (string participantId in participantStates.Keys)
            {
                if (voiceChatRoom.Participants.RemoteParticipant(participantId) == null) { participantsToRemove.Add(participantId); }
            }

            string localParticipantId = localParticipant.Identity;
            participantsToRemove.RemoveAll(id => id == localParticipantId);

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
                if (participant.Identity == LocalParticipantId)
                {
                    RefreshParticipantStateFromMetadata(participant, LocalParticipantState);
                    ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Refreshed local participant state during reconnection");
                }
                else if (participantStates.TryGetValue(participant.Identity, out ParticipantState existingState)) { RefreshParticipantState(participant, existingState); }
                else
                {
                    ParticipantState state = CreateParticipantState(participant);
                    connectedParticipants.Add(participant.Identity);
                    joinedParticipants.Add((participant.Identity, state));
                    ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Participant joined during refresh: {participant.Identity}");
                }
            }

            if (joinedParticipants.Count > 0 || participantsToRemove.Count > 0) { ParticipantsStateRefreshed?.Invoke(joinedParticipants, participantsToRemove); }
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
            existingState.Role.Value = metadata?.role ?? UserCommunityRoleMetadata.none;
        }

        private void UpdateParticipantSpeaking(string participantId, bool isSpeaking)
        {
            ParticipantState? participantState = GetParticipantState(participantId);

            if (participantState != null) { participantState.IsSpeaking.Value = isSpeaking; }
        }

        private void UpdateParticipantMetadata(string participantId, ParticipantCallMetadata metadata)
        {
            ParticipantState? participantState = GetParticipantState(participantId);

            if (participantState != null) { ApplyMetadataToParticipantState(participantState, participantId, metadata); }
        }

        private void CreateLocalParticipantState()
        {
            LocalParticipantState = new ParticipantState(
                LocalParticipantId,
                new ReactiveProperty<bool>(false),
                new ReactiveProperty<string?>(null),
                new ReactiveProperty<bool?>(false),
                new ReactiveProperty<string?>(null),
                new ReactiveProperty<bool?>(false),
                new ReactiveProperty<bool>(false),
                new ReactiveProperty<UserCommunityRoleMetadata>(UserCommunityRoleMetadata.none)
            );

            ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Created local participant state for {LocalParticipantId}");
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
            public ReactiveProperty<bool?> IsRequestingToSpeak { get; set; }
            public ReactiveProperty<bool> IsSpeaker { get; set; }
            public ReactiveProperty<UserCommunityRoleMetadata> Role { get; set; }

            public ParticipantState(string walletId, ReactiveProperty<bool> isSpeaking, ReactiveProperty<string?> name, ReactiveProperty<bool?> hasClaimedName, ReactiveProperty<string?> profilePictureUrl,
                ReactiveProperty<bool?> isRequestingToSpeak, ReactiveProperty<bool> isSpeaker, ReactiveProperty<UserCommunityRoleMetadata> role)
            {
                WalletId = walletId;
                IsSpeaking = isSpeaking;
                Name = name;
                HasClaimedName = hasClaimedName;
                ProfilePictureUrl = profilePictureUrl;
                IsRequestingToSpeak = isRequestingToSpeak;
                IsSpeaker = isSpeaker;
                Role = role;
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
            public UserCommunityRoleMetadata role;

            public override string ToString() =>
                $"(Name: {name}, HasClaimedName: {hasClaimedName}, ProfilePictureUrl: {profilePictureUrl}, IsRequestingToSpeak: {isRequestingToSpeak}, IsSpeaker: {isSpeaker}, Role: {role})";
        }
    }
}
