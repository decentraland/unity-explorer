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

        private readonly IRoom room;
        private readonly Dictionary<StreamKey, (LivekitAudioSource source, Weak<AudioStream> stream)> audioSources = new ();
        private Weak<IVideoStream> currentVideoStream = Weak<IVideoStream>.Null;
        private PlayerState playerState;
        private LivekitAddress? playingAddress;
        private LivekitAddress? currentVideo;
        private Vector3 audioPosition;
        private float videoSwitchedAtTime;
        private float lastAudioScanTime;
        private bool disposed;

        public bool MediaOpened =>
            // TODO: this is not precise and might introduce inconsistencies depending on the kind of stream needed
            IsVideoOpened || isAudioOpened;

        public float Volume { get; private set; }

        public PlayerState State => playerState;

        public bool IsVideoOpened => currentVideoStream.Resource.Has;

        private bool isAudioOpened => audioSources.Count > 0;

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
                // Scenes only author UserStream or CurrentStream addresses, so whether the previous
                // target was a specific user that went offline or a current-stream that had no tracks,
                // the recovery is the same: fall back to first-available.
                ReopenVideoStream(LivekitAddress.CurrentStream());
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

            bool anyDied = false;
            List<StreamKey>? deadKeys = null;

            foreach (var kvp in audioSources)
            {
                if (kvp.Value.stream.Resource.Has) continue;

                anyDied = true;
                (deadKeys ??= new List<StreamKey>()).Add(kvp.Key);

                if (kvp.Value.source != null)
                    OBJECT_POOL.Release(kvp.Value.source);
            }

            if (deadKeys != null)
                foreach (StreamKey k in deadKeys)
                    audioSources.Remove(k);

            // When a stream died, rescan immediately to replace it.
            // Otherwise, throttle rescans to discover new participants without per-frame lock acquisition.
            if (!anyDied && UnityEngine.Time.realtimeSinceStartup - lastAudioScanTime < AUDIO_RESCAN_INTERVAL_SECONDS)
                return;

            lastAudioScanTime = UnityEngine.Time.realtimeSinceStartup;
            OpenMissingAudioStreams();
        }

        public void OpenMedia(LivekitAddress livekitAddress)
        {
            CloseCurrentStream();
            lastAudioScanTime = 0f;

            currentVideoStream = livekitAddress.Match(
                this,
                onUserStream: static (self, userStream) => self.OpenUserVideoStream(userStream),
                onPresentationBotStream: static (self, bot) => self.OpenPresentationBotVideoStream(bot),
                onCurrentStream: static self => self.FirstVideoTrackingIdentity()
            );

            OpenMissingAudioStreams();

            playerState = PlayerState.PLAYING;
            playingAddress = livekitAddress;
            videoSwitchedAtTime = UnityEngine.Time.realtimeSinceStartup;
        }

        private void ReopenVideoStream(LivekitAddress livekitAddress)
        {
            currentVideoStream = livekitAddress.Match(
                this,
                onUserStream: static (self, userStream) => self.OpenUserVideoStream(userStream),
                onPresentationBotStream: static (self, bot) => self.OpenPresentationBotVideoStream(bot),
                onCurrentStream: static self => self.FirstVideoTrackingIdentity()
            );

            playingAddress = livekitAddress;
            videoSwitchedAtTime = UnityEngine.Time.realtimeSinceStartup;
        }

        private Weak<IVideoStream> OpenUserVideoStream(UserStream userStream)
        {
            currentVideo = LivekitAddress.FromUserStream(userStream);
            return room.VideoStreams.ActiveStream(new StreamKey(userStream.Identity, userStream.Sid));
        }

        private Weak<IVideoStream> OpenPresentationBotVideoStream(PresentationBotStream bot)
        {
            currentVideo = LivekitAddress.FromPresentationBotStream(bot);
            return room.VideoStreams.ActiveStream(new StreamKey(bot.Identity, bot.Sid));
        }

        private void OpenMissingAudioStreams()
        {
            lock (room.Participants)
            {
                foreach ((string identity, _) in room.Participants.RemoteParticipantIdentities())
                {
                    var participant = room.Participants.RemoteParticipant(identity);

                    if (participant == null)
                        continue;

                    foreach ((string sid, TrackPublication track) in participant.Tracks)
                    {
                        if (track.Kind != TrackKind.KindAudio)
                            continue;

                        var key = new StreamKey(identity, sid);

                        if (audioSources.ContainsKey(key))
                            continue;

                        Weak<AudioStream> audioStream = room.AudioStreams.ActiveStream(key);

                        if (!audioStream.Resource.Has)
                            continue;

                        LivekitAudioSource source = OBJECT_POOL.Get();
                        source.Construct(audioStream);
                        source.SetVolume(Volume);
                        source.transform.position = audioPosition;
                        source.Play();
                        audioSources[key] = (source, audioStream);
                    }
                }
            }
        }

        private Weak<IVideoStream> FirstVideoTrackingIdentity()
        {
            StreamKey? result = FirstAvailableTrackSid(TrackKind.KindVideo);

            if (result.HasValue == false)
            {
                currentVideo = null;
                return Weak<IVideoStream>.Null;
            }

            currentVideo = LivekitAddress.FromIdentity(result.Value.identity, result.Value.sid);
            return room.VideoStreams.ActiveStream(result.Value);
        }

        private void TryFollowActiveSpeaker()
        {
            if (playingAddress is not { } playing) return;
            if (playing.IsUserStream(out _)) return;

            // If a presentation bot is active, try to switch to it and don't follow speakers.
            if (TrySwitchToPresentationBot()) return;

            // If currently showing a presentation bot, don't switch away to a speaker.
            if (currentVideo?.IsPresentationBot == true) return;

            if (UnityEngine.Time.realtimeSinceStartup - videoSwitchedAtTime < MIN_SPEAKER_HOLD_SECONDS) return;

            List<string> activeSpeakersSnapshot;

            try
            {
                if (room.ActiveSpeakers.Count == 0) return;

                // ActiveSpeakers is backed by a plain List<string> in the livekit-sdk
                // (DefaultActiveSpeakers) mutated on the FFI thread without synchronization.
                // The SDK owns this list, so a lock on our side would not protect FFI-thread writes —
                // snapshot-under-retry is the best defence available without an SDK change.
                activeSpeakersSnapshot = new List<string>(room.ActiveSpeakers);
            }
            catch (InvalidOperationException)
            {
                // Collection modified during snapshot; retry next frame.
                return;
            }

            if (activeSpeakersSnapshot.Count == 0) return;

            string activeSpeaker = activeSpeakersSnapshot[0];

            if (activeSpeaker == currentVideo?.Identity) return;

            StreamKey? videoTrack = FindVideoTrackForParticipant(activeSpeaker);

            if (videoTrack == null) return;

            currentVideoStream = room.VideoStreams.ActiveStream(videoTrack.Value);
            currentVideo = LivekitAddress.FromIdentity(activeSpeaker, videoTrack.Value.sid);
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
                            if (remoteParticipantIdentity.IsPresentationBotIdentity())
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
            if (currentVideo?.IsPresentationBot == true)
                return true;

            StreamKey? botTrack = FindPresentationBotVideoTrack();

            if (botTrack == null) return false;

            currentVideoStream = room.VideoStreams.ActiveStream(botTrack.Value);
            currentVideo = LivekitAddress.FromPresentationBotStream(new PresentationBotStream(botTrack.Value.identity, botTrack.Value.sid));
            videoSwitchedAtTime = UnityEngine.Time.realtimeSinceStartup;
            return true;
        }

        private StreamKey? FindPresentationBotVideoTrack()
        {
            lock (room.Participants)
            {
                foreach ((string identity, _) in room.Participants.RemoteParticipantIdentities())
                {
                    if (!identity.IsPresentationBotIdentity())
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
            foreach (var (source, _) in audioSources.Values)
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
            currentVideo = null;
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

            foreach (var (source, _) in audioSources.Values)
                source.Play();
        }

        public void Pause()
        {
            playerState = PlayerState.PAUSED;

            // There is no "pause" for a streaming source.
            foreach (var (source, _) in audioSources.Values)
                source.Stop();
        }

        public void Stop()
        {
            playerState = PlayerState.STOPPED;

            foreach (var (source, _) in audioSources.Values)
                source.Stop();
        }

        public void SetVolume(float target)
        {
            Volume = target;

            foreach (var (source, _) in audioSources.Values)
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

            foreach (var (source, _) in audioSources.Values)
                source.transform.position = position;
        }

        /// <summary>
        /// MUST be used in place, caller doesn't take ownership of the reference.
        /// Returns any one of the currently-playing audio sources for visualization purposes.
        /// With multiple remote participants, LivekitPlayer holds one audio source per
        /// participant track; this method is non-deterministic about which one is returned.
        /// </summary>
        public AudioSource? AnyExposedAudioSource()
        {
            // Could be cached in LivekitAudioSource in future.
            // Strongly NOT RECOMMENDED to cache it here (LivekitPlayer.cs)
            // to avoid implementation coupling and possiblity of caching bugs.
            foreach (var (source, _) in audioSources.Values)
                return source.gameObject.GetComponent<AudioSource>();

            return null;
        }
    }
}
