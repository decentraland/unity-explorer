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

namespace DCL.SDKComponents.MediaStream
{
    [UpdateInGroup(typeof(CleanUpGroup))]
    [LogCategory(ReportCategory.MEDIA_STREAM)]
    public partial class CleanUpMediaPlayerSystem : BaseUnityLoopSystem
    {
        private readonly IComponentPool<MediaPlayer> mediaPlayerPool;
        private readonly IExtendedObjectPool<Texture2D> videoTexturesPool;

        private CleanUpMediaPlayerSystem(World world, IComponentPool<MediaPlayer> mediaPlayerPool, IExtendedObjectPool<Texture2D> videoTexturesPool) : base(world)
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
        }

        [Query]
        [None(typeof(PBAudioStream), typeof(DeleteEntityIntention), typeof(VideoTextureComponent))]
        private void HandleSdkAudioStreamComponentRemoval(Entity entity, ref MediaPlayerComponent mediaPlayer)
        {
            CleanUpMediaPlayer(ref mediaPlayer);
            World.Remove<MediaPlayerComponent>(entity);
        }

        [Query]
        [None(typeof(PBVideoPlayer), typeof(DeleteEntityIntention))]
        private void HandleSdkVideoPlayerComponentRemoval(Entity entity, ref VideoTextureComponent textureComponent, ref MediaPlayerComponent mediaPlayer)
        {
            CleanUpVideoTexture(ref textureComponent);
            CleanUpMediaPlayer(ref mediaPlayer);
            World.Remove<MediaPlayerComponent, VideoTextureComponent>(entity);
        }

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void HandleMediaPlayerDestruction(ref MediaPlayerComponent mediaPlayer)
        {
            CleanUpMediaPlayer(ref mediaPlayer);
        }

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void HandleVideoEntityDestruction(ref VideoTextureComponent textureComponent)
        {
            CleanUpVideoTexture(ref textureComponent);
        }

        private void CleanUpVideoTexture(ref VideoTextureComponent videoTextureComponent)
        {
            videoTexturesPool.Release(videoTextureComponent.Texture);
            videoTextureComponent.Dispose();
        }

        private void CleanUpMediaPlayer(ref MediaPlayerComponent mediaPlayerComponent)
        {
            mediaPlayerPool.Release(mediaPlayerComponent.MediaPlayer);
            mediaPlayerComponent.Dispose();
        }
    }
}
