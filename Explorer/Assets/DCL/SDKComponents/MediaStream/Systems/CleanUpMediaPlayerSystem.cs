using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Diagnostics;
using DCL.ECSComponents;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle;
using ECS.LifeCycle.Components;
using ECS.StreamableLoading.Textures;
using ECS.Unity.Textures.Components;

namespace DCL.SDKComponents.MediaStream
{
    [UpdateInGroup(typeof(CleanUpGroup))]
    [LogCategory(ReportCategory.MEDIA_STREAM)]
    public partial class CleanUpMediaPlayerSystem : BaseUnityLoopSystem, IFinalizeWorldSystem
    {
        internal CleanUpMediaPlayerSystem(World world) : base(world) { }

        protected override void Update(float t)
        {
            HandleSdkAudioStreamComponentRemovalQuery(World);
            HandleSdkVideoPlayerComponentRemovalQuery(World);

            TryReleaseConsumerQuery(World);
            HandleTextureWithoutConsumersQuery(World);

            HandleMediaPlayerDestructionQuery(World);
        }

        [Query]
        [None(typeof(PBAudioStream), typeof(DeleteEntityIntention), typeof(VideoTextureConsumer))]
        private void HandleSdkAudioStreamComponentRemoval(Entity entity, ref MediaPlayerComponent mediaPlayer)
        {
            CleanUpMediaPlayer(ref mediaPlayer);
            World.Remove<MediaPlayerComponent>(entity);
        }

        [Query]
        [All(typeof(VideoTextureConsumer))]
        [None(typeof(PBVideoPlayer), typeof(DeleteEntityIntention))]
        private void HandleSdkVideoPlayerComponentRemoval(Entity entity, ref MediaPlayerComponent mediaPlayer)
        {
            CleanUpMediaPlayer(ref mediaPlayer);
            World.Remove<MediaPlayerComponent, VideoStateByPriorityComponent>(entity);
        }

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void HandleMediaPlayerDestruction(ref MediaPlayerComponent mediaPlayer)
        {
            CleanUpMediaPlayer(ref mediaPlayer);
        }

        private void CleanUpMediaPlayer(ref MediaPlayerComponent mediaPlayerComponent)
        {
            mediaPlayerComponent.Dispose();
        }

        public void FinalizeComponents(in Query query)
        {
            FinalizeMediaPlayerComponentQuery(World);
            FinalizeVideoTextureConsumerComponentQuery(World);
        }

        [Query]
        private void FinalizeMediaPlayerComponent(ref MediaPlayerComponent component) =>
            CleanUpMediaPlayer(ref component);

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void TryReleaseConsumer(Entity entity, ref VideoTextureConsumer textureConsumer)
        {
            CleanUpVideoTexture(ref textureConsumer);
            World.Remove<VideoTextureConsumer>(entity);
        }

        /// <summary>
        ///     Prevents CPU and memory leaks by cleaning up video textures and media players that are not being used anymore.
        /// </summary>
        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void HandleTextureWithoutConsumers(Entity entity, ref VideoTextureConsumer textureConsumer, TextureData textureData)
        {
            if (textureData.referenceCount == 0)
            {
                CleanUpVideoTexture(ref textureConsumer);
                World.Remove<VideoTextureConsumer>(entity);
            }
        }

        private void CleanUpVideoTexture(ref VideoTextureConsumer videoTextureConsumer) =>
            videoTextureConsumer.Dispose();

        [Query]
        private void FinalizeVideoTextureConsumerComponent(ref VideoTextureConsumer component) =>
            CleanUpVideoTexture(ref component);
    }
}
