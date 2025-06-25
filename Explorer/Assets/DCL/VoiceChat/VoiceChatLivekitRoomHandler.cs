using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.RoomHubs;
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

namespace DCL.VoiceChat
{
    public class VoiceChatLivekitRoomHandler : IDisposable
    {
        private readonly VoiceChatCombinedStreamsAudioSource combinedStreamsAudioSource;
        private readonly VoiceChatMicrophoneHandler microphoneHandler;
        private readonly IRoomHub roomHub;
        private readonly IRoom voiceChatRoom;
        private readonly IVoiceChatCallStatusService voiceChatCallStatusService;
        private readonly VoiceChatConfiguration configuration;
        private readonly VoiceChatMicrophoneStateManager voiceChatMicrophoneStateManager;

        private bool disposed;
        private ITrack microphoneTrack;
        private CancellationTokenSource cts;
        private bool isMediaOpen;
        private OptimizedMonoRtcAudioSource monoRtcAudioSource;
        private int reconnectionAttempts;
        private CancellationTokenSource? reconnectionCts;
        private bool isOrderedDisconnection;
        private VoiceChatStatus currentStatus;
        private CancellationTokenSource? orderedDisconnectionCts;

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
            voiceChatRoom.LocalTrackPublished += OnLocalTrackPublished;
            voiceChatRoom.LocalTrackUnpublished += OnLocalTrackUnpublished;
            voiceChatCallStatusService.StatusChanged += OnCallStatusChanged;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            voiceChatRoom.ConnectionUpdated -= OnConnectionUpdated;
            voiceChatRoom.LocalTrackPublished -= OnLocalTrackPublished;
            voiceChatRoom.LocalTrackUnpublished -= OnLocalTrackUnpublished;
            voiceChatCallStatusService.StatusChanged -= OnCallStatusChanged;
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
            bool success = await roomHub.VoiceChatRoom().TrySetConnectionStringAndActivateAsync(voiceChatCallStatusService.RoomUrl);

            if (!success)
            {
                ReportHub.Log(ReportCategory.VOICE_CHAT, $"Initial connection failed for room {voiceChatCallStatusService.RoomUrl}");
                voiceChatCallStatusService.HandleLivekitConnectionFailed();
            }
        }

        private async UniTaskVoid DisconnectFromRoomAsync()
        {
            ReportHub.Log(ReportCategory.VOICE_CHAT, "Starting ordered disconnection");
            isOrderedDisconnection = true;
            CleanupReconnectionState();
            await roomHub.VoiceChatRoom().DeactivateAsync();
            isOrderedDisconnection = false;
            ReportHub.Log(ReportCategory.VOICE_CHAT, "Completed ordered disconnection");
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
                        cts = cts.SafeRestart();

                        SubscribeToRemoteTracks();
                        PublishTrack(cts.Token);
                    }

                    voiceChatMicrophoneStateManager.OnRoomConnectionChanged(true);
                    break;
                case ConnectionUpdate.Disconnected:
                    CleanupReconnectionState();
                    isMediaOpen = false;

                    voiceChatMicrophoneStateManager.OnRoomConnectionChanged(false);

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

            try
            {
                await UniTask.Delay(500, cancellationToken: orderedDisconnectionCts.Token);

                if (!isOrderedDisconnection)
                {
                    ReportHub.Log(ReportCategory.VOICE_CHAT, "[VoiceChatLivekitRoomHandler] No ordered disconnection received after 5 seconds - starting reconnection attempts");
                    HandleUnexpectedDisconnection();
                }
                else { ReportHub.Log(ReportCategory.VOICE_CHAT, "[VoiceChatLivekitRoomHandler] Ordered disconnection received during grace period - no reconnection needed"); }
            }
            catch (OperationCanceledException) { ReportHub.Log(ReportCategory.VOICE_CHAT, "[VoiceChatLivekitRoomHandler] Grace period cancelled - ordered disconnection received"); }
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
            if (!PlayerLoopHelper.IsMainThread) { await UniTask.SwitchToMainThread(); }

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
                await UniTask.Delay(configuration.ReconnectionDelayMs, cancellationToken: ct);

                bool success = await roomHub.VoiceChatRoom().TrySetConnectionStringAndActivateAsync(voiceChatCallStatusService.RoomUrl);

                if (success)
                {
                    ReportHub.Log(ReportCategory.VOICE_CHAT, "[VoiceChatLivekitRoomHandler] Reconnection successful");
                    CleanupReconnectionState();
                    return;
                }

                ReportHub.Log(ReportCategory.VOICE_CHAT, $"[VoiceChatLivekitRoomHandler] Reconnection attempt {reconnectionAttempts} failed");
            }
        }
    }
}
