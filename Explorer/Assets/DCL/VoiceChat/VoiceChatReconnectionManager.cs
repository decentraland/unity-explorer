using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Utilities.Extensions;
using LiveKit.Rooms;
using System;
using System.Threading;
using Utility;
using Utility.Types;

namespace DCL.VoiceChat
{
    public class VoiceChatReconnectionManager : IDisposable
    {
        private const string TAG = nameof(VoiceChatReconnectionManager);

        private readonly IRoomHub roomHub;
        private readonly IVoiceChatOrchestrator voiceChatOrchestrator;
        private readonly VoiceChatConfiguration configuration;
        private readonly IRoom voiceChatRoom;

        private int reconnectionAttempts;
        private bool isDisposed;

        private CancellationTokenSource reconnectionCts;

        public event Action ReconnectionStarted;
        public event Action ReconnectionSuccessful;
        public event Action ReconnectionFailed;

        public VoiceChatReconnectionManager(
            IRoomHub roomHub,
            IVoiceChatOrchestrator voiceChatOrchestrator,
            VoiceChatConfiguration configuration,
            IRoom voiceChatRoom)
        {
            this.roomHub = roomHub;
            this.voiceChatOrchestrator = voiceChatOrchestrator;
            this.configuration = configuration;
            this.voiceChatRoom = voiceChatRoom;
        }

        public void Dispose()
        {
            if (isDisposed) return;
            isDisposed = true;

            CleanupReconnectionState();

            ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Disposed");
        }

        public void HandleDisconnection()
        {
            if (isDisposed) return;

            HandleUnexpectedDisconnection();
        }

        private void HandleUnexpectedDisconnection()
        {
            if (isDisposed) return;

            int remoteCount = voiceChatRoom.Participants.RemoteParticipantIdentities().Count;

            if (remoteCount == 0)
            {
                ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} No remote participants in room, skipping reconnection attempts");
                ReconnectionFailed?.Invoke();
                return;
            }

            reconnectionAttempts = 0;
            reconnectionCts = reconnectionCts.SafeRestart();
            ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Starting reconnection attempts");
            ReconnectionStarted?.Invoke();
            AttemptReconnectionAsync(reconnectionCts.Token).Forget();
        }

        private async UniTaskVoid AttemptReconnectionAsync(CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    if (reconnectionAttempts >= configuration.MaxReconnectionAttempts)
                    {
                        reconnectionAttempts = 0;
                        ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Max reconnection attempts ({configuration.MaxReconnectionAttempts}) reached");
                        ReconnectionFailed?.Invoke();
                        return;
                    }

                    reconnectionAttempts++;
                    ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Reconnection attempt {reconnectionAttempts}/{configuration.MaxReconnectionAttempts}");

                    try { await UniTask.Delay(configuration.ReconnectionDelayMs, cancellationToken: ct); }
                    catch (OperationCanceledException)
                    {
                        ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Reconnection cancelled");
                        return;
                    }

                    Result<bool> result = await roomHub.VoiceChatRoom()
                                                       .TrySetConnectionStringAndActivateAsync(voiceChatOrchestrator.CurrentConnectionUrl)
                                                       .SuppressToResultAsync();

                    if (result.Success)
                    {
                        ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Reconnection successful");
                        CleanupReconnectionState();
                        ReconnectionSuccessful?.Invoke();
                        return;
                    }

                    ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Reconnection attempt {reconnectionAttempts} failed");
                }
            }
            catch (Exception ex)
            {
                ReportHub.LogWarning(ReportCategory.VOICE_CHAT, $"{TAG} Unexpected exception in reconnection loop: {ex.Message}");
                ReconnectionFailed?.Invoke();
            }
        }

        private void CleanupReconnectionState()
        {
            reconnectionCts.SafeCancelAndDispose();
            reconnectionCts = null;
            reconnectionAttempts = 0;
        }
    }
}
