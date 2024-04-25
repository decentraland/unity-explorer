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
using System.Threading;
using UnityEngine;

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
            UpdateMediaStream(ref component, sdkComponent, sdkComponent.Url, sdkComponent.HasVolume, sdkComponent.Volume, sdkComponent.HasPlaying, sdkComponent.Playing, OnComplete);
            return;

            void OnComplete(MediaPlayer mediaPlayer) => mediaPlayer.UpdatePlayback(sdkComponent.HasPlaying, sdkComponent.Playing);
        }

        [Query]
        private void UpdateVideoStream(ref MediaPlayerComponent component, PBVideoPlayer sdkComponent)
        {
            UpdateMediaStream(ref component, sdkComponent, sdkComponent.Src, sdkComponent.HasVolume, sdkComponent.Volume, sdkComponent.HasPlaying, sdkComponent.Playing, OnComplete);
            return;

            void OnComplete(MediaPlayer mediaPlayer) =>
                mediaPlayer.UpdatePlayback(sdkComponent.HasPlaying, sdkComponent.Playing)
                           .UpdatePlaybackProperties(sdkComponent);
        }

        private void UpdateMediaStream(ref MediaPlayerComponent component, IDirtyMarker marker, string url, bool hasVolume, float volume, bool hasPlaying, bool playing, Action<MediaPlayer> onComplete)
        {
            if (component.State != VideoState.VsError)
                component.MediaPlayer.UpdateVolume(sceneStateProvider.IsCurrent, hasVolume, volume);

            if (marker.IsDirty && frameTimeBudget.TrySpendBudget())
            {
                MediaPlayer mediaPlayer = component.MediaPlayer;

                if (component.URL == url)
                {
                    if (component.State != VideoState.VsError)
                        mediaPlayer.UpdatePlayback(hasPlaying, playing);
                }
                else if (UpdateStreamUrl(ref component, url) != VideoState.VsError)
                {
                    component.Cts.Cancel();
                    component.Cts = new CancellationTokenSource();
                    mediaPlayer.OpenMediaIfReachableAsync(webRequestController, component.URL, autoPlay: false, component.Cts.Token, OnComplete).Forget();

                    void OnComplete() => onComplete?.Invoke(mediaPlayer);
                }

                marker.IsDirty = false;
            }
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

        private VideoState UpdateStreamUrl(ref MediaPlayerComponent component, string url)
        {
            component.MediaPlayer.CloseCurrentStream();

            if (!url.IsValidUrl() && sceneData.TryGetMediaUrl(url, out URLAddress mediaUrl))
                url = mediaUrl;

            component.URL = url;
            component.State = url.IsValidUrl() ? VideoState.VsNone : VideoState.VsError;

            return component.State;
        }
    }
}
