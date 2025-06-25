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
        public float Volume { get; private set; }

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

            currentStream = livekitAddress.Match(
                this,
                onUserStream: static (self, userStream) => //Audio via user stream are not supported yet
                    (self.room.VideoStreams.ActiveStream(userStream.Identity, userStream.Sid), null),
                onCurrentStream: static self =>
                {
                    var videoTrack = self.FirstVideo();
                    var audioTrack = self.FirstAudio();

                    if (audioTrack != null)
                    {
                        self.audioSource.Construct(audioTrack);
                        self.audioSource.Play();
                    }

                    return (videoTrack, audioTrack);
                }
            );

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
            // See: https://github.com/decentraland/unity-explorer/issues/3796
            lock (room.Participants)
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

            // retry to fetch the stream if it's not presented yet
            if (playingAddress != null && currentStream?.video == null)
                OpenMedia(playingAddress.Value);

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
            Volume = target;
            audioSource.SetVolume(target);
        }

        public void SetVolumeFaded(bool isCurrentScene, float targetVolume, float volumeDelta)
        {
            switch (isCurrentScene)
            {
                case true when Volume < targetVolume:
                    SetVolume(Mathf.Min(targetVolume, Volume + volumeDelta));
                    break;
                case false when Volume > 0:
                    SetVolume(Mathf.Max(0, targetVolume - volumeDelta));
                    break;
            }
        }

        public void PlaceAudioAt(Vector3 position)
        {
            audioSource.transform.position = position;
        }
    }
}
