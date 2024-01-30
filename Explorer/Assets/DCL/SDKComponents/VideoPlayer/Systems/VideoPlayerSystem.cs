using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using DCL.SDKComponents.AudioStream;
using DCL.Utilities.Extensions;
using ECS.Abstract;
using ECS.Unity.Groups;
using ECS.Unity.Textures.Components;
using RenderHeads.Media.AVProVideo;
using SceneRunner.Scene;
using System.Runtime.CompilerServices;
using UnityEngine;
using static DCL.SDKComponents.VideoPlayer.VideoPlayerComponent;

namespace DCL.SDKComponents.VideoPlayer.Systems
{
    [UpdateInGroup(typeof(ComponentInstantiationGroup))]
    [LogCategory(ReportCategory.VIDEO_PLAYER)]
    public partial class VideoPlayerSystem : BaseUnityLoopSystem
    {
        private readonly ISceneStateProvider sceneStateProvider;
        private readonly IComponentPool<MediaPlayer> mediaPlayerPool;

        private VideoPlayerSystem(World world, IComponentPool<MediaPlayer> mediaPlayerPool, ISceneStateProvider sceneStateProvider) : base(world)
        {
            this.sceneStateProvider = sceneStateProvider;
            this.mediaPlayerPool = mediaPlayerPool;
        }

        protected override void Update(float t)
        {
            CreateVideoStreamPlayerQuery(World);
            UpdateVideoStreamTextureQuery(World);
        }

        [Query]
        [None(typeof(VideoPlayerComponent))]
        [All(typeof(VideoTextureComponent))]
        private void CreateVideoStreamPlayer(in Entity entity, ref PBVideoPlayer sdkVideo)
        {
            var component = new VideoPlayerComponent(sdkVideo, mediaPlayerPool.Get());
            UpdateVolume(ref component, sdkVideo, sceneStateProvider.IsCurrent);

            World.Add(entity, component);
        }

        [Query]
        private void UpdateVideoStreamTexture(ref PBVideoPlayer sdkVideo, ref VideoPlayerComponent playerComponent, ref VideoTextureComponent assignedTexture)
        {
            UpdateVolume(ref playerComponent, sdkVideo, sceneStateProvider.IsCurrent);

            if (playerComponent.IsPlaying)
                UpdateVideoTexture(ref assignedTexture, playerComponent.MediaPlayer.TextureProducer.GetTexture());

            if (!sdkVideo.IsDirty || !sdkVideo.Src.IsValidUrl()) return;

            UpdateComponentChange(ref playerComponent, sdkVideo);

            sdkVideo.IsDirty = false;
        }

        private static void UpdateComponentChange(ref VideoPlayerComponent playerComponent, PBVideoPlayer sdkVideo)
        {
            playerComponent.MediaPlayer.Loop = sdkVideo.HasLoop && sdkVideo.Loop; // default: loop = false
            playerComponent.MediaPlayer.Control.SetPlaybackRate(sdkVideo.HasPlaybackRate ? sdkVideo.PlaybackRate : DEFAULT_PLAYBACK_RATE);
            playerComponent.MediaPlayer.Control.Seek(sdkVideo.HasPosition ? sdkVideo.Position : DEFAULT_POSITION);

            UpdateStreamUrl(ref playerComponent, sdkVideo.Src);
            UpdatePlayback(ref playerComponent, sdkVideo);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void UpdateStreamUrl(ref VideoPlayerComponent component, string newUrl)
        {
            if (component.URL == newUrl) return;

            component.URL = newUrl;
            component.MediaPlayer.CloseCurrentStream();
            component.MediaPlayer.OpenMedia(MediaPathType.AbsolutePathOrURL, newUrl, autoPlay: false);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void UpdatePlayback(ref VideoPlayerComponent component, PBVideoPlayer sdkComponent)
        {
            if (sdkComponent.HasPlaying && sdkComponent.Playing != component.MediaPlayer.Control.IsPlaying())
            {
                if (sdkComponent.Playing)
                    component.MediaPlayer.Play();
                else
                    component.MediaPlayer.Stop();
            }
        }

        private static void UpdateVideoTexture(ref VideoTextureComponent assignedTexture, Texture avText)
        {
            if (avText == null) return;

            if (assignedTexture.Texture.HasEqualResolution(to: avText))
                CopyVideoTexture(avText, assignedTexture.Texture);
            else
                ResizeVideoTexture(avText, assignedTexture.Texture); // will be updated on the next frame/update-loop
        }

        private static void CopyVideoTexture(Texture avText, Texture videoTexture)
        {
            Graphics.CopyTexture(avText, videoTexture);
        }

        private static void ResizeVideoTexture(Texture avTexture, Texture2D videoTexture)
        {
            videoTexture.Reinitialize(avTexture.width, avTexture.height);
            videoTexture.Apply();
        }

        private static void UpdateVolume(ref VideoPlayerComponent component, PBVideoPlayer sdkComponent, bool isCurrentScene)
        {
            if (isCurrentScene)
                component.MediaPlayer.AudioVolume = sdkComponent.HasVolume ? sdkComponent.Volume : DEFAULT_VOLUME;
            else
                component.MediaPlayer.AudioVolume = 0f;
        }
    }
}
