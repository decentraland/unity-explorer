using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using ECS.Abstract;
using ECS.Unity.Groups;
using RenderHeads.Media.AVProVideo;

namespace DCL.SDKComponents.VideoPlayer.Systems
{
    [UpdateInGroup(typeof(ComponentInstantiationGroup))]
    [LogCategory(ReportCategory.VIDEO_PLAYER)]
    public partial class VideoPlayerSystem : BaseUnityLoopSystem
    {
        private readonly IComponentPool<MediaPlayer> mediaPlayerPool;

        private VideoPlayerSystem(World world, IComponentPool<MediaPlayer> mediaPlayerPool) : base(world)
        {
            this.mediaPlayerPool = mediaPlayerPool;
        }

        protected override void Update(float t)
        {
            InstantiateVideoStreamQuery(World);
        }

        [Query]
        [None(typeof(VideoPlayerComponent))]
        private void InstantiateVideoStream(in Entity entity, ref PBVideoPlayer sdkVideo)
        {
            var component = new VideoPlayerComponent(sdkVideo, mediaPlayerPool.Get());
            World.Add(entity, component);
        }
    }
}
