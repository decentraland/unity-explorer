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
            UpdateAudioStreamQuery(World);
            UpdateVideoStreamQuery(World);

            UpdateVideoTextureQuery(World);
        }

        [Query]
        private void UpdateAudioStream(ref MediaPlayerComponent component, PBAudioStream sdkComponent)
        {
            if (!frameTimeBudget.TrySpendBudget()) return;

            if (component.State != VideoState.VsError)
            {
                float actualVolume = sdkComponent.Volume * worldVolumePercentage * masterVolumePercentage;
                component.MediaPlayer.UpdateVolume(sceneStateProvider.IsCurrent, sdkComponent.HasVolume, actualVolume);
            }

            HandleComponentChange(ref component, sdkComponent, sdkComponent.Url, sdkComponent.HasPlaying, sdkComponent.Playing);
            ConsumePromise(ref component, sdkComponent.HasPlaying && sdkComponent.Playing);
        }

        [Query]
        private void UpdateVideoStream(ref MediaPlayerComponent component, PBVideoPlayer sdkComponent)
        {
            if (!frameTimeBudget.TrySpendBudget()) return;

            if (component.State != VideoState.VsError)
            {
                float actualVolume = sdkComponent.Volume * worldVolumePercentage * masterVolumePercentage;
                component.MediaPlayer.UpdateVolume(sceneStateProvider.IsCurrent, sdkComponent.HasVolume, actualVolume);
            }

            HandleComponentChange(ref component, sdkComponent, sdkComponent.Src, sdkComponent.HasPlaying, sdkComponent.Playing, sdkComponent, static (mediaPlayer, sdk) => mediaPlayer.UpdatePlaybackProperties(sdk));
            ConsumePromise(ref component, sdkComponent.HasPlaying && sdkComponent.Playing, sdkComponent, static (mediaPlayer, sdk) => mediaPlayer.SetPlaybackProperties(sdk));
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

        private void HandleComponentChange(ref MediaPlayerComponent component, IDirtyMarker sdkComponent, string url, bool hasPlaying, bool isPlaying,
            PBVideoPlayer sdkVideoComponent = null, Action<MediaPlayer, PBVideoPlayer> onPlaybackUpdate = null)
        {
            if (!sdkComponent.IsDirty) return;

            sceneData.TryGetMediaUrl(url, out URLAddress localMediaUrl, out string mediaFileHash);

            if (component.URL != url && component.URL != localMediaUrl)
            {
                component.MediaPlayer.CloseCurrentStream();

                UpdateStreamUrl(ref component, url);

                if (component.State != VideoState.VsError)
                {
                    component.Cts = component.Cts.SafeRestart();
                    component.OpenMediaPromise.UrlReachabilityResolveAsync(webRequestController, component.URL, GetReportData(), component.Cts.Token).Forget();
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

        private static void ConsumePromise(ref MediaPlayerComponent component, bool autoPlay, PBVideoPlayer sdkVideoComponent = null, Action<MediaPlayer, PBVideoPlayer> onOpened = null)
        {
            if (!component.OpenMediaPromise.IsResolved) return;

            if (component.OpenMediaPromise.IsReachableConsume(component.URL))
            {
                component.MediaPlayer.OpenMedia(MediaPathType.AbsolutePathOrURL, component.URL, autoPlay);

                if (sdkVideoComponent != null)
                    onOpened?.Invoke(component.MediaPlayer, sdkVideoComponent);
            }
            else
            {
                component.State = VideoState.VsError;
                component.MediaPlayer.CloseCurrentStream();
            }
        }

        private void UpdateStreamUrl(ref MediaPlayerComponent component, string url)
        {
            component.MediaPlayer.CloseCurrentStream();

            if (!url.IsValidUrl() && sceneData.TryGetMediaUrl(url, out URLAddress mediaUrl, out string mediaFileHash))
                url = mediaUrl;

            component.URL = url;
            component.State = url.IsValidUrl() ? VideoState.VsNone : VideoState.VsError;
        }

        public override void Dispose()
        {
            base.Dispose();

#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
            worldVolumeMacBus.OnWorldVolumeChanged -= OnWorldVolumeChanged;
            worldVolumeMacBus.OnMasterVolumeChanged -= OnMasterVolumeChanged;
#endif
        }
    }
}
