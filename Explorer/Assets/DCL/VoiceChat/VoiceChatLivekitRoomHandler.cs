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
using System.Collections.Generic;
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
        private readonly VoiceChatMicrophoneHandler microphoneHandler;

        private bool disposed;
        private ITrack microphoneTrack;
        private CancellationTokenSource cts;
        private bool isMediaOpen;
        private bool pendingTrackPublish = false;

        private readonly Dictionary<string, WeakReference<IAudioStream>> activeStreams = new();

        private static string GetStreamKey(string participantIdentity, string trackSid) => $"{participantIdentity}:{trackSid}";

        public VoiceChatLivekitRoomHandler(
            VoiceChatCombinedAudioSource combinedAudioSource,
            VoiceChatMicrophoneAudioFilter microphoneAudioFilter,
            AudioSource microphoneAudioSource,
            IRoom voiceChatRoom,
            IVoiceChatCallStatusService voiceChatCallStatusService,
            IRoomHub roomHub,
            VoiceChatMicrophoneHandler microphoneHandler)
        {
            this.combinedAudioSource = combinedAudioSource;
            this.microphoneAudioFilter = microphoneAudioFilter;
            this.microphoneAudioSource = microphoneAudioSource;
            this.voiceChatRoom = voiceChatRoom;
            this.voiceChatCallStatusService = voiceChatCallStatusService;
            this.roomHub = roomHub;
            this.microphoneHandler = microphoneHandler;
            voiceChatRoom.ConnectionUpdated += OnConnectionUpdated;
            voiceChatCallStatusService.StatusChanged += OnCallStatusChanged;
            microphoneHandler.RtcAudioSourceReconfigured += OnRtcAudioSourceReconfigured;
            microphoneHandler.MicrophoneReady += OnMicrophoneReady;
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
            microphoneHandler.RtcAudioSourceReconfigured -= OnRtcAudioSourceReconfigured;
            microphoneHandler.MicrophoneReady -= OnMicrophoneReady;
            CloseMedia();
            activeStreams.Clear();
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
                    activeStreams.Clear();
                    voiceChatRoom.Participants.LocalParticipant().UnpublishTrack(microphoneTrack, true);
                    break;
                case ConnectionUpdate.Reconnecting:
                    // Keep media open during reconnection
                    break;
                case ConnectionUpdate.Reconnected:
                    // Media should already be open, just retry publishing if needed
                    if (isMediaOpen && pendingTrackPublish && cts != null && !cts.Token.IsCancellationRequested)
                    {
                        TryPublishTrack(cts.Token);
                    }
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

        /// <summary>
        /// Called when the RtcAudioSource is reconfigured due to sample rate changes
        /// </summary>
        private void OnRtcAudioSourceReconfigured(RtcAudioSource newRtcAudioSource)
        {
            ReportHub.Log(ReportCategory.VOICE_CHAT, "RtcAudioSource reconfigured - updating track if published");

            // If we have an active track, we need to republish with the new RtcAudioSource
            if (microphoneTrack != null && isMediaOpen && cts != null && !cts.Token.IsCancellationRequested)
            {
                ReportHub.Log(ReportCategory.VOICE_CHAT, "Republishing track with reconfigured RtcAudioSource");

                // Unpublish the old track
                voiceChatRoom.Participants.LocalParticipant().UnpublishTrack(microphoneTrack, true);

                // Publish new track with reconfigured RtcAudioSource
                TryPublishTrack(cts.Token);
            }
        }

        private bool PublishTrack(CancellationToken ct)
        {
            // Get RtcAudioSource from MicrophoneHandler (single source of truth)
            RtcAudioSource rtcAudioSource = microphoneHandler.RtcAudioSource;

            if (rtcAudioSource == null)
            {
                ReportHub.LogWarning(ReportCategory.VOICE_CHAT, "Cannot publish track: RtcAudioSource is null. Microphone may not be initialized yet.");
                return false;
            }

            // Ensure microphone AudioSource and AudioClip are ready
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
            ReportHub.LogError(ReportCategory.VOICE_CHAT,
                $"Creating LiveKit audio track with existing RtcAudioSource - Microphone: {microphoneAudioSource.clip.name}, " +
                $"SampleRate: {microphoneAudioSource.clip.frequency}Hz, " +
                $"Channels: {microphoneAudioSource.clip.channels}, " +
                $"Length: {microphoneAudioSource.clip.length:F2}s, " +
                $"Samples: {microphoneAudioSource.clip.samples}, " +
                $"AudioSource Volume: {microphoneAudioSource.volume}, " +
                $"AudioSource Pitch: {microphoneAudioSource.pitch}");

            ReportHub.Log(ReportCategory.VOICE_CHAT, $"Using existing RtcAudioSource - IsRunning: {rtcAudioSource.IsRunning}");

            microphoneTrack = voiceChatRoom.AudioTracks.CreateAudioTrack("New Track", rtcAudioSource);

            var options = new TrackPublishOptions
            {
                AudioEncoding = new AudioEncoding
                {
                    MaxBitrate = 128000, // 128 kbps - proper bitrate for voice chat (was 48kbps causing massive delays!)
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
                        {
                            string streamKey = GetStreamKey(remoteParticipantIdentity, sid);
                            activeStreams[streamKey] = stream;
                            combinedAudioSource.AddStream(stream);
                        }
                    }
                }
            }

            voiceChatRoom.TrackSubscribed += OnTrackSubscribed;
            voiceChatRoom.TrackUnsubscribed += OnTrackUnsubscribed;

            combinedAudioSource.gameObject.SetActive(true);
            combinedAudioSource.Play();
        }

        private void OnTrackSubscribed(ITrack track, TrackPublication publication, Participant participant)
        {
            if (publication.Kind == TrackKind.KindAudio)
            {
                WeakReference<IAudioStream> stream = voiceChatRoom.AudioStreams.ActiveStream(participant.Identity, publication.Sid);

                if (stream != null)
                {
                    string streamKey = GetStreamKey(participant.Identity, publication.Sid);
                    activeStreams[streamKey] = stream;
                    combinedAudioSource.AddStream(stream);
                }
                else
                {
                    ReportHub.LogWarning(ReportCategory.VOICE_CHAT, $"Failed to get audio stream for participant: {participant.Identity}, track: {publication.Sid}");
                }
            }
        }

        private void OnTrackUnsubscribed(ITrack track, TrackPublication publication, Participant participant)
        {
            if (publication.Kind == TrackKind.KindAudio)
            {
                string streamKey = GetStreamKey(participant.Identity, publication.Sid);
                if (activeStreams.TryGetValue(streamKey, out WeakReference<IAudioStream> stream))
                {
                    combinedAudioSource.RemoveStream(stream);
                    activeStreams.Remove(streamKey);
                }
                else
                {
                    ReportHub.LogWarning(ReportCategory.VOICE_CHAT, $"Failed to find audio stream to remove for participant: {participant.Identity}, track: {publication.Sid}");
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
            voiceChatRoom.TrackSubscribed -= OnTrackSubscribed;
            voiceChatRoom.TrackUnsubscribed -= OnTrackUnsubscribed;

            activeStreams.Clear();

            if (combinedAudioSource != null)
            {
                combinedAudioSource.Stop();
                combinedAudioSource.Free();
                combinedAudioSource.gameObject.SetActive(false);
            }
        }

        private async UniTaskVoid CloseMediaAsync()
        {
            await using ExecuteOnMainThreadScope scope = await ExecuteOnMainThreadScope.NewScopeAsync();


            voiceChatRoom.TrackSubscribed -= OnTrackSubscribed;
            voiceChatRoom.TrackUnsubscribed -= OnTrackUnsubscribed;

            activeStreams.Clear();

            if (combinedAudioSource != null)
            {
                combinedAudioSource.Stop();
                combinedAudioSource.Free();
                combinedAudioSource.gameObject.SetActive(false);
            }
        }
    }
}
