using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Optimization.PerformanceBudgeting;
using DCL.SDKComponents.MediaStream.Settings;
using DCL.Utilities.Extensions;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle;
using ECS.LifeCycle.Components;
using ECS.Unity.Textures.Components;
using ECS.Unity.Transforms.Components;
using SceneRunner.Scene;
using UnityEngine;
using UnityEngine.Profiling;

namespace DCL.SDKComponents.MediaStream
{
    [UpdateInGroup(typeof(SyncedPresentationSystemGroup))]
    [LogCategory(ReportCategory.MEDIA_STREAM)]
    [ThrottlingEnabled]
    public partial class UpdateMediaPlayerSystem : BaseUnityLoopSystem, ISceneIsCurrentListener
    {
        private readonly ISceneData sceneData;
        private readonly ISceneStateProvider sceneStateProvider;
        private readonly IPerformanceBudget frameTimeBudget;
        private readonly MediaFactory mediaFactory;
        private const int MAX_LIVEKIT_VIDEO_WIDTH = 2048;
        private const int MAX_LIVEKIT_VIDEO_HEIGHT = 2048;

        private readonly float audioFadeSpeed;
        private readonly Material flipMaterial;
        private readonly VideoPrioritizationSettings videoPrioritizationSettings;

        private static float lastOpenMediaTime;
        private const float MIN_OPEN_MEDIA_INTERVAL_SECONDS = 0.5f;

        // Generic-URL retry policy: 5 attempts with exponential backoff (2s, 4s, 8s, 16s, 32s).
        // Covers cold-network scenarios on Windows where the initial reachability probe fails
        // before DNS/SSL is warm. After MAX_RETRY_ATTEMPTS the component stays in VsError so
        // genuinely-broken URLs don't burn CPU forever.
        private const int MAX_RETRY_ATTEMPTS = 5;

        public UpdateMediaPlayerSystem(
            World world,
            ISceneData sceneData,
            ISceneStateProvider sceneStateProvider,
            IPerformanceBudget frameTimeBudget,
            MediaFactory mediaFactory,
            float audioFadeSpeed,
            Material flipMaterial,
            VideoPrioritizationSettings videoPrioritizationSettings
        ) : base(world)
        {
            this.sceneData = sceneData;
            this.sceneStateProvider = sceneStateProvider;
            this.frameTimeBudget = frameTimeBudget;
            this.mediaFactory = mediaFactory;
            this.audioFadeSpeed = audioFadeSpeed;
            this.flipMaterial = flipMaterial;
            this.videoPrioritizationSettings = videoPrioritizationSettings;
        }

        protected override void Update(float t)
        {
            RemoveDeadMediaPlayersQuery(World);
            UpdateMediaPlayerPositionQuery(World);
            UpdateAudioStreamQuery(World, t);
            UpdateVideoStreamQuery(World, t);
            UpdateCustomStreamQuery(World, t);
            UpdateVideoTextureQuery(World);
        }

        public void OnSceneIsCurrentChanged(bool enteredScene)
        {
            ToggleCurrentStreamsStateQuery(World, enteredScene);
        }

        // Drops players whose AVPro object was destroyed under them (pool eviction / scene teardown). Runs first so no
        // later query dereferences the stale ref; recreated from the SDK component next frame.
        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void RemoveDeadMediaPlayers(Entity entity, ref MediaPlayerComponent mediaPlayer)
        {
            if (mediaPlayer.MediaPlayer.IsValid) return;

            RemoveAndForceReInitialization(ref mediaPlayer, entity);
        }

        [Query]
        private void UpdateMediaPlayerPosition(ref MediaPlayerComponent mediaPlayer, ref TransformComponent transformComponent)
        {
            mediaPlayer.MediaPlayer.PlaceAt(transformComponent.Transform.position);
        }

        [Query]
        private void UpdateAudioStream(in Entity entity, ref MediaPlayerComponent component, PBAudioStream sdkComponent, [Data] float dt)
        {
            if (!frameTimeBudget.TrySpendBudget()) return;

            var address = MediaAddress.New(sdkComponent.Url!);

            if (TryReInitializeOnSourceChange(entity, ref component, address)) return;
            if (TryReInitializeOnExpiredYouTubeUrl(entity, ref component)) return;
            if (TryReInitializeOnFailedMedia(entity, ref component)) return;

            component.UpdateState();

            FadeVolume(ref component, sdkComponent.HasVolume ? sdkComponent.Volume : MediaPlayerComponent.DEFAULT_VOLUME, dt);

            if (component.State != VideoState.VsError)
            {
                if (sdkComponent.HasPlaying && sdkComponent.Playing != component.IsPlaying)
                    component.MediaPlayer.UpdatePlayback(sdkComponent.HasPlaying, sdkComponent.Playing);

                if (component.IsPlaying)
                    if (component.MediaPlayer.IsLivekitPlayer(out LivekitPlayer? livekitPlayer))
                        livekitPlayer?.EnsureAudioIsPlaying();

                bool hasSpatialEnabledChanged = sdkComponent.HasSpatial && sdkComponent.Spatial != component.IsSpatial;

                bool hasSpatialMaxDistanceChanged = (sdkComponent.HasSpatialMaxDistance && !Mathf.Approximately(sdkComponent.SpatialMaxDistance, component.SpatialMaxDistance))
                                                    // In case the sdk component has no spatial max distance, then it should reset to its default value
                                                    || (!sdkComponent.HasSpatialMaxDistance && !Mathf.Approximately(component.SpatialMaxDistance, MediaPlayerComponent.DEFAULT_SPATIAL_MAX_DISTANCE));

                bool hasSpatialMinDistanceChanged = (sdkComponent.HasSpatialMinDistance && !Mathf.Approximately(sdkComponent.SpatialMinDistance, component.SpatialMinDistance))
                                                    // In case the sdk component has no spatial min distance, then it should reset to its default value
                                                    || (!sdkComponent.HasSpatialMinDistance && !Mathf.Approximately(component.SpatialMinDistance, MediaPlayerComponent.DEFAULT_SPATIAL_MIN_DISTANCE));

                if (hasSpatialEnabledChanged || hasSpatialMaxDistanceChanged || hasSpatialMinDistanceChanged)
                    component.UpdateSpatialAudio(sdkComponent.Spatial,
                        sdkComponent.HasSpatialMinDistance ? sdkComponent.SpatialMinDistance : null,
                        sdkComponent.HasSpatialMaxDistance ? sdkComponent.SpatialMaxDistance : null);
            }

            if (!videoPrioritizationSettings.PlayCurrentSceneStreamOnly || sceneStateProvider.IsCurrent)
            {
                ConsumePromise(ref component, sdkComponent.HasPlaying && sdkComponent.Playing);
                component.UpdateState();

                // Keep last: may trigger an archetype move that invalidates component's ref.
                UpdateRetryState(entity, ref component);
            }
            else
                component.UpdateState();
        }

        [Query]
        private void UpdateVideoStream(in Entity entity, ref MediaPlayerComponent component, PBVideoPlayer sdkComponent, [Data] float dt)
        {
            var address = MediaAddress.New(sdkComponent.Src!);

            if (TryReInitializeOnSourceChange(entity, ref component, address)) return;
            if (TryReInitializeOnExpiredYouTubeUrl(entity, ref component)) return;
            if (TryReInitializeOnFailedMedia(entity, ref component)) return;

            if (component.MediaPlayer.WaitingForProperties) return;
            if (!frameTimeBudget.TrySpendBudget()) return;

            component.UpdateState();

            FadeVolume(ref component, sdkComponent.HasVolume ? sdkComponent.Volume : MediaPlayerComponent.DEFAULT_VOLUME, dt);

            if (component.State != VideoState.VsError)
            {
                if (sdkComponent.HasPlaying && sdkComponent.Playing != component.IsPlaying)
                {
                    component.MediaPlayer.UpdatePlayback(sdkComponent.HasPlaying, sdkComponent.Playing);
                    component.MediaPlayer.UpdatePlaybackProperties(sdkComponent);
                }

                if (component.IsPlaying)

                    // Covers cases like leaving and re-entering the scene
                    // or the stream not being available for some time, like OBS not started while the stream is active
                    if (component.MediaPlayer.IsLivekitPlayer(out LivekitPlayer? livekitPlayer))
                        livekitPlayer?.EnsureVideoIsPlaying();

                bool hasSpatialEnabledChanged = sdkComponent.HasSpatial && sdkComponent.Spatial != component.IsSpatial;

                bool hasSpatialMaxDistanceChanged = (sdkComponent.HasSpatialMaxDistance && !Mathf.Approximately(sdkComponent.SpatialMaxDistance, component.SpatialMaxDistance))
                                                    // In case the sdk component has no spatial max distance, then it should reset to its default value
                                                    || (!sdkComponent.HasSpatialMaxDistance && !Mathf.Approximately(component.SpatialMaxDistance, MediaPlayerComponent.DEFAULT_SPATIAL_MAX_DISTANCE));

                bool hasSpatialMinDistanceChanged = (sdkComponent.HasSpatialMinDistance && !Mathf.Approximately(sdkComponent.SpatialMinDistance, component.SpatialMinDistance))
                                                    // In case the sdk component has no spatial min distance, then it should reset to its default value
                                                    || (!sdkComponent.HasSpatialMinDistance && !Mathf.Approximately(component.SpatialMinDistance, MediaPlayerComponent.DEFAULT_SPATIAL_MIN_DISTANCE));

                if (hasSpatialEnabledChanged || hasSpatialMaxDistanceChanged || hasSpatialMinDistanceChanged)
                    component.UpdateSpatialAudio(sdkComponent.Spatial,
                        sdkComponent.HasSpatialMinDistance ? sdkComponent.SpatialMinDistance : null,
                        sdkComponent.HasSpatialMaxDistance ? sdkComponent.SpatialMaxDistance : null);
            }

            if (!videoPrioritizationSettings.PlayCurrentSceneStreamOnly || sceneStateProvider.IsCurrent)
            {
                if (ConsumePromise(ref component, false))
                    component.MediaPlayer.SetPlaybackPropertiesAsync(sdkComponent, component.IsLiveStream).Forget();

                component.UpdateState();

                // Keep last: may trigger an archetype move that invalidates component's ref.
                UpdateRetryState(entity, ref component);
            }
            else
                component.UpdateState();
        }

        /// <summary>
        ///     If there is no SDK component which controls the playback state, the video is looped and started automatically
        /// </summary>
        [Query]
        private void UpdateCustomStream(ref MediaPlayerComponent mediaPlayer, CustomMediaStream customMediaStream, [Data] float dt)
        {
            if (!frameTimeBudget.TrySpendBudget()) return;

            FadeVolume(ref mediaPlayer, customMediaStream.Volume, dt);

            if ((!videoPrioritizationSettings.PlayCurrentSceneStreamOnly || sceneStateProvider.IsCurrent) && ConsumePromise(ref mediaPlayer, true))
                mediaPlayer.MediaPlayer.SetPlaybackProperties(customMediaStream);
        }

        // This query for all media players regardless of their origin
        [Query]
        private void UpdateVideoTexture(ref MediaPlayerComponent playerComponent, ref VideoTextureConsumer assignedTexture)
        {
            if (!playerComponent.IsPlaying)
            {
                if (playerComponent.State is VideoState.VsError or VideoState.VsNone)
                {
                    RenderBlackTexture(ref assignedTexture);
                    return;
                }

                if (playerComponent.State == VideoState.VsPaused)
                    return;
            }

            if (playerComponent.MediaPlayer.IsLivekitPlayer(out LivekitPlayer livekitPlayer))
            {
                if (!livekitPlayer.IsVideoOpened)
                {
                    RenderBlackTexture(ref assignedTexture);
                    return;
                }
            }

            // Video is already playing in the background, and CopyTexture is a GPU operation,
            // so it does not make sense to budget by CPU as it can lead to much worse UX

            Texture? avText = playerComponent.MediaPlayer.LastTexture();
            if (avText == null) return;

            int targetWidth = avText.width;
            int targetHeight = avText.height;

            // Cap LiveKit video resolution to prevent GPU stalls from 4K+ streams.
            if (livekitPlayer != null && (avText.width > MAX_LIVEKIT_VIDEO_WIDTH || avText.height > MAX_LIVEKIT_VIDEO_HEIGHT))
            {
                float scale = Mathf.Min((float)MAX_LIVEKIT_VIDEO_WIDTH / avText.width, (float)MAX_LIVEKIT_VIDEO_HEIGHT / avText.height);
                targetWidth = Mathf.RoundToInt(avText.width * scale);
                targetHeight = Mathf.RoundToInt(avText.height * scale);
            }

            if (assignedTexture.Texture.width != targetWidth || assignedTexture.Texture.height != targetHeight)
                assignedTexture.Resize(targetWidth, targetHeight);

            bool needsFlip = playerComponent.MediaPlayer.GetTexureScale.Equals(new Vector2(1, -1));
            bool dimensionsCapped = targetWidth != avText.width || targetHeight != avText.height;

            if (needsFlip)
            {
                //Regular blit or blit with material are called based on the source color space, we blit with material only
                //in case we are in linear and need to convert the colorspace as well as target assigned texture is always in gamma
                if (avText.isDataSRGB)
                    Graphics.Blit(avText, assignedTexture.Texture, new Vector2(1, -1), new Vector2(0, 1));
                else
                    Graphics.Blit(avText, assignedTexture.Texture, flipMaterial);
            }
            else if (dimensionsCapped)
            {
                Graphics.Blit(avText, assignedTexture.Texture);
            }
            else
            {
                Graphics.CopyTexture(avText, assignedTexture.Texture);
            }

            return;

            void RenderBlackTexture(ref VideoTextureConsumer assignedTexture) =>
                Graphics.Blit(Texture2D.blackTexture, assignedTexture.Texture);
        }

        [Query]
        private void ToggleCurrentStreamsState(Entity entity, MediaPlayerComponent mediaPlayerComponent, [Data] bool enteredScene)
        {
            if (enteredScene) return;

            bool isLivekit = mediaPlayerComponent.MediaPlayer.IsLivekitPlayer(out _);

            // Livekit streams rely on the livekit room being active for the current scene only.
            // Non-livekit streams are also stopped when PlayCurrentSceneStreamOnly is on so they can be
            // recreated fresh by CreateMediaPlayerSystem when the player re-enters the scene.
            if (isLivekit || videoPrioritizationSettings.PlayCurrentSceneStreamOnly)
            {
                mediaPlayerComponent.Dispose();
                World.Remove<MediaPlayerComponent>(entity);
            }
        }

        private bool TryReInitializeOnExpiredYouTubeUrl(in Entity entity, ref MediaPlayerComponent component)
        {
            if (component.State != VideoState.VsError) return false;
            if (component.ResolvedUrlExpiresAt <= 0f) return false;
            if (UnityEngine.Time.realtimeSinceStartup <= component.ResolvedUrlExpiresAt) return false;

            ReportHub.Log(ReportCategory.MEDIA_STREAM, "[YouTubeResolver] Resolved URL expired, triggering re-resolution");
            RemoveAndForceReInitialization(ref component, entity);
            return true;
        }

        private bool TryReInitializeOnFailedMedia(in Entity entity, ref MediaPlayerComponent component)
        {
            if (component.State != VideoState.VsError) return false;
            if (!World.Has<MediaPlayerRetryState>(entity)) return false;

            MediaPlayerRetryState retry = World.Get<MediaPlayerRetryState>(entity);

            if (retry.Attempts > MAX_RETRY_ATTEMPTS) return false;
            if (UnityEngine.Time.realtimeSinceStartup < retry.NextRetryAt) return false;

            ReportHub.Log(ReportCategory.MEDIA_STREAM,
                $"[MediaRetry] Triggering retry {retry.Attempts}/{MAX_RETRY_ATTEMPTS} for {component.MediaAddress}");

            // Tear down only the media player — retry state must survive the recreation, otherwise
            // the backoff curve resets every cycle and we'd retry forever.
            RemoveAndForceReInitialization(ref component, entity);
            return true;
        }

        private void UpdateRetryState(in Entity entity, ref MediaPlayerComponent component)
        {
            // Success path: clear any stale retry bookkeeping the moment the promise resolves cleanly.
            if (!component.HasFailed)
            {
                if (component.OpenMediaPromise is { IsConsumed: true } && World.Has<MediaPlayerRetryState>(entity))
                    World.Remove<MediaPlayerRetryState>(entity);
                return;
            }

            if (component.OpenMediaPromise is not { IsConsumed: true }) return;

            float now = UnityEngine.Time.realtimeSinceStartup;
            int attempts = 1;
            bool hasState = World.Has<MediaPlayerRetryState>(entity);

            if (hasState)
            {
                MediaPlayerRetryState existing = World.Get<MediaPlayerRetryState>(entity);
                // Still waiting for the already-scheduled retry; nothing to do.
                if (existing.NextRetryAt > now) return;
                attempts = existing.Attempts + 1;
            }

            if (attempts > MAX_RETRY_ATTEMPTS)
            {
                // Otherwise the elapsed state keeps passing TryReInitializeOnFailedMedia's guard and retries fire every frame forever.
                World.Set(entity, new MediaPlayerRetryState { Attempts = MAX_RETRY_ATTEMPTS + 1, NextRetryAt = float.MaxValue });
                return;
            }

            float delay = 2f * (1 << (attempts - 1)); // 2, 4, 8, 16, 32

            var newState = new MediaPlayerRetryState
            {
                Attempts = attempts,
                NextRetryAt = now + delay,
            };

            // World.Add below invalidates component's ref, so read the address first.
            MediaAddress mediaAddressForLog = component.MediaAddress;

            if (hasState)
                World.Set(entity, newState);
            else
                World.Add(entity, newState);

            ReportHub.LogWarning(ReportCategory.MEDIA_STREAM,
                $"[MediaRetry] {mediaAddressForLog} unreachable; scheduled retry {attempts}/{MAX_RETRY_ATTEMPTS} in {delay:F0}s");
        }

        private bool TryReInitializeOnSourceChange(in Entity entity, ref MediaPlayerComponent component, MediaAddress address)
        {
            if (component.MediaAddress.IsUrlMediaAddress(out var urlMediaAddress) && address.IsUrlMediaAddress(out var other))
            {
                string selfUrl = urlMediaAddress.Url;
                string otherUrl = other!.Url;

                if (selfUrl == otherUrl
                    || (sceneData.TryGetMediaUrl(otherUrl, out var localMediaUrl) && selfUrl == localMediaUrl)) return false;

                // Dispose while the ref is valid; clearing retry state is a structural change that would invalidate it.
                RemoveAndForceReInitialization(ref component, entity);
                ClearRetryStateOnSourceChange(entity);
                return true;
            }

            if (component.MediaAddress == address) return false;

            RemoveAndForceReInitialization(ref component, entity);
            ClearRetryStateOnSourceChange(entity);
            return true;
        }

        private void ClearRetryStateOnSourceChange(in Entity entity)
        {
            // A genuine source change starts a fresh retry budget; don't carry over backoff from the previous URL.
            if (World.Has<MediaPlayerRetryState>(entity))
                World.Remove<MediaPlayerRetryState>(entity);
        }

        private void RemoveAndForceReInitialization(ref MediaPlayerComponent component, in Entity entity)
        {
            component.Dispose();
            World.Remove<MediaPlayerComponent>(entity);
        }

        private static bool ConsumePromise(ref MediaPlayerComponent component, bool autoPlay)
        {
            if (!component.OpenMediaPromise.IsResolved) return false;
            if (component.OpenMediaPromise.IsConsumed) return false;

            // On macOS, enforce minimum gap between HLS stream opens to prevent
            // AVFoundation crashes when opening multiple streams simultaneously.
            // If too soon since last open, defer the opening.
            // On windows deferr to avoid frame drops when opening multiple streams simultaneously, as the media player can consume a lot of resources while opening a stream.
            float currentTime = UnityEngine.Time.realtimeSinceStartup;
            float timeSinceLastOpen = currentTime - lastOpenMediaTime;

            if (timeSinceLastOpen < MIN_OPEN_MEDIA_INTERVAL_SECONDS)
                return false;

            if (component.OpenMediaPromise.IsReachableConsume(component.MediaAddress))
            {

                // Transfer YouTube resolution metadata from the promise to the component
                component.ResolvedUrlExpiresAt = component.OpenMediaPromise.resolvedUrlExpiresAt;
                component.IsLiveStream = component.OpenMediaPromise.isLiveStream;

                // Use the resolved media address (which may be a direct URL after YouTube resolution)
                MediaAddress resolvedAddress = component.OpenMediaPromise.mediaAddress;

                lastOpenMediaTime = currentTime;

                ReportHub.Log(ReportCategory.MEDIA_STREAM,
                    $"[OpenMedia] Opening media: {component.MediaAddress} → {resolvedAddress}, Time: {currentTime:F3}, TimeSinceLastOpen: {timeSinceLastOpen:F3}s");

                Profiler.BeginSample(component.MediaPlayer.HasControl
                    ? "MediaPlayer.OpenMedia"
                    : "MediaPlayer.InitialiseAndOpenMedia");

                try { component.MediaPlayer.OpenMedia(resolvedAddress, component.IsFromContentServer, autoPlay); }
                finally { Profiler.EndSample(); }

                return true;
            }

            component.MarkAsFailed(!component.MediaAddress.IsEmpty);

            Profiler.BeginSample("MediaPlayer.CloseCurrentStream");

            try { component.MediaPlayer.CloseCurrentStream(); }
            finally { Profiler.EndSample(); }

            return false;
        }

        private void FadeVolume(ref MediaPlayerComponent component, float volume, float dt)
        {
            if (component.State != VideoState.VsError)
            {
                float targetVolume = volume * mediaFactory.worldVolumePercentage * mediaFactory.masterVolumePercentage;

                if (!sceneStateProvider.IsCurrent)
                    targetVolume = 0f;

                component.MediaPlayer.CrossfadeVolume(targetVolume, dt * audioFadeSpeed);
            }
        }
    }
}
