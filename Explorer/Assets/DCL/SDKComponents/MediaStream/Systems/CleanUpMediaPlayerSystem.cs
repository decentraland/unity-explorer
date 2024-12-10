using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle;
using ECS.LifeCycle.Components;
using ECS.Unity.Textures.Components;
using RenderHeads.Media.AVProVideo;
using System;
using UnityEngine;

namespace DCL.SDKComponents.MediaStream
{
    [UpdateInGroup(typeof(CleanUpGroup))]
    [LogCategory(ReportCategory.MEDIA_STREAM)]
    public partial class CleanUpMediaPlayerSystem : BaseUnityLoopSystem, IFinalizeWorldSystem
    {
        private readonly IComponentPool<MediaPlayer> mediaPlayerPool;
        private readonly IExtendedObjectPool<Texture2D> videoTexturesPool;

        internal CleanUpMediaPlayerSystem(World world, IComponentPool<MediaPlayer> mediaPlayerPool, IExtendedObjectPool<Texture2D> videoTexturesPool) : base(world)
        {
            this.mediaPlayerPool = mediaPlayerPool;
            this.videoTexturesPool = videoTexturesPool;
        }

        protected override void Update(float t)
        {
            HandleSdkAudioStreamComponentRemovalQuery(World);
            HandleSdkVideoPlayerComponentRemovalQuery(World);

            HandleMediaPlayerDestructionQuery(World);
            HandleVideoEntityDestructionQuery(World);

            HandleVideoPlayerWithoutConsumersQuery(World);
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
        private void HandleSdkVideoPlayerComponentRemoval(Entity entity, ref VideoTextureConsumer textureConsumer, ref MediaPlayerComponent mediaPlayer)
        {
            CleanUpVideoTexture(ref textureConsumer);
            CleanUpMediaPlayer(ref mediaPlayer);
            World.Remove<MediaPlayerComponent, VideoTextureConsumer>(entity);
            World.Remove<VideoStateByPriorityComponent>(entity);
        }

        /// <summary>
        ///     Prevents CPU and memory leaks by cleaning up video textures and media players that are not being used anymore.
        /// </summary>
        [Query]
        [All(typeof(PBVideoPlayer))]
        [None(typeof(DeleteEntityIntention))]
        private void HandleVideoPlayerWithoutConsumers(Entity entity, ref VideoTextureConsumer textureConsumer, ref MediaPlayerComponent mediaPlayerComponent)
        {
            if (textureConsumer.ConsumersCount == 0)
            {
                CleanUpVideoTexture(ref textureConsumer);
                CleanUpMediaPlayer(ref mediaPlayerComponent);
                World.Remove<MediaPlayerComponent, VideoTextureConsumer>(entity);
                World.Remove<VideoStateByPriorityComponent>(entity);
            }
        }

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void HandleMediaPlayerDestruction(ref MediaPlayerComponent mediaPlayer)
        {
            CleanUpMediaPlayer(ref mediaPlayer);
        }

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void HandleVideoEntityDestruction(ref VideoTextureConsumer textureConsumer)
        {
            CleanUpVideoTexture(ref textureConsumer);
        }

        private void CleanUpVideoTexture(ref VideoTextureConsumer videoTextureConsumer)
        {
            videoTexturesPool.Release(videoTextureConsumer.Texture);
            videoTextureConsumer.Dispose();
        }

        private void CleanUpMediaPlayer(ref MediaPlayerComponent mediaPlayerComponent)
        {
            mediaPlayerPool.Release(mediaPlayerComponent.MediaPlayer);
            mediaPlayerComponent.Dispose();
        }

        public void FinalizeComponents(in Query query)
        {
            FinalizeVideoTextureConsumerComponentQuery(World);
            FinalizeMediaPlayerComponentQuery(World);
        }

        [Query]
        private void FinalizeVideoTextureConsumerComponent(ref VideoTextureConsumer component) =>
            CleanUpVideoTexture(ref component);

        [Query]
        private void FinalizeMediaPlayerComponent(ref MediaPlayerComponent component) =>
            CleanUpMediaPlayer(ref component);
    }
}
