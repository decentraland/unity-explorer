using Cysharp.Threading.Tasks;
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
        private readonly IRoomHub roomHub;
        private readonly IRoom voiceChatRoom;
        private readonly IVoiceChatCallStatusService voiceChatCallStatusService;
        private readonly VoiceChatConfiguration configuration;

        private bool disposed;
        private ITrack microphoneTrack;
        private CancellationTokenSource cts;
        private bool isMediaOpen;
        private OptimizedRtcAudioSource rtcAudioSource;

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
            CloseMedia();
        }

        private void OnCallStatusChanged(VoiceChatStatus newStatus)
        {
            switch (newStatus)
            {
                case VoiceChatStatus.VOICE_CHAT_ENDING_CALL: break;
                case VoiceChatStatus.DISCONNECTED:
                    DisconnectFromRoomAsync().Forget();
                    break;
                case VoiceChatStatus.VOICE_CHAT_IN_CALL: break;
                case VoiceChatStatus.VOICE_CHAT_STARTED_CALL:
                    ConnectToRoomAsync().Forget();
                    break;
            }
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
                        SubscribeToRemoteTracks();
                        PublishTrack(cts.Token);
                    }

                    break;
                case ConnectionUpdate.Disconnected:
                    cts.SafeCancelAndDispose();
                    CloseMedia();
                    isMediaOpen = false;
                    voiceChatRoom.Participants.LocalParticipant().UnpublishTrack(microphoneTrack, true);
                    break;
                case ConnectionUpdate.Reconnecting:
                    break;
                case ConnectionUpdate.Reconnected:
                    break;
            }
        }

        private void PublishTrack(CancellationToken ct)
        {
            rtcAudioSource = new OptimizedRtcAudioSource(microphoneAudioFilter);
            rtcAudioSource.Start();
            microphoneTrack = voiceChatRoom.AudioTracks.CreateAudioTrack("New Track", rtcAudioSource);

            var options = new TrackPublishOptions
            {
                AudioEncoding = new AudioEncoding
                {
                    MaxBitrate = 124000,
                },
                Source = TrackSource.SourceMicrophone,
            };

            voiceChatRoom.Participants.LocalParticipant().PublishTrack(microphoneTrack, options, ct);

            isMediaOpen = true;
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
                return;
            }

            if (combinedAudioSource != null)
            {
                combinedAudioSource.Stop();
                combinedAudioSource.Free();
            }

            rtcAudioSource?.Stop();
            voiceChatRoom.TrackSubscribed -= OnTrackSubscribed;
            voiceChatRoom.TrackUnsubscribed -= OnTrackUnsubscribed;
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
            voiceChatRoom.TrackUnsubscribed -= OnTrackUnsubscribed;
        }
    }
}
