using DCL.Diagnostics;
using DCL.Utilities;
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
        public delegate void ParticipantJoinedDelegate(string participantId, ParticipantState participantState);
        public delegate void ParticipantLeftDelegate(string participantId);
        public delegate void ParticipantsStateRefreshDelegate(List<(string participantId, ParticipantState state)> joinedParticipants, List<string> leftParticipantIds);
        private const string TAG = nameof(VoiceChatParticipantsStateService);

        private readonly IRoom voiceChatRoom;
        private readonly HashSet<string> connectedParticipants = new ();
        private readonly Dictionary<string, ParticipantState> participantStates = new ();
        private readonly Dictionary<string, ReactiveProperty<bool>> onlineStatus = new ();

        private bool isDisposed;
        private HashSet<string> activeSpeakers = new ();

        public IReadOnlyCollection<string> ConnectedParticipants => connectedParticipants;
        public IReadOnlyCollection<string> ActiveSpeakers => activeSpeakers;

        public string LocalParticipantId => voiceChatRoom.Participants.LocalParticipant().Identity;

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

        public VoiceChatParticipantsStateService(IRoom voiceChatRoom)
        {
            this.voiceChatRoom = voiceChatRoom;

            voiceChatRoom.Participants.UpdatesFromParticipant += OnParticipantUpdated;
            voiceChatRoom.ActiveSpeakers.Updated += OnActiveSpeakersUpdated;
            voiceChatRoom.ConnectionUpdated += OnConnectionUpdated;
        }

        public void Dispose()
        {
            if (isDisposed) return;
            isDisposed = true;

            voiceChatRoom.Participants.UpdatesFromParticipant -= OnParticipantUpdated;
            voiceChatRoom.ActiveSpeakers.Updated -= OnActiveSpeakersUpdated;
            voiceChatRoom.ConnectionUpdated -= OnConnectionUpdated;

            foreach (ParticipantState state in participantStates.Values) { DisposeParticipantState(state); }

            participantStates.Clear();

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
            participantStates.TryGetValue(participantId, out ParticipantState state) ? state : null;

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
                        ParticipantState state = CreateParticipantState(participant);
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
                ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Disconnected with reason {disconnectReason}, cleared all participant data");
            }
            else { ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Disconnected with reason {disconnectReason}, keeping participant data for potential reconnection"); }
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
            try
            {
                ParticipantCallMetadata callMetadata = JsonUtility.FromJson<ParticipantCallMetadata>(metadata);
                ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Parsed metadata for {participantId}: {callMetadata}");

                UpdateParticipantMetadata(participantId, callMetadata);
            }
            catch (Exception e) { ReportHub.LogError(ReportCategory.VOICE_CHAT, $"{TAG} Failed to parse metadata for {participantId}: {e.Message}"); }
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
            participantStates.TryGetValue(participantId, out ParticipantState state) && state.IsSpeaker.Value;

        private ParticipantState CreateParticipantState(Participant participant)
        {
            ParticipantCallMetadata? metadata = null;

            if (!string.IsNullOrEmpty(participant.Metadata))
            {
                try
                {
                    metadata = JsonUtility.FromJson<ParticipantCallMetadata>(participant.Metadata);
                    ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Parsed initial metadata for {participant.Identity}: {metadata}");
                }
                catch (Exception e) { ReportHub.LogError(ReportCategory.VOICE_CHAT, $"{TAG} Failed to parse initial metadata for {participant.Identity}: {e.Message}"); }
            }

            var state = new ParticipantState
            {
                WalletId = participant.Identity,
                IsSpeaking = new ReactiveProperty<bool>(false),
                Name = new ReactiveProperty<string?>(metadata?.name),
                HasClaimedName = new ReactiveProperty<bool?>(metadata?.hasClaimedName),
                ProfilePictureUrl = new ReactiveProperty<string?>(metadata?.profilePictureUrl),
                IsRequestingToSpeak = new ReactiveProperty<bool?>(metadata?.isRequestingToSpeak),
                IsSpeaker = new ReactiveProperty<bool>(metadata?.isSpeaker ?? false),
                Role = new ReactiveProperty<UserCommunityRoleMetadata>(metadata?.role ?? UserCommunityRoleMetadata.None)
            };

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
                if (participantStates.TryGetValue(participant.Identity, out ParticipantState existingState)) { RefreshParticipantState(participant, existingState); }
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
            ParticipantCallMetadata? metadata = null;

            if (!string.IsNullOrEmpty(participant.Metadata))
            {
                try { metadata = JsonUtility.FromJson<ParticipantCallMetadata>(participant.Metadata); }
                catch (Exception e) { ReportHub.LogError(ReportCategory.VOICE_CHAT, $"{TAG} Failed to parse metadata for {participant.Identity}: {e.Message}"); }
            }

            existingState.Name.Value = metadata?.name;
            existingState.HasClaimedName.Value = metadata?.hasClaimedName;
            existingState.ProfilePictureUrl.Value = metadata?.profilePictureUrl;
            existingState.IsRequestingToSpeak.Value = metadata?.isRequestingToSpeak;
            existingState.IsSpeaker.Value = metadata?.isSpeaker ?? false;
            existingState.Role.Value = metadata?.role ?? UserCommunityRoleMetadata.None;
        }

        private void UpdateParticipantSpeaking(string participantId, bool isSpeaking)
        {
            if (participantStates.TryGetValue(participantId, out ParticipantState state)) { state.IsSpeaking.Value = isSpeaking; }
        }

        private void UpdateParticipantMetadata(string participantId, ParticipantCallMetadata metadata)
        {
            if (participantStates.TryGetValue(participantId, out ParticipantState state))
            {
                state.WalletId = participantId;
                state.Name.Value = metadata.name;
                state.HasClaimedName.Value = metadata.hasClaimedName;
                state.ProfilePictureUrl.Value = metadata.profilePictureUrl;
                state.IsRequestingToSpeak.Value = metadata.isRequestingToSpeak;
                state.IsSpeaker.Value = metadata.isSpeaker ?? false;
                state.Role.Value = metadata.role;
            }
        }

        public struct ParticipantState
        {
            public string WalletId { get; set; }
            public ReactiveProperty<bool> IsSpeaking { get; set; }
            public ReactiveProperty<string?> Name { get; set; }
            public ReactiveProperty<bool?> HasClaimedName { get; set; }
            public ReactiveProperty<string?> ProfilePictureUrl { get; set; }
            public ReactiveProperty<bool?> IsRequestingToSpeak { get; set; }
            public ReactiveProperty<bool> IsSpeaker { get; set; }
            public ReactiveProperty<UserCommunityRoleMetadata> Role { get; set; }
        }

        public enum UserCommunityRoleMetadata
        {
            None,
            User,
            Moderator,
            Owner
        }

        [Serializable]
        [UsedImplicitly]
        [SuppressMessage("ReSharper", "InconsistentNaming")]
        public struct ParticipantCallMetadata
        {
            public string? name;
            public string? profilePictureUrl;
            public bool? hasClaimedName;
            public bool? isRequestingToSpeak;
            public bool? isSpeaker;
            public UserCommunityRoleMetadata role;

            public override string ToString() =>
                $"(Name: {name}, HasClaimedName: {hasClaimedName}, ProfilePictureUrl: {profilePictureUrl}, IsRequestingToSpeak: {isRequestingToSpeak}, IsSpeaker: {isSpeaker}, Role: {role})";
        }
    }
}
