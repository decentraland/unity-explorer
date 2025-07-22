using DCL.Diagnostics;
using DCL.Utilities;
using LiveKit.Rooms;
using LiveKit.Rooms.Participants;
using LiveKit.Proto;
using LiveKit.Rooms.TrackPublications;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.VoiceChat
{
    /// <summary>
    /// Manages voice chat participant events and state, providing a centralized interface
    /// for participant-related operations and notifications.
    /// </summary>
    public class VoiceChatParticipantManager : IDisposable
    {
        private const string TAG = nameof(VoiceChatParticipantManager);

        private readonly IRoom voiceChatRoom;

        private bool isDisposed;
        private HashSet<string> activeSpeakers = new();
        private HashSet<string> connectedParticipants = new();
        private Dictionary<string, ParticipantState> participantStates = new();

        public delegate void ParticipantJoinedUpdate(string participantId, ParticipantState participantState);
        public delegate void ParticipantLeftUpdate(string participantId);
        public delegate void ParticipantBatchUpdate(List<(string participantId, ParticipantState state)> joinedParticipants, List<string> leftParticipantIds);

        public event ParticipantJoinedUpdate ParticipantJoined;
        public event ParticipantLeftUpdate ParticipantLeft;
        public event ParticipantBatchUpdate ParticipantBatchUpdated;

        public IReadOnlyCollection<string> ConnectedParticipants => connectedParticipants;
        public IReadOnlyCollection<string> ActiveSpeakers => activeSpeakers;

        public string LocalParticipantId => voiceChatRoom.Participants.LocalParticipant().Identity;

        public ParticipantState? GetParticipantState(string participantId)
        {
            return participantStates.TryGetValue(participantId, out var state) ? state : null;
        }

        public VoiceChatParticipantManager(IRoom voiceChatRoom)
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

            ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Disposed");
        }

        private void OnParticipantUpdated(Participant participant, UpdateFromParticipant update)
        {
            switch (update)
            {
                case UpdateFromParticipant.Connected:
                    if (connectedParticipants.Add(participant.Identity))
                    {
                        var state = CreateParticipantState(participant);
                        ParticipantJoined?.Invoke(participant.Identity, state);
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

                if (!activeSpeakers.Contains(speakerId))
                {
                    UpdateParticipantSpeaking(speakerId, true);
                }
            }

            foreach (string oldSpeakerId in activeSpeakers)
            {
                if (!newActiveSpeakers.Contains(oldSpeakerId))
                {
                    UpdateParticipantSpeaking(oldSpeakerId, false);
                }
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
            var shouldClearData = ShouldClearDataOnDisconnect(disconnectReason);
            
            if (shouldClearData)
            {
                connectedParticipants.Clear();
                activeSpeakers.Clear();
                ClearAllParticipantStates();
                ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Disconnected with reason {disconnectReason}, cleared all participant data");
            }
            else
            {
                ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Disconnected with reason {disconnectReason}, keeping participant data for potential reconnection");
            }
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
                _ => false
            };
        }

        private void OnParticipantMetadataChanged(string participantId, string metadata)
        {
            try
            {
                var callMetadata = JsonUtility.FromJson<ParticipantCallMetadata>(metadata);
                ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Parsed metadata for {participantId}: {callMetadata}");

                UpdateParticipantMetadata(participantId, callMetadata);
            }
            catch (Exception e)
            {
                ReportHub.LogError(ReportCategory.VOICE_CHAT, $"{TAG} Failed to parse metadata for {participantId}: {e.Message}");
            }
        }

        public Participant? GetParticipant(string identity)
        {
            return voiceChatRoom.Participants.RemoteParticipant(identity);
        }

        public IReadOnlyCollection<string> GetRemoteParticipantIdentities()
        {
            return voiceChatRoom.Participants.RemoteParticipantIdentities();
        }

        public bool IsParticipantSpeaking(string participantId)
        {
            return activeSpeakers.Contains(participantId);
        }

        public bool IsParticipantConnected(string participantId)
        {
            return connectedParticipants.Contains(participantId);
        }

        public bool IsParticipantSpeaker(string participantId)
        {
            return participantStates.TryGetValue(participantId, out var state) && state.IsSpeaker.Value;
        }

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
                catch (Exception e)
                {
                    ReportHub.LogError(ReportCategory.VOICE_CHAT, $"{TAG} Failed to parse initial metadata for {participant.Identity}: {e.Message}");
                }
            }

            ParticipantState state = new ParticipantState
            {
                IsSpeaking = new ReactiveProperty<bool>(false),
                Name = new ReactiveProperty<string?>(metadata?.name),
                HasClaimedName = new ReactiveProperty<bool?>(metadata?.hasClaimedName),
                ProfilePictureUrl = new ReactiveProperty<string?>(metadata?.profilePictureUrl),
                IsRequestingToSpeak = new ReactiveProperty<bool?>(metadata?.isRequestingToSpeak),
                IsSpeaker = new ReactiveProperty<bool>(metadata?.isSpeaker ?? false)
            };

            participantStates[participant.Identity] = state;
            return state;
        }

        private void RemoveParticipantState(string participantId)
        {
            if (participantStates.TryGetValue(participantId, out var state))
            {
                state.IsSpeaking?.Dispose();
                state.Name?.Dispose();
                state.HasClaimedName?.Dispose();
                state.ProfilePictureUrl?.Dispose();
                state.IsRequestingToSpeak?.Dispose();
                state.IsSpeaker?.Dispose();
                participantStates.Remove(participantId);
            }
        }

        private void ClearAllParticipantStates()
        {
            foreach (var state in participantStates.Values)
            {
                state.IsSpeaking?.Dispose();
                state.Name?.Dispose();
                state.HasClaimedName?.Dispose();
                state.ProfilePictureUrl?.Dispose();
                state.IsRequestingToSpeak?.Dispose();
                state.IsSpeaker?.Dispose();
            }
            participantStates.Clear();
        }

        private void RefreshAllParticipantStates()
        {
            var currentParticipants = new List<Participant>();
            
            var localParticipant = voiceChatRoom.Participants.LocalParticipant();
            if (localParticipant != null)
                currentParticipants.Add(localParticipant);
            
            foreach (var participantId in voiceChatRoom.Participants.RemoteParticipantIdentities())
            {
                var participant = voiceChatRoom.Participants.RemoteParticipant(participantId);
                currentParticipants.Add(participant);
            }

            var participantsToRemove = new List<string>();
            foreach (var participantId in participantStates.Keys)
            {
                if (voiceChatRoom.Participants.RemoteParticipant(participantId) == null)
                {
                    participantsToRemove.Add(participantId);
                }
            }
            
            var localParticipantId = voiceChatRoom.Participants.LocalParticipant()?.Identity;
            participantsToRemove.RemoveAll(id => id == localParticipantId);
            
            foreach (var participantId in participantsToRemove)
            {
                RemoveParticipantState(participantId);
                connectedParticipants.Remove(participantId);
                activeSpeakers.Remove(participantId);
                ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Removed disconnected participant during refresh: {participantId}");
            }
            
            var joinedParticipants = new List<(string participantId, ParticipantState state)>();
            foreach (var participant in currentParticipants)
            {
                if (participantStates.TryGetValue(participant.Identity, out var existingState))
                {
                    RefreshParticipantState(participant, existingState);
                }
                else
                {
                    var state = CreateParticipantState(participant);
                    connectedParticipants.Add(participant.Identity);
                    joinedParticipants.Add((participant.Identity, state));
                    ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Participant joined during refresh: {participant.Identity}");
                }
            }

            if (joinedParticipants.Count > 0 || participantsToRemove.Count > 0)
            {
                ParticipantBatchUpdated?.Invoke(joinedParticipants, participantsToRemove);
            }
        }

        private void RefreshParticipantState(Participant participant, ParticipantState existingState)
        {
            ParticipantCallMetadata? metadata = null;
            if (!string.IsNullOrEmpty(participant.Metadata))
            {
                try
                {
                    metadata = JsonUtility.FromJson<ParticipantCallMetadata>(participant.Metadata);
                }
                catch (Exception e)
                {
                    ReportHub.LogError(ReportCategory.VOICE_CHAT, $"{TAG} Failed to parse metadata for {participant.Identity}: {e.Message}");
                }
            }

            existingState.Name.Value = metadata?.name;
            existingState.HasClaimedName.Value = metadata?.hasClaimedName;
            existingState.ProfilePictureUrl.Value = metadata?.profilePictureUrl;
            existingState.IsRequestingToSpeak.Value = metadata?.isRequestingToSpeak;
            existingState.IsSpeaker.Value = metadata?.isSpeaker ?? false;
        }

        private void UpdateParticipantSpeaking(string participantId, bool isSpeaking)
        {
            if (participantStates.TryGetValue(participantId, out var state))
            {
                state.IsSpeaking.Value = isSpeaking;
            }
        }

        private void UpdateParticipantMetadata(string participantId, ParticipantCallMetadata metadata)
        {
            if (participantStates.TryGetValue(participantId, out var state))
            {
                state.Name.Value = metadata.name;
                state.HasClaimedName.Value = metadata.hasClaimedName;
                state.ProfilePictureUrl.Value = metadata.profilePictureUrl;
                state.IsRequestingToSpeak.Value = metadata.isRequestingToSpeak;
                state.IsSpeaker.Value = metadata.isSpeaker ?? false;
            }
        }

        public struct ParticipantState
        {
            public ReactiveProperty<bool> IsSpeaking { get; set; }
            public ReactiveProperty<string?> Name { get; set; }
            public ReactiveProperty<bool?> HasClaimedName { get; set; }
            public ReactiveProperty<string?> ProfilePictureUrl { get; set; }
            public ReactiveProperty<bool?> IsRequestingToSpeak { get; set; }
            public ReactiveProperty<bool> IsSpeaker { get; set; }
        }

        [Serializable]
        public struct ParticipantCallMetadata
        {
            public string? name;
            public bool? hasClaimedName;
            public string? profilePictureUrl;
            public bool? isRequestingToSpeak;
            public bool? isSpeaker;

            public override string ToString() =>
                $"(Name: {name}, HasClaimedName: {hasClaimedName}, ProfilePictureUrl: {profilePictureUrl}, IsRequestingToSpeak: {isRequestingToSpeak}, IsSpeaker: {isSpeaker})";
        }
    }
}
