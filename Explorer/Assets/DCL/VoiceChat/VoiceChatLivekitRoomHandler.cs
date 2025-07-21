using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Utilities;
using DCL.Utilities.Extensions;
using LiveKit;
using LiveKit.Proto;
using LiveKit.Rooms;
using LiveKit.Rooms.Participants;
using LiveKit.Rooms.Streaming.Audio;
using LiveKit.Rooms.TrackPublications;
using LiveKit.Rooms.Tracks;
using System;
using System.Threading;
using Utility;
using Utility.Types;

namespace DCL.VoiceChat
{
    public class VoiceChatLivekitRoomHandler : IDisposable
    {
        private const int WAIT_BEFORE_DISCONNECT_DELAY = 500;
        private readonly VoiceChatCombinedStreamsAudioSource combinedStreamsAudioSource;
        private readonly VoiceChatMicrophoneHandler microphoneHandler;
        private readonly IRoomHub roomHub;
        private readonly IRoom voiceChatRoom;
        private readonly IVoiceChatCallStatusService voiceChatCallStatusService;
        private readonly VoiceChatConfiguration configuration;
        private readonly VoiceChatMicrophoneStateManager voiceChatMicrophoneStateManager;
        private readonly IDisposable statusSubscription;

        private ITrack microphoneTrack;
        private OptimizedMonoRtcAudioSource monoRtcAudioSource;
        private int reconnectionAttempts;

        private bool isOrderedDisconnection;
        private bool isMediaOpen;
        private bool isDisposed;

        private VoiceChatStatus currentStatus;
        private CancellationTokenSource orderedDisconnectionCts;
        private CancellationTokenSource reconnectionCts;
        private CancellationTokenSource trackPublishingCts;

        public VoiceChatLivekitRoomHandler(
            VoiceChatCombinedStreamsAudioSource combinedStreamsAudioSource,
            VoiceChatMicrophoneHandler microphoneHandler,
            IRoom voiceChatRoom,
            IVoiceChatCallStatusService voiceChatCallStatusService,
            IRoomHub roomHub,
            VoiceChatConfiguration configuration,
            VoiceChatMicrophoneStateManager voiceChatMicrophoneStateManager)
        {
            this.combinedStreamsAudioSource = combinedStreamsAudioSource;
            this.microphoneHandler = microphoneHandler;
            this.voiceChatRoom = voiceChatRoom;
            this.voiceChatCallStatusService = voiceChatCallStatusService;
            this.roomHub = roomHub;
            this.configuration = configuration;
            this.voiceChatMicrophoneStateManager = voiceChatMicrophoneStateManager;

            voiceChatRoom.ConnectionUpdated += OnConnectionUpdated;
            //voiceChatRoom.LocalTrackPublished += OnLocalTrackPublished;
            //voiceChatRoom.LocalTrackUnpublished += OnLocalTrackUnpublished;
            statusSubscription = voiceChatCallStatusService.Status.Subscribe(OnCallStatusChanged);
        }

        public void Dispose()
        {
            if (isDisposed) return;
            isDisposed = true;
            voiceChatRoom.ConnectionUpdated -= OnConnectionUpdated;
            //voiceChatRoom.LocalTrackPublished -= OnLocalTrackPublished;
            //voiceChatRoom.LocalTrackUnpublished -= OnLocalTrackUnpublished;
            statusSubscription?.Dispose();
            reconnectionCts.SafeCancelAndDispose();
            CloseMedia();
        }

        private void OnCallStatusChanged(VoiceChatStatus newStatus)
        {
            if (newStatus == currentStatus) return;

            switch (newStatus)
            {
                case VoiceChatStatus.VOICE_CHAT_ENDING_CALL:
                case VoiceChatStatus.DISCONNECTED:
                case VoiceChatStatus.VOICE_CHAT_GENERIC_ERROR:
                    if (currentStatus == VoiceChatStatus.VOICE_CHAT_IN_CALL)
                    {
                        orderedDisconnectionCts?.Cancel();
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
            CleanupReconnectionState();
            Result<bool> result = await roomHub.VoiceChatRoom().TrySetConnectionStringAndActivateAsync(voiceChatCallStatusService.RoomUrl).SuppressToResultAsync();

            if (!result.Success)
            {
                ReportHub.Log(ReportCategory.VOICE_CHAT, $"Initial connection failed for room {voiceChatCallStatusService.RoomUrl}: {result.ErrorMessage}");
                voiceChatCallStatusService.HandleLivekitConnectionFailed();
            }
        }

        private async UniTaskVoid DisconnectFromRoomAsync()
        {
            ReportHub.Log(ReportCategory.VOICE_CHAT, "Starting ordered disconnection");
            isOrderedDisconnection = true;
            CleanupReconnectionState();

            EnumResult<TaskError> result = await roomHub.VoiceChatRoom().DeactivateAsync().SuppressToResultAsync();

            ReportHub.Log(ReportCategory.VOICE_CHAT,
                result.Success ? "Completed ordered disconnection" : $"Exception during ordered disconnection: {result.Error?.Message}");

            isOrderedDisconnection = false;
        }

        private void OnConnectionUpdated(IRoom room, ConnectionUpdate connectionUpdate)
        {
            switch (connectionUpdate)
            {
                case ConnectionUpdate.Connected:
                    CleanupReconnectionState();

                    if (!isMediaOpen)
                    {
                        isMediaOpen = true;
                        trackPublishingCts = trackPublishingCts.SafeRestart();

                        SubscribeToRemoteTracks();
                        PublishTrack(trackPublishingCts.Token);
                    }

                    voiceChatMicrophoneStateManager.OnRoomConnectionChanged(true);
                    break;
                case ConnectionUpdate.Disconnected:
                    CleanupReconnectionState();
                    isMediaOpen = false;

                    voiceChatMicrophoneStateManager.OnRoomConnectionChanged(false);

                    if (microphoneTrack != null)
                        voiceChatRoom.Participants.LocalParticipant().UnpublishTrack(microphoneTrack, true);

                    CloseMedia();

                    if (!isOrderedDisconnection)
                    {
                        ReportHub.Log(ReportCategory.VOICE_CHAT, "[VoiceChatLivekitRoomHandler] Unexpected disconnection detected - waiting for potential ordered disconnection");
                        WaitForOrderedDisconnectionAsync().Forget();
                    }

                    break;
                case ConnectionUpdate.Reconnecting:
                    ReportHub.Log(ReportCategory.VOICE_CHAT, "[VoiceChatLivekitRoomHandler] Reconnecting...");
                    voiceChatMicrophoneStateManager.OnRoomConnectionChanged(false);
                    break;
                case ConnectionUpdate.Reconnected:
                    ReportHub.Log(ReportCategory.VOICE_CHAT, "[VoiceChatLivekitRoomHandler] Reconnected successfully");
                    voiceChatMicrophoneStateManager.OnRoomConnectionChanged(true);
                    break;
            }
        }

        private async UniTaskVoid WaitForOrderedDisconnectionAsync()
        {
            orderedDisconnectionCts = orderedDisconnectionCts.SafeRestart();

            await UniTask.Delay(WAIT_BEFORE_DISCONNECT_DELAY, cancellationToken: orderedDisconnectionCts.Token).SuppressCancellationThrow();

            if (!isOrderedDisconnection)
            {
                ReportHub.Log(ReportCategory.VOICE_CHAT, "[VoiceChatLivekitRoomHandler] No ordered disconnection received after 5 seconds - starting reconnection attempts");
                HandleUnexpectedDisconnection();
            }
            else { ReportHub.Log(ReportCategory.VOICE_CHAT, "[VoiceChatLivekitRoomHandler] Ordered disconnection received during grace period - no reconnection needed"); }
        }

        private void CleanupReconnectionState()
        {
            reconnectionCts.SafeCancelAndDispose();
            reconnectionCts = null;
            reconnectionAttempts = 0;
            orderedDisconnectionCts.SafeCancelAndDispose();
            orderedDisconnectionCts = null;
        }

        private void PublishTrack(CancellationToken ct)
        {
            monoRtcAudioSource = new OptimizedMonoRtcAudioSource(microphoneHandler.AudioFilter);
            monoRtcAudioSource.Start();
            microphoneTrack = voiceChatRoom.AudioTracks.CreateAudioTrack(voiceChatRoom.Participants.LocalParticipant().Name, monoRtcAudioSource);

            var options = new TrackPublishOptions
            {
                AudioEncoding = new AudioEncoding
                {
                    MaxBitrate = 124000,
                },
                Source = TrackSource.SourceMicrophone,
            };

            voiceChatRoom.Participants.LocalParticipant().PublishTrack(microphoneTrack, options, ct);
        }

        private void SubscribeToRemoteTracks()
        {
            combinedStreamsAudioSource.Reset();

            foreach (string remoteParticipantIdentity in voiceChatRoom.Participants.RemoteParticipantIdentities())
            {
                Participant participant = voiceChatRoom.Participants.RemoteParticipant(remoteParticipantIdentity);
                if (participant == null) continue;

                foreach ((string sid, TrackPublication value) in participant.Tracks)
                {
                    if (value.Kind == TrackKind.KindAudio)
                    {
                        WeakReference<IAudioStream> stream = voiceChatRoom.AudioStreams.ActiveStream(remoteParticipantIdentity, sid);

                        if (stream != null)
                            combinedStreamsAudioSource.AddStream(stream);
                    }
                }
            }

            voiceChatRoom.TrackSubscribed += OnTrackSubscribed;
            voiceChatRoom.TrackUnsubscribed += OnTrackUnsubscribed;
            combinedStreamsAudioSource.Play();
        }

        private void OnTrackSubscribed(ITrack track, TrackPublication publication, Participant participant)
        {
            if (publication.Kind == TrackKind.KindAudio)
            {
                WeakReference<IAudioStream> stream = voiceChatRoom.AudioStreams.ActiveStream(participant.Identity, publication.Sid);

                if (stream != null) { combinedStreamsAudioSource.AddStream(stream); }
            }
        }

        private void OnTrackUnsubscribed(ITrack track, TrackPublication publication, Participant participant)
        {
            if (publication.Kind == TrackKind.KindAudio)
            {
                WeakReference<IAudioStream> stream = voiceChatRoom.AudioStreams.ActiveStream(participant.Identity, publication.Sid);

                if (stream != null) { combinedStreamsAudioSource.RemoveStream(stream); }
            }
        }

        private void OnLocalTrackPublished(TrackPublication publication, Participant participant)
        {
            if (publication.Kind == TrackKind.KindAudio && configuration.EnableLocalTrackPlayback)
            {
                WeakReference<IAudioStream> stream = voiceChatRoom.AudioStreams.ActiveStream(participant.Identity, publication.Sid);

                if (stream != null) { combinedStreamsAudioSource.AddStream(stream); }
            }
        }

        private void OnLocalTrackUnpublished(TrackPublication publication, Participant participant)
        {
            if (publication.Kind == TrackKind.KindAudio && configuration.EnableLocalTrackPlayback)
            {
                WeakReference<IAudioStream> stream = voiceChatRoom.AudioStreams.ActiveStream(participant.Identity, publication.Sid);

                if (stream != null) { combinedStreamsAudioSource.RemoveStream(stream); }
            }
        }

        private void CloseMedia()
        {
            if (!PlayerLoopHelper.IsMainThread) { CloseMediaAsync().Forget(); }

            CloseMediaInternal();
        }

        private async UniTaskVoid CloseMediaAsync()
        {
            if (!PlayerLoopHelper.IsMainThread) await UniTask.SwitchToMainThread();

            CloseMediaInternal();
        }

        private void CloseMediaInternal()
        {
            if (combinedStreamsAudioSource != null)
            {
                combinedStreamsAudioSource.Stop();
                combinedStreamsAudioSource.Reset();
            }

            monoRtcAudioSource?.Stop();
            voiceChatRoom.TrackSubscribed -= OnTrackSubscribed;
            voiceChatRoom.TrackUnsubscribed -= OnTrackUnsubscribed;
        }

        private void HandleUnexpectedDisconnection()
        {
            int remoteCount = voiceChatRoom.Participants.RemoteParticipantIdentities().Count;

            if (remoteCount == 0)
            {
                ReportHub.Log(ReportCategory.VOICE_CHAT, "[VoiceChatLivekitRoomHandler] No remote participants in room, skipping reconnection attempts");
                voiceChatCallStatusService.HandleLivekitConnectionFailed();
                return;
            }

            reconnectionAttempts = 0;
            reconnectionCts = reconnectionCts.SafeRestart();
            ReportHub.Log(ReportCategory.VOICE_CHAT, "[VoiceChatLivekitRoomHandler] Starting reconnection attempts");
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
                        ReportHub.Log(ReportCategory.VOICE_CHAT, $"[VoiceChatLivekitRoomHandler] Max reconnection attempts ({configuration.MaxReconnectionAttempts}) reached - calling HandleLivekitConnectionFailed");
                        voiceChatCallStatusService.HandleLivekitConnectionFailed();
                        return;
                    }

                    reconnectionAttempts++;
                    ReportHub.Log(ReportCategory.VOICE_CHAT, $"[VoiceChatLivekitRoomHandler] Reconnection attempt {reconnectionAttempts}/{configuration.MaxReconnectionAttempts}");

                    try { await UniTask.Delay(configuration.ReconnectionDelayMs, cancellationToken: ct); }
                    catch (OperationCanceledException)
                    {
                        ReportHub.Log(ReportCategory.VOICE_CHAT, "[VoiceChatLivekitRoomHandler] Reconnection cancelled");
                        return;
                    }

                    Result<bool> result = await roomHub.VoiceChatRoom().TrySetConnectionStringAndActivateAsync(voiceChatCallStatusService.RoomUrl).SuppressToResultAsync();

                    if (result.Success)
                    {
                        ReportHub.Log(ReportCategory.VOICE_CHAT, "[VoiceChatLivekitRoomHandler] Reconnection successful");
                        CleanupReconnectionState();
                        return;
                    }

                    ReportHub.Log(ReportCategory.VOICE_CHAT, $"[VoiceChatLivekitRoomHandler] Reconnection attempt {reconnectionAttempts} failed");
                }
            }
            catch (Exception ex)
            {
                ReportHub.Log(ReportCategory.VOICE_CHAT, $"[VoiceChatLivekitRoomHandler] Unexpected exception in reconnection loop: {ex.Message}");
                voiceChatCallStatusService.HandleLivekitConnectionFailed();
            }
        }
    }
}
