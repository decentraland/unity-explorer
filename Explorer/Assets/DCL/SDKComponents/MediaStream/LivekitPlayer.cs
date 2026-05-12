using DCL.Diagnostics;
using DCL.LiveKit.Public;
using DCL.Optimization.ThreadSafePool;
using DCL.SDKComponents.MediaStream;
using LiveKit.Proto;
using LiveKit.Rooms;
using LiveKit.Rooms.Participants;
using LiveKit.Rooms.Streaming;
using LiveKit.Rooms.Streaming.Audio;
using LiveKit.Rooms.TrackPublications;
using LiveKit.Rooms.Tracks;
using LiveKit.Rooms.VideoStreaming;
using RichTypes;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using REnum;

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
        private PlayerState playerState;

        private LivekitAddress? playingAddress;

        private CurrentVideoStreamInfo? cvs = null;

        private readonly Dictionary<StreamKey, (LivekitAudioSource source, Weak<AudioStream> stream)> audioSources = new ();
        private Vector3 audioPosition;
        private float lastAudioScanTime;

        private bool disposed;

        // Set from LiveKit FFI callbacks (off main thread); consumed by EnsureVideoIsPlaying / EnsureAudioIsPlaying
        // on the main thread. Forces immediate re-discovery so we don't wait for the polling cycle in cases
        // where the streamer was already publishing before we joined (TrackPublication visible, but
        // trackPublication.Track stays null until subscription completes — see LiveKit Streams.ActiveStream).
        private volatile bool pendingVideoRediscovery;
        private volatile bool pendingAudioRediscovery;

        public bool MediaOpened =>
            // TODO: this is not precise and might introduce inconsistencies depending on the kind of stream needed
            IsVideoOpened || isAudioOpened;

        public float Volume { get; private set; }

        public PlayerState State => playerState;

        public bool IsVideoOpened => cvs.HasValue && cvs.Value.videoStream.Resource.Has;

        private bool isAudioOpened => audioSources.Count > 0;

        public LivekitPlayer(IRoom streamingRoom)
        {
            room = streamingRoom;

            room.ConnectionUpdated += OnRoomConnectionUpdated;
            room.TrackSubscribed += OnRoomTrackSubscribed;
            room.TrackUnsubscribed += OnRoomTrackUnsubscribed;
            room.Participants.UpdatesFromParticipant += OnRoomParticipantUpdate;
        }

        public void EnsureVideoIsPlaying()
        {
            if (State != PlayerState.PLAYING) return;
            if (playingAddress == null) return;

            bool forceRediscover = pendingVideoRediscovery;
            if (forceRediscover) pendingVideoRediscovery = false;

            if (IsVideoOpened && !forceRediscover)
            {
                TryFollowVideoStreamToActiveSpeaker(playingAddress.Value);
            }
            else
            {
                // target was a specific user that went offline or a current-stream that had no tracks,
                // the recovery is: fall back to first-available.
                OpenVideoStream(LivekitAddress.CurrentStream());
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

            bool forceRediscover = pendingAudioRediscovery;
            if (forceRediscover) pendingAudioRediscovery = false;

            using var _ = ListPool<StreamKey>.Get(out List<StreamKey> deadKeys);

            foreach (var kvp in audioSources)
            {
                if (kvp.Value.stream.Resource.Has) continue;
                deadKeys.Add(kvp.Key);
                if (kvp.Value.source != null) OBJECT_POOL.Release(kvp.Value.source);
            }

            foreach (StreamKey k in deadKeys) audioSources.Remove(k);

            // When a stream died OR a room event signaled a change, rescan immediately.
            // Otherwise, throttle rescans to discover new participants without per-frame lock acquisition.
            if (!forceRediscover && deadKeys.Count == 0 && UnityEngine.Time.realtimeSinceStartup - lastAudioScanTime < AUDIO_RESCAN_INTERVAL_SECONDS)
                return;

            lastAudioScanTime = UnityEngine.Time.realtimeSinceStartup;
            OpenMissingAudioStreams();
        }

        public void OpenMedia(LivekitAddress livekitAddress)
        {
            CloseCurrentStream();
            lastAudioScanTime = 0f;

            OpenVideoStream(livekitAddress);
            OpenMissingAudioStreams();
            playerState = PlayerState.PLAYING;
        }

        private void OpenVideoStream(LivekitAddress livekitAddress)
        {
            StreamKey? streamKey = livekitAddress.Match(
                this,
                onUserStream: static (self, userStream) => new StreamKey(userStream.Identity, userStream.Sid),
                onCurrentStream: static self => self.FirstAvailableTrackSid(TrackKind.KindVideo)
            );

            if (streamKey.HasValue)
            {
                Weak<IVideoStream> stream = room.VideoStreams.ActiveStream(streamKey.Value);
                cvs = CurrentVideoStreamInfo.New(streamKey.Value.identity, stream);
            }
            else
            {
                cvs = null;
            }

            playingAddress = livekitAddress;
        }

        private void OpenMissingAudioStreams()
        {
            foreach ((string identity, _) in room.Participants.RemoteParticipantIdentities())
            {
                var participant = room.Participants.RemoteParticipant(identity);

                if (participant == null)
                    continue;

                // participant.Tracks are thread-safe
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

        private void TryFollowVideoStreamToActiveSpeaker(LivekitAddress address)
        {
            if (address.IsUserStream(out _)) return; // if stream dedicated user then don't auto-follow

            if (cvs?.IsFromPresentationBot() ?? false) return; // already streams high-priority presentation bot

            StreamKey? targetKey = BestFollowCandidate();

            // switch if found
            if (targetKey != null)
            {
                var currentVideoStream = room.VideoStreams.ActiveStream(targetKey.Value);
                cvs = CurrentVideoStreamInfo.New(targetKey.Value.identity, currentVideoStream);
            }
        }

        // Pure
        private StreamKey? BestFollowCandidate()
        {
            StreamKey? targetKey = PresentationBotVideoKey();

            // try pick up another key if PresentationBot is unavailable
            if (targetKey == null)
            {
                float lastSwitch = cvs?.switchedAtTime ?? 0;
                float delta = UnityEngine.Time.realtimeSinceStartup - lastSwitch;

                // attempt to switch only if hold exceeds
                if (delta > MIN_SPEAKER_HOLD_SECONDS)
                {
                    foreach (string activeSpeaker in room.ActiveSpeakers)
                    {
                        if (activeSpeaker == cvs?.fromIdentity)
                            break; // we don't need to switch if he is already playing

                        targetKey = FindVideoTrackForParticipant(activeSpeaker);
                        if (targetKey != null)
                            break;
                    }
                }
            }

            return targetKey;
        }

        private StreamKey? FindVideoTrackForParticipant(string identity)
        {
            // See: solved https://github.com/decentraland/unity-explorer/issues/3796
            // room.Participants is thread-safe
            var participant = room.Participants.RemoteParticipant(identity);

            if (participant == null) return null;

            foreach ((string sid, TrackPublication track) in participant.Tracks)
            {
                if (track.Kind == TrackKind.KindVideo)
                    return new StreamKey(identity, sid);
            }

            return null;
        }

        private StreamKey? FirstAvailableTrackSid(TrackKind kind)
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

        // Pure
        private StreamKey? PresentationBotVideoKey()
        {
            string? identity = PresentationBotIdentity();
            if (identity == null) return null;
            return FindVideoTrackForParticipant(identity);
        }

        // Pure
        private string? PresentationBotIdentity()
        {
            foreach ((string identity, _) in room.Participants.RemoteParticipantIdentities())
                if (identity.IsPresentationBotIdentity())
                    return identity;

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
            cvs = null;
            playerState = PlayerState.STOPPED;
            ReleaseAllAudioSources();
        }

        public Texture? LastTexture()
        {
            if (playerState is not PlayerState.PLAYING)
                return null;

            return cvs.HasValue && cvs.Value.videoStream.Resource.Has
                ? cvs.Value.videoStream.Resource.Value.DecodeLastFrame()
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

            room.ConnectionUpdated -= OnRoomConnectionUpdated;
            room.TrackSubscribed -= OnRoomTrackSubscribed;
            room.TrackUnsubscribed -= OnRoomTrackUnsubscribed;
            room.Participants.UpdatesFromParticipant -= OnRoomParticipantUpdate;

            CloseCurrentStream();
        }

        // The four handlers below are invoked from LiveKit's FFI thread. The class is otherwise
        // main-thread only, so the handlers MUST NOT touch any field other than the volatile rediscovery
        // flags. Consumption happens on the main thread inside EnsureVideoIsPlaying / EnsureAudioIsPlaying.
        private void OnRoomConnectionUpdated(IRoom _, ConnectionUpdate update, LKDisconnectReason? __)
        {
            if (update is ConnectionUpdate.Connected or ConnectionUpdate.Reconnected)
            {
                pendingVideoRediscovery = true;
                pendingAudioRediscovery = true;
            }
        }

        private void OnRoomTrackSubscribed(ITrack _, TrackPublication publication, LKParticipant __)
        {
            // Fixes the deep-link case: streamer published before we joined, so participant.Tracks held
            // the publication but trackPublication.Track was null until subscription completed. The
            // poll-based retry kept getting Weak.Null from VideoStreams.ActiveStream — this event fires
            // precisely when ActiveStream becomes resolvable.
            switch (publication.Kind)
            {
                case TrackKind.KindVideo:
                    pendingVideoRediscovery = true;
                    break;
                case TrackKind.KindAudio:
                    pendingAudioRediscovery = true;
                    break;
            }
        }

        private void OnRoomTrackUnsubscribed(ITrack _, TrackPublication publication, LKParticipant __)
        {
            switch (publication.Kind)
            {
                case TrackKind.KindVideo:
                    pendingVideoRediscovery = true;
                    break;
                case TrackKind.KindAudio:
                    pendingAudioRediscovery = true;
                    break;
            }
        }

        private void OnRoomParticipantUpdate(LKParticipant _, UpdateFromParticipant update)
        {
            if (update == UpdateFromParticipant.Disconnected)
            {
                pendingVideoRediscovery = true;
                pendingAudioRediscovery = true;
            }
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

        private readonly struct CurrentVideoStreamInfo
        {
            public readonly string fromIdentity;
            public readonly Weak<IVideoStream> videoStream;
            public readonly float switchedAtTime;

            private CurrentVideoStreamInfo(
                    string fromIdentity,
                    Weak<IVideoStream> videoStream,
                    float switchedAtTime)
            {
                this.fromIdentity = fromIdentity;
                this.videoStream = videoStream;
                this.switchedAtTime = switchedAtTime;
            }

            public static CurrentVideoStreamInfo New(
                    string fromIdentity,
                    Weak<IVideoStream> videoStream)
            {
                return new (
                        fromIdentity,
                        videoStream,
                        UnityEngine.Time.realtimeSinceStartup
                        );
            }

            // add "IsFromPresentationBot"?
            public bool IsFromPresentationBot()
            {
                return fromIdentity.IsPresentationBotIdentity();
            }
        }
    }
}
