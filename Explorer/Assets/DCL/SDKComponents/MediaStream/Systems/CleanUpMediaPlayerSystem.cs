using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Diagnostics;
using DCL.ECSComponents;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle;
using ECS.LifeCycle.Components;
using ECS.Unity.Textures.Components;

namespace DCL.SDKComponents.MediaStream
{
    [UpdateInGroup(typeof(CleanUpGroup))]
    [LogCategory(ReportCategory.MEDIA_STREAM)]
    public partial class CleanUpMediaPlayerSystem : BaseUnityLoopSystem, IFinalizeWorldSystem
    {
        internal CleanUpMediaPlayerSystem(World world) : base(world) { }
        private readonly MediaPlayerCustomPool mediaPlayerPool;

        internal CleanUpMediaPlayerSystem(World world, MediaPlayerCustomPool mediaPlayerPool) : base(world)
        {
            this.mediaPlayerPool = mediaPlayerPool;
        }

        protected override void Update(float t)
        {
            HandleSdkAudioStreamComponentRemovalQuery(World);
            HandleSdkVideoPlayerComponentRemovalQuery(World);
        }

        [Query]
        [None(typeof(PBAudioStream), typeof(DeleteEntityIntention), typeof(VideoTextureConsumer))]
        private void HandleSdkAudioStreamComponentRemoval(Entity entity, ref MediaPlayerComponent mediaPlayer)
        {
            CleanUpMediaPlayer(ref mediaPlayer);
            World.Remove<MediaPlayerComponent>(entity);
        }

        [Query]
        [None(typeof(PBVideoPlayer), typeof(DeleteEntityIntention))]
        private void HandleSdkVideoPlayerComponentRemoval(Entity entity, ref MediaPlayerComponent mediaPlayer, ref VideoTextureConsumer videoTextureConsumer)
        {
            CleanUpMediaPlayer(ref mediaPlayer);
            CleanUpVideoTextureConsumer(ref videoTextureConsumer);
            World.Remove<MediaPlayerComponent>(entity);
            World.Remove<VideoStateByPriorityComponent>(entity);
        }


        private void CleanUpMediaPlayer(ref MediaPlayerComponent mediaPlayerComponent)
        {
            mediaPlayerComponent.Dispose();
        }

        private void CleanUpVideoTextureConsumer(ref VideoTextureConsumer videoTextureConsumer)
        {
            videoTextureConsumer.Dispose();
        }

        public void FinalizeComponents(in Query query)
        {
            FinalizeMediaPlayerComponentQuery(World);
        }

        [Query]
        private void FinalizeMediaPlayerComponent(ref MediaPlayerComponent component) =>
            CleanUpMediaPlayer(ref component);
    }
}
