using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Settings;
using DCL.Utilities.Extensions;
using DCL.WebRequests;
using ECS.Abstract;
using ECS.Groups;
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
    public partial class UpdateMediaPlayerSystem : BaseUnityLoopSystem
    {
        private readonly IWebRequestController webRequestController;
        private readonly ISceneData sceneData;
        private readonly ISceneStateProvider sceneStateProvider;
        private readonly IPerformanceBudget frameTimeBudget;
#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
        private readonly WorldVolumeMacBus worldVolumeMacBus;
#endif

        private readonly float audioFadeSpeed;

        private float worldVolumePercentage = 1f;
        private float masterVolumePercentage = 1f;

        public UpdateMediaPlayerSystem(
            World world,
            IWebRequestController webRequestController,
            ISceneData sceneData,
            ISceneStateProvider sceneStateProvider,
            IPerformanceBudget frameTimeBudget,
            WorldVolumeMacBus worldVolumeMacBus,
            float audioFadeSpeed
        ) : base(world)
        {
            this.webRequestController = webRequestController;
            this.sceneData = sceneData;
            this.sceneStateProvider = sceneStateProvider;
            this.frameTimeBudget = frameTimeBudget;
            this.audioFadeSpeed = audioFadeSpeed;

            //This following part is a workaround applied for the MacOS platform, the reason
            //is related to the video and audio streams, the MacOS environment does not support
            //the volume control for the video and audio streams, as it doesn’t allow to route audio
            //from HLS through to Unity. This is a limitation of Apple’s AVFoundation framework
            //Similar issue reported here https://github.com/RenderHeads/UnityPlugin-AVProVideo/issues/1086
#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
            this.worldVolumeMacBus = worldVolumeMacBus;
            this.worldVolumeMacBus.OnWorldVolumeChanged += OnWorldVolumeChanged;
            this.worldVolumeMacBus.OnMasterVolumeChanged += OnMasterVolumeChanged;
            masterVolumePercentage = worldVolumeMacBus.GetMasterVolume();
            worldVolumePercentage = worldVolumeMacBus.GetWorldVolume();
#endif
        }

        private void OnWorldVolumeChanged(float volume)
        {
            worldVolumePercentage = volume;
        }

        private void OnMasterVolumeChanged(float volume)
        {
            masterVolumePercentage = volume;
        }

        protected override void Update(float t)
        {
            UpdateMediaPlayerPositionQuery(World);
            UpdateAudioStreamQuery(World, t);
            UpdateVideoStreamQuery(World, t);

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
                float actualVolume = (sdkComponent.HasVolume ? sdkComponent.Volume : MediaPlayerComponent.DEFAULT_VOLUME) * worldVolumePercentage * masterVolumePercentage;
                component.MediaPlayer.UpdateVolume(sceneStateProvider.IsCurrent, sdkComponent.HasVolume, actualVolume, dt * audioFadeSpeed * actualVolume);
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
                float actualVolume = (sdkComponent.HasVolume ? sdkComponent.Volume : MediaPlayerComponent.DEFAULT_VOLUME) * worldVolumePercentage * masterVolumePercentage;
                component.MediaPlayer.UpdateVolume(sceneStateProvider.IsCurrent, sdkComponent.HasVolume, actualVolume, dt * audioFadeSpeed * actualVolume);
            }

            var address = MediaAddress.New(sdkComponent.Src!);
            if (RequiresURLChange(entity, ref component, address, sdkComponent)) return;

            HandleComponentChange(ref component, sdkComponent, address, sdkComponent.HasPlaying, sdkComponent.Playing, sdkComponent, static (mediaPlayer, sdk) => mediaPlayer.UpdatePlaybackProperties(sdk));
            ConsumePromise(ref component, false, sdkComponent, static (mediaPlayer, sdk) => mediaPlayer.SetPlaybackProperties(sdk));
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
                    && (!sceneData.TryGetMediaUrl(otherUrl, out Uri localMediaUrl) || selfUrl != localMediaUrl.OriginalString))
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

        [Query]
        [All(typeof(PBVideoPlayer))]
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

            // Handle texture update
            if (assignedTexture.Texture.Asset.HasEqualResolution(to: avText))
                Graphics.CopyTexture(avText, assignedTexture.Texture);
            else
                assignedTexture.Texture.Asset.ResizeTexture(to: avText); // will be updated on the next frame/update-loop
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
                        return !ctx.sceneData.TryGetMediaUrl(mediaAddressUrl, out Uri localMediaUrl) || componentAddress.Url != localMediaUrl.OriginalString;
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

        private static void ConsumePromise(ref MediaPlayerComponent component, bool autoPlay, PBVideoPlayer? sdkVideoComponent = null, Action<MultiMediaPlayer, PBVideoPlayer>? onOpened = null)
        {
            if (!component.OpenMediaPromise.IsResolved) return;

            if (component.OpenMediaPromise.IsReachableConsume(component.MediaAddress))
            {
                Profiler.BeginSample(component.MediaPlayer.HasControl
                    ? "MediaPlayer.OpenMedia"
                    : "MediaPlayer.InitialiseAndOpenMedia");

                try { component.MediaPlayer.OpenMedia(component.MediaAddress, component.IsFromContentServer, autoPlay); }
                finally { Profiler.EndSample(); }

                if (sdkVideoComponent != null)
                    onOpened?.Invoke(component.MediaPlayer, sdkVideoComponent);
            }
            else
            {
                component.SetState(component.MediaAddress.IsEmpty ? VideoState.VsNone : VideoState.VsError);
                Profiler.BeginSample("MediaPlayer.CloseCurrentStream");

                try { component.MediaPlayer.CloseCurrentStream(); }
                finally { Profiler.EndSample(); }
            }
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
                isValidLocalPath = sceneData.TryGetMediaUrl(url, out Uri mediaUrl);

                if (isValidLocalPath)
                    mediaAddress = MediaAddress.New(mediaUrl.OriginalString);
            }

            component.MediaAddress = mediaAddress;
            component.SetState(isValidStreamUrl || isValidLocalPath || mediaAddress.IsEmpty ? VideoState.VsNone : VideoState.VsError);
        }

        protected override void OnDispose()
        {
#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
            worldVolumeMacBus.OnWorldVolumeChanged -= OnWorldVolumeChanged;
            worldVolumeMacBus.OnMasterVolumeChanged -= OnMasterVolumeChanged;
#endif
        }
    }
}
