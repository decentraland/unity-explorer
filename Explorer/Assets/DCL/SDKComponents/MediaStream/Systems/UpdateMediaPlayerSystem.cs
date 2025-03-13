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
using RenderHeads.Media.AVProVideo;
using SceneRunner.Scene;
using System;
using UnityEngine;
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
        private readonly WorldVolumeMacBus worldVolumeMacBus;
        private float worldVolumePercentage = 1f;
        private float masterVolumePercentage = 1f;

        public UpdateMediaPlayerSystem(
            World world,
            IWebRequestController webRequestController,
            ISceneData sceneData,
            ISceneStateProvider sceneStateProvider,
            IPerformanceBudget frameTimeBudget,
            WorldVolumeMacBus worldVolumeMacBus) : base(world)
        {
            this.webRequestController = webRequestController;
            this.sceneData = sceneData;
            this.sceneStateProvider = sceneStateProvider;
            this.frameTimeBudget = frameTimeBudget;
            this.worldVolumeMacBus = worldVolumeMacBus;

            //This following part is a workaround applied for the MacOS platform, the reason
            //is related to the video and audio streams, the MacOS environment does not support
            //the volume control for the video and audio streams, as it doesn’t allow to route audio
            //from HLS through to Unity. This is a limitation of Apple’s AVFoundation framework
            //Similar issue reported here https://github.com/RenderHeads/UnityPlugin-AVProVideo/issues/1086
#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
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
            UpdateAudioStreamQuery(World);
            UpdateVideoStreamQuery(World);

            UpdateVideoTextureQuery(World);
        }

        [Query]
        private void UpdateMediaPlayerPosition(ref MediaPlayerComponent mediaPlayer, ref TransformComponent transformComponent)
        {
            // Needed for positional sound
            mediaPlayer.MediaPlayer.transform.position = transformComponent.Transform.position;
        }

        [Query]
        private void UpdateAudioStream(ref MediaPlayerComponent component, PBAudioStream sdkComponent)
        {
            if (!frameTimeBudget.TrySpendBudget()) return;

            if (component.State != VideoState.VsError)
            {
                float actualVolume = (sdkComponent.HasVolume ? sdkComponent.Volume : MediaPlayerComponent.DEFAULT_VOLUME) * worldVolumePercentage * masterVolumePercentage;
                component.MediaPlayer.UpdateVolume(sceneStateProvider.IsCurrent, sdkComponent.HasVolume, actualVolume);
            }

            var address = MediaAddress.New(sdkComponent.Url!);
            HandleComponentChange(ref component, sdkComponent, address, sdkComponent.HasPlaying, sdkComponent.Playing);
            ConsumePromise(ref component, sdkComponent.HasPlaying && sdkComponent.Playing);
        }

        [Query]
        private void UpdateVideoStream(ref MediaPlayerComponent component, PBVideoPlayer sdkComponent)
        {
            if (!frameTimeBudget.TrySpendBudget()) return;

            if (component.State != VideoState.VsError)
            {
                float actualVolume = (sdkComponent.HasVolume ? sdkComponent.Volume : MediaPlayerComponent.DEFAULT_VOLUME) * worldVolumePercentage * masterVolumePercentage;
                component.MediaPlayer.UpdateVolume(sceneStateProvider.IsCurrent, sdkComponent.HasVolume, actualVolume);
            }

            var address = MediaAddress.New(sdkComponent.Src!);
            HandleComponentChange(ref component, sdkComponent, address, sdkComponent.HasPlaying, sdkComponent.Playing, sdkComponent, static (mediaPlayer, sdk) => mediaPlayer.UpdatePlaybackProperties(sdk));
            ConsumePromise(ref component, false, sdkComponent, static (mediaPlayer, sdk) => mediaPlayer.SetPlaybackProperties(sdk));
        }

        [Query]
        [All(typeof(PBVideoPlayer))]
        private void UpdateVideoTexture(ref MediaPlayerComponent playerComponent, ref VideoTextureConsumer assignedTexture)
        {
            if (!playerComponent.IsPlaying || playerComponent.State == VideoState.VsError || !playerComponent.MediaPlayer.MediaOpened)
                return;

            // Video is already playing in the background, and CopyTexture is a GPU operation,
            // so it does not make sense to budget by CPU as it can lead to much worse UX

            Texture avText = playerComponent.MediaPlayer.TextureProducer.GetTexture();
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
            Action<MediaPlayer, PBVideoPlayer>? onPlaybackUpdate = null
        )
        {
            if (!sdkComponent.IsDirty) return;

            if (mediaAddress.MediaKind is MediaAddress.Kind.LIVEKIT)
            {
                ReportHub.LogError(GetReportCategory(), "LiveKit is not implemented yet.");
                return;
            }

            if (component.MediaAddress != mediaAddress && (!sceneData.TryGetMediaUrl(mediaAddress.Url, out URLAddress localMediaUrl) || component.MediaAddress.Url != localMediaUrl))
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

        private static void ConsumePromise(ref MediaPlayerComponent component, bool autoPlay, PBVideoPlayer? sdkVideoComponent = null, Action<MediaPlayer, PBVideoPlayer>? onOpened = null)
        {
            if (!component.OpenMediaPromise.IsResolved) return;

            if (component.OpenMediaPromise.IsReachableConsume(component.MediaAddress))
            {
                switch (component.MediaAddress.MediaKind)
                {
                    case MediaAddress.Kind.URL:

                        //The problem is that video files coming from our content server are flagged as application/octet-stream,
                        //but mac OS without a specific content type cannot play them. (more info here https://github.com/RenderHeads/UnityPlugin-AVProVideo/issues/2008 )
                        //This adds a query param for video files from content server to force the correct content type
                        string url = component.MediaAddress.Url;
                        component.MediaPlayer.OpenMedia(MediaPathType.AbsolutePathOrURL, component.IsFromContentServer ? string.Format("{0}?includeMimeType", url) : url, autoPlay);

                        break;
                    case MediaAddress.Kind.LIVEKIT:
                        ReportHub.LogError(ReportCategory.MEDIA_STREAM, "LiveKit is not implemented yet.");
                        break;
                    default: throw new ArgumentOutOfRangeException();
                }

                if (sdkVideoComponent != null)
                    onOpened?.Invoke(component.MediaPlayer, sdkVideoComponent);
            }
            else
            {
                component.SetState(component.MediaAddress.IsEmpty ? VideoState.VsNone : VideoState.VsError);
                component.MediaPlayer.CloseCurrentStream();
            }
        }

        private void UpdateStreamUrl(ref MediaPlayerComponent component, MediaAddress mediaAddress)
        {
            if (component.MediaAddress.MediaKind is MediaAddress.Kind.LIVEKIT)
            {
                component.MediaAddress = mediaAddress;
                return;
            }

            string url = mediaAddress.Url;

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

        protected override void OnDispose()
        {
#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
            worldVolumeMacBus.OnWorldVolumeChanged -= OnWorldVolumeChanged;
            worldVolumeMacBus.OnMasterVolumeChanged -= OnMasterVolumeChanged;
#endif
        }
    }
}
