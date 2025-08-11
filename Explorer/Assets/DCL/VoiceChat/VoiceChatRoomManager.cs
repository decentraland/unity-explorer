using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Utilities;
using DCL.Utilities.Extensions;
using LiveKit.Rooms;
using LiveKit.Rooms.Participants;
using LiveKit.Rooms.TrackPublications;
using LiveKit.Rooms.Tracks;
using LiveKit.Proto;
using System;
using System.Threading;
using Utility.Types;

namespace DCL.VoiceChat
{
    /// <summary>
    ///     Orchestrates voice chat room operations by coordinating between track and
    ///     connection management.
    /// </summary>
    public class VoiceChatRoomManager : IDisposable
    {
        private const string TAG = nameof(VoiceChatRoomManager);

        private readonly VoiceChatTrackManager trackManager;
        private readonly VoiceChatReconnectionManager reconnectionManager;
        private readonly IRoomHub roomHub;
        private readonly IRoom voiceChatRoom;
        private readonly IVoiceChatOrchestrator voiceChatOrchestrator;
        private readonly VoiceChatMicrophoneStateManager voiceChatMicrophoneStateManager;
        private readonly IDisposable callStatusSubscription;
        private readonly IDisposable localParticipantIsSpeakerSubscription;

        private bool isDisposed;
        private VoiceChatStatus currentStatus;
        private ConnectionUpdate connectionState = ConnectionUpdate.Disconnected;
        private bool isClientInitiatedDisconnection = false;

        public event Action ConnectionEstablished;
        public event Action ConnectionLost;

        public VoiceChatRoomManager(
            VoiceChatTrackManager trackManager,
            IRoomHub roomHub,
            IRoom voiceChatRoom,
            IVoiceChatOrchestrator voiceChatOrchestrator,
            VoiceChatConfiguration configuration,
            VoiceChatMicrophoneStateManager voiceChatMicrophoneStateManager)
        {
            this.trackManager = trackManager;
            this.roomHub = roomHub;
            this.voiceChatRoom = voiceChatRoom;
            this.voiceChatOrchestrator = voiceChatOrchestrator;
            this.voiceChatMicrophoneStateManager = voiceChatMicrophoneStateManager;

            reconnectionManager = new VoiceChatReconnectionManager(
                roomHub, voiceChatOrchestrator, configuration, voiceChatRoom);

            voiceChatRoom.ConnectionUpdated += OnConnectionUpdated;
            voiceChatRoom.TrackSubscribed += OnTrackSubscribed;
            voiceChatRoom.TrackUnsubscribed += OnTrackUnsubscribed;
            voiceChatRoom.LocalTrackPublished += OnLocalTrackPublished;
            voiceChatRoom.LocalTrackUnpublished += OnLocalTrackUnpublished;

            reconnectionManager.ReconnectionStarted += OnReconnectionStarted;
            reconnectionManager.ReconnectionSuccessful += OnReconnectionSuccessful;
            reconnectionManager.ReconnectionFailed += OnReconnectionFailed;

            callStatusSubscription = voiceChatOrchestrator.CurrentCallStatus.Subscribe(OnCallStatusChanged);
            localParticipantIsSpeakerSubscription = voiceChatOrchestrator.ParticipantsStateService.LocalParticipantState.IsSpeaker.Subscribe(OnLocalParticipantIsSpeakerUpdated);
        }

        public void Dispose()
        {
            if (isDisposed) return;
            isDisposed = true;

            voiceChatRoom.ConnectionUpdated -= OnConnectionUpdated;
            voiceChatRoom.TrackSubscribed -= OnTrackSubscribed;
            voiceChatRoom.TrackUnsubscribed -= OnTrackUnsubscribed;
            voiceChatRoom.LocalTrackPublished -= OnLocalTrackPublished;
            voiceChatRoom.LocalTrackUnpublished -= OnLocalTrackUnpublished;

            reconnectionManager.ReconnectionStarted -= OnReconnectionStarted;
            reconnectionManager.ReconnectionSuccessful -= OnReconnectionSuccessful;
            reconnectionManager.ReconnectionFailed -= OnReconnectionFailed;

            callStatusSubscription?.Dispose();
            reconnectionManager?.Dispose();
            trackManager?.Dispose();

            ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Disposed");
        }

        /// <summary>
        ///     Handles call status changes and triggers appropriate room operations.
        /// </summary>
        private void OnCallStatusChanged(VoiceChatStatus newStatus)
        {
            if (newStatus == currentStatus) return;

            ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Call status changed: {currentStatus} -> {newStatus}");

            switch (newStatus)
            {
                case VoiceChatStatus.VOICE_CHAT_ENDING_CALL:
                        isClientInitiatedDisconnection = true;
                        DisconnectFromRoomAsync().Forget();
                    break;
                case VoiceChatStatus.DISCONNECTED:
                case VoiceChatStatus.VOICE_CHAT_GENERIC_ERROR:
                    //We ignore these states as they are final states. If we reach these we should be already disconnected from the room altogether.
                    break;
                case VoiceChatStatus.VOICE_CHAT_IN_CALL:
                    ConnectToRoomAsync().Forget();
                    break;
            }

            currentStatus = newStatus;
        }

        private void OnLocalParticipantIsSpeakerUpdated(bool isSpeaker)
        {
            if (isSpeaker && voiceChatOrchestrator.CurrentCallStatus.Value == VoiceChatStatus.VOICE_CHAT_IN_CALL && roomHub.VoiceChatRoom().Activated)
            {
                voiceChatMicrophoneStateManager.OnRoomConnectionChanged(true);
                trackManager.PublishLocalTrack(CancellationToken.None);
            }
            else
            {
                voiceChatMicrophoneStateManager.OnRoomConnectionChanged(false);
                trackManager.UnpublishLocalTrack();
            }
        }


        private async UniTaskVoid ConnectToRoomAsync()
        {
            try
            {
                ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Connecting to room");

                Result<bool> result = await roomHub.VoiceChatRoom()
                                                   .TrySetConnectionStringAndActivateAsync(
                                                        voiceChatOrchestrator.CurrentConnectionUrl)
                                                   .SuppressToResultAsync();

                if (!result.Success)
                {
                    ReportHub.Log(ReportCategory.VOICE_CHAT, $"Initial connection failed for room {voiceChatOrchestrator.CurrentConnectionUrl}: {result.ErrorMessage}");
                    roomHub.VoiceChatRoom().DeactivateAsync().Forget();
                    voiceChatOrchestrator.HandleConnectionError();
                }
            }
            catch (Exception ex)
            {
                ReportHub.LogWarning(ReportCategory.VOICE_CHAT, $"{TAG} Failed to connect to room: {ex.Message}");
                voiceChatOrchestrator.HandleConnectionError();
            }
        }

        private async UniTaskVoid DisconnectFromRoomAsync()
        {
            try
            {
                ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Disconnecting from room");

                EnumResult<TaskError> result = await roomHub.VoiceChatRoom()
                                                            .DeactivateAsync()
                                                            .SuppressToResultAsync();

                ReportHub.Log(ReportCategory.VOICE_CHAT,
                    result.Success ? $"{TAG} Completed disconnection" : $"{TAG} Exception during disconnection: {result.Error?.Message}");
            }
            catch (Exception ex)
            {
                ReportHub.LogWarning(ReportCategory.VOICE_CHAT, $"{TAG} Failed to disconnect from room: {ex.Message}");
            }
        }

        private void OnConnectionUpdated(IRoom room, ConnectionUpdate connectionUpdate, DisconnectReason? disconnectReason = null)
        {
            OnConnectionUpdatedInternalAsync().Forget();
            return;

            async UniTaskVoid OnConnectionUpdatedInternalAsync()
            {
                await UniTask.SwitchToMainThread();

                ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Connection update: {connectionUpdate}");

                if (connectionState == connectionUpdate) return;

                connectionState = connectionUpdate;

                switch (connectionUpdate)
                {
                    case ConnectionUpdate.Connected:
                        OnConnectionEstablished();
                        break;

                    case ConnectionUpdate.Disconnected:
                        OnConnectionLost(disconnectReason);
                        break;

                    case ConnectionUpdate.Reconnecting:
                        ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Reconnecting...");
                        voiceChatMicrophoneStateManager.OnRoomConnectionChanged(false);
                        break;

                    case ConnectionUpdate.Reconnected:
                        ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Reconnected successfully");

                        bool canSpeak = voiceChatOrchestrator.ParticipantsStateService.LocalParticipantState.IsSpeaker.Value ||
                                        voiceChatOrchestrator.CurrentVoiceChatType.Value == VoiceChatType.PRIVATE;
                        if (canSpeak)
                            voiceChatMicrophoneStateManager.OnRoomConnectionChanged(true);
                        break;
                }
            }
        }

        private void OnTrackSubscribed(ITrack track, TrackPublication publication, Participant participant)
        {
            OnTrackSubscribedInternalAsync().Forget();
            return;

            async UniTaskVoid OnTrackSubscribedInternalAsync()
            {
                await UniTask.SwitchToMainThread();
                trackManager.HandleTrackSubscribed(track, publication, participant);
            }
        }

        private void OnTrackUnsubscribed(ITrack track, TrackPublication publication, Participant participant)
        {
            OnTrackUnsubscribedInternalAsync().Forget();
            return;

            async UniTaskVoid OnTrackUnsubscribedInternalAsync()
            {
                await UniTask.SwitchToMainThread();
                trackManager.HandleTrackUnsubscribed(track, publication, participant);
            }
        }

        private void OnLocalTrackPublished(TrackPublication publication, Participant participant)
        {
            OnLocalTrackPublishedInternalAsync().Forget();
            return;

            async UniTaskVoid OnLocalTrackPublishedInternalAsync()
            {
                await UniTask.SwitchToMainThread();
                trackManager.HandleLocalTrackPublished(publication, participant);
            }
        }

        private void OnLocalTrackUnpublished(TrackPublication publication, Participant participant)
        {
            OnLocalTrackUnpublishedInternal().Forget();
            return;

            async UniTaskVoid OnLocalTrackUnpublishedInternal()
            {
                await UniTask.SwitchToMainThread();
                trackManager.HandleLocalTrackUnpublished(publication, participant);
            }
        }

        private void OnReconnectionStarted()
        {
            ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Reconnection process started");
        }

        private void OnReconnectionSuccessful()
        {
            ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Reconnection successful");
        }

        private void OnReconnectionFailed()
        {
            ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Reconnection failed");
            DisconnectFromRoomAsync().Forget();
            voiceChatOrchestrator.HandleConnectionError();
        }

        private void OnConnectionEstablished()
        {
            try
            {
                // If its a community chat but local participant is not a speaker, we dont publish the track.

                ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Setting up tracks and media");

                trackManager.StartListeningToRemoteTracks();

                ConnectionEstablished?.Invoke();
                ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Connection setup completed");

                bool canSpeak = voiceChatOrchestrator.ParticipantsStateService.LocalParticipantState.IsSpeaker.Value ||
                                voiceChatOrchestrator.CurrentVoiceChatType.Value == VoiceChatType.PRIVATE;

                if (canSpeak)
                {
                    voiceChatMicrophoneStateManager.OnRoomConnectionChanged(true);
                    trackManager.PublishLocalTrack(CancellationToken.None);
                }
            }
            catch (Exception ex) { ReportHub.LogWarning(ReportCategory.VOICE_CHAT, $"{TAG} Failed to setup connection: {ex.Message}"); }
        }

        private void OnConnectionLost(DisconnectReason? disconnectReason)
        {
            try
            {
                ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Cleaning up tracks and media");

                trackManager.UnpublishLocalTrack();
                trackManager.StopListeningToRemoteTracks();

                voiceChatMicrophoneStateManager.OnRoomConnectionChanged(false);

                ConnectionLost?.Invoke();

                ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Connection cleanup completed");

                if (isClientInitiatedDisconnection)
                {
                    ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Client-initiated disconnection - no reconnection needed");
                    isClientInitiatedDisconnection = false;
                    voiceChatOrchestrator.HandleConnectionEnded();
                    return;
                }

                if (VoiceChatDisconnectReasonHelper.IsValidDisconnectReason(disconnectReason))
                {
                    ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Valid disconnect reason ({disconnectReason}) - no reconnection needed");
                    // We call this here in case the disconnection was not triggered by us but by the server, if it was already stopped, it won't do anything.
                    DisconnectFromRoomAsync().Forget();
                    voiceChatOrchestrator.HandleConnectionEnded();
                    return;
                }

                ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Unexpected disconnect reason ({disconnectReason}) - starting reconnection attempts");
                reconnectionManager.HandleDisconnection();
            }
            catch (Exception ex) { ReportHub.LogWarning(ReportCategory.VOICE_CHAT, $"{TAG} Failed to cleanup connection: {ex.Message}"); }
        }
    }
}
