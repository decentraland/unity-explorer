using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Utilities;
using DCL.Utilities.Extensions;
using LiveKit;
using LiveKit.Proto;
using LiveKit.Rooms;
using System;
using System.Threading;
using Utility;
using Utility.Types;

namespace DCL.VoiceChat
{
    /// <summary>
    /// Orchestrates voice chat room operations by coordinating between track and
    /// connection management.
    /// </summary>
    public class VoiceChatRoomManager : IDisposable
    {
        private const string TAG = nameof(VoiceChatRoomManager);

        private readonly VoiceChatTrackManager trackManager;
        private readonly IRoomHub roomHub;
        private readonly IRoom voiceChatRoom;
        private readonly IVoiceChatCallStatusService voiceChatCallStatusService;
        private readonly VoiceChatConfiguration configuration;
        private readonly VoiceChatMicrophoneStateManager voiceChatMicrophoneStateManager;
        private readonly IDisposable statusSubscription;

        private bool isDisposed;
        private VoiceChatStatus currentStatus;

        public event Action ConnectionEstablished;
        public event Action ConnectionLost;
        public event Action MediaActivated;
        public event Action MediaDeactivated;

        public VoiceChatRoomManager(
            VoiceChatTrackManager trackManager,
            IRoomHub roomHub,
            IRoom voiceChatRoom,
            IVoiceChatCallStatusService voiceChatCallStatusService,
            VoiceChatConfiguration configuration,
            VoiceChatMicrophoneStateManager voiceChatMicrophoneStateManager)
        {
            this.trackManager = trackManager;
            this.roomHub = roomHub;
            this.voiceChatRoom = voiceChatRoom;
            this.voiceChatCallStatusService = voiceChatCallStatusService;
            this.configuration = configuration;
            this.voiceChatMicrophoneStateManager = voiceChatMicrophoneStateManager;

            // Subscribe to room connection events
            voiceChatRoom.ConnectionUpdated += OnConnectionUpdated;

            // Subscribe to call status changes
            statusSubscription = voiceChatCallStatusService.Status.Subscribe(OnCallStatusChanged);
        }

        /// <summary>
        /// Handles call status changes and triggers appropriate room operations.
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
                    if (currentStatus == VoiceChatStatus.VOICE_CHAT_IN_CALL)
                    {
                        DisconnectFromRoomAsync().Forget();
                    }
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
                    .TrySetConnectionStringAndActivateAsync(voiceChatCallStatusService.RoomUrl)
                    .SuppressToResultAsync();

                if (!result.Success)
                {
                    ReportHub.Log(ReportCategory.VOICE_CHAT,
                        $"Initial connection failed for room {voiceChatCallStatusService.RoomUrl}: {result.ErrorMessage}");
                    voiceChatCallStatusService.HandleLivekitConnectionFailed();
                }
            }
            catch (Exception ex)
            {
                ReportHub.LogWarning(ReportCategory.VOICE_CHAT, $"{TAG} Failed to connect to room: {ex.Message}");
                voiceChatCallStatusService.HandleLivekitConnectionFailed();
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

        private void OnConnectionUpdated(IRoom room, ConnectionUpdate connectionUpdate)
        {
            ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Connection update: {connectionUpdate}");

            switch (connectionUpdate)
            {
                case ConnectionUpdate.Connected:
                    OnConnectionEstablished();
                    break;

                case ConnectionUpdate.Disconnected:
                    OnConnectionLost();
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

        private void OnConnectionEstablished()
        {
            try
            {
                ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Setting up tracks and media");

                trackManager.SubscribeToRemoteTracks();

                trackManager.PublishLocalTrack(CancellationToken.None);

                voiceChatMicrophoneStateManager.OnRoomConnectionChanged(true);

                ConnectionEstablished?.Invoke();
                MediaActivated?.Invoke();

                ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Connection setup completed");
            }
            catch (Exception ex)
            {
                ReportHub.LogWarning(ReportCategory.VOICE_CHAT, $"{TAG} Failed to setup connection: {ex.Message}");
            }
        }

        private void OnConnectionLost()
        {
            try
            {
                ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Cleaning up tracks and media");

                trackManager.UnpublishLocalTrack();

                trackManager.UnsubscribeFromRemoteTracks();

                voiceChatMicrophoneStateManager.OnRoomConnectionChanged(false);

                ConnectionLost?.Invoke();
                MediaDeactivated?.Invoke();

                ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Connection cleanup completed");
            }
            catch (Exception ex)
            {
                ReportHub.LogWarning(ReportCategory.VOICE_CHAT, $"{TAG} Failed to cleanup connection: {ex.Message}");
            }
        }

        public void Dispose()
        {
            if (isDisposed) return;
            isDisposed = true;

            voiceChatRoom.ConnectionUpdated -= OnConnectionUpdated;
            statusSubscription?.Dispose();

            trackManager?.Dispose();

            ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Disposed");
        }
    }
}
