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
        private void UpdateAudioStream(ref MediaPlayerComponent component, PBAudioStream sdkComponent, ref VideoStateByPriorityComponent videoStateByPriority)
        {
            if (!frameTimeBudget.TrySpendBudget()) return;

            if (component.State != VideoState.VsError)
            {
                float actualVolume = (sdkComponent.HasVolume ? sdkComponent.Volume : MediaPlayerComponent.DEFAULT_VOLUME) * worldVolumePercentage * masterVolumePercentage;
                component.MediaPlayer.UpdateVolume(sceneStateProvider.IsCurrent, sdkComponent.HasVolume, actualVolume);
            }

            HandleComponentChange(ref component, sdkComponent, ref videoStateByPriority, sdkComponent.Url, sdkComponent.HasPlaying, sdkComponent.Playing);
            ConsumePromise(ref component, sdkComponent.HasPlaying && sdkComponent.Playing);
        }

        [Query]
        private void UpdateVideoStream(ref MediaPlayerComponent component, PBVideoPlayer sdkComponent, ref VideoStateByPriorityComponent videoStateByPriority)
        {
            if (!frameTimeBudget.TrySpendBudget()) return;

            if (component.State != VideoState.VsError)
            {
                float actualVolume = (sdkComponent.HasVolume ? sdkComponent.Volume : MediaPlayerComponent.DEFAULT_VOLUME) * worldVolumePercentage * masterVolumePercentage;
                component.MediaPlayer.UpdateVolume(sceneStateProvider.IsCurrent, sdkComponent.HasVolume, actualVolume);
            }

            HandleComponentChange(ref component, sdkComponent, ref videoStateByPriority, sdkComponent.Src, sdkComponent.HasPlaying, sdkComponent.Playing, sdkComponent, static (mediaPlayer, sdk) => mediaPlayer.UpdatePlaybackProperties(sdk));
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

        private void HandleComponentChange(ref MediaPlayerComponent component, IDirtyMarker sdkComponent, ref VideoStateByPriorityComponent videoStateByPriority,
            string url, bool hasPlaying, bool isPlaying,
            PBVideoPlayer sdkVideoComponent = null, Action<MediaPlayer, PBVideoPlayer> onPlaybackUpdate = null)
        {
            if (!sdkComponent.IsDirty) return;

            if (component.URL != url && (!sceneData.TryGetMediaUrl(url, out URLAddress localMediaUrl) || component.URL != localMediaUrl))
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
                UpdatePlayback(component.MediaPlayer, ref videoStateByPriority, hasPlaying, isPlaying);

                if (sdkVideoComponent != null)
                    onPlaybackUpdate?.Invoke(component.MediaPlayer, sdkVideoComponent);
            }

            sdkComponent.IsDirty = false;
        }

        public static void UpdatePlayback(in MediaPlayer mediaPlayer, ref VideoStateByPriorityComponent videoStateByPriority, bool hasPlaying, bool playing)
        {
            if (!mediaPlayer.MediaOpened) return;

            IMediaControl control = mediaPlayer.Control;

            if (hasPlaying)
            {
                if (playing != control.IsPlaying())
                {
                    if (playing)
                    {
                        control.Play();
                        videoStateByPriority.WantsToPlay = true;
                        videoStateByPriority.MediaPlayStartTime = Time.realtimeSinceStartup;
                        Debug.Log("xxx: PLAY");
                    }
                    else
                    {
                        control.Pause();
                        videoStateByPriority.WantsToPlay = false;
                        Debug.Log("xxx: PAUSE");
                    }
                }
            }
            else if (control.IsPlaying())
            {
                control.Stop();
                videoStateByPriority.WantsToPlay = false;
                Debug.Log("xxx: STOP");
            }
        }

        private static void ConsumePromise(ref MediaPlayerComponent component, bool autoPlay, PBVideoPlayer sdkVideoComponent = null, Action<MediaPlayer, PBVideoPlayer> onOpened = null)
        {
            if (!component.OpenMediaPromise.IsResolved) return;

            if (component.OpenMediaPromise.IsReachableConsume(component.URL))
            {
                //The problem is that video files coming from our content server are flagged as application/octet-stream,
                //but mac OS without a specific content type cannot play them. (more info here https://github.com/RenderHeads/UnityPlugin-AVProVideo/issues/2008 )
                //This adds a query param for video files from content server to force the correct content type
                component.MediaPlayer.OpenMedia(MediaPathType.AbsolutePathOrURL, component.IsFromContentServer ? string.Format("{0}?includeMimeType", component.URL) : component.URL, autoPlay);

                if (sdkVideoComponent != null)
                    onOpened?.Invoke(component.MediaPlayer, sdkVideoComponent);
            }
            else
            {
                component.SetState(string.IsNullOrEmpty(component.URL) ? VideoState.VsNone : VideoState.VsError);
                component.MediaPlayer.CloseCurrentStream();
            }
        }

        private void UpdateStreamUrl(ref MediaPlayerComponent component, string url)
        {
            bool isValidStreamUrl = url.IsValidUrl();
            bool isValidLocalPath = false;
            if (!isValidStreamUrl)
            {
                isValidLocalPath = sceneData.TryGetMediaUrl(url, out URLAddress mediaUrl);
                if(isValidLocalPath)
                    url = mediaUrl;
            }

            component.URL = url;
            component.SetState(isValidStreamUrl || isValidLocalPath || string.IsNullOrEmpty(url) ? VideoState.VsNone : VideoState.VsError);
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
