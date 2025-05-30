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
using UnityEngine;
using Utility;
using Utility.Multithreading;

namespace DCL.VoiceChat
{
    public class VoiceChatLivekitRoomHandler : IDisposable
    {
        private readonly VoiceChatCombinedAudioSource combinedAudioSource;
        private readonly VoiceChatMicrophoneAudioFilter microphoneAudioFilter;
        private readonly AudioSource microphoneAudioSource;
        private readonly IRoomHub roomHub;
        private readonly IRoom voiceChatRoom;
        private readonly IVoiceChatCallStatusService voiceChatCallStatusService;

        private bool disposed;
        private ITrack microphoneTrack;
        private CancellationTokenSource cts;
        private bool isMediaOpen;
        private RtcAudioSource rtcAudioSource;
        private bool pendingTrackPublish = false;

        public VoiceChatLivekitRoomHandler(
            VoiceChatCombinedAudioSource combinedAudioSource,
            VoiceChatMicrophoneAudioFilter microphoneAudioFilter,
            AudioSource microphoneAudioSource,
            IRoom voiceChatRoom,
            IVoiceChatCallStatusService voiceChatCallStatusService,
            IRoomHub roomHub)
        {
            this.combinedAudioSource = combinedAudioSource;
            this.microphoneAudioFilter = microphoneAudioFilter;
            this.microphoneAudioSource = microphoneAudioSource;
            this.voiceChatRoom = voiceChatRoom;
            this.voiceChatCallStatusService = voiceChatCallStatusService;
            this.roomHub = roomHub;
            voiceChatRoom.ConnectionUpdated += OnConnectionUpdated;
            voiceChatCallStatusService.StatusChanged += OnCallStatusChanged;
        }

        private void OnCallStatusChanged(VoiceChatStatus newStatus)
        {
            switch (newStatus)
            {
                case VoiceChatStatus.DISCONNECTED:
                    DisconnectFromRoomAsync().Forget();
                    break;
                case VoiceChatStatus.VOICE_CHAT_RECEIVED_CALL: break;
                case VoiceChatStatus.VOICE_CHAT_STARTING_CALL:
                    ConnectToRoomAsync().Forget();
                    break;
                case VoiceChatStatus.VOICE_CHAT_STARTED_CALL: break;
                case VoiceChatStatus.VOICE_CHAT_IN_CALL: break;
                case VoiceChatStatus.VOICE_CHAT_ENDING_CALL: break;
                case VoiceChatStatus.VOICE_CHAT_ENDED_CALL: break;
                default: throw new ArgumentOutOfRangeException(nameof(newStatus), newStatus, null);
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            voiceChatRoom.ConnectionUpdated -= OnConnectionUpdated;
            voiceChatCallStatusService.StatusChanged -= OnCallStatusChanged;
            CloseMedia();
        }

        private async UniTaskVoid ConnectToRoomAsync()
        {
            await roomHub.VoiceChatRoom().ActivateAsync();
        }

        private async UniTaskVoid DisconnectFromRoomAsync()
        {
            await roomHub.VoiceChatRoom().DeactivateAsync();
        }

        private void OnConnectionUpdated(IRoom room, ConnectionUpdate connectionUpdate)
        {
            switch (connectionUpdate)
            {
                case ConnectionUpdate.Connected:
                    if (!isMediaOpen)
                    {
                        cts = cts.SafeRestart();
                        OpenMedia();
                        TryPublishTrack(cts.Token);
                    }
                    break;
                case ConnectionUpdate.Disconnected:
                    cts.SafeCancelAndDispose();
                    CloseMedia();
                    isMediaOpen = false;
                    pendingTrackPublish = false;
                    voiceChatRoom.Participants.LocalParticipant().UnpublishTrack(microphoneTrack, true);
                    break;
                case ConnectionUpdate.Reconnecting:
                    break;
                case ConnectionUpdate.Reconnected:
                    break;
            }
        }

        private void TryPublishTrack(CancellationToken ct)
        {
            if (PublishTrack(ct))
            {
                pendingTrackPublish = false;
            }
            else
            {
                pendingTrackPublish = true;
                ReportHub.Log(ReportCategory.VOICE_CHAT, "Track publishing deferred until microphone is ready");
            }
        }

        /// <summary>
        /// Call this when microphone becomes available to retry publishing if needed
        /// </summary>
        public void OnMicrophoneReady()
        {
            if (pendingTrackPublish && cts != null && !cts.Token.IsCancellationRequested)
            {
                ReportHub.Log(ReportCategory.VOICE_CHAT, "Microphone ready - attempting to publish track");
                if (PublishTrack(cts.Token))
                {
                    pendingTrackPublish = false;
                }
            }
        }

        private bool PublishTrack(CancellationToken ct)
        {
            // Ensure microphone AudioSource and AudioClip are ready before creating RTCAudioSource
            if (microphoneAudioSource == null)
            {
                ReportHub.LogWarning(ReportCategory.VOICE_CHAT, "Cannot publish track: microphone AudioSource is null. Microphone may not be initialized yet.");
                return false;
            }

            if (microphoneAudioSource.clip == null)
            {
                ReportHub.LogWarning(ReportCategory.VOICE_CHAT, "Cannot publish track: microphone AudioClip is null. Microphone may not be started yet.");
                return false;
            }

            // Log microphone and audio configuration details
            ReportHub.Log(ReportCategory.VOICE_CHAT,
                $"Creating LiveKit audio track - Microphone: {microphoneAudioSource.clip.name}, " +
                $"SampleRate: {microphoneAudioSource.clip.frequency}Hz, " +
                $"Channels: {microphoneAudioSource.clip.channels}, " +
                $"Length: {microphoneAudioSource.clip.length:F2}s, " +
                $"Samples: {microphoneAudioSource.clip.samples}, " +
                $"AudioSource Volume: {microphoneAudioSource.volume}, " +
                $"AudioSource Pitch: {microphoneAudioSource.pitch}");

            ReportHub.Log(ReportCategory.VOICE_CHAT,
                $"AudioFilter State - IsValid: {microphoneAudioFilter.IsValid}, " +
                $"Component Enabled: {microphoneAudioFilter.enabled}, " +
                $"GameObject Active: {microphoneAudioFilter.gameObject.activeInHierarchy}");

            rtcAudioSource = new RtcAudioSource(microphoneAudioSource, microphoneAudioFilter);
            rtcAudioSource.Start();

            ReportHub.Log(ReportCategory.VOICE_CHAT, "RtcAudioSource created and started");

            microphoneTrack = voiceChatRoom.AudioTracks.CreateAudioTrack("New Track", rtcAudioSource);

            var options = new TrackPublishOptions
            {
                AudioEncoding = new AudioEncoding
                {
                    MaxBitrate = 48000,
                },
                Source = TrackSource.SourceMicrophone,
            };

            ReportHub.Log(ReportCategory.VOICE_CHAT,
                $"Publishing audio track with options - MaxBitrate: {options.AudioEncoding.MaxBitrate}, " +
                $"Source: {options.Source}, TrackSID: {microphoneTrack?.Sid}");

            voiceChatRoom.Participants.LocalParticipant().PublishTrack(microphoneTrack, options, ct);
            isMediaOpen = true;
            ReportHub.Log(ReportCategory.VOICE_CHAT, "Voice chat track published successfully");
            return true;
        }

        private void OpenMedia()
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
            combinedAudioSource.Play();
        }

        private void OnTrackSubscribed(ITrack track, TrackPublication publication, Participant participant)
        {
            if (publication.Kind == TrackKind.KindAudio)
            {
                WeakReference<IAudioStream> stream = voiceChatRoom.AudioStreams.ActiveStream(participant.Identity, publication.Sid);

                if (stream != null)
                {
                    combinedAudioSource.AddStream(stream);
                }
            }
        }

        private void CloseMedia()
        {
            if (!PlayerLoopHelper.IsMainThread)
            {
                CloseMediaAsync().Forget();
                return;
            }

            if (combinedAudioSource != null)
            {
                combinedAudioSource.Stop();
                combinedAudioSource.Free();
            }

            rtcAudioSource?.Stop();
            voiceChatRoom.TrackSubscribed -= OnTrackSubscribed;
        }

        private async UniTaskVoid CloseMediaAsync()
        {
            await using ExecuteOnMainThreadScope scope = await ExecuteOnMainThreadScope.NewScopeAsync();

            if (combinedAudioSource != null)
            {
                combinedAudioSource.Stop();
                combinedAudioSource.Free();
            }

            rtcAudioSource?.Stop();
            voiceChatRoom.TrackSubscribed -= OnTrackSubscribed;
        }
    }
}
