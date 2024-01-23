using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using ECS.Abstract;
using ECS.Unity.Groups;
using RenderHeads.Media.AVProVideo;
using UnityEngine;

namespace DCL.SDKComponents.VideoPlayer.Systems
{
    [UpdateInGroup(typeof(ComponentInstantiationGroup))]
    [LogCategory(ReportCategory.VIDEO_PLAYER)]
    public partial class VideoPlayerSystem : BaseUnityLoopSystem
    {
        // Refs VideoPlayerHandler, AvProVideoPlayer : IVideoPlayer, VideoPluginWrapper_AVPro, DCLVideoTexture
        private readonly IComponentPool<MediaPlayer> mediaPlayerPool;

        private VideoPlayerSystem(World world, IComponentPool<MediaPlayer> mediaPlayerPool) : base(world)
        {
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
            World.Add(entity, component);
        }

        [Query]
        private void UpdateVideoStreamTexture(ref VideoPlayerComponent mediaPlayer, ref VideoTextureComponent assignedTexture)
        {
            Texture avText = mediaPlayer.mediaPlayer.TextureProducer.GetTexture();
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
    }
}
