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

            HandleSdkComponentRemovalQuery(World);
            HandleEntityDestructionQuery(World);
        }

        [Query]
        [None(typeof(PBAudioStream), typeof(DeleteEntityIntention), typeof(VideoTextureComponent))]
        private void HandleSdkAudioStreamComponentRemoval(ref MediaPlayerComponent mediaPlayer)
        {
            CleanUpMediaPlayer(ref mediaPlayer);
        }

        [Query]
        [None(typeof(PBVideoPlayer), typeof(DeleteEntityIntention))]
        private void HandleSdkComponentRemoval(ref VideoTextureComponent textureComponent, ref MediaPlayerComponent mediaPlayer)
        {
            CleanUpVideoTexture(ref textureComponent);
            CleanUpMediaPlayer(ref mediaPlayer);
        }

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void HandleEntityDestruction(ref VideoTextureComponent textureComponent, ref MediaPlayerComponent mediaPlayer)
        {
            CleanUpVideoTexture(ref textureComponent);
            CleanUpMediaPlayer(ref mediaPlayer);
        }

        private void CleanUpVideoTexture(ref VideoTextureComponent textureComponent)
        {
            var videoTexture = textureComponent.Texture;
            textureComponent.Dispose();
            videoTexturesPool.Release(videoTexture);
        }

        private void CleanUpMediaPlayer(ref MediaPlayerComponent mediaPlayer)
        {
            mediaPlayer.Dispose();
            mediaPlayerPool.Release(mediaPlayer.MediaPlayer);
        }
    }
}
