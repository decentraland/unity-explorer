using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Multiplayer.Connections.Rooms.Connective;
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
using Utility.Multithreading;
using UnityEngine;

namespace DCL.VoiceChat
{
    public class VoiceChatLivekitRoomHandler : IDisposable
    {
        private readonly VoiceChatCombinedAudioSource combinedAudioSource;
        private readonly VoiceChatMicrophoneAudioFilter microphoneAudioFilter;
        private readonly IRoomHub roomHub;
        private readonly IRoom voiceChatRoom;
        private readonly IVoiceChatCallStatusService voiceChatCallStatusService;
        private readonly VoiceChatConfiguration configuration;

        private bool disposed;
        private ITrack microphoneTrack;
        private CancellationTokenSource cts;
        private bool isMediaOpen;
        private OptimizedRtcAudioSource rtcAudioSource;
        private int reconnectionAttempts;
        private CancellationTokenSource? reconnectionCts;
        private bool isOrderedDisconnection;
        private VoiceChatStatus currentStatus;

        public VoiceChatLivekitRoomHandler(
            VoiceChatCombinedAudioSource combinedAudioSource,
            VoiceChatMicrophoneAudioFilter microphoneAudioFilter,
            IRoom voiceChatRoom,
            IVoiceChatCallStatusService voiceChatCallStatusService,
            IRoomHub roomHub,
            VoiceChatConfiguration configuration)
        {
            this.combinedAudioSource = combinedAudioSource;
            this.microphoneAudioFilter = microphoneAudioFilter;
            this.voiceChatRoom = voiceChatRoom;
            this.voiceChatCallStatusService = voiceChatCallStatusService;
            this.roomHub = roomHub;
            this.configuration = configuration;
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
                        DisconnectFromRoomAsync().Forget();
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
                Debug.Log($"[VoiceChatLivekitRoomHandler] Initial connection failed for room {voiceChatCallStatusService.RoomUrl}");
                voiceChatCallStatusService.HandleLivekitConnectionFailed();
            }
        }

        private async UniTaskVoid DisconnectFromRoomAsync()
        {
            Debug.Log("[VoiceChatLivekitRoomHandler] Starting ordered disconnection");
            isOrderedDisconnection = true;
            CleanupReconnectionState();
            await roomHub.VoiceChatRoom().DeactivateAsync();
            isOrderedDisconnection = false;
            Debug.Log("[VoiceChatLivekitRoomHandler] Completed ordered disconnection");
        }

        private void OnConnectionUpdated(IRoom room, ConnectionUpdate connectionUpdate)
        {
            Debug.Log($"[VoiceChatLivekitRoomHandler] Connection update: {connectionUpdate}, IsOrderedDisconnection: {isOrderedDisconnection}");

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

                    break;
                case ConnectionUpdate.Disconnected:
                    CleanupReconnectionState();
                    isMediaOpen = false;
                    voiceChatRoom.Participants.LocalParticipant().UnpublishTrack(microphoneTrack, true);
                    CloseMedia();

                    if (!isOrderedDisconnection && roomHub.VoiceChatRoom().CurrentConnectionLoopHealth == IConnectiveRoom.ConnectionLoopHealth.Stopped)
                    {
                        Debug.Log("[VoiceChatLivekitRoomHandler] Unexpected disconnection detected - starting reconnection attempts");
                        HandleUnexpectedDisconnection();
                    }
                    else
                    {
                        Debug.Log($"[VoiceChatLivekitRoomHandler] Disconnection handled - IsOrdered: {isOrderedDisconnection}, ConnectionHealth: {roomHub.VoiceChatRoom().CurrentConnectionLoopHealth}");
                    }

                    break;
                case ConnectionUpdate.Reconnecting:
                    Debug.Log("[VoiceChatLivekitRoomHandler] Reconnecting...");
                    break;
                case ConnectionUpdate.Reconnected:
                    Debug.Log("[VoiceChatLivekitRoomHandler] Reconnected successfully");
                    break;
            }
        }

        private void CleanupReconnectionState()
        {
            reconnectionCts.SafeCancelAndDispose();
            reconnectionCts = null;
            reconnectionAttempts = 0;
        }

        private void PublishTrack(CancellationToken ct)
        {
            rtcAudioSource = new OptimizedRtcAudioSource(microphoneAudioFilter);
            rtcAudioSource.Start();
            microphoneTrack = voiceChatRoom.AudioTracks.CreateAudioTrack(voiceChatRoom.Participants.LocalParticipant().Name, rtcAudioSource);

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
                            combinedAudioSource.AddStream(stream);
                    }
                }
            }

            voiceChatRoom.TrackSubscribed += OnTrackSubscribed;
            voiceChatRoom.TrackUnsubscribed += OnTrackUnsubscribed;
            combinedAudioSource.Play();
        }

        private void OnTrackSubscribed(ITrack track, TrackPublication publication, Participant participant)
        {
            if (publication.Kind == TrackKind.KindAudio)
            {
                WeakReference<IAudioStream> stream = voiceChatRoom.AudioStreams.ActiveStream(participant.Identity, publication.Sid);

                if (stream != null) { combinedAudioSource.AddStream(stream); }
            }
        }

        private void OnTrackUnsubscribed(ITrack track, TrackPublication publication, Participant participant)
        {
            if (publication.Kind == TrackKind.KindAudio)
            {
                WeakReference<IAudioStream> stream = voiceChatRoom.AudioStreams.ActiveStream(participant.Identity, publication.Sid);

                if (stream != null) { combinedAudioSource.RemoveStream(stream); }
            }
        }

        private void OnLocalTrackPublished(TrackPublication publication, Participant participant)
        {
            if (publication.Kind == TrackKind.KindAudio && configuration.EnableLocalTrackPlayback)
            {
                WeakReference<IAudioStream> stream = voiceChatRoom.AudioStreams.ActiveStream(participant.Identity, publication.Sid);

                if (stream != null) { combinedAudioSource.AddStream(stream); }
            }
        }

        private void OnLocalTrackUnpublished(TrackPublication publication, Participant participant)
        {
            if (publication.Kind == TrackKind.KindAudio && configuration.EnableLocalTrackPlayback)
            {
                WeakReference<IAudioStream> stream = voiceChatRoom.AudioStreams.ActiveStream(participant.Identity, publication.Sid);

                if (stream != null) { combinedAudioSource.RemoveStream(stream); }
            }
        }

        private void CloseMedia()
        {
            if (!PlayerLoopHelper.IsMainThread)
            {
                CloseMediaAsync().Forget();
            }

            CloseMediaInternal();
        }

        private async UniTaskVoid CloseMediaAsync()
        {
            if (!PlayerLoopHelper.IsMainThread)
            {
                await UniTask.SwitchToMainThread();
            }

            CloseMediaInternal();
        }

        private void CloseMediaInternal()
        {
            if (combinedAudioSource != null)
            {
                combinedAudioSource.Stop();
                combinedAudioSource.Free();
            }

            rtcAudioSource?.Stop();
            voiceChatRoom.TrackSubscribed -= OnTrackSubscribed;
            voiceChatRoom.TrackUnsubscribed -= OnTrackUnsubscribed;
        }

        private void HandleUnexpectedDisconnection()
        {
            if (roomHub.VoiceChatRoom().CurrentConnectionLoopHealth != IConnectiveRoom.ConnectionLoopHealth.Stopped)
            {
                Debug.Log("[VoiceChatLivekitRoomHandler] Skipping reconnection - connection health is not Stopped");
                return;
            }

            reconnectionAttempts = 0;
            reconnectionCts = reconnectionCts.SafeRestart();
            Debug.Log("[VoiceChatLivekitRoomHandler] Starting reconnection attempts");
            AttemptReconnectionAsync(reconnectionCts.Token).Forget();
        }

        private async UniTaskVoid AttemptReconnectionAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                if (reconnectionAttempts >= configuration.MaxReconnectionAttempts)
                {
                    reconnectionAttempts = 0;
                    Debug.Log($"[VoiceChatLivekitRoomHandler] Max reconnection attempts ({configuration.MaxReconnectionAttempts}) reached - calling HandleLivekitConnectionFailed");
                    voiceChatCallStatusService.HandleLivekitConnectionFailed();
                    return;
                }

                reconnectionAttempts++;
                Debug.Log($"[VoiceChatLivekitRoomHandler] Reconnection attempt {reconnectionAttempts}/{configuration.MaxReconnectionAttempts}");
                await UniTask.Delay(configuration.ReconnectionDelayMs, cancellationToken: ct);

                bool success = await roomHub.VoiceChatRoom().TrySetConnectionStringAndActivateAsync(voiceChatCallStatusService.RoomUrl);

                if (success)
                {
                    Debug.Log("[VoiceChatLivekitRoomHandler] Reconnection successful");
                    CleanupReconnectionState();
                    return;
                }
                else
                {
                    Debug.Log($"[VoiceChatLivekitRoomHandler] Reconnection attempt {reconnectionAttempts} failed");
                }
            }
        }
    }
}
