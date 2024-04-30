using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Optimization.PerformanceBudgeting;
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

        public UpdateMediaPlayerSystem(World world, IWebRequestController webRequestController, ISceneData sceneData, ISceneStateProvider sceneStateProvider, IPerformanceBudget frameTimeBudget) : base(world)
        {
            this.webRequestController = webRequestController;
            this.sceneData = sceneData;
            this.sceneStateProvider = sceneStateProvider;
            this.frameTimeBudget = frameTimeBudget;
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
                component.MediaPlayer.UpdateVolume(sceneStateProvider.IsCurrent, sdkComponent.HasVolume, sdkComponent.Volume);

            HandleComponentChange(ref component, sdkComponent, sdkComponent.Url, sdkComponent.HasPlaying, sdkComponent.Playing);
            ConsumePromise(ref component, sdkComponent.HasPlaying && sdkComponent.Playing);
        }

        [Query]
        private void UpdateVideoStream(ref MediaPlayerComponent component, PBVideoPlayer sdkComponent)
        {
            if (!frameTimeBudget.TrySpendBudget()) return;

            if (component.State != VideoState.VsError)
                component.MediaPlayer.UpdateVolume(sceneStateProvider.IsCurrent, sdkComponent.HasVolume, sdkComponent.Volume);

            HandleComponentChange(ref component, sdkComponent, sdkComponent.Src, sdkComponent.HasPlaying, sdkComponent.Playing, OnPlaybackUpdate);
            ConsumePromise(ref component, sdkComponent.HasPlaying && sdkComponent.Playing, OnMediaOpened);

            return;

            void OnMediaOpened(MediaPlayer mediaPlayer) =>
                mediaPlayer.SetPlaybackProperties(sdkComponent);

            void OnPlaybackUpdate(MediaPlayer mediaPlayer) =>
                mediaPlayer.UpdatePlaybackProperties(sdkComponent);
        }

        [Query]
        [All(typeof(PBVideoPlayer))]
        private void UpdateVideoTexture(ref MediaPlayerComponent playerComponent, ref VideoTextureComponent assignedTexture)
        {
            if (!playerComponent.IsPlaying || playerComponent.State == VideoState.VsError || !playerComponent.MediaPlayer.MediaOpened || !frameTimeBudget.TrySpendBudget())
                return;

            Texture avText = playerComponent.MediaPlayer.TextureProducer.GetTexture();
            if (avText == null) return;

            // Handle texture update
            if (assignedTexture.Texture.HasEqualResolution(to: avText))
                Graphics.CopyTexture(avText, assignedTexture.Texture);
            else
                assignedTexture.Texture.ResizeTexture(to: avText); // will be updated on the next frame/update-loop
        }

        private void HandleComponentChange(ref MediaPlayerComponent component, IDirtyMarker sdkComponent, string url, bool hasPlaying, bool isPlaying,
            Action<MediaPlayer> onPlaybackUpdate = null)
        {
            if (!sdkComponent.IsDirty) return;

            if (component.URL != url)
            {
                component.MediaPlayer.CloseCurrentStream();

                UpdateStreamUrl(ref component, url);

                if (component.State != VideoState.VsError)
                {
                    component.Cts = component.Cts.SafeRestart();
                    component.OpenMediaPromise.UrlReachabilityResolveAsync(webRequestController, component.URL, component.Cts.Token).Forget();
                }
            }
            else if (component.State != VideoState.VsError)
            {
                component.MediaPlayer.UpdatePlayback(hasPlaying, isPlaying);
                onPlaybackUpdate?.Invoke(component.MediaPlayer);
            }

            sdkComponent.IsDirty = false;
        }

        private static void ConsumePromise(ref MediaPlayerComponent component, bool autoPlay, Action<MediaPlayer> onOpened = null)
        {
            if (!component.OpenMediaPromise.IsResolved) return;

            if (component.OpenMediaPromise.IsReachableConsume(component.URL))
            {
                component.MediaPlayer.OpenMedia(MediaPathType.AbsolutePathOrURL, component.URL, autoPlay);
                onOpened?.Invoke(component.MediaPlayer);
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

            if (!url.IsValidUrl() && sceneData.TryGetMediaUrl(url, out URLAddress mediaUrl))
                url = mediaUrl;

            component.URL = url;
            component.State = url.IsValidUrl() ? VideoState.VsNone : VideoState.VsError;
        }
    }
}
