using DCL.Diagnostics;
using DCL.LiveKit.Public;
using DCL.Optimization.ThreadSafePool;
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

        private readonly Func<bool> isRoomRunning;
        private readonly IRoom room;
        private readonly AvatarPlaceHolderTextureSource? placeholderSource;
        private readonly SlideTextureCache slideCache;
        private readonly PresentationCompositor compositor;
        private PlayerState playerState;
        private PresentationBotMetadata? presentation;
        private string? presentationRawMetadata;
        private string? composingBot;
        private CurrentVideoStreamInfo? presentationVideo;
        private CurrentVideoStreamInfo? presenterCamera;
        private Texture2D? shownSlide;
        private string? shownSlideUrl;
        private Texture? composedTexture;
        private int composedFrame = -1;
        private bool loggedBadMetadata;

        private LivekitAddress? requestedAddress;
        private LivekitAddress? playingAddress;

        private CurrentVideoStreamInfo? cvs;

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

        // Set on Connected/Reconnected (FFI thread), consumed on the main thread: any video stream held
        // across a connection change belongs to the torn-down connection and must be dropped AND evicted
        // from the room's stream cache (see EnsureVideoIsPlaying).
        private volatile bool pendingVideoReset;
        private volatile bool pendingPresentationRefresh;

        public bool MediaOpened =>
            // TODO: this is not precise and might introduce inconsistencies depending on the kind of stream needed
            IsVideoOpened || isAudioOpened;

        public float Volume { get; private set; }

        public PlayerState State => playerState;

        public bool IsVideoOpened => isComposing || (cvs.HasValue && cvs.Value.videoStream.Resource.Has);

        // Live LiveKit frames are vertically flipped; the camera-off placeholder and the presentation composite are upright.
        public Vector2 CurrentTextureScale =>
            isComposing || (placeholderSource != null && cvs.HasValue && IsCameraVideoMuted(cvs.Value))
                ? Vector2.one
                : new Vector2(1f, -1f);

#if UNITY_INCLUDE_TESTS
        internal int compositorBlitCount => compositor.blitCount;
#endif

        private bool isAudioOpened => audioSources.Count > 0;

        private bool isComposing =>
            presentation?.slide != null;

        // Both checks needed: connective state catches synchronous teardown start,
        // FFI connection state catches the async disconnect completion.
        // (Delegate, not the connective room type: ECS.Unity -> DCL.Multiplayer is an asmdef cycle.)
        private bool canOpenStreams =>
            isRoomRunning()
            && room.Info.ConnectionState == LKConnectionState.ConnConnected;

        public LivekitPlayer(IRoom streamingRoom, Func<bool> isRoomRunning, AvatarPlaceHolderTextureSource? placeholderSource,
            SlideTextureCache slideCache, Material compositorMaterial)
        {
            this.isRoomRunning = isRoomRunning;
            room = streamingRoom;
            this.placeholderSource = placeholderSource;
            this.slideCache = slideCache;
            compositor = new PresentationCompositor(compositorMaterial);

            room.ConnectionUpdated += OnRoomConnectionUpdated;
            room.TrackSubscribed += OnRoomTrackSubscribed;
            room.TrackUnsubscribed += OnRoomTrackUnsubscribed;
            room.Participants.UpdatesFromParticipant += OnRoomParticipantUpdate;
        }

        public void EnsureVideoIsPlaying()
        {
            if (State != PlayerState.Playing) return;
            if (playingAddress == null) return;

            // Room is tearing down — skip to avoid opening streams with invalid FFI handles,
            // which would poison the reusable stream cache. Pending flags stay set for reconnect.
            if (!canOpenStreams)
            {
                if (presentation != null)
                    DropPresentation();

                EnsureAudioIsPlaying(); // still releases audio sources whose streams died with the room
                return;
            }

            if (pendingVideoReset)
            {
                pendingVideoReset = false;

                // Evict the stale streams from the room's cache so re-open gets a fresh instance.
                ReleaseVideoStream(ref cvs);
                ReleaseVideoStream(ref presentationVideo);
                ReleaseVideoStream(ref presenterCamera);
            }

            // Consume the flag even when IsVideoOpened: prevents stale-flag pile-up while the stream is healthy.
            // We deliberately do NOT re-open an established stream here — TryFollowVideoStreamToActiveSpeaker
            // already handles "look for a better source" safely, and re-allocating cvs while a subscription
            // is mid-flight can stomp the in-flight Weak<IVideoStream> and stall playback (observed on Windows).
            bool rescan = pendingVideoRediscovery;

            if (rescan)
                pendingVideoRediscovery = false;

            if (pendingPresentationRefresh)
            {
                pendingPresentationRefresh = false;
                rescan |= RefreshPresentation();
            }

            if (isComposing)
            {
                cvs = null;
                playingAddress = requestedAddress;
                EnsurePresentationStreams(rescan);
                EnsureAudioIsPlaying();
                return;
            }

            if (IsVideoOpened)
            {
                TryFollowVideoStreamToActiveSpeaker(playingAddress.Value);
            }
            else
            {
                // target was a specific user that went offline or a current-stream that had no tracks,
                // the recovery is: fall back to first-available. With no stream held (composition just ended),
                // reopen the playing address first.
                OpenVideoStream(cvs.HasValue ? LivekitAddress.CurrentStream() : playingAddress.Value);
            }

            // UpdateMediaPlayerSystem has two separate queries: UpdateAudioStream (for PBAudioStream)
            // and UpdateVideoTexture (for PBVideoPlayer). Entities with only PBVideoPlayer never enter
            // the audio query, so we drive audio discovery here to keep LiveKit rooms audible.
            EnsureAudioIsPlaying();
        }

        public void EnsureAudioIsPlaying()
        {
            if (State != PlayerState.Playing) return;
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

            requestedAddress = livekitAddress;
            playingAddress = livekitAddress;

            if (canOpenStreams)
            {
                pendingPresentationRefresh = false;
                RefreshPresentation();
            }

            if (isComposing)
                pendingVideoRediscovery = true;
            else
                OpenVideoStream(livekitAddress);

            OpenMissingAudioStreams();
            playerState = PlayerState.Playing;
        }

        private void OpenVideoStream(LivekitAddress livekitAddress)
        {
            // Room not ready — defer the open; EnsureVideoIsPlaying will retry once connected.
            if (!canOpenStreams)
            {
                cvs = null;
                playingAddress = livekitAddress;
                return;
            }

            StreamKey? streamKey = livekitAddress.Match(
                this,
                onUserStream: static (_, userStream) => new StreamKey(userStream.Identity, userStream.Sid),
                onCurrentStream: static self => self.BestInitialVideoKey()
            );

            if (streamKey.HasValue)
            {
                Weak<IVideoStream> stream = room.VideoStreams.ActiveStream(streamKey.Value);
                cvs = CurrentVideoStreamInfo.New(streamKey.Value, stream);
            }
            else
            {
                cvs = null;
            }

            playingAddress = livekitAddress;
        }

        private void OpenMissingAudioStreams()
        {
            if (!canOpenStreams) return;

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

            // Switch only if the best source actually changed; re-allocating cvs every frame would
            // reset the speaker-hold timer and re-wrap a healthy stream (e.g. while a screen share holds it).
            if (targetKey != null && cvs?.key.Equals(targetKey.Value) != true)
            {
                var currentVideoStream = room.VideoStreams.ActiveStream(targetKey.Value);
                cvs = CurrentVideoStreamInfo.New(targetKey.Value, currentVideoStream);
            }
        }

        // Pure
        private StreamKey? BestFollowCandidate()
        {
            StreamKey? targetKey = PresentationBotVideoKey();

            // Screen share ranks below the presentation bot but above speaker cameras.
            targetKey ??= FirstScreenShareVideoKey();

            // try pick up another key if presentation bot and screen share are unavailable
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

        // Pure. Initial selection priority: presentation bot, then screen share, then first available track.
        private StreamKey? BestInitialVideoKey() =>
            PresentationBotVideoKey() ?? FirstScreenShareVideoKey() ?? FirstAvailableTrackSid(TrackKind.KindVideo);

        private StreamKey? FindVideoTrackForParticipant(string identity) =>
            FindVideoTrack(identity, static track => !IsPresentationVideo(track));

        private StreamKey? FirstScreenShareVideoKey()
        {
            foreach ((string identity, _) in room.Participants.RemoteParticipantIdentities())
            {
                var participant = room.Participants.RemoteParticipant(identity);

                if (participant == null)
                    continue;

                foreach ((string sid, TrackPublication track) in participant.Tracks)
                {
                    // Skip a paused (muted) share so video falls through to the active speaker until it resumes.
                    if (track.Kind == TrackKind.KindVideo && track.Source == TrackSource.SourceScreenshare && !track.Muted && !IsPresentationVideo(track))
                        return new StreamKey(identity, sid);
                }
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
                    if (value.Kind == kind && !IsPresentationVideo(value))
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

        private string? ComposingBotIdentity()
        {
            if (!requestedAddress.HasValue) return null;

            if (requestedAddress.Value.IsUserStream(out UserStream userStream))
                return userStream.Identity.IsPresentationBotIdentity() ? userStream.Identity : null;

            return PresentationBotIdentity();
        }

        private static bool IsPresentationVideo(TrackPublication track) =>
            track.Name == LiveKitMediaExtensions.PRESENTATION_VIDEO_TRACK_NAME;

        private bool RefreshPresentation()
        {
            string? identity = ComposingBotIdentity();
            string? raw = identity == null ? null : room.Participants.RemoteParticipant(identity)?.Metadata;

            if (string.Equals(identity, composingBot, StringComparison.Ordinal) && string.Equals(raw, presentationRawMetadata, StringComparison.Ordinal))
                return false;

            composingBot = identity;
            presentationRawMetadata = raw;
            SetPresentation(PresentationLayout.Parse(raw));

            if (!isComposing)
            {
                presentationVideo = null;
                presenterCamera = null;
            }

            if (presentation == null && !string.IsNullOrEmpty(raw) && !loggedBadMetadata)
            {
                loggedBadMetadata = true;
                ReportHub.LogWarning(ReportCategory.MEDIA_STREAM, "Presentation bot metadata is unparseable or out of bounds, showing the legacy track");
            }

            return true;
        }

        private void SetPresentation(PresentationBotMetadata? value)
        {
            bool wasComposing = isComposing;
            presentation = value;
            composedFrame = -1;

            if (isComposing == wasComposing)
                return;

            if (isComposing)
            {
                slideCache.BeginComposing();
                return;
            }

            shownSlide = null;
            shownSlideUrl = null;
            compositor.Release();
            slideCache.EndComposing();
        }

        private void DropPresentation()
        {
            SetPresentation(null);
            presentationRawMetadata = null;
            composingBot = null;
            pendingPresentationRefresh = true;
        }

        private void EnsurePresentationStreams(bool rescan)
        {
            if (rescan || IsUnresolved(presentationVideo))
            {
                string? bot = composingBot;
                presentationVideo = RebindVideoStream(presentationVideo, bot == null ? null : FindVideoTrack(bot, static track => IsPresentationVideo(track)));
            }

            if (rescan || IsUnresolved(presenterCamera))
            {
                string? presenter = presentation?.presenterIdentity;
                presenterCamera = RebindVideoStream(presenterCamera, presenter == null ? null : FindVideoTrack(presenter, static track => track.Source == TrackSource.SourceCamera));
            }
        }

        private static bool IsUnresolved(CurrentVideoStreamInfo? stream) =>
            stream.HasValue && !stream.Value.videoStream.Resource.Has;

        private StreamKey? FindVideoTrack(string identity, Func<TrackPublication, bool> match)
        {
            // See: solved https://github.com/decentraland/unity-explorer/issues/3796
            // room.Participants is thread-safe
            var participant = room.Participants.RemoteParticipant(identity);

            if (participant == null) return null;

            foreach ((string sid, TrackPublication track) in participant.Tracks)
            {
                if (track.Kind == TrackKind.KindVideo && match(track))
                    return new StreamKey(identity, sid);
            }

            return null;
        }

        private CurrentVideoStreamInfo? RebindVideoStream(CurrentVideoStreamInfo? held, StreamKey? key)
        {
            if (!key.HasValue)
                return null;

            if (held.HasValue && held.Value.key.Equals(key.Value) && held.Value.videoStream.Resource.Has)
                return held;

            return CurrentVideoStreamInfo.New(key.Value, room.VideoStreams.ActiveStream(key.Value));
        }

        private void ReleaseVideoStream(ref CurrentVideoStreamInfo? stream)
        {
            if (!stream.HasValue) return;

            room.VideoStreams.Release(stream.Value.key);
            stream = null;
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
            requestedAddress = null;
            cvs = null;
            presentationVideo = null;
            presenterCamera = null;
            DropPresentation();
            playerState = PlayerState.Stopped;
            ReleaseAllAudioSources();
        }

        public Texture? LastTexture()
        {
            if (playerState is not PlayerState.Playing)
                return null;

            if (isComposing)
            {
                if (composedFrame != UnityEngine.Time.frameCount)
                {
                    composedTexture = ComposePresentation();
                    composedFrame = UnityEngine.Time.frameCount;
                }

                return composedTexture;
            }

            if (!cvs.HasValue || !cvs.Value.videoStream.Resource.Has)
                return null;

            CurrentVideoStreamInfo videoInfo = cvs.Value;
            return CameraOffPlaceholder(videoInfo) ?? videoInfo.videoStream.Resource.Value.DecodeLastFrame();
        }

        private Texture? ComposePresentation()
        {
            PresentationBotMetadata? metadata = presentation;
            PresentationSlide? slide = metadata?.slide;

            if (metadata == null || slide?.url == null || composingBot == null)
                return null;

            Texture2D? cachedSlide = slideCache.GetOrRequest(slide.url, composingBot);

            if (cachedSlide != null)
            {
                shownSlide = cachedSlide;
                shownSlideUrl = slide.url;
            }
            else if (shownSlideUrl != null)
                slideCache.KeepAlive(shownSlideUrl);

            Texture slideTexture = shownSlide != null ? shownSlide : Texture2D.blackTexture;
            bool showRect = PresentationLayout.TryVideoRect(metadata, out Vector4 videoRect);
            Texture? video = showRect ? DecodeLastFrame(presentationVideo) : null;

            Vector4 cameraRect = PresentationLayout.CameraRect(metadata.overlay, slide.width, slide.height);
            Texture? camera = cameraRect.z > 0f && IsPresenterCameraLive() ? DecodeLastFrame(presenterCamera) : null;

            return compositor.Compose(slide.width, slide.height, slideTexture, showRect, videoRect, video, camera, cameraRect);
        }

        private static Texture2D? DecodeLastFrame(CurrentVideoStreamInfo? stream) =>
            stream.HasValue && stream.Value.videoStream.Resource.Has ? stream.Value.videoStream.Resource.Value.DecodeLastFrame() : null;

        private bool IsPresenterCameraLive() =>
            presenterCamera.HasValue && PublicationOf(presenterCamera.Value) is { Muted: false };

        private Texture? CameraOffPlaceholder(CurrentVideoStreamInfo videoInfo) =>
            placeholderSource != null && IsCameraVideoMuted(videoInfo)
                ? placeholderSource.TextureFor(StreamerName(videoInfo))
                : null;

        // Screen-shares are not cameras, so they keep their live frame and never show the placeholder.
        private bool IsCameraVideoMuted(CurrentVideoStreamInfo videoInfo) =>
            PublicationOf(videoInfo) is { Muted: true } track && track.Source != TrackSource.SourceScreenshare;

        private TrackPublication? PublicationOf(CurrentVideoStreamInfo videoInfo)
        {
            var participant = room.Participants.RemoteParticipant(videoInfo.fromIdentity);
            return participant != null && participant.Tracks.TryGetValue(videoInfo.key.sid, out TrackPublication track) ? track : null;
        }

        private string? StreamerName(CurrentVideoStreamInfo videoInfo)
        {
            var participant = room.Participants.RemoteParticipant(videoInfo.fromIdentity);
            return participant == null || string.IsNullOrEmpty(participant.Name) ? null : participant.Name;
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
            compositor.Dispose();
        }

        // The four handlers below are invoked from LiveKit's FFI thread. The class is otherwise
        // main-thread only, so the handlers MUST NOT touch any field other than the volatile
        // pending* flags. Consumption happens on the main thread inside EnsureVideoIsPlaying / EnsureAudioIsPlaying.
        private void OnRoomConnectionUpdated(IRoom _, ConnectionUpdate update, LKDisconnectReason? __)
        {
            if (update is ConnectionUpdate.Connected or ConnectionUpdate.Reconnected)
            {
                pendingVideoReset = true;
                pendingVideoRediscovery = true;
                pendingAudioRediscovery = true;
                pendingPresentationRefresh = true;
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
            switch (update)
            {
                case UpdateFromParticipant.Disconnected:
                    pendingVideoRediscovery = true;
                    pendingAudioRediscovery = true;
                    pendingPresentationRefresh = true;
                    break;
                case UpdateFromParticipant.Connected:
                case UpdateFromParticipant.MetadataChanged:
                    pendingPresentationRefresh = true;
                    break;
            }
        }

        public void Play()
        {
            playerState = PlayerState.Playing;

            foreach (var (source, _) in audioSources.Values)
                source.Play();
        }

        public void Pause()
        {
            playerState = PlayerState.Paused;

            // There is no "pause" for a streaming source.
            foreach (var (source, _) in audioSources.Values)
                source.Stop();
        }

        public void Stop()
        {
            playerState = PlayerState.Stopped;

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
            public readonly StreamKey key;
            public readonly Weak<IVideoStream> videoStream;
            public readonly float switchedAtTime;

            public string fromIdentity => key.identity;

            private CurrentVideoStreamInfo(
                    StreamKey key,
                    Weak<IVideoStream> videoStream,
                    float switchedAtTime)
            {
                this.key = key;
                this.videoStream = videoStream;
                this.switchedAtTime = switchedAtTime;
            }

            public static CurrentVideoStreamInfo New(
                    StreamKey key,
                    Weak<IVideoStream> videoStream)
            {
                return new (
                        key,
                        videoStream,
                        UnityEngine.Time.realtimeSinceStartup
                        );
            }

            public bool IsFromPresentationBot()
            {
                return key.identity.IsPresentationBotIdentity();
            }
        }
    }
}
