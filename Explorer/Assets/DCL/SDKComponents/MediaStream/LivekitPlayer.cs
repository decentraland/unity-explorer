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
    /// <summary>
    /// Main-thread only. Not thread-safe.
    /// </summary>
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

        private const float MIN_SPEAKER_HOLD_SECONDS = 1.5f;
        private const float AUDIO_RESCAN_INTERVAL_SECONDS = 2.0f;
        private const string PRESENTATION_BOT_PREFIX = "presentation-bot:";

        private readonly IRoom room;
        private readonly List<(LivekitAudioSource source, Weak<AudioStream> stream, StreamKey key)> audioSources = new ();
        private readonly List<StreamKey> streamKeysBuffer = new ();
        private Weak<IVideoStream> currentVideoStream = Weak<IVideoStream>.Null;
        private PlayerState playerState;
        private LivekitAddress? playingAddress;
        private Vector3 audioPosition;
        private string? currentVideoIdentity;
        private float videoSwitchedAtTime;
        private float lastAudioScanTime;
        private bool hasLiveAudio;
        private bool disposed;

        public bool MediaOpened =>
            // TODO: this is not precise and might introduce inconsistencies depending on the kind of stream needed
            IsVideoOpened || isAudioOpened;

        public float Volume { get; private set; }

        public PlayerState State => playerState;

        public bool IsVideoOpened => currentVideoStream.Resource.Has;

        private bool isAudioOpened => hasLiveAudio;

        public LivekitPlayer(IRoom streamingRoom)
        {
            room = streamingRoom;
        }

        public void EnsureVideoIsPlaying()
        {
            if (State != PlayerState.PLAYING) return;
            if (playingAddress == null) return;

            if (IsVideoOpened)
            {
                // Video is alive — try to follow the active speaker (CurrentStream only)
                TryFollowActiveSpeaker();
            }
            else
            {
                // If a specific user stream died, fallback to current-stream (first available track)
                if (playingAddress.Value.IsUserStream(out _))
                    ReopenVideoStream(LivekitAddress.CurrentStream());
                else
                    ReopenVideoStream(playingAddress.Value);
            }

            // UpdateMediaPlayerSystem has two separate queries: UpdateAudioStream (for PBAudioStream)
            // and UpdateVideoTexture (for PBVideoPlayer). Entities with only PBVideoPlayer never enter
            // the audio query, so we drive audio discovery here to keep LiveKit rooms audible.
            EnsureAudioIsPlaying();
        }

        public void EnsureAudioIsPlaying()
        {
            if (State != PlayerState.PLAYING) return;
            if (playingAddress == null) return;

            // Remove dead audio sources immediately (no lock needed).
            bool anyDied = false;

            for (int i = audioSources.Count - 1; i >= 0; i--)
            {
                if (!audioSources[i].stream.Resource.Has)
                {
                    anyDied = true;
                    var source = audioSources[i].source;

                    if (source != null)
                        OBJECT_POOL.Release(source);

                    audioSources.RemoveAt(i);
                }
            }

            hasLiveAudio = audioSources.Count > 0;

            // When a stream died, rescan immediately to replace it.
            // Otherwise, throttle rescans to discover new participants without per-frame lock acquisition.
            if (!anyDied)
            {
                if (UnityEngine.Time.realtimeSinceStartup - lastAudioScanTime < AUDIO_RESCAN_INTERVAL_SECONDS)
                    return;
            }

            lastAudioScanTime = UnityEngine.Time.realtimeSinceStartup;
            OpenAllAudioStreams();
            hasLiveAudio = audioSources.Count > 0;
        }

        public void OpenMedia(LivekitAddress livekitAddress)
        {
            CloseCurrentStream();
            lastAudioScanTime = 0f;

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
            hasLiveAudio = audioSources.Count > 0;

            playerState = PlayerState.PLAYING;
            playingAddress = livekitAddress;
            videoSwitchedAtTime = UnityEngine.Time.realtimeSinceStartup;
        }

        private void ReopenVideoStream(LivekitAddress livekitAddress)
        {
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

            playingAddress = livekitAddress;
            videoSwitchedAtTime = UnityEngine.Time.realtimeSinceStartup;
        }

        private void OpenAllAudioStreams()
        {
            CollectAllAudioTracks(streamKeysBuffer);

            foreach (StreamKey key in streamKeysBuffer)
            {
                if (HasAudioSourceForKey(key))
                    continue;

                Weak<AudioStream> audioStream = room.AudioStreams.ActiveStream(key);

                if (!audioStream.Resource.Has)
                    continue;

                LivekitAudioSource source = OBJECT_POOL.Get();
                source.Construct(audioStream);
                source.SetVolume(Volume);
                source.transform.position = audioPosition;
                source.Play();
                audioSources.Add((source, audioStream, key));
            }
        }

        private bool HasAudioSourceForKey(StreamKey key)
        {
            foreach (var (_, _, existingKey) in audioSources)
                if (existingKey.Equals(key)) return true;

            return false;
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

            // If a presentation bot is active, try to switch to it and don't follow speakers.
            if (TrySwitchToPresentationBot()) return;

            // If currently showing a presentation bot, don't switch away to a speaker.
            if (currentVideoIdentity != null && currentVideoIdentity.StartsWith(PRESENTATION_BOT_PREFIX)) return;

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
                StreamKey? fallback = null;

                foreach ((string remoteParticipantIdentity, _) in room.Participants.RemoteParticipantIdentities())
                {
                    var participant = room.Participants.RemoteParticipant(remoteParticipantIdentity);

                    if (participant == null)
                        continue;

                    foreach ((string sid, TrackPublication value) in participant.Tracks)
                    {
                        if (value.Kind == kind)
                        {
                            // Presentation bot always has priority.
                            if (remoteParticipantIdentity.StartsWith(PRESENTATION_BOT_PREFIX))
                                return new StreamKey(remoteParticipantIdentity, sid);

                            fallback ??= new StreamKey(remoteParticipantIdentity, sid);
                        }
                    }
                }

                return fallback;
            }
        }

        private bool TrySwitchToPresentationBot()
        {
            // Already on the presentation bot.
            if (currentVideoIdentity != null && currentVideoIdentity.StartsWith(PRESENTATION_BOT_PREFIX))
                return true;

            StreamKey? botTrack = FindPresentationBotVideoTrack();

            if (botTrack == null) return false;

            currentVideoStream = room.VideoStreams.ActiveStream(botTrack.Value);
            currentVideoIdentity = botTrack.Value.identity;
            videoSwitchedAtTime = UnityEngine.Time.realtimeSinceStartup;
            return true;
        }

        private StreamKey? FindPresentationBotVideoTrack()
        {
            lock (room.Participants)
            {
                foreach ((string identity, _) in room.Participants.RemoteParticipantIdentities())
                {
                    if (!identity.StartsWith(PRESENTATION_BOT_PREFIX))
                        continue;

                    var participant = room.Participants.RemoteParticipant(identity);

                    if (participant == null) continue;

                    foreach ((string sid, TrackPublication track) in participant.Tracks)
                    {
                        if (track.Kind == TrackKind.KindVideo)
                            return new StreamKey(identity, sid);
                    }
                }
            }

            return null;
        }

        private void ReleaseAllAudioSources()
        {
            foreach (var (source, _, _) in audioSources)
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
            currentVideoStream = Weak<IVideoStream>.Null;
            currentVideoIdentity = null;
            hasLiveAudio = false;
            playerState = PlayerState.STOPPED;
            ReleaseAllAudioSources();
        }

        public Texture? LastTexture()
        {
            if (playerState is not PlayerState.PLAYING)
                return null;

            return currentVideoStream.Resource.Has
                ? currentVideoStream.Resource.Value.DecodeLastFrame()
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

            foreach (var (source, _, _) in audioSources)
                source.Play();
        }

        public void Pause()
        {
            playerState = PlayerState.PAUSED;

            // There is no "pause" for a streaming source.
            foreach (var (source, _, _) in audioSources)
                source.Stop();
        }

        public void Stop()
        {
            playerState = PlayerState.STOPPED;

            foreach (var (source, _, _) in audioSources)
                source.Stop();
        }

        public void SetVolume(float target)
        {
            Volume = target;

            foreach (var (source, _, _) in audioSources)
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

            foreach (var (source, _, _) in audioSources)
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
