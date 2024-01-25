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

namespace DCL.SDKComponents.VideoPlayer.Systems
{
    [UpdateInGroup(typeof(CleanUpGroup))]
    [LogCategory(ReportCategory.VIDEO_PLAYER)]
    public partial class CleanUpVideoPlayerSystem : BaseUnityLoopSystem
    {
        private readonly IComponentPool<MediaPlayer> mediaPlayerPool;

        private CleanUpVideoPlayerSystem(World world, IComponentPool<MediaPlayer> mediaPlayerPool) : base(world)
        {
            this.mediaPlayerPool = mediaPlayerPool;
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
            textureComponent.Dispose();

            videoPlayer.Dispose();
            mediaPlayerPool.Release(videoPlayer.MediaPlayer);
        }
    }
}
