using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using Arch.SystemGroups.Throttling;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Utilities.Extensions;
using ECS.Abstract;
using ECS.Groups;
using ECS.Unity.Textures.Components;
using RenderHeads.Media.AVProVideo;
using SceneRunner.Scene;
using UnityEngine;

namespace DCL.SDKComponents.MediaStream
{
    [UpdateInGroup(typeof(SyncedPresentationSystemGroup))]
    [LogCategory(ReportCategory.MEDIA_STREAM)]
    [ThrottlingEnabled]
    public partial class UpdateMediaPlayerSystem: BaseUnityLoopSystem
    {
        private readonly ISceneStateProvider sceneStateProvider;
        private readonly IPerformanceBudget frameTimeBudget;

        public UpdateMediaPlayerSystem(World world, ISceneStateProvider sceneStateProvider, IPerformanceBudget frameTimeBudget) : base(world)
        {
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
        private void UpdateAudioStream(ref MediaPlayerComponent component, ref PBAudioStream sdkComponent)
        {
            component.MediaPlayer.UpdateVolume(sceneStateProvider.IsCurrent, sdkComponent.HasVolume, sdkComponent.Volume);

            if (sdkComponent.IsDirty && sdkComponent.Url.IsValidUrl() && frameTimeBudget.TrySpendBudget())
            {
                UpdateStreamUrl(ref component, sdkComponent.Url);
                component.MediaPlayer.UpdatePlayback(sdkComponent.HasPlaying, sdkComponent.Playing);

                sdkComponent.IsDirty = false;
            }
        }

        [Query]
        private void UpdateVideoStream(ref MediaPlayerComponent component, ref PBVideoPlayer sdkComponent)
        {
            if (component.State != VideoState.VsError)
                component.MediaPlayer.UpdateVolume(sceneStateProvider.IsCurrent, sdkComponent.HasVolume, sdkComponent.Volume);

            if (sdkComponent.IsDirty && sdkComponent.Src.IsValidUrl() && frameTimeBudget.TrySpendBudget())
            {
                UpdateStreamUrl(ref component, sdkComponent.Src);

                component.MediaPlayer.UpdatePlayback(sdkComponent.HasPlaying, sdkComponent.Playing)
                         .UpdatePlaybackProperties(sdkComponent);

                sdkComponent.IsDirty = false;
            }
        }

        [Query]
        [All(typeof(PBVideoPlayer))]
        private void UpdateVideoTexture(ref MediaPlayerComponent playerComponent, ref VideoTextureComponent assignedTexture)
        {
            if (!playerComponent.IsPlaying || !frameTimeBudget.TrySpendBudget()) return;

            Texture avText = playerComponent.MediaPlayer.TextureProducer.GetTexture();
            if (avText == null) return;

            // Handle texture update
            if (assignedTexture.Texture.HasEqualResolution(to: avText))
                Graphics.CopyTexture(avText, assignedTexture.Texture);
            else
                assignedTexture.Texture.ResizeTexture(to: avText); // will be updated on the next frame/update-loop
        }

        private static void UpdateStreamUrl(ref MediaPlayerComponent component, string newUrl)
        {
            if (component.URL == newUrl) return;

            component.URL = newUrl;
            component.MediaPlayer.CloseCurrentStream();
            component.MediaPlayer.OpenMedia(MediaPathType.AbsolutePathOrURL, newUrl, autoPlay: false);
        }
    }
}
