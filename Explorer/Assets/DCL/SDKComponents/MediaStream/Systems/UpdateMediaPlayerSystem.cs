using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Audio;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Utilities.Extensions;
using DCL.WebRequests;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle;
using ECS.LifeCycle.Components;
using ECS.Unity.Textures.Components;
using ECS.Unity.Transforms.Components;
using SceneRunner.Scene;
using System;
using UnityEngine;
using UnityEngine.Profiling;
using Utility;

namespace DCL.SDKComponents.MediaStream
{
    [UpdateInGroup(typeof(SyncedPresentationSystemGroup))]
    [LogCategory(ReportCategory.MEDIA_STREAM)]
    [ThrottlingEnabled]
    public partial class UpdateMediaPlayerSystem : BaseUnityLoopSystem, ISceneIsCurrentListener
    {
        private readonly IWebRequestController webRequestController;
        private readonly ISceneData sceneData;
        private readonly ISceneStateProvider sceneStateProvider;
        private readonly IPerformanceBudget frameTimeBudget;
        private readonly MediaFactory mediaFactory;
        private readonly VolumeBus volumeBus;

        private readonly float audioFadeSpeed;

        public UpdateMediaPlayerSystem(
            World world,
            IWebRequestController webRequestController,
            ISceneData sceneData,
            ISceneStateProvider sceneStateProvider,
            IPerformanceBudget frameTimeBudget,
            MediaFactory mediaFactory,
            float audioFadeSpeed
        ) : base(world)
        {
            this.webRequestController = webRequestController;
            this.sceneData = sceneData;
            this.sceneStateProvider = sceneStateProvider;
            this.frameTimeBudget = frameTimeBudget;
            this.mediaFactory = mediaFactory;
            this.audioFadeSpeed = audioFadeSpeed;
        }

        protected override void Update(float t)
        {
            UpdateMediaPlayerPositionQuery(World);
            UpdateAudioStreamQuery(World, t);
            UpdateVideoStreamQuery(World, t);
            OpenMediaAutomaticallyQuery(World);

            UpdateVideoTextureQuery(World);
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

            if (component.State != VideoState.VsError)
            {
                float targetVolume = (sdkComponent.HasVolume ? sdkComponent.Volume : MediaPlayerComponent.DEFAULT_VOLUME) * mediaFactory.worldVolumePercentage * mediaFactory.masterVolumePercentage;

                if (!sceneStateProvider.IsCurrent)
                    targetVolume = 0f;

                component.MediaPlayer.CrossfadeVolume(targetVolume, dt * audioFadeSpeed);
            }

            var address = MediaAddress.New(sdkComponent.Url!);
            if (RequiresURLChange(entity, ref component, address, sdkComponent)) return;

            HandleComponentChange(ref component, sdkComponent, address, sdkComponent.HasPlaying, sdkComponent.Playing);
            ConsumePromise(ref component, sdkComponent.HasPlaying && sdkComponent.Playing);
        }

        [Query]
        private void UpdateVideoStream(in Entity entity, ref MediaPlayerComponent component, PBVideoPlayer sdkComponent, [Data] float dt)
        {
            if (!frameTimeBudget.TrySpendBudget()) return;

            if (component.State != VideoState.VsError)
            {
                float targetVolume = (sdkComponent.HasVolume ? sdkComponent.Volume : MediaPlayerComponent.DEFAULT_VOLUME) * mediaFactory.worldVolumePercentage * mediaFactory.masterVolumePercentage;

                if (!sceneStateProvider.IsCurrent)
                    targetVolume = 0f;

                component.MediaPlayer.CrossfadeVolume(targetVolume, dt * audioFadeSpeed);
            }

            var address = MediaAddress.New(sdkComponent.Src!);
            if (RequiresURLChange(entity, ref component, address, sdkComponent)) return;

            HandleComponentChange(ref component, sdkComponent, address, sdkComponent.HasPlaying, sdkComponent.Playing, sdkComponent, static (mediaPlayer, sdk) => mediaPlayer.UpdatePlaybackProperties(sdk));
            ConsumePromise(ref component, false, sdkComponent, static (mediaPlayer, sdk) => mediaPlayer.SetPlaybackProperties(sdk));
        }

        /// <summary>
        ///     If there is no SDK component which controls the playback state, the video is looped and started automatically
        /// </summary>
        /// <param name="mediaPlayer"></param>
        [Query]
        [None(typeof(PBVideoPlayer), typeof(PBAudioStream), typeof(DeleteEntityIntention))]
        private void OpenMediaAutomatically(ref MediaPlayerComponent mediaPlayer)
        {
            if (!frameTimeBudget.TrySpendBudget()) return;

            if (ConsumePromise(ref mediaPlayer, true))
                mediaPlayer.MediaPlayer.SetLooping(true);
        }

        private bool RequiresURLChange(in Entity entity, ref MediaPlayerComponent component, MediaAddress address, IDirtyMarker sdkComponent)
        {
            if (sdkComponent.IsNotDirty())
                return false;

            if (component.MediaAddress.IsUrlMediaAddress(out var urlMediaAddress) && address.IsUrlMediaAddress(out var other))
            {
                string selfUrl = urlMediaAddress!.Value.Url;
                string otherUrl = other!.Value.Url;

                if (selfUrl != otherUrl
                    && (!sceneData.TryGetMediaUrl(otherUrl, out var localMediaUrl) || selfUrl != localMediaUrl))
                    return PerformRemove(World, ref component, sdkComponent, entity);
            }
            else if (component.MediaAddress != address)
                return PerformRemove(World, ref component, sdkComponent, entity);

            return false;

            static bool PerformRemove(World world, ref MediaPlayerComponent component, IDirtyMarker sdkComponent, Entity entity)
            {
                component.Dispose();
                sdkComponent.IsDirty = false;
                world.Remove<MediaPlayerComponent>(entity);
                return true;
            }
        }

        // This query for all media players regardless of their origin
        [Query]
        private void UpdateVideoTexture(ref MediaPlayerComponent playerComponent, ref VideoTextureConsumer assignedTexture)
        {
            playerComponent.MediaPlayer.EnsurePlaying();

            if (!playerComponent.IsPlaying
                || playerComponent.State == VideoState.VsError
                || !playerComponent.MediaPlayer.MediaOpened
               )
                return;

            // Video is already playing in the background, and CopyTexture is a GPU operation,
            // so it does not make sense to budget by CPU as it can lead to much worse UX

            Texture? avText = playerComponent.MediaPlayer.LastTexture();
            if (avText == null) return;

            if (!assignedTexture.Texture.HasEqualResolution(to: avText))
                assignedTexture.Resize(avText.width, avText.height);

            if (playerComponent.MediaPlayer.GetTexureScale.Equals(new Vector2(1, -1)))
                Graphics.Blit(avText, assignedTexture.Texture, new Vector2(1, -1), new Vector2(0, 1));
            else
                Graphics.CopyTexture(avText, assignedTexture.Texture);
        }

        private void HandleComponentChange(
            ref MediaPlayerComponent component,
            IDirtyMarker sdkComponent,
            MediaAddress mediaAddress,
            bool hasPlaying,
            bool isPlaying,
            PBVideoPlayer? sdkVideoComponent = null,
            Action<MultiMediaPlayer, PBVideoPlayer>? onPlaybackUpdate = null
        )
        {
            if (!sdkComponent.IsDirty) return;

            bool ShouldUpdateSource(in MediaPlayerComponent component) =>
                component.MediaAddress.Match(
                    (sceneData, mediaAddress),
                    onUrlMediaAddress: static (ctx, componentAddress) =>
                    {
                        string mediaAddressUrl = ctx.mediaAddress.IsUrlMediaAddress(out var otherUrl) ? otherUrl!.Value.Url : "";
                        return !ctx.sceneData.TryGetMediaUrl(mediaAddressUrl, out URLAddress localMediaUrl) || componentAddress.Url != localMediaUrl;
                    },
                    onLivekitAddress: static (_, _) => true
                );

            if (component.MediaAddress != mediaAddress && ShouldUpdateSource(in component))
            {
                component.MediaPlayer.CloseCurrentStream();

                UpdateStreamUrl(ref component, mediaAddress);

                if (component.State != VideoState.VsError)
                {
                    component.Cts = component.Cts.SafeRestart();
                    component.OpenMediaPromise.UrlReachabilityResolveAsync(webRequestController, component.MediaAddress, GetReportData(), component.Cts.Token).Forget();
                }
            }
            else if (component.State != VideoState.VsError)
            {
                component.MediaPlayer.UpdatePlayback(hasPlaying, isPlaying);

                if (sdkVideoComponent != null)
                    onPlaybackUpdate?.Invoke(component.MediaPlayer, sdkVideoComponent);
            }

            sdkComponent.IsDirty = false;
        }

        private static bool ConsumePromise(ref MediaPlayerComponent component, bool autoPlay, PBVideoPlayer? sdkVideoComponent = null, Action<MultiMediaPlayer, PBVideoPlayer>? onOpened = null)
        {
            if (!component.OpenMediaPromise.IsResolved) return false;

            if (component.OpenMediaPromise.IsReachableConsume(component.MediaAddress))
            {
                Profiler.BeginSample(component.MediaPlayer.HasControl
                    ? "MediaPlayer.OpenMedia"
                    : "MediaPlayer.InitialiseAndOpenMedia");

                try { component.MediaPlayer.OpenMedia(component.MediaAddress, component.IsFromContentServer, autoPlay); }
                finally { Profiler.EndSample(); }

                if (sdkVideoComponent != null)
                    onOpened?.Invoke(component.MediaPlayer, sdkVideoComponent);

                return true;
            }

            component.SetState(component.MediaAddress.IsEmpty ? VideoState.VsNone : VideoState.VsError);
            Profiler.BeginSample("MediaPlayer.CloseCurrentStream");

            try { component.MediaPlayer.CloseCurrentStream(); }
            finally { Profiler.EndSample(); }

            return false;
        }

        private void UpdateStreamUrl(ref MediaPlayerComponent component, MediaAddress mediaAddress)
        {
            if (component.MediaAddress.IsLivekitAddress(out _))
            {
                component.MediaAddress = mediaAddress;
                return;
            }

            mediaAddress.IsUrlMediaAddress(out var urlMediaAddress);
            string url = urlMediaAddress!.Value.Url;

            bool isValidStreamUrl = url.IsValidUrl();
            bool isValidLocalPath = false;

            if (!isValidStreamUrl)
            {
                isValidLocalPath = sceneData.TryGetMediaUrl(url, out URLAddress mediaUrl);

                if (isValidLocalPath)
                    mediaAddress = MediaAddress.New(mediaUrl.Value);
            }

            component.MediaAddress = mediaAddress;
            component.SetState(isValidStreamUrl || isValidLocalPath || mediaAddress.IsEmpty ? VideoState.VsNone : VideoState.VsError);
        }

        public void OnSceneIsCurrentChanged(bool enteredScene)
        {
            ToggleCurrentStreamsStateQuery(World, enteredScene);
        }

        [Query]
        private void ToggleCurrentStreamsState(Entity entity, MediaPlayerComponent mediaPlayerComponent, [Data] bool enteredScene)
        {
            if (mediaPlayerComponent.MediaPlayer.IsLivekitPlayer(out LivekitPlayer livekitPlayer) && !enteredScene)
            {
                //Streams rely on livekit room being active; which can only be in we are on the same scene. Next time we enter the scene, it will be recreate by
                //the regular CreateMediaPlayerSystem
                mediaPlayerComponent.Dispose();
                World.Remove<MediaPlayerComponent>(entity);
            }

        }
    }
}
