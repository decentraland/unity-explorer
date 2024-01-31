using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Utilities.Extensions;
using ECS.Abstract;
using ECS.Unity.Textures.Components;
using RenderHeads.Media.AVProVideo;
using SceneRunner.Scene;
using UnityEngine;

namespace DCL.SDKComponents.MediaStream
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [LogCategory(ReportCategory.MEDIA_STREAM)]
    public partial class UpdateMediaPlayerSystem: BaseUnityLoopSystem
    {
        private readonly ISceneStateProvider sceneStateProvider;

        private UpdateMediaPlayerSystem(World world, ISceneStateProvider sceneStateProvider) : base(world)
        {
            this.sceneStateProvider = sceneStateProvider;
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

            if (sdkComponent.IsDirty && sdkComponent.Url.IsValidUrl())
            {
                UpdateStreamUrl(ref component, sdkComponent.Url);
                component.MediaPlayer.UpdatePlayback(sdkComponent.HasPlaying, sdkComponent.Playing);
            }
        }

        [Query]
        private void UpdateVideoStream(ref MediaPlayerComponent component, ref PBVideoPlayer sdkComponent)
        {
            component.MediaPlayer.UpdateVolume(sceneStateProvider.IsCurrent, sdkComponent.HasVolume, sdkComponent.Volume);

            if (sdkComponent.IsDirty && sdkComponent.Src.IsValidUrl())
            {
                UpdateStreamUrl(ref component, sdkComponent.Src);
                component.MediaPlayer.UpdatePlayback(sdkComponent.HasPlaying, sdkComponent.Playing)
                         .UpdatePlaybackProperties(sdkComponent);
            }
        }

        [Query]
        [All(typeof(PBVideoPlayer))]
        private static void UpdateVideoTexture(ref MediaPlayerComponent playerComponent, ref VideoTextureComponent assignedTexture)
        {
            if (!playerComponent.IsPlaying) return;

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
