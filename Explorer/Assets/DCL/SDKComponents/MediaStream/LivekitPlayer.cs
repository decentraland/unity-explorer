using DCL.Diagnostics;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Optimization.ThreadSafePool;
using LiveKit.Proto;
using LiveKit.Rooms;
using LiveKit.Rooms.Streaming.Audio;
using LiveKit.Rooms.TrackPublications;
using LiveKit.Rooms.VideoStreaming;
using System;
using UnityEngine;
using UnityEngine.Pool;

namespace DCL.SDKComponents.MediaStream
{
    public class LivekitPlayer : IDisposable
    {
        private static readonly IObjectPool<LivekitAudioSource> OBJECT_POOL = new ThreadSafeObjectPool<LivekitAudioSource>(
            () => LivekitAudioSource.New(explicitName: true),
            actionOnGet: static source => source.gameObject.SetActive(true),
            actionOnRelease: static source =>
            {
                source.Stop();
                source.Free();
                source.gameObject.SetActive(false);
            });

        private readonly IRoom room;
        private readonly LivekitAudioSource audioSource;
        private (WeakReference<IVideoStream>? video, WeakReference<IAudioStream>? audio)? currentStream;
        private PlayerState playerState;
        private LivekitAddress? playingAddress;

        private bool disposed;

        public bool MediaOpened => currentStream != null;

        public PlayerState State => playerState;

        public LivekitPlayer(IRoomHub roomHub)
        {
            room = roomHub.StreamingRoom();
            audioSource = OBJECT_POOL.Get();
        }

        public void EnsurePlaying()
        {
            if (this is { MediaOpened: false, State: PlayerState.PLAYING, playingAddress: { } address })
                OpenMedia(address);
        }

        public void OpenMedia(LivekitAddress livekitAddress)
        {
            CloseCurrentStream();

            switch (livekitAddress.StreamKind)
            {
                case LivekitAddress.Kind.CURRENT_STREAM:
                    var videoTrack = FirstVideo();
                    var audioTrack = FirstAudio();
                    currentStream = (videoTrack, audioTrack);

                    if (audioTrack != null)
                    {
                        audioSource.Construct(audioTrack);
                        audioSource.Play();
                    }

                    break;
                case LivekitAddress.Kind.USER_STREAM:
                    (string identity, string sid) = livekitAddress.UserStream;

                    //Audio via user stream are not supported yet
                    currentStream = (room.VideoStreams.ActiveStream(identity, sid), null);
                    break;
                default: throw new ArgumentOutOfRangeException();
            }

            playerState = PlayerState.PLAYING;
            playingAddress = livekitAddress;
        }

        private WeakReference<IVideoStream>? FirstVideo()
        {
            var result = FirstAvailableTrackSid(TrackKind.KindVideo);
            if (result.HasValue == false) return null;
            var value = result.Value;
            return room.VideoStreams.ActiveStream(value.identity, value.sid);
        }

        private WeakReference<IAudioStream>? FirstAudio()
        {
            var result = FirstAvailableTrackSid(TrackKind.KindAudio);
            if (result.HasValue == false) return null;
            var value = result.Value;
            return room.AudioStreams.ActiveStream(value.identity, value.sid);
        }

        private (string identity, string sid)? FirstAvailableTrackSid(TrackKind kind)
        {
            foreach (string remoteParticipantIdentity in room.Participants.RemoteParticipantIdentities())
            {
                var participant = room.Participants.RemoteParticipant(remoteParticipantIdentity);

                if (participant == null)
                    continue;

                foreach ((string sid, TrackPublication value) in participant.Tracks)
                    if (value.Kind == kind)
                        return (remoteParticipantIdentity, sid);
            }

            return null;
        }

        public void CloseCurrentStream()
        {
            // doesn't need to dispose the stream, because it's responsibility of the owning room
            currentStream = null;
            playerState = PlayerState.STOPPED;
            audioSource.Stop();
            audioSource.Free();
        }

        public Texture? LastTexture()
        {
            if (playerState is not PlayerState.PLAYING)
                return null;

            return currentStream?.video?.TryGetTarget(out var videoStream) ?? false
                ? videoStream.DecodeLastFrame()
                : null;
        }

        public void Dispose()
        {
            if (disposed)
            {
                ReportHub.LogError(ReportCategory.MEDIA_STREAM, $"Attempt to double dispose {nameof(LivekitPlayer)}");
                return;
            }

            disposed = true;

            CloseCurrentStream();
            OBJECT_POOL.Release(audioSource);
        }

        public void Play()
        {
            playerState = PlayerState.PLAYING;
            audioSource.Play();
        }

        public void Pause()
        {
            playerState = PlayerState.PAUSED;

            //it's actually no "pause" for a streaming source
            audioSource.Stop();
        }

        public void Stop()
        {
            playerState = PlayerState.STOPPED;
            audioSource.Stop();
        }

        public void SetVolume(float target)
        {
            audioSource.SetVolume(target);
        }

        public void PlaceAudioAt(Vector3 position)
        {
            audioSource.transform.position = position;
        }
    }
}
