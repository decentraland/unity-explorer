using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle.Components;
using RenderHeads.Media.AVProVideo;

namespace DCL.SDKComponents.AudioStream
{
    [UpdateInGroup(typeof(CleanUpGroup))]
    [LogCategory(ReportCategory.AUDIO_STREAM)]
    public partial class CleanUpAudioStreamSystem : BaseUnityLoopSystem
    {
        private readonly IComponentPool<MediaPlayer> mediaPlayerPool;

        private CleanUpAudioStreamSystem(World world, IComponentPool<MediaPlayer> mediaPlayerPool) : base(world)
        {
            this.mediaPlayerPool = mediaPlayerPool;
        }

        protected override void Update(float t)
        {
            HandleSdkComponentRemovalQuery(World);
        }

        [Query]
        [None(typeof(PBAudioStream), typeof(DeleteEntityIntention))]
        private void HandleSdkComponentRemoval(ref AudioStreamComponent component)
        {
            component.Dispose();
            mediaPlayerPool.Release(component.MediaPlayer);
        }
    }
}
