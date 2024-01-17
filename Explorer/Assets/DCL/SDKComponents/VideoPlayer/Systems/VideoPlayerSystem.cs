using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Diagnostics;
using DCL.ECSComponents;
using ECS.Abstract;
using ECS.Unity.Groups;

namespace DCL.SDKComponents.VideoPlayer.Systems
{
    [UpdateInGroup(typeof(ComponentInstantiationGroup))]
    [LogCategory(ReportCategory.VIDEO_PLAYER)]
    public partial class VideoPlayerSystem : BaseUnityLoopSystem
    {
        private VideoPlayerSystem(World world) : base(world)
        {
        }

        protected override void Update(float t)
        {
            InstantiateVideoStreamQuery(World);
        }

        [Query]
        [None(typeof(VideoPlayerComponent))]
        private void InstantiateVideoStream(in Entity entity, ref PBVideoPlayer sdkVideo)
        {
            var component = new VideoPlayerComponent(sdkVideo);
            World.Add(entity, component);
        }
    }
}
