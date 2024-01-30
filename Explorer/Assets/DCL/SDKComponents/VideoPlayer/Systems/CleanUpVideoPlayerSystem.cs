using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle.Components;
using ECS.Unity.Textures.Components;
using RenderHeads.Media.AVProVideo;
using UnityEngine;

namespace DCL.SDKComponents.VideoPlayer.Systems
{
    [UpdateInGroup(typeof(CleanUpGroup))]
    [LogCategory(ReportCategory.VIDEO_PLAYER)]
    public partial class CleanUpVideoPlayerSystem : BaseUnityLoopSystem
    {
        private readonly IComponentPool<MediaPlayer> mediaPlayerPool;
        private readonly IExtendedObjectPool<Texture2D> videoTexturesPool;

        private CleanUpVideoPlayerSystem(World world, IComponentPool<MediaPlayer> mediaPlayerPool, IExtendedObjectPool<Texture2D> videoTexturesPool) : base(world)
        {
            this.mediaPlayerPool = mediaPlayerPool;
            this.videoTexturesPool = videoTexturesPool;
        }

        protected override void Update(float t)
        {
            HandleSdkComponentRemovalQuery(World);
            HandleEntityDestructionQuery(World);
        }

        [Query]
        [None(typeof(PBVideoPlayer), typeof(DeleteEntityIntention))]
        private void HandleSdkComponentRemoval(ref VideoTextureComponent textureComponent, ref VideoPlayerComponent videoPlayer)
        {
            CleanUpComponents(ref textureComponent, ref videoPlayer);
        }

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void HandleEntityDestruction(ref VideoTextureComponent textureComponent, ref VideoPlayerComponent videoPlayer)
        {
            CleanUpComponents(ref textureComponent, ref videoPlayer);
        }


        private void CleanUpComponents(ref VideoTextureComponent textureComponent, ref VideoPlayerComponent videoPlayer)
        {
            var videoTexture = textureComponent.Texture;
            textureComponent.Dispose();
            videoTexturesPool.Release(videoTexture);

            videoPlayer.Dispose();
            mediaPlayerPool.Release(videoPlayer.MediaPlayer);
        }
    }
}
