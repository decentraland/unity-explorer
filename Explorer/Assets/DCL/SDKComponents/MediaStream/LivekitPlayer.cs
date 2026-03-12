using DCL.Diagnostics;
using DCL.Optimization.ThreadSafePool;
using LiveKit.Proto;
using LiveKit.Rooms;
using LiveKit.Rooms.Streaming;
using LiveKit.Rooms.Streaming.Audio;
using LiveKit.Rooms.TrackPublications;
using LiveKit.Rooms.VideoStreaming;
using RichTypes;
using System;
using System.Collections.Generic;
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
                source?.Stop();
                source?.Free();
                source?.gameObject.SetActive(false);
            });

        private readonly IRoom room;
        private readonly List<(LivekitAudioSource source, Weak<AudioStream> stream)> audioSources = new ();
        private readonly List<StreamKey> tempStreamKeys = new ();
        private Weak<IVideoStream>? currentVideoStream;
        private PlayerState playerState;
        private LivekitAddress? playingAddress;
        private Vector3 audioPosition;

        private string? currentVideoIdentity;
        private float videoSwitchedAtTime;
        private const float MIN_SPEAKER_HOLD_SECONDS = 1.5f;

        private bool disposed;

        public bool MediaOpened =>
            // TODO: this is not precise and might introduce inconsistencies depending on the kind of stream needed
            IsVideoOpened || isAudioOpened;

        public float Volume { get; private set; }

        public PlayerState State => playerState;

        public bool IsVideoOpened => currentVideoStream != null && currentVideoStream.Value.Resource.Has;

        private bool isAudioOpened => audioSources.Count > 0
            && audioSources.Exists(static a => a.stream.Resource.Has);

        public LivekitPlayer(IRoom streamingRoom)
        {
            room = streamingRoom;
        }

        public void EnsureVideoIsPlaying()
        {
            if (State != PlayerState.PLAYING) return;
            if (playingAddress == null) return;

            if (!IsVideoOpened)
            {
                // If a specific user stream died, fallback to current-stream (first available track)
                if (playingAddress.Value.IsUserStream(out _))
                {
                    OpenMedia(LivekitAddress.CurrentStream());
                    return;
                }

                OpenMedia(playingAddress.Value);
                return;
            }

            // Video is alive — try to follow the active speaker (CurrentStream only)
            TryFollowActiveSpeaker();
        }

        public void EnsureAudioIsPlaying()
        {
            if (State != PlayerState.PLAYING) return;
            if (playingAddress == null) return;

            // Check if any audio stream died — if so, refresh all audio
            bool anyDied = false;

            foreach (var (_, stream) in audioSources)
            {
                if (!stream.Resource.Has)
                {
                    anyDied = true;
                    break;
                }
            }

            if (!anyDied && audioSources.Count > 0) return;

            // Release dead sources and re-collect all audio
            ReleaseAllAudioSources();
            OpenAllAudioStreams();
        }

        public void OpenMedia(LivekitAddress livekitAddress)
        {
            CloseCurrentStream();

            currentVideoIdentity = null;

            currentVideoStream = livekitAddress.Match(
                this,
                onUserStream: static (self, userStream) =>
                {
                    self.currentVideoIdentity = userStream.Identity;
                    return self.room.VideoStreams.ActiveStream(new StreamKey(userStream.Identity, userStream.Sid));
                },
                onCurrentStream: static self => self.FirstVideoTrackingIdentity()
            );

            OpenAllAudioStreams();

            playerState = PlayerState.PLAYING;
            playingAddress = livekitAddress;
            videoSwitchedAtTime = UnityEngine.Time.realtimeSinceStartup;
        }

        private void OpenAllAudioStreams()
        {
            CollectAllAudioTracks(tempStreamKeys);

            foreach (StreamKey key in tempStreamKeys)
            {
                Weak<AudioStream> audioStream = room.AudioStreams.ActiveStream(key);

                if (!audioStream.Resource.Has)
                    continue;

                LivekitAudioSource source = OBJECT_POOL.Get();
                source.Construct(audioStream);
                source.SetVolume(Volume);
                source.transform.position = audioPosition;
                source.Play();
                audioSources.Add((source, audioStream));
            }
        }

        private void CollectAllAudioTracks(List<StreamKey> output)
        {
            output.Clear();

            lock (room.Participants)
            {
                foreach ((string identity, _) in room.Participants.RemoteParticipantIdentities())
                {
                    var participant = room.Participants.RemoteParticipant(identity);

                    if (participant == null)
                        continue;

                    foreach ((string sid, TrackPublication track) in participant.Tracks)
                        if (track.Kind == TrackKind.KindAudio)
                            output.Add(new StreamKey(identity, sid));
                }
            }
        }

        private Weak<IVideoStream> FirstVideoTrackingIdentity()
        {
            StreamKey? result = FirstAvailableTrackSid(TrackKind.KindVideo);

            if (result.HasValue == false)
            {
                currentVideoIdentity = null;
                return Weak<IVideoStream>.Null;
            }

            currentVideoIdentity = result.Value.identity;
            return room.VideoStreams.ActiveStream(result.Value);
        }

        private void TryFollowActiveSpeaker()
        {
            if (playingAddress!.Value.IsUserStream(out _)) return;

            if (UnityEngine.Time.realtimeSinceStartup - videoSwitchedAtTime < MIN_SPEAKER_HOLD_SECONDS) return;

            if (room.ActiveSpeakers.Count == 0) return;

            string? dominantSpeaker = null;

            foreach (string speakerIdentity in room.ActiveSpeakers)
            {
                dominantSpeaker = speakerIdentity;
                break;
            }

            if (dominantSpeaker == null) return;
            if (dominantSpeaker == currentVideoIdentity) return;

            StreamKey? videoTrack = FindVideoTrackForParticipant(dominantSpeaker);

            if (videoTrack == null) return;

            currentVideoStream = room.VideoStreams.ActiveStream(videoTrack.Value);
            currentVideoIdentity = dominantSpeaker;
            videoSwitchedAtTime = UnityEngine.Time.realtimeSinceStartup;
        }

        private StreamKey? FindVideoTrackForParticipant(string identity)
        {
            lock (room.Participants)
            {
                var participant = room.Participants.RemoteParticipant(identity);

                if (participant == null) return null;

                foreach ((string sid, TrackPublication track) in participant.Tracks)
                {
                    if (track.Kind == TrackKind.KindVideo)
                        return new StreamKey(identity, sid);
                }
            }

            return null;
        }

        private StreamKey? FirstAvailableTrackSid(TrackKind kind)
        {
            // See: https://github.com/decentraland/unity-explorer/issues/3796
            lock (room.Participants)
            {
                foreach ((string remoteParticipantIdentity, _) in room.Participants.RemoteParticipantIdentities())
                {
                    var participant = room.Participants.RemoteParticipant(remoteParticipantIdentity);

                    if (participant == null)
                        continue;

                    foreach ((string sid, TrackPublication value) in participant.Tracks)
                        if (value.Kind == kind)
                            return new StreamKey(remoteParticipantIdentity, sid);
                }
            }

            return null;
        }

        /// <summary>
        /// Finds the audio track paired to a specific video track from the same participant.
        /// Available for future targeted audio scenarios.
        /// </summary>
        private Weak<AudioStream> FindPairedAudio(string identity, string videoSid)
        {
            lock (room.Participants)
            {
                var participant = room.Participants.RemoteParticipant(identity);

                if (participant == null)
                    return Weak<AudioStream>.Null;

                TrackSource? targetAudioSource = null;

                foreach ((string sid, TrackPublication track) in participant.Tracks)
                {
                    if (sid == videoSid)
                    {
                        targetAudioSource = track.Source switch
                        {
                            TrackSource.SourceCamera => TrackSource.SourceMicrophone,
                            TrackSource.SourceScreenshare => TrackSource.SourceScreenshareAudio,
                            _ => null,
                        };

                        break;
                    }
                }

                if (targetAudioSource == null)
                    return Weak<AudioStream>.Null;

                foreach ((string sid, TrackPublication track) in participant.Tracks)
                {
                    if (track.Source == targetAudioSource)
                        return room.AudioStreams.ActiveStream(new StreamKey(identity, sid));
                }
            }

            return Weak<AudioStream>.Null;
        }

        private void ReleaseAllAudioSources()
        {
            foreach (var (source, _) in audioSources)
            {
                // Source might already be destroyed when closing the game with a running livekit stream.
                if (source != null)
                    OBJECT_POOL.Release(source);
            }

            audioSources.Clear();
        }

        public void CloseCurrentStream()
        {
            // Doesn't need to dispose the stream, because it's responsibility of the owning room.
            currentVideoStream = null;
            currentVideoIdentity = null;
            playerState = PlayerState.STOPPED;
            ReleaseAllAudioSources();
        }

        public Texture? LastTexture()
        {
            if (playerState is not PlayerState.PLAYING)
                return null;

            return currentVideoStream != null && currentVideoStream.Value.Resource.Has
                ? currentVideoStream.Value.Resource.Value.DecodeLastFrame()
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
        }

        public void Play()
        {
            playerState = PlayerState.PLAYING;

            foreach (var (source, _) in audioSources)
                source.Play();
        }

        public void Pause()
        {
            playerState = PlayerState.PAUSED;

            // There is no "pause" for a streaming source.
            foreach (var (source, _) in audioSources)
                source.Stop();
        }

        public void Stop()
        {
            playerState = PlayerState.STOPPED;

            foreach (var (source, _) in audioSources)
                source.Stop();
        }

        public void SetVolume(float target)
        {
            Volume = target;

            foreach (var (source, _) in audioSources)
                source.SetVolume(target);
        }

        public void CrossfadeVolume(float targetVolume, float volumeDelta)
        {
            SetVolume(Volume > targetVolume
                ? Mathf.Max(0, targetVolume - volumeDelta)
                : Mathf.Min(targetVolume, Volume + volumeDelta));
        }

        public void PlaceAudioAt(Vector3 position)
        {
            audioPosition = position;

            foreach (var (source, _) in audioSources)
                source.transform.position = position;
        }

        /// <summary>
        /// MUST be used in place, caller doesn't take ownership of the reference.
        /// Returns the first available audio source for audio visualization purposes.
        /// </summary>
        public AudioSource? ExposedAudioSource()
        {
            if (audioSources.Count == 0)
                return null;

            return audioSources[0].source.gameObject.GetComponent<AudioSource>();
        }
    }
}
