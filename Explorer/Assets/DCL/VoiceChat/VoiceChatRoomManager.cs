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
        private readonly VoiceChatConfiguration configuration;
        private readonly VoiceChatMicrophoneStateManager voiceChatMicrophoneStateManager;
        private readonly IDisposable statusSubscription;

        private bool isDisposed;
        private VoiceChatStatus currentStatus;
        private ConnectionUpdate connectionState = ConnectionUpdate.Disconnected;

        public event Action ConnectionEstablished;
        public event Action ConnectionLost;
        public event Action MediaActivated;
        public event Action MediaDeactivated;

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
            this.configuration = configuration;
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

            statusSubscription = voiceChatOrchestrator.CurrentCallStatus.Subscribe(OnCallStatusChanged);
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

            statusSubscription?.Dispose();
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
                case VoiceChatStatus.DISCONNECTED:
                case VoiceChatStatus.VOICE_CHAT_GENERIC_ERROR:
                    if (currentStatus == VoiceChatStatus.VOICE_CHAT_IN_CALL) { DisconnectFromRoomAsync().Forget(); }

                    break;

                case VoiceChatStatus.VOICE_CHAT_IN_CALL:
                    ConnectToRoomAsync().Forget();
                    break;
            }

            currentStatus = newStatus;
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
                    ReportHub.Log(ReportCategory.VOICE_CHAT,
                        $"Initial connection failed for room {voiceChatOrchestrator.CurrentConnectionUrl}: {result.ErrorMessage}");

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
            catch (Exception ex) { ReportHub.LogWarning(ReportCategory.VOICE_CHAT, $"{TAG} Failed to disconnect from room: {ex.Message}"); }
        }

        private void OnConnectionUpdated(IRoom room, ConnectionUpdate connectionUpdate, DisconnectReason? disconnectReason = null)
        {
            OnConnectionUpdatedInternal().Forget();
            return;

            async UniTaskVoid OnConnectionUpdatedInternal()
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
                        voiceChatMicrophoneStateManager.OnRoomConnectionChanged(true);
                        break;
                }
            }
        }

        private void OnTrackSubscribed(ITrack track, TrackPublication publication, Participant participant)
        {
            OnTrackSubscribedInternal().Forget();
            return;

            async UniTaskVoid OnTrackSubscribedInternal()
            {
                await UniTask.SwitchToMainThread();
                trackManager.HandleTrackSubscribed(track, publication, participant);
            }
        }

        private void OnTrackUnsubscribed(ITrack track, TrackPublication publication, Participant participant)
        {
            OnTrackUnsubscribedInternal().Forget();
            return;

            async UniTaskVoid OnTrackUnsubscribedInternal()
            {
                await UniTask.SwitchToMainThread();
                trackManager.HandleTrackUnsubscribed(track, publication, participant);
            }
        }

        private void OnLocalTrackPublished(TrackPublication publication, Participant participant)
        {
            OnLocalTrackPublishedInternal().Forget();
            return;

            async UniTaskVoid OnLocalTrackPublishedInternal()
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
        }

        private void OnConnectionEstablished()
        {
            try
            {
                ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Setting up tracks and media");

                voiceChatMicrophoneStateManager.OnRoomConnectionChanged(true);
                trackManager.StartListeningToRemoteTracks();
                trackManager.PublishLocalTrack(CancellationToken.None);

                ConnectionEstablished?.Invoke();
                MediaActivated?.Invoke();

                ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Connection setup completed");
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
                MediaDeactivated?.Invoke();

                ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Connection cleanup completed");

                reconnectionManager.HandleDisconnection(disconnectReason);
            }
            catch (Exception ex) { ReportHub.LogWarning(ReportCategory.VOICE_CHAT, $"{TAG} Failed to cleanup connection: {ex.Message}"); }
        }
    }
}
