using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using ECS.Abstract;
using ECS.Unity.Groups;
using RenderHeads.Media.AVProVideo;
using SceneRunner.Scene;
using UnityEngine;

namespace DCL.SDKComponents.VideoPlayer.Systems
{
    [UpdateInGroup(typeof(ComponentInstantiationGroup))]
    [LogCategory(ReportCategory.VIDEO_PLAYER)]
    public partial class VideoPlayerSystem : BaseUnityLoopSystem
    {
        // Refs VideoPlayerHandler, AvProVideoPlayer : IVideoPlayer, VideoPluginWrapper_AVPro, DCLVideoTexture
        private readonly ISceneStateProvider sceneStateProvider;
        private readonly IComponentPool<MediaPlayer> mediaPlayerPool;

        private VideoPlayerSystem(World world, IComponentPool<MediaPlayer> mediaPlayerPool, ISceneStateProvider sceneStateProvider) : base(world)
        {
            this.sceneStateProvider = sceneStateProvider;
            this.mediaPlayerPool = mediaPlayerPool;
        }

        protected override void Update(float t)
        {
            InstantiateVideoStreamQuery(World);
            UpdateVideoStreamTextureQuery(World);
        }

        [Query]
        [None(typeof(VideoPlayerComponent))]
        [All(typeof(VideoTextureComponent))]
        private void InstantiateVideoStream(in Entity entity, ref PBVideoPlayer sdkVideo)
        {
            var component = new VideoPlayerComponent(sdkVideo, mediaPlayerPool.Get());
            UpdateVolume(ref component, sdkVideo, sceneStateProvider.IsCurrent);

            World.Add(entity, component);
        }

        [Query]
        private void UpdateVideoStreamTexture(ref PBVideoPlayer sdkVideo, ref VideoPlayerComponent mediaPlayer, ref VideoTextureComponent assignedTexture)
        {
            UpdateVolume(ref mediaPlayer, sdkVideo, sceneStateProvider.IsCurrent);
            UpdateVideoTexture(ref assignedTexture, mediaPlayer.MediaPlayer.TextureProducer.GetTexture());
        }

        private static void UpdateVideoTexture(ref VideoTextureComponent assignedTexture, Texture avText)
        {
            if (avText == null) return;

            if (assignedTexture.texture.HasEqualResolution(to: avText))
                UpdateVideoTexture(avText, assignedTexture.texture);
            else
                ResizeVideoTexture(avText, assignedTexture.texture); // will be updated on the next frame/update-loop
        }

        private static void UpdateVideoTexture(Texture avText, Texture2D videoTexture)
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
                component.MediaPlayer.AudioVolume = sdkComponent.HasVolume ? sdkComponent.Volume : VideoPlayerComponent.DEFAULT_VOLUME;
            else
                component.MediaPlayer.AudioVolume = 0f;
        }
    }
}
