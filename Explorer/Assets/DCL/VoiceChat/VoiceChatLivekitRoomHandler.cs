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

namespace DCL.VoiceChat
{
    public class VoiceChatLivekitRoomHandler : IDisposable
    {
        private readonly VoiceChatCombinedAudioSource combinedAudioSource;
        private readonly AudioFilter microphoneAudioFilter;
        private readonly AudioSource microphoneAudioSource;
        private readonly IRoomHub roomHub;
        private readonly IRoom voiceChatRoom;

        private bool disposed;
        private ITrack microphoneTrack;
        private CancellationTokenSource cts;
        private bool trackPublished;

        public VoiceChatLivekitRoomHandler(VoiceChatCombinedAudioSource combinedAudioSource, AudioFilter microphoneAudioFilter, AudioSource microphoneAudioSource, IRoom voiceChatRoom)
        {
            this.combinedAudioSource = combinedAudioSource;
            this.microphoneAudioFilter = microphoneAudioFilter;
            this.microphoneAudioSource = microphoneAudioSource;
            this.voiceChatRoom = voiceChatRoom;
            voiceChatRoom.ConnectionUpdated += OnConnectionUpdated;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            voiceChatRoom.ConnectionUpdated -= OnConnectionUpdated;
            CloseMedia();
        }

        private void OnConnectionUpdated(IRoom room, ConnectionUpdate connectionUpdate)
        {
            switch (connectionUpdate)
            {
                case ConnectionUpdate.Connected:
                    if (!trackPublished)
                    {
                        cts = cts.SafeRestart();
                        OpenMedia();
                        PublishTrack(cts.Token);
                    }
                    break;
                case ConnectionUpdate.Disconnected:
                    cts.SafeCancelAndDispose();
                    CloseMedia();
                    trackPublished = false;
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
            var rtcAudioSource = new RtcAudioSource(microphoneAudioSource, microphoneAudioFilter);
            rtcAudioSource.Start();
            microphoneTrack = voiceChatRoom.CreateAudioTrack("New Track", rtcAudioSource);

            var options = new TrackPublishOptions
            {
                AudioEncoding = new AudioEncoding
                {
                    MaxBitrate = 48000,
                },
                Source = TrackSource.SourceMicrophone,
            };

            voiceChatRoom.Participants.LocalParticipant().PublishTrack(microphoneTrack, options, ct);
            trackPublished = true;
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

            voiceChatRoom.TrackPublished += OnTrackPublished;
            combinedAudioSource.Play();
        }

        //We listen to track published events so we can add them to the list of streams to evaluate
        private void OnTrackPublished(TrackPublication publication, Participant participant)
        {
            if (publication.Kind == TrackKind.KindAudio)
            {
                WeakReference<IAudioStream> stream = voiceChatRoom.AudioStreams.ActiveStream(participant.Identity, publication.Sid);

                if (stream != null)
                    combinedAudioSource.AddStream(stream);
            }
        }

        private void CloseMedia()
        {
            if (combinedAudioSource != null)
            {
                combinedAudioSource.Stop();
                combinedAudioSource.Free();
            }

            voiceChatRoom.TrackPublished -= OnTrackPublished;
        }
    }
}
