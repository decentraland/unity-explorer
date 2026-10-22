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
using RichTypes;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace DCL.SDKComponents.MediaStream
{
    /// <summary>
    /// Main-thread only. Not thread-safe. What it shows is a <see cref="LivekitVideoMode" />, changed only through the
    /// transition methods <see cref="Defer" />, <see cref="Follow" />, <see cref="StartComposing" /> and <see cref="StopComposing" />.
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
        private LivekitVideoMode video = LivekitVideoMode.Closed();
        private (string? bot, string? metadata) presentationSource;
        private bool loggedBadMetadata;

        private readonly Dictionary<StreamKey, (LivekitAudioSource source, Weak<AudioStream> stream)> audioSources = new ();
        private Vector3 audioPosition;
        private float lastAudioScanTime;

        private bool disposed;

        // Set from LiveKit FFI callbacks (off main thread) and from the main thread to rescan on the next update;
        // consumed by EnsureVideoIsPlaying / EnsureAudioIsPlaying on the main thread. Forces immediate re-discovery so we don't wait for the polling cycle in cases
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

        public bool IsVideoOpened => video.IsComposingVideo(out _) || (video.IsLegacyVideo(out LegacyVideo legacy) && legacy.Stream.IsLive);

        // Live LiveKit frames are vertically flipped; the camera-off placeholder and the presentation composite are upright.
        public Vector2 CurrentTextureScale =>
            video.IsComposingVideo(out _) || (placeholderSource != null && video.IsLegacyVideo(out LegacyVideo legacy) && IsCameraVideoMuted(legacy.Stream))
                ? Vector2.one
                : new Vector2(1f, -1f);

#if UNITY_INCLUDE_TESTS
        internal int compositorBlitCount => compositor.blitCount;
#endif

        private bool isAudioOpened => audioSources.Count > 0;

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
            if (video.Requested is not { } requested) return;

            // Room is tearing down — skip to avoid opening streams with invalid FFI handles,
            // which would poison the reusable stream cache. Pending flags stay set for reconnect.
            if (!canOpenStreams)
            {
                DropPresentation();
                EnsureAudioIsPlaying(); // still releases audio sources whose streams died with the room
                return;
            }

            if (pendingVideoReset)
            {
                pendingVideoReset = false;

                // Evict the stale streams from the room's cache so re-open gets a fresh instance.
                ReleaseHeldVideoStreams();
            }

            // Consume the flag even when IsVideoOpened: prevents stale-flag pile-up while the stream is healthy.
            // We deliberately do NOT re-open an established stream here — TryFollowVideoStreamToActiveSpeaker
            // already handles "look for a better source" safely, and re-allocating the stream while a subscription
            // is mid-flight can stomp the in-flight Weak<IVideoStream> and stall playback (observed on Windows).
            bool rescan = pendingVideoRediscovery;

            if (rescan)
                pendingVideoRediscovery = false;

            if (pendingPresentationRefresh)
            {
                pendingPresentationRefresh = false;
                rescan |= RefreshPresentation(requested);
            }

            if (video.IsComposingVideo(out ComposingVideo composing))
                EnsurePresentationStreams(composing, rescan);
            else if (video.IsLegacyVideo(out LegacyVideo legacy))
            {
                if (legacy.Stream.IsLive)
                    TryFollowVideoStreamToActiveSpeaker(legacy);
                else
                {
                    // target was a specific user that went offline or a current-stream that had no tracks,
                    // the recovery is: fall back to first-available.
                    OpenVideoStream(legacy.Requested, LivekitAddress.CurrentStream());
                }
            }
            else if (video.IsDeferredVideo(out DeferredVideo deferred))
                OpenVideoStream(deferred.Requested, deferred.Target);

            // UpdateMediaPlayerSystem has two separate queries: UpdateAudioStream (for PBAudioStream)
            // and UpdateVideoTexture (for PBVideoPlayer). Entities with only PBVideoPlayer never enter
            // the audio query, so we drive audio discovery here to keep LiveKit rooms audible.
            EnsureAudioIsPlaying();
        }

        public void EnsureAudioIsPlaying()
        {
            if (State != PlayerState.Playing) return;
            if (video.IsClosed()) return;

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
            Defer(livekitAddress, livekitAddress);

            if (canOpenStreams)
            {
                pendingPresentationRefresh = false;
                RefreshPresentation(livekitAddress);
            }

            if (video.IsComposingVideo(out _))
                pendingVideoRediscovery = true;
            else
                OpenVideoStream(livekitAddress, livekitAddress);

            OpenMissingAudioStreams();
            playerState = PlayerState.Playing;
        }

        private void OpenVideoStream(LivekitAddress requested, LivekitAddress target)
        {
            // Room not ready — defer the open; EnsureVideoIsPlaying will retry once connected.
            if (!canOpenStreams)
            {
                Defer(requested, target);
                return;
            }

            StreamKey? streamKey = target.Match(
                this,
                onUserStream: static (_, userStream) => new StreamKey(userStream.Identity, userStream.Sid),
                onCurrentStream: static self => self.BestInitialVideoKey()
            );

            if (streamKey.HasValue)
                Follow(requested, target, streamKey.Value);
            else
                Defer(requested, target);
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

        private void TryFollowVideoStreamToActiveSpeaker(LegacyVideo legacy)
        {
            if (legacy.Target.IsUserStream(out _)) return; // if stream dedicated user then don't auto-follow

            if (legacy.Stream.IsFromPresentationBot()) return; // already streams high-priority presentation bot

            StreamKey? targetKey = BestFollowCandidate(legacy.Stream);

            // Switch only if the best source actually changed; re-allocating the stream every frame would
            // reset the speaker-hold timer and re-wrap a healthy stream (e.g. while a screen share holds it).
            if (targetKey != null && !legacy.Stream.Key.Equals(targetKey.Value))
                Follow(legacy.Requested, legacy.Target, targetKey.Value);
        }

        // Pure
        private StreamKey? BestFollowCandidate(VideoStreamInfo current)
        {
            StreamKey? targetKey = PresentationBotVideoKey();

            // Screen share ranks below the presentation bot but above speaker cameras.
            targetKey ??= FirstScreenShareVideoKey();

            // try pick up another key if presentation bot and screen share are unavailable
            if (targetKey == null)
            {
                float delta = UnityEngine.Time.realtimeSinceStartup - current.SwitchedAtTime;

                // attempt to switch only if hold exceeds
                if (delta > MIN_SPEAKER_HOLD_SECONDS)
                {
                    foreach (string activeSpeaker in room.ActiveSpeakers)
                    {
                        if (activeSpeaker == current.FromIdentity)
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

        private string? ComposingBotIdentity(LivekitAddress requested)
        {
            if (requested.IsUserStream(out UserStream userStream))
                return userStream.Identity.IsPresentationBotIdentity() ? userStream.Identity : null;

            return PresentationBotIdentity();
        }

        private static bool IsPresentationVideo(TrackPublication track) =>
            track.Name == LiveKitMediaExtensions.PRESENTATION_VIDEO_TRACK_NAME;

        private bool RefreshPresentation(LivekitAddress requested)
        {
            string? bot = ComposingBotIdentity(requested);
            string? raw = bot == null ? null : room.Participants.RemoteParticipant(bot)?.Metadata;

            if (string.Equals(bot, presentationSource.bot, StringComparison.Ordinal) && string.Equals(raw, presentationSource.metadata, StringComparison.Ordinal))
                return false;

            presentationSource = (bot, raw);
            PresentationBotMetadata? metadata = PresentationBotMetadata.Parse(raw);

            if (bot != null && metadata?.slide is { } slide)
            {
                if (video.IsComposingVideo(out ComposingVideo composing))
                    composing.Show(bot, metadata, slide);
                else
                    StartComposing(requested, bot, metadata, slide);
            }
            else if (video.IsComposingVideo(out ComposingVideo composing))
                StopComposing(composing);

            if (metadata == null && !string.IsNullOrEmpty(raw) && !loggedBadMetadata)
            {
                loggedBadMetadata = true;
                ReportHub.LogWarning(ReportCategory.MEDIA_STREAM, "Presentation bot metadata is unparseable or out of bounds, showing the legacy track");
            }

            return true;
        }

        private void DropPresentation()
        {
            if (video.IsComposingVideo(out ComposingVideo composing))
                StopComposing(composing);

            presentationSource = default;
            pendingPresentationRefresh = true;
        }

        private void Defer(LivekitAddress requested, LivekitAddress target) =>
            SetVideo(LivekitVideoMode.FromDeferredVideo(new DeferredVideo(requested, target)));

        private void Follow(LivekitAddress requested, LivekitAddress target, StreamKey key) =>
            SetVideo(LivekitVideoMode.FromLegacyVideo(new LegacyVideo(requested, target, VideoStreamInfo.New(key, room.VideoStreams.ActiveStream(key)))));

        private void StartComposing(LivekitAddress requested, string bot, PresentationBotMetadata metadata, PresentationSlide slide) =>
            SetVideo(LivekitVideoMode.FromComposingVideo(new ComposingVideo(requested, slideCache.BeginComposing(), bot, metadata, slide)));

        private void StopComposing(ComposingVideo composing) =>
            Defer(composing.Requested, composing.Requested);

        private void SetVideo(LivekitVideoMode next)
        {
            if (video.IsComposingVideo(out ComposingVideo composing))
            {
                composing.SlideLease.Dispose();
                compositor.Release();
            }

            video = next;
        }

        private void ReleaseHeldVideoStreams()
        {
            if (video.IsLegacyVideo(out LegacyVideo legacy))
            {
                room.VideoStreams.Release(legacy.Stream.Key);
                Defer(legacy.Requested, legacy.Target);
            }
            else if (video.IsComposingVideo(out ComposingVideo composing))
            {
                ReleaseVideoStream(ref composing.PresentationVideo);
                ReleaseVideoStream(ref composing.PresenterCamera);
            }
        }

        private void EnsurePresentationStreams(ComposingVideo composing, bool rescan)
        {
            if (rescan || IsUnresolved(composing.PresentationVideo))
                composing.PresentationVideo = RebindVideoStream(composing.PresentationVideo, FindVideoTrack(composing.Bot, static track => IsPresentationVideo(track)));

            if (rescan || IsUnresolved(composing.PresenterCamera))
            {
                string? presenter = composing.Presentation.presenterIdentity;
                composing.PresenterCamera = RebindVideoStream(composing.PresenterCamera, presenter == null ? null : FindVideoTrack(presenter, static track => track.Source == TrackSource.SourceCamera));
            }
        }

        private static bool IsUnresolved(VideoStreamInfo? stream) =>
            stream is { IsLive: false };

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

        private VideoStreamInfo? RebindVideoStream(VideoStreamInfo? held, StreamKey? key)
        {
            if (!key.HasValue)
                return null;

            if (held is { IsLive: true } live && live.Key.Equals(key.Value))
                return held;

            return VideoStreamInfo.New(key.Value, room.VideoStreams.ActiveStream(key.Value));
        }

        private void ReleaseVideoStream(ref VideoStreamInfo? stream)
        {
            if (!stream.HasValue) return;

            room.VideoStreams.Release(stream.Value.Key);
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

        /// <summary>
        ///     Drops every held stream and stops. <see cref="Play" /> resumes the address the player was opened with.
        /// </summary>
        public void CloseCurrentStream()
        {
            // Doesn't need to dispose the stream, because it's responsibility of the owning room.
            DropPresentation();

            if (video.Requested is { } requested)
                Defer(requested, requested);

            playerState = PlayerState.Stopped;
            ReleaseAllAudioSources();
        }

        public Texture? LastTexture()
        {
            if (playerState is not PlayerState.Playing)
                return null;

            if (video.IsComposingVideo(out ComposingVideo composing))
            {
                if (composing.ComposedFrame != UnityEngine.Time.frameCount)
                {
                    composing.ComposedTexture = ComposePresentation(composing);
                    composing.ComposedFrame = UnityEngine.Time.frameCount;
                }

                return composing.ComposedTexture;
            }

            if (!video.IsLegacyVideo(out LegacyVideo legacy) || !legacy.Stream.IsLive)
                return null;

            return CameraOffPlaceholder(legacy.Stream) ?? legacy.Stream.VideoStream.Resource.Value.DecodeLastFrame();
        }

        private Texture? ComposePresentation(ComposingVideo composing)
        {
            PresentationBotMetadata metadata = composing.Presentation;
            PresentationSlide slide = composing.Slide;

            if (slide.url == null)
                return null;

            DCL.Utility.Types.EnumResult<Texture2D, SlideState> cachedSlide = slideCache.GetOrRequest(slide.url, composing.Bot);

            if (cachedSlide.Success)
            {
                composing.ShownSlide = cachedSlide.Value;
                composing.ShownSlideUrl = slide.url;
            }
            else if (composing.ShownSlideUrl != null)
                slideCache.KeepAlive(composing.ShownSlideUrl);

            Texture slideTexture = composing.ShownSlide != null ? composing.ShownSlide : Texture2D.blackTexture;
            bool showRect = metadata.TryVideoRect(out Vector4 videoRect);
            Texture? videoFrame = showRect ? DecodeLastFrame(composing.PresentationVideo) : null;

            Vector4 cameraRect = metadata.CameraRect(slide.width, slide.height);
            Texture? camera = cameraRect.z > 0f && IsPresenterCameraLive(composing) ? DecodeLastFrame(composing.PresenterCamera) : null;

            return compositor.Compose(slide.width, slide.height, slideTexture, showRect, videoRect, videoFrame, camera, cameraRect);
        }

        private static Texture2D? DecodeLastFrame(VideoStreamInfo? stream) =>
            stream is { IsLive: true } live ? live.VideoStream.Resource.Value.DecodeLastFrame() : null;

        private bool IsPresenterCameraLive(ComposingVideo composing) =>
            composing.PresenterCamera.HasValue && PublicationOf(composing.PresenterCamera.Value) is { Muted: false };

        private Texture? CameraOffPlaceholder(VideoStreamInfo videoInfo) =>
            placeholderSource != null && IsCameraVideoMuted(videoInfo)
                ? placeholderSource.TextureFor(StreamerName(videoInfo))
                : null;

        // Screen-shares are not cameras, so they keep their live frame and never show the placeholder.
        private bool IsCameraVideoMuted(VideoStreamInfo videoInfo) =>
            PublicationOf(videoInfo) is { Muted: true } track && track.Source != TrackSource.SourceScreenshare;

        private TrackPublication? PublicationOf(VideoStreamInfo videoInfo)
        {
            var participant = room.Participants.RemoteParticipant(videoInfo.FromIdentity);
            return participant != null && participant.Tracks.TryGetValue(videoInfo.Key.sid, out TrackPublication track) ? track : null;
        }

        private string? StreamerName(VideoStreamInfo videoInfo)
        {
            var participant = room.Participants.RemoteParticipant(videoInfo.FromIdentity);
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
    }
}
